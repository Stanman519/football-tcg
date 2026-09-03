#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Gameplay;

namespace TcgEngine.Dev
{
    // Runtime-callable entry point. Editor-only wrappers live in Editor/Dev/.
    public static class DevBootstrap
    {
        public const string PrefKey = "FL.DevScenarioPath";

        // Called from GameLogicService.StartChoosePlayersPhase (UNITY_EDITOR guarded).
        public static void ApplyScenario(GameLogicService logic)
        {
            var s = DevState.PendingScenario;
            if (s == null || DevState.ScenarioApplied) return;
            DevState.ScenarioApplied = true;

            var g = logic.GameData;
            if (g == null) return;

            g.current_down = s.currentDown;
            g.raw_ball_on = s.ballOn;
            g.plays_left_in_half = s.playsLeftInHalf;
            if (g.players.Length >= 1) g.players[0].points = s.score0;
            if (g.players.Length >= 2) g.players[1].points = s.score1;

            InjectCards(g.players[0], s.offenseHandCardIds, toBoard: false);
            if (g.players.Length >= 2) InjectCards(g.players[1], s.defenseHandCardIds, toBoard: false);
            InjectCards(g.players[0], s.offenseBoardCardIds, toBoard: true);
            if (g.players.Length >= 2) InjectCards(g.players[1], s.defenseBoardCardIds, toBoard: true);

            Debug.Log($"[DevBootstrap] Applied scenario '{s.scenarioName}'. Target: {s.targetPhase}");

            if (s.targetPhase == GamePhase.ChoosePlay)
            {
                logic.RevealNewPlayers();
                logic.StartPlayCallPhase();
            }
            else
            {
                logic.RefreshData();
            }
        }

        private static void InjectCards(Player player, List<string> ids, bool toBoard)
        {
            if (player == null || ids == null) return;
            foreach (string id in ids)
            {
                if (string.IsNullOrEmpty(id)) continue;
                Card found = null;
                for (int i = player.cards_deck.Count - 1; i >= 0; i--)
                {
                    if (player.cards_deck[i].card_id == id)
                    {
                        found = player.cards_deck[i];
                        player.cards_deck.RemoveAt(i);
                        break;
                    }
                }
                if (found == null)
                {
                    var data = CardData.Get(id);
                    if (data == null) { Debug.LogWarning($"[DevBootstrap] Card '{id}' not found"); continue; }
                    found = Card.Create(data, VariantData.GetDefault(), player);
                }
                if (toBoard) player.cards_board.Add(found);
                else player.cards_hand.Add(found);
            }
        }
    }
}
#endif
