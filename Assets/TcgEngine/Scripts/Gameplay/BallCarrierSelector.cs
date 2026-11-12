using System.Linq;
using TcgEngine;

namespace TcgEngine.Gameplay
{
    /// <summary>
    /// Who carries the ball on a run (for animation + recap). RBs and TEs share the RB_TE position
    /// group, so "best run bonus in the group" used to hand the ball to a tight end.
    /// Rule: best run-bonus running back; a tight end only if there is no running back on the field.
    /// </summary>
    public static class BallCarrierSelector
    {
        public static Card Pick(Player offense)
        {
            if (offense == null) return null;
            return offense.cards_board
                .Where(c => c != null && c.Data != null && c.Data.playerPosition == PlayerPositionGrp.RB_TE)
                .OrderBy(c => c.Data.isTightEnd ? 1 : 0)
                .ThenByDescending(c => c.Data.run_bonus)
                .FirstOrDefault();
        }
    }
}
