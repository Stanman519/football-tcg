using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using TcgEngine;

namespace TcgEngine.UI
{
    public class AudibleCalloutUI : MonoBehaviour
    {
        [Header("Audio")]
        public AudioClip hotRouteSfx;

        private Canvas overlayCanvas;
        private CanvasGroup canvasGroup;
        private RectTransform stripRT;
        private TextMeshProUGUI mainText;
        private TextMeshProUGUI subText;
        private Sequence activeSeq;

        private static AudibleCalloutUI _instance;
        public static AudibleCalloutUI Get() => _instance;

        void Awake()
        {
            _instance = this;
            BuildUI();
            SetVisible(false);
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
            activeSeq?.Kill();
        }

        public void Show(string receiverName, RouteShape newRoute, float duration = 0.8f)
        {
            activeSeq?.Kill();
            SetVisible(true);

            string routeName = RouteShapeName(newRoute);
            if (mainText != null) mainText.text = "HOT ROUTE!";
            if (subText != null)
            {
                string sub = "";
                if (!string.IsNullOrEmpty(receiverName))
                    sub = receiverName.ToUpper();
                if (!string.IsNullOrEmpty(routeName))
                    sub += (sub.Length > 0 ? " — " : "") + routeName.ToUpper();
                subText.text = sub;
            }

            if (hotRouteSfx != null)
                AudioTool.Get()?.PlaySFX("hot_route", hotRouteSfx, 0.7f);

            float slideIn = Mathf.Min(0.2f, duration * 0.25f);
            float slideOut = Mathf.Min(0.15f, duration * 0.19f);
            float hold = Mathf.Max(0f, duration - slideIn - slideOut - 0.15f);

            float offscreenX = 800f;
            stripRT.anchoredPosition = new Vector2(offscreenX, 0);
            mainText.transform.localScale = Vector3.one;

            activeSeq = DOTween.Sequence()
                .Append(stripRT.DOAnchorPosX(0, slideIn).SetEase(Ease.OutQuad))
                .Append(mainText.transform.DOPunchScale(Vector3.one * 0.15f, 0.15f, 6, 0.5f))
                .AppendInterval(hold)
                .Append(stripRT.DOAnchorPosX(-offscreenX, slideOut).SetEase(Ease.InQuad))
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
            overlayCanvas = gameObject.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 105;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            gameObject.AddComponent<GraphicRaycaster>();
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Strip container — top-center, ~78-88% height
            GameObject stripGo = new GameObject("AudibleStrip");
            stripGo.transform.SetParent(transform, false);
            var stripBg = stripGo.AddComponent<Image>();
            stripBg.color = new Color(0.05f, 0.05f, 0.1f, 0.85f);
            stripRT = stripGo.GetComponent<RectTransform>();
            stripRT.anchorMin = new Vector2(0.15f, 0.78f);
            stripRT.anchorMax = new Vector2(0.85f, 0.88f);
            stripRT.offsetMin = stripRT.offsetMax = Vector2.zero;

            // Main text — "HOT ROUTE!"
            GameObject mainGo = new GameObject("MainText");
            mainGo.transform.SetParent(stripGo.transform, false);
            mainText = mainGo.AddComponent<TextMeshProUGUI>();
            mainText.fontSize = 48;
            mainText.fontStyle = FontStyles.Bold;
            mainText.alignment = TextAlignmentOptions.Center;
            mainText.color = new Color(1f, 0.85f, 0.2f);
            var mainRT = mainGo.GetComponent<RectTransform>();
            mainRT.anchorMin = new Vector2(0f, 0.4f);
            mainRT.anchorMax = new Vector2(1f, 1f);
            mainRT.offsetMin = mainRT.offsetMax = Vector2.zero;

            // Sub text — receiver + route
            GameObject subGo = new GameObject("SubText");
            subGo.transform.SetParent(stripGo.transform, false);
            subText = subGo.AddComponent<TextMeshProUGUI>();
            subText.fontSize = 28;
            subText.alignment = TextAlignmentOptions.Center;
            subText.color = new Color(1f, 1f, 1f, 0.7f);
            var subRT = subGo.GetComponent<RectTransform>();
            subRT.anchorMin = new Vector2(0f, 0f);
            subRT.anchorMax = new Vector2(1f, 0.45f);
            subRT.offsetMin = subRT.offsetMax = Vector2.zero;
        }

        public static string RouteShapeName(RouteShape shape)
        {
            switch (shape)
            {
                case RouteShape.Slant: return "Slant";
                case RouteShape.Out: return "Out";
                case RouteShape.Curl: return "Curl";
                case RouteShape.Hitch: return "Hitch";
                case RouteShape.Go: return "Go";
                case RouteShape.Post: return "Post";
                case RouteShape.Corner: return "Corner";
                case RouteShape.Flat: return "Flat";
                case RouteShape.Drag: return "Drag";
                case RouteShape.Seam: return "Seam";
                case RouteShape.Screen: return "Screen";
                case RouteShape.Swing: return "Swing";
                case RouteShape.Wheel: return "Wheel";
                default: return shape.ToString();
            }
        }
    }
}
