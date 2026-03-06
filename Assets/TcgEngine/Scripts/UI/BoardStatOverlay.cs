using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine.UI
{
    /// <summary>
    /// Shows per-card bonus labels floating above board cards during the Resolution phase.
    /// Labels display each card's contribution to the current play (e.g. "+8 run", "-4 cov").
    /// Fades out when phase leaves Resolution.
    ///
    /// No Inspector wiring required — builds label pool at runtime.
    /// Attach to a GameObject in the Game scene (or to GameFeedbackUI's GameObject).
    /// Called each frame via Update (needed for live WorldToScreenPoint tracking).
    /// </summary>
    public class BoardStatOverlay : MonoBehaviour
    {
        [Header("Settings")]
        public float labelYOffset = 80f;        // px above card center
        public float fadeDuration = 1.0f;

        private Canvas overlayCanvas;
        private readonly List<StatLabel> labelPool = new List<StatLabel>();

        // Per-label data pairing
        private class StatLabel
        {
            public GameObject root;
            public Text text;
            public CanvasGroup group;
            public BoardCard trackedCard;       // null when free in pool
        }

        private GamePhase prevPhase = GamePhase.None;
        private Coroutine fadeCoroutine;
        private bool labelsVisible = false;

        void Awake()
        {
            BuildCanvas();
        }

        /// <summary>Called by GameFeedbackUI on each data refresh.</summary>
        public void Refresh(Game g, Player p0, Player p1)
        {
            if (g == null) return;

            bool inResolution = g.phase == GamePhase.Resolution;
            bool justEnteredResolution = inResolution && prevPhase != GamePhase.Resolution;
            bool justLeftResolution = !inResolution && prevPhase == GamePhase.Resolution;

            if (justEnteredResolution)
                ShowLabels(g, p0, p1);

            if (justLeftResolution)
                StartFadeOut();

            prevPhase = g.phase;
        }

        void Update()
        {
            // Track card positions every frame while labels are showing
            if (!labelsVisible) return;
            if (Camera.main == null) return;

            foreach (var lbl in labelPool)
            {
                if (lbl.trackedCard == null) continue;
                Vector3 screenPos = Camera.main.WorldToScreenPoint(lbl.trackedCard.transform.position);
                screenPos.y += labelYOffset;
                lbl.root.transform.position = screenPos;
            }
        }

        // -----------------------------------------------------------------------

        private void ShowLabels(Game g, Player p0, Player p1)
        {
            if (fadeCoroutine != null) { StopCoroutine(fadeCoroutine); fadeCoroutine = null; }
            ReturnAllToPool();

            bool p0isOff = g.current_offensive_player?.player_id == 0;

            foreach (var bc in BoardCard.GetAll())
            {
                Card card = bc.GetCard();
                if (card == null || card.Data == null) continue;

                // Determine which player owns this card
                bool isOffCard = p0isOff ? card.player_id == 0 : card.player_id == 1;

                string labelText = BuildLabel(card.Data, isOffCard, g.last_play_type);
                if (string.IsNullOrEmpty(labelText)) continue;

                StatLabel lbl = GetOrCreateLabel();
                lbl.trackedCard = bc;
                lbl.text.text = labelText;
                lbl.text.color = isOffCard
                    ? new Color(0.3f, 1f, 0.4f)    // green for offense
                    : new Color(1f, 0.4f, 0.4f);    // red for defense
                lbl.group.alpha = 1f;
                lbl.root.SetActive(true);
            }

            labelsVisible = true;
        }

        private string BuildLabel(CardData data, bool isOffensive, PlayType playType)
        {
            if (isOffensive)
            {
                int val = playType switch
                {
                    PlayType.Run => data.run_bonus,
                    PlayType.ShortPass => data.short_pass_bonus,
                    PlayType.LongPass => data.deep_pass_bonus,
                    _ => 0
                };
                string type = playType switch
                {
                    PlayType.Run => "run",
                    PlayType.ShortPass => "short",
                    PlayType.LongPass => "deep",
                    _ => ""
                };
                return val != 0 ? $"+{val} {type}" : "";
            }
            else
            {
                int val = playType switch
                {
                    PlayType.Run => data.run_coverage_bonus,
                    PlayType.ShortPass => data.short_pass_coverage_bonus,
                    PlayType.LongPass => data.deep_pass_coverage_bonus,
                    _ => 0
                };
                string type = playType switch
                {
                    PlayType.Run => "run cov",
                    PlayType.ShortPass => "sht cov",
                    PlayType.LongPass => "deep cov",
                    _ => ""
                };
                return val != 0 ? $"-{val} {type}" : "";
            }
        }

        private void StartFadeOut()
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeOutLabels());
        }

        private IEnumerator FadeOutLabels()
        {
            float elapsed = 0f;
            // Snapshot current alphas (all 1f)
            while (elapsed < fadeDuration)
            {
                float a = 1f - (elapsed / fadeDuration);
                foreach (var lbl in labelPool)
                    if (lbl.trackedCard != null) lbl.group.alpha = a;
                elapsed += Time.deltaTime;
                yield return null;
            }
            ReturnAllToPool();
            labelsVisible = false;
            fadeCoroutine = null;
        }

        private void ReturnAllToPool()
        {
            foreach (var lbl in labelPool)
            {
                lbl.trackedCard = null;
                lbl.root.SetActive(false);
            }
        }

        // -----------------------------------------------------------------------
        // Label pool
        // -----------------------------------------------------------------------

        private StatLabel GetOrCreateLabel()
        {
            // Find a free (inactive) label in pool
            foreach (var lbl in labelPool)
            {
                if (lbl.trackedCard == null)
                    return lbl;
            }
            // Create new
            return CreateLabel();
        }

        private StatLabel CreateLabel()
        {
            GameObject root = new GameObject("StatLabel");
            root.transform.SetParent(overlayCanvas.transform, false);

            CanvasGroup cg = root.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            // Background pill
            Image bg = root.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.72f);

            RectTransform rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(110f, 28f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            // Text child
            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(root.transform, false);
            Text t = textGo.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 14;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.supportRichText = false;
            RectTransform trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;

            root.SetActive(false);

            var lbl = new StatLabel { root = root, text = t, group = cg, trackedCard = null };
            labelPool.Add(lbl);
            return lbl;
        }

        private void BuildCanvas()
        {
            overlayCanvas = gameObject.GetComponent<Canvas>();
            if (overlayCanvas == null)
            {
                overlayCanvas = gameObject.AddComponent<Canvas>();
                overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                overlayCanvas.sortingOrder = 55;
                gameObject.AddComponent<CanvasScaler>();
                // No raycaster — labels don't need interaction
            }
        }
    }
}
