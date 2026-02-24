using UnityEngine;
using UnityEditor;
using TcgEngine; // CardData namespace
using System.Linq;

/// <summary>
/// Editor window: assign a single Sprite to CardData assets located in Assets/Resources/Cards only.
/// </summary>
public class ApplyTestCardArt : EditorWindow
{
    private Sprite testSprite;
    private bool applyArtFull = true;
    private bool applyArtBoard = true;
    private string targetFolder = "Assets/Resources/Cards";

    [MenuItem("Tools/Card Data/Apply Test Art")]
    public static void ShowWindow() => GetWindow<ApplyTestCardArt>("Apply Test Card Art");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Apply sprite to CardData in Assets/Resources/Cards", EditorStyles.boldLabel);
        testSprite = (Sprite)EditorGUILayout.ObjectField("Test Sprite", testSprite, typeof(Sprite), false);
        applyArtFull = EditorGUILayout.Toggle("Set art_full", applyArtFull);
        applyArtBoard = EditorGUILayout.Toggle("Set art_board", applyArtBoard);

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(testSprite == null || (!applyArtFull && !applyArtBoard)))
        {
            if (GUILayout.Button("Apply to CardData in Resources/Cards"))
            {
                if (EditorUtility.DisplayDialog("Confirm",
                    $"Assign '{testSprite.name}' to {(applyArtFull ? "art_full " : "")}{(applyArtBoard ? "art_board" : "")} for all CardData under {targetFolder}?",
                    "Yes", "Cancel"))
                {
                    ApplyToFolder();
                }
            }
        }

        if (GUILayout.Button("Count CardData in folder"))
        {
            int count = CountCardDataInFolder();
            EditorUtility.DisplayDialog("CardData count", $"{count} CardData assets found in {targetFolder}.", "OK");
        }
    }

    int CountCardDataInFolder()
    {
        var guids = AssetDatabase.FindAssets("t:CardData", new[] { targetFolder });
        return guids.Length;
    }

    void ApplyToFolder()
    {
        int modified = 0;
        string[] guids = AssetDatabase.FindAssets("t:CardData", new[] { targetFolder });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);
            if (card == null) continue;

            Undo.RecordObject(card, "Apply Test Card Art");

            if (applyArtFull) card.art_full = testSprite;
            if (applyArtBoard) card.art_board = testSprite;

            EditorUtility.SetDirty(card);
            modified++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Done", $"Updated {modified} CardData assets in {targetFolder}.", "OK");
    }
}