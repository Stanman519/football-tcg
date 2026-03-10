using System.Collections.Generic;
using System.Linq;
using TcgEngine;
using TcgEngine.Client;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Gameplay
{
    public partial class GameLogicService
    {
        //---- Gameplay Actions --------------

        public virtual void SelectPlayerCardForBoard(Card card)
        {
            // check l
        }

        public virtual void PlayCard(Card card, CardPositionSlot slot, bool skip_cost = false)
        {
            if (game_data.CanPlayCard(card, slot, skip_cost))
            {
                Player player = game_data.GetPlayer(card.player_id);

                //Play card
                player.RemoveCardFromAllGroups(card);

                //Add to board
                CardData icard = card.CardData;
                if (icard.IsBoardCard())
                {
                    player.cards_board.Add(card);
                    card.slot = slot;
                    card.exhausted = true;

                    if (icard.IsPlayer() && icard.suit != CardSuit.None)
                        player.suits_played_this_turn.Add(icard.suit);
                }
                else if (icard.IsEquipment())
                {
                    List<Card> bearer = game_data.GetSlotCards(slot);
                    EquipCard(bearer[0], card);
                    card.exhausted = true;
                }
                else if (icard.IsSecret())
                {
                    player.cards_secret.Add(card);
                }
                else if (icard.IsLiveBall())
                {
                    player.cards_temp.Add(card);
                    player.LiveBallCard = card;
                }
                else
                {
                    player.cards_discard.Add(card);
                    card.slot = slot;
                    if (icard.IsPlayEnhancer())
                        player.PlayEnhancer = card;
                }

                //History
                if (!is_ai_predict && !icard.IsSecret())
                    player.AddHistory(GameAction.PlayCard, card);

                //Update ongoing effects
                game_data.last_played = card.uid;
                UpdateOngoing();

                //Trigger abilities
                if (card.CardData.IsDynamicManaCost())
                {
                    GoToSelectorCost(card);
                }
                else
                {
                    TriggerSecrets(AbilityTrigger.OnPlayOther, card);
                    TriggerCardAbilityType(AbilityTrigger.OnPlay, card);
                    TriggerOtherCardsAbilityType(AbilityTrigger.OnPlayOther, card);
                }

                RefreshData();

                onCardPlayed?.Invoke(card, slot);
                resolve_queue.ResolveAll(0.3f);
            }
        }

        public virtual void MoveCard(Card card, CardPositionSlot slot, bool skip_cost = false)
        {
            if (game_data.CanMoveCard(card, slot, skip_cost))
            {
                card.slot = FindNextOpenPosition(slot);

                GameClient.Get().Move(card, card.slot);

                RefreshData();
            }
        }

        private CardPositionSlot FindNextOpenPosition(CardPositionSlot slot)
        {
            Game gdata = GameClient.Get().GetGameData();
            Player player = GameClient.Get().GetPlayer();

            List<Card> existingPlayers = gdata.GetSlotCards(slot, player.player_id);

            if (existingPlayers.Count < slot.max_cards)
            {
                return new CardPositionSlot(player.player_id, 1, slot.posGroupType);
            }

            return CardPositionSlot.None;
        }

        public virtual void CastAbility(Card card, AbilityData iability)
        {
            if (game_data.CanCastAbility(card, iability))
            {
                Player player = game_data.GetPlayer(card.player_id);
                if (!is_ai_predict && iability.target != AbilityTarget.SelectTarget)
                    player.AddHistory(GameAction.CastAbility, card, iability);
                card.RemoveStatus(StatusType.Stealth);
                TriggerCardAbility(iability, card);
                resolve_queue.ResolveAll();
            }
        }

        public virtual void AttackTarget(Card attacker, Card target, bool skip_cost = false)
        {
            if (game_data.CanAttackTarget(attacker, target, skip_cost))
            {
                Player player = game_data.GetPlayer(attacker.player_id);
                if (!is_ai_predict)
                    player.AddHistory(GameAction.Attack, attacker, target);

                game_data.last_target = target.uid;

                TriggerCardAbilityType(AbilityTrigger.OnBeforeAttack, attacker, target);
                TriggerCardAbilityType(AbilityTrigger.OnBeforeDefend, target, attacker);
                TriggerSecrets(AbilityTrigger.OnBeforeAttack, attacker);
                TriggerSecrets(AbilityTrigger.OnBeforeDefend, target);

                resolve_queue.AddAttack(attacker, target, ResolveAttack, skip_cost);
                resolve_queue.ResolveAll();
            }
        }

        protected virtual void ResolveAttack(Card attacker, Card target, bool skip_cost)
        {
            if (!game_data.IsOnBoard(attacker) || !game_data.IsOnBoard(target))
                return;

            onAttackStart?.Invoke(attacker, target);

            attacker.RemoveStatus(StatusType.Stealth);
            UpdateOngoing();

            resolve_queue.AddAttack(attacker, target, ResolveAttackHit, skip_cost);
            resolve_queue.ResolveAll(0.3f);
        }

        protected virtual void ResolveAttackHit(Card attacker, Card target, bool skip_cost)
        {
            int datt1 = attacker.GetAttack();
            int datt2 = target.GetAttack();

            DamageCard(attacker, target, datt1);

            if (!attacker.HasStatus(StatusType.Intimidate))
                DamageCard(target, attacker, datt2);

            if (!skip_cost)
                ExhaustBattle(attacker);

            UpdateOngoing();

            bool att_board = game_data.IsOnBoard(attacker);
            bool def_board = game_data.IsOnBoard(target);
            if (att_board)
                TriggerCardAbilityType(AbilityTrigger.OnAfterAttack, attacker, target);
            if (def_board)
                TriggerCardAbilityType(AbilityTrigger.OnAfterDefend, target, attacker);
            if (att_board)
                TriggerSecrets(AbilityTrigger.OnAfterAttack, attacker);
            if (def_board)
                TriggerSecrets(AbilityTrigger.OnAfterDefend, target);

            onAttackEnd?.Invoke(attacker, target);
            RefreshData();
            CheckForWinner();

            resolve_queue.ResolveAll(0.2f);
        }

        public virtual void AttackPlayer(Card attacker, Player target, bool skip_cost = false)
        {
            if (attacker == null || target == null)
                return;

            if (!game_data.CanAttackTarget(attacker, target, skip_cost))
                return;

            Player player = game_data.GetPlayer(attacker.player_id);
            if (!is_ai_predict)
                player.AddHistory(GameAction.AttackPlayer, attacker, target);

            TriggerSecrets(AbilityTrigger.OnBeforeAttack, attacker);
            TriggerCardAbilityType(AbilityTrigger.OnBeforeAttack, attacker, target);

            resolve_queue.AddAttack(attacker, target, ResolveAttackPlayer, skip_cost);
            resolve_queue.ResolveAll();
        }

        protected virtual void ResolveAttackPlayer(Card attacker, Player target, bool skip_cost)
        {
            if (!game_data.IsOnBoard(attacker))
                return;

            onAttackPlayerStart?.Invoke(attacker, target);

            attacker.RemoveStatus(StatusType.Stealth);
            UpdateOngoing();

            resolve_queue.AddAttack(attacker, target, ResolveAttackPlayerHit, skip_cost);
            resolve_queue.ResolveAll(0.3f);
        }

        protected virtual void ResolveAttackPlayerHit(Card attacker, Player target, bool skip_cost)
        {
            DamagePlayer(attacker, target, attacker.GetAttack());

            if (!skip_cost)
                ExhaustBattle(attacker);

            UpdateOngoing();

            if (game_data.IsOnBoard(attacker))
                TriggerCardAbilityType(AbilityTrigger.OnAfterAttack, attacker, target);

            TriggerSecrets(AbilityTrigger.OnAfterAttack, attacker);

            onAttackPlayerEnd?.Invoke(attacker, target);
            RefreshData();
            CheckForWinner();

            resolve_queue.ResolveAll(0.2f);
        }

        //Exhaust after battle
        public virtual void ExhaustBattle(Card attacker)
        {
            bool attacked_before = game_data.cards_attacked.Contains(attacker.uid);
            game_data.cards_attacked.Add(attacker.uid);
            bool attack_again = attacker.HasStatus(StatusType.Fury) && !attacked_before;
            attacker.exhausted = !attack_again;
        }

        //Redirect attack to a new target
        public virtual void RedirectAttack(Card attacker, Card new_target)
        {
            foreach (AttackQueueElement att in resolve_queue.GetAttackQueue())
            {
                if (att.attacker.uid == attacker.uid)
                {
                    att.target = new_target;
                    att.ptarget = null;
                    att.callback = ResolveAttack;
                    att.pcallback = null;
                }
            }
        }

        public virtual void RedirectAttack(Card attacker, Player new_target)
        {
            foreach (AttackQueueElement att in resolve_queue.GetAttackQueue())
            {
                if (att.attacker.uid == attacker.uid)
                {
                    att.ptarget = new_target;
                    att.target = null;
                    att.pcallback = ResolveAttackPlayer;
                    att.callback = null;
                }
            }
        }

        public virtual void ShuffleDeck(List<Card> cards)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                Card temp = cards[i];
                int randomIndex = random.Next(i, cards.Count);
                cards[i] = cards[randomIndex];
                cards[randomIndex] = temp;
            }
        }

        public virtual void DrawCard(Player player, int nb = 1)
        {
            for (int i = 0; i < nb; i++)
            {
                if (player.cards_deck.Count > 0 && player.cards_hand.Count < GameplayData.Get().cards_max)
                {
                    Card card = player.cards_deck[0];
                    player.cards_deck.RemoveAt(0);
                    player.cards_hand.Add(card);
                }
            }

            onCardDrawn?.Invoke(nb);
        }

        //Put a card from deck into discard
        public virtual void DrawDiscardCard(Player player, int nb = 1)
        {
            for (int i = 0; i < nb; i++)
            {
                if (player.cards_deck.Count > 0)
                {
                    Card card = player.cards_deck[0];
                    player.cards_deck.RemoveAt(0);
                    player.cards_discard.Add(card);
                }
            }
        }

        //Summon copy of an existing card
        public virtual Card SummonCopy(Player player, Card copy, CardPositionSlot slot)
        {
            CardData icard = copy.CardData;
            return SummonCard(player, icard, copy.VariantData, slot);
        }

        //Summon copy of an existing card into hand
        public virtual Card SummonCopyHand(Player player, Card copy)
        {
            CardData icard = copy.CardData;
            return SummonCardHand(player, icard, copy.VariantData);
        }

        //Create a new card and send it to the board
        public virtual Card SummonCard(Player player, CardData card, VariantData variant, CardPositionSlot slot)
        {
            if (!slot.IsValid())
                return null;

            if (game_data.GetSlotCards(slot).Count > 0)
                return null;

            Card acard = SummonCardHand(player, card, variant);
            PlayCard(acard, slot, true);

            onCardSummoned?.Invoke(acard, slot);

            return acard;
        }

        //Create a new card and send it to your hand
        public virtual Card SummonCardHand(Player player, CardData card, VariantData variant)
        {
            Card acard = Card.Create(card, variant, player);
            player.cards_hand.Add(acard);
            game_data.last_summoned = acard.uid;
            return acard;
        }

        //Transform card into another one
        public virtual Card TransformCard(Card card, CardData transform_to)
        {
            card.SetCard(transform_to, card.VariantData);

            onCardTransformed?.Invoke(card);

            return card;
        }

        public virtual void EquipCard(Card card, Card equipment)
        {
            if (card != null && equipment != null && card.player_id == equipment.player_id)
            {
                if (!card.CardData.IsEquipment() && equipment.CardData.IsEquipment())
                {
                    UnequipAll(card);

                    Player player = game_data.GetPlayer(card.player_id);
                    player.RemoveCardFromAllGroups(equipment);
                    player.cards_equip.Add(equipment);
                    card.equipped_uid = equipment.uid;
                    equipment.slot = card.slot;
                }
            }
        }

        public virtual void UnequipAll(Card card)
        {
            if (card != null && card.equipped_uid != null)
            {
                Player player = game_data.GetPlayer(card.player_id);
                Card equip = player.GetEquipCard(card.equipped_uid);
                if (equip != null)
                {
                    card.equipped_uid = null;
                    DiscardCard(equip);
                }
            }
        }

        //Change owner of a card
        public virtual void ChangeOwner(Card card, Player owner)
        {
            if (card.player_id != owner.player_id)
            {
                Player powner = game_data.GetPlayer(card.player_id);
                powner.RemoveCardFromAllGroups(card);
                powner.cards_all.Remove(card.uid);
                owner.cards_all[card.uid] = card;
                card.player_id = owner.player_id;
            }
        }

        //Damage a player
        public virtual void DamagePlayer(Card attacker, Player target, int value)
        {
            //onPlayerDamaged?.Invoke(target, value);
        }

        //Heal a card
        public virtual void HealCard(Card target, int value)
        {
            if (target == null)
                return;

            if (target.HasStatus(StatusType.Invincibility))
                return;

            target.damage -= value;
            target.damage = Mathf.Max(target.damage, 0);

            onCardHealed?.Invoke(target, value);
        }

        public virtual void HealPlayer(Player target, int value)
        {
            //onPlayerHealed?.Invoke(target, value);
        }

        //Generic damage that doesnt come from another card
        public virtual void DamageCard(Card target, int value)
        {
            if (target == null)
                return;

            if (target.HasStatus(StatusType.Invincibility))
                return;

            if (target.HasStatus(StatusType.SpellImmunity))
                return;

            target.damage += value;

            onCardDamaged?.Invoke(target, value);

            if (target.GetHP() <= 0)
                DiscardCard(target);
        }

        //Damage a card with attacker/caster
        public virtual void DamageCard(Card attacker, Card target, int value, bool spell_damage = false)
        {
            if (attacker == null || target == null)
                return;

            if (target.HasStatus(StatusType.Invincibility))
                return;

            if (target.HasStatus(StatusType.SpellImmunity) && attacker.CardData.type != CardType.OffensivePlayer)
                return;

            //Shell
            bool doublelife = target.HasStatus(StatusType.Shell);
            if (doublelife && value > 0)
            {
                target.RemoveStatus(StatusType.Shell);
                return;
            }

            //Armor
            if (!spell_damage && target.HasStatus(StatusType.Armor))
                value = Mathf.Max(value - target.GetStatusValue(StatusType.Armor), 0);

            //Damage
            int damage_max = Mathf.Min(value, target.GetHP());
            int extra = value - target.GetHP();
            target.damage += value;

            //Remove sleep on damage
            target.RemoveStatus(StatusType.Sleep);

            //Callback
            onCardDamaged?.Invoke(target, value);

            //Deathtouch
            if (value > 0 && attacker.HasStatus(StatusType.Deathtouch) && target.CardData.type == CardType.OffensivePlayer)
                KillCard(attacker, target);

            //Kill card if no hp
            if (target.GetHP() <= 0)
                KillCard(attacker, target);
        }

        //A card that kills another card
        public virtual void KillCard(Card attacker, Card target)
        {
            if (attacker == null || target == null)
                return;

            if (!game_data.IsOnBoard(target) && !game_data.IsEquipped(target))
                return;

            if (target.HasStatus(StatusType.Invincibility))
                return;

            DiscardCard(target);

            TriggerCardAbilityType(AbilityTrigger.OnKill, attacker, target);
        }

        //Send card into discard
        public virtual void DiscardCard(Card card)
        {
            if (card == null)
                return;

            if (game_data.IsInDiscard(card))
                return;

            CardData icard = card.CardData;
            Player player = game_data.GetPlayer(card.player_id);
            bool was_on_board = game_data.IsOnBoard(card) || game_data.IsEquipped(card);

            UnequipAll(card);

            player.RemoveCardFromAllGroups(card);
            player.cards_discard.Add(card);
            game_data.last_destroyed = card.uid;

            Card bearer = player.GetBearerCard(card);
            if (bearer != null)
                bearer.equipped_uid = null;

            if (was_on_board)
            {
                TriggerCardAbilityType(AbilityTrigger.OnDeath, card);
                TriggerOtherCardsAbilityType(AbilityTrigger.OnDeathOther, card);
                TriggerSecrets(AbilityTrigger.OnDeathOther, card);
                UpdateOngoingCards();
            }

            cards_to_clear.Add(card);
            onCardDiscarded?.Invoke(card);

            TriggerPlayerCardsAbilityType(game_data.current_offensive_player, AbilityTrigger.OnDiscard);
            Player defPlayer = game_data.GetCurrentDefensivePlayer();
            if (defPlayer != null)
                TriggerPlayerCardsAbilityType(defPlayer, AbilityTrigger.OnDiscard);
        }

        public int RollRandomValue(int dice)
        {
            return RollRandomValue(1, dice + 1);
        }

        public virtual int RollRandomValue(int min, int max)
        {
            game_data.rolled_value = random.Next(min, max);
            onRollValue?.Invoke(game_data.rolled_value);
            resolve_queue.SetDelay(1f);
            return game_data.rolled_value;
        }
    }
}
