using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TcgEngine.Client;
using TcgEngine.Gameplay;
using DG.Tweening;

namespace TcgEngine.UI
{
    /// <summary>
    /// 2-frame reveal overlay for live ball cards.
    /// Frame 1: YOUR CARD (quick). Frame 2: OPPONENT'S CARD (suspense).
    /// Cloned from PlayCallRevealPanel pattern: self-building ScreenSpaceOverlay.
    /// Triggered by GameFeedbackUI when both live ball cards are visible.
    /// </summary>
    public class LiveBallRevealPanel : MonoBehaviour
    {
        // ── Colors (match PlayCallRevealPanel) ──
        static readonly Color COL_MINT      = new Color32(170, 255, 220, 255);
        static readonly Color COL_CORAL     = new Color32(255, 113, 108, 255);
        static readonly Color COL_BG        = new Color32(14, 14, 14, 255);
        static readonly Color COL_DIM_WHITE = new Color32(173, 170, 170, 255);
        static readonly Color COL_GOLD      = new Color32(255, 215, 80, 255);

        // ── Timing ──
        [Header("Timing")]
        public float frame1FadeIn  = 0.3f;
        public float frame1Hold    = 0.8f;
        public float frame2FadeIn  = 0.35f;
        public float frame2Hold    = 2.0f;   // suspense
        public float finalFadeOut  = 0.5f;

        // ── Built refs ──
        private Canvas overlayCanvas;
        private CanvasGroup rootGroup;
        private GameObject rootPanel;

        // Frame 1: Your Card
        private CanvasGroup frame1Group;
        private TextMeshProUGUI frame1Label;
        private TextMeshProUGUI frame1CardName;
        private TextMeshProUGUI frame1AbilityText;
        private TextMeshProUGUI frame1SlotReqs;

        // Frame 2: Opponent's Card
        private CanvasGroup frame2Group;
        private TextMeshProUGUI frame2Label;
        private TextMeshProUGUI frame2CardName;
        private TextMeshProUGUI frame2AbilityText;
        private TextMeshProUGUI frame2SlotReqs;

        private Coroutine activeCoroutine;
        private bool selfBuilt;
        private bool isShowing;

        // ── Singleton ──
        private static LiveBallRevealPanel instance;
        public static LiveBallRevealPanel Get() => instance;

        void Awake()
        {
            instance = this;
            BuildUI();
            if (rootPanel != null) rootPanel.SetActive(false);
        }

        /// <summary>Show the 2-frame live ball reveal.</summary>
        public void ShowReveal(Card localCard, Card opponentCard, bool localIsOffense)
        {
            ShowReveal(localCard?.Data, opponentCard?.Data, localIsOffense);
        }

        /// <summary>CardData overload: driven by the LiveBall Reveal event (ids only, no state race).</summary>
        public void ShowReveal(CardData localCard, CardData opponentCard, bool localIsOffense)
        {
            if (isShowing) return;
            if (localCard == null && opponentCard == null) return;
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);
            activeCoroutine = StartCoroutine(RunRevealSequence(localCard, opponentCard, localIsOffense));
        }

