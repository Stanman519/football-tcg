using System.Collections.Generic;
using System.Linq;
using TcgEngine;

namespace TcgEngine.Gameplay
{
    /// <summary>Result of coverage resolution: who catches, and who took the top receiver away.</summary>
    public struct CoverageResult
    {
        public Card receiver;            // target receiver after coverage (null = all covered)
        public Card ctrDefender;         // CTR card that caused the shift (null = no CTR)
        public Card coveredTopReceiver;  // the top receiver that was taken away (null = no shift)
    }

    public class ReceiverRankingSystem
    {
        private readonly List<Card> _eligibleReceivers;


        public ReceiverRankingSystem(List<Card> receivers, bool isDeepPass)
        {
            if (isDeepPass)
            {
                // Include both base card bonuses and any ongoing status bonuses
                _eligibleReceivers = receivers
                    .OrderByDescending(r => r.CardData.deep_pass_bonus + r.GetStatusValue(StatusType.AddedDeepPassBonus))
                    .ToList();
            }
            else
            {
                _eligibleReceivers = receivers
                    .OrderByDescending(r => r.CardData.short_pass_bonus + r.GetStatusValue(StatusType.AddedShortPassBonus))
                    .ToList();
            }
        }

        public CoverageResult ApplyCoverage(Game game_data)
        {
            // Determine the best receiver card after applying defensive coverages (CTR/CNR system)
            var coverageDawgs = game_data.GetCurrentDefensivePlayer()
                .cards_board
                .Where(c => c.HasAbility(AbilityTrigger.CoverNextReceiver) || c.HasAbility(AbilityTrigger.CoverTopReceiver));

            if (!coverageDawgs.Any(c => c.HasAbility(AbilityTrigger.CoverTopReceiver)))
                return new CoverageResult { receiver = _eligibleReceivers.FirstOrDefault() };

            var orderedCoverage = coverageDawgs.OrderBy(c => c.HasAbility(AbilityTrigger.CoverTopReceiver) ? 0 : 1);

            // Length of the covering group = how many top receivers get nullified
            var topReceiversNullifiedNumber = orderedCoverage.Count();
            Card ctr = orderedCoverage.First();
            Card coveredTop = _eligibleReceivers.FirstOrDefault();

            if (topReceiversNullifiedNumber > _eligibleReceivers.Count - 1)
            {
                // Everyone covered — no eligible receiver
                return new CoverageResult { receiver = null, ctrDefender = ctr, coveredTopReceiver = coveredTop };
            }

            return new CoverageResult
            {
                receiver = _eligibleReceivers[topReceiversNullifiedNumber], // next best receiver
                ctrDefender = ctr,
                coveredTopReceiver = coveredTop,
            };
        }
    }
}
