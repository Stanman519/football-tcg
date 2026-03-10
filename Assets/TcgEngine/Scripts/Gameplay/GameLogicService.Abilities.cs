using Assets.TcgEngine.Scripts.Effects;
using System.Collections.Generic;
using System.Linq;
using TcgEngine;
using UnityEngine;

namespace Assets.TcgEngine.Scripts.Gameplay
{
    public partial class GameLogicService
    {
        //--- Abilities --

        public virtual void TriggerCardAbilityType(AbilityTrigger type, Card caster, Card triggerer = null)
        {
            foreach (AbilityData iability in caster.GetAbilities())
            {
                if (iability && iability.trigger == type)
                {
                    TriggerCardAbility(iability, caster, triggerer);
                }
            }

            Card equipped = game_data.GetEquipCard(caster.equipped_uid);
            if (equipped != null)
                TriggerCardAbilityType(type, equipped, triggerer);
        }

        public virtual void TriggerCardAbilityType(AbilityTrigger type, Card caster, Player triggerer)
        {
            foreach (AbilityData iability in caster.GetAbilities())
            {
                if (iability && iability.trigger == type)
                {
                    TriggerCardAbility(iability, caster, triggerer);
                }
            }

            Card equipped = game_data.GetEquipCard(caster.equipped_uid);
            if (equipped != null)
                TriggerCardAbilityType(type, equipped, triggerer);
        }

        public virtual void TriggerOtherCardsAbilityType(AbilityTrigger type, Card triggerer)
        {
            foreach (Player oplayer in game_data.players)
            {
                if (oplayer.hero != null)
                    TriggerCardAbilityType(type, oplayer.hero, triggerer);

                foreach (Card card in oplayer.cards_board)
                    TriggerCardAbilityType(type, card, triggerer);
            }
        }

        public virtual void TriggerPlayerCardsAbilityType(Player player, AbilityTrigger type)
        {
            if (player.hero != null)
                TriggerCardAbilityType(type, player.hero, player.hero);

            foreach (Card card in player.cards_board)
                TriggerCardAbilityType(type, card, card);
        }

        // TriggerBoardSlotAbilities → GameLogicService.Yardage.cs

        public virtual void TriggerCardAbility(AbilityData iability, Card caster)
        {
            TriggerCardAbility(iability, caster, caster);
        }

        public virtual void TriggerCardAbility(AbilityData iability, Card caster, Card triggerer)
        {
            Card trigger_card = triggerer != null ? triggerer : caster;
            if (!caster.HasStatus(StatusType.Silenced) && iability.AreTriggerConditionsMet(game_data, caster, trigger_card))
            {
                resolve_queue.AddAbility(iability, caster, trigger_card, ResolveCardAbility);
            }
        }

        public virtual void TriggerCardAbility(AbilityData iability, Card caster, Player triggerer)
        {
            if (!caster.HasStatus(StatusType.Silenced) && iability.AreTriggerConditionsMet(game_data, caster, triggerer))
            {
                resolve_queue.AddAbility(iability, caster, caster, ResolveCardAbility);
            }
        }

        public virtual void TriggerAbilityDelayed(AbilityData iability, Card caster)
        {
            resolve_queue.AddAbility(iability, caster, caster, TriggerCardAbility);
        }

        public virtual void TriggerAbilityDelayed(AbilityData iability, Card caster, Card triggerer)
        {
            Card trigger_card = triggerer != null ? triggerer : caster;
            resolve_queue.AddAbility(iability, caster, trigger_card, TriggerCardAbility);
        }

        //Resolve a card ability, may stop to ask for target
        protected virtual void ResolveCardAbility(AbilityData iability, Card caster, Card triggerer)
        {
            if (!caster.CanDoAbilities())
                return;

            onAbilityStart?.Invoke(iability, caster);
            game_data.ability_triggerer = triggerer.uid;
            game_data.ability_played.Add(iability.id);

            bool is_selector = ResolveCardAbilitySelector(iability, caster);
            if (is_selector)
                return;

            ResolveCardAbilityPlayTarget(iability, caster);
            ResolveCardAbilityPlayers(iability, caster);
            ResolveCardAbilityCards(iability, caster);
            ResolveCardAbilitySlots(iability, caster);
            ResolveCardAbilityCardData(iability, caster);
            ResolveCardAbilityNoTarget(iability, caster);
            AfterAbilityResolved(iability, caster);
        }

