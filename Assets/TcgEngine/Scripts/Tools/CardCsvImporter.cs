using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TcgEngine;
using Assets.TcgEngine.Scripts.Gameplay;
using UnityEditor;

namespace TcgEngine.Importer
{
    /// <summary>
    /// Imports card data from CSV files into Unity ScriptableObjects
    /// Run from Unity: Tools > Import Cards from CSV
    /// </summary>
    public class CardCsvImporter : MonoBehaviour
    {
        [Header("CSV Files")]
        public TextAsset playerCardsCsv;
        public TextAsset playEnhancersCsv;
        
        [Header("Output")]
        public string cardOutputFolder = "Resources/Data/Cards";
        public string abilityOutputFolder = "Resources/Data/Abilities";

        [MenuItem("Tools/Import Cards from CSV")]
        public static void ImportCards()
        {
            var importer = new GameObject("CardCsvImporter").AddComponent<CardCsvImporter>();
            importer.Import();
        }

        public void Import()
        {
            if (playerCardsCsv != null)
            {
                ImportPlayerCards(playerCardsCsv.text);
            }
            
            if (playEnhancersCsv != null)
            {
                ImportPlayEnhancers(playEnhancersCsv.text);
            }
            
            Debug.Log("Card import complete!");
        }

        private void ImportPlayerCards(string csvContent)
        {
            string[] lines = csvContent.Split('\n');
            if (lines.Length < 2) return;

            // Parse header
            string[] headers = ParseCsvLine(lines[0]);
            
            // Create card data
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                
                string[] values = ParseCsvLine(lines[i]);
                if (values.Length < headers.Length) continue;
                
                var card = CreateCardData(headers, values);
                Debug.Log($"Created card: {card.id} - {card.title}");
            }
        }

        private void ImportPlayEnhancers(string csvContent)
        {
            string[] lines = csvContent.Split('\n');
            if (lines.Length < 2) return;

            // Parse header
            string[] headers = ParseCsvLine(lines[0]);
            
            for (int i = 1; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                
                string[] values = ParseCsvLine(lines[i]);
                if (values.Length < headers.Length) continue;
                
                CreatePlayEnhancer(headers, values);
            }
        }

        private CardData CreateCardData(string[] headers, string[] values)
        {
            // Create new CardData (you'll need to implement based on your CardData class)
            // This is a simplified version
            
            CardData card = ScriptableObject.CreateInstance<CardData>();
            
            // Map CSV columns to card properties
            for (int i = 0; i < headers.Length && i < values.Length; i++)
            {
                string header = headers[i].Trim().ToLower();
                string value = values[i].Trim();
                
                switch (header)
                {
                    case "name":
                    case "card_id":
                        card.id = value.Replace(" ", "_").ToLower();
                        break;
                    case "title":
                        card.title = value;
                        break;
                    case "pos":
                        card.SetPositionGroup(ParsePosition(value));
                        break;
                    case "superstar":
                        card.isSuperstar = value == "1" || value.ToLower() == "yes";
                        break;
                    case "passive":
                        // Create passive ability
                        if (!string.IsNullOrEmpty(value))
                        {
                            card.abilities = new AbilityData[] { CreatePassiveAbility(value) };
                        }
                        break;
                    case "runmod":
                        // Store as attack (run bonus)
                        if (int.TryParse(value, out int runMod))
                            card.attack = runMod;
                        break;
                    case "stamina":
                        if (int.TryParse(value, out int stamina))
                            card.stamina = stamina;
                        break;
                    // Add more mappings as needed
                }
            }
            
            return card;
        }

        private AbilityData CreatePassiveAbility(string description)
        {
            // Create ability from description
            // This would parse the description and create appropriate trigger/effects
            
            AbilityData ability = ScriptableObject.CreateInstance<AbilityData>();
            ability.id = "passive_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            ability.title = "Passive";
            ability.desc = description;
            ability.trigger = AbilityTrigger.None; // Passive ability
            
            return ability;
        }

        private void CreatePlayEnhancer(string[] headers, string[] values)
        {
            // Create play enhancer card data
            Debug.Log($"Creating play enhancer: {GetValue(headers, values, "name")}");
        }

        private string GetValue(string[] headers, string[] values, string targetHeader)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                if (headers[i].Trim().ToLower() == targetHeader.ToLower() && i < values.Length)
                    return values[i].Trim();
            }
            return "";
        }

        private PlayerPositionGrp ParsePosition(string pos)
        {
            pos = pos.ToUpper().Trim();
            
            if (pos.Contains("OL")) return PlayerPositionGrp.OL;
            if (pos.Contains("QB")) return PlayerPositionGrp.QB;
            if (pos.Contains("RB") || pos.Contains("TE")) return PlayerPositionGrp.RB_TE;
            if (pos.Contains("WR")) return PlayerPositionGrp.WR;
            if (pos.Contains("DL")) return PlayerPositionGrp.DL;
            if (pos.Contains("LB")) return PlayerPositionGrp.LB;
            if (pos.Contains("DB")) return PlayerPositionGrp.DB;
            
            return PlayerPositionGrp.NONE;
        }

        private string[] ParseCsvLine(string line)
        {
            List<string> result = new List<string>();
            bool inQuotes = false;
            string current = "";
            
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current);
                    current = "";
                }
                else
                {
                    current += c;
                }
            }
            
            result.Add(current);
            return result.ToArray();
        }
    }
}
