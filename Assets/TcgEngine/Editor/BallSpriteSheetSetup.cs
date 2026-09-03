#pragma warning disable CS0618
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Auto-creates BallSpriteSheet SO if it doesn't exist.
/// Runs on domain reload via InitializeOnLoadMethod.
/// Also available as Tools > Setup Ball Sprite Sheet.
/// </summary>
[InitializeOnLoad]
public class BallSpriteSheetSetup
{
    static BallSpriteSheetSetup()
    {
        // Delay to ensure assets are loaded
        EditorApplication.delayCall += AutoSetup;
    }

    [MenuItem("Tools/Setup Ball Sprite Sheet")]
    public static void MenuSetup() => AutoSetup();

    static void AutoSetup()
    {
        string soPath = "Assets/Resources/football/BallSpriteSheet.asset";
        var existing = AssetDatabase.LoadAssetAtPath<BallSpriteSheet>(soPath);
        if (existing != null)
        {
            Debug.Log("[BallSetup] BallSpriteSheet already exists, skipping.");
            return;
        }

        // Load sliced sprites
        string fumblePath = "Assets/Resources/football/Football fumble animation sprite sheet.png";
        string spiralPath = "Assets/Resources/football/Spiraling football motion sequence.png";

        var fumbleSprites = LoadSpritesOrdered(fumblePath);
        var spiralSprites = LoadSpritesOrdered(spiralPath);

        if (fumbleSprites.Length < 12)
        {
            Debug.LogWarning($"[BallSetup] Fumble sheet has {fumbleSprites.Length} sprites (expected 12). Skipping auto-setup.");
            return;
        }
        if (spiralSprites.Length < 10)
        {
            Debug.LogWarning($"[BallSetup] Spiral sheet has {spiralSprites.Length} sprites (expected 10). Skipping auto-setup.");
            return;
        }

        var so = ScriptableObject.CreateInstance<BallSpriteSheet>();
        so.heldFrames = new Sprite[] { fumbleSprites[0], fumbleSprites[1], fumbleSprites[2] };
        so.heldFPS = 4f;
        so.spiralFrames = spiralSprites;
        so.spiralFPS = 12f;
        so.tumbleFrames = fumbleSprites;
        so.tumbleFPS = 10f;
        so.looseFrames = new Sprite[] { fumbleSprites[4], fumbleSprites[5], fumbleSprites[6], fumbleSprites[7] };
        so.looseFPS = 6f;
        so.onGroundSprite = fumbleSprites[5];

        AssetDatabase.CreateAsset(so, soPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[BallSetup] Created BallSpriteSheet at {soPath}");
    }

    static Sprite[] LoadSpritesOrdered(string path)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        var sprites = new List<Sprite>();
        foreach (var a in assets)
            if (a is Sprite s)
                sprites.Add(s);
        sprites.Sort((a, b) => ExtractIndex(a.name).CompareTo(ExtractIndex(b.name)));
        return sprites.ToArray();
    }

    static int ExtractIndex(string name)
    {
        int last = name.LastIndexOf('_');
        if (last >= 0 && int.TryParse(name.Substring(last + 1), out int idx))
            return idx;
        return 0;
    }
}
#pragma warning restore CS0618