        protected virtual bool ResolveCardAbilitySelector(AbilityData iability, Card caster)
        {
            if (iability.target == AbilityTarget.SelectTarget)
            {
                GoToSelectTarget(iability, caster);
                return true;
            }
            else if (iability.target == AbilityTarget.CardSelector)
            {
                GoToSelectorCard(iability, caster);
                return true;
            }
            else if (iability.target == AbilityTarget.CardSelectorHand)
            {
                GoToSelectorCard(iability, caster);
                return true;
            }
            else if (iability.target == AbilityTarget.CardSelectorDiscover)
            {
                PopulateDiscoverCards(iability, caster);
                Player discoverPlayer = game_data.GetPlayer(caster.player_id);
                if (discoverPlayer != null && discoverPlayer.cards_temp.Count > 0)
                {
                    GoToSelectorCard(iability, caster);
                    return true;
                }
                return false;
            }
            else if (iability.target == AbilityTarget.ChoiceSelector)
            {
                GoToSelectorChoice(iability, caster);
                return true;
            }
            return false;
        }

        protected virtual void ResolveCardAbilityPlayTarget(AbilityData iability, Card caster)
        {
            if (iability.target == AbilityTarget.PlayTarget)
            {
                CardPositionSlot slot = caster.slot;
                List<Card> slot_cards = game_data.GetSlotCards(slot);
                if (slot_cards.Count > 0)
                {
                    if (iability.CanTarget(game_data, caster, slot_cards[0]))
                    {
                        game_data.last_target = slot_cards[0].uid;
                        ResolveEffectTarget(iability, caster, slot_cards[0]);
                    }
                }
                else
                {
                    if (iability.CanTarget(game_data, caster, slot))
                        ResolveEffectTarget(iability, caster, slot);
                }
            }
        }

        protected virtual void ResolveCardAbilityPlayers(AbilityData iability, Card caster)
        {
            List<Player> targets = iability.GetPlayerTargets(game_data, caster, player_array);

            foreach (Player target in targets)
            {
                ResolveEffectTarget(iability, caster, target);
            }
        }

        protected virtual void ResolveCardAbilityCards(AbilityData iability, Card caster)
        {
            List<Card> targets = iability.GetCardTargets(game_data, caster, card_array);

            foreach (Card target in targets)
            {
                ResolveEffectTarget(iability, caster, target);
            }
        }

        protected virtual void ResolveCardAbilitySlots(AbilityData iability, Card caster)
        {
            List<CardPositionSlot> targets = iability.GetSlotTargets(game_data, caster, slot_array);

            foreach (CardPositionSlot target in targets)
            {
                ResolveEffectTarget(iability, caster, target);
            }
        }

        protected virtual void ResolveCardAbilityCardData(AbilityData iability, Card caster)
        {
            List<CardData> targets = iability.GetCardDataTargets(game_data, caster, card_data_array);

            foreach (CardData target in targets)
            {
                ResolveEffectTarget(iability, caster, target);
            }
        }

        protected virtual void ResolveCardAbilityNoTarget(AbilityData iability, Card caster)
        {
            if (iability.target == AbilityTarget.None)
                iability.DoEffects(this, caster);
        }

        protected virtual void ResolveEffectTarget(AbilityData iability, Card caster, Player target)
        {
            iability.DoEffects(this, caster, target);

            onAbilityTargetPlayer?.Invoke(iability, caster, target);
        }

        protected virtual void ResolveEffectTarget(AbilityData iability, Card caster, Card target)
        {
            iability.DoEffects(this, caster, target);

            onAbilityTargetCard?.Invoke(iability, caster, target);
        }

