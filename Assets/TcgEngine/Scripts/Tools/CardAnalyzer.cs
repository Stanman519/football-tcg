using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.IO;
using TcgEngine;
using UnityEditor;

namespace TcgEngine.Toolbox
{
    /// <summary>
    /// Analyzes card synergies with coaches and generates reports
    /// Run this from Unity editor menu: Tools > Card Analyzer
    /// </summary>
    public class CardAnalyzer : MonoBehaviour
    {
        [Header("Output")]
        public string outputPath = "Assets/CardAnalysis.csv";
        public bool generateMarkdown = true;

        [MenuItem("Tools/Analyze Cards")]
        public static void AnalyzeCards()
        {
            var analyzer = new GameObject("CardAnalyzer").AddComponent<CardAnalyzer>();
            analyzer.Analyze();
        }

        public void Analyze()
        {
            // Load all card data
            CardData.Load();
            
            var report = new System.Text.StringBuilder();
            var csv = new System.Text.StringBuilder();
            
            report.AppendLine("# Card Analysis Report");
            report.AppendLine();
            report.AppendLine($"Generated: {System.DateTime.Now}");
            report.AppendLine();
            
            // CSV Header
            csv.AppendLine("CardId,Name,Position,IsStar,Tags,RunBonus,ShortPassBonus,DeepPassBonus,CoachSynergies,Notes");
            
            // Analyze each card
            foreach (var card in CardData.card_list)
            {
                string tags = GetCardTags(card);
                string coachSynergies = GetCoachSynergies(card);
                string notes = GetCardNotes(card);
                
                csv.AppendLine($"{card.id},{card.title},{card.type},{tags},{GetRunBonus(card)},{GetShortPassBonus(card)},{GetDeepPassBonus(card)},\"{coachSynergies}\",\"{notes}\"");
            }
            
            report.AppendLine("## Synergy Matrix");
            report.AppendLine();
            report.AppendLine("| Coach | Strong Synergy Cards | Weak/No Synergy |");
            report.AppendLine("|-------|---------------------|-----------------|");
            
            // Coach synergies
            var coachSynergies2 = GetCoachSynergyMatrix();
            foreach (var coach in coachSynergies2)
            {
                report.AppendLine($"| {coach.Key} | {coach.Value.strong} | {coach.Value.weak} |");
            }
            
            // Write files
            string csvPath = Application.dataPath + "/../" + outputPath;
            File.WriteAllText(csvPath, csv.ToString());
            Debug.Log($"Card analysis saved to: {csvPath}");
            
            if (generateMarkdown)
            {
                string mdPath = outputPath.Replace(".csv", ".md");
                File.WriteAllText(Application.dataPath + "/../" + mdPath, report.ToString());
                Debug.Log($"Markdown report saved to: {mdPath}");
            }
        }

        private string GetCardTags(CardData card)
        {
            List<string> tags = new List<string>();
            
            // Add position tags
            if (card.type == CardType.OffensivePlayer)
            {
                tags.Add(card.id.Contains("OL") ? "OL" : 
                        card.id.Contains("QB") ? "QB" :
                        card.id.Contains("RB") ? "RB" :
                        card.id.Contains("WR") ? "WR" :
                        card.id.Contains("TE") ? "TE" : "OFF");
            }
            else if (card.type == CardType.DefensivePlayer)
            {
                tags.Add(card.id.Contains("DL") ? "DL" :
                        card.id.Contains("LB") ? "LB" :
                        card.id.Contains("DB") ? "DB" : "DEF");
            }
            
            // Check traits for additional tags
            foreach (var ability in card.abilities)
            {
                string trigger = ability.trigger.ToString();
                if (!tags.Contains(trigger))
                    tags.Add(trigger);
            }
            
            return string.Join(", ", tags);
        }

        private string GetCoachSynergies(CardData card)
        {
            List<string> synergies = new List<string>();
            
            // Risk-Taker (4th down, slot manipulation)
            if (HasTag(card, "DOWN") || HasTag(card, "SLOT"))
                synergies.Add("Risk-Taker");
            
            // Red Zone Guru
            if (HasTag(card, "RZ") || HasTag(card, "RUN") || HasTag(card, "SP") || HasTag(card, "DP"))
                synergies.Add("Red Zone Guru");
            
            // Turnover Tactician
            if (HasTag(card, "TURNOVER") || HasTag(card, "FUMBLE"))
                synergies.Add("Turnover Tactician");
            
            // Sideline Motivator
            if (HasTag(card, "STARQTY"))
                synergies.Add("Sideline Motivator");
            
            // Balanced Approach
            if (HasTag(card, "SEQ") || HasTag(card, "RUN") || HasTag(card, "SP") || HasTag(card, "DP"))
                synergies.Add("Balanced Approach");
            
            return string.Join(", ", synergies);
        }

        private bool HasTag(CardData card, string tag)
        {
            // Check card traits and abilities for tags
            foreach (var ability in card.abilities)
            {
                if (ability.id.Contains(tag) || ability.desc.Contains(tag))
                    return true;
            }
            return false;
        }

        private Dictionary<string, (string strong, string weak)> GetCoachSynergyMatrix()
        {
            return new Dictionary<string, (string strong, string weak)>
            {
                { "Risk-Taker", ("SLOTMANIP cards, 4th down cards", "No specific synergy") },
                { "Red Zone Guru", ("RUN/SP/DP bonuses", "No field position cards yet") },
                { "Turnover Tactician", ("TURNOVER, FUMBLE, LOSS_PREV", "Good coverage") },
                { "Momentum Shifter", ("SEQ cards", "Need after-TD trigger") },
                { "High-Octane Offense", ("NONE - gap!", "No FG-related cards") },
                { "Sideline Motivator", ("STARQTY, star players", "Good coverage") },
                { "Balanced Approach", ("RUN, SP, DP, SEQ", "Need different-type logic") },
                { "Play Fast!", ("DOWN cards", "Need play counter fix") }
            };
        }

        private string GetCardNotes(CardData card)
        {
            List<string> notes = new List<string>();
            
            if (card.abilities.Length == 0)
                notes.Add("No abilities");
            
            // Check for complex abilities
            foreach (var ability in card.abilities)
            {
                if (ability.conditions_trigger != null && ability.conditions_trigger.Length > 2)
                    notes.Add("Complex trigger");
            }
            
            return string.Join("; ", notes);
        }

        private string GetRunBonus(CardData card) => "0"; // Would extract from card data
        private string GetShortPassBonus(CardData card) => "0";
        private string GetDeepPassBonus(CardData card) => "0";
    }
}
