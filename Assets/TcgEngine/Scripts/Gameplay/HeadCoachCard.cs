using System;
using System.Collections.Generic;

namespace Assets.TcgEngine.Scripts.Gameplay
{
    [Serializable]
    public class HCPlayerSchemeData
    {
        public int pos_max { get; set; }
    }
    [Serializable]
    public struct CompletionRequirement
    {
        public SlotMachineIconType icon; // Type of icon required
        public int minCount;      // How many are required to succeed
    }
    [Serializable]
    public class HeadCoachCard
    {
        public Dictionary<PlayerPositionGrp, HCPlayerSchemeData> positional_Scheme;
        public Dictionary<PlayType, int> baseOffenseYardage;
        public Dictionary<PlayType, int> baseDefenseYardage;
        // Slot completion requirements for play types
        public Dictionary<PlayType, List<CompletionRequirement>> completionRequirements;
    }
}