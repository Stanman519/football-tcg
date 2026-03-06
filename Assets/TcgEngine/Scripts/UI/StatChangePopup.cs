using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine.UI
{
    /// <summary>
    /// Spawns floating "+5 run" / "+3 short cov" labels off board cards
    /// whenever a card's stat-bonus status values change.
    ///
    /// Works via diff detection on onRefreshAll — compares each board card's
    /// six stat-bonus status values against cached values from the previous
    /// refresh. Any increase spawns an animated popup at that card's position.
    ///
    /// No Inspector wiring required. Attach to GameFeedbackUI's GameObject
    /// or any persistent GameObject in the Game scene.
    /// </summary>
    public class StatChangePopup : MonoBehaviour
    {
        [Header("Settings")]
        public float floatDistance = 110f;     // px upward travel
        public float floatDuration = 1.4f;     // seconds for full arc
        public float fontSize = 17f;

        // Per-card, per-stat cache: card_uid → (StatusType → last known value)
        private readonly Dictionary<string, int[]> cachedValues = new Dictionary<string, int[]>();

        // Ordered stat types and labels (must stay in sync — index matters)
        private static readonly StatusType[] TrackedStats =
        {
            StatusType.AddedRunBonus,
            StatusType.AddedShortPassBonus,
            StatusType.AddedDeepPassBonus,
            StatusType.AddedRunCoverageBonus,
            StatusType.AddedShortPassCoverageBonus,
            StatusType.AddedDeepPassCoverageBonus,
        };
        private static readonly string[] StatLabels =
        {
            "run",
            "short",
            "deep",
            "run cov",
            "sht cov",
            "deep cov",
        };
        // true = offensive bonus (green), false = defensive bonus (red)
        private static readonly bool[] IsOffensive =
        {
            true, true, true,
            false, false, false,
        };

        private Canvas overlayCanvas;

        void Awake()
        {
            // Ensure we have a screen-space canvas for the labels
            overlayCanvas = gameObject.GetComponent<Canvas>();
            if (overlayCanvas == null)
            {
                overlayCanvas = gameObject.AddComponent<Canvas>();
                overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                overlayCanvas.sortingOrder = 56;
                gameObject.AddComponent<CanvasScaler>();
            }
        }

        void Start()
        {
            GameClient.Get().onRefreshAll += OnRefreshAll;
        }

        void OnDestroy()
        {
            if (GameClient.Get() != null)
                GameClient.Get().onRefreshAll -= OnRefreshAll;
        }

        private void OnRefreshAll()
        {
            Game g = GameClient.Get()?.GetGameData();
            if (g == null || g.players == null) return;

            // Walk every board card for every player
            foreach (Player p in g.players)
            {
                if (p == null) continue;
                foreach (Card card in p.cards_board)
                {
                    if (card == null) continue;
                    CheckCardForChanges(card);
                }
            }

            // Clear cached uids that are no longer on any board (cards removed/discarded)
            CleanStaleCache(g);
        }

        private void CheckCardForChanges(Card card)
        {
            if (!cachedValues.TryGetValue(card.uid, out int[] prev))
            {
                // First time seeing this card — seed the cache, no popup
                cachedValues[card.uid] = ReadStatValues(card);
                return;
            }

            int[] current = ReadStatValues(card);
            for (int i = 0; i < TrackedStats.Length; i++)
            {
                int delta = current[i] - prev[i];
                if (delta == 0) continue;

                // Find the BoardCard MonoBehaviour to get world position
                BoardCard bc = FindBoardCard(card.uid);
                if (bc != null)
                    SpawnPopup(bc.transform.position, delta, i);
            }

            cachedValues[card.uid] = current;
        }

        private int[] ReadStatValues(Card card)
        {
            var vals = new int[TrackedStats.Length];
            for (int i = 0; i < TrackedStats.Length; i++)
                vals[i] = card.GetStatusValue(TrackedStats[i]);
            return vals;
        }

        private void SpawnPopup(Vector3 worldPos, int delta, int statIndex)
        {
            string label = delta > 0
                ? $"+{delta} {StatLabels[statIndex]}"
                : $"{delta} {StatLabels[statIndex]}";

            Color color = IsOffensive[statIndex]
                ? new Color(0.3f, 1f, 0.45f)    // green — offense gain
                : new Color(1f, 0.45f, 0.45f);  // red   — defense gain

            // Convert world position to screen position
            if (Camera.main == null) return;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            if (screenPos.z < 0) return; // behind camera

            StartCoroutine(AnimatePopup(screenPos, label, color));
        }

        private IEnumerator AnimatePopup(Vector3 startScreenPos, string label, Color color)
        {
            // Create label GameObject on the overlay canvas
            GameObject go = new GameObject("StatPopup");
            go.transform.SetParent(overlayCanvas.transform, false);

            Text t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = Mathf.RoundToInt(fontSize);
            t.color = color;
            t.text = label;
            t.alignment = TextAnchor.MiddleCenter;
            t.supportRichText = false;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(140f, 30f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.position = startScreenPos;

            // Shadow for readability
            Shadow shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.8f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            float elapsed = 0f;
            Vector3 targetPos = startScreenPos + new Vector3(0, floatDistance, 0);
            Color startColor = color;

            while (elapsed < floatDuration)
            {
                float progress = elapsed / floatDuration;
                // Ease-out upward float
                float easedY = Mathf.Lerp(startScreenPos.y, targetPos.y, 1f - (1f - progress) * (1f - progress));
                rt.position = new Vector3(startScreenPos.x, easedY, 0f);

                // Fade: hold full alpha for first 40%, then fade out
                float alpha = progress < 0.4f ? 1f : Mathf.Lerp(1f, 0f, (progress - 0.4f) / 0.6f);
                t.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

                elapsed += Time.deltaTime;
                yield return null;
            }

            Destroy(go);
        }

        private static BoardCard FindBoardCard(string uid)
        {
            foreach (BoardCard bc in BoardCard.GetAll())
            {
                if (bc != null && bc.GetCardUID() == uid)
                    return bc;
            }
            return null;
        }

        private void CleanStaleCache(Game g)
        {
            var activeUids = new HashSet<string>();
            foreach (Player p in g.players)
            {
                if (p == null) continue;
                foreach (Card c in p.cards_board)
                    if (c != null) activeUids.Add(c.uid);
            }

            var toRemove = new List<string>();
            foreach (string uid in cachedValues.Keys)
                if (!activeUids.Contains(uid)) toRemove.Add(uid);
            foreach (string uid in toRemove)
                cachedValues.Remove(uid);
        }
    }
}
