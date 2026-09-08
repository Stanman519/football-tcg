using System.Collections.Generic;
using System.Linq;
using TcgEngine;

namespace TcgEngine.Gameplay
{
    /// <summary>
    /// Server-side pick of the defender credited with ending the play.
    /// Deterministic and pure — drives the client tackle animation.
    /// </summary>
    public static class TacklerSelector
    {
        /// <summary>Returns uid of the credited tackler, or null (TD / incomplete / turnover ends elsewhere).</summary>
        public static string Choose(Game g)
        {
            if (g == null) return null;

            // Touchdown — nobody caught him
            if (g.raw_ball_on + g.yardage_this_play >= 100)
                return null;

            switch (g.last_fail_event)
            {
                case FailPlayEventType.Sack:
                case FailPlayEventType.TackleForLoss:
                    return g.fail_event_caster_uid; // the man who made the play

                case FailPlayEventType.IncompletePass:
                case FailPlayEventType.BattedPass:
                    return null; // ball never caught

                case FailPlayEventType.TippedPass:
                case FailPlayEventType.Interception:
                    return null; // turnover return end handled in M5

                case FailPlayEventType.QBFumble:
                case FailPlayEventType.RunnerFumble:
                    // Fumble spot contact — the forcer is the natural tackler
                    return g.fail_event_caster_uid;
            }

            // Normal run/pass tackle: pick by yardage band, fallback through groups,
            // tie-break highest grit then uid for determinism
            Player def = g.GetCurrentDefensivePlayer();
            if (def == null || def.cards_board == null || def.cards_board.Count == 0)
                return null;

            int net = g.yardage_this_play;
            PlayerPositionGrp[] order = net <= 3
                ? new[] { PlayerPositionGrp.DL, PlayerPositionGrp.LB, PlayerPositionGrp.DB }
                : net <= 7
                    ? new[] { PlayerPositionGrp.LB, PlayerPositionGrp.DB, PlayerPositionGrp.DL }
                    : new[] { PlayerPositionGrp.DB, PlayerPositionGrp.LB, PlayerPositionGrp.DL };

            foreach (var grp in order)
            {
                Card pick = def.cards_board
                    .Where(c => c.CardData != null && c.CardData.playerPosition == grp)
                    .OrderByDescending(c => c.CardData.grit)
                    .ThenBy(c => c.uid, System.StringComparer.Ordinal)
                    .FirstOrDefault();
                if (pick != null)
                    return pick.uid;
            }

            // No standard defender groups on board — take anyone
            return def.cards_board
                .OrderByDescending(c => c.CardData?.grit ?? 0)
                .ThenBy(c => c.uid, System.StringComparer.Ordinal)
                .FirstOrDefault()?.uid;
        }
    }
}
