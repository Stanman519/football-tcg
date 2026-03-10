using Assets.TcgEngine.Scripts.Gameplay;
using System.Linq;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// Forces a fumble during live ball resolution. Grit-based resolution:
    ///   - If offense played EffectPreventTurnover (Ball Security) → auto-denied
    ///   - Otherwise compare board grit: def > off → turnover, off >= def → recovery
    ///   - Return yards = 2x grit difference (if turnover)
    ///   - Either outcome ends the play at subtotal
    ///
    /// Detected by ResolveLiveBallEffects via HasEffect&lt;EffectForceTurnover&gt;().
    /// </summary>
    [CreateAssetMenu(fileName = "EffectForceTurnover", menuName = "TcgEngine/Effect/ForceTurnover", order = 10)]
    public class EffectForceTurnover : EffectData
    {
        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            // Grit-based resolution is handled by ResolveLiveBallEffects directly.
            // This DoEffect only fires if resolution has already determined a turnover.
            Game game = logic.GetGameData();
            Player offense = game.current_offensive_player;

            int defGrit = game.GetCurrentDefensivePlayer().cards_board
                .Sum(c => c.Data.grit + c.GetStatusValue(StatusType.AddGrit));
            // B1 fix: include grit bonus stored by ResolveLiveBallEffects so return yards are not inflated
            int offGrit = offense.cards_board
                .Sum(c => c.Data.grit + c.GetStatusValue(StatusType.AddGrit))
                + game.live_ball_grit_bonus;

            int returnYards = Mathf.Max(0, (defGrit - offGrit) * 2);
            Debug.Log($"[LiveBall] Fumble! {caster?.card_id} forces turnover. Return: {returnYards} yds (defGrit={defGrit} offGrit={offGrit})");

            logic.HandleLiveBallTurnover(returnYards);
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            DoEffect(logic, ability, caster);
        }
    }
}
