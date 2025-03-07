using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;
using static UnityEditor.Experimental.AssetDatabaseExperimental.AssetDatabaseCounters;
using UnityEngine.UIElements;

[Serializable]
public class SlotData
{
    public int id;
    public List<SlotIconData> reelIconInventory;
    public ReelUI activeIcons;
    public float stopDelay;
    //spins left? for the extra slots that get added
}
[Serializable]
public class SlotIconData
{
    public SlotMachineIconType IconID;
    public float Weight = 1f;
    public Sprite IconSprite;

    public SlotIconData()
    {
           
    }
    public SlotIconData(SlotMachineIconType iconID, float weight)
    {
        IconID = iconID;
        Weight = weight;
        IconSprite = LoadSprite(iconID);
    }

    private Sprite LoadSprite(SlotMachineIconType iconID)
    {
        var test = Resources.Load<Sprite>($"SlotMachine/slot-{Enum.GetName(typeof(SlotMachineIconType), iconID).ToLower()}-icon");
        return test;
    }
}