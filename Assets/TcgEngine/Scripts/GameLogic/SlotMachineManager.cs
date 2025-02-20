using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class ReelSpriteData
{
    public Sprite TopImage;
    public Sprite MiddleImage;
    public Sprite BottomImage;
}
public class SlotMachineResultDTO
{
    public List<ReelSpriteData> Results { get; set; }
    public List<SlotData> SlotDataCopy { get; set; }
}


public class SlotMachineManager
{



    private List<List<SlotIconData>> extraReels; // Stores extra reel added by a card
    public bool isExtraReelActive = false;
    public List<SlotData> slot_data;
    private Dictionary<string, Sprite> iconSprites = new Dictionary<string, Sprite>();

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

    public Sprite GetSpriteForID(string iconID)
    {
        if (iconSprites.TryGetValue(iconID, out Sprite sprite))
        {
            return sprite;
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
        string mid = WeightedPick(tempList);
        ReduceIconWeight(tempList, mid);

        // Top
        string top = WeightedPick(tempList);
        ReduceIconWeight(tempList, top);

        // Bottom
        string bot = WeightedPick(tempList);
        // optional reduce if you want no duplicates
        ReduceIconWeight(tempList, bot);

        return new ReelSpriteData
        {
            TopImage =  GetSpriteForID(top),
            MiddleImage = GetSpriteForID(mid),
            BottomImage = GetSpriteForID(bot)
        };

    }


    private string WeightedPick(List<SlotIconData> icons)
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
    private void ReduceIconWeight(List<SlotIconData> icons, string iconID)
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