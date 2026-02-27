using System;
using System.Collections.Generic;
using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Data asset describing where each position group's slots should line up on the field.
    /// Coordinates use football convention:
    ///   xFraction: -0.5 = left sideline, 0 = center, +0.5 = right sideline
    ///   yardsFromLOS: negative = own backfield, positive = opponent territory
    /// </summary>
    [CreateAssetMenu(menuName = "FirstAndLong/FormationData", fileName = "formation")]
    public class FormationData : ScriptableObject
    {
        public bool isDefense;
        public List<FormationSlotEntry> slots = new List<FormationSlotEntry>();
    }

    [Serializable]
    public class FormationSlotEntry
    {
        public PlayerPositionGrp posGroup;
        public int slotIndex;       // 0 = first slot of this group, 1 = second, etc.
        public float xFraction;     // -0.5 left … 0 center … +0.5 right
        public float yardsFromLOS;  // negative = own backfield, positive = opponent side
    }
}
