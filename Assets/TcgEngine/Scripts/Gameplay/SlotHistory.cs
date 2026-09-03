using System.Collections.Generic;

namespace TcgEngine.Gameplay
{
    [System.Serializable]
    public class SlotHistory
    {
        public int turnId { get; set; }
        public List<SlotMachineResultDTO> slots { get; set; }
        public int offensivePlayerId { get; set; }

    }
}
