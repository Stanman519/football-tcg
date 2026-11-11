using UnityEngine;
using TcgEngine.Client;
using TcgEngine.Gameplay;

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
        public PlayCallRevealPanel playCallRevealPanel;
        public LiveBallRevealPanel liveBallRevealPanel;

        // Cached previous state for change detection
        private GamePhase prevPhase = GamePhase.None;

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
            prevPhase = GamePhase.None;
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

            // BigPlayOverlay is fired from PlayAnimationController choreography at the moment the
            // event happens — the old StartTurn heuristics guessed from possession/yardage and
            // could mislabel (e.g. a QB fumble on a pass read as "INTERCEPTION!").

            // --- PlayCallRevealPanel: show when phase transitions TO RevealPlayCalls ---
            bool justEnteredReveal = prevPhase != GamePhase.RevealPlayCalls && g.phase == GamePhase.RevealPlayCalls;
            if (justEnteredReveal && playCallRevealPanel != null)
            {
                int localId = GameClient.Get().GetPlayerID();
                Player localP = localId == 0 ? p0 : p1;
                Player oppP = localId == 0 ? p1 : p0;
                playCallRevealPanel.ShowReveal(g, localP, oppP);
            }

            // LiveBallRevealPanel is driven by the LiveBall Reveal event (PlayAnimationController beat
            // queue) so it starts in step with the server's beat timing, not on first card visibility.

            // PlaySummaryPanel retired: YardageSummaryPanel (Resolution tail) is the single recap.
            // The old one re-derived its breakdown client-side from base card stats, ignoring
            // status buffs, so it printed different numbers than the server for the same play.

            prevPhase = g.phase;
        }
    }
}