        protected virtual void ResolveEffectTarget(AbilityData iability, Card caster, CardPositionSlot target)
        {
            iability.DoEffects(this, caster, target);

            onAbilityTargetSlot?.Invoke(iability, caster, target);
        }

        protected virtual void ResolveEffectTarget(AbilityData iability, Card caster, CardData target)
        {
            iability.DoEffects(this, caster, target);
        }

        protected virtual void AfterAbilityResolved(AbilityData iability, Card caster)
        {
            Player player = game_data.GetPlayer(caster.player_id);

            //Pay cost
            if (iability.trigger == AbilityTrigger.Activate || iability.trigger == AbilityTrigger.None)
            {
                caster.exhausted = caster.exhausted || iability.exhaust;
            }

            //Recalculate and clear
            UpdateOngoing();
            CheckForWinner();

            //Chain ability
            if (iability.target != AbilityTarget.ChoiceSelector && game_data.state != GameState.GameEnded)
            {
                foreach (AbilityData chain_ability in iability.chain_abilities ?? new AbilityData[0])
                {
                    if (chain_ability != null)
                    {
                        TriggerCardAbility(chain_ability, caster);
                    }
                }
            }

            onAbilityEnd?.Invoke(iability, caster);
            resolve_queue.ResolveAll(0.5f);
            RefreshData();
        }

