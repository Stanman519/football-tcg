using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Client;
using TcgEngine.UI;
using System.Linq;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TcgEngine.Client
{
    /// <summary>
    /// Visual representation of a Slot.cs
    /// Will highlight when can be interacted with
    /// </summary>

    public class BoardSlot : BSlot, IDropHandler
    {
        public BoardSlotType type;
        public int x;
        public int y;
        private CardPositionSlot assignedSlot;
        private Image slotImage;
        private bool isOccupied = false;
        private SpriteRenderer slotIndicator; // UI highlight for replaceable player
        private GameObject replacementPlayer; // X or O placeholder for non-star players



        private static List<BoardSlot> slot_list = new List<BoardSlot>();

        protected override void Awake()
        {
            base.Awake();
            slot_list.Add(this);
            slotIndicator = GetComponent<SpriteRenderer>();

        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            slot_list.Remove(this);
        }
        public void Initialize(PlayerPositionGrp positionGroup)
        {
            player_position_type = positionGroup;
            replacementPlayer = Instantiate(Resources.Load<GameObject>("PlaceholderXOrO"), transform);
            replacementPlayer.SetActive(true);
        }
        private void Start()
        {
            assignedSlot = GetSlot();
            SetupReplacementPlayer();
        }

        protected override void Update()
        {
            base.Update();

            if (!GameClient.Get().IsReady())
                return;
            BoardCard selectedCard = PlayerControls.Get().GetSelected();
            HandCard draggedCard = HandCard.GetDrag();
            Game gdata = GameClient.Get().GetGameData();
            Player player = GameClient.Get().GetPlayer();
            CardPositionSlot slot = GetSlot();
            List<Card> slot_cards = gdata.GetSlotCards(slot);



            // Highlight slot if a valid card is being dragged
            target_alpha = IsValidDragTarget() ? 1f : 0f;

            // Toggle replacement player visibility
            if (replacementPlayer != null)
                replacementPlayer.SetActive(slot_cards.Count == 0);

            /*            BoardCard bcard_selected = PlayerControls.Get().GetSelected();
                        HandCard drag_card = HandCard.GetDrag();

                        Game gdata = GameClient.Get().GetGameData();
                        Player player = GameClient.Get().GetPlayer();
                        CardPositionSlot slot = GetSlot();
                        Card dcard = drag_card?.GetCard();
                        List<Card> slot_cards = gdata.GetSlotCards(GetSlot());
                        bool your_turn = GameClient.Get().IsYourTurn();
                        //collide.enabled = slot_card == null; //Disable collider when a card is here

                        //Find target opacity value
                        target_alpha = 0f;
                        if (your_turn && dcard != null && dcard.CardData.IsBoardCard() && gdata.CanPlayCard(dcard, slot))
                        {
                            target_alpha = 1f; //hightlight when dragging a character or artifact
                        }

                        if (your_turn && dcard != null && dcard.CardData.IsRequireTarget() && gdata.CanPlayCard(dcard, slot))
                        {
                            target_alpha = 1f; //Highlight when dragin a spell with target
                        }

                        if (gdata.selector == SelectorType.SelectTarget && player.player_id == gdata.selector_player_id)
                        {
                            Card caster = gdata.GetCard(gdata.selector_caster_uid);
                            AbilityData ability = AbilityData.Get(gdata.selector_ability_id);
                            if(ability != null && slot_cards.Count < player.head_coach.positional_Scheme[player_position_type].pos_max && ability.CanTarget(gdata, caster, slot))
                                target_alpha = 1f; //Highlight when selecting a target and slot are valid
                            if (ability != null && slot_cards.Count > 0 && ability.CanTarget(gdata, caster, slot_cards[0]))
                                target_alpha = 1f; //Highlight when selecting a target and cards are valid
                        }

                        Card select_card = bcard_selected?.GetCard();
                        bool can_do_move = your_turn && select_card != null && slot_cards.Count < player.head_coach.positional_Scheme[player_position_type].pos_max && gdata.CanMoveCard(select_card, slot);
                        // bool can_do_attack = your_turn && select_card != null && slot_card != null && gdata.CanAttackTarget(select_card, slot_card); TODO: cant do attacks yet

                        if (can_do_move)//  (can_do_attack || can_do_move)
                        {
                            target_alpha = 1f;
                        }*/
        }


        private void SetupReplacementPlayer()
        {
            // Placeholder players (X or O) for non-star players
            replacementPlayer = new GameObject("Replacement Player");
            SpriteRenderer sprite = replacementPlayer.AddComponent<SpriteRenderer>();
            sprite.sprite = Resources.Load<Sprite>("ReplacementIcon"); // Ensure you have this asset
            sprite.color = new Color(1, 1, 1, 0.5f); // Slight transparency
            replacementPlayer.transform.SetParent(transform);
            replacementPlayer.transform.localPosition = Vector3.zero;
        }
        public bool IsValidDragTarget()
        {
            Card dragCard = HandCard.GetDrag()?.GetCard();
            return dragCard != null && dragCard.playerPosition == player_position_type;
        }
        // Determines if a dragged card can be placed in this area
/*        private bool CanPlaceCardHere(Card card)
        {
            Game gdata = GameClient.Get().GetGameData();
            Player player = GameClient.Get().GetPlayer();
            int maxPlayers = player.head_coach.positional_Scheme[player_position_type].pos_max;
            int currentPlayers = gdata.GetSlotCards(player_position_type, player.player_id).Count;

            return currentPlayers < maxPlayers;
        }*/

        public void OnDrop(PointerEventData eventData)
        {
            Card dragCard = HandCard.GetDrag()?.GetCard();

            if (dragCard != null && dragCard.playerPosition == player_position_type)
            {
                GameClient.Get().PlayCard(dragCard, assignedSlot);
            }
        }
        public override CardPositionSlot GetEmptySlot(Vector3 wpos)
        {
            Game gdata = GameClient.Get().GetGameData();
            Player player = GameClient.Get().GetPlayer();

            // Determine which position group was clicked
            PlayerPositionGrp selectedGroup = DeterminePositionGroupFromClick(wpos);

            if (selectedGroup == PlayerPositionGrp.NONE)
                return CardPositionSlot.None;

            // Get all slots for this group
            List<CardPositionSlot> groupSlots = CardPositionSlot.GetAll(player.player_id)
                .Where(slot => slot.posGroupType == selectedGroup)
                .ToList();

            // Check which slots are already occupied
            foreach (CardPositionSlot slot in groupSlots)
            {
                if (gdata.GetSlotCards(slot, player.player_id).Count == 0)
                {
                    return slot; // Found an empty slot
                }
            }

            return CardPositionSlot.None; // No space left
        }

        //Find the actual slot coordinates of this board slot we replaced this - below.
        /*        public override CardPositionSlot GetSlot()
                {
                    int p = 0;
                    int max_players = 0;
                    Game gdata = GameClient.Get().GetGameData();

                    if (type == BoardSlotType.FlipX)
                    {
                        int pid = GameClient.Get().GetPlayerID();
                        max_players = gdata.players.First(play => play.player_id == pid).head_coach.positional_Scheme[player_position_type].pos_max;
                        int px = x;
                        if ((pid % 2) == 1)
                            px = CardPositionSlot.x_max - x + CardPositionSlot.x_min; //Flip X coordinate if not the first player
                        return new CardPositionSlot(px, y, p, max_players, player_position_type);
                    }

                    if (type == BoardSlotType.FlipY)
                    {
                        int pid = GameClient.Get().GetPlayerID();
                        int py = y;
                        max_players = gdata.players.First(play => play.player_id == pid).head_coach.positional_Scheme[player_position_type].pos_max;
                        if ((pid % 2) == 1)
                            py = CardPositionSlot.y_max - y + CardPositionSlot.y_min; //Flip Y coordinate if not the first player
                        return new CardPositionSlot(x, py, p, max_players, player_position_type);
                    }

                    if (type == BoardSlotType.PlayerSelf)
                        p = GameClient.Get().GetPlayerID();
                    if(type == BoardSlotType.PlayerOpponent)
                        p = GameClient.Get().GetOpponentPlayerID();

                    max_players =  gdata.players.First(play => play.player_id == p).head_coach.positional_Scheme[player_position_type].pos_max;
                    return new CardPositionSlot(x, y, p, max_players, player_position_type);
                }*/
        public override CardPositionSlot GetSlot()
        {
            Game gdata = GameClient.Get().GetGameData();
            int p = GameClient.Get().GetPlayerID();

            // Handle formation slots dynamically
            if (type == BoardSlotType.FlipX)
            {
                int pid = GameClient.Get().GetPlayerID();
                int px = x;
                if ((pid % 2) == 1)
                    px = CardPositionSlot.x_max - x + CardPositionSlot.x_min; // Flip X for second player

                return new CardPositionSlot(px, y, p, gdata.GetPlayer(pid).head_coach.positional_Scheme[player_position_type].pos_max, player_position_type);
            }

            if (type == BoardSlotType.FlipY)
            {
                int pid = GameClient.Get().GetPlayerID();
                int py = y;
                if ((pid % 2) == 1)
                    py = CardPositionSlot.y_max - y + CardPositionSlot.y_min; // Flip Y for second player

                return new CardPositionSlot(x, py, p, gdata.GetPlayer(pid).head_coach.positional_Scheme[player_position_type].pos_max, player_position_type);
            }

            if (type == BoardSlotType.PlayerSelf)
                p = GameClient.Get().GetPlayerID();
            if (type == BoardSlotType.PlayerOpponent)
                p = GameClient.Get().GetOpponentPlayerID();

            return new CardPositionSlot(x, y, p, gdata.GetPlayer(p).head_coach.positional_Scheme[player_position_type].pos_max, player_position_type);
        }
        public void HighlightSlot()
        {
            if (slotIndicator != null)
                slotIndicator.enabled = true;
        }

        public void UnhighlightSlot()
        {
            if (slotIndicator != null)
                slotIndicator.enabled = false;
        }

        //When clicking on the slot
        public void OnMouseDown()
        {
            if (GameUI.IsOverUI())
                return;

            Game gdata = GameClient.Get().GetGameData();
            int player_id = GameClient.Get().GetPlayerID();

            if (gdata.selector == SelectorType.SelectTarget && player_id == gdata.selector_player_id)
            {
                CardPositionSlot slot = GetSlot();
                List<Card> slot_cards = gdata.GetSlotCards(slot);
                if (slot_cards.Count == 0)
                {
                    GameClient.Get().SelectSlot(slot);
                }
            }
        }


        //Temp???
        private PlayerPositionGrp DeterminePositionGroupFromClick(Vector3 wpos)
        {
            // Define the world-space coordinates of the different positional areas
            if (wpos.x > -5 && wpos.x < 5 && wpos.z > 30) return PlayerPositionGrp.QB;  // QB Area (Near Center)
            if (wpos.x > -10 && wpos.x < 10 && wpos.z > 20 && wpos.z <= 30) return PlayerPositionGrp.RB_TE; // RB/TE Area
            if (wpos.x > -15 && wpos.x < 15 && wpos.z > 10 && wpos.z <= 20) return PlayerPositionGrp.WR; // WRs Lineup Wide
            if (wpos.x > -20 && wpos.x < 20 && wpos.z > 0 && wpos.z <= 10) return PlayerPositionGrp.OL; // Offensive Line

            // Defense
            if (wpos.x > -20 && wpos.x < 20 && wpos.z > -10 && wpos.z <= 0) return PlayerPositionGrp.DL; // Defensive Line
            if (wpos.x > -15 && wpos.x < 15 && wpos.z > -20 && wpos.z <= -10) return PlayerPositionGrp.LB; // Linebackers
            if (wpos.x > -10 && wpos.x < 10 && wpos.z > -30 && wpos.z <= -20) return PlayerPositionGrp.DB; // Defensive Backs

            // Special Teams
            if (wpos.x > -5 && wpos.x < 5 && wpos.z < -30) return PlayerPositionGrp.K;  // Kicker
            if (wpos.x > -5 && wpos.x < 5 && wpos.z > 40) return PlayerPositionGrp.P;  // Punter

            return PlayerPositionGrp.NONE; // No valid position found
        }

    }
}