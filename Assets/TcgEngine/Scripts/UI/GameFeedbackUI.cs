using UnityEngine;
using TcgEngine.Client;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine.UI
{
    /// <summary>
    /// Orchestrator for all game feedback UI components.
    /// Attach to an empty GameObject in the Game scene.
    /// Assign sub-component references in the Inspector.
    /// </summary>
    public class GameFeedbackUI : MonoBehaviour
    {
        [Header("Sub-Components")]
        public ScoreboardHUD scoreboardHUD;
        public PlaySummaryPanel playSummaryPanel;
        public BigPlayOverlay bigPlayOverlay;
        public BoardStatOverlay boardStatOverlay;
        public StatChangePopup statChangePopup;
        public AbilitySpotlight abilitySpotlight;

        // Cached previous state for change detection
        private GamePhase prevPhase = GamePhase.None;
        private int prevP0Points = 0;
        private int prevP1Points = 0;
        private int prevOffensivePlayerId = -1;
        private int prevLastYardage = 0;
        private PlayType prevLastPlayType = PlayType.Huddle;

        private static GameFeedbackUI instance;
        public static GameFeedbackUI Get() => instance;

        void Awake()
        {
            instance = this;
        }

        void Start()
        {
            GameClient.Get().onRefreshAll += OnRefreshAll;
            GameClient.Get().onGameStart += OnGameStart;
        }

        void OnDestroy()
        {
            if (GameClient.Get() != null)
            {
                GameClient.Get().onRefreshAll -= OnRefreshAll;
                GameClient.Get().onGameStart -= OnGameStart;
            }
        }

        private void OnGameStart()
        {
            // Reset cached state on new game
            prevPhase = GamePhase.None;
            prevP0Points = 0;
            prevP1Points = 0;
            prevOffensivePlayerId = -1;
            prevLastYardage = 0;
        }

        private void OnRefreshAll()
        {
            Game g = GameClient.Get()?.GetGameData();
            if (g == null) return;

            Player p0 = g.players != null && g.players.Length > 0 ? g.players[0] : null;
            Player p1 = g.players != null && g.players.Length > 1 ? g.players[1] : null;

            // --- Scoreboard (always update) ---
            scoreboardHUD?.Refresh(g, p0, p1);

            // --- BoardStatOverlay ---
            boardStatOverlay?.Refresh(g, p0, p1);

            // --- Detect events for BigPlayOverlay ---
            DetectAndFireBigPlayEvents(g, p0, p1);

            // --- PlaySummaryPanel: show when phase transitions TO StartTurn after a play ---
            bool justFinishedPlay = prevPhase != GamePhase.StartTurn && g.phase == GamePhase.StartTurn
                                    && g.last_play_yardage != 0;
            if (justFinishedPlay)
                playSummaryPanel?.ShowSummary(g, p0, p1);

            // Cache state
            prevPhase = g.phase;
            prevP0Points = p0?.points ?? 0;
            prevP1Points = p1?.points ?? 0;
            prevOffensivePlayerId = g.current_offensive_player?.player_id ?? -1;
            prevLastYardage = g.last_play_yardage;
            prevLastPlayType = g.last_play_type;
        }

        private void DetectAndFireBigPlayEvents(Game g, Player p0, Player p1)
        {
            if (bigPlayOverlay == null) return;

            int curP0 = p0?.points ?? 0;
            int curP1 = p1?.points ?? 0;
            int curOffId = g.current_offensive_player?.player_id ?? -1;

            bool p0Scored = curP0 > prevP0Points;
            bool p1Scored = curP1 > prevP1Points;
            bool possessionChanged = prevOffensivePlayerId >= 0 && curOffId != prevOffensivePlayerId;

            // Touchdown
            if (p0Scored || p1Scored)
            {
                bigPlayOverlay.ShowEvent("TOUCHDOWN!", new Color(1f, 0.75f, 0f));
                return;
            }

            // Possession changes (turnover events)
            if (possessionChanged && g.phase == GamePhase.StartTurn)
            {
                bool wasPass = prevLastPlayType == PlayType.ShortPass || prevLastPlayType == PlayType.LongPass;
                bool wasRun = prevLastPlayType == PlayType.Run;

                if (wasPass)
                    bigPlayOverlay.ShowEvent("INTERCEPTION!", Color.red);
                else if (wasRun)
                    bigPlayOverlay.ShowEvent("FUMBLE!", new Color(1f, 0.5f, 0f));
                else
                    bigPlayOverlay.ShowEvent("TURNOVER ON DOWNS", new Color(0.8f, 0.8f, 0.8f));
                return;
            }

            // Big gain / sack — only fire when phase just became StartTurn
            if (prevPhase != GamePhase.StartTurn && g.phase == GamePhase.StartTurn)
            {
                int yards = g.last_play_yardage;
                if (yards >= 20)
                    bigPlayOverlay.ShowEvent($"BIG PLAY!  +{yards} YDS", new Color(0.2f, 0.9f, 0.3f));
                else if (yards <= -7)
                    bigPlayOverlay.ShowEvent($"SACK!  {yards} YDS", Color.red);
            }
        }
    }
}