        public virtual bool TriggerSecrets(AbilityTrigger secret_trigger, Card trigger_card)
        {
            if (trigger_card != null && trigger_card.HasStatus(StatusType.SpellImmunity))
                return false;

            for (int p = 0; p < game_data.players.Length; p++)
            {
                var loopPlayer = game_data.players[p];
                if (loopPlayer != game_data.current_offensive_player)
                {
                    Player other_player = game_data.players[p];
                    for (int i = other_player.cards_secret.Count - 1; i >= 0; i--)
                    {
                        Card card = other_player.cards_secret[i];
                        CardData icard = card.CardData;
                        if (icard.type == CardType.Secret && !card.exhausted)
                        {
                            Card trigger = trigger_card != null ? trigger_card : card;
                            if (card.AreAbilityConditionsMet(secret_trigger, game_data, card, trigger))
                            {
                                resolve_queue.AddSecret(secret_trigger, card, trigger, ResolveSecret);
                                resolve_queue.SetDelay(0.5f);
                                card.exhausted = true;

                                if (onSecretTrigger != null)
                                    onSecretTrigger.Invoke(card, trigger);

                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }

        protected virtual void ResolveSecret(AbilityTrigger secret_trigger, Card secret_card, Card trigger)
        {
            CardData icard = secret_card.CardData;
            Player player = game_data.GetPlayer(secret_card.player_id);
            if (icard.type == CardType.Secret)
            {
                Player tplayer = game_data.GetPlayer(trigger.player_id);
                if (!is_ai_predict)
                    tplayer.AddHistory(GameAction.SecretTriggered, secret_card, trigger);

                TriggerCardAbilityType(secret_trigger, secret_card, trigger);
                DiscardCard(secret_card);

                if (onSecretResolve != null)
                    onSecretResolve.Invoke(secret_card, trigger);
            }
        }

        //---- Resolve Selector -----

        public virtual void SelectCard(Card target)
        {
            if (game_data.selector == SelectorType.None)
                return;

            Card caster = game_data.GetCard(game_data.selector_caster_uid);
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);

            if (caster == null || target == null || ability == null)
                return;

            if (game_data.selector == SelectorType.SelectTarget)
            {
                if (!ability.CanTarget(game_data, caster, target))
                    return;

                Player player = game_data.GetPlayer(caster.player_id);
                if (!is_ai_predict)
                    player.AddHistory(GameAction.CastAbility, caster, ability, target);

                game_data.selector = SelectorType.None;
                game_data.last_target = target.uid;
                ResolveEffectTarget(ability, caster, target);
                AfterAbilityResolved(ability, caster);
                resolve_queue.ResolveAll();
            }

            if (game_data.selector == SelectorType.SelectorCard)
            {
                if (!ability.IsCardSelectionValid(game_data, caster, target, card_array))
                    return;

                game_data.selector = SelectorType.None;
                game_data.last_target = target.uid;
                ResolveEffectTarget(ability, caster, target);
                AfterAbilityResolved(ability, caster);
                resolve_queue.ResolveAll();
            }
        }

        public virtual void SelectPlayer(Player target)
        {
            if (game_data.selector == SelectorType.None)
                return;

            Card caster = game_data.GetCard(game_data.selector_caster_uid);
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);

            if (caster == null || target == null || ability == null)
                return;

            if (game_data.selector == SelectorType.SelectTarget)
            {
                if (!ability.CanTarget(game_data, caster, target))
                    return;

                Player player = game_data.GetPlayer(caster.player_id);
                if (!is_ai_predict)
                    player.AddHistory(GameAction.CastAbility, caster, ability, target);

                game_data.selector = SelectorType.None;
                ResolveEffectTarget(ability, caster, target);
                AfterAbilityResolved(ability, caster);
                resolve_queue.ResolveAll();
            }
        }

        public virtual void SelectSlot(CardPositionSlot target)
        {
            if (game_data.selector == SelectorType.None)
                return;

            Card caster = game_data.GetCard(game_data.selector_caster_uid);
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);

            if (caster == null || ability == null || !target.IsValid())
                return;

            if (game_data.selector == SelectorType.SelectTarget)
            {
                if (!ability.CanTarget(game_data, caster, target))
                    return;

                Player player = game_data.GetPlayer(caster.player_id);
                if (!is_ai_predict)
                    player.AddHistory(GameAction.CastAbility, caster, ability, target);

                game_data.selector = SelectorType.None;
                ResolveEffectTarget(ability, caster, target);
                AfterAbilityResolved(ability, caster);
                resolve_queue.ResolveAll();
            }
        }

        public virtual void SelectChoice(int choice)
        {
            if (game_data.selector == SelectorType.None)
                return;

            Card caster = game_data.GetCard(game_data.selector_caster_uid);
            AbilityData ability = AbilityData.Get(game_data.selector_ability_id);

            if (caster == null || ability == null || choice < 0)
                return;

            if (game_data.selector == SelectorType.SelectorChoice && ability.target == AbilityTarget.ChoiceSelector)
            {
                if (choice >= 0 && choice < ability.chain_abilities.Length)
                {
                    AbilityData achoice = ability.chain_abilities[choice];
                    if (achoice != null && game_data.CanSelectAbility(caster, achoice))
                    {
                        game_data.selector = SelectorType.None;
                        AfterAbilityResolved(ability, caster);
                        ResolveCardAbility(achoice, caster, caster);
                        resolve_queue.ResolveAll();
                    }
                }
            }
        }

        public virtual void SelectCost(int select_cost)
        {
            if (game_data.selector == SelectorType.None)
                return;

            Player player = game_data.GetPlayer(game_data.selector_player_id);
            Card caster = game_data.GetCard(game_data.selector_caster_uid);

            if (player == null || caster == null || select_cost < 0)
                return;

            if (game_data.selector == SelectorType.SelectorCost)
            {
                if (select_cost >= 0 && select_cost < 10)
                {
                    game_data.selector = SelectorType.None;
                    game_data.selected_value = select_cost;
                    RefreshData();

                    TriggerSecrets(AbilityTrigger.OnPlayOther, caster);
                    TriggerCardAbilityType(AbilityTrigger.OnPlay, caster);
                    TriggerOtherCardsAbilityType(AbilityTrigger.OnPlayOther, caster);
                    resolve_queue.ResolveAll();
                }
            }
        }

        public virtual void CancelSelection()
        {
            if (game_data.selector != SelectorType.None)
            {
                if (game_data.selector == SelectorType.SelectorCost)
                    CancelPlayCard();

                game_data.selector = SelectorType.None;
                RefreshData();
            }
        }

        public void CancelPlayCard()
        {
            Card card = game_data.GetCard(game_data.selector_caster_uid);
            if (card != null)
            {
                Player player = game_data.GetPlayer(card.player_id);

                player.RemoveCardFromAllGroups(card);
                player.AddCard(player.cards_hand, card);
                card.Clear();
            }
        }

        public virtual void Mulligan(Player player, string[] cards)
        {
            if (game_data.phase == GamePhase.Mulligan && !player.ready)
            {
                int count = 0;
                List<Card> remove_list = new List<Card>();
                foreach (Card card in player.cards_hand)
                {
                    if (cards.Contains(card.uid))
                    {
                        remove_list.Add(card);
                        count++;
                    }
                }

                foreach (Card card in remove_list)
                {
                    player.RemoveCardFromAllGroups(card);
                    player.cards_discard.Add(card);
                }

                player.ready = true;
                DrawCard(player, count);
                RefreshData();

                if (game_data.AreAllPlayersReady())
                {
                    StartTurn();
                }
            }
        }

        //-----Trigger Selector-----

        protected virtual void GoToSelectTarget(AbilityData iability, Card caster)
        {
            game_data.selector = SelectorType.SelectTarget;
            game_data.selector_player_id = caster.player_id;
            game_data.selector_ability_id = iability.id;
            game_data.selector_caster_uid = caster.uid;
            RefreshData();
        }

        protected virtual void GoToSelectorCard(AbilityData iability, Card caster)
        {
            game_data.selector = SelectorType.SelectorCard;
            game_data.selector_player_id = caster.player_id;
            game_data.selector_ability_id = iability.id;
            game_data.selector_caster_uid = caster.uid;
            RefreshData();
        }

        /// <summary>
        /// Pulls random cards matching the EffectDiscover filter from the player's deck
        /// into cards_temp so the CardSelector UI can show them.
        /// </summary>
        protected virtual void PopulateDiscoverCards(AbilityData iability, Card caster)
        {
            Player player = game_data.GetPlayer(caster.player_id);
            if (player == null) return;

            CardType filterType = CardType.None;
            int drawCount = 3;
            foreach (var eff in iability.effects)
            {
                if (eff is EffectDiscover discover)
                {
                    filterType = discover.filterType;
                    drawCount = discover.drawCount;
                    break;
                }
            }

            player.cards_temp.Clear();

            List<Card> matches = new List<Card>();
            foreach (Card c in player.cards_deck)
            {
                if (filterType == CardType.None || c.CardData.type == filterType)
                    matches.Add(c);
            }

            for (int i = matches.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (matches[i], matches[j]) = (matches[j], matches[i]);
            }

            int count = Mathf.Min(drawCount, matches.Count);
            for (int i = 0; i < count; i++)
            {
                Card card = matches[i];
                player.cards_deck.Remove(card);
                player.cards_temp.Add(card);
            }

            Debug.Log($"[Discover] Pulled {count} cards (filter={filterType}) to temp for player {caster.player_id}");
        }

        protected virtual void GoToSelectorChoice(AbilityData iability, Card caster)
        {
            game_data.selector = SelectorType.SelectorChoice;
            game_data.selector_player_id = caster.player_id;
            game_data.selector_ability_id = iability.id;
            game_data.selector_caster_uid = caster.uid;
            RefreshData();
        }

        protected virtual void GoToSelectorCost(Card caster)
        {
            game_data.selector = SelectorType.SelectorCost;
            game_data.selector_player_id = caster.player_id;
            game_data.selector_ability_id = "";
            game_data.selector_caster_uid = caster.uid;
            game_data.selected_value = 0;
            RefreshData();
        }

        protected virtual void GoToMulligan()
        {
            game_data.phase = GamePhase.Mulligan;
            game_data.turn_timer = GameplayData.Get().turn_duration;
            foreach (Player player in game_data.players)
                player.ready = false;
            RefreshData();
        }
    }
}
