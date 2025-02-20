using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.UI;

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
    public string IconID;
    public float Weight = 1f;
    public Sprite IconSprite;

    public SlotIconData()
    {
           
    }
    public SlotIconData(string iconID, float weight)
    {
        IconID = iconID;
        Weight = weight;
        IconSprite = LoadSprite(iconID);
    }

    private Sprite LoadSprite(string iconID)
    {
        var test = Resources.Load<Sprite>($"SlotMachine/slot-{iconID.ToLower()}-icon");
        return test;
    }
}