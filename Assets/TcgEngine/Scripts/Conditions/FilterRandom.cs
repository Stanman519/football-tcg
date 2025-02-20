using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TcgEngine
{
    //Pick X number of targets at random from the source array

    [CreateAssetMenu(fileName = "filter", menuName = "TcgEngine/Filter/Random", order = 10)]
    public class FilterRandom : FilterData
    {
        public int amount = 1; //Number of random targets selected

        public override List<Card> FilterTargets(Game data, AbilityData ability, Card caster, List<Card> source, List<Card> dest)
        {
            return GameTool.PickXRandom(source, dest, amount);
        }

        public override List<Player> FilterTargets(Game data, AbilityData ability, Card caster, List<Player> source, List<Player> dest)
        {
            return GameTool.PickXRandom(source, dest, amount);
        }

        public override List<CardPositionSlot> FilterTargets(Game data, AbilityData ability, Card caster, List<CardPositionSlot> source, List<CardPositionSlot> dest)
        {
            return GameTool.PickXRandom(source, dest, amount);
        }

        public override List<CardData> FilterTargets(Game data, AbilityData ability, Card caster, List<CardData> source, List<CardData> dest)
        {
            return GameTool.PickXRandom(source, dest, amount);
        }
    }
}
