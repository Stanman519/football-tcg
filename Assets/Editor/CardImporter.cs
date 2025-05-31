using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using TcgEngine;
using Assets.TcgEngine.Scripts.Gameplay;

public class CardImporter : EditorWindow
{
    private TextAsset csvFile;
    private static Dictionary<string, PlayerPositionGrp> _posDict = new Dictionary<string, PlayerPositionGrp>
    {
        {"QB", PlayerPositionGrp.QB },
        {"RB", PlayerPositionGrp.RB_TE },
        {"TE", PlayerPositionGrp.RB_TE },
        {"WR", PlayerPositionGrp.WR },
        {"DL", PlayerPositionGrp.DL },
        {"DB", PlayerPositionGrp.DB },
        {"LB", PlayerPositionGrp.LB },
        {"P", PlayerPositionGrp.P },
        {"OL", PlayerPositionGrp.OL },
        {"K", PlayerPositionGrp.K }
    };
    private static List<string> off_pos_list = new List<string> { "QB", "RB", "TE", "WR", "OL" };
    private static List<string> def_pos_list = new List<string> { "LB", "DL", "DB" };
    private static List<string> spec_team_list = new List<string> { "K", "P" };


    [MenuItem("Tools/Import Football Cards")]
    public static void ShowWindow()
    {
        GetWindow<CardImporter>("Import Football Cards");
    }

    private void OnGUI()
    {
        GUILayout.Label("CSV File for Card Data", EditorStyles.boldLabel);
        csvFile = (TextAsset)EditorGUILayout.ObjectField("CSV File", csvFile, typeof(TextAsset), false);

        if (GUILayout.Button("Import Cards"))
        {
            if (csvFile != null)
            {
                ImportCards(csvFile);
            }
            else
            {
                Debug.LogError("Please assign a CSV file before importing.");
            }
        }
    }

    private static void ImportCards(TextAsset csvFile)
    {
        string[] lines = csvFile.text.Split('\n');

        string assetPath = "Assets/Resources/Cards/";

        if (!AssetDatabase.IsValidFolder(assetPath))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "Cards");
        }

        for (int i = 1; i < lines.Length; i++)  // Skip header
        {
            string[] values = lines[i].Split(',');

/*            if (values.Length < 10)  // Adjust this based on your CSV columns
                continue;*/

            string id = $"{i.ToString("D5")}_{values[1]}_{values[0].Replace(" ", "_").Replace("\"", "")}";
            string name = values[0].Trim();
            CardType type = getCardTypeFromPos(values[1]);
            string displayPos = values[1].Trim().ToUpper();
            PlayerPositionGrp pos = getPosGroupFromPos(values[1]);
            int stamina = int.Parse(values[10].Trim());
            int grit = int.Parse(values[11].Trim());
            int runBonus = int.Parse(values[4].Trim());
            int shortPassBonus = int.Parse(values[5].Trim());
            int deepPassBonus = int.Parse(values[6].Trim());
            int runCoverageBonus = int.Parse(values[7].Trim());
            int shortPassCoverageBonus = int.Parse(values[8].Trim());
            int DeepPassCoverageBonus = int.Parse(values[9].Trim());
            bool isSuperstar = int.Parse(values[2]) == 1;
            string title = $"{displayPos} {name}";

            AbilityData[] abilities = new AbilityData[1]
            {
                new AbilityData
                {
                    desc = values[3]
                }
            };
            // Create a new CardData asset
            CardData newCard = ScriptableObject.CreateInstance<CardData>();

            newCard.id = id;
            newCard.title = title;
            newCard.type = type;
            newCard.stamina = stamina;
            newCard.grit = grit;
            newCard.run_bonus = runBonus;
            newCard.short_pass_bonus = shortPassBonus;
            newCard.deep_pass_bonus = deepPassBonus;
            newCard.run_coverage_bonus = runCoverageBonus;
            newCard.short_pass_coverage_bonus = shortPassCoverageBonus;
            newCard.deep_pass_coverage_bonus = DeepPassCoverageBonus;
            newCard.name = name;
            newCard.playerPosition = pos;
            newCard.isSuperstar = isSuperstar;
            newCard.abilities = abilities;

            newCard.text = abilities[0].desc;
            // Save the asset
            string fileName = $"{assetPath}{id}.asset";
            AssetDatabase.CreateAsset(newCard, fileName);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

    }

    private static CardType getCardTypeFromPos(string postition)
    {
        var fixedPos = postition.Trim().ToUpper();
        if (def_pos_list.Contains(fixedPos)) return CardType.DefensivePlayer;
        if (off_pos_list.Contains(fixedPos)) return CardType.OffensivePlayer;
        if (spec_team_list.Contains(fixedPos)) return CardType.SpecialTeamsPlayer;
        return CardType.None;
    }
    private static PlayerPositionGrp getPosGroupFromPos(string position)
    {
        return _posDict[position.ToUpper().Trim()];
    }
}