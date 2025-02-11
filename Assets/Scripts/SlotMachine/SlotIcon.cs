using UnityEngine;

[System.Serializable]
public class SlotIcon
{
    public string IconID;
    // e.g. "Football", "Helmet", "Star", "Wrench"

    public float Weight = 1f;
    // weighting if some icons are more/less likely.
    // You can ignore if all icons have equal probability.
}
