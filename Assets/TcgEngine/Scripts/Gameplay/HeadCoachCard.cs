using System;
using System.Collections.Generic;
using TcgEngine;

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

        // Formation assets per play type (null entries fall back to hardcoded data in FieldSlotManager)
        [System.NonSerialized] public Dictionary<PlayType, FormationData> offenseFormations = new Dictionary<PlayType, FormationData>();
        [System.NonSerialized] public Dictionary<PlayType, FormationData> defenseFormations = new Dictionary<PlayType, FormationData>();

        [System.NonSerialized] public CoachData coachData;
        public CoachType coachType = CoachType.Balanced;

        public HeadCoachCard()
        {
            positional_Scheme = new Dictionary<PlayerPositionGrp, HCPlayerSchemeData>();
            
            // Use TryAdd to avoid issues with duplicate enum values
            TryAddPosition(PlayerPositionGrp.NONE, 0);
            TryAddPosition(PlayerPositionGrp.WR, 2);
            TryAddPosition(PlayerPositionGrp.RB_TE, 2);
            TryAddPosition(PlayerPositionGrp.QB, 1);
            TryAddPosition(PlayerPositionGrp.OL, 2);
            TryAddPosition(PlayerPositionGrp.DL, 2);
            TryAddPosition(PlayerPositionGrp.DB, 2);
            TryAddPosition(PlayerPositionGrp.LB, 2);
            TryAddPosition(PlayerPositionGrp.P, 1);
            TryAddPosition(PlayerPositionGrp.K, 1);

            baseOffenseYardage = new Dictionary<PlayType, int>
            {
                { PlayType.Run, 0 },
                { PlayType.ShortPass, 0 },
                { PlayType.LongPass, 0 },
            };

            baseDefenseYardage = new Dictionary<PlayType, int>
            {
                { PlayType.Run, 0 },
                { PlayType.ShortPass, 0 },
                { PlayType.LongPass, 0 },
            };

            completionRequirements = new Dictionary<PlayType, List<CompletionRequirement>>();
        }

        private void TryAddPosition(PlayerPositionGrp pos, int max)
        {
            if (!positional_Scheme.ContainsKey(pos))
            {
                positional_Scheme[pos] = new HCPlayerSchemeData { pos_max = max };
            }
        }

        /// <summary>
        /// Populate runtime dictionaries from a CoachCardData asset.
        /// </summary>
        public void InitFromData(CoachCardData data)
        {
            if (data == null) return;

            if (data.offenseYardage != null)
                foreach (var e in data.offenseYardage)
                    baseOffenseYardage[e.playType] = e.yards;

            if (data.defenseYardage != null)
                foreach (var e in data.defenseYardage)
                    baseDefenseYardage[e.playType] = e.yards;

            if (data.positionalScheme != null)
                foreach (var e in data.positionalScheme)
                    positional_Scheme[e.position] = new HCPlayerSchemeData { pos_max = e.maxCards };

            coachData = data.coachProfile;
            coachType = data.coachProfile != null ? data.coachProfile.coachType : CoachType.Balanced;

            offenseFormations.Clear();
            if (data.offenseFormations != null)
                foreach (var e in data.offenseFormations)
                    offenseFormations[e.playType] = e.formation;

            defenseFormations.Clear();
            if (data.defenseFormations != null)
                foreach (var e in data.defenseFormations)
                    defenseFormations[e.playType] = e.formation;
        }
    }
}