using TcgEngine;
using UnityEngine;
using UnityEngine.Profiling;

namespace Assets.TcgEngine.Scripts.Gameplay
{
    public partial class GameLogicService
    {
        //This function is called often to update status/stats affected by ongoing abilities.
        //It first resets all bonuses to 0 (CleanOngoing) then recalculates to ensure they are still present.
        public virtual void UpdateOngoing()
        {
            Profiler.BeginSample("Update Ongoing");
            UpdateOngoingCards();
            UpdateOngoingKills();
            Profiler.EndSample();
        }

        protected virtual void UpdateOngoingCards()
        {
            for (int p = 0; p < game_data.players.Length; p++)
            {
                Player player = game_data.players[p];
                player.ClearOngoing();

                for (int c = 0; c < player.cards_board.Count; c++)
                    player.cards_board[c].ClearOngoing();

                for (int c = 0; c < player.cards_equip.Count; c++)
                    player.cards_equip[c].ClearOngoing();

                for (int c = 0; c < player.cards_hand.Count; c++)
                    player.cards_hand[c].ClearOngoing();
            }

            for (int p = 0; p < game_data.players.Length; p++)
            {
                Player player = game_data.players[p];
                UpdateOngoingAbilities(player, player.hero);

                for (int c = 0; c < player.cards_board.Count; c++)
                {
                    Card card = player.cards_board[c];
                    UpdateOngoingAbilities(player, card);
                }

                for (int c = 0; c < player.cards_equip.Count; c++)
                {
                    Card card = player.cards_equip[c];
                    UpdateOngoingAbilities(player, card);
                }
            }

            //Stats bonus
            for (int p = 0; p < game_data.players.Length; p++)
            {
                Player player = game_data.players[p];
                for (int c = 0; c < player.cards_board.Count; c++)
                {
                    Card card = player.cards_board[c];

                    //Taunt effect
                    if (card.HasStatus(StatusType.Protection) && !card.HasStatus(StatusType.Stealth))
                    {
                        player.AddOngoingStatus(StatusType.Protected, 0);

                        for (int tc = 0; tc < player.cards_board.Count; tc++)
                        {
                            Card tcard = player.cards_board[tc];
                            if (!tcard.HasStatus(StatusType.Protection) && !tcard.HasStatus(StatusType.Protected))
                            {
                                tcard.AddOngoingStatus(StatusType.Protected, 0);
                            }
                        }
                    }

                    //Status bonus
                    foreach (CardStatus status in card.status)
                        AddOngoingStatusBonus(card, status);
                    foreach (CardStatus status in card.ongoing_status)
                        AddOngoingStatusBonus(card, status);
                }

                for (int c = 0; c < player.cards_hand.Count; c++)
                {
                    Card card = player.cards_hand[c];
                    foreach (CardStatus status in card.status)
                        AddOngoingStatusBonus(card, status);
                    foreach (CardStatus status in card.ongoing_status)
                        AddOngoingStatusBonus(card, status);
                }
            }
        }

        protected virtual void UpdateOngoingKills()
        {
            //Kill stuff with 0 hp
            for (int p = 0; p < game_data.players.Length; p++)
            {
                Player player = game_data.players[p];
                for (int i = player.cards_board.Count - 1; i >= 0; i--)
                {
                    if (i < player.cards_board.Count)
                    {
                        Card card = player.cards_board[i];
                        // Player cards don't die from HP — they stay on the field
                        if (card.CardData.IsPlayer())
                            continue;
                        if (card.GetHP() <= 0)
                            DiscardCard(card);
                    }
                }
                for (int i = player.cards_equip.Count - 1; i >= 0; i--)
                {
                    if (i < player.cards_equip.Count)
                    {
                        Card card = player.cards_equip[i];
                        if (card.GetHP() <= 0)
                            DiscardCard(card);
                        Card bearer = player.GetBearerCard(card);
                        if (bearer == null)
                            DiscardCard(card);
                    }
                }
            }

            //Clear cards
            for (int c = 0; c < cards_to_clear.Count; c++)
                cards_to_clear[c].Clear();
            cards_to_clear.Clear();
        }

