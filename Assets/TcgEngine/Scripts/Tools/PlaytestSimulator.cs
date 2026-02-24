using Assets.TcgEngine.Scripts.Gameplay;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TcgEngine.Playtest
{
    /// <summary>
    /// Simulates games to test balance and synergies
    /// Run this from Unity editor or build to test
    /// </summary>
    public class PlaytestSimulator : MonoBehaviour
    {
        [Header("Simulation Settings")]
        public int numGames = 1000;
        public bool verbose = false;
        
        [Header("Player 1 Setup")]
        public DeckData player1Deck;
        public HeadCoachCard player1Coach;
        
        [Header("Player 2 Setup")]
        public DeckData player2Deck;
        public HeadCoachCard player2Coach;

        // Results
        private int player1Wins = 0;
        private int player2Wins = 0;
        private int ties = 0;
        
        private List<int> player1Yards = new List<int>();
        private List<int> player2Yards = new List<int>();
        private List<int> player1Points = new List<int>();
        private List<int> player2Points = new List<int>();

        // Per-card tracking
        private Dictionary<string, int> cardPlayedCount = new Dictionary<string, int>();
        private Dictionary<string, int> cardWinCount = new Dictionary<string, int>();

        [ContextMenu("Run Simulation")]
        public void RunSimulation()
        {
            ResetStats();
            
            for (int i = 0; i < numGames; i++)
            {
                SimulateGame();
                
                if (verbose && (i + 1) % 100 == 0)
                {
                    Debug.Log($"Completed {i + 1}/{numGames} games...");
                }
            }
            
            PrintResults();
        }

        private void ResetStats()
        {
            player1Wins = 0;
            player2Wins = 0;
            ties = 0;
            player1Yards.Clear();
            player2Yards.Clear();
            player1Points.Clear();
            player2Points.Clear();
            cardPlayedCount.Clear();
            cardWinCount.Clear();
        }

        private void SimulateGame()
        {
            // Simplified game simulation
            // In a real implementation, this would use the actual game logic
            
            var gameState = new SimGameState();
            gameState.player1Coach = player1Coach;
            gameState.player2Coach = player2Coach;
            
            int playsInHalf = 12; // 6 plays per quarter, 2 quarters
            int currentQuarter = 1;
            
            // Simulate each play
            for (int play = 0; play < playsInHalf * 2; play++)
            {
                // Determine offensive player (simplified - alternate)
                bool player1Offense = play < playsInHalf;
                
                // Select play (simplified - random)
                PlayType selectedPlay = (PlayType)Random.Range(0, 3);
                
                // Calculate base yards
                int baseYards = GetBaseYards(selectedPlay, player1Offense ? gameState : null);
                
                // Apply card bonuses
                int cardBonus = CalculateCardBonuses(
                    player1Offense ? gameState.player1Board : gameState.player2Board,
                    selectedPlay
                );
                
                // Apply coach bonuses
                int coachBonus = CalculateCoachBonuses(
                    player1Offense ? gameState.player1Coach : gameState.player2Coach,
                    gameState,
                    selectedPlay
                );
                
                int totalYards = baseYards + cardBonus + coachBonus;
                
                // Track yards
                if (player1Offense)
                {
                    gameState.player1TotalYards += totalYards;
                    gameState.player1BallOn += totalYards;
                }
                else
                {
                    gameState.player2TotalYards += totalYards;
                    gameState.player2BallOn += totalYards;
                }
                
                // Check for first down
                bool firstDown = totalYards >= gameState.yardsToGo;
                
                if (firstDown)
                {
                    gameState.yardsToGo = 10;
                    // Check for touchdown (inside 20)
                    if ((player1Offense && gameState.player1BallOn >= 80) ||
                        (!player1Offense && gameState.player2BallOn >= 80))
                    {
                        // Touchdown!
                        if (player1Offense) gameState.player1Score += 7;
                        else gameState.player2Score += 7;
                    }
                }
                else
                {
                    gameState.yardsToGo -= totalYards;
                }
                
                // Check for turnover (simplified random)
                if (Random.value < 0.03f) // 3% turnover chance
                {
                    player1Offense = !player1Offense;
                }
                
                // Update down
                gameState.currentDown++;
                if (gameState.currentDown > 4 || firstDown)
                {
                    gameState.currentDown = 1;
                }
                
                // Half time
                if (play == playsInHalf)
                {
                    currentQuarter = 2;
                    gameState.player1BallOn = 25; // Kickoff
                    gameState.player2BallOn = 25;
                    gameState.yardsToGo = 10;
                }
            }
            
            // Record results
            if (gameState.player1Score > gameState.player2Score)
                player1Wins++;
            else if (gameState.player2Score > gameState.player1Score)
                player2Wins++;
            else
                ties++;
                
            player1Yards.Add(gameState.player1TotalYards);
            player2Yards.Add(gameState.player2TotalYards);
            player1Points.Add(gameState.player1Score);
            player2Points.Add(gameState.player2Score);
        }

        private int GetBaseYards(PlayType play, SimGameState state)
        {
            switch (play)
            {
                case PlayType.Run:
                    return Random.Range(2, 6); // 2-5 yards
                case PlayType.ShortPass:
                    return Random.Range(4, 9); // 4-8 yards
                case PlayType.LongPass:
                    return Random.Range(8, 20); // 8-19 yards
                default:
                    return 3;
            }
        }

        private int CalculateCardBonuses(List<Card> board, PlayType play)
        {
            int bonus = 0;
            foreach (Card card in board)
            {
                CardData data = card.CardData;
                switch (play)
                {
                    case PlayType.Run:
                        bonus += data.attack; // Using attack as run bonus
                        break;
                    case PlayType.ShortPass:
                        // Would check short pass bonus
                        break;
                    case PlayType.LongPass:
                        // Would check deep pass bonus
                        break;
                }
            }
            return bonus;
        }

        private int CalculateCoachBonuses(HeadCoachCard coach, SimGameState state, PlayType play)
        {
            if (coach == null) return 0;
            
            int bonus = 0;
            
            // Check coach-specific bonuses
            // This would be expanded based on coach abilities
            
            return bonus;
        }

        private void PrintResults()
        {
            Debug.Log("=== PLAYTEST SIMULATION RESULTS ===");
            Debug.Log($"Total Games: {numGames}");
            Debug.Log($"Player 1 Win Rate: {(float)player1Wins / numGames * 100:F1}%");
            Debug.Log($"Player 2 Win Rate: {(float)player2Wins / numGames * 100:F1}%");
            Debug.Log($"Tie Rate: {(float)ties / numGames * 100:F1}%");
            Debug.Log("");
            Debug.Log($"Player 1 Avg Yards: {player1Yards.Average():F1}");
            Debug.Log($"Player 2 Avg Yards: {player2Yards.Average():F1}");
            Debug.Log($"Player 1 Avg Points: {player1Points.Average():F1}");
            Debug.Log($"Player 2 Avg Points: {player2Points.Average():F1}");
        }

        // Simplified game state for simulation
        private class SimGameState
        {
            public HeadCoachCard player1Coach;
            public HeadCoachCard player2Coach;
            public List<Card> player1Board = new List<Card>();
            public List<Card> player2Board = new List<Card>();
            
            public int player1Score = 0;
            public int player2Score = 0;
            public int player1TotalYards = 0;
            public int player2TotalYards = 0;
            
            public int player1BallOn = 25;
            public int player2BallOn = 25;
            public int yardsToGo = 10;
            public int currentDown = 1;
        }
    }
}
