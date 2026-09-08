using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TcgEngine.Gameplay;
using DG.Tweening;

namespace TcgEngine.UI
{
    /// <summary>
    /// Post-play yardage breakdown overlay. Shows after ball animation completes.
    /// Self-building ScreenSpaceOverlay (same pattern as PlayCallRevealPanel).
    /// Triggered by PlayAnimationController at end of ResolutionSequence.
    /// </summary>
    public class YardageSummaryPanel : MonoBehaviour
    {
        static readonly Color COL_BG        = new Color32(14, 14, 14, 255);
        static readonly Color COL_GREEN     = new Color32(100, 255, 160, 255);
        static readonly Color COL_RED       = new Color32(255, 100, 100, 255);
        static readonly Color COL_GOLD      = new Color32(255, 215, 80, 255);
        static readonly Color COL_DIM       = new Color32(160, 160, 160, 255);
        static readonly Color COL_WHITE     = new Color32(230, 230, 230, 255);
        static readonly Color COL_CYAN      = new Color32(0, 227, 253, 255);

        [Header("Timing")]
        public float fadeInDur   = 0.4f;
        public float holdDur     = 3.0f;
        public float fadeOutDur  = 0.5f;

        private Canvas overlayCanvas;
        private CanvasGroup rootGroup;
        private GameObject rootPanel;

        // Text elements
        private TextMeshProUGUI headerText;
        private TextMeshProUGUI netYardageText;
        private TextMeshProUGUI breakdownText;
        private TextMeshProUGUI receiverText;
        private TextMeshProUGUI tackleText;
        private TextMeshProUGUI coverageText;
        private TextMeshProUGUI failEventText;

        private Coroutine activeCoroutine;
        private bool selfBuilt;
        private bool isShowing;

        private static YardageSummaryPanel instance;
        public static YardageSummaryPanel Get() => instance;

        void Awake()
        {
            instance = this;
            BuildUI();
            if (rootPanel != null) rootPanel.SetActive(false);
        }

        public void ShowSummary(Game g)
        {
            if (isShowing || g == null) return;
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);
            activeCoroutine = StartCoroutine(RunSummary(g));
        }

