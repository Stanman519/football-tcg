using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TcgEngine.Gameplay;

namespace TcgEngine.UI
{
    /// <summary>
    /// RETIRED (2026-09-08) — no longer triggered. YardageSummaryPanel is the single post-play
    /// recap. This one fired a phase later at StartTurn and re-derived its breakdown client-side
    /// from base CardData stats, ignoring status buffs, so it printed different numbers than the
    /// server for the same play. Kept only because a scene GameObject may still reference the
    /// component; delete the file and the GameFeedbackUI field together once the scene is editable.
    ///
    /// Post-play yardage breakdown panel.
    /// Appears for ~4 seconds after each play, then fades out.
    /// Clicking anywhere dismisses early.
    /// </summary>
    public class PlaySummaryPanel : MonoBehaviour
    {
        [Header("UI References (optional — auto-built if null)")]
        public GameObject panelRoot;
        public TextMeshProUGUI playTypeText;
        public TextMeshProUGUI yardageText;
        public TextMeshProUGUI downDistText;
        public TextMeshProUGUI breakdownText;

        [Header("Settings")]
        public float displayDuration = 4f;
        public float fadeDuration = 0.6f;

        private Coroutine activeCoroutine;
        private CanvasGroup canvasGroup;
        private bool selfBuilt = false;

        // Built refs
        private GameObject builtRoot;
        private TextMeshProUGUI builtPlayType, builtYardage, builtDownDist, builtBreakdown;
        private CanvasGroup builtGroup;

        void Awake()
        {
            bool hasRef = panelRoot != null || playTypeText != null;
            if (!hasRef)
                BuildSelfContainedPanel();
            else
            {
                if (panelRoot != null)
                    panelRoot.SetActive(false);
            }
        }

        /// <summary>Called by GameFeedbackUI when a play just resolved.</summary>
        public void ShowSummary(Game g, Player p0, Player p1)
        {
            if (g == null) return;
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);
            activeCoroutine = StartCoroutine(DisplaySummary(g, p0, p1));
        }

        private IEnumerator DisplaySummary(Game g, Player p0, Player p1)
        {
            // Resolve refs
            GameObject root = panelRoot ?? builtRoot;
            TextMeshProUGUI ptText = playTypeText ?? builtPlayType;
            TextMeshProUGUI ydText = yardageText ?? builtYardage;
            TextMeshProUGUI ddText = downDistText ?? builtDownDist;
            TextMeshProUGUI bdText = breakdownText ?? builtBreakdown;
            CanvasGroup cg = canvasGroup ?? builtGroup;

            if (root == null) yield break;

            // Populate
            PopulateContent(g, p0, p1, ptText, ydText, ddText, bdText);

            // Show at full alpha
            root.SetActive(true);
            if (cg != null) cg.alpha = 1f;

            // Hold
            float elapsed = 0f;
            while (elapsed < displayDuration)
            {
                // Dismiss on click
                if (Input.GetMouseButtonDown(0)) break;
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Fade out
            if (cg != null)
            {
                float fadeElapsed = 0f;
                while (fadeElapsed < fadeDuration)
                {
                    cg.alpha = 1f - (fadeElapsed / fadeDuration);
                    fadeElapsed += Time.deltaTime;
                    yield return null;
                }
                cg.alpha = 0f;
            }

            root.SetActive(false);
            activeCoroutine = null;
        }

        private void PopulateContent(Game g, Player p0, Player p1,
                                     TextMeshProUGUI ptText, TextMeshProUGUI ydText,
                                     TextMeshProUGUI ddText, TextMeshProUGUI bdText)
        {
            // --- Play type label ---
            string playLabel = g.last_play_type switch
            {
                PlayType.Run => "RUN PLAY",
                PlayType.ShortPass => "SHORT PASS",
                PlayType.LongPass => "DEEP PASS",
                _ => "PLAY"
            };
            if (ptText != null) ptText.text = playLabel;

            // --- Yardage headline ---
            int yards = g.last_play_yardage;
            string ydLabel;
            if (yards > 0)
                ydLabel = $"+{yards} yards";
            else if (yards < 0)
                ydLabel = $"{yards} yards";
            else
                ydLabel = "No gain";
            if (ydText != null) ydText.text = ydLabel;

            // --- Down & distance (current, after the play) ---
            if (ddText != null)
            {
                string downStr = g.current_down switch
                {
                    1 => "1st",
                    2 => "2nd",
                    3 => "3rd",
                    4 => "4th",
                    _ => $"{g.current_down}th"
                };
                ddText.text = $"{downStr} & {g.yardage_to_go}";
            }

            // --- Breakdown body ---
            if (bdText == null) return;

            bool p0isOff = g.current_offensive_player?.player_id == 0;
            Player offP = p0isOff ? p0 : p1;
            Player defP = p0isOff ? p1 : p0;

            int coachBase = 0;
            int offCards = 0;
            int defCards = 0;

            // Coach base
            if (offP?.head_coach?.baseOffenseYardage != null &&
                offP.head_coach.baseOffenseYardage.ContainsKey(g.last_play_type))
                coachBase = offP.head_coach.baseOffenseYardage[g.last_play_type];

            // Card totals
            if (offP != null)
            {
                foreach (var c in offP.cards_board)
                {
                    if (c.Data == null) continue;
                    if (g.last_play_type == PlayType.Run)
                        offCards += c.Data.run_bonus;
                    else if (g.last_play_type == PlayType.ShortPass)
                        offCards += c.Data.short_pass_bonus;
                    else
                        offCards += c.Data.deep_pass_bonus;
                }
            }
            if (defP != null)
            {
                foreach (var c in defP.cards_board)
                {
                    if (c.Data == null) continue;
                    if (g.last_play_type == PlayType.Run)
                        defCards += c.Data.run_coverage_bonus;
                    else if (g.last_play_type == PlayType.ShortPass)
                        defCards += c.Data.short_pass_coverage_bonus;
                    else
                        defCards += c.Data.deep_pass_coverage_bonus;
                }
            }

            var lines = new List<string>
            {
                $"  Coach base           {FormatYd(coachBase)}",
                $"  Your cards           {FormatYd(offCards)}",
                $"  Their coverage       {FormatYd(-defCards)}",
            };
            bdText.text = string.Join("\n", lines);
        }

        private static string FormatYd(int val)
        {
            return val >= 0 ? $"+{val}" : val.ToString();
        }

        // -----------------------------------------------------------------------
        // Self-contained panel builder
        // -----------------------------------------------------------------------
        private void BuildSelfContainedPanel()
        {
            if (selfBuilt) return;
            selfBuilt = true;

            Canvas c = gameObject.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 60;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            gameObject.AddComponent<GraphicRaycaster>();

            // Panel card (center of screen, lower third)
            builtRoot = new GameObject("SummaryPanel");
            builtRoot.transform.SetParent(transform, false);
            builtGroup = builtRoot.AddComponent<CanvasGroup>();

            Image panelBg = builtRoot.AddComponent<Image>();
            panelBg.color = new Color(0.05f, 0.05f, 0.1f, 0.93f);
            RectTransform rt = builtRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.25f, 0.08f);
            rt.anchorMax = new Vector2(0.75f, 0.42f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            // Play type (top)
            builtPlayType = MakeTmpText(builtRoot.transform, "PlayType", 18,
                new Color(1f, 0.85f, 0.2f), new Vector2(0.05f, 0.75f), new Vector2(0.95f, 1f),
                TextAlignmentOptions.Center, FontStyles.Bold);

            // Yardage headline
            builtYardage = MakeTmpText(builtRoot.transform, "Yardage", 26,
                Color.white, new Vector2(0.05f, 0.52f), new Vector2(0.95f, 0.78f),
                TextAlignmentOptions.Center, FontStyles.Bold);

            // Down & distance
            builtDownDist = MakeTmpText(builtRoot.transform, "DownDist", 14,
                new Color(0.7f, 0.7f, 0.7f), new Vector2(0.05f, 0.38f), new Vector2(0.95f, 0.55f),
                TextAlignmentOptions.Center);

            // Breakdown body
            builtBreakdown = MakeTmpText(builtRoot.transform, "Breakdown", 13,
                Color.white, new Vector2(0.02f, 0.0f), new Vector2(0.98f, 0.42f),
                TextAlignmentOptions.TopLeft);

            // Separator line
            GameObject sep = new GameObject("Separator");
            sep.transform.SetParent(builtRoot.transform, false);
            Image sepImg = sep.AddComponent<Image>();
            sepImg.color = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            RectTransform sepRT = sep.GetComponent<RectTransform>();
            sepRT.anchorMin = new Vector2(0.05f, 0.41f);
            sepRT.anchorMax = new Vector2(0.95f, 0.42f);
            sepRT.offsetMin = sepRT.offsetMax = Vector2.zero;

            builtRoot.SetActive(false);
        }

        private TextMeshProUGUI MakeTmpText(Transform parent, string name, float size, Color color,
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
            t.richText = false;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return t;
        }
    }
}
