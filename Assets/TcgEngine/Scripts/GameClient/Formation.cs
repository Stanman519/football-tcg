using Assets.TcgEngine.Scripts.Gameplay;
using System.Collections.Generic;
using TcgEngine;
using UnityEngine;

public class FormationSlotData
{
    public PlayerPositionGrp positionGroup;
    public float xOffset; // Horizontal offset relative to center
    public int yardLine; // Y-position relative to line of scrimmage
    public bool isOffense; // Determines if slot belongs to the offensive or defensive team

    public FormationSlotData(PlayerPositionGrp posGroup, float xOffset, int yardLine, bool isOffense)
    {
        this.positionGroup = posGroup;
        this.xOffset = xOffset;
        this.yardLine = yardLine;
        this.isOffense = isOffense;
    }
}

public class Formation
{
    public Dictionary<PlayerPositionGrp, Vector3[]> slotPositions = new Dictionary<PlayerPositionGrp, Vector3[]>();

    public string formationName;
    public List<FormationSlotData> slots = new List<FormationSlotData>();

    public Formation(string name)
    {
        formationName = name;
    }

    public void AddSlot(PlayerPositionGrp positionGroup, float xOffset, int yardLine, bool isOffense)
    {
        slots.Add(new FormationSlotData(positionGroup, xOffset, yardLine, isOffense));
    }

    public Formation()
    {
        // QB always in center
        slotPositions[PlayerPositionGrp.QB] = new Vector3[] { new Vector3(0, 0, 0) };

        // WRs spread out
        slotPositions[PlayerPositionGrp.WR] = new Vector3[] {
            new Vector3(-3, 2, 0),
            new Vector3(3, 2, 0),
            new Vector3(-5, 2, 0)
        };

        // RB & TE near QB
        slotPositions[PlayerPositionGrp.RB_TE] = new Vector3[] {
            new Vector3(0, -1, 0),
            new Vector3(1, -1, 0)
        };

        // Offensive Line
        slotPositions[PlayerPositionGrp.OL] = new Vector3[] {
            new Vector3(-2, -0.5f, 0),
            new Vector3(-1, -0.5f, 0),
            new Vector3(0, -0.5f, 0),
            new Vector3(1, -0.5f, 0),
            new Vector3(2, -0.5f, 0)
        };
    }
}
