using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TcgEngine.Client;
using TcgEngine.UI;
using System.Linq;

namespace TcgEngine.Client
{
    /// <summary>
    /// Visual representation of a Slot.cs that position cards automatically
    /// </summary>

    public class BoardSlotGroup : BSlot
    {
        public BoardSlotType type;
        public int min_x = 1;
        public int max_x = 5;
        public int y = 1;

        public float spacing = 2.5f;
        public float reduce_delay = 1f;

        private int nb_occupied = 0;

        private List<GroupSlot> group_slots = new List<GroupSlot>();

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }

        private void Start()
        {
            if (min_x < CardPositionSlot.x_min || max_x > CardPositionSlot.x_max || y < CardPositionSlot.y_min || y > CardPositionSlot.y_max)
                Debug.LogError("Board Slot X and Y value must be within the min and max set for those values, check Slot.cs script to change those min/max.");

            GameClient.Get().onConnectGame += OnConnect;

            nb_occupied = 0;
            collide.enabled = false;
        }

        private void OnConnect()
        {
            foreach (CardPositionSlot slot in CardPositionSlot.GetAll())
            {
                if (IsInGroup(slot))
                {
                    GroupSlot pos = new GroupSlot();
                    pos.slot = slot;
                    pos.pos = transform.position;
                    group_slots.Add(pos);
                }
            }
        }

        protected override void Update()
        {
            base.Update();

            if (!GameClient.Get().IsReady())
                return;

            Game gdata = GameClient.Get().GetGameData();
            HandCard drag_card = HandCard.GetDrag();
            bool your_turn = GameClient.Get().IsYourTurn();
            Card dcard = drag_card?.GetCard();

            //Find target opacity value
            target_alpha = 0f;
            if (your_turn && dcard != null && dcard.CardData.IsBoardCard())
            {
                foreach (GroupSlot slot in group_slots)
                {
                    if(gdata.CanPlayCard(dcard, slot.slot))
                        target_alpha = 1f; //hightlight when dragging a character or artifact
                }
            }

            UpdateOccupied();
            UpdatePositions();
        }

        public void UpdateOccupied()
        {
            int count = 0;
            Game gdata = GameClient.Get().GetGameData();

            foreach (GroupSlot slot in group_slots)
            {
                List<Card> cards = gdata.GetSlotCards(slot.slot);
                slot.timer += (cards.Count > 0 ? 1f : -1f) * Time.deltaTime / reduce_delay;
                slot.timer = Mathf.Clamp01(slot.timer);

                if (cards.Count == gdata.players.First(pl => pl.player_id == slot.slot.p).head_coach.positional_Scheme[player_position_type].pos_max)
                    count += 1;
            }

            nb_occupied = count;
        }

        public void UpdatePositions()
        {
            bool even = nb_occupied % 2 == 0;
            float offset = (nb_occupied / 2) * -spacing;
            if (even)
                offset += spacing * 0.5f;

            int index = 0;
            foreach (GroupSlot slot in group_slots)
            {
                if (slot.IsOccupied)
                {
                    slot.pos = transform.position + Vector3.right * (index * spacing + offset);
                    index++;
                }
                else
                {
                    slot.pos = transform.position + Vector3.right * (nb_occupied * spacing + offset);
                }
            }
        }

        public bool IsInGroup(CardPositionSlot slot)
        {
            return IsInGroup(slot.x, slot.y, slot.p);
        }

        public bool IsInGroup(int x, int y)
        {
            CardPositionSlot min = GetSlotMin();
            CardPositionSlot max = GetSlotMax();
            return x >= min.x && x <= max.x && y >= min.y && y <= max.y;
        }

        public bool IsInGroup(int x, int y, int p)
        {
            CardPositionSlot min = GetSlotMin();
            CardPositionSlot max = GetSlotMax();
            return x >= min.x && x <= max.x && y >= min.y && y <= max.y && p >= min.p && p <= max.p;
        }

        public CardPositionSlot GetSlotMin()
        {
            return GetSlot(min_x, y);
        }

        public CardPositionSlot GetSlotMax()
        {
            return GetSlot(max_x, y);
        }

        //Find the actual slot coordinates of this board slot
        public CardPositionSlot GetSlot(int x, int y)
        {
            int p = 0;
            int max_players = 0;
            Game gdata = GameClient.Get().GetGameData();
            if (type == BoardSlotType.FlipX)
            {
                int pid = GameClient.Get().GetPlayerID();
                int px = x;
                max_players = gdata.players.First(play => play.player_id == pid).head_coach.positional_Scheme[player_position_type].pos_max;
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
            max_players = gdata.players.First(play => play.player_id == p).head_coach.positional_Scheme[player_position_type].pos_max;

            return new CardPositionSlot(x, y, p, max_players, player_position_type);
        }

        public override CardPositionSlot GetSlot(Vector3 wpos)
        {
            GroupSlot nearest = null;
            float min_dist = 99f;

            foreach (GroupSlot spos in group_slots)
            {
                float dist = (spos.pos - wpos).magnitude;
                if (dist < min_dist)
                {
                    min_dist = dist;
                    nearest = spos;
                }
            }

            if (nearest != null)
                return nearest.slot;
            return CardPositionSlot.None;
        }

        public virtual CardPositionSlot GetSlotOccupied(Vector3 wpos)
        {
            GroupSlot nearest = null;
            float min_dist = 99f;

            foreach (GroupSlot spos in group_slots)
            {
                float dist = (spos.pos - wpos).magnitude;
                if (spos.IsOccupied && dist < min_dist)
                {
                    min_dist = dist;
                    nearest = spos;
                }
            }

            if (nearest != null)
                return nearest.slot;
            return CardPositionSlot.None;
        }

        public override List<Card> GetSlotCards(Vector3 wpos)
        {
            Game gdata = GameClient.Get().GetGameData();
            CardPositionSlot slot = GetSlotOccupied(wpos);
            if (slot != CardPositionSlot.None)
                return gdata.GetSlotCards(slot);
            return null;
        }

        public override bool HasSlot(CardPositionSlot slot)
        {
            foreach (GroupSlot spos in group_slots)
            {
                if (spos.slot == slot)
                    return true;
            }
            return false;
        }

        public override Vector3 GetPosition(CardPositionSlot slot)
        {
            foreach (GroupSlot spos in group_slots)
            {
                if (spos.slot == slot)
                    return spos.pos;
            }
            return transform.position;
        }

        public override CardPositionSlot GetEmptySlot(Vector3 wpos)
        {
            foreach (GroupSlot slot in group_slots)
            {
                if (!slot.IsOccupied)
                    return slot.slot;
            }
            return CardPositionSlot.None;
        }

    }

    public class GroupSlot
    {
        public CardPositionSlot slot;
        public Vector3 pos;
        public float timer;

        public bool IsOccupied { get { return timer > 0.01f;  } }
    }
}