using System.Collections.Generic;
using UnityEngine;

public enum CardType
{
    OffensivePlayer,
    DefensivePlayer,
    SpecialTeamsPlayer,
    DefensivePlayEnhancer,
    OffensivePlayEnhancer,
    ComboPlayEnhancer,
    LiveBall,
    HeadCoach
}

public enum CardTriggerType
{
    OnSlotSpinComplete,
    OnPlayCall,
    OnCoverageCheck,
    OnFumble,
    OnDriveStart,

}

public enum EffectType
{
    AddRunBonus,
    AddShortPassBonus,
    ForceFumble,
    HealStamina,
    NegateTopReceiver,
    DrawCards,
    DiscardCards,
}

[System.Serializable]
public class CardEffectData
{
    [Header("Trigger")]
    public CardTriggerType triggerType;

    [Header("Condition")]
    [Tooltip("Optional condition data, e.g. 'slotChance=10' or 'starsNeeded=2'")]
    public string conditionString;

    [Header("Effect")]
    public EffectType effectType;
    public int effectValue;
    // Could store additional fields: e.g., numberOfCardsToDiscard, chance, etc.
}

[CreateAssetMenu(menuName = "Cards/New Card Definition")]
public class CardDefinition : ScriptableObject
{
    [Header("Basic Info")]
    public string CardName;
    [TextArea] public string Description;
    [TextArea] public string FlavorText;
    public Sprite CardArt;

    [Header("Card Attributes")]
    public CardType CardType;
    public int Stamina;
    public int Grit;

    // Offensive fields (only used if cardType == OffensivePlayer, e.g.)
    public int RunBonus;
    public int ShortPassBonus;
    public int DeepPassBonus;

    // Defensive fields (only used if cardType == DefensivePlayer, e.g.)
    public int RunCoverage;
    public int ShortCoverage;
    public int DeepCoverage;

    [Header("Effect Data")]
    public List<CardEffectData> Effects = new List<CardEffectData>();
}