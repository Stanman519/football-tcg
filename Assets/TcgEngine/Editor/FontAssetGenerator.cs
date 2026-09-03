#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using TMPro;

/// <summary>
/// Generates TMP SDF font assets from TTF files in Assets/TcgEngine/Fonts/.
/// Menu: First&Long > Generate TMP Font Assets
/// </summary>
public static class FontAssetGenerator
{
    [MenuItem("First&Long/Generate TMP Font Assets")]
    static void GenerateAll()
    {
        string[] fontFiles = new[]
        {
            "Assets/TcgEngine/Fonts/SpaceGrotesk-Bold.ttf",
            "Assets/TcgEngine/Fonts/Inter-Bold.ttf",
            "Assets/TcgEngine/Fonts/Inter-Medium.ttf",
            "Assets/TcgEngine/Fonts/NotoSans-Condensed.ttf",
        };

        int created = 0;
        foreach (var ttfPath in fontFiles)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (font == null)
            {
                Debug.LogWarning($"[FontGen] TTF not found: {ttfPath}");
                continue;
            }

            string sdfPath = ttfPath.Replace(".ttf", " SDF.asset");
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(sdfPath);
            if (existing != null)
            {
                Debug.Log($"[FontGen] Already exists: {sdfPath}");
                continue;
            }

            // Create font asset using TMP's built-in method
            var fontAsset = TMP_FontAsset.CreateFontAsset(font);
            if (fontAsset == null)
            {
                Debug.LogError($"[FontGen] Failed to create SDF for: {ttfPath}");
                continue;
            }

            AssetDatabase.CreateAsset(fontAsset, sdfPath);
            // Also save the atlas texture as a sub-asset
            if (fontAsset.atlasTexture != null)
            {
                fontAsset.atlasTexture.name = font.name + " Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
            }
            if (fontAsset.material != null)
            {
                fontAsset.material.name = font.name + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[FontGen] Created: {sdfPath}");
            created++;
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Font Generation",
            $"Created {created} TMP font asset(s). Check console for details.", "OK");
    }
}
#endif
