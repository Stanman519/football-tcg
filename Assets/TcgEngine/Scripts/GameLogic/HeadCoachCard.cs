
using System.Collections.Generic;

namespace TcgEngine
{
    [System.Serializable]
    public class HCPlayerSchemeData
    {
        public int pos_max { get; set; }
    }
    [System.Serializable]
    public class HeadCoachCard
    {
        public Dictionary<PlayerPositionGrp, HCPlayerSchemeData> positional_Scheme;
    }
}