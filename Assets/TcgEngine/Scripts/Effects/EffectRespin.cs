using Assets.TcgEngine.Scripts.Gameplay;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Effects
{
    /// <summary>
    /// Gives a free slot respin
    /// </summary>
    public class EffectRespin : EffectData
    {
        public int count = 1; // Number of free respins
        
        // Can target specific reel or all reels
        public bool allReels = true;
        public int targetReel = -1;

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster)
        {
            Game game = logic.GetGameData();
            game.pending_respins += count;
            Debug.Log($"[Respin] {count} respin(s) queued. Total pending: {game.pending_respins}");
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Card target)
        {
            DoEffect(logic, ability, caster);
        }

        public override void DoEffect(GameLogicService logic, AbilityData ability, Card caster, Player target)
        {
            DoEffect(logic, ability, caster);
        }
    }
}
