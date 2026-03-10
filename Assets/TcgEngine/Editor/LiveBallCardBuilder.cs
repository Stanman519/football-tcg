#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using TcgEngine;
using Assets.TcgEngine.Scripts.Effects;
using Object = UnityEngine.Object;

/// <summary>
/// Creates all live ball card, ability, and effect assets.
/// Menu: First&amp;Long > Create Live Ball Cards
///
/// Offense (10): Juke Move, Stiff Arm, Speed Burst, Ball Security, Grit Up,
///               Second Wind, In the Zone, Smart Play, Momentum, Second Effort
/// Defense (10): Ankle Tackle, Form Tackle, Gang Tackle, Strip the Ball, Ball Hawk,
///               Big Ol Lick, Lights Out, Blanket Coverage, Wear Them Down, Disruption
/// </summary>
public static class LiveBallCardBuilder
{
    const string CARDS_PATH      = "Assets/Resources/Cards/";
    const string ABILITIES_PATH  = "Assets/Resources/Abilities/";
    const string CONDITIONS_PATH = "Assets/Resources/Conditions/";
    const string EFFECTS_PATH    = "Assets/Resources/Effects/";

    [MenuItem("First&Long/Create Live Ball Cards")]
    static void Build()
    {
        if (!EditorUtility.DisplayDialog("Create Live Ball Cards",
            "Creates 20 live ball cards + abilities + effects in Resources/.\n\nExisting assets with the same name will be overwritten.\nProceed?",
            "Create", "Cancel"))
            return;

        CreateDir(CARDS_PATH);
        CreateDir(ABILITIES_PATH);
        CreateDir(CONDITIONS_PATH);
        CreateDir(EFFECTS_PATH);

        // Shared assets
        var condOwnerSame = GetOrCreate<ConditionOwner>(CONDITIONS_PATH + "condition_owner_same.asset", c =>
        {
            c.oper = ConditionOperatorBool.IsTrue;
        });

        var effectYardage = GetOrCreate<EffectYardageModifier>(EFFECTS_PATH + "effect_yardage_modifier.asset", _ => { });
        var effectDrawCard = GetOrCreate<EffectDrawCard>(EFFECTS_PATH + "effect_draw_card.asset", e => { e.count = 1; });

        // Offense
        BuildJukeMove(effectYardage);
        BuildStiffArm(effectYardage);
        BuildSpeedBurst(effectYardage);
        BuildBallSecurity();
        BuildGritUp(condOwnerSame);
        BuildSecondWind();
        BuildInTheZone();
        BuildSmartPlay(effectDrawCard);
        BuildMomentum(effectYardage, effectDrawCard);
        BuildSecondEffort(effectYardage);

        // Defense
        BuildAnkleTackle(effectYardage);
        BuildFormTackle(effectYardage);
        BuildGangTackle(effectYardage);
        BuildStripTheBall();
        BuildBallHawk();
        BuildBigOlLick();
        BuildLightsOut();
        BuildBlanketCoverage();
        BuildWearThemDown();
        BuildDisruption();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Done",
            "20 live ball cards created (00400-00419).\n\nCheck Assets/Resources/Cards/ and Assets/Resources/Abilities/.", "OK");
    }

    // ── OFFENSE (00400-00409) ────────────────────────────────────────────────

    static void BuildJukeMove(EffectYardageModifier effectYardage)
    {
        var ability = MakeAbility("ability_lb_juke_move", "Juke Move", "+3 yards.");
        ability.value = 3;
        ability.effects = new EffectData[] { effectYardage };
        EditorUtility.SetDirty(ability);

        MakeLiveBallCard("00400_LB_Juke_Move", "Juke Move", CardType.OffLiveBall,
            new AbilityData[] { ability }, "+3 yards.", new SlotRequirement[0]);
    }

    static void BuildStiffArm(EffectYardageModifier effectYardage)
    {
        var ability = MakeAbility("ability_lb_stiff_arm", "Stiff Arm", "+5 yards.");
        ability.value = 5;
        ability.effects = new EffectData[] { effectYardage };
        EditorUtility.SetDirty(ability);

        MakeLiveBallCard("00401_LB_Stiff_Arm", "Stiff Arm", CardType.OffLiveBall,
            new AbilityData[] { ability }, "+5 yards.",
            new SlotRequirement[] { MakeSlotReq(SlotMachineIconType.Helmet, 1) });
    }

    static void BuildSpeedBurst(EffectYardageModifier effectYardage)
    {
        var ability = MakeAbility("ability_lb_speed_burst", "Speed Burst", "+7 yards.");
        ability.value = 7;
        ability.effects = new EffectData[] { effectYardage };
        EditorUtility.SetDirty(ability);

        MakeLiveBallCard("00402_LB_Speed_Burst", "Speed Burst", CardType.OffLiveBall,
            new AbilityData[] { ability }, "+7 yards.",
            new SlotRequirement[] { MakeSlotReq(SlotMachineIconType.Star, 1) });
    }

    static void BuildBallSecurity()
    {
        var ability = MakeAbility("ability_lb_ball_security", "Ball Security", "Prevent fumble.");
        var effect = AddSubAsset<EffectPreventTurnover>(ability, "EffectBallSecurity", _ => { });
        ability.effects = new EffectData[] { effect };
        EditorUtility.SetDirty(ability);

        MakeLiveBallCard("00403_LB_Ball_Security", "Ball Security", CardType.OffLiveBall,
            new AbilityData[] { ability }, "Prevent forced fumble this play.",
            new SlotRequirement[] { MakeSlotReq(SlotMachineIconType.Star, 1) });
    }

    static void BuildGritUp(ConditionOwner condOwnerSame)
    {
        var ability = MakeAbility("ability_lb_grit_up", "Grit Up", "+5 grit this play.");
        ability.target = AbilityTarget.AllCardsBoard;
        ability.conditions_target = new ConditionData[] { condOwnerSame };
        var effect = AddSubAsset<EffectModifyGrit>(ability, "EffectGritUp", e =>
        {
            e.value = 5;
            e.removeGrit = false;
            e.target = GritTarget.Self;
            e.temporary = true;
        });
        ability.effects = new EffectData[] { effect };
        EditorUtility.SetDirty(ability);

        MakeLiveBallCard("00404_LB_Grit_Up", "Grit Up", CardType.OffLiveBall,
            new AbilityData[] { ability }, "+5 grit this play (fumble protection).",
            new SlotRequirement[] { MakeSlotReq(SlotMachineIconType.Football, 1) });
    }

    static void BuildSecondWind()
    {
        var ability = MakeAbility("ability_lb_second_wind", "Second Wind", "+1 stamina to ball carrier.");
        var effect = AddSubAsset<EffectModifyStamina>(ability, "EffectSecondWind", e =>
        {
            e.value = 1;
            e.removeStamina = false;
            e.target = EffectTarget.Self;
        });
        ability.effects = new EffectData[] { effect };
        EditorUtility.SetDirty(ability);

        MakeLiveBallCard("00405_LB_Second_Wind", "Second Wind", CardType.OffLiveBall,
            new AbilityData[] { ability }, "+1 stamina.",
            new SlotRequirement[] { MakeSlotReq(SlotMachineIconType.Football, 1) });
    }

    static void BuildInTheZone()
    {
        var ability = MakeAbility("ability_lb_in_the_zone", "In the Zone", "Immune to all defensive live ball effects.");
        var effect = AddSubAsset<EffectImmunity>(ability, "EffectInTheZone", _ => { });
        ability.effects = new EffectData[] { effect };
        EditorUtility.SetDirty(ability);

        MakeLiveBallCard("00406_LB_In_The_Zone", "In the Zone", CardType.OffLiveBall,
            new AbilityData[] { ability }, "Immune to all defensive effects this play.",
            new SlotRequirement[] {
                MakeSlotReq(SlotMachineIconType.Star, 1),
                MakeSlotReq(SlotMachineIconType.Helmet, 1)
            });
    }

    static void BuildSmartPlay(EffectDrawCard effectDrawCard)
    {
        var ability = MakeAbility("ability_lb_smart_play", "Smart Play", "Draw 1 card.");
        ability.effects = new EffectData[] { effectDrawCard };
        EditorUtility.SetDirty(ability);

        MakeLiveBallCard("00407_LB_Smart_Play", "Smart Play", CardType.OffLiveBall,
            new AbilityData[] { ability }, "Draw 1 card.", new SlotRequirement[0]);
    }

    static void BuildMomentum(EffectYardageModifier effectYardage, EffectDrawCard effectDrawCard)
    {
        // Ability 1: +4 yards (always fires)
        var abilYardage = MakeAbility("ability_lb_momentum_yards", "Momentum Yards", "+4 yards.");
        abilYardage.value = 4;
        abilYardage.effects = new EffectData[] { effectYardage };
        EditorUtility.SetDirty(abilYardage);

        // Ability 2: Draw 1 if gaining 8+ (conditional)
        var condYardage = GetOrCreate<ConditionYardageGained>(CONDITIONS_PATH + "condition_yardage_gte_8.asset", c =>
        {
            c.threshold = 8;
            c.oper = ConditionOperatorInt.GreaterEqual;
            c.checkLastPlay = false;
        });

        var abilDraw = MakeAbility("ability_lb_momentum_draw", "Momentum Draw", "If gaining 8+: draw 1.");
        abilDraw.conditions_trigger = new ConditionData[] { condYardage };
        abilDraw.effects = new EffectData[] { effectDrawCard };
        EditorUtility.SetDirty(abilDraw);

        MakeLiveBallCard("00408_LB_Momentum", "Momentum", CardType.OffLiveBall,
            new AbilityData[] { abilYardage, abilDraw }, "+4 yards. If gaining 8+: draw 1.",
            new SlotRequirement[] { MakeSlotReq(SlotMachineIconType.Helmet, 1) });
    }

    static void BuildSecondEffort(EffectYardageModifier effectYardage)
    {
        // Ability 1: +3 yards
        var abilYards = MakeAbility("ability_lb_second_effort_yards", "Second Effort Yards", "+3 yards.");
        abilYards.value = 3;
        abilYards.effects = new EffectData[] { effectYardage };
        EditorUtility.SetDirty(abilYards);

        // Ability 2: -1 stamina on self
        var abilStamina = MakeAbility("ability_lb_second_effort_stamina", "Second Effort Stamina", "-1 stamina.");
        var effect = AddSubAsset<EffectModifyStamina>(abilStamina, "EffectSecondEffortDrain", e =>
        {
            e.value = 1;
            e.removeStamina = true;
            e.target = EffectTarget.Self;
        });
        abilStamina.effects = new EffectData[] { effect };
        EditorUtility.SetDirty(abilStamina);

        MakeLiveBallCard("00409_LB_Second_Effort", "Second Effort", CardType.OffLiveBall,
            new AbilityData[] { abilYards, abilStamina }, "+3 yards. -1 stamina on ball carrier.",
            new SlotRequirement[] { MakeSlotReq(SlotMachineIconType.Football, 1) });
    }

    // ── DEFENSE (00410-00419) ────────────────────────────────────────────────

    static void BuildAnkleTackle(EffectYardageModifier effectYardage)
    {
        var ability = MakeAbility("ability_lb_ankle_tackle", "Ankle Tackle", "-2 yards.");
        ability.value = -2;
        ability.effects = new EffectData[] { effectYardage };
        EditorUtility.SetDirty(ability);

        MakeLiveBallCard("00410_LB_Ankle_Tackle", "Ankle Tackle", CardType.DefLiveBall,
            new AbilityData[] { ability }, "-2 yards.", new SlotRequirement[0]);
    }

    static void BuildFormTackle(EffectYardageModifier effectYardage)
    {
        var ability = MakeAbility("ability_lb_form_tackle", "Form Tackle", "-4 yards.");
        ability.value = -4;
        ability.effects = new EffectData[] { effectYardage };
        EditorUtility.SetDirty(ability);

        MakeLiveBallCard("00411_LB_Form_Tackle", "Form Tackle", CardType.DefLiveBall,
            new AbilityData[] { ability }, "-4 yards.",
            new SlotRequirement[] { MakeSlotReq(SlotMachineIconType.Football, 1) });
    }

    static void BuildGangTackle(EffectYardageModifier effectYardage)
    {
        var ability = MakeAbility("ability_lb_gang_tackle", "Gang Tackle", "-6 yards.");
        ability.value = -6;
        ability.effects = new EffectData[] { effectYardage };
        EditorUtility.SetDirty(ability);

        MakeLiveBallCard("00412_LB_Gang_Tackle", "Gang Tackle", CardType.DefLiveBall,
            new AbilityData[] { ability }, "-6 yards.",
            new SlotRequirement[] {
                MakeSlotReq(SlotMachineIconType.Helmet, 1),
                MakeSlotReq(SlotMachineIconType.Football, 1)
            });
    }

    static void BuildStripTheBall()
    {
        var ability = MakeAbility("ability_lb_strip_the_ball", "Strip the Ball", "Force fumble.");
        var effect = AddSubAsset<EffectForceTurnover>(ability, "EffectStrip", _ => { });
        ability.effects = new EffectData[] { effect };
        EditorUtility.SetDirty(ability);

        MakeLiveBallCard("00413_LB_Strip_The_Ball", "Strip the Ball", CardType.DefLiveBall,
            new AbilityData[] { ability }, "Force fumble (grit check).",
            new SlotRequirement[] { MakeSlotReq(SlotMachineIconType.Star, 1) });
    }

    static void BuildBallHawk()
    {
        var condYardage = GetOrCreate<ConditionYardageGained>(CONDITIONS_PATH + "condition_yardage_gte_5.asset", c =>
        {
            c.threshold = 5;
            c.oper = ConditionOperatorInt.GreaterEqual;
            c.checkLastPlay = false;
        });

        var ability = MakeAbility("ability_lb_ball_hawk", "Ball Hawk", "Force fumble if gaining 5+ yards.");
        ability.conditions_trigger = new ConditionData[] { condYardage };
        var effect = AddSubAsset<EffectForceTurnover>(ability, "EffectBallHawk", _ => { });
        ability.effects = new EffectData[] { effect };
        EditorUtility.SetDirty(ability);

        MakeLiveBallCard("00414_LB_Ball_Hawk", "Ball Hawk", CardType.DefLiveBall,
            new AbilityData[] { ability }, "Force fumble if gaining 5+ yards (grit check).",
            new SlotRequirement[] { MakeSlotReq(SlotMachineIconType.Helmet, 1) });
    }

    static void BuildBigOlLick()
    {
        var ability = MakeAbility("ability_lb_big_ol_lick", "Big Ol Lick", "Silence target offensive player for 1 turn.");
        ability.target = AbilityTarget.AllBoardOffense;
        // Apply Silenced status for 1 turn via ability's status array
        var silencedStatus = GetOrCreate<StatusData>(EFFECTS_PATH + "status_silenced.asset", s =>
        {
            s.effect = StatusType.Silenced;
        });
        ability.status = new StatusData[] { silencedStatus };
        ability.duration = 1;
        ability.effects = new EffectData[0];
        EditorUtility.SetDirty(ability);

        MakeLiveBallCard("00415_LB_Big_Ol_Lick", "Big Ol Lick", CardType.DefLiveBall,
            new AbilityData[] { ability }, "Silence an offensive player for 1 turn.",
            new SlotRequirement[] { MakeSlotReq(SlotMachineIconType.Helmet, 1) });
    }

    static void BuildLightsOut()
    {
        var ability = MakeAbility("ability_lb_lights_out", "Lights Out", "KO target player (stamina to 0).");
        ability.target = AbilityTarget.AllBoardOffense;
        var effect = AddSubAsset<EffectModifyStamina>(ability, "EffectLightsOut", e =>
        {
            e.value = 99; // drain enough to zero out any stamina
            e.removeStamina = true;
            e.target = EffectTarget.Card;
        });
        ability.effects = new EffectData[] { effect };
        EditorUtility.SetDirty(ability);

        MakeLiveBallCard("00416_LB_Lights_Out", "Lights Out", CardType.DefLiveBall,
            new AbilityData[] { ability }, "KO an offensive player (stamina to 0).",
            new SlotRequirement[] {
                MakeSlotReq(SlotMachineIconType.Star, 2)
            });
    }

    static void BuildBlanketCoverage()
    {
        var ability = MakeAbility("ability_lb_blanket_coverage", "Blanket Coverage", "Negate opponent's live ball card.");
        var effect = AddSubAsset<EffectNegateCard>(ability, "EffectBlanketCoverage", _ => { });
        ability.effects = new EffectData[] { effect };
        EditorUtility.SetDirty(ability);

        MakeLiveBallCard("00417_LB_Blanket_Coverage", "Blanket Coverage", CardType.DefLiveBall,
            new AbilityData[] { ability }, "Negate opponent's live ball card.",
            new SlotRequirement[] { MakeSlotReq(SlotMachineIconType.Football, 1) });
    }

    static void BuildWearThemDown()
    {
        var ability = MakeAbility("ability_lb_wear_them_down", "Wear Them Down", "-1 stamina all offensive players.");
        var effect = AddSubAsset<EffectModifyStamina>(ability, "EffectWearDown", e =>
        {
            e.value = 1;
            e.removeStamina = true;
            e.target = EffectTarget.Opponent;
        });
        ability.effects = new EffectData[] { effect };
        EditorUtility.SetDirty(ability);

        MakeLiveBallCard("00418_LB_Wear_Them_Down", "Wear Them Down", CardType.DefLiveBall,
            new AbilityData[] { ability }, "-1 stamina to all offensive players.",
            new SlotRequirement[] { MakeSlotReq(SlotMachineIconType.Star, 1) });
    }

    static void BuildDisruption()
    {
        var ability = MakeAbility("ability_lb_disruption", "Disruption", "Add Wrench to outer reels next spin.");

        // Add Wrench to reel 0 (left outer)
        var effectLeft = AddSubAsset<EffectAddSlotSymbol>(ability, "EffectDisruptionLeft", e =>
        {
            e.symbolType = SlotMachineIconType.Wrench;
            e.count = 1;
            e.targetReel = 0;
            e.slotPosition = -1;
            e.addReel = false;
            e.duration = 1;
        });

        // Add Wrench to reel 2 (right outer)
        var effectRight = AddSubAsset<EffectAddSlotSymbol>(ability, "EffectDisruptionRight", e =>
        {
            e.symbolType = SlotMachineIconType.Wrench;
            e.count = 1;
            e.targetReel = 2;
            e.slotPosition = -1;
            e.addReel = false;
            e.duration = 1;
        });

        ability.effects = new EffectData[] { effectLeft, effectRight };
        EditorUtility.SetDirty(ability);

        MakeLiveBallCard("00419_LB_Disruption", "Disruption", CardType.DefLiveBall,
            new AbilityData[] { ability }, "Add Wrench to each outer reel next spin.",
            new SlotRequirement[] { MakeSlotReq(SlotMachineIconType.Helmet, 1) });
    }

    // ── Asset factories ─────────────────────────────────────────────────────

    static AbilityData MakeAbility(string id, string title, string desc)
    {
        string path = ABILITIES_PATH + id + ".asset";
        AbilityData ability = AssetDatabase.LoadAssetAtPath<AbilityData>(path);
        if (ability == null)
        {
            ability = ScriptableObject.CreateInstance<AbilityData>();
            AssetDatabase.CreateAsset(ability, path);
        }

        ability.id                 = id;
        ability.trigger            = AbilityTrigger.OnLiveBallResolution;
        ability.target             = AbilityTarget.None;
        ability.conditions_target  = new ConditionData[0];
        ability.conditions_trigger = new ConditionData[0];
        ability.title              = title;
        ability.desc               = desc;
        ability.value              = 0;
        ability.effects            = new EffectData[0];
        ability.status             = new StatusData[0];
        ability.chain_abilities    = new AbilityData[0];
        EditorUtility.SetDirty(ability);
        return ability;
    }

    static CardData MakeLiveBallCard(
        string filename, string title, CardType type,
        AbilityData[] abilities, string text,
        SlotRequirement[] slotReqs)
    {
        string path = CARDS_PATH + filename + ".asset";
        CardData card = AssetDatabase.LoadAssetAtPath<CardData>(path);
        if (card == null)
        {
            card = ScriptableObject.CreateInstance<CardData>();
            AssetDatabase.CreateAsset(card, path);
        }

        card.id               = filename.ToLower().Replace(" ", "_");
        card.title             = title;
        card.type              = type;
        card.abilities         = abilities;
        card.text              = text;
        card.slotRequirements  = slotReqs;
        card.required_plays    = new Assets.TcgEngine.Scripts.Gameplay.PlayType[0];
        card.mana              = 0;

        // Not player cards — zero out stats
        card.stamina                      = 0;
        card.grit                         = 0;
        card.run_bonus                    = 0;
        card.short_pass_bonus             = 0;
        card.deep_pass_bonus              = 0;
        card.run_coverage_bonus           = 0;
        card.short_pass_coverage_bonus    = 0;
        card.deep_pass_coverage_bonus     = 0;

        EditorUtility.SetDirty(card);
        return card;
    }

    static SlotRequirement MakeSlotReq(SlotMachineIconType icon, int count)
    {
        return new SlotRequirement { icon = icon, requiredCount = count };
    }

    static T AddSubAsset<T>(AbilityData ability, string assetName, System.Action<T> configure)
        where T : EffectData
    {
        string abilityPath = AssetDatabase.GetAssetPath(ability);
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
