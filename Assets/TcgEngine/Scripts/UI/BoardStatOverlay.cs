using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using TcgEngine.Gameplay;
using DG.Tweening;

namespace TcgEngine.UI
{
    /// <summary>
    /// Shows per-card bonus labels floating above board cards during the Resolution phase.
    /// Supports both instant (legacy) and sequential reveal modes.
    /// Sequential mode: cards revealed one-by-one in position-group order with camera nudge.
    /// </summary>
    public class BoardStatOverlay : MonoBehaviour
    {
        [Header("Settings")]
        public float labelYOffset = 80f;
        public float fadeDuration = 2.5f;

        [Header("Sequential Reveal")]
        public float totalRevealCap = 4f;   // whole reveal (labels + hold) fits in this
        public float minPerCard = 0.25f;
        public float maxPerCard = 0.5f;
        public float fadeInDuration = 0.15f;
        public float punchScale = 0.1f;
        public float punchDuration = 0.2f;
        public float holdAllDuration = 1.0f;
        public float cameraFocusDuration = 1.3f;

        private Canvas overlayCanvas;
        private readonly List<StatLabel> labelPool = new List<StatLabel>();

        private class StatLabel
        {
            public GameObject root;
            public Text text;
            public CanvasGroup group;
            public BoardCard trackedCard;
        }

        private GamePhase prevPhase = GamePhase.None;
        private Coroutine fadeCoroutine;
        private Coroutine sequentialCoroutine;
        private bool labelsVisible = false;
        private bool sequentialMode = false;

        void Awake()
        {
            BuildCanvas();
        }

        /// <summary>Called by GameFeedbackUI on each data refresh.</summary>
        public void Refresh(Game g, Player p0, Player p1)
        {
            if (g == null) return;

            // Skip auto-show if sequential mode is active (PlayAnimationController drives it)
            if (sequentialMode) { prevPhase = g.phase; return; }

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

        // ── Sequential Reveal (Phase A) ──────────────────────

        /// <summary>
        /// Coroutine: reveal labels one-by-one sorted by position group.
        /// Offense: OL→QB→RB_TE→WR, then defense: DL→LB→DB.
        /// Camera nudges toward each card briefly.
        /// </summary>
        public IEnumerator ShowLabelsSequential(Game g, Player p0, Player p1)
        {
            if (fadeCoroutine != null) { StopCoroutine(fadeCoroutine); fadeCoroutine = null; }
            ReturnAllToPool();
            sequentialMode = true;
            labelsVisible = true;

            bool p0isOff = g.current_offensive_player?.player_id == 0;

            // Build sorted card entries
            var entries = new List<(BoardCard bc, string text, bool isOff, int sortKey)>();
            foreach (var bc in BoardCard.GetAll())
            {
                Card card = bc.GetCard();
                if (card == null || card.Data == null) continue;

                bool isOffCard = p0isOff ? card.player_id == 0 : card.player_id == 1;
                string labelText = BuildLabel(card.Data, isOffCard, g.last_play_type);
                if (string.IsNullOrEmpty(labelText)) continue;

                int sortKey = PositionGroupSortKey(card.Data.playerPosition, isOffCard);
                entries.Add((bc, labelText, isOffCard, sortKey));
            }

            entries.Sort((a, b) => a.sortKey.CompareTo(b.sortKey));

            // Pace to fit the cap regardless of label count
            float perCard = Mathf.Clamp(
                (totalRevealCap - holdAllDuration) / Mathf.Max(1, entries.Count),
                minPerCard, maxPerCard);

            GameCamera cam = GameCamera.Get();

            // Single wide focus instead of a per-card camera tour
            if (cam != null && entries.Count > 0)
                cam.FocusOn(entries[0].bc.transform.position, 0.95f, cameraFocusDuration);

            foreach (var entry in entries)
            {
                StatLabel lbl = GetOrCreateLabel();
                lbl.trackedCard = entry.bc;
                lbl.text.text = entry.text;
                lbl.text.color = entry.isOff
                    ? new Color(0.3f, 1f, 0.4f)
                    : new Color(1f, 0.4f, 0.4f);
                lbl.group.alpha = 0f;
                lbl.root.SetActive(true);

                // Fade in
                lbl.group.DOKill();
                lbl.group.DOFade(1f, fadeInDuration).SetLink(lbl.root);

                // Scale punch on board card
                entry.bc.transform.DOKill();
                entry.bc.transform.DOPunchScale(Vector3.one * punchScale, punchDuration, 6, 0.5f)
                    .SetLink(entry.bc.gameObject);

                yield return TimeController.Instance != null
                    ? TimeController.Instance.ScaledWait(perCard)
                    : new WaitForSeconds(perCard);
            }

            // Release camera after all shown
            if (cam != null)
                cam.ReleaseFocus(0.3f);

            // Hold all labels visible
            yield return TimeController.Instance != null
                ? TimeController.Instance.ScaledWait(holdAllDuration)
                : new WaitForSeconds(holdAllDuration);

            sequentialMode = false;
        }

        /// <summary>Fade out all labels (called externally or on phase exit).</summary>
        public void StartFadeOut()
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeOutLabels());
        }

        // ── Position Group Sort ──────────────────────────────

        private static int PositionGroupSortKey(PlayerPositionGrp grp, bool isOffense)
        {
            if (isOffense)
            {
                return grp switch
                {
                    PlayerPositionGrp.OL => 0,
                    PlayerPositionGrp.QB => 1,
                    PlayerPositionGrp.RB_TE => 2,
                    PlayerPositionGrp.WR => 3,
                    _ => 4
                };
            }
            else
            {
                return grp switch
                {
                    PlayerPositionGrp.DL => 10,
                    PlayerPositionGrp.LB => 11,
                    PlayerPositionGrp.DB => 12,
                    _ => 13
                };
            }
        }

        // ── Legacy Instant Show ──────────────────────────────

        private void ShowLabels(Game g, Player p0, Player p1)
        {
            if (fadeCoroutine != null) { StopCoroutine(fadeCoroutine); fadeCoroutine = null; }
            ReturnAllToPool();

            bool p0isOff = g.current_offensive_player?.player_id == 0;

            foreach (var bc in BoardCard.GetAll())
            {
                Card card = bc.GetCard();
                if (card == null || card.Data == null) continue;

                bool isOffCard = p0isOff ? card.player_id == 0 : card.player_id == 1;
                string labelText = BuildLabel(card.Data, isOffCard, g.last_play_type);
                if (string.IsNullOrEmpty(labelText)) continue;

                StatLabel lbl = GetOrCreateLabel();
                lbl.trackedCard = bc;
                lbl.text.text = labelText;
                lbl.text.color = isOffCard
                    ? new Color(0.3f, 1f, 0.4f)
                    : new Color(1f, 0.4f, 0.4f);
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

        // ── Fade ─────────────────────────────────────────────

        private IEnumerator FadeOutLabels()
        {
            float elapsed = 0f;
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

        // ── Label Pool ───────────────────────────────────────

        private StatLabel GetOrCreateLabel()
        {
            foreach (var lbl in labelPool)
            {
                if (lbl.trackedCard == null)
                    return lbl;
            }
            return CreateLabel();
        }

        private StatLabel CreateLabel()
        {
            GameObject root = new GameObject("StatLabel");
            root.transform.SetParent(overlayCanvas.transform, false);

            CanvasGroup cg = root.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            Image bg = root.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.72f);

            RectTransform rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(110f, 28f);
            rt.pivot = new Vector2(0.5f, 0.5f);

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
            }
        }
    }
}
