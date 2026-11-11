using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TcgEngine.Gameplay;
using DG.Tweening;
using static TcgEngine.UI.UITheme;

namespace TcgEngine.UI
{
    /// <summary>
    /// Post-play yardage breakdown overlay. Shows after ball animation completes.
    /// Self-building ScreenSpaceOverlay (same pattern as PlayCallRevealPanel).
    /// Triggered by PlayAnimationController at end of ResolutionSequence.
    /// </summary>
    public class YardageSummaryPanel : MonoBehaviour
    {
        static readonly Color COL_BG        = UITheme.Bg;
        static readonly Color COL_GREEN     = UITheme.Positive;
        static readonly Color COL_RED       = UITheme.Negative;
        static readonly Color COL_GOLD      = UITheme.Gold;
        static readonly Color COL_DIM       = UITheme.TextDim;
        static readonly Color COL_WHITE     = UITheme.TextMain;
        static readonly Color COL_CYAN      = UITheme.Accent;

        [Header("Timing")]
        public float fadeInDur   = 0.4f;
        public float holdDur     = 2.2f;   // after the total lands
        public float fadeOutDur  = 0.5f;
        public float rowStagger  = 0.3f;   // one ledger row at a time, total last

        public bool IsShowing => isShowing;
        private int rowCount;

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

            // Ledger reveal: rows tick in one by one, THEN the total pops — reads as "this + this - that = result"
            breakdownText.maxVisibleLines = 0;
            netYardageText.alpha = 0f;
            SetFooterAlpha(0f);

            rootGroup.DOFade(1f, fadeInDur).SetEase(Ease.OutQuad).SetLink(gameObject);
            yield return new WaitForSeconds(fadeInDur);

            for (int i = 1; i <= rowCount; i++)
            {
                breakdownText.maxVisibleLines = i;
                yield return new WaitForSeconds(rowStagger);
            }

            netYardageText.alpha = 1f;
            netYardageText.transform.DOKill();
            netYardageText.transform.localScale = Vector3.one;
            netYardageText.transform.DOPunchScale(Vector3.one * 0.18f, 0.35f, 5, 0.5f).SetLink(gameObject);
            SetFooterAlpha(1f);
            yield return new WaitForSeconds(holdDur);

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

            // Breakdown — skipped on fail events (sack/incomplete/turnover: the math didn't decide the play)
            rowCount = 0;
            if (bd != null && g.last_fail_event == FailPlayEventType.None)
            {
                var sb = new StringBuilder();
                if (isPass)
                {
                    Row(sb, "Receiver", bd.offPlayerBase, true);
                    Row(sb, "Receiver bonus", bd.offStatusBonuses, false);
                    Row(sb, "QB / OL", bd.offOtherPositions, false);
                }
                else
                {
                    Row(sb, "Runner", bd.offPlayerBase, true);
                    Row(sb, "Run bonuses", bd.offStatusBonuses, false);
                }
                Row(sb, "Coach", bd.offCoachBase, false);
                Row(sb, "Def coverage", -bd.defCoverageBase, false);
                Row(sb, "Def bonuses", -bd.defStatusBonuses, false);
                Row(sb, "Def coach", -bd.defCoachBase, false);
                Row(sb, "Coverage guess", -bd.defCoverageMod, false);
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

        private void Row(StringBuilder sb, string label, int val, bool always)
        {
            if (val == 0 && !always) return; // zero rows are noise
            string col = val > 0 ? PositiveHex : (val < 0 ? NegativeHex : DimHex);
            sb.AppendLine($"{label}<pos=78%><color={col}><b>{FormatSigned(val)}</b></color>");
            rowCount++;
        }

        private void SetFooterAlpha(float a)
        {
            receiverText.alpha = a;
            tackleText.alpha = a;
            coverageText.alpha = a;
            failEventText.alpha = a;
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
            SetAnchors(rootPanel, new Vector2(0.03f, 0.16f), new Vector2(0.37f, 0.88f));

            // Left accent border
            GameObject border = new GameObject("LeftBorder");
            border.transform.SetParent(rootPanel.transform, false);
            Image bImg = border.AddComponent<Image>();
            bImg.color = COL_GOLD;
            SetAnchors(border, new Vector2(0f, 0f), new Vector2(0f, 1f));
            border.GetComponent<RectTransform>().sizeDelta = new Vector2(4f, 0f);

            // Header (play type label)
            headerText = MakeTMP(rootPanel.transform, "Header", 26f, COL_GOLD,
                new Vector2(0.08f, 0.90f), new Vector2(0.5f, 0.96f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            headerText.characterSpacing = 8f;

            // Net yardage (big)
            netYardageText = MakeTMP(rootPanel.transform, "NetYardage", 96f, COL_GREEN,
                new Vector2(0.08f, 0.76f), new Vector2(0.92f, 0.90f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            netYardageText.enableAutoSizing = true;
            netYardageText.fontSizeMin = 48f;
            netYardageText.fontSizeMax = 96f;

            // Separator
            GameObject sep = new GameObject("Separator");
            sep.transform.SetParent(rootPanel.transform, false);
            Image sepImg = sep.AddComponent<Image>();
            sepImg.color = new Color(1f, 1f, 1f, 0.15f);
            SetAnchors(sep, new Vector2(0.08f, 0.74f), new Vector2(0.92f, 0.74f));
            sep.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 2f);

            // Breakdown (monospace-style rows)
            breakdownText = MakeTMP(rootPanel.transform, "Breakdown", 32f, COL_WHITE,
                new Vector2(0.08f, 0.36f), new Vector2(0.92f, 0.74f),
                TextAlignmentOptions.TopLeft, FontStyles.Normal);
            breakdownText.lineSpacing = 8f;

            // Receiver / ball carrier
            receiverText = MakeTMP(rootPanel.transform, "Receiver", 28f, COL_CYAN,
                new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.35f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            // Who made the stop
            tackleText = MakeTMP(rootPanel.transform, "Tackle", 26f, COL_DIM,
                new Vector2(0.08f, 0.21f), new Vector2(0.92f, 0.28f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Normal);

            // Coverage shift
            coverageText = MakeTMP(rootPanel.transform, "Coverage", 26f, COL_DIM,
                new Vector2(0.08f, 0.14f), new Vector2(0.92f, 0.21f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Normal);

            // Fail event
            failEventText = MakeTMP(rootPanel.transform, "FailEvent", 36f, COL_RED,
                new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.13f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            rootPanel.SetActive(false);
        }

    }
}
