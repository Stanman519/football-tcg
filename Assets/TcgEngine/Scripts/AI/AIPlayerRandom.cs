using Assets.TcgEngine.Scripts.Gameplay;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TcgEngine.AI
{
    /// <summary>
    /// AI player making random decisions per football phase. Bad AI but useful for testing.
    /// </summary>

    public class AIPlayerRandom : AIPlayer
    {
        private bool is_playing = false;
        private bool is_selecting = false;

        private System.Random rand = new System.Random();

        private GamePhase[] relevantPhases = { GamePhase.ChoosePlayers, GamePhase.ChoosePlay, GamePhase.LiveBall };

        public AIPlayerRandom(GameLogicService gameplay, int id, int level)
        {
            this.gameplay = gameplay;
            player_id = id;
        }

        public override void Update()
        {
            if (!CanPlay())
                return;

            Game game_data = gameplay.GetGameData();
            Player player = game_data.GetPlayer(player_id);

            if (game_data.HasEnded())
                return;

            // Phase-based action
            if (!is_playing && relevantPhases.Contains(game_data.phase))
            {
                if (!player.IsReadyForPhase(game_data.phase))
                {
                    is_playing = true;
                    TimeTool.StartCoroutine(AiTurn());
                }
            }

            // Selector handling
            if (!is_selecting && game_data.selector != SelectorType.None && game_data.selector_player_id == player_id)
            {
                if (game_data.selector == SelectorType.SelectTarget)
                {
                    is_selecting = true;
                    TimeTool.StartCoroutine(AiSelectTarget());
                }
                if (game_data.selector == SelectorType.SelectorCard)
                {
                    is_selecting = true;
                    TimeTool.StartCoroutine(AiSelectCard());
                }
                if (game_data.selector == SelectorType.SelectorChoice)
                {
                    is_selecting = true;
                    TimeTool.StartCoroutine(AiSelectChoice());
                }
                if (game_data.selector == SelectorType.SelectorCost)
                {
                    is_selecting = true;
                    TimeTool.StartCoroutine(AiSelectCost());
                }
            }

            // Mulligan
            if (!is_selecting && game_data.IsPlayerMulliganTurn(player))
            {
                is_selecting = true;
                TimeTool.StartCoroutine(AiSelectMulligan());
            }
        }

        private IEnumerator AiTurn()
        {
            yield return new WaitForSeconds(1f);

            Game game_data = gameplay.GetGameData();
            Player player = game_data.GetPlayer(player_id);
            GamePhase phase = game_data.phase;
            Player offPlayer = game_data.current_offensive_player;
            bool isOffense = offPlayer != null && offPlayer.player_id == player_id;

            if (phase == GamePhase.ChoosePlayers)
            {
                // Play up to 3 random player cards
                for (int i = 0; i < 3; i++)
                {
                    yield return new WaitForSeconds(0.3f);
                    PlayRandomPlayerCard(player, isOffense);
                }
            }
            else if (phase == GamePhase.ChoosePlay)
            {
                // Pick a random play type
                PlayType[] plays = { PlayType.Run, PlayType.ShortPass, PlayType.LongPass };
                player.SelectedPlay = plays[rand.Next(plays.Length)];

                // Optionally play a random enhancer
                CardType enhType = isOffense ? CardType.OffensivePlayEnhancer : CardType.DefensivePlayEnhancer;
                var enhancers = player.cards_hand.Where(c => c.Data.type == enhType).ToList();
                if (enhancers.Count > 0 && rand.Next(2) == 0) // 50% chance to use enhancer
                {
                    Card enh = enhancers[rand.Next(enhancers.Count)];
                    player.PlayEnhancer = enh;
                }
            }
            else if (phase == GamePhase.LiveBall)
            {
                // Play a random live ball card if available
                CardType liveType = isOffense ? CardType.OffLiveBall : CardType.DefLiveBall;
                var liveCards = player.cards_hand
                    .Where(c => c.Data.type == liveType && game_data.CanPlayCard(c, CardPositionSlot.None))
                    .ToList();
                if (liveCards.Count > 0)
                {
                    Card card = liveCards[rand.Next(liveCards.Count)];
                    gameplay.PlayCard(card, CardPositionSlot.None);
                }
            }

            yield return new WaitForSeconds(0.3f);

            // Signal ready
            game_data = gameplay.GetGameData();
            player = game_data.GetPlayer(player_id);
            if (!player.IsReadyForPhase(phase))
            {
                player.SetReadyForPhase(phase, true);
                game_data.playerPhaseReady[player_id] = true;
            }

            is_playing = false;
        }

        private void PlayRandomPlayerCard(Player player, bool isOffense)
        {
            if (!CanPlay()) return;
            Game game_data = gameplay.GetGameData();

            var playerCards = player.cards_hand
                .Where(c => {
                    if (!c.CardData.IsPlayer()) return false;
                    var pos = c.CardData.playerPosition;
                    if (pos == PlayerPositionGrp.NONE) return false;
                    if (isOffense) return game_data.offensive_pos_grps.Contains(pos);
                    return game_data.defensive_pos_grps.Contains(pos);
                })
                .ToList();

            if (playerCards.Count == 0) return;

            Card card = playerCards[rand.Next(playerCards.Count)];

            // Find a valid slot
            var slots = CardPositionSlot.GetAll(player.player_id)
                .Where(s => s.posGroupType == card.CardData.playerPosition)
                .ToList();
            foreach (var slot in slots)
            {
                if (game_data.CanPlayCard(card, slot))
                {
                    gameplay.PlayCard(card, slot);
                    return;
                }
            }
        }

        private IEnumerator AiSelectCard()
        {
            yield return new WaitForSeconds(0.5f);
            SelectCard();
            yield return new WaitForSeconds(0.5f);
            CancelSelect();
            is_selecting = false;
        }

        private IEnumerator AiSelectTarget()
        {
            yield return new WaitForSeconds(0.5f);
            SelectTarget();
            yield return new WaitForSeconds(0.5f);
            CancelSelect();
            is_selecting = false;
        }

        private IEnumerator AiSelectChoice()
        {
            yield return new WaitForSeconds(0.5f);
            SelectChoice();
            yield return new WaitForSeconds(0.5f);
            CancelSelect();
            is_selecting = false;
        }

        private IEnumerator AiSelectCost()
        {
            yield return new WaitForSeconds(0.5f);
            CancelSelect();
            is_selecting = false;
        }

        private IEnumerator AiSelectMulligan()
        {
            yield return new WaitForSeconds(0.5f);
            SelectMulligan();
            yield return new WaitForSeconds(0.5f);
            is_selecting = false;
        }

        //----------

        public void SelectCard()
        {
            if (!CanPlay()) return;
            Game game_data = gameplay.GetGameData();
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);
            Card caster = game_data.GetCard(game_data.selector_caster_uid);
            if (ability != null && caster != null)
            {
                List<Card> card_list = ability.GetCardTargets(game_data, caster);
                if (card_list.Count > 0)
                {
                    Card card = card_list[rand.Next(0, card_list.Count)];
                    gameplay.SelectCard(card);
                }
            }
        }

        public void SelectTarget()
        {
            if (!CanPlay()) return;
            Game game_data = gameplay.GetGameData();
            if (game_data.selector != SelectorType.None)
            {
                int target_player = player_id;
                AbilityData ability = AbilityData.Get(game_data.selector_ability_id);
                if (ability != null && ability.target == AbilityTarget.SelectTarget)
                    target_player = (player_id == 0 ? 1 : 0);

                Player tplayer = game_data.GetPlayer(target_player);
                if (tplayer.cards_board.Count > 0)
                {
                    Card random = tplayer.GetRandomCard(tplayer.cards_board, rand);
                    if (random != null)
                        gameplay.SelectCard(random);
                }
            }
        }

        public void SelectChoice()
        {
            if (!CanPlay()) return;
            Game game_data = gameplay.GetGameData();
            if (game_data.selector != SelectorType.None)
            {
                AbilityData ability = AbilityData.Get(game_data.selector_ability_id);
                if (ability != null && ability.chain_abilities.Length > 0)
                {
                    int choice = rand.Next(0, ability.chain_abilities.Length);
                    gameplay.SelectChoice(choice);
                }
            }
        }

        public void CancelSelect()
        {
            if (CanPlay())
                gameplay.CancelSelection();
        }

        public void SelectMulligan()
        {
            if (!CanPlay()) return;
            Game game_data = gameplay.GetGameData();
            if (game_data.phase == GamePhase.Mulligan)
            {
                Player player = game_data.GetPlayer(player_id);
                string[] cards = new string[0]; //Don't mulligan
                gameplay.Mulligan(player, cards);
            }
        }
    }
}
