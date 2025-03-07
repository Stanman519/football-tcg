using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class ReelSpriteData
{
    public IconResultData Top;
    public IconResultData Middle;
    public IconResultData Bottom;
}
[Serializable]
public class IconResultData
{
    public Sprite Image;
    public SlotMachineIconType IconId;
}

public class SlotMachineResultDTO
{
    public List<ReelSpriteData> Results { get; set; }
    public List<SlotData> SlotDataCopy { get; set; }
}
public enum SlotMachineIconType
{ //use insdead of strings.
    Football,
    Helmet,
    WildCard,
    Wrench,
    Star,
    None
}

public class SlotMachineManager
{



    private List<List<SlotIconData>> extraReels; // Stores extra reel added by a card
    public bool isExtraReelActive = false;
    public List<SlotData> slot_data;
    private Dictionary<SlotMachineIconType, Sprite> iconSprites = new Dictionary<SlotMachineIconType, Sprite>();

    public SlotMachineManager(List<SlotData> defaultSlotData)
    {
        slot_data = defaultSlotData;
        foreach (var slotData in slot_data)
        {
            LoadIcons(slotData.reelIconInventory);
        }
    }

    private void LoadIcons(List<SlotIconData> icons)
    {
        foreach (var icon in icons)
        {
            if (!iconSprites.ContainsKey(icon.IconID))
            {
                iconSprites.Add(icon.IconID, icon.IconSprite);
            }
        }
    }

    public IconResultData GetSpriteForID(SlotMachineIconType iconID)
    {
        if (iconSprites.TryGetValue(iconID, out Sprite sprite))
        {
            return new IconResultData
            {
                Image = sprite,
                IconId = iconID,
            };
        }
        return null; // Fallback case if no sprite is found
    }


    public SlotMachineResultDTO CalculateSpinResults()
    {
        var results = new List<ReelSpriteData>();
        foreach (var slot in slot_data)
        {
            results.Add(PickThreeIcons(slot.reelIconInventory));
        }
        return new SlotMachineResultDTO
        {
            Results = results,
            SlotDataCopy = slot_data
        };
        
    }

    private ReelSpriteData PickThreeIcons(List<SlotIconData> baseIcons)
    {
        List<SlotIconData> tempList = CopyIconList(baseIcons);

        // Middle
        var mid = WeightedPick(tempList);
        ReduceIconWeight(tempList, mid);

        // Top
        var top = WeightedPick(tempList);
        ReduceIconWeight(tempList, top);

        // Bottom
        var bot = WeightedPick(tempList);
        // optional reduce if you want no duplicates
        ReduceIconWeight(tempList, bot);

        return new ReelSpriteData
        {

            Top =  GetSpriteForID(top),
            Middle = GetSpriteForID(mid),
            Bottom = GetSpriteForID(bot)
        };

    }


    private SlotMachineIconType WeightedPick(List<SlotIconData> icons)
    {
        float totalWeight = icons.Sum(i => i.Weight);
        float randomValue = UnityEngine.Random.Range(0f, totalWeight);
        float cumulativeWeight = 0f;

        foreach (var icon in icons)
        {
            cumulativeWeight += icon.Weight;
            if (randomValue <= cumulativeWeight)
                return icon.IconID;
        }

        return icons.Last().IconID;
    }
    private void ReduceIconWeight(List<SlotIconData> icons, SlotMachineIconType iconID)
    {
        for (int i = 0; i < icons.Count; i++)
        {
            if (icons[i].IconID == iconID)
            {
                if (icons[i].Weight > 0f)
                    icons[i].Weight -= 1f;
                break;
            }
        }
    }
    private List<SlotIconData> CopyIconList(List<SlotIconData> source)
    {
        var copy = new List<SlotIconData>();
        foreach (var icon in source)
        {
            copy.Add(new SlotIconData
            {
                IconID = icon.IconID,
                Weight = icon.Weight,
                IconSprite = icon.IconSprite
            });
        }
        return copy;
    }
}