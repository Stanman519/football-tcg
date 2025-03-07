using System.Linq;

namespace Assets.TcgEngine.Scripts.Gameplay
{
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

        //TODO: make sure these get automated at the start of each play. save slot results too?
        public CardHistory(Game gData, Card card)
        { 
            PlayId = gData.turn_count;
            PlaysRemainingInHalf = gData.plays_left_in_half;
            MyStamina = card.current_stamina;
            Down = gData.current_down;
            DistanceToGo = gData.yardage_to_go;
            BallStartedOn = gData.raw_ball_on;
            MyTeamPlayType = gData.players.First(p => p.player_id == card.player_id).SelectedPlay;
            OpponentPlayType = gData.players.First(p => p.player_id != card.player_id).SelectedPlay;
        }
    }
}
