#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using TcgEngine;
using TcgEngine.Conditions;
using Assets.TcgEngine.Scripts.Conditions;
using Assets.TcgEngine.Scripts.Effects;
using Assets.TcgEngine.Scripts.Gameplay;
using System;

/// <summary>
/// Reads a CSV and creates CardData + AbilityData ScriptableObject assets.
/// Menu: First&Long → Import Player Cards from CSV
///
/// ── CSV Column Layout ────────────────────────────────────────────────────────
///  0  card_id            e.g.  00001
///  1  Name               e.g.  Trent Hawthorne
///  2  pos                QB | RB | TE | WR | OL | DL | LB | DB
///  3  Superstar?         1 = superstar, 0 = regular
///  4  passive?           Display text shown on card (not parsed for logic)
///  5  Suit               Clubs | Diamonds | Hearts | Spades  (not yet on CardData — skipped)
///  6  Run Bonus          integer
///  7  Short Pass         integer
///  8  Deep Pass          integer
///  9  Run Coverage Bonus integer
/// 10  Short Pass Coverage Bonus  integer
/// 11  Deep Pass Coverage Bonus   integer
/// 12  Stamina            integer
/// 13  Grit               integer
///
/// Then up to 2 ability slots (each 9 cols, starting at col 14):
/// [base+0] a_trigger     OnPlay | OnRun | OnPass    (blank = OnPlay)
/// [base+1] a_target      Self | AllBoard | None      (blank = None)
/// [base+2] a_fail_event  TFL | Sack | Fumble | Interception | Incomplete | BattedPass
/// [base+3] a_cond        Condition 1 type shorthand (blank = no condition)
/// [base+4] a_cond_val    Pipe-delimited params for condition 1
/// [base+5] a_cond2       Condition 2 type shorthand — AND'd with cond 1 (blank = none)
/// [base+6] a_cond2_val   Pipe-delimited params for condition 2
/// [base+7] a_effect      Effect type shorthand (blank = no effect)
/// [base+8] a_effect_val  Pipe-delimited params for that effect
///
/// Two-condition example (Football+Helmet ≥1 each ≈ 53%):
///   a_cond=SlotIcon  a_cond_val=Football|>=1  a_cond2=SlotIcon  a_cond2_val=Helmet|>=1
///
/// ── Condition Shorthands ────────────────────────────────────────────────────
///  SlotIcon         Football|>=1   Helmet|>=2   Star|>=1   Wrench|>=1
///  BoardCount       OL|Self|>=3    WR|Opp|<=2
///  CompareCounts    OL|DL|>   (caster position > opponent position count)
///  Down             4   (equal to down 4)
///  PlayType         Run | ShortPass | LongPass
///  LastPlayType     Run | ShortPass | LongPass
///  Consecutive      Run|>=2   (consecutive plays of that type)
///  FieldPos         InRedZone | InOpponentTerritory
///  HandCount        >=6 | <=2 | ==0
///  StarCount        OL|Self|>=2   WR|Self|>=3   (position|player|count)
///  PlaysLeft        <=5 | <=3
///  CoverageGuess    correct | wrong
///  Random           10 | 25 | 50   (percent chance, integer)
///  FirstSnap        (no params)
///  FirstPlay        (no params)
///  LastPassIncomplete (no params)
///  YardageGained    >=15 | <=7 | <0 | Last|>=10 | Last|<0
///                   "Last|" prefix checks the PREVIOUS play's yardage
///  GritCompare      higher | lower | tied | higher_or_tied
///
/// ── Effect Shorthands ───────────────────────────────────────────────────────
///  AddStat          RunBonus|5|1   (stat|amount|duration)
///                   Stat names: RunBonus ShortPassBonus DeepPassBonus
///                               RunCoverage ShortCoverage DeepCoverage
///                               Stamina Grit
///  AddStatPerIcon   Star|DeepPassBonus|3   (icon|stat|amountPerIcon)
///  ModifyStamina    Self|-1 | Opp|-2 | Team|-1
///  DrawCard         1 | 2
///  Knockout         (no params — removes target from board)
///  Charge           Helmet|20|RunBonus|20|1   (icon|threshold|stat|bonus|duration)
///                   icon=None for event-mode (fixed +1 per activation); duration=2 to carry into next play
///  StackStat        RunBonus|Run|2|0   (stat|stackPlayType|bonusPerStack|maxBonus)
///                   Scales bonus with consecutive streak; 0 maxBonus = uncapped
///                   e.g. "RunBonus|Run|1|5" → +1 per consecutive run, max +5
///  DiscardCard      Opponent | Self   (auto-discard, no player choice)
///  DiscardChoice    stat|bonus|dur    (player picks hand card to discard; opens CardSelectorHand UI)
///                   e.g. "RunBonus|6|1" → discard choice + +6 run this play; "" → discard only
///  MANUAL           (skip this ability slot — implement by hand in Inspector)
/// </summary>
public static class PlayerCardImporter
{
    const string CARDS_PATH      = "Assets/Resources/Cards/";
    const string ABILITIES_PATH  = "Assets/Resources/Abilities/";
    const string CONDITIONS_PATH = "Assets/Resources/Conditions/";
    const string EFFECTS_PATH    = "Assets/Resources/Effects/";

