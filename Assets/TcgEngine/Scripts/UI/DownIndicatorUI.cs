using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using TcgEngine;
using TcgEngine.Client;
using TcgEngine.Gameplay;

namespace TcgEngine.UI
{
    public class DownIndicatorUI : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset boldFont;

        private CanvasGroup canvasGroup;
        private RectTransform slantRT;
        private TextMeshProUGUI label;
        private Sequence activeSeq;
        private GamePhase prevPhase = GamePhase.None;

        private static DownIndicatorUI _instance;
        public static DownIndicatorUI Get() => _instance;

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
            if (gd == null) return;

            if (gd.phase == GamePhase.ChoosePlayers && prevPhase != GamePhase.ChoosePlayers)
            {
                string text = gd.current_down switch
                {
                    1 => "1ST DOWN",
                    2 => "2ND DOWN",
                    3 => "3RD DOWN",
                    4 => "4TH DOWN",
                    _ => $"{gd.current_down}TH DOWN"
                };
                Show(text);
            }
            prevPhase = gd.phase;
        }

        public void Show(string text)
        {
            activeSeq?.Kill();
            SetVisible(true);
            if (label != null) label.text = text;

            slantRT.anchoredPosition = new Vector2(-1920f, 0f);

            activeSeq = DOTween.Sequence()
                .Append(slantRT.DOAnchorPosX(0f, 0.3f).SetEase(Ease.OutBack))
                .AppendInterval(1.5f)
                .Append(slantRT.DOAnchorPosX(1920f, 0.25f).SetEase(Ease.InQuad))
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
            canvas.sortingOrder = 111;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            gameObject.AddComponent<GraphicRaycaster>();
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Slanted outer container — centered, rotated for parallelogram effect
            var slantGo = new GameObject("SlantOuter");
            slantGo.transform.SetParent(transform, false);
            slantRT = slantGo.AddComponent<RectTransform>();
            slantRT.anchorMin = new Vector2(0.5f, 0.5f);
            slantRT.anchorMax = new Vector2(0.5f, 0.5f);
            slantRT.pivot = new Vector2(0.5f, 0.5f);
            slantRT.anchoredPosition = Vector2.zero;
            slantRT.sizeDelta = new Vector2(1120f, 160f);
            slantGo.transform.localEulerAngles = new Vector3(0f, 0f, -6f);

            // Dark background
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(slantGo.transform, false);
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.102f, 0.102f, 0.102f, 1f); // #1a1a1a
            var bgRT = bgGo.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;

            // Orange glow overlay
            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(slantGo.transform, false);
            var glowImg = glowGo.AddComponent<Image>();
            glowImg.color = new Color(1f, 0.541f, 0f, 0.12f); // rgba(255,138,0,0.12)
            var glowRT = glowGo.GetComponent<RectTransform>();
            glowRT.anchorMin = Vector2.zero;
            glowRT.anchorMax = Vector2.one;
            glowRT.offsetMin = glowRT.offsetMax = Vector2.zero;

            // Left 8px orange border
            var borderGo = new GameObject("LeftBorder");
            borderGo.transform.SetParent(slantGo.transform, false);
            var borderImg = borderGo.AddComponent<Image>();
            borderImg.color = new Color(1f, 0.541f, 0f, 1f); // #ff8a00
            var borderRT = borderGo.GetComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = new Vector2(0f, 1f);
            borderRT.pivot = new Vector2(0f, 0.5f);
            borderRT.anchoredPosition = Vector2.zero;
            borderRT.sizeDelta = new Vector2(8f, 0f);

            // Counter-rotated text container (keeps text upright)
            var textOuterGo = new GameObject("TextOuter");
            textOuterGo.transform.SetParent(slantGo.transform, false);
            var textOuterRT = textOuterGo.AddComponent<RectTransform>();
            textOuterRT.anchorMin = Vector2.zero;
            textOuterRT.anchorMax = Vector2.one;
            textOuterRT.offsetMin = textOuterRT.offsetMax = Vector2.zero;
            textOuterGo.transform.localEulerAngles = new Vector3(0f, 0f, 6f);

            // Down text label
            var labelGo = new GameObject("DownLabel");
            labelGo.transform.SetParent(textOuterGo.transform, false);
            label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = "1ST DOWN";
            label.fontSize = 96f;
            label.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.characterSpacing = -4f;
            if (boldFont != null) label.font = boldFont;
            var labelRT = labelGo.GetComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = new Vector2(56f, 0f);
            labelRT.offsetMax = new Vector2(-48f, 0f);
        }
    }
}
