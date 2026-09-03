using UnityEngine;
using UnityEditor;
using TMPro;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace TcgEngine.Editor
{
    /// <summary>
    /// Builds a TMP_SpriteAsset from the canonical slot icon PNGs.
    /// Run after replacing any icon: First&Long > Rebuild Slot Icon Sprite Asset
    /// </summary>
    public static class SlotIconSpriteAssetBuilder
    {
        private static readonly string[] iconNames = { "football", "helmet", "star", "wild", "wrench" };
        private static readonly string sourceFolder = "Assets/Resources/SlotMachine";
        private static readonly string atlasPath = "Assets/Resources/SlotMachine/SlotIconAtlas.png";
        private static readonly string spriteAssetPath = "Assets/TextMesh Pro/Resources/Sprite Assets/SlotIcons.asset";

        [MenuItem("First&Long/Rebuild Slot Icon Sprite Asset")]
        public static void Build()
        {
            // Load source textures
            var textures = new List<Texture2D>();
            foreach (var name in iconNames)
            {
                string path = $"{sourceFolder}/slot-{name}-icon.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    bool changed = false;
                    if (!importer.isReadable) { importer.isReadable = true; changed = true; }
                    if (importer.textureType != TextureImporterType.Sprite)
                    { importer.textureType = TextureImporterType.Sprite; changed = true; }
                    if (changed) importer.SaveAndReimport();
                }

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null)
                {
                    Debug.LogError($"[SlotIconBuilder] Missing: {path}");
                    return;
                }
                textures.Add(tex);
            }

            // Pack into atlas (uniform grid — all icons get same cell size)
            int cellSize = 0;
            foreach (var t in textures)
                cellSize = Mathf.Max(cellSize, Mathf.Max(t.width, t.height));

            int cols = textures.Count;
            int atlasWidth = cols * cellSize;
            int atlasHeight = cellSize;

            var atlas = new Texture2D(atlasWidth, atlasHeight, TextureFormat.RGBA32, false);
            // Clear to transparent
            var clear = new Color32[atlasWidth * atlasHeight];
            atlas.SetPixels32(clear);

            for (int i = 0; i < textures.Count; i++)
            {
                var src = textures[i];
                int xOff = i * cellSize + (cellSize - src.width) / 2;
                int yOff = (cellSize - src.height) / 2;

                var pixels = src.GetPixels32();
                for (int y = 0; y < src.height; y++)
                    for (int x = 0; x < src.width; x++)
                        atlas.SetPixel(xOff + x, yOff + y, pixels[y * src.width + x]);
            }
            atlas.Apply();

            // Save atlas PNG
            var dir = Path.GetDirectoryName(atlasPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(atlasPath, atlas.EncodeToPNG());
            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceUpdate);
            Object.DestroyImmediate(atlas);

            // Set atlas import settings
            var atlasImporter = AssetImporter.GetAtPath(atlasPath) as TextureImporter;
            if (atlasImporter != null)
            {
                atlasImporter.textureType = TextureImporterType.Sprite;
                atlasImporter.spriteImportMode = SpriteImportMode.Multiple;
                atlasImporter.isReadable = true;
                atlasImporter.mipmapEnabled = false;
                atlasImporter.filterMode = FilterMode.Bilinear;
                atlasImporter.textureCompression = TextureImporterCompression.Uncompressed;

                // Define sprite rects
                var spriteSheet = new SpriteMetaData[iconNames.Length];
                for (int i = 0; i < iconNames.Length; i++)
                {
                    var srcTex = textures[i];
                    spriteSheet[i] = new SpriteMetaData
                    {
                        name = iconNames[i],
                        rect = new Rect(i * cellSize, 0, cellSize, cellSize),
                        alignment = (int)SpriteAlignment.Center,
                        pivot = new Vector2(0.5f, 0.5f)
                    };
                }
                atlasImporter.spritesheet = spriteSheet;
                atlasImporter.SaveAndReimport();
            }

            // Load the atlas texture for the sprite asset
            var atlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);

            // Create or update TMP_SpriteAsset
            var spriteAssetDir = Path.GetDirectoryName(spriteAssetPath);
            if (!Directory.Exists(spriteAssetDir)) Directory.CreateDirectory(spriteAssetDir);

            var spriteAsset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(spriteAssetPath);
            if (spriteAsset == null)
            {
                spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
                AssetDatabase.CreateAsset(spriteAsset, spriteAssetPath);
            }

            spriteAsset.spriteSheet = atlasTexture;

            // Build glyph and character tables
            var glyphs = new List<TMP_SpriteGlyph>();
            var characters = new List<TMP_SpriteCharacter>();

            for (int i = 0; i < iconNames.Length; i++)
            {
                var glyph = new TMP_SpriteGlyph
                {
                    index = (uint)i,
                    metrics = new UnityEngine.TextCore.GlyphMetrics(cellSize, cellSize, 0, cellSize * 0.8f, cellSize),
                    glyphRect = new UnityEngine.TextCore.GlyphRect(i * cellSize, 0, cellSize, cellSize),
                    scale = 1.0f
                };
                glyphs.Add(glyph);

                var character = new TMP_SpriteCharacter
                {
                    name = iconNames[i],
                    glyphIndex = (uint)i,
                    glyph = glyph,
                    scale = 1.0f
                };
                characters.Add(character);
            }

            // Setters are internal — use reflection to bypass
            var glyphProp = typeof(TMP_SpriteAsset).GetProperty("spriteGlyphTable",
                BindingFlags.Public | BindingFlags.Instance);
            glyphProp.SetValue(spriteAsset, glyphs);

            var charProp = typeof(TMP_SpriteAsset).GetProperty("spriteCharacterTable",
                BindingFlags.Public | BindingFlags.Instance);
            charProp.SetValue(spriteAsset, characters);

            spriteAsset.UpdateLookupTables();
            EditorUtility.SetDirty(spriteAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Wire into TMP Settings as fallback
            WireIntoTMPSettings(spriteAsset);

            Debug.Log($"[SlotIconBuilder] Built sprite asset with {iconNames.Length} icons. Cell size: {cellSize}px");
        }

        private static void WireIntoTMPSettings(TMP_SpriteAsset spriteAsset)
        {
            var settings = TMP_Settings.instance;
            if (settings == null)
            {
                Debug.LogWarning("[SlotIconBuilder] TMP_Settings not found — assign SlotIcons sprite asset manually.");
                return;
            }

            // Use serialized object to set the default sprite asset
            var so = new SerializedObject(settings);
            var defaultSpriteProp = so.FindProperty("m_defaultSpriteAsset");
            if (defaultSpriteProp != null)
            {
                defaultSpriteProp.objectReferenceValue = spriteAsset;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
                Debug.Log("[SlotIconBuilder] Set as TMP default sprite asset.");
            }
            else
            {
                Debug.LogWarning("[SlotIconBuilder] Could not find m_defaultSpriteAsset on TMP_Settings. Assign manually.");
            }
        }
    }
}
