using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TcgEngine.UI
{
    /// <summary>
    /// Retro full-screen event animation overlay.
    ///
    /// Inspector wiring (all optional — self-builds if left null):
    ///   overlayBackground   — Image covering full screen (dark BG)
    ///   eventText           — Text that types out letter by letter
    ///   subText             — Smaller text beneath (optional)
    ///
    /// Font: assign "Press Start 2P" (Google Font .ttf) in Inspector for retro look.
    ///
    /// Canvas sortingOrder is set to 100 at runtime so it renders above everything.
    ///
    /// Public API:
    ///   ShowEvent(string text, Color accentColor)
    /// </summary>
    public class BigPlayOverlay : MonoBehaviour
    {
        [Header("UI References (optional — auto-built if null)")]
        public Image overlayBackground;
        public Text eventText;
        public Text subText;

        [Header("Settings")]
        [Tooltip("Assign 'Press Start 2P' or similar retro font. Falls back to built-in.")]
        public Font retroFont;
        public float typewriterDelay = 0.04f;   // seconds per character
        public float holdDuration = 2.0f;
        public float fadeDuration = 0.5f;

        private Canvas overlayCanvas;
        private Coroutine activeCoroutine;
        private bool selfBuilt = false;

        // Built refs
        private Image builtBg;
        private Text builtText;

        void Awake()
        {
            bool hasRef = overlayBackground != null || eventText != null;
            if (!hasRef)
                BuildSelfContainedOverlay();
            else
                SetVisible(false);
        }

        /// <summary>Show a big-play event with retro typewriter animation.</summary>
        public void ShowEvent(string text, Color accentColor)
        {
            if (activeCoroutine != null)
                StopCoroutine(activeCoroutine);
            activeCoroutine = StartCoroutine(AnimateEvent(text, accentColor));
        }

        private IEnumerator AnimateEvent(string text, Color accentColor)
        {
            // Resolve refs
            Image bg = overlayBackground ?? builtBg;
            Text display = eventText ?? builtText;

            if (bg == null || display == null) yield break;

            // --- 1. Activate ---
            SetVisible(true);
            bg.color = new Color(0.05f, 0.05f, 0.05f, 0.92f);
            display.color = accentColor;
            display.text = "";
            if (subText != null) subText.text = "";

            // --- 2. Typewriter reveal ---
            for (int i = 0; i <= text.Length; i++)
            {
                display.text = text.Substring(0, i);
                yield return new WaitForSeconds(typewriterDelay);
            }

            // --- 3. Hold ---
            yield return new WaitForSeconds(holdDuration);

            // --- 4. Fade out ---
            float elapsed = 0f;
            Color bgStart = bg.color;
            Color textStart = display.color;
            while (elapsed < fadeDuration)
            {
                float t = elapsed / fadeDuration;
                float alpha = 1f - t;
                bg.color = new Color(bgStart.r, bgStart.g, bgStart.b, bgStart.a * alpha);
                display.color = new Color(textStart.r, textStart.g, textStart.b, alpha);
                elapsed += Time.deltaTime;
                yield return null;
            }

            // --- 5. Deactivate ---
            SetVisible(false);
            activeCoroutine = null;
        }

        private void SetVisible(bool show)
        {
            Image bg = overlayBackground ?? builtBg;
            Text display = eventText ?? builtText;
            if (bg != null) bg.gameObject.SetActive(show);
            if (display != null) display.gameObject.SetActive(show);
            if (subText != null) subText.gameObject.SetActive(show);
        }

        // -----------------------------------------------------------------------
        // Self-contained overlay builder
        // -----------------------------------------------------------------------
        private void BuildSelfContainedOverlay()
        {
            if (selfBuilt) return;
            selfBuilt = true;

            overlayCanvas = gameObject.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 100;
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();

            // Full-screen dark background
            GameObject bgGo = new GameObject("BigPlayBG");
            bgGo.transform.SetParent(transform, false);
            builtBg = bgGo.AddComponent<Image>();
            builtBg.color = new Color(0.05f, 0.05f, 0.05f, 0.92f);
            RectTransform bgRT = bgGo.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;

            // Event text (centered)
            GameObject textGo = new GameObject("BigPlayText");
            textGo.transform.SetParent(bgGo.transform, false);
            builtText = textGo.AddComponent<Text>();
            builtText.font = retroFont != null ? retroFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            builtText.fontSize = retroFont != null ? 52 : 42;
            builtText.color = Color.white;
            builtText.alignment = TextAnchor.MiddleCenter;
            builtText.supportRichText = false;
            RectTransform textRT = textGo.GetComponent<RectTransform>();
            textRT.anchorMin = new Vector2(0.05f, 0.35f);
            textRT.anchorMax = new Vector2(0.95f, 0.65f);
            textRT.offsetMin = textRT.offsetMax = Vector2.zero;

            // Start hidden
            bgGo.SetActive(false);
            textGo.SetActive(false);
        }
    }
}
