using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TcgEngine.UI
{
    /// <summary>
    /// Single source for overlay-panel look: palette, type scale, and the build helpers every
    /// code-built panel was duplicating. Change a color or size HERE, not in a panel.
    /// Font sizes are in 1920x1080 canvas-reference units.
    /// </summary>
    public static class UITheme
    {
        // ── Palette ──
        public static readonly Color Bg        = new Color32(14, 14, 14, 255);    // #0E0E0E panel background
        public static readonly Color Surface   = new Color32(26, 26, 26, 255);    // #1A1A1A raised card
        public static readonly Color Offense   = new Color32(170, 255, 220, 255); // #AAFFDC mint  — offense / "yours"
        public static readonly Color Defense   = new Color32(255, 113, 108, 255); // #FF716C coral — defense / "theirs"
        public static readonly Color Accent    = new Color32(0, 227, 253, 255);   // #00E3FD cyan  — info highlight
        public static readonly Color Gold      = new Color32(255, 215, 80, 255);  // #FFD750 neutral result / headers
        public static readonly Color Score     = new Color32(255, 191, 0, 255);   // #FFBF00 touchdowns
        public static readonly Color Positive  = new Color32(100, 255, 160, 255); // #64FFA0 yards gained
        public static readonly Color Negative  = new Color32(255, 100, 100, 255); // #FF6464 yards lost / fail events
        public static readonly Color TextMain  = new Color32(230, 230, 230, 255);
        public static readonly Color TextDim   = new Color32(173, 170, 170, 255); // #ADAAAA

        public const string PositiveHex = "#64FFA0";
        public const string NegativeHex = "#FF6464";
        public const string DimHex      = "#A0A0A0";

        // ── Type scale ──
        public const float SizeLabel    = 26f;  // small caps tags ("YOUR LIVE BALL")
        public const float SizeBody     = 32f;  // rows, descriptions
        public const float SizeTitle    = 44f;  // names
        public const float SizeHeadline = 88f;  // the one number/word the panel is about

        public const float PanelAlpha = 0.9f;

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
        public static Color PanelBg(float alpha = PanelAlpha) => WithAlpha(Bg, alpha);

        // ── Build helpers ──

        /// <summary>Standard overlay canvas: ScreenSpaceOverlay, scales with 1920x1080.</summary>
        public static Canvas BuildOverlayCanvas(GameObject go, int sortingOrder, bool raycaster = true)
        {
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            if (raycaster) go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static void StretchFill(GameObject go)
        {
            SetAnchors(go, Vector2.zero, Vector2.one);
        }

        public static void SetAnchors(GameObject go, Vector2 min, Vector2 max)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static TextMeshProUGUI MakeTMP(Transform parent, string name, float size, Color color,
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
            t.raycastTarget = false;
            SetAnchors(go, anchorMin, anchorMax);
            return t;
        }
    }
}
