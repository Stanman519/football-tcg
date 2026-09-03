using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using TcgEngine;
using TcgEngine.Client;
using TcgEngine.Gameplay;

namespace TcgEngine.UI
{
    public class PhaseIndicatorUI : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset boldFont;

        private CanvasGroup canvasGroup;
        private RectTransform barRT;
        private TextMeshProUGUI label;
        private Sequence activeSeq;
        private GamePhase prevPhase = GamePhase.None;

        private static PhaseIndicatorUI _instance;
        public static PhaseIndicatorUI Get() => _instance;

        void Awake()
        {
            _instance = this;
            BuildUI();
            SetVisible(false);
        }

        void Start()
        {
            var gc = GameClient.Get();
            if (gc != null) gc.onRefreshAll += OnRefresh;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
            activeSeq?.Kill();
            var gc = GameClient.Get();
            if (gc != null) gc.onRefreshAll -= OnRefresh;
        }

        private void OnRefresh()
        {
            Game gd = GameClient.Get()?.GetGameData();
            if (gd == null || gd.phase == prevPhase) return;
            GamePhase cur = gd.phase;
            prevPhase = cur;

            string text = cur switch
            {
                GamePhase.ChoosePlayers => "CHOOSE PLAYERS",
                GamePhase.ChoosePlay => "SELECT PLAY",
                GamePhase.LiveBall => "LIVE BALL",
                _ => null
            };
            if (text != null) Show(text);
        }

        public void Show(string text)
        {
            activeSeq?.Kill();
            SetVisible(true);
            if (label != null) label.text = text;

            barRT.anchoredPosition = new Vector2(-1920f, 0f);

            activeSeq = DOTween.Sequence()
                .Append(barRT.DOAnchorPosX(0f, 0.25f).SetEase(Ease.OutQuad))
                .AppendInterval(1.5f)
                .Append(barRT.DOAnchorPosX(-1920f, 0.2f).SetEase(Ease.InQuad))
                .OnComplete(() => SetVisible(false))
                .SetLink(gameObject);
        }

        private void SetVisible(bool show)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = show ? 1f : 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
        }

        private void BuildUI()
        {
            // Root canvas
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 110;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            gameObject.AddComponent<GraphicRaycaster>();
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Bar root — full width, 80px tall, anchored near top
            var barGo = new GameObject("PhaseBar");
            barGo.transform.SetParent(transform, false);
            barGo.AddComponent<Image>().color = new Color(0.102f, 0.102f, 0.102f, 0.85f);
            barRT = barGo.GetComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0f, 1f);
            barRT.anchorMax = new Vector2(1f, 1f);
            barRT.pivot = new Vector2(0.5f, 1f);
            barRT.anchoredPosition = new Vector2(0f, -130f);
            barRT.sizeDelta = new Vector2(0f, 80f);

            // Left 4px mint border
            var borderGo = new GameObject("LeftBorder");
            borderGo.transform.SetParent(barGo.transform, false);
            var borderImg = borderGo.AddComponent<Image>();
            borderImg.color = new Color(0.667f, 1f, 0.863f, 1f); // #aaffdc
            var borderRT = borderGo.GetComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = new Vector2(0f, 1f);
            borderRT.pivot = new Vector2(0f, 0.5f);
            borderRT.anchoredPosition = Vector2.zero;
            borderRT.sizeDelta = new Vector2(4f, 0f);

            // Left slanted accent (rotated box for parallelogram aesthetic)
            var accentGo = new GameObject("LeftAccent");
            accentGo.transform.SetParent(barGo.transform, false);
            var accentImg = accentGo.AddComponent<Image>();
            accentImg.color = new Color(0.667f, 1f, 0.863f, 0.1f);
            var accentRT = accentGo.GetComponent<RectTransform>();
            accentRT.anchorMin = new Vector2(0f, 0f);
            accentRT.anchorMax = new Vector2(0f, 1f);
            accentRT.pivot = new Vector2(0f, 0.5f);
            accentRT.anchoredPosition = new Vector2(-28f, 0f);
            accentRT.sizeDelta = new Vector2(128f, 0f);
            accentGo.transform.localEulerAngles = new Vector3(0f, 0f, -12f);

            // Phase text label
            var labelGo = new GameObject("PhaseLabel");
            labelGo.transform.SetParent(barGo.transform, false);
            label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = "CHOOSE PLAYERS";
            label.fontSize = 36f;
            label.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            label.color = new Color(0.667f, 1f, 0.863f, 1f); // #aaffdc
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.characterSpacing = -4f;
            if (boldFont != null) label.font = boldFont;
            var labelRT = labelGo.GetComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = new Vector2(48f, 0f);
            labelRT.offsetMax = new Vector2(-48f, 0f);
        }
    }
}
