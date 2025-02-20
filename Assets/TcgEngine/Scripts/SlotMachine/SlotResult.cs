using System.Collections.Generic;

public class SlotResult
{
    // The final chosen icons for each reel
    public List<string> ChosenIcons = new List<string>();

    public SlotResult(int reelCount)
    {
        ChosenIcons.Capacity = reelCount;
    }

    // e.g. if you want to track star count, helmet count, etc.
    // you could parse these after creation
    public int StarCount;
    public int HelmetCount;
    // ...
}
