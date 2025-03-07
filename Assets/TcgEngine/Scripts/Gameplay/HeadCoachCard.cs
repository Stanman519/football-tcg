using System;
using System.Collections.Generic;

namespace Assets.TcgEngine.Scripts.Gameplay
{
    [Serializable]
    public class HCPlayerSchemeData
    {
        public int pos_max { get; set; }
    }
    [Serializable]
    public class HeadCoachCard
    {
        public Dictionary<PlayerPositionGrp, HCPlayerSchemeData> positional_Scheme;

    }
}