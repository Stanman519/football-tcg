using System;
using System.Collections.Generic;
using UnityEngine;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// Data asset describing where each position group's slots should line up on the field.
    /// Coordinates use football convention:
    ///   xFraction:    -0.5 = left sideline,  0 = center,  +0.5 = right sideline
    ///   yardsFromLOS:  negative = own backfield,  positive = opponent territory
    ///
    /// posGroup options: QB  WR  RB_TE  OL  K  DL  LB  DB
    ///
    /// ┌──────────┬───────────┬───────────┬──────────────┐
    /// │ posGroup │ slotIndex │ xFraction │ yardsFromLOS │
    /// ├──────────┼───────────┼───────────┼──────────────┤
    /// │          │     0     │   0.00    │     0.0      │  ← BLANK (copy this row)
    /// ├──────────┼───────────┼───────────┼──────────────┤
    /// │ QB       │     0     │   0.00    │   -11.0      │
    /// │ WR       │     0     │  -0.18    │   -12.0      │
    /// │ WR       │     1     │   0.00    │   -12.5      │
    /// │ WR       │     2     │   0.18    │   -12.0      │
    /// │ RB_TE    │     0     │  -0.08    │   -13.0      │
    /// │ RB_TE    │     1     │   0.08    │   -13.0      │
    /// │ OL       │     0     │  -0.20    │   -12.0      │
    /// │ OL       │     1     │  -0.10    │   -12.0      │
    /// │ OL       │     2     │   0.00    │   -12.0      │
    /// │ OL       │     3     │   0.10    │   -12.0      │
    /// │ OL       │     4     │   0.20    │   -12.0      │
    /// │ K        │     0     │   0.00    │   -13.0      │
    /// │ DL       │     0     │  -0.10    │    11.0      │
    /// │ DL       │     1     │   0.10    │    11.0      │
    /// │ LB       │     0     │  -0.08    │    12.0      │
    /// │ LB       │     1     │   0.08    │    12.0      │
    /// │ DB       │     0     │  -0.18    │    13.0      │
    /// │ DB       │     1     │   0.00    │    13.5      │
    /// │ DB       │     2     │   0.18    │    13.0      │
    /// └──────────┴───────────┴───────────┴──────────────┘
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
