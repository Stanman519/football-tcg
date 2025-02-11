using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public class SlotMachineTestMulti : MonoBehaviour
{
    [Serializable]
    public class SlotIconData
    {
        public string IconID;
        public float Weight = 1f;
        public Sprite IconSprite;
    }

    [Serializable]
    public class ReelUI
    {
        public Image TopImage;
        public Image MiddleImage;
        public Image BottomImage;
    }

    [Header("UI Refs for Each Reel (3 total)")]
    public ReelUI reel1UI;
    public ReelUI reel2UI;
    public ReelUI reel3UI;

    [Header("Icon Data For Each Reel")]
    public List<SlotIconData> reel1Icons;
    public List<SlotIconData> reel2Icons;
    public List<SlotIconData> reel3Icons;

    [Header("Spin Durations (seconds before each reel stops)")]
    public float reel1StopDelay = 1.5f;
    public float reel2StopDelay = 2.0f;
    public float reel3StopDelay = 2.5f;

    // Called by the Spin button
    public void OnClickSpin()
    {
        // Start all reels at once
        StartCoroutine(SpinReel(1, reel1UI, reel1Icons, reel1StopDelay));
        StartCoroutine(SpinReel(2, reel2UI, reel2Icons, reel2StopDelay));
        StartCoroutine(SpinReel(3, reel3UI, reel3Icons, reel3StopDelay));
    }

    private IEnumerator SpinReel(int reelNumber, ReelUI reelUI, List<SlotIconData> reelIcons, float stopDelay)
    {
        float elapsed = 0f;

        // 1) Randomize the reel visually until time > stopDelay
        while (elapsed < stopDelay)
        {
            elapsed += Time.deltaTime;

            // "Fake spin": pick random icons for top/mid/bottom each frame
            string topID = PickRandomIconID(reelIcons);
            string midID = PickRandomIconID(reelIcons);
            string botID = PickRandomIconID(reelIcons);

            UpdateReelUI(reelUI, topID, midID, botID);

            yield return null; // wait a frame
        }

        // 2) Now that we've waited stopDelay, we do the final weighted pick
        ReelWindow finalWindow = PickThreeIcons(reelIcons);
        UpdateReelUI(reelUI, finalWindow.TopIcon, finalWindow.ActiveIcon, finalWindow.BottomIcon);

        Debug.Log($"Reel {reelNumber} stopped. Final middle = {finalWindow.ActiveIcon}");
        // (Optionally) store the final result in some manager if you want to check yardage, etc.
    }

    // Just for the final "3 icons" structure
    private class ReelWindow
    {
        public string TopIcon;
        public string ActiveIcon; // the "middle"
        public string BottomIcon;
        public ReelWindow(string top, string active, string bottom)
        {
            TopIcon = top;
            ActiveIcon = active;
            BottomIcon = bottom;
        }
    }

    #region WeightedPickLogic

    private ReelWindow PickThreeIcons(List<SlotIconData> baseIcons)
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
        // ReduceIconWeight(tempList, bot);

        return new ReelWindow(top, mid, bot);
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

    private string WeightedPick(List<SlotIconData> icons)
    {
        if (icons.Count == 0)
            return "EMPTY";

        float total = 0f;
        foreach (var i in icons)
            total += i.Weight;

        float rand = UnityEngine.Random.Range(0f, total);
        float cumulative = 0f;
        foreach (var i in icons)
        {
            cumulative += i.Weight;
            if (rand <= cumulative)
            {
                return i.IconID;
            }
        }
        return icons[icons.Count - 1].IconID;
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

    #endregion

    /// <summary>
    /// Just picks a random icon in reelIcons for the "spinning" animation (not weighted).
    /// </summary>
    private string PickRandomIconID(List<SlotIconData> reelIcons)
    {
        if (reelIcons == null || reelIcons.Count == 0)
            return "EMPTY";
        return reelIcons[UnityEngine.Random.Range(0, reelIcons.Count)].IconID;
    }

    private void UpdateReelUI(ReelUI reelUI, string topID, string midID, string botID)
    {
        if (reelUI.TopImage) reelUI.TopImage.sprite = GetSpriteForID(topID);
        if (reelUI.MiddleImage) reelUI.MiddleImage.sprite = GetSpriteForID(midID);
        if (reelUI.BottomImage) reelUI.BottomImage.sprite = GetSpriteForID(botID);
    }

    private Sprite GetSpriteForID(string iconID)
    {
        // you might unify or do reel-specific lookups, 
        // but for a quick approach, just search each reel's data 
        // or store all icons in a dictionary
        // For demonstration, let's search all:
        var s = TryFindSpriteInList(iconID, reel1Icons);
        if (!s) s = TryFindSpriteInList(iconID, reel2Icons);
        if (!s) s = TryFindSpriteInList(iconID, reel3Icons);
        return s;
    }

    private Sprite TryFindSpriteInList(string iconID, List<SlotIconData> list)
    {
        foreach (var icon in list)
            if (icon.IconID == iconID) return icon.IconSprite;
        return null;
    }
}