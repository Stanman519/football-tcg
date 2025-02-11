using UnityEngine;
using System.Collections.Generic;

public class CardEffectSystem : MonoBehaviour
{
    // You might keep track of all cards on the field
    public List<CardInstance> CardsInPlay = new List<CardInstance>();

    public void OnSlotSpinComplete(SlotResult result)
    {
        // Loop over each card in play
        foreach (var cardInstance in CardsInPlay)
        {
            var definition = cardInstance.Definition;
            foreach (var effectData in definition.Effects)
            {
                if (effectData.triggerType == CardTriggerType.OnSlotSpinComplete)
                {
                    // Check the conditionString logic if any
                    if (MeetsCondition(effectData.conditionString, result))
                    {
                        ApplyEffect(effectData, cardInstance);
                    }
                }
                // other trigger types => flow to another orchestrator 

            }
        }
    }

    private bool MeetsCondition(string condition, SlotResult result)
    {
        // Parse the condition string, e.g. "stars>=2" or "slotChance=10"
        // This is custom logic for your game. For example:
        // if condition == "stars>=2" then check result.starCount
        // if condition == "slotChance=10" do a random roll
        return true;
    }

    private void ApplyEffect(CardEffectData effectData, CardInstance sourceCard)
    {
        switch (effectData.effectType)
        {
            case EffectType.AddRunBonus:
                // e.g. temporarily boost run bonus for this play
                //sourceCard.TempRunBonus += effectData.effectValue;
                Debug.Log($"{sourceCard.Definition.CardName} gained run bonus of {effectData.effectValue}");
                break;

            case EffectType.ForceFumble:
                // Trigger a fumble logic in the GameManager or FieldManager
                Debug.Log("Fumble forced!");
                break;

                // etc.
        }
    }
}