        protected virtual void UpdateOngoingAbilities(Player player, Card card)
        {
            if (card == null || !card.CanDoAbilities())
                return;

            var cabilities = card.GetAbilities();
            for (int a = 0; a < cabilities.Count; a++)
            {
                AbilityData ability = cabilities[a];
                if (ability != null && ability.trigger == AbilityTrigger.Ongoing && ability.AreTriggerConditionsMet(game_data, card))
                {
                    if (ability.target == AbilityTarget.Self)
                    {
                        if (ability.AreTargetConditionsMet(game_data, card, card))
                        {
                            ability.DoOngoingEffects(this, card, card);
                        }
                    }

                    if (ability.target == AbilityTarget.PlayerSelf)
                    {
                        if (ability.AreTargetConditionsMet(game_data, card, player))
                        {
                            ability.DoOngoingEffects(this, card, player);
                        }
                    }

                    if (ability.target == AbilityTarget.AllPlayers || ability.target == AbilityTarget.PlayerOpponent)
                    {
                        for (int tp = 0; tp < game_data.players.Length; tp++)
                        {
                            if (ability.target == AbilityTarget.AllPlayers || tp != player.player_id)
                            {
                                Player oplayer = game_data.players[tp];
                                if (ability.AreTargetConditionsMet(game_data, card, oplayer))
                                {
                                    ability.DoOngoingEffects(this, card, oplayer);
                                }
                            }
                        }
                    }

                    if (ability.target == AbilityTarget.EquippedCard)
                    {
                        if (card.CardData.IsEquipment())
                        {
                            Card target = player.GetBearerCard(card);
                            if (target != null && ability.AreTargetConditionsMet(game_data, card, target))
                            {
                                ability.DoOngoingEffects(this, card, target);
                            }
                        }
                        else if (card.equipped_uid != null)
                        {
                            Card target = game_data.GetCard(card.equipped_uid);
                            if (target != null && ability.AreTargetConditionsMet(game_data, card, target))
                            {
                                ability.DoOngoingEffects(this, card, target);
                            }
                        }
                    }

                    if (ability.target == AbilityTarget.AllCardsAllPiles || ability.target == AbilityTarget.AllCardsHand || ability.target == AbilityTarget.AllCardsBoard)
                    {
                        for (int tp = 0; tp < game_data.players.Length; tp++)
                        {
                            Player tplayer = game_data.players[tp];

                            if (ability.target == AbilityTarget.AllCardsAllPiles || ability.target == AbilityTarget.AllCardsHand)
                            {
                                for (int tc = 0; tc < tplayer.cards_hand.Count; tc++)
                                {
                                    Card tcard = tplayer.cards_hand[tc];
                                    if (ability.AreTargetConditionsMet(game_data, card, tcard))
                                    {
                                        ability.DoOngoingEffects(this, card, tcard);
                                    }
                                }
                            }

                            if (ability.target == AbilityTarget.AllCardsAllPiles || ability.target == AbilityTarget.AllCardsBoard)
                            {
                                for (int tc = 0; tc < tplayer.cards_board.Count; tc++)
                                {
                                    Card tcard = tplayer.cards_board[tc];
                                    if (ability.AreTargetConditionsMet(game_data, card, tcard))
                                    {
                                        ability.DoOngoingEffects(this, card, tcard);
                                    }
                                }
                            }

                            if (ability.target == AbilityTarget.AllCardsAllPiles)
                            {
                                for (int tc = 0; tc < tplayer.cards_equip.Count; tc++)
                                {
                                    Card tcard = tplayer.cards_equip[tc];
                                    if (ability.AreTargetConditionsMet(game_data, card, tcard))
                                    {
                                        ability.DoOngoingEffects(this, card, tcard);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        protected virtual void AddOngoingStatusBonus(Card card, CardStatus status)
        {
            if (status.type == StatusType.AddAttack)
                card.attack_ongoing += status.value;
            if (status.type == StatusType.AddHP)
                card.hp_ongoing += status.value;
            if (status.type == StatusType.AddManaCost)
                card.mana_ongoing += status.value;
        }
    }
}
