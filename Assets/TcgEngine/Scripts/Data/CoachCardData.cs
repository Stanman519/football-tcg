using System;
using TcgEngine;
using Assets.TcgEngine.Scripts.Gameplay;
using UnityEngine;

[Serializable]
public struct CoachYardageEntry
{
    public PlayType playType;
    public int yards;
}

[Serializable]
public struct CoachSchemeEntry
{
    public PlayerPositionGrp position;
    public int maxCards;
}

[Serializable]
public struct CoachFormationEntry
{
    public PlayType playType;
    public FormationData formation;
}

[CreateAssetMenu(menuName = "TcgEngine/CoachCardData", fileName = "CoachCard")]
public class CoachCardData : ScriptableObject
{
    public string title;
    public Sprite card_image;
    [TextArea] public string ability_text;

    public CoachYardageEntry[] offenseYardage;
    public CoachYardageEntry[] defenseYardage;
    public CoachSchemeEntry[]  positionalScheme;
    public CoachFormationEntry[] offenseFormations;
    public CoachFormationEntry[] defenseFormations;
    public CoachData coachProfile;

    public int GetOffenseYardage(PlayType pt)
    {
        if (offenseYardage != null)
            foreach (var e in offenseYardage)
                if (e.playType == pt) return e.yards;
        return 0;
    }

    public int GetDefenseYardage(PlayType pt)
    {
        if (defenseYardage != null)
            foreach (var e in defenseYardage)
                if (e.playType == pt) return e.yards;
        return 0;
    }

    public int GetMaxCards(PlayerPositionGrp pos)
    {
        if (positionalScheme != null)
            foreach (var e in positionalScheme)
                if (e.position == pos) return e.maxCards;
        return 0;
    }

    public FormationData GetOffenseFormation(PlayType pt)
    {
        if (offenseFormations != null)
            foreach (var e in offenseFormations)
                if (e.playType == pt) return e.formation;
        return null;
    }

    public FormationData GetDefenseFormation(PlayType pt)
    {
        if (defenseFormations != null)
            foreach (var e in defenseFormations)
                if (e.playType == pt) return e.formation;
        return null;
    }
}