        public void Hide()
        {
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);
            activeCoroutine = null;
            isShowing = false;
            if (rootPanel != null) rootPanel.SetActive(false);
        }

        public bool IsShowing => isShowing;

        // =====================================================================
        // Reveal coroutine
        // =====================================================================
        private IEnumerator RunRevealSequence(CardData localCard, CardData opponentCard, bool localIsOffense)
        {
            isShowing = true;

            // Populate text
            PopulateFrame(frame1CardName, frame1AbilityText, frame1SlotReqs, frame1Label,
                localCard, localIsOffense ? "YOUR LIVE BALL" : "YOUR LIVE BALL",
                localIsOffense ? COL_MINT : COL_CORAL);

            PopulateFrame(frame2CardName, frame2AbilityText, frame2SlotReqs, frame2Label,
                opponentCard, "OPPONENT'S CARD",
                localIsOffense ? COL_CORAL : COL_MINT);

            // Reset
            rootPanel.SetActive(true);
            rootGroup.alpha = 1f;
            frame1Group.alpha = 0f;
            frame2Group.alpha = 0f;

            // ── Frame 1: Your Card ──
            frame1Group.DOFade(1f, frame1FadeIn).SetEase(Ease.OutQuad).SetLink(gameObject);
            yield return new WaitForSeconds(frame1FadeIn + frame1Hold);

            // ── Frame 2: Opponent's Card (suspense) ──
            frame1Group.DOFade(0.3f, frame2FadeIn).SetEase(Ease.InQuad).SetLink(gameObject);
            frame2Group.DOFade(1f, frame2FadeIn).SetEase(Ease.OutQuad).SetLink(gameObject);
            yield return new WaitForSeconds(frame2FadeIn + frame2Hold);

            // ── Fade out ──
            rootGroup.DOFade(0f, finalFadeOut).SetEase(Ease.InQuad).SetLink(gameObject);
            yield return new WaitForSeconds(finalFadeOut);

            rootPanel.SetActive(false);
            isShowing = false;
            activeCoroutine = null;
        }

        // =====================================================================
        // Data population
        // =====================================================================
        private void PopulateFrame(TextMeshProUGUI nameTmp, TextMeshProUGUI abilityTmp,
            TextMeshProUGUI slotTmp, TextMeshProUGUI labelTmp,
            CardData card, string label, Color accentColor)
        {
            labelTmp.text = label;
            labelTmp.color = accentColor;

            if (card == null)
            {
                nameTmp.text = "NO CARD";
                nameTmp.color = COL_DIM_WHITE;
                abilityTmp.text = "No card played";
                abilityTmp.color = COL_DIM_WHITE;
                slotTmp.text = "";
                return;
            }

            nameTmp.text = (!string.IsNullOrEmpty(card.title) ? card.title : card.id)?.ToUpper() ?? "UNKNOWN";
            nameTmp.color = accentColor;

            // Ability text
            if (card.abilities != null && card.abilities.Length > 0 && card.abilities[0] != null
                && !string.IsNullOrEmpty(card.abilities[0].desc))
                abilityTmp.text = card.abilities[0].desc;
            else
                abilityTmp.text = card.text ?? "";
            abilityTmp.color = Color.white;

            // Slot requirements
            if (card.slotRequirements != null && card.slotRequirements.Length > 0)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var req in card.slotRequirements)
                    sb.Append($"{req.requiredCount}x {req.icon}  ");
                slotTmp.text = sb.ToString().TrimEnd();
                slotTmp.color = COL_GOLD;
            }
            else
            {
                slotTmp.text = ""; // no cost = say nothing
            }
        }

        // =====================================================================
        // Self-contained UI builder
        // =====================================================================
        private void BuildUI()
        {
            if (selfBuilt) return;
            selfBuilt = true;

            // ── Canvas ──
            overlayCanvas = gameObject.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 210; // above LiveBallCardZone (200) — the reveal was rendering under its dim
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // ── Root panel ──
            rootPanel = new GameObject("LiveBallRevealRoot");
            rootPanel.transform.SetParent(transform, false);
            rootGroup = rootPanel.AddComponent<CanvasGroup>();
            rootGroup.blocksRaycasts = false;

            Image rootBg = rootPanel.AddComponent<Image>();
            rootBg.color = new Color(COL_BG.r, COL_BG.g, COL_BG.b, 0.9f);
            StretchFill(rootPanel);

            try
            {
                // ── Frame 1: YOUR CARD (left half) ──
                frame1Group = BuildCardFrame(rootPanel.transform, "Frame1_YourCard",
                    new Vector2(0.06f, 0.12f), new Vector2(0.48f, 0.88f),
                    COL_MINT, out frame1Label, out frame1CardName, out frame1AbilityText, out frame1SlotReqs);

                // ── Frame 2: OPPONENT'S CARD (right half) ──
                frame2Group = BuildCardFrame(rootPanel.transform, "Frame2_OppCard",
                    new Vector2(0.52f, 0.12f), new Vector2(0.94f, 0.88f),
                    COL_CORAL, out frame2Label, out frame2CardName, out frame2AbilityText, out frame2SlotReqs);
            }
            finally
            {
                rootPanel.SetActive(false);
            }
        }

        private CanvasGroup BuildCardFrame(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color accentColor,
            out TextMeshProUGUI labelTmp, out TextMeshProUGUI nameTmp,
            out TextMeshProUGUI abilityTmp, out TextMeshProUGUI slotTmp)
        {
            GameObject frame = new GameObject(name);
            frame.transform.SetParent(parent, false);
            Image frameBg = frame.AddComponent<Image>();
            frameBg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.14f);
            CanvasGroup cg = frame.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            SetAnchors(frame, anchorMin, anchorMax);

            // Left border strip (4px)
            GameObject border = new GameObject("LeftBorder");
            border.transform.SetParent(frame.transform, false);
            Image bImg = border.AddComponent<Image>();
            bImg.color = accentColor;
            SetAnchors(border, new Vector2(0f, 0f), new Vector2(0f, 1f));
            border.GetComponent<RectTransform>().sizeDelta = new Vector2(8f, 0f);

            // Label tag
            GameObject labelBg = new GameObject("LabelBg");
            labelBg.transform.SetParent(frame.transform, false);
            Image lbImg = labelBg.AddComponent<Image>();
            lbImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.2f);
            SetAnchors(labelBg, new Vector2(0.08f, 0.80f), new Vector2(0.62f, 0.88f));

            labelTmp = MakeTMP(frame.transform, "Label", 26f, accentColor,
                new Vector2(0.10f, 0.80f), new Vector2(0.61f, 0.88f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            labelTmp.characterSpacing = 6f;

            // Card name (large)
            nameTmp = MakeTMP(frame.transform, "CardName", 88f, accentColor,
                new Vector2(0.08f, 0.52f), new Vector2(0.92f, 0.78f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            nameTmp.enableAutoSizing = true;
            nameTmp.fontSizeMin = 40f;
            nameTmp.fontSizeMax = 88f;

            // Ability text
            abilityTmp = MakeTMP(frame.transform, "AbilityText", 36f,
                Color.white,
                new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.52f),
                TextAlignmentOptions.TopLeft, FontStyles.Normal);
            abilityTmp.enableWordWrapping = true;

            // Slot requirements
            slotTmp = MakeTMP(frame.transform, "SlotReqs", 26f, COL_GOLD,
                new Vector2(0.08f, 0.14f), new Vector2(0.92f, 0.26f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            slotTmp.characterSpacing = 2f;

            return cg;
        }

        // =====================================================================
        // Helpers
        // =====================================================================
        private static void StretchFill(GameObject go)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void SetAnchors(GameObject go, Vector2 min, Vector2 max)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private TextMeshProUGUI MakeTMP(Transform parent, string name, float size, Color color,
            Vector2 anchorMin, Vector2 anchorMax, TextAlignmentOptions alignment,
            FontStyles style = FontStyles.Normal)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.color = color;
            t.alignment = alignment;
            t.fontStyle = style;
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Truncate;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return t;
        }
    }
}
