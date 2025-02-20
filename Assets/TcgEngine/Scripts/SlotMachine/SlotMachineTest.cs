using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public class SlotMachineTest : MonoBehaviour
{
    [Serializable]
    public class SlotIconData
    {
        public string IconID;    // e.g. "Star", "Helmet"
        public float Weight = 1; // how likely it is
        public Sprite IconSprite;
    }

    [Serializable]
    public class ReelUI
    {
        public Image TopImage;
        public Image MiddleImage;
        public Image BottomImage;
    }

    [Header("UI Refs for Each of the 3 Reels")]
    public ReelUI[] reelsUI;  // We expect size = 3 in the Inspector

    [Header("Available Icons (Base Weights)")]
    public List<SlotIconData> baseIcons; // e.g. Star, Helmet, Football, etc.

    // We'll do 3 "reels," each using the same icons in this demo
    private const int REEL_COUNT = 3;

    // Called by the Spin Button

    public void OnClickSpin()
    {
        // For each reel, pick 3 icons (Top, Middle, Bottom)
        // then show them in the UI
        for (int reelIndex = 0; reelIndex < REEL_COUNT; reelIndex++)
        {
            // 1. Copy the baseIcons into a temporary list
            List<SlotIconData> tempList = CopyIconList(baseIcons);

            // 2. Weighted pick the Middle icon (active result)
            var middleID = WeightedPick(tempList);
            // reduce the weight by 1 to avoid duplicates
            ReduceIconWeight(tempList, middleID);

            // 3. Weighted pick the Top icon
            var topID = WeightedPick(tempList);
            ReduceIconWeight(tempList, topID);

            // 4. Weighted pick the Bottom icon
            var bottomID = WeightedPick(tempList);
            // if you want to avoid duplicates among these three, reduce again
            ReduceIconWeight(tempList, bottomID);

            // Update the reel UI
            UpdateReelUI(reelIndex, topID, middleID, bottomID);
        }
    }

    /// <summary>
    /// Creates a shallow copy of the baseIcons so we can modify weights without affecting the original.
    /// </summary>
    private List<SlotIconData> CopyIconList(List<SlotIconData> source)
    {
        List<SlotIconData> copy = new List<SlotIconData>();
        foreach (var icon in source)
        {
            SlotIconData newIcon = new SlotIconData
            {
                IconID = icon.IconID,
                Weight = icon.Weight,
                IconSprite = icon.IconSprite
            };
            copy.Add(newIcon);
        }
        return copy;
    }

    /// <summary>
    /// Weighted pick an IconID from the given list.
    /// </summary>
    private string WeightedPick(List<SlotIconData> icons)
    {
        if (icons == null || icons.Count == 0)
            return "EMPTY";

        // sum total weights
        float total = 0f;
        foreach (var icon in icons)
        {
            total += icon.Weight;
        }
        // random val
        float rand = UnityEngine.Random.Range(0f, total);
        float cumulative = 0f;
        foreach (var icon in icons)
        {
            cumulative += icon.Weight;
            if (rand <= cumulative)
            {
                return icon.IconID;
            }
        }

        return icons[icons.Count - 1].IconID; // fallback
    }

    /// <summary>
    /// Reduces the weight of an icon by 1 in the local list
    /// (treating each 1 as a "slot" in the reel).
    /// </summary>
    private void ReduceIconWeight(List<SlotIconData> icons, string chosenID)
    {
        for (int i = 0; i < icons.Count; i++)
        {
            if (icons[i].IconID == chosenID)
            {
                if (icons[i].Weight > 0)
                    icons[i].Weight -= 1;
                // if weight hits 0, you could optionally remove from the list
                // icons.RemoveAt(i);
                break;
            }
        }
    }

    /// <summary>
    /// Updates the reel UI images with the sprites for each icon ID.
    /// </summary>
    private void UpdateReelUI(int reelIndex, string topID, string midID, string botID)
    {
        if (reelIndex < 0 || reelIndex >= reelsUI.Length) return;

        var reel = reelsUI[reelIndex];

        // find the sprites for each ID
        Sprite topSprite = GetSpriteForID(topID);
        Sprite midSprite = GetSpriteForID(midID);
        Sprite botSprite = GetSpriteForID(botID);

        reel.TopImage.sprite = topSprite;
        reel.MiddleImage.sprite = midSprite;
        reel.BottomImage.sprite = botSprite;
    }

    private Sprite GetSpriteForID(string iconID)
    {
        // find in baseIcons
        foreach (var icon in baseIcons)
        {
            if (icon.IconID == iconID)
                return icon.IconSprite;
        }
        return null; // or a default sprite
    }
}
