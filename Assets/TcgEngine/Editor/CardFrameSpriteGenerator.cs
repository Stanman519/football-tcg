using UnityEngine;
using UnityEditor;
using System.IO;

public class CardFrameSpriteGenerator
{
    private static readonly string outputDir = "Assets/Resources/Sprites/CardFrame";

    [MenuItem("First&Long/Generate Card Frame Sprites")]
    public static void GenerateAll()
    {
        if (!Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        GenerateCardBg();
        GeneratePositionBadgeBg();
        GenerateStatStripBg();
        GenerateStaminaPillBg();
        GenerateGritPillBg();
        GenerateAbilityPanelBg();
        GenerateGradientOverlay();
        GenerateSuitIcons();
        GenerateStatIcons();
        GenerateSlotIcons();
        GenerateAccentCorner();
        GenerateSlashBar();

        AssetDatabase.Refresh();
        Debug.Log("[CardFrame] All sprites generated in " + outputDir);
    }

    static void GenerateCardBg()
    {
        int w = 64, h = 64;
        var fill = new Color32(0x1a, 0x1a, 0x1a, 0xFF);
        var border = new Color32(0x48, 0x48, 0x47, 0x4D);
        int bw = 2, cr = 4;
        var tex = MakeTex(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool inCorner = IsInCorner(x, y, w, h, cr);
                bool isBorder = x < bw || x >= w - bw || y < bw || y >= h - bw;
                tex.SetPixel(x, y, inCorner ? Color.clear : (isBorder ? (Color)border : (Color)fill));
            }
        Save(tex, "card_bg", new Vector4(cr + bw, cr + bw, cr + bw, cr + bw));
    }

    static void GeneratePositionBadgeBg()
    {
        int w = 80, h = 80;
        float skew = 12f;
        int extra = (int)(h * Mathf.Tan(skew * Mathf.Deg2Rad));
        int totalW = w + extra;
        var tex = MakeTex(totalW, h);
        for (int y = 0; y < h; y++)
        {
            float offset = (h - 1 - y) * Mathf.Tan(skew * Mathf.Deg2Rad);
            for (int x = 0; x < totalW; x++)
            {
                float lx = x - offset;
                tex.SetPixel(x, y, (lx >= 0 && lx < w) ? Color.white : Color.clear);
            }
        }
        Save(tex, "position_badge_bg", Vector4.zero);
    }

    static void GenerateStatStripBg()
    {
        int w = 128, h = 48;
        float skew = 12f;
        var fill = new Color(0.173f, 0.173f, 0.173f, 0.8f);
        int extra = (int)(h * Mathf.Tan(skew * Mathf.Deg2Rad));
        int totalW = w + extra;
        var tex = MakeTex(totalW, h);
        for (int y = 0; y < h; y++)
        {
            float offset = (h - 1 - y) * Mathf.Tan(skew * Mathf.Deg2Rad);
            for (int x = 0; x < totalW; x++)
            {
                float lx = x - offset;
                tex.SetPixel(x, y, (lx >= 0 && lx < w) ? fill : Color.clear);
            }
        }
        Save(tex, "stat_strip_bg", Vector4.zero);
    }

    static void GenerateStaminaPillBg()
    {
        CreatePill("stamina_pill_bg", new Color32(0x26, 0x26, 0x26, 0xFF), new Color32(0x81, 0xEC, 0xFF, 0xFF));
    }

    static void GenerateGritPillBg()
    {
        CreatePill("grit_pill_bg", new Color32(0x26, 0x26, 0x26, 0xFF), new Color32(0xFF, 0x71, 0x6C, 0xFF));
    }

    static void CreatePill(string name, Color32 fill, Color32 leftBorder)
    {
        int w = 96, h = 24, bw = 3, cr = 3;
        var tex = MakeTex(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool inCorner = IsInCorner(x, y, w, h, cr);
                tex.SetPixel(x, y, inCorner ? Color.clear : (x < bw ? (Color)leftBorder : (Color)fill));
            }
        Save(tex, name, new Vector4(bw + cr, cr, cr, cr));
    }

    static void GenerateAbilityPanelBg()
    {
        int w = 128, h = 64;
        var fill = new Color32(0x13, 0x13, 0x13, 0xFF);
        var topBorder = new Color32(0xAA, 0xFF, 0xDC, 0x1A);
        var tex = MakeTex(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, (y >= h - 1) ? (Color)topBorder : (Color)fill);
        Save(tex, "ability_panel_bg", new Vector4(4, 4, 4, 3));
    }

    static void GenerateGradientOverlay()
    {
        int w = 8, h = 128;
        var tex = MakeTex(w, h);
        var bottom = new Color(0.055f, 0.055f, 0.055f, 1f);
        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1);
            Color c = Color.Lerp(bottom, Color.clear, t);
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, c);
        }
        Save(tex, "gradient_overlay", Vector4.zero);
    }

    static void GenerateSuitIcons()
    {
        CreateSuitIcon("suit_hearts", 0);
        CreateSuitIcon("suit_spades", 1);
        CreateSuitIcon("suit_diamonds", 2);
        CreateSuitIcon("suit_clubs", 3);
    }

    static void CreateSuitIcon(string name, int shape)
    {
        int size = 32;
        var tex = MakeTex(size, size);
        float half = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x - half) / half;
                float ny = (y - half) / half;
                bool inside = false;

                if (shape == 0) // Heart
                {
                    float hx = nx;
                    float hy = -ny + 0.2f;
                    inside = Mathf.Pow(hx * hx + hy * hy - 0.3f, 3) - hx * hx * hy * hy * hy < 0;
                }
                else if (shape == 1) // Spade
                {
                    float sx = nx;
                    float sy = ny - 0.15f;
                    inside = Mathf.Pow(sx * sx + sy * sy - 0.3f, 3) - sx * sx * sy * sy * sy < 0;
                    if (Mathf.Abs(nx) < 0.12f && ny < -0.3f) inside = true;
                }
                else if (shape == 2) // Diamond
                {
                    inside = Mathf.Abs(nx) + Mathf.Abs(ny) < 0.75f;
                }
                else if (shape == 3) // Club
                {
                    float r = 0.28f;
                    var p = new Vector2(nx, ny);
                    inside = Vector2.Distance(p, new Vector2(0, 0.3f)) < r;
                    inside |= Vector2.Distance(p, new Vector2(-0.28f, -0.05f)) < r;
                    inside |= Vector2.Distance(p, new Vector2(0.28f, -0.05f)) < r;
                    if (Mathf.Abs(nx) < 0.1f && ny < -0.1f) inside = true;
                }
                tex.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
        Save(tex, name, Vector4.zero);
    }

    static void GenerateStatIcons()
    {
        // Lightning bolt
        int s = 32;
        var tex = MakeTex(s, s);
        Vector2[] bolt = {
            new Vector2(0.55f, 1.0f), new Vector2(0.35f, 0.55f), new Vector2(0.55f, 0.55f),
            new Vector2(0.45f, 0.0f), new Vector2(0.65f, 0.45f), new Vector2(0.45f, 0.45f)
        };
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, PtInPoly((float)x / s, (float)y / s, bolt) ? Color.white : Color.clear);
        Save(tex, "stat_lightning", Vector4.zero);

        // Shield
        tex = MakeTex(s, s);
        float half = s / 2f;
        for (int y = 0; y < s; y++)
        {
            float ny = (float)y / s;
            float wf = ny > 0.4f ? 0.8f : 0.8f * (1f - Mathf.Pow((0.4f - ny) / 0.4f, 2));
            float hw = s * wf * 0.5f;
            for (int x = 0; x < s; x++)
                tex.SetPixel(x, y, Mathf.Abs(x - half) < hw ? Color.white : Color.clear);
        }
        Save(tex, "stat_shield", Vector4.zero);
    }

    static void GenerateSlotIcons()
    {
        CreateCircle("slot_football", 16, new Color32(0xAA, 0xFF, 0xDC, 0xFF));
        CreateCircle("slot_helmet", 16, new Color32(0x81, 0xEC, 0xFF, 0xFF));
        CreateCircle("slot_star", 16, new Color32(0xFF, 0xD7, 0x00, 0xFF));
        CreateCircle("slot_wildcard", 16, new Color32(0xFF, 0xFF, 0xFF, 0xFF));
        CreateCircle("slot_wrench", 16, new Color32(0xFF, 0x71, 0x6C, 0xFF));
    }

    static void CreateCircle(string name, int size, Color32 color)
    {
        var tex = MakeTex(size, size);
        var center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 1;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                if (dist < radius) tex.SetPixel(x, y, color);
                else if (dist < radius + 1) tex.SetPixel(x, y, new Color(color.r / 255f, color.g / 255f, color.b / 255f, radius + 1 - dist));
                else tex.SetPixel(x, y, Color.clear);
            }
        Save(tex, name, Vector4.zero);
    }

    static void GenerateAccentCorner()
    {
        int s = 64;
        var tex = MakeTex(s, s);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float t = Mathf.Clamp01((float)(x + (s - y)) / (s + s));
                float a = Mathf.Clamp01(t - 0.4f) * 0.5f;
                tex.SetPixel(x, y, new Color(0.667f, 1f, 0.863f, a));
            }
        Save(tex, "accent_corner", Vector4.zero);
    }

    static void GenerateSlashBar()
    {
        int w = 4, h = 16;
        var c = new Color32(0x48, 0x48, 0x47, 0xFF);
        var tex = MakeTex(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, c);
        Save(tex, "slash_bar", Vector4.zero);
    }

    // Helpers
    static Texture2D MakeTex(int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var clear = new Color[w * h];
        t.SetPixels(clear);
        return t;
    }

    static bool IsInCorner(int x, int y, int w, int h, int r)
    {
        if (x < r && y < r) return Vector2.Distance(new Vector2(x, y), new Vector2(r, r)) > r;
        if (x >= w - r && y < r) return Vector2.Distance(new Vector2(x, y), new Vector2(w - r - 1, r)) > r;
        if (x < r && y >= h - r) return Vector2.Distance(new Vector2(x, y), new Vector2(r, h - r - 1)) > r;
        if (x >= w - r && y >= h - r) return Vector2.Distance(new Vector2(x, y), new Vector2(w - r - 1, h - r - 1)) > r;
        return false;
    }

    static bool PtInPoly(float px, float py, Vector2[] poly)
    {
        bool inside = false;
        int j = poly.Length - 1;
        for (int i = 0; i < poly.Length; j = i++)
        {
            if ((poly[i].y > py) != (poly[j].y > py) &&
                px < (poly[j].x - poly[i].x) * (py - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        }
        return inside;
    }

    static void Save(Texture2D tex, string name, Vector4 border)
    {
        tex.Apply();
        byte[] png = tex.EncodeToPNG();
        string path = outputDir + "/" + name + ".png";
        File.WriteAllBytes(path, png);
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            if (border != Vector4.zero)
                importer.spriteBorder = border;
            importer.SaveAndReimport();
        }
    }
}
