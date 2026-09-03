#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine.Dev
{
    [CreateAssetMenu(menuName = "First&Long/Dev Scenario", fileName = "DevScenario")]
    public class DevScenario : ScriptableObject
    {
        public string scenarioName = "Untitled";
        public GamePhase targetPhase = GamePhase.ChoosePlayers;

        [Header("Game State")]
        [Range(1,4)] public int currentDown = 1;
        [Range(1,99)] public int ballOn = 25;
        public int playsLeftInHalf = 11;
        public int score0 = 0;
        public int score1 = 0;

        [Header("Hands (card_id strings — see Resources/Cards)")]
        public List<string> offenseHandCardIds = new List<string>();
        public List<string> defenseHandCardIds = new List<string>();

        [Header("Board (auto-placed for ChoosePlay target)")]
        public List<string> offenseBoardCardIds = new List<string>();
        public List<string> defenseBoardCardIds = new List<string>();

        public bool pauseAI = true;
    }
}
#endif
