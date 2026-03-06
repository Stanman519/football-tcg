using Assets.TcgEngine.Scripts.Gameplay;
using System.Linq;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// Forces a turnover during live ball resolution (Step 1 — highest priority, uncounterable).
    /// Switches possession and resets the drive. Detected by ResolveLiveBallEffects via HasEffect<EffectForceTurnover>().
    ///
    /// Use on DefLiveBall cards. Set slotRequirements on the CardData for the play cost.
    /// Gate further with conditions on the ability (e.g. ConditionCoverageGuess for correct coverage read).
    /// </summary>
    [CreateAssetMenu(fileName = "EffectForceTurnover", menuName = "TcgEngine/Effect/ForceTurnover", order = 10)]
    public class EffectForceTurnover : EffectData
    {
        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            Game game = logic.GetGameData();
            Debug.Log($"[LiveBall] Interception! {caster?.card_id} forces turnover.");

            // Return yardage = 2x grit advantage for the intercepting defender's team
            Player offense = game.current_offensive_player;
            int defGrit = game.GetCurrentDefensivePlayer().cards_board
                .Sum(c => c.Data.grit + c.GetStatusValue(StatusType.AddGrit));
            int offGrit = offense.cards_board
                .Sum(c => c.Data.grit + c.GetStatusValue(StatusType.AddGrit));

            int returnYards = Mathf.Max(0, (defGrit - offGrit) * 2);
            Debug.Log($"[LiveBall] Return yards: {returnYards} (defGrit={defGrit} offGrit={offGrit})");

            // Switch possession — new offense starts at their 25 + return yards
            logic.HandleLiveBallTurnover(returnYards);
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            DoEffect(logic, ability, caster);
        }
    }
}
