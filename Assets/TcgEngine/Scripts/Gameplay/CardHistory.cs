using System;
using System.Collections.Generic;
using System.Linq;

namespace Assets.TcgEngine.Scripts.Gameplay
{
    [Serializable]
    public class CardHistory
    {
        public int PlayId { get; set; }
        public int PlaysRemainingInHalf { get; set; }
        public int MyStamina { get; set; }
        public int Down { get; set; }
        public int DistanceToGo { get; set; }
        public int BallStartedOn { get; set; }
        public PlayType MyTeamPlayType { get; set; }
        public PlayType OpponentPlayType { get; set; }
        public bool CorrectGuess => MyTeamPlayType == OpponentPlayType;
        public int YardsGained { get; set; }
        public PlayResult PlayResult { get; set; }
        public List<string> TeammateUids { get; set; }

        public CardHistory(Game gData, Card card)
        {
            PlayId = gData.turn_count;
            PlaysRemainingInHalf = gData.plays_left_in_half;
            MyStamina = card.current_stamina;
            Down = gData.current_down;
            DistanceToGo = gData.yardage_to_go;
            BallStartedOn = gData.raw_ball_on;
            Player myPlayer = gData.players.First(p => p.player_id == card.player_id);
            Player opponent = gData.players.First(p => p.player_id != card.player_id);
            MyTeamPlayType = myPlayer.SelectedPlay;
            OpponentPlayType = opponent.SelectedPlay;
            TeammateUids = myPlayer.cards_board
                .Where(c => c.uid != card.uid)
                .Select(c => c.uid)
                .ToList();
        }
    }
}
