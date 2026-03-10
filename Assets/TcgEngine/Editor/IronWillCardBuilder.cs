#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TcgEngine;
using Assets.TcgEngine.Scripts.Effects;
using Assets.TcgEngine.Scripts.Gameplay;

/// <summary>
/// Creates the "Iron Will" test player card demonstrating OnKnockout + LowestStaminaAlly.
/// Menu: First&amp;Long > Create Iron Will Card
/// </summary>
public static class IronWillCardBuilder
{
    const string CARDS_PATH     = "Assets/Resources/Cards/";
    const string ABILITIES_PATH = "Assets/Resources/Abilities/";
    const string EFFECTS_PATH   = "Assets/Resources/Effects/";

    [MenuItem("First&Long/Create Iron Will Card")]
    static void Build()
    {
        if (!EditorUtility.DisplayDialog("Create Iron Will Card",
            "Creates the Iron Will player card + ability + effect in Resources/.\n\nExisting assets will be overwritten. Proceed?",
            "Create", "Cancel"))
            return;

        EnsureDir(CARDS_PATH);
        EnsureDir(ABILITIES_PATH);
        EnsureDir(EFFECTS_PATH);

        // Effect: restore 5 stamina (sub-asset of the ability)
        var ability = MakeAbility("ability_iron_will_on_knockout", "Iron Will – On Knockout",
            "Restore 5 stamina to teammate with lowest stamina.");

        ability.trigger = AbilityTrigger.OnKnockout;
        ability.target  = AbilityTarget.LowestStaminaAlly;

        var effect = AddSubAsset<EffectModifyStamina>(ability, "EffectIronWillStamina", e =>
        {
            e.value         = 5;
            e.removeStamina = false;
            e.target        = EffectTarget.Self; // overridden by AbilityTarget.LowestStaminaAlly targeting
        });
        ability.effects = new EffectData[] { effect };
        EditorUtility.SetDirty(ability);

        // Card
        string cardPath = CARDS_PATH + "iron_will.asset";
        CardData card = AssetDatabase.LoadAssetAtPath<CardData>(cardPath);
        if (card == null)
        {
            card = ScriptableObject.CreateInstance<CardData>();
            AssetDatabase.CreateAsset(card, cardPath);
        }

        card.id             = "iron_will";
        card.title          = "Iron Will";
        card.type           = CardType.OffensivePlayer;
        card.playerPosition = PlayerPositionGrp.OL;
        card.stamina        = 3;
        card.grit           = 4;
        card.run_bonus      = 2;
        card.short_pass_bonus             = 0;
        card.deep_pass_bonus              = 0;
        card.run_coverage_bonus           = 0;
        card.short_pass_coverage_bonus    = 0;
        card.deep_pass_coverage_bonus     = 0;
        card.abilities      = new AbilityData[] { ability };
        card.text           = "[On Knockout] Restore 5 stamina to teammate with lowest stamina.";
        card.required_plays = new PlayType[0];
        card.mana           = 0;
        EditorUtility.SetDirty(card);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Done",
            "Iron Will card created.\nCheck Assets/Resources/Cards/iron_will.asset", "OK");
    }

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
        ability.title              = title;
        ability.desc               = desc;
        ability.value              = 0;
        ability.effects            = new EffectData[0];
        ability.status             = new StatusData[0];
        ability.chain_abilities    = new AbilityData[0];
        ability.conditions_trigger = new ConditionData[0];
        ability.conditions_target  = new ConditionData[0];
        EditorUtility.SetDirty(ability);
        return ability;
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
                configure((T)obj);
                EditorUtility.SetDirty(obj);
                return (T)obj;
            }
        }
        T effect = ScriptableObject.CreateInstance<T>();
        effect.name = assetName;
        configure(effect);
        AssetDatabase.AddObjectToAsset(effect, ability);
        EditorUtility.SetDirty(ability);
        return effect;
    }

    static void EnsureDir(string path)
    {
        if (!System.IO.Directory.Exists(path))
            System.IO.Directory.CreateDirectory(path);
    }
}
#endif