        public void Hide()
        {
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);
            activeCoroutine = null;
            isShowing = false;
            if (rootPanel != null) rootPanel.SetActive(false);
        }

        private IEnumerator RunSummary(Game g)
        {
            isShowing = true;
            Populate(g);

            rootPanel.SetActive(true);
            rootGroup.alpha = 0f;

            rootGroup.DOFade(1f, fadeInDur).SetEase(Ease.OutQuad).SetLink(gameObject);
            yield return new WaitForSeconds(fadeInDur + holdDur);

            rootGroup.DOFade(0f, fadeOutDur).SetEase(Ease.InQuad).SetLink(gameObject);
            yield return new WaitForSeconds(fadeOutDur);

            rootPanel.SetActive(false);
            isShowing = false;
            activeCoroutine = null;
        }

        private void Populate(Game g)
        {
            var bd = g.yardage_breakdown;
            PlayType pt = g.last_play_type;
            int net = g.yardage_this_play;
            bool isPass = pt == PlayType.ShortPass || pt == PlayType.LongPass;

            // Header
            string playLabel = pt switch
            {
                PlayType.Run => "RUN",
                PlayType.ShortPass => "SHORT PASS",
                PlayType.LongPass => "LONG PASS",
                _ => "PLAY"
            };
            headerText.text = playLabel;

            // Net yardage (big number)
            string sign = net >= 0 ? "+" : "";
            netYardageText.text = $"{sign}{net} YDS";
            netYardageText.color = net >= 0 ? COL_GREEN : COL_RED;

            // Breakdown
            if (bd != null)
            {
                var sb = new StringBuilder();
                if (isPass)
                {
                    sb.AppendLine($"Receiver Base      {FormatSigned(bd.offPlayerBase)}");
                    sb.AppendLine($"Receiver Bonus     {FormatSigned(bd.offStatusBonuses)}");
                    sb.AppendLine($"QB / OL            {FormatSigned(bd.offOtherPositions)}");
                }
                else
                {
                    sb.AppendLine($"Player Run Base    {FormatSigned(bd.offPlayerBase)}");
                    sb.AppendLine($"Run Bonuses        {FormatSigned(bd.offStatusBonuses)}");
                }
                sb.AppendLine($"Coach              {FormatSigned(bd.offCoachBase)}");
                sb.AppendLine($"Def Coverage       {FormatSigned(-bd.defCoverageBase)}");
                if (bd.defStatusBonuses != 0)
                    sb.AppendLine($"Def Bonuses        {FormatSigned(-bd.defStatusBonuses)}");
                sb.AppendLine($"Def Coach          {FormatSigned(-bd.defCoachBase)}");
                if (bd.defCoverageMod != 0)
                    sb.AppendLine($"Coverage Guess     {FormatSigned(-bd.defCoverageMod)}");
                breakdownText.text = sb.ToString().TrimEnd();
            }
            else
            {
                breakdownText.text = "";
            }

            // Receiver / carrier
            if (bd != null && !string.IsNullOrEmpty(bd.receiverName))
            {
                string role = isPass ? "Receiver" : "Ball Carrier";
                receiverText.text = $"{role}: {bd.receiverName}";
                receiverText.color = COL_CYAN;
            }
            else
            {
                receiverText.text = "";
            }

            // Who made the stop (null on TD / incomplete / turnover)
            string tacklerName = CardTitle(g, g.tackler_uid);
            tackleText.text = string.IsNullOrEmpty(tacklerName) ? "" : $"Tackle: {tacklerName}";

            // Coverage shift — who took the top receiver away
            if (bd != null && !string.IsNullOrEmpty(bd.ctrDefenderName))
            {
                string covered = CardTitle(g, g.ctr_covered_receiver_uid);
                coverageText.text = string.IsNullOrEmpty(covered)
                    ? $"Coverage: {bd.ctrDefenderName}"
                    : $"Coverage: {bd.ctrDefenderName} on {covered}";
            }
            else
            {
                coverageText.text = "";
            }

            // Fail event
            var fe = g.last_fail_event;
            if (fe != FailPlayEventType.None)
            {
                failEventText.text = fe switch
                {
                    FailPlayEventType.Sack => "SACK",
                    FailPlayEventType.QBFumble => "QB FUMBLE",
                    FailPlayEventType.RunnerFumble => "FUMBLE",
                    FailPlayEventType.Interception => "INTERCEPTION",
                    FailPlayEventType.IncompletePass => "INCOMPLETE",
                    FailPlayEventType.BattedPass => "BATTED PASS",
                    FailPlayEventType.TippedPass => "TIPPED PASS",
                    _ => fe.ToString().ToUpper()
                };
                failEventText.color = COL_RED;
            }
            else
            {
                failEventText.text = "";
            }
        }

        private static string FormatSigned(int val)
        {
            return val >= 0 ? $"+{val}" : $"{val}";
        }

        private static string CardTitle(Game g, string uid)
        {
            if (g == null || string.IsNullOrEmpty(uid)) return "";
            return g.GetCard(uid)?.CardData?.title ?? "";
        }

        // =====================================================================
        // Self-contained UI builder
        // =====================================================================
        private void BuildUI()
        {
            if (selfBuilt) return;
            selfBuilt = true;

            overlayCanvas = gameObject.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 75;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // Root panel (right-side card, not full screen)
            rootPanel = new GameObject("YardageSummaryRoot");
            rootPanel.transform.SetParent(transform, false);
            rootGroup = rootPanel.AddComponent<CanvasGroup>();
            rootGroup.blocksRaycasts = false;

            Image rootBg = rootPanel.AddComponent<Image>();
            rootBg.color = new Color(COL_BG.r, COL_BG.g, COL_BG.b, 0.88f);
            // Left side: the mini slot machine lives at (0.72-0.98 x) and must stay readable
            SetAnchors(rootPanel, new Vector2(0.04f, 0.15f), new Vector2(0.38f, 0.85f));

            // Left accent border
            GameObject border = new GameObject("LeftBorder");
            border.transform.SetParent(rootPanel.transform, false);
            Image bImg = border.AddComponent<Image>();
            bImg.color = COL_GOLD;
            SetAnchors(border, new Vector2(0f, 0f), new Vector2(0f, 1f));
            border.GetComponent<RectTransform>().sizeDelta = new Vector2(4f, 0f);

            // Header (play type label)
            headerText = MakeTMP(rootPanel.transform, "Header", 14f, COL_GOLD,
                new Vector2(0.08f, 0.90f), new Vector2(0.5f, 0.96f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            headerText.characterSpacing = 8f;

            // Net yardage (big)
            netYardageText = MakeTMP(rootPanel.transform, "NetYardage", 56f, COL_GREEN,
                new Vector2(0.08f, 0.76f), new Vector2(0.92f, 0.90f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            netYardageText.enableAutoSizing = true;
            netYardageText.fontSizeMin = 36f;
            netYardageText.fontSizeMax = 56f;

            // Separator
            GameObject sep = new GameObject("Separator");
            sep.transform.SetParent(rootPanel.transform, false);
            Image sepImg = sep.AddComponent<Image>();
            sepImg.color = new Color(1f, 1f, 1f, 0.15f);
            SetAnchors(sep, new Vector2(0.08f, 0.74f), new Vector2(0.92f, 0.74f));
            sep.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 2f);

            // Breakdown (monospace-style rows)
            breakdownText = MakeTMP(rootPanel.transform, "Breakdown", 15f, COL_WHITE,
                new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.74f),
                TextAlignmentOptions.TopLeft, FontStyles.Normal);
            breakdownText.lineSpacing = 8f;

            // Receiver / ball carrier
            receiverText = MakeTMP(rootPanel.transform, "Receiver", 16f, COL_CYAN,
                new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.35f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            // Who made the stop
            tackleText = MakeTMP(rootPanel.transform, "Tackle", 15f, COL_DIM,
                new Vector2(0.08f, 0.21f), new Vector2(0.92f, 0.28f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Normal);

            // Coverage shift
            coverageText = MakeTMP(rootPanel.transform, "Coverage", 15f, COL_DIM,
                new Vector2(0.08f, 0.14f), new Vector2(0.92f, 0.21f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Normal);

            // Fail event
            failEventText = MakeTMP(rootPanel.transform, "FailEvent", 20f, COL_RED,
                new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.13f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            rootPanel.SetActive(false);
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
