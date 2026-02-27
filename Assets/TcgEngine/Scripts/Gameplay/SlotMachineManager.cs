using Assets.TcgEngine.Scripts.Effects;
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
    public SlotMachineIconType IconId;

    // Image is NOT serialized - client looks it up based on IconId
    [System.NonSerialized]
    public Sprite Image;
}
[Serializable]
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

    /// <summary>
    /// Calculate spin results with optional modifiers (for abilities like Risk-Taker)
    /// </summary>
    /// <param name="modifiers">List of slot modifiers from abilities</param>
    /// <returns>Spin results with modifiers applied</returns>
    public SlotMachineResultDTO CalculateSpinResults(List<SlotModifier> modifiers = null)
    {
        var results = new List<ReelSpriteData>();
        
        // Check for extra reels from modifiers
        bool hasExtraReels = modifiers != null && modifiers.Any(m => m.addReel);
        
        // Process each base reel
        foreach (var slot in slot_data)
        {
            List<SlotIconData> iconsToUse = ApplyModifiersToReel(slot.reelIconInventory, modifiers, slot.id);
            results.Add(PickThreeIcons(iconsToUse));
        }
        
        // Add extra reels if any modifier adds them
        if (hasExtraReels)
        {
            int extraReelCount = modifiers.Count(m => m.addReel);
            for (int i = 0; i < extraReelCount; i++)
            {
                // Create a new reel with default distribution (or could be custom)
                List<SlotIconData> extraReelIcons = CreateDefaultReelIcons();
                extraReelIcons = ApplyModifiersToReel(extraReelIcons, modifiers, -1); // -1 = extra reel
                results.Add(PickThreeIcons(extraReelIcons));
            }
            isExtraReelActive = true;
        }
        
        return new SlotMachineResultDTO
        {
            Results = results,
            // SlotDataCopy intentionally not set here — Sprites in SlotIconData are not
            // network-serializable. Callers that need timing data use slotMachineManager.slot_data directly.
        };
    }

    /// <summary>
    /// Apply symbol modifiers to a single reel's icon list
    /// </summary>
    private List<SlotIconData> ApplyModifiersToReel(List<SlotIconData> baseIcons, List<SlotModifier> modifiers, int reelId)
    {
        // Start with a copy of base icons
        var modified = CopyIconList(baseIcons);
        
        if (modifiers == null || modifiers.Count == 0)
            return modified;
            
        foreach (var mod in modifiers)
        {
            // Skip reel additions - handled separately
            if (mod.addReel)
                continue;
                
            // Skip if this modifier is for a specific reel and doesn't match
            if (mod.targetReel >= 0 && mod.targetReel != reelId)
                continue;
            
            // Add the symbol(s) - increase weight to guarantee it appears
            // Alternatively, could add a new entry to ensure it triggers
            for (int i = 0; i < mod.count; i++)
            {
                // Find existing or add new
                var existing = modified.FirstOrDefault(x => x.IconID == mod.symbolType);
                if (existing != null)
                {
                    // Increase weight significantly to make it likely to appear
                    existing.Weight += 5f;
                }
                else
                {
                    // Add new symbol to this reel
                    modified.Add(new SlotIconData
                    {
                        IconID = mod.symbolType,
                        Weight = 5f,
                        IconSprite = null
                    });
                }
            }
        }
        
        return modified;
    }
    
    /// <summary>
    /// Create default icon list for extra reels
    /// </summary>
    private List<SlotIconData> CreateDefaultReelIcons()
    {
        return new List<SlotIconData>
        {
            new SlotIconData { IconID = SlotMachineIconType.Football, Weight = 3f },
            new SlotIconData { IconID = SlotMachineIconType.Helmet, Weight = 2f },
            new SlotIconData { IconID = SlotMachineIconType.Star, Weight = 1f },
            new SlotIconData { IconID = SlotMachineIconType.Wrench, Weight = 1f },
            new SlotIconData { IconID = SlotMachineIconType.WildCard, Weight = 1f }
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
