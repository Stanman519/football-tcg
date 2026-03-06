#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using TcgEngine;
using Assets.TcgEngine.Scripts.Effects;
using Object = UnityEngine.Object;
using Assets.TcgEngine.Scripts.Gameplay;

/// <summary>
/// Creates all play enhancer card, ability, and effect assets.
/// Menu: First&Long → Create Play Enhancer Cards
///
/// Cards created:
///   I Formation (Run, +5 yards)
///   Perfect Pocket (Pass, +3 yards)
///   Scramble (Pass, add extra reel)
///   Two Tight End Set (Run, add extra reel)
///   Run The Damn Ball (Run, opp -2 stamina)
///   Hurry Up (Any, play exempt from count)
///   Hot Route (Short Pass, +3 yards)
///   Throw It Away (Long Pass, sack = incomplete)
///   Ball Security (Run, -1 yard + fumble tag)
///   Film Study (Any, Discover: pick 1 of 3 enhancers from deck)
/// </summary>
public static class PlayEnhancerCardBuilder
{
    const string CARDS_PATH       = "Assets/Resources/Cards/";
    const string ABILITIES_PATH   = "Assets/Resources/Abilities/";
    const string CONDITIONS_PATH  = "Assets/Resources/Conditions/";
    const string EFFECTS_PATH     = "Assets/Resources/Effects/";

    // ── Entry point ───────────────────────────────────────────────────────────

    [MenuItem("First&Long/Create Play Enhancer Cards")]
    static void Build()
    {
        if (!EditorUtility.DisplayDialog("Create Play Enhancer Cards",
            "Creates ~9 play enhancer cards + abilities + effects in Resources/.\n\nExisting assets with the same name will be overwritten.\nProceed?",
            "Create", "Cancel"))
            return;

        CreateDir(CARDS_PATH);
        CreateDir(ABILITIES_PATH);
        CreateDir(CONDITIONS_PATH);
        CreateDir(EFFECTS_PATH);

        // Shared assets (reused by multiple cards)
        ConditionOwner condOwnerSame = GetOrCreate<ConditionOwner>(CONDITIONS_PATH + "condition_owner_same.asset", asset =>
        {
            asset.oper = ConditionOperatorBool.IsTrue;
        });

        EffectAddStat effectAddStat = GetOrCreate<EffectAddStat>(EFFECTS_PATH + "effect_add_stat.asset", _ => { });

        // ── Build each card ───────────────────────────────────────────────────

        BuildIFormation(condOwnerSame, effectAddStat);
        BuildPerfectPocket(condOwnerSame, effectAddStat);
        BuildScramble();
        BuildTwoTightEndSet();
        BuildRunTheDamnBall();
        BuildHurryUp();
        BuildHotRoute(condOwnerSame, effectAddStat);
        BuildThrowItAway();
        BuildBallSecurity(condOwnerSame, effectAddStat);
        BuildFilmStudy();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Done",
            "10 play enhancer cards created.\n\nCheck Assets/Resources/Cards/ and Assets/Resources/Abilities/.\n\nSave your project to keep changes.", "OK");
    }

    // ── Individual card builders ──────────────────────────────────────────────

    static void BuildIFormation(ConditionOwner condOwnerSame, EffectAddStat effectAddStat)
    {
        var ability = MakeStatAbility("ability_i_formation",
            AbilityTarget.AllCardsBoard,
            new ConditionData[] { condOwnerSame },
            effectAddStat,
            StatusTypePrintedStats.AddedRunBonus, 5, 1,
            "I Formation", "Run play gains +5 yards this play.");

        MakeCard("00300_ENH_I_Formation", "I Formation",
            CardType.OffensivePlayEnhancer,
            new PlayType[] { PlayType.Run },
            new AbilityData[] { ability },
            "Run play gains +5 yards this play.", 0);
    }

    static void BuildPerfectPocket(ConditionOwner condOwnerSame, EffectAddStat effectAddStat)
    {
        // Two chained abilities: boosts both short AND deep pass bonuses
        var abilShort = MakeStatAbility("ability_perfect_pocket_short",
            AbilityTarget.AllCardsBoard,
            new ConditionData[] { condOwnerSame },
            effectAddStat,
            StatusTypePrintedStats.AddedShortPassBonus, 3, 1,
            "Perfect Pocket Short", "Pass plays gain +3 yards this play.");

        var abilDeep = MakeStatAbility("ability_perfect_pocket_deep",
            AbilityTarget.AllCardsBoard,
            new ConditionData[] { condOwnerSame },
            effectAddStat,
            StatusTypePrintedStats.AddedDeepPassBonus, 3, 1,
            "Perfect Pocket Deep", "");

        MakeCard("00301_ENH_Perfect_Pocket", "Perfect Pocket",
            CardType.OffensivePlayEnhancer,
            new PlayType[] { PlayType.ShortPass, PlayType.LongPass },
            new AbilityData[] { abilShort, abilDeep },
            "Pass play gains +3 yards this play.\nRequired: Any Pass", 0);
    }

    static void BuildScramble()
    {
        var ability = MakeAbility("ability_scramble",
            AbilityTarget.None, null, null,
            "Scramble", "Adds an extra slot machine reel this play.");
        var effect = AddSubAsset<EffectAddSlotSymbol>(ability, "EffectScrambleReel", e =>
        {
            e.addReel = true;
            e.duration = 0;
        });
        ability.effects = new EffectData[] { effect };
        EditorUtility.SetDirty(ability);

        MakeCard("00302_ENH_Scramble", "Scramble",
            CardType.OffensivePlayEnhancer,
            new PlayType[] { PlayType.ShortPass, PlayType.LongPass },
            new AbilityData[] { ability },
            "Adds an extra slot reel this play.\nRequired: Any Pass", 0);
    }

    static void BuildTwoTightEndSet()
    {
        var ability = MakeAbility("ability_two_te_set",
            AbilityTarget.None, null, null,
            "Two Tight End Set", "Adds an extra slot machine reel this play.");
        var effect = AddSubAsset<EffectAddSlotSymbol>(ability, "EffectTwoTEReel", e =>
        {
            e.addReel = true;
            e.duration = 0;
        });
        ability.effects = new EffectData[] { effect };
        EditorUtility.SetDirty(ability);

        MakeCard("00303_ENH_Two_TE_Set", "Two Tight End Set",
            CardType.OffensivePlayEnhancer,
            new PlayType[] { PlayType.Run },
            new AbilityData[] { ability },
            "Adds an extra slot reel this play.\nRequired: Run", 0);
    }

    static void BuildRunTheDamnBall()
    {
        var ability = MakeAbility("ability_run_the_damn_ball",
            AbilityTarget.None, null, null,
            "Run The Damn Ball", "Opponent's players lose 2 stamina this play.");
        var effect = AddSubAsset<EffectModifyStamina>(ability, "EffectDrainStamina", e =>
        {
            e.value = 2;
            e.removeStamina = true;
            e.target = EffectTarget.Opponent;
        });
        ability.effects = new EffectData[] { effect };
        EditorUtility.SetDirty(ability);

        MakeCard("00304_ENH_Run_The_Damn_Ball", "Run The Damn Ball",
            CardType.OffensivePlayEnhancer,
            new PlayType[] { PlayType.Run },
            new AbilityData[] { ability },
            "Opponent's players lose 2 stamina this play.\nRequired: Run", 0);
    }

    static void BuildHurryUp()
    {
        var ability = MakeAbility("ability_hurry_up",
            AbilityTarget.None, null, null,
            "Hurry Up", "This play does not count toward plays remaining in the half.");
        var effect = AddSubAsset<EffectModifyPlayCount>(ability, "EffectExemptPlay", e =>
        {
            e.modifier = PlayCountModifier.ExemptThisPlay;
            e.value = 1;
        });
        ability.effects = new EffectData[] { effect };
        EditorUtility.SetDirty(ability);

        MakeCard("00305_ENH_Hurry_Up", "Hurry Up",
            CardType.OffensivePlayEnhancer,
            new PlayType[0],
            new AbilityData[] { ability },
            "This play does not count toward the plays in the half.", 0);
    }

    static void BuildHotRoute(ConditionOwner condOwnerSame, EffectAddStat effectAddStat)
    {
        var ability = MakeStatAbility("ability_hot_route",
            AbilityTarget.AllCardsBoard,
            new ConditionData[] { condOwnerSame },
            effectAddStat,
            StatusTypePrintedStats.AddedShortPassBonus, 3, 1,
            "Hot Route", "Completed short pass gains +3 yards this play.");

        MakeCard("00306_ENH_Hot_Route", "Hot Route",
            CardType.OffensivePlayEnhancer,
            new PlayType[] { PlayType.ShortPass },
            new AbilityData[] { ability },
            "Short pass gains +3 yards this play.\nRequired: Short Pass", 0);
    }

    static void BuildThrowItAway()
    {
        // No ability effect — handled by GameLogicService.ResolvePassFailEvents
        // checking player.PlayEnhancer?.card_id == "enh_throw_it_away"
        var ability = MakeAbility("ability_throw_it_away",
            AbilityTarget.None, null, null,
            "Throw It Away", "If you would be sacked, the play is an incomplete pass instead.");
        ability.effects = new EffectData[0];
        EditorUtility.SetDirty(ability);

        MakeCard("00307_ENH_Throw_It_Away", "Throw It Away",
            CardType.OffensivePlayEnhancer,
            new PlayType[] { PlayType.LongPass },
            new AbilityData[] { ability },
            "If sacked, the play becomes an incomplete pass instead.\nRequired: Long Pass", 0);
    }

    static void BuildBallSecurity(ConditionOwner condOwnerSame, EffectAddStat effectAddStat)
    {
        // -1 yard penalty, but flags fumble protection (checked at resolution via card_id)
        var ability = MakeStatAbility("ability_ball_security",
            AbilityTarget.AllCardsBoard,
            new ConditionData[] { condOwnerSame },
            effectAddStat,
            StatusTypePrintedStats.AddedRunBonus, -1, 1,
            "Ball Security", "Run plays lose 1 yard but are protected from fumbles this play.");

        MakeCard("00308_ENH_Ball_Security", "Ball Security",
            CardType.OffensivePlayEnhancer,
            new PlayType[] { PlayType.Run },
            new AbilityData[] { ability },
            "Run play: -1 yard. Fumble protection this play.\nRequired: Run", 0);
    }

    static void BuildFilmStudy()
    {
        var ability = MakeAbility("ability_film_study",
            AbilityTarget.CardSelectorDiscover, null, null,
            "Film Study", "Search your deck for 3 play enhancers, keep 1, shuffle the rest back.");

        var effect = AddSubAsset<EffectDiscover>(ability, "EffectFilmStudyDiscover", e =>
        {
            e.filterType = CardType.OffensivePlayEnhancer;
            e.drawCount = 3;
        });
        ability.effects = new EffectData[] { effect };
        EditorUtility.SetDirty(ability);

        MakeCard("00309_ENH_Film_Study", "Film Study",
            CardType.OffensivePlayEnhancer,
            new PlayType[0],
            new AbilityData[] { ability },
            "Search your deck for 3 play enhancers, keep 1, shuffle the rest back.", 0);
    }

    // ── Asset factories ───────────────────────────────────────────────────────

    /// Creates an AbilityData that uses EffectAddStat (reads affected_stat and stat_bonus_amount from ability).
    static AbilityData MakeStatAbility(
        string id,
        AbilityTarget target,
        ConditionData[] conditionsTarget,
        EffectAddStat effectAddStat,
        StatusTypePrintedStats stat, int bonus, int duration,
        string title, string desc)
    {
        var ability = MakeAbility(id, target, conditionsTarget, null, title, desc);
        ability.affected_stat     = stat;
        ability.stat_bonus_amount = bonus;
        ability.duration          = duration;
        ability.effects           = new EffectData[] { effectAddStat };
        EditorUtility.SetDirty(ability);
        return ability;
    }

    /// Creates and saves an AbilityData asset with OnPlay trigger.
    static AbilityData MakeAbility(
        string id,
        AbilityTarget target,
        ConditionData[] conditionsTarget,
        ConditionData[] conditionsTrigger,
        string title, string desc)
    {
        string path = ABILITIES_PATH + id + ".asset";
        AbilityData ability = AssetDatabase.LoadAssetAtPath<AbilityData>(path);
        if (ability == null)
        {
            ability = ScriptableObject.CreateInstance<AbilityData>();
            AssetDatabase.CreateAsset(ability, path);
        }

        ability.id                 = id;
        ability.trigger            = AbilityTrigger.OnPlay;
        ability.target             = target;
        ability.conditions_target  = conditionsTarget ?? new ConditionData[0];
        ability.conditions_trigger = conditionsTrigger ?? new ConditionData[0];
        ability.title              = title;
        ability.desc               = desc;
        ability.value              = 0;
        ability.effects            = new EffectData[0];
        ability.status             = new StatusData[0];
        ability.chain_abilities    = new AbilityData[0];
        EditorUtility.SetDirty(ability);
        return ability;
    }

    /// Creates an effect sub-asset inside the ability asset file.
    static T AddSubAsset<T>(AbilityData ability, string assetName, System.Action<T> configure)
        where T : EffectData
    {
        string abilityPath = AssetDatabase.GetAssetPath(ability);

        // Remove old sub-assets of this type to avoid accumulation on re-run
        Object[] existing = AssetDatabase.LoadAllAssetsAtPath(abilityPath);
        foreach (Object obj in existing)
        {
            if (obj is T && obj.name == assetName)
            {
                AssetDatabase.RemoveObjectFromAsset(obj);
                break;
            }
        }

        T effect = ScriptableObject.CreateInstance<T>();
        effect.name = assetName;
        configure(effect);
        AssetDatabase.AddObjectToAsset(effect, ability);
        EditorUtility.SetDirty(ability);
        return effect;
    }

    /// Creates and saves a CardData asset.
    static CardData MakeCard(
        string filename,
        string title,
        CardType type,
        PlayType[] requiredPlays,
        AbilityData[] abilities,
        string text,
        int manaCost)
    {
        string path = CARDS_PATH + filename + ".asset";
        CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);
        if (card == null)
        {
            card = ScriptableObject.CreateInstance<CardData>();
            AssetDatabase.CreateAsset(card, path);
        }

        // Derive id from filename (lowercase, underscores)
        card.id             = filename.ToLower().Replace(" ", "_");
        card.title          = title;
        card.type           = type;
        card.required_plays = requiredPlays;
        card.abilities      = abilities;
        card.text           = text;
        card.mana           = manaCost;

        // Play enhancers are not player cards — zero out all stat fields
        card.stamina        = 0;
        card.grit           = 0;
        card.run_bonus                    = 0;
        card.short_pass_bonus             = 0;
        card.deep_pass_bonus              = 0;
        card.run_coverage_bonus           = 0;
        card.short_pass_coverage_bonus    = 0;
        card.deep_pass_coverage_bonus     = 0;

        EditorUtility.SetDirty(card);
        return card;
    }

    // ── Shared asset helpers ──────────────────────────────────────────────────

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
}
#endif
