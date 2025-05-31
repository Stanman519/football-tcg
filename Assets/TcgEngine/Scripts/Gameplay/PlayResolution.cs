using System.Collections.Generic;
using TcgEngine;

public class PlayResolution
{
    public bool BallIsLive { get; set; }
    public int YardageGained { get; set; }
    public bool Turnover { get; set; } = false;
    public List<AbilityQueueElement> ContributingAbilities { get; set; }
}