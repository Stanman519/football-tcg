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
    /// Full-screen broadcast-style reveal overlay that fires after both players confirm plays.
    /// Sequential 3-frame reveal: Your Play -> Opponent Call -> Enhancers (if any).
    /// Self-builds UI if Inspector refs are left null.
    /// Triggered by GameFeedbackUI on RevealPlayCalls phase transition.
    /// </summary>
    public class PlayCallRevealPanel : MonoBehaviour
    {
        // ── Figma-exact colors ──
        static readonly Color COL_MINT       = new Color32(170, 255, 220, 255);  // #AAFFDC
        static readonly Color COL_CORAL      = new Color32(255, 113, 108, 255);  // #FF716C
        static readonly Color COL_CYAN       = new Color32(0, 227, 253, 255);    // #00E3FD
        static readonly Color COL_BG         = new Color32(14, 14, 14, 255);     // #0E0E0E
        static readonly Color COL_DIM_WHITE  = new Color32(173, 170, 170, 255);  // #ADAAAA
        static readonly Color COL_DARK_CARD  = new Color32(26, 26, 26, 255);     // #1A1A1A
        static readonly Color COL_BORDER_DIM = new Color32(72, 72, 71, 77);      // rgba(72,72,71,0.3)

        // ── Timing (suspense delays) ──
        [Header("Timing")]
        public float frame1FadeIn   = 0.35f;
        public float frame1Hold     = 0.6f;
        public float frame2FadeIn   = 0.4f;
        public float frame2Hold     = 1.8f;   // suspense
        public float frame3FadeIn   = 0.35f;
        public float frame3Hold     = 1.5f;
        public float finalFadeOut   = 0.5f;

        // ── Built refs ──
        private Canvas overlayCanvas;
        private CanvasGroup rootGroup;
        private GameObject rootPanel;

        // Frame 1: Your Play
        private CanvasGroup frame1Group;
        private Image frame1Border;
        private TextMeshProUGUI frame1Label;
        private TextMeshProUGUI frame1PlayName;
        private TextMeshProUGUI frame1CoachBonus;

        // Frame 2: Opponent Call
        private CanvasGroup frame2Group;
        private Image frame2Border;
        private TextMeshProUGUI frame2Label;
        private TextMeshProUGUI frame2PlayName;
        private TextMeshProUGUI frame2CoachBonus;
        private TextMeshProUGUI frame2GuessLabel;
        private TextMeshProUGUI frame2GuessValue;
        private Image frame2GuessBg;

        // Frame 3: Enhancers
        private CanvasGroup frame3Group;
        private TextMeshProUGUI frame3Label;
        private TextMeshProUGUI frame3UserName;
        private TextMeshProUGUI frame3UserSub;
        private TextMeshProUGUI frame3OppName;
        private TextMeshProUGUI frame3OppSub;
        private Image frame3UserBorder;
        private Image frame3OppBorder;

        private Coroutine activeCoroutine;
        private bool selfBuilt;
        private bool isShowing;

        // ── Singleton ──
        private static PlayCallRevealPanel instance;
        public static PlayCallRevealPanel Get() => instance;

        void Awake()
        {
            instance = this;
            BuildUI();
            if (rootPanel != null) rootPanel.SetActive(false);
        }

        /// <summary>Show the 3-frame reveal sequence with real game data.</summary>
        public void ShowReveal(Game g, Player localPlayer, Player opponentPlayer)
        {
            if (g == null || isShowing) return;
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);
            activeCoroutine = StartCoroutine(RunRevealSequence(g, localPlayer, opponentPlayer));
        }

        /// <summary>Immediately hide (e.g. scene change).</summary>
        public void Hide()
        {
            if (activeCoroutine != null) StopCoroutine(activeCoroutine);
            activeCoroutine = null;
            isShowing = false;
            if (rootPanel != null) rootPanel.SetActive(false);
        }

        // =====================================================================
        // Reveal coroutine
        // =====================================================================
        private IEnumerator RunRevealSequence(Game g, Player localPlayer, Player opponentPlayer)
        {
            isShowing = true;

            // Determine offense/defense from local perspective
            bool localIsOffense = g.current_offensive_player?.player_id == localPlayer?.player_id;
            Player offP = localIsOffense ? localPlayer : opponentPlayer;
            Player defP = localIsOffense ? opponentPlayer : localPlayer;

            PlayType offPlay = offP?.SelectedPlay ?? PlayType.Huddle;
            PlayType defGuess = defP?.SelectedPlay ?? g.defense_guess;
            bool guessCorrect = defGuess == offPlay && defGuess != PlayType.Huddle;

            Card localEnhancer = localPlayer?.PlayEnhancer;
            Card oppEnhancer = opponentPlayer?.PlayEnhancer;
            bool hasEnhancers = localEnhancer != null || oppEnhancer != null;

            // Populate text
            PopulateFrame1(localIsOffense ? offPlay : defGuess, localIsOffense, localPlayer);
            PopulateFrame2(localIsOffense ? defGuess : offPlay, localIsOffense, offPlay, defGuess, guessCorrect, opponentPlayer);
            if (hasEnhancers) PopulateFrame3(localEnhancer, oppEnhancer);

            // Reset all frames hidden
            rootPanel.SetActive(true);
            rootGroup.alpha = 1f;
            frame1Group.alpha = 0f;
            frame2Group.alpha = 0f;
            frame3Group.alpha = 0f;
            frame3Group.gameObject.SetActive(hasEnhancers);

            // ── Frame 1: Your Play ──
            frame1Group.DOFade(1f, frame1FadeIn).SetEase(Ease.OutQuad).SetLink(gameObject);
            yield return new WaitForSeconds(frame1FadeIn + frame1Hold);

            // ── Frame 2: Opponent Call (with suspense) ──
            // Fade frame 1 to 30% while frame 2 fades in
            frame1Group.DOFade(0.3f, frame2FadeIn).SetEase(Ease.InQuad).SetLink(gameObject);
            frame2Group.DOFade(1f, frame2FadeIn).SetEase(Ease.OutQuad).SetLink(gameObject);
            yield return new WaitForSeconds(frame2FadeIn + frame2Hold);

            // ── Frame 3: Enhancers (if any) ──
            if (hasEnhancers)
            {
                frame2Group.DOFade(0.6f, frame3FadeIn).SetEase(Ease.InQuad).SetLink(gameObject);
                frame3Group.DOFade(1f, frame3FadeIn).SetEase(Ease.OutQuad).SetLink(gameObject);
                yield return new WaitForSeconds(frame3FadeIn + frame3Hold);
            }

            // ── Fade out entire overlay ──
            rootGroup.DOFade(0f, finalFadeOut).SetEase(Ease.InQuad).SetLink(gameObject);
            yield return new WaitForSeconds(finalFadeOut);

            rootPanel.SetActive(false);
            isShowing = false;
            activeCoroutine = null;
        }

        // =====================================================================
        // Populate frames with data
        // =====================================================================
        private void PopulateFrame1(PlayType play, bool isOffense, Player localPlayer)
        {
            frame1Label.text = isOffense ? "YOUR PLAY" : "YOUR COVERAGE";
            frame1PlayName.text = PlayTypeLabel(play);
            frame1PlayName.color = COL_MINT;
            frame1Border.color = MintBorderBg();
            frame1Label.color = COL_MINT;
            frame1CoachBonus.text = GetCoachBonusText(localPlayer, play, isOffense);
        }

        private void PopulateFrame2(PlayType play, bool localIsOffense,
                                    PlayType offPlay, PlayType defGuess, bool guessCorrect,
                                    Player opponentPlayer)
        {
            frame2Label.text = localIsOffense ? "OPPONENT CALL" : "OPPONENT PLAY";
            frame2PlayName.text = PlayTypeLabel(play);
            frame2PlayName.color = COL_CORAL;
            frame2Border.color = CoralBorderBg();
            frame2Label.color = COL_CORAL;
            frame2CoachBonus.text = GetCoachBonusText(opponentPlayer, play, !localIsOffense);

            // Coverage guess display
            frame2GuessLabel.text = "COVERAGE GUESS";
            frame2GuessValue.text = PlayTypeLabel(defGuess);
            frame2GuessValue.color = guessCorrect ? COL_CYAN : COL_DIM_WHITE;

            Color guessBg = guessCorrect
                ? new Color(0f, 0.89f, 0.99f, 0.1f)   // cyan tint
                : new Color(1f, 1f, 1f, 0.05f);        // dim
            frame2GuessBg.color = guessBg;
        }

        private void PopulateFrame3(Card localEnhancer, Card oppEnhancer)
        {
            frame3UserName.text = localEnhancer != null ? localEnhancer.Data?.title?.ToUpper() ?? "NONE" : "NONE";
            frame3UserSub.text = localEnhancer != null ? GetEnhancerSubtext(localEnhancer) : "NO ENHANCER";

            frame3OppName.text = oppEnhancer != null ? oppEnhancer.Data?.title?.ToUpper() ?? "NONE" : "NONE";
            frame3OppSub.text = oppEnhancer != null ? GetEnhancerSubtext(oppEnhancer) : "NO ENHANCER";
        }

        private string GetEnhancerSubtext(Card c)
        {
            if (c?.Data == null) return "";
            if (c.Data.abilities != null && c.Data.abilities.Length > 0)
            {
                var ab = c.Data.abilities[0];
                return ab.desc != null && ab.desc.Length > 0 ? ab.desc : "ACTIVE";
            }
            return "ACTIVE";
        }

        private static string PlayTypeLabel(PlayType pt) => pt switch
        {
            PlayType.Run       => "RUN",
            PlayType.ShortPass => "SHORT PASS",
            PlayType.LongPass  => "LONG PASS",
            _                  => "HUDDLE"
        };

        private static string GetCoachBonusText(Player player, PlayType play, bool isOffense)
        {
            if (player?.head_coach == null) return "COACH: +0 YDS";
            var dict = isOffense ? player.head_coach.baseOffenseYardage : player.head_coach.baseDefenseYardage;
            int bonus = dict != null && dict.TryGetValue(play, out int val) ? val : 0;
            string suffix = isOffense ? "YDS" : "COV";
            return $"COACH: +{bonus} {suffix}";
        }

        private static Color MintBorderBg()  => new Color(0.667f, 1f, 0.863f, 0.05f);
        private static Color CoralBorderBg() => new Color(1f, 0.443f, 0.424f, 0.05f);

        // =====================================================================
        // Self-contained UI builder (matches Figma layout)
        // =====================================================================
        private void BuildUI()
        {
            if (selfBuilt) return;
            selfBuilt = true;

            // ── Canvas (ScreenSpace Overlay, sortOrder 80) ──
            overlayCanvas = gameObject.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 80;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // ── Root panel (full screen dark overlay) ──
            rootPanel = new GameObject("RevealRoot");
            rootPanel.transform.SetParent(transform, false);
            rootGroup = rootPanel.AddComponent<CanvasGroup>();
            rootGroup.blocksRaycasts = false;

            Image rootBg = rootPanel.AddComponent<Image>();
            rootBg.color = new Color(COL_BG.r, COL_BG.g, COL_BG.b, 0.75f);
            StretchFill(rootPanel);

            // ── 3-column container (centered, padded) ──
            // Figma: 3 equal columns at 373px each in 1280px → ~29% each with gaps
            // In 1920: pad left/right ~10%, columns fill middle 80%

            try
            {
                // ── Frame 1: YOUR PLAY (left third) ──
                frame1Group = BuildFrame(rootPanel.transform, "Frame1_YourPlay",
                    new Vector2(0.04f, 0.12f), new Vector2(0.34f, 0.88f),
                    COL_MINT, out frame1Border, out frame1Label, out frame1PlayName);
                frame1CoachBonus = MakeTMP(frame1Group.transform, "CoachBonus", 14f,
                    new Color(COL_MINT.r, COL_MINT.g, COL_MINT.b, 0.6f),
                    new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.38f),
                    TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                frame1CoachBonus.characterSpacing = 4f;

                // ── Frame 2: OPPONENT CALL (center third) ──
                frame2Group = BuildFrame(rootPanel.transform, "Frame2_OpponentCall",
                    new Vector2(0.35f, 0.12f), new Vector2(0.65f, 0.88f),
                    COL_CORAL, out frame2Border, out frame2Label, out frame2PlayName);
                frame2CoachBonus = MakeTMP(frame2Group.transform, "CoachBonus", 14f,
                    new Color(COL_CORAL.r, COL_CORAL.g, COL_CORAL.b, 0.6f),
                    new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.38f),
                    TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                frame2CoachBonus.characterSpacing = 4f;

                // Build coverage guess sub-panel inside frame 2
                BuildCoverageGuessUI(frame2Group.transform);

                // ── Frame 3: ENHANCERS (right third) ──
                frame3Group = BuildEnhancerFrame(rootPanel.transform, "Frame3_Enhancers",
                    new Vector2(0.66f, 0.12f), new Vector2(0.96f, 0.88f));
            }
            finally
            {
                rootPanel.SetActive(false);
            }
        }

        // ── Build a single play-reveal frame (Frame 1 or 2) ──
        private CanvasGroup BuildFrame(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color accentColor,
            out Image borderImg, out TextMeshProUGUI labelTmp, out TextMeshProUGUI playNameTmp)
        {
            GameObject frame = new GameObject(name);
            frame.transform.SetParent(parent, false);
            Image frameBg = frame.AddComponent<Image>();
            frameBg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.05f);
            CanvasGroup cg = frame.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            SetAnchors(frame, anchorMin, anchorMax);
            borderImg = frameBg;

            // Left border strip (4px)
            GameObject border = new GameObject("LeftBorder");
            border.transform.SetParent(frame.transform, false);
            Image bImg = border.AddComponent<Image>();
            bImg.color = accentColor;
            SetAnchors(border, new Vector2(0f, 0f), new Vector2(0f, 1f));
            border.GetComponent<RectTransform>().sizeDelta = new Vector2(4f, 0f);

            // Label ("YOUR PLAY" / "OPPONENT CALL") — skewed tag style
            GameObject labelBg = new GameObject("LabelBg");
            labelBg.transform.SetParent(frame.transform, false);
            Image lbImg = labelBg.AddComponent<Image>();
            lbImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.2f);
            SetAnchors(labelBg, new Vector2(0.15f, 0.72f), new Vector2(0.55f, 0.78f));

            labelTmp = MakeTMP(frame.transform, "Label", 12f, accentColor,
                new Vector2(0.16f, 0.72f), new Vector2(0.54f, 0.78f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            labelTmp.characterSpacing = 12f;
            labelTmp.enableAutoSizing = false;

            // Big play name (72px equivalent → 56px at 1920 scale)
            playNameTmp = MakeTMP(frame.transform, "PlayName", 64f, accentColor,
                new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.72f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            playNameTmp.enableAutoSizing = true;
            playNameTmp.fontSizeMin = 36f;
            playNameTmp.fontSizeMax = 64f;

            return cg;
        }

        // ── Coverage guess sub-UI (inside Frame 2) ──
        private void BuildCoverageGuessUI(Transform parent)
        {
            GameObject guessPanel = new GameObject("CoverageGuess");
            guessPanel.transform.SetParent(parent, false);
            frame2GuessBg = guessPanel.AddComponent<Image>();
            frame2GuessBg.color = new Color(0f, 0.89f, 0.99f, 0.1f);
            SetAnchors(guessPanel, new Vector2(0.2f, 0.15f), new Vector2(0.8f, 0.32f));

            // Thin bottom border (cyan)
            GameObject guessBorder = new GameObject("GuessBorder");
            guessBorder.transform.SetParent(guessPanel.transform, false);
            Image gbImg = guessBorder.AddComponent<Image>();
            gbImg.color = COL_CYAN;
            SetAnchors(guessBorder, new Vector2(0f, 0f), new Vector2(1f, 0f));
            guessBorder.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 2f);

            frame2GuessLabel = MakeTMP(guessPanel.transform, "GuessLabel", 10f, COL_DIM_WHITE,
                new Vector2(0.1f, 0.55f), new Vector2(0.9f, 0.9f),
                TextAlignmentOptions.Center, FontStyles.Bold);
            frame2GuessLabel.characterSpacing = 4f;

            frame2GuessValue = MakeTMP(guessPanel.transform, "GuessValue", 18f, COL_CYAN,
                new Vector2(0.1f, 0.08f), new Vector2(0.9f, 0.55f),
                TextAlignmentOptions.Center, FontStyles.Bold);
        }

        // ── Build enhancer frame (Frame 3) ──
        private CanvasGroup BuildEnhancerFrame(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject frame = new GameObject(name);
            frame.transform.SetParent(parent, false);
            Image frameBg = frame.AddComponent<Image>();
            frameBg.color = new Color(0.125f, 0.125f, 0.122f, 0.4f);
            CanvasGroup cg = frame.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            SetAnchors(frame, anchorMin, anchorMax);

            // Left border strip
            GameObject border = new GameObject("LeftBorder");
            border.transform.SetParent(frame.transform, false);
            Image bImg = border.AddComponent<Image>();
            bImg.color = new Color(1f, 1f, 1f, 0.2f);
            SetAnchors(border, new Vector2(0f, 0f), new Vector2(0f, 1f));
            border.GetComponent<RectTransform>().sizeDelta = new Vector2(4f, 0f);

            // Label
            GameObject labelBg = new GameObject("LabelBg");
            labelBg.transform.SetParent(frame.transform, false);
            Image lbImg = labelBg.AddComponent<Image>();
            lbImg.color = new Color(1f, 1f, 1f, 0.1f);
            SetAnchors(labelBg, new Vector2(0.1f, 0.72f), new Vector2(0.7f, 0.78f));

            frame3Label = MakeTMP(frame.transform, "EnhLabel", 12f, Color.white,
                new Vector2(0.12f, 0.72f), new Vector2(0.68f, 0.78f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            frame3Label.characterSpacing = 12f;
            frame3Label.text = "PLAY ENHANCERS";

            // ── User enhancer card ──
            BuildEnhancerCard(frame.transform, "UserCard", COL_MINT,
                new Vector2(0.08f, 0.48f), new Vector2(0.92f, 0.64f),
                out frame3UserName, out frame3UserSub, out frame3UserBorder);

            // ── Opponent enhancer card ──
            BuildEnhancerCard(frame.transform, "OppCard", COL_CORAL,
                new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.44f),
                out frame3OppName, out frame3OppSub, out frame3OppBorder);

            return cg;
        }

        private void BuildEnhancerCard(Transform parent, string name, Color accentColor,
            Vector2 anchorMin, Vector2 anchorMax,
            out TextMeshProUGUI nameTmp, out TextMeshProUGUI subTmp, out Image borderImg)
        {
            GameObject card = new GameObject(name);
            card.transform.SetParent(parent, false);
            Image cardBg = card.AddComponent<Image>();
            cardBg.color = new Color(0f, 0f, 0f, 0.8f);
            SetAnchors(card, anchorMin, anchorMax);

            // Left accent border
            GameObject bdr = new GameObject("AccentBorder");
            bdr.transform.SetParent(card.transform, false);
            borderImg = bdr.AddComponent<Image>();
            borderImg.color = accentColor;
            SetAnchors(bdr, new Vector2(0f, 0f), new Vector2(0f, 1f));
            bdr.GetComponent<RectTransform>().sizeDelta = new Vector2(3f, 0f);

            // Card name
            nameTmp = MakeTMP(card.transform, "CardName", 18f, accentColor,
                new Vector2(0.12f, 0.35f), new Vector2(0.95f, 0.95f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);

            // Subtitle
            subTmp = MakeTMP(card.transform, "CardSub", 10f, COL_DIM_WHITE,
                new Vector2(0.12f, 0.05f), new Vector2(0.95f, 0.4f),
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
            subTmp.characterSpacing = 4f;
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