    // Shared assets reused across all imported cards
    static ConditionOwner   s_condOwnerSame;
    static EffectAddStat    s_effectAddStat;

    // ── Entry point ──────────────────────────────────────────────────────────

    [MenuItem("First&Long/Import Player Cards from CSV")]
    static void ImportMenu()
    {
        string path = EditorUtility.OpenFilePanel("Select Player Card CSV", "Assets/Resources", "csv");
        if (string.IsNullOrEmpty(path)) return;
        Run(path);
    }

    public static void Run(string csvPath)
    {
        CreateDir(CARDS_PATH);
        CreateDir(ABILITIES_PATH);
        CreateDir(CONDITIONS_PATH);
        CreateDir(EFFECTS_PATH);

        s_spriteCache.Clear();  // Refresh art lookup each run

        // Shared assets
        s_condOwnerSame = GetOrCreate<ConditionOwner>(CONDITIONS_PATH + "condition_owner_same.asset",
            c => c.oper = ConditionOperatorBool.IsTrue);

        s_effectAddStat = GetOrCreate<EffectAddStat>(EFFECTS_PATH + "effect_add_stat.asset", _ => { });

        string[] lines;
        using (var fs = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var sr = new StreamReader(fs, Encoding.UTF8))
            lines = sr.ReadToEnd().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        int imported = 0, skipped = 0, manual = 0;

        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] cols = ParseCSVLine(line);
            if (cols.Length < 14)
            {
                Debug.LogWarning($"[Importer] Row {i + 1}: only {cols.Length} columns — skipped.");
                skipped++;
                continue;
            }

            try
            {
                int manualCount = ProcessRow(cols, i + 1);
                manual += manualCount;
                imported++;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Importer] Row {i + 1}: {ex.Message}\n{ex.StackTrace}");
                skipped++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Import Complete",
            $"Imported: {imported} cards\nSkipped: {skipped} rows\nManual abilities: {manual}\n\nCheck Console for details.", "OK");
    }

    // ── Row processor ────────────────────────────────────────────────────────

    static int ProcessRow(string[] cols, int lineNum)
    {
        string cardId    = cols[0].Trim();
        string name      = cols[1].Trim();
        string pos       = cols[2].Trim().ToUpper();
        bool superstar   = cols[3].Trim() == "1";
        string text      = cols[4].Trim();
        CardSuit suit    = ParseSuit(cols[5]);
        int runBonus     = ParseInt(cols[6]);
        int shortPass    = ParseInt(cols[7]);
        int deepPass     = ParseInt(cols[8]);
        int runCov       = ParseInt(cols[9]);
        int shortCov     = ParseInt(cols[10]);
        int deepCov      = ParseInt(cols[11]);
        int stamina      = ParseInt(cols[12]);
        int grit         = ParseInt(cols[13]);

        if (string.IsNullOrEmpty(cardId))
        {
            Debug.LogWarning($"[Importer] Row {lineNum}: empty card_id — skipped.");
            return 0;
        }

        var abilities = new List<AbilityData>();
        int manualCount = 0;

        // Up to 2 ability slots, each 9 columns wide starting at col 14
        for (int slot = 0; slot < 2; slot++)
        {
            int b = 14 + slot * 9;
            if (cols.Length <= b) break;

            string trigger   = Get(cols, b + 0);
            string target    = Get(cols, b + 1);
            string failEvt   = Get(cols, b + 2);
            string condType  = Get(cols, b + 3);
            string condVal   = Get(cols, b + 4);
            string cond2Type = Get(cols, b + 5);
            string cond2Val  = Get(cols, b + 6);
            string effType   = Get(cols, b + 7);
            string effVal    = Get(cols, b + 8);

            if (string.IsNullOrEmpty(trigger)) continue;

            if (trigger.ToUpper() == "MANUAL" || condType.ToUpper() == "MANUAL" || effType.ToUpper() == "MANUAL")
            {
                Debug.LogWarning($"[Importer] {cardId} ability {slot + 1}: marked MANUAL — wire in Inspector.");
                manualCount++;
                continue;
            }

            string abilId = $"ability_{cardId}_a{slot + 1}";
            var ability = BuildAbility(abilId, cardId, trigger, target, failEvt,
                                       condType, condVal, cond2Type, cond2Val,
                                       effType, effVal);
            if (ability != null)
                abilities.Add(ability);
        }

        // Create or update the CardData asset
        string assetName = cardId.ToLower().Replace(" ", "_");
        string cardPath  = CARDS_PATH + assetName + ".asset";
        CardData card = AssetDatabase.LoadAssetAtPath<CardData>(cardPath);
        if (card == null)
        {
            card = ScriptableObject.CreateInstance<CardData>();
            AssetDatabase.CreateAsset(card, cardPath);
        }

        card.id                           = assetName;
        card.title                        = name;
        card.type                         = PosToCardType(pos);
        card.playerPosition               = PosToPositionGrp(pos);
        card.isSuperstar                  = superstar;
        card.suit                         = suit;
        card.text                         = text;
        card.stamina                      = stamina;
        card.grit                         = grit;
        card.run_bonus                    = runBonus;
        card.short_pass_bonus             = shortPass;
        card.deep_pass_bonus              = deepPass;
        card.run_coverage_bonus           = runCov;
        card.short_pass_coverage_bonus    = shortCov;
        card.deep_pass_coverage_bonus     = deepCov;
        card.abilities                    = abilities.ToArray();
        card.deckbuilding                 = true;
        card.required_plays               = new PlayType[0];
        card.slotRequirements             = new SlotRequirement[0];

        AssignCardArt(card, pos);
        EditorUtility.SetDirty(card);
        Debug.Log($"[Importer] Created: {cardId} ({name}, {pos})");
        return manualCount;
    }

    // ── Ability builder ──────────────────────────────────────────────────────

    static AbilityData BuildAbility(string abilId, string cardId,
        string trigger, string target, string failEvt,
        string condType, string condVal,
        string cond2Type, string cond2Val,
        string effType, string effVal)
    {
        string path = ABILITIES_PATH + abilId + ".asset";
        AbilityData ability = AssetDatabase.LoadAssetAtPath<AbilityData>(path);
        if (ability == null)
        {
            ability = ScriptableObject.CreateInstance<AbilityData>();
            AssetDatabase.CreateAsset(ability, path);
        }

        // Clear old sub-assets on reimport
        RemoveSubAssets(path);

        ability.id                 = abilId;
        ability.title              = abilId;
        ability.trigger            = ParseTrigger(trigger);
        ability.target             = ParseTarget(target);
        ability.failEventType      = ParseFailEvent(failEvt);
        ability.conditions_trigger = new ConditionData[0];
        ability.conditions_target  = new ConditionData[0];
        ability.effects            = new EffectData[0];
        ability.status             = new StatusData[0];
        ability.chain_abilities    = new AbilityData[0];
        ability.value              = 0;

        // Build up to 2 AND'd trigger conditions
        var triggerConds = new List<ConditionData>();
        if (!string.IsNullOrEmpty(condType))
        {
            ConditionData cond = BuildConditionSubAsset(ability, condType, condVal);
            if (cond != null) triggerConds.Add(cond);
            else Debug.LogWarning($"[Importer] {cardId}: unknown condition '{condType}' — skipped.");
        }
        if (!string.IsNullOrEmpty(cond2Type))
        {
            ConditionData cond2 = BuildConditionSubAsset(ability, cond2Type, cond2Val);
            if (cond2 != null)
            {
                // Rename so sub-asset names don't collide when both are SlotIcon
                cond2.name = cond2.name.Replace("Cond_", "Cond2_");
                triggerConds.Add(cond2);
            }
            else Debug.LogWarning($"[Importer] {cardId}: unknown condition2 '{cond2Type}' — skipped.");
        }
        ability.conditions_trigger = triggerConds.ToArray();

        // Target condition — add ConditionOwner for AllBoard so buffs only hit own cards
        if (ability.target == AbilityTarget.AllCardsBoard)
            ability.conditions_target = new ConditionData[] { s_condOwnerSame };

        // Effect
        if (!string.IsNullOrEmpty(effType))
        {
            bool effectApplied = ApplyEffect(ability, effType, effVal);
            if (!effectApplied)
                Debug.LogWarning($"[Importer] {cardId}: unknown effect '{effType}' — skipped.");
        }

        EditorUtility.SetDirty(ability);
        return ability;
    }

    // ── Condition sub-asset factory ──────────────────────────────────────────

    static ConditionData BuildConditionSubAsset(AbilityData ability, string condType, string condVal)
    {
        string[] p = condVal.Split('|');

        switch (condType)
        {
            case "SlotIcon":
            {
                // params: Football|>=1  or  Helmet|>=2
                var c = AddSubAsset<ConditionSlotIconCountTrigger>(ability, $"Cond_{condType}");
                c.icon_type            = ParseSlotIcon(Get(p, 0));
                c.oper                 = ParseOperatorInt(Get(p, 1, ">="));
                c.required_icon_count  = ParseIntFrom(Get(p, 1, ">=1"), 1);
                return c;
            }

            case "BoardCount":
            {
                // params: OL|Self|>=3
                var c = AddSubAsset<ConditionBoardPositionCount>(ability, $"Cond_{condType}");
                c.positionGroup = ParsePositionGrp(Get(p, 0));
                c.target        = ParseCondPlayerType(Get(p, 1, "Self"));
                c.oper          = ParseOperatorInt(Get(p, 2, ">="));
                c.value         = ParseIntFrom(Get(p, 2, ">=1"), 1);
                return c;
            }

            case "CompareCounts":
            {
                // params: OL|DL|>
                var c = AddSubAsset<ConditionCompareTwoBoardPositionCounts>(ability, $"Cond_{condType}");
                c.casterPosition = ParsePositionGrp(Get(p, 0));
                c.targetPosition = ParsePositionGrp(Get(p, 1));
                c.oper           = ParseOperatorInt(Get(p, 2, ">"));
                return c;
            }

            case "Down":
            {
                // params: 4   or   >=3
                var c = AddSubAsset<ConditionGameDown>(ability, $"Cond_{condType}");
                string raw = Get(p, 0, "==4");
                c.oper          = ParseOperatorInt(raw);
                c.required_down = ParseIntFrom(raw, 1);
                return c;
            }

            case "PlayType":
            {
                // params: Run | ShortPass | LongPass
                var c = AddSubAsset<ConditionPlayType>(ability, $"Cond_{condType}");
                c.required_play = ParsePlayType(Get(p, 0));
                c.oper          = ConditionOperatorBool.IsTrue;
                return c;
            }

            case "LastPlayType":
            {
                // params: Run | ShortPass | LongPass
                var c = AddSubAsset<ConditionLastPlayType>(ability, $"Cond_{condType}");
                c.requiredPlayType = ParsePlayType(Get(p, 0));
                c.requireDifferent = false;
                return c;
            }

            case "Consecutive":
            {
                // params: Run|>=2
                var c = AddSubAsset<ConditionConsecutivePlays>(ability, $"Cond_{condType}");
                c.playType      = ParsePlayType(Get(p, 0));
                string raw      = Get(p, 1, ">=2");
                c.oper          = ParseOperatorInt(raw);
                c.requiredCount = ParseIntFrom(raw, 2);
                return c;
            }

            case "FieldPos":
            {
                // params: InRedZone  or  InOpponentTerritory
                var c = AddSubAsset<ConditionFieldPosition>(ability, $"Cond_{condType}");
                c.positionType  = ParseFieldPosition(Get(p, 0));
                c.oper          = ConditionOperatorInt.Equal;
                return c;
            }

            case "HandCount":
            {
                // params: >=6 | <=2 | ==0
                var c = AddSubAsset<ConditionHandCount>(ability, $"Cond_{condType}");
                string raw      = Get(p, 0, ">=0");
                c.oper          = ParseOperatorInt(raw);
                c.required_count = ParseIntFrom(raw, 0);
                return c;
            }

            case "StarCount":
            {
                // params: OL|Self|>=2   (position|player|count)
                var c = AddSubAsset<ConditionStarCount>(ability, $"Cond_{condType}");
                c.positionFilter = ParsePositionGrp(Get(p, 0));
                c.target         = ParseCondPlayerType(Get(p, 1, "Self"));
                string raw       = Get(p, 2, ">=1");
                c.oper           = ParseOperatorInt(raw);
                c.value          = ParseIntFrom(raw, 1);
                return c;
            }

            case "PlaysLeft":
            {
                // params: <=5
                var c = AddSubAsset<ConditionPlaysRemaining>(ability, $"Cond_{condType}");
                string raw      = Get(p, 0, "<=5");
                c.oper          = ParseOperatorInt(raw);
                c.playsThreshold = ParseIntFrom(raw, 5);
                return c;
            }

            case "CoverageGuess":
            {
                // params: correct | wrong
                var c = AddSubAsset<ConditionCoverageGuess>(ability, $"Cond_{condType}");
                c.guessCorrect  = Get(p, 0, "correct").ToLower() == "correct";
                return c;
            }

            case "Random":
            {
                // params: 25  (integer percentage)
                var c = AddSubAsset<ConditionRandom>(ability, $"Cond_{condType}");
                c.chance        = ParseInt(Get(p, 0, "50"));
                return c;
            }

            case "FirstSnap":
            {
                var c = AddSubAsset<ConditionFirstSnap>(ability, $"Cond_{condType}");
                c.firstSnapOfHalf = false; // game-wide first snap
                return c;
            }

            case "FirstPlay":
            {
                return AddSubAsset<ConditionFirstPlay>(ability, $"Cond_{condType}");
            }

            case "LastPassIncomplete":
            {
                return AddSubAsset<ConditionLastPassIncomplete>(ability, $"Cond_{condType}");
            }

            case "YardageGained":
            {
                // params: ">=15" | "<=7" | "<0" | "Last|>=10" | "Last|<0"
                // If first token is "Last", check last_play_yardage; otherwise check yardage_this_play.
                var c = AddSubAsset<ConditionYardageGained>(ability, $"Cond_{condType}");
                string raw;
                if (Get(p, 0, "").ToLower() == "last")
                {
                    c.checkLastPlay = true;
                    raw = Get(p, 1, ">=0");
                }
                else
                {
                    c.checkLastPlay = false;
                    raw = Get(p, 0, ">=0");
                }
                c.oper      = ParseOperatorInt(raw);
                c.threshold = ParseIntFrom(raw, 0);
                return c;
            }

            case "GritCompare":
            {
                // params: "higher" | "lower" | "tied" | "higher|N" (offset)
                // "higher" → caster team grit >= opponent grit  (compareToOpponent based on card side)
                // "lower"  → caster team grit <  opponent grit
                // "tied"   → caster team grit == opponent grit
                var c = AddSubAsset<TcgEngine.ConditionGritCompare>(ability, $"Cond_{condType}");
                string keyword = Get(p, 0, "higher").ToLower();
                int offset     = ParseInt(Get(p, 1, "0"));
                c.value = offset;
                // Determine side: if card is defensive, compareToOpponent=true means def > off
                // We treat "higher" always as "my side >= opponent"
                // All current grit-compare cards are defensive — defense compares its grit vs offense.
                // For offensive grit cards, flip this in the Unity Inspector.
                c.compareToOpponent = true;
                switch (keyword)
                {
                    case "higher":
                        c.oper = ConditionOperatorInt.GreaterEqual;
                        break;
                    case "lower":
                        c.oper = ConditionOperatorInt.Less;
                        break;
                    case "tied":
                        c.oper = ConditionOperatorInt.Equal;
                        break;
                    case "higher_or_tied":
                        c.oper = ConditionOperatorInt.GreaterEqual;
                        break;
                    default:
                        c.oper = ConditionOperatorInt.GreaterEqual;
                        break;
                }
                return c;
            }

            default:
                return null;
        }
    }

    // ── Effect applicator ────────────────────────────────────────────────────

    /// Returns true if the effect was recognised and applied.
    static bool ApplyEffect(AbilityData ability, string effType, string effVal)
    {
        string[] p = effVal.Split('|');

        switch (effType)
        {
            case "AddStat":
            {
                // params: RunBonus|5|1   (stat|amount|duration)
                ability.affected_stat     = ParseStat(Get(p, 0));
                ability.stat_bonus_amount = ParseInt(Get(p, 1, "0"));
                ability.duration          = ParseInt(Get(p, 2, "1"));
                ability.effects           = new EffectData[] { s_effectAddStat };
                return true;
            }

            case "AddStatPerIcon":
            {
                // params: Star|DeepPassBonus|3   (icon|stat|amountPerIcon)
                ability.affected_stat     = ParseStat(Get(p, 1));
                ability.stat_bonus_amount = ParseInt(Get(p, 2, "1"));
                ability.duration          = 1;
                var eff = AddSubAsset<EffectAddStatPerSlotIcon>(ability, "Eff_AddStatPerIcon");
                eff.iconToCount           = ParseSlotIcon(Get(p, 0));
                eff.countWilds            = false;
                ability.effects           = new EffectData[] { eff };
                return true;
            }

            case "ModifyStamina":
            {
                // params: Self|-1   Opp|-2   Team|-1
                var eff = AddSubAsset<EffectModifyStamina>(ability, "Eff_ModifyStamina");
                string targetStr = Get(p, 0, "Self");
                int amount       = ParseInt(Get(p, 1, "-1"));
                eff.removeStamina = amount < 0;
                eff.value         = Mathf.Abs(amount);
                eff.target        = targetStr.ToUpper() switch
                {
                    "OPP" or "OPPONENT" => EffectTarget.Opponent,
                    "TEAM"              => EffectTarget.Team,
                    _                   => EffectTarget.Self
                };
                ability.effects = new EffectData[] { eff };
                return true;
            }

            case "DrawCard":
            {
                // params: 1 | 2
                var eff = AddSubAsset<EffectDrawCard>(ability, "Eff_DrawCard");
                eff.count       = ParseInt(Get(p, 0, "1"));
                ability.effects = new EffectData[] { eff };
                return true;
            }

            case "Knockout":
            {
                var eff = AddSubAsset<EffectKnockout>(ability, "Eff_Knockout");
                ability.effects = new EffectData[] { eff };
                return true;
            }

            case "Charge":
            {
                // params: Icon|threshold|stat|bonus|duration
                //   Icon      = Football | Helmet | Star | Wrench | Wild | None (event-based)
                //   threshold = charge needed to trigger
                //   stat      = RunBonus | ShortCoverage | etc.  (same as AddStat)
                //   bonus     = amount applied when triggered
                //   duration  = status duration (1 = this play; 2 = carries into next play)
                // Examples:
                //   "Helmet|20|RunBonus|20|1"   Maxwell Payne: 20 helmets → +20 run
                //   "Football|8|RunBonus|5|1"   Tre Rostic:   8 footballs → +5 run
                //   "Star|5|RunCoverage|6|1"    Henry Walker: 5 stars → +6 run cov
                //   "None|3|RunCoverage|6|2"    William Ford: 3 events → +6 run cov next play
                ability.affected_stat     = ParseStat(Get(p, 2, "RunBonus"));
                ability.stat_bonus_amount = ParseInt(Get(p, 3, "0"));
                ability.duration          = ParseInt(Get(p, 4, "1"));
                var eff = AddSubAsset<EffectCharge>(ability, "Eff_Charge");
                eff.chargeIcon = ParseSlotIcon(Get(p, 0, "None"));
                eff.threshold  = ParseInt(Get(p, 1, "20"));
                ability.effects = new EffectData[] { eff };
                return true;
            }

            case "StackStat":
            {
                // params: RunBonus|Run|2|0   (stat | stackPlayType | bonusPerStack | maxBonus)
                // stat       → ability.affected_stat (same inspector field AddStat uses)
                // stackPlayType → Run | ShortPass | LongPass
                // bonusPerStack → bonus applied per play in the streak
                // maxBonus   → cap on total bonus; 0 = uncapped
                ability.affected_stat     = ParseStat(Get(p, 0, "RunBonus"));
                ability.duration          = 1;
                var eff = AddSubAsset<EffectStackStat>(ability, "Eff_StackStat");
                eff.stackPlayType = ParsePlayType(Get(p, 1, "Run"));
                eff.bonusPerStack = ParseInt(Get(p, 2, "1"));
                eff.maxBonus      = ParseInt(Get(p, 3, "0"));
                ability.effects   = new EffectData[] { eff };
                return true;
            }

            case "DiscardCard":
            {
                // params: Opponent | Self
                // Auto-discard (no player choice). Used for triggered effects like Nash Thornton.
                var eff = AddSubAsset<EffectDiscardCard>(ability, "Eff_DiscardCard");
                string who = Get(p, 0, "Opponent").ToUpper();
                eff.discardFromOpponent = who != "SELF";
                ability.effects = new EffectData[] { eff };
                return true;
            }

            case "DiscardChoice":
            {
                // params: stat|bonus|duration  (all optional — omit for discard-only, no bonus)
                //   "RunBonus|6|1"  → discard chosen card + +6 RunBonus to caster this play
                //   "Grit|3|1"      → discard chosen card + +3 Grit to caster
                //   ""              → discard chosen card only
                // Opens CardSelectorHand UI so the caster's player picks which hand card to discard.
                // Cancelling the selector skips both the discard and any bonus.
                ability.target            = AbilityTarget.CardSelectorHand;
                ability.affected_stat     = ParseStat(Get(p, 0, "None"));
                ability.stat_bonus_amount = ParseInt(Get(p, 1, "0"));
                ability.duration          = ParseInt(Get(p, 2, "1"));
                var eff = AddSubAsset<EffectDiscardCard>(ability, "Eff_DiscardChoice");
                eff.discardFromOpponent = false;
                ability.effects = new EffectData[] { eff };
                return true;
            }

            case "PreventLoss":
            {
                // params: (none needed — negateCompletely defaults to true)
                // Sets "prevent_loss" trait on the caster. GameLogicService already
                // clamps negative yardage to 0 when any board card has this trait.
                // Does NOT prevent turnovers (INTs, fumbles).
                var eff = AddSubAsset<EffectPreventLoss>(ability, "Eff_PreventLoss");
                eff.negateCompletely = true;
                ability.effects = new EffectData[] { eff };
                return true;
            }

            case "Discover":
            {
                // params: CardType|count   e.g. "OffensivePlayEnhancer|3"
                // Pulls count random cards of CardType from deck, player picks 1 → hand, rest back to deck.
                ability.target = AbilityTarget.CardSelectorDiscover;
                var eff = AddSubAsset<EffectDiscover>(ability, "Eff_Discover");
                eff.filterType = ParseCardType(Get(p, 0, "OffensivePlayEnhancer"));
                eff.drawCount  = ParseInt(Get(p, 1, "3"));
                ability.effects = new EffectData[] { eff };
                return true;
            }

            case "ReturnToHand":
            {
                // params: (none)
                // Moves the caster from board back to its owner's hand.
                // Use with StartOfTurn trigger + ConditionGameDown to auto-sit a card out on a specific down.
                var eff = AddSubAsset<EffectReturnToHand>(ability, "Eff_ReturnToHand");
                ability.effects = new EffectData[] { eff };
                return true;
            }

            default:
                return false;
        }
    }

    // ── Parsing helpers ──────────────────────────────────────────────────────

    static AbilityTrigger ParseTrigger(string s) => s.ToUpper() switch
    {
        "ONRUN"  or "ONRUNRESOLUTION"  => AbilityTrigger.OnRunResolution,
        "ONPASS" or "ONPASSRESOLUTION" => AbilityTrigger.OnPassResolution,
        "ONLIVE" or "ONLIVEBALL"       => AbilityTrigger.OnLiveBallResolution,
        "ONDISCARD"                    => AbilityTrigger.OnDiscard,
        "STARTOFTURN" or "STARTTURN"   => AbilityTrigger.StartOfTurn,
        "ENDOFTURN"   or "ENDTURN"     => AbilityTrigger.EndOfTurn,
        _                              => AbilityTrigger.OnPlay
    };

    static AbilityTarget ParseTarget(string s) => s.ToUpper() switch
    {
        "SELF"     => AbilityTarget.Self,
        "ALLBOARD" => AbilityTarget.AllCardsBoard,
        "ALLHAND"  => AbilityTarget.AllCardsHand,
        _          => AbilityTarget.None
    };

    static FailPlayEventType ParseFailEvent(string s) => s.ToUpper() switch
    {
        "TFL"          => FailPlayEventType.TackleForLoss,
        "SACK"         => FailPlayEventType.Sack,
        "FUMBLE"       => FailPlayEventType.RunnerFumble,
        "QBFUMBLE"     => FailPlayEventType.QBFumble,
        "INTERCEPTION" => FailPlayEventType.Interception,
        "INCOMPLETE"   => FailPlayEventType.IncompletePass,
        "BATTEDPASS"   => FailPlayEventType.BattedPass,
        "TIPPEDPASS"   => FailPlayEventType.TippedPass,
        _              => FailPlayEventType.None
    };

    static StatusTypePrintedStats ParseStat(string s) => s.ToUpper() switch
    {
        "RUNBONUS"      or "RUN"           => StatusTypePrintedStats.AddedRunBonus,
        "SHORTPASSBONUS"or "SHORTPASS"     => StatusTypePrintedStats.AddedShortPassBonus,
        "DEEPPASSBONUS" or "DEEPPASS"      => StatusTypePrintedStats.AddedDeepPassBonus,
        "RUNCOVERAGE"                      => StatusTypePrintedStats.AddedRunCoverageBonus,
        "SHORTCOVERAGE" or "SHORTPASSCOV"  => StatusTypePrintedStats.AddedShortPassCoverageBonus,
        "DEEPCOVERAGE"  or "DEEPPASSCOV"   => StatusTypePrintedStats.AddedDeepPassCoverageBonus,
        "STAMINA"                          => StatusTypePrintedStats.AddStamina,
        "GRIT"                             => StatusTypePrintedStats.AddGrit,
        _                                  => StatusTypePrintedStats.AddedRunBonus
    };

    static PlayType ParsePlayType(string s) => s.ToUpper() switch
    {
        "RUN"       => PlayType.Run,
        "SHORTPASS" => PlayType.ShortPass,
        "LONGPASS"  => PlayType.LongPass,
        _           => PlayType.Huddle
    };

    static PlayerPositionGrp ParsePositionGrp(string s) => s.ToUpper() switch
    {
        "QB"  => PlayerPositionGrp.QB,
        "OL"  => PlayerPositionGrp.OL,
        "RB"  => PlayerPositionGrp.RB_TE,
        "TE"  => PlayerPositionGrp.RB_TE,
        "WR"  => PlayerPositionGrp.WR,
        "DL"  => PlayerPositionGrp.DL,
        "LB"  => PlayerPositionGrp.LB,
        "DB"  => PlayerPositionGrp.DB,
        _     => PlayerPositionGrp.NONE
    };

    static ConditionPlayerType ParseCondPlayerType(string s) => s.ToUpper() switch
    {
        "OPP" or "OPPONENT" => ConditionPlayerType.Opponent,
        "BOTH"              => ConditionPlayerType.Both,
        _                   => ConditionPlayerType.Self
    };

    static SlotMachineIconType ParseSlotIcon(string s) => s.ToUpper() switch
    {
        "FOOTBALL"  => SlotMachineIconType.Football,
        "HELMET"    => SlotMachineIconType.Helmet,
        "STAR"      => SlotMachineIconType.Star,
        "WRENCH"    => SlotMachineIconType.Wrench,
        "WILD"      => SlotMachineIconType.WildCard,
        "NONE"      => SlotMachineIconType.None,
        _           => SlotMachineIconType.Football
    };

    static FieldPositionCheck ParseFieldPosition(string s) => s.ToUpper() switch
    {
        "INREDZONE"            => FieldPositionCheck.InRedZone,
        "INOPPONENTTERRITORY"  => FieldPositionCheck.InOpponentTerritory,
        "INOWNTERRITORY"       => FieldPositionCheck.InOwnTerritory,
        "GOALLINE"             => FieldPositionCheck.GoalToGo,
        "BACKEDUP"             => FieldPositionCheck.BackedUp,
        _                      => FieldPositionCheck.InRedZone
    };

    /// Parses a string like ">=3", "<=5", ">2", "<4", "==1", "!=0" into operator + int.
    static ConditionOperatorInt ParseOperatorInt(string s)
    {
        if (s.StartsWith(">=")) return ConditionOperatorInt.GreaterEqual;
        if (s.StartsWith("<=")) return ConditionOperatorInt.LessEqual;
        if (s.StartsWith(">"))  return ConditionOperatorInt.Greater;
        if (s.StartsWith("<"))  return ConditionOperatorInt.Less;
        if (s.StartsWith("!=")) return ConditionOperatorInt.NotEqual;
        return ConditionOperatorInt.Equal; // "==4" or just "4"
    }

    /// Extracts the integer part from strings like ">=3", "<=5", ">2", "4".
    static int ParseIntFrom(string s, int fallback)
    {
        string digits = s.TrimStart('>', '<', '=', '!', ' ');
        return int.TryParse(digits, out int v) ? v : fallback;
    }

    static CardType PosToCardType(string pos) => pos switch
    {
        "DL" or "LB" or "DB" => CardType.DefensivePlayer,
        _                    => CardType.OffensivePlayer
    };

    static PlayerPositionGrp PosToPositionGrp(string pos) => ParsePositionGrp(pos);

    static CardSuit ParseSuit(string s) => s?.Trim().ToUpper() switch
    {
        "CLUBS"    => CardSuit.Clubs,
        "DIAMONDS" => CardSuit.Diamonds,
        "HEARTS"   => CardSuit.Hearts,
        "SPADES"   => CardSuit.Spades,
        _          => CardSuit.None
    };

    static CardType ParseCardType(string s) => s.ToUpper() switch
    {
        "OFFENSIVEPLAYENHANCER" or "OFFENHANCER" => CardType.OffensivePlayEnhancer,
        "DEFENSIVEPLAYENHANCER" or "DEFENHANCER" => CardType.DefensivePlayEnhancer,
        "OFFENSIVEPLAYER" or "OFFENSIVE"         => CardType.OffensivePlayer,
        "DEFENSIVEPLAYER" or "DEFENSIVE"         => CardType.DefensivePlayer,
        "OFFLIVEBALL"                            => CardType.OffLiveBall,
        "DEFLIVEBALL"                            => CardType.DefLiveBall,
        "EQUIPMENT"                              => CardType.Equipment,
        _                                        => CardType.None
    };

    static int ParseInt(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        return int.TryParse(s.Trim(), out int v) ? v : 0;
    }

    // ── Sub-asset helpers ────────────────────────────────────────────────────

    static T AddSubAsset<T>(AbilityData ability, string assetName) where T : ScriptableObject
    {
        T obj = ScriptableObject.CreateInstance<T>();
        obj.name = assetName;
        AssetDatabase.AddObjectToAsset(obj, ability);
        EditorUtility.SetDirty(ability);
        return obj;
    }

    static void RemoveSubAssets(string abilityPath)
    {
        UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath(abilityPath);
        foreach (UnityEngine.Object obj in all)
        {
            if (obj == null) continue;
            if (AssetDatabase.IsMainAsset(obj)) continue;
            AssetDatabase.RemoveObjectFromAsset(obj);
        }
    }

    static T GetOrCreate<T>(string path, System.Action<T> configure) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
        configure(asset);
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static void CreateDir(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    static string Get(string[] arr, int idx, string fallback = "")
        => arr != null && idx < arr.Length ? arr[idx].Trim() : fallback;

    // ── CSV parser (handles quoted fields with commas/newlines) ──────────────

    static string[] ParseCSVLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"'); // escaped quote
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields.ToArray();
    }

    // ── Placeholder art assignment ────────────────────────────────────────────

    const string ART_ROOT = "Assets/Resources/player-test-action-shots";
    static readonly string[] SUPPORTED_EXTS = { ".jpg", ".jpeg", ".png", ".webp" };
    static readonly Dictionary<string, List<string>> s_spriteCache = new Dictionary<string, List<string>>();

    static void AssignCardArt(CardData card, string pos)
    {
        // Map importer pos token to folder name (already uppercase)
        string folder = $"{ART_ROOT}/{pos}";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogWarning($"[Importer] No art folder for pos '{pos}' at {folder} — skipping art.");
            return;
        }

        if (!s_spriteCache.TryGetValue(pos, out List<string> paths))
        {
            paths = new List<string>();
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (string guid in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string ext = Path.GetExtension(p).ToLowerInvariant();
                bool supported = System.Array.IndexOf(SUPPORTED_EXTS, ext) >= 0;
                if (supported)
                    paths.Add(p);
            }

            // Set any not-yet-sprite textures to Sprite import mode
            foreach (string p in paths)
                EnsureSpriteMode(p);

            s_spriteCache[pos] = paths;
        }

        if (paths.Count == 0)
        {
            Debug.LogWarning($"[Importer] No supported images found in {folder} — skipping art.");
            return;
        }

        // Deterministic pick: same card always gets the same image
        int idx = Mathf.Abs(card.id.GetHashCode()) % paths.Count;
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(paths[idx]);
        if (sprite == null)
        {
            Debug.LogWarning($"[Importer] Could not load sprite at {paths[idx]}");
            return;
        }

        card.art_full  = sprite;
        card.art_board = sprite;
    }

    static void EnsureSpriteMode(string assetPath)
    {
        TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (ti == null || ti.textureType == TextureImporterType.Sprite) return;

        ti.textureType     = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.mipmapEnabled   = false;
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
    }
}
#endif
