using System.Collections.Generic;
using System.Linq;
using TcgEngine;

namespace Assets.TcgEngine.Scripts.Gameplay
{
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
        public Card ApplyCoverage(Game game_data, ReceiverRankingSystem rankingSystem)
        {
            // method to determine the best receiver card after applying defensive coverages with the CTR/CNR system

            // if there are not CTR/CNRs just return the top receiver card iguess
            var coverageDawgs = game_data.GetCurrentDefensivePlayer()
                .cards_board
                .Where(c => c.HasAbility(AbilityTrigger.CoverNextReceiver) || c.HasAbility(AbilityTrigger.CoverTopReceiver));

            //TODO: this needs to also return the defensive player


            if (!coverageDawgs.Any(c => c.HasAbility(AbilityTrigger.CoverTopReceiver)))
                return _eligibleReceivers.FirstOrDefault();

            var orderedCoverage = coverageDawgs.OrderBy(c => c.HasAbility(AbilityTrigger.CoverTopReceiver) ? 0 : 1);

            // i guess we dont even really need to order it? as long as there is 1 CTR...
            // then the length of the covering players is how many bonuses you remove from the beginning of the list of receivers

            var topReceiversNullifiedNumber = orderedCoverage.Count();

            if (topReceiversNullifiedNumber > _eligibleReceivers.Count - 1)
            {
                return null;
            }
            return _eligibleReceivers[topReceiversNullifiedNumber]; //take the next best receiver.
           
        }


    }

}
