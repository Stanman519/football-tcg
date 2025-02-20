using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SlotReel
{
    public string ReelName = "Reel";
    public bool IsFrozen = false;
    public string LastActiveIcon;
    // If true, we'll skip random selection and keep last result

    public List<SlotIcon> PossibleIcons = new List<SlotIcon>();

    public SlotReel(string reelName)
    {
        ReelName = reelName;
    }

    // Add or remove icons
    public void AddIcon(SlotIcon icon)
    {
        PossibleIcons.Add(icon);
    }

    public void RemoveIcon(string iconID)
    {
        PossibleIcons.RemoveAll(i => i.IconID == iconID);
    }

    // Example: you could have a method to find an icon by ID
    // or to "shift" to next index if you want that style manipulation
}
