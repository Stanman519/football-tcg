/*using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;



public class SlotMachineTestMulti : MonoBehaviour
{
    private Dictionary<string, Sprite> iconSprites = new Dictionary<string, Sprite>();
    public SlotMachineTestMulti(List<SlotIconData> reel1, List<SlotIconData> reel2, List<SlotIconData> reel3)
    {
        reel1Icons = new List<SlotIconData>(reel1);
        reel2Icons = new List<SlotIconData>(reel2);
        reel3Icons = new List<SlotIconData>(reel3);
        extraReels = new List<List<SlotIconData>>();

        

        LoadIcons(reel1);
        LoadIcons(reel2);
        LoadIcons(reel3);
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



    [Header("Spin Durations (seconds before each reel stops)")]
    public float reel1StopDelay = 1.5f;
    public float reel2StopDelay = 2.0f;
    public float reel3StopDelay = 2.5f;

    

    public List<ReelUI> GetActiveReelIcons()
    {
        return new List<ReelUI> { reel1UI, reel2UI, reel3UI };
    }





}*/