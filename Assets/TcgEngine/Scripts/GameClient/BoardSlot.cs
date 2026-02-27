using Assets.TcgEngine.Scripts.Gameplay;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TcgEngine.Client
{
    public class BoardSlot : MonoBehaviour, IDropHandler
    {
        public int slot_id; // WR1 vs WR2, OL1 vs OL2, etc.
        public int slotIndex = 0;           // Index within position group (0 = first slot, 1 = second, ...)
        protected Collider collide;
        protected Bounds bounds;
        public int player_id;
        protected float start_alpha = 0f;
        protected float current_alpha = 0f;
        protected float target_alpha = 0f;
        public CardPositionSlot assignedSlot;
        private Card assignedCard;
        public PlayerPositionGrp player_position_type;
        public bool isStarSlot = false; // Determines if a Star/Superstar can go here

        private Vector3 targetLocalPos;
        public float moveSpeed = 3f;

        private static List<BoardSlot> slot_list = new List<BoardSlot>();
        public SpriteRenderer spriteRenderer; // Controls the slot appearance
        public Sprite defaultSprite; // X/O placeholder sprite
        public Sprite activePlayerSprite; // Card slot sprite
        public Sprite defaultSpriteOffense;
        public Sprite defaultSpriteDefense;
        public GameObject replacementMarker; // Placeholder (X or O) object
        protected virtual void Awake()
        {
            slot_list.Add(this);
            spriteRenderer = GetComponent<SpriteRenderer>();
            collide = GetComponent<Collider>();
            start_alpha = spriteRenderer.color.a;
            bounds = collide.bounds;
            targetLocalPos = transform.localPosition;
        }

        protected virtual void OnDestroy()
        {
            slot_list.Remove(this);
        }

        protected virtual void Update()
        {
            current_alpha = Mathf.MoveTowards(current_alpha, target_alpha * start_alpha, 2f * Time.deltaTime);
            if (!GameClient.Get()?.IsReady() ?? false) return;

            // Keep assigned card in sync with actual game state so the X/O hides/shows correctly
            // even if AssignCard() was never called externally.
            Game gdata = GameClient.Get().GetGameData();
            if (gdata != null)
            {
                var slotCards = gdata.GetSlotCards(assignedSlot);
                Card occupying = (slotCards != null && slotCards.Count > 0) ? slotCards[0] : null;
                if (occupying != assignedCard)
                {
                    assignedCard = occupying;
                    UpdateSlotVisual();
                }
            }

            bool valid = IsValidDragTarget();

            // Visual Feedback: dim when not a valid drop target
            if (spriteRenderer != null && spriteRenderer.enabled)
            {
                Color col = valid ? Color.white : new Color(1f, 1f, 1f, 0.2f);
                spriteRenderer.color = col;
            }

            target_alpha = valid ? 1f : 0f;

            // Smooth lerp to formation target position
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetLocalPos, moveSpeed * Time.deltaTime);
        }
        public bool IsValidDragTarget()
        {
            Card dragCard = HandCard.GetDrag()?.GetCard();
            if (dragCard == null)
                return false;

            // CHECK: Player can only drag cards for their current side
            Game game = GameClient.Get().GetGameData();
            Player current_player = game.GetPlayer(GameClient.Get().GetPlayerID());
            bool is_offensive_player = (current_player.player_id == game.current_offensive_player.player_id);
            
            bool card_is_offensive = System.Array.Exists(game.offensive_pos_grps, pos => pos == dragCard.Data.playerPosition);
            bool card_is_defensive = System.Array.Exists(game.defensive_pos_grps, pos => pos == dragCard.Data.playerPosition);
            
            // If you're on offense, you can only play offensive cards
            if (is_offensive_player && card_is_defensive)
                return false;
            
            // If you're on defense, you can only play defensive cards
            if (!is_offensive_player && card_is_offensive)
                return false;

            // Also check position match
            if (dragCard.Data.playerPosition != player_position_type)
                return false;

            // This specific slot must be empty
            if (!IsEmpty())
                return false;

            Player player = game.GetPlayer(GameClient.Get().GetPlayerID());

            int currentCount = player.cards_board.FindAll(c => c.Data.playerPosition == player_position_type).Count;
            int maxAllowed = player.head_coach.positional_Scheme[player_position_type].pos_max;

            return currentCount < maxAllowed;
        }
        public void OnDrop(PointerEventData eventData)
        {
            Debug.Log($"BoardSlot.OnDrop: slot {assignedSlot.posGroupType}-{assignedSlot.p}, pointerDrag: {eventData.pointerDrag?.name}");
            Card dragCard = HandCard.GetDrag()?.GetCard();
            if (dragCard != null && dragCard.Data.playerPosition == player_position_type)
            {
                Debug.Log($"BoardSlot.OnDrop: playing card {dragCard.uid} to slot {assignedSlot.posGroupType}-{assignedSlot.p}");
                GameClient.Get().PlayCard(dragCard, assignedSlot);
            }
            else
            {
                Debug.Log("BoardSlot.OnDrop: dragCard null or mismatched position");
            }
        }

        public CardPositionSlot GetSlot()
        {
            /*            int p = GameClient.Get().GetPlayerID();
                        Game gdata = GameClient.Get().GetGameData();
                        return new CardPositionSlot(x, y, p, gdata.GetPlayer(p).head_coach.positional_Scheme[player_position_type].pos_max, player_position_type);*/
            return assignedSlot;
        }

        public void HighlightSlot()
        {
            /*if (spriteRenderer != null)
                spriteRenderer.color = new Color(1f, 1f, 1f, 0.7f); // Slight transparency to highlight*/
        }

        public void UnhighlightSlot()
        {
            /*            if (spriteRenderer != null)
                            spriteRenderer.color = new Color(1f, 1f, 1f, 1f); // Restore full visibility*/
        }
        public static List<BoardSlot> GetAll()
        {
            return slot_list;
        }
        // slotIdx: 0 = first slot for this position group, 1 = second, etc.
        // max_cards is repurposed as the per-slot identifier (slotIdx + 1) so each slot
        // has a unique CardPositionSlot that matches card.slot set by the server.
        public void Initialize(PlayerPositionGrp positionGroup, int playerId, bool isOffense, int slotIdx = 0)
        {
            this.player_position_type = positionGroup;
            this.slotIndex = slotIdx;
            this.assignedSlot = new CardPositionSlot(playerId, slotIdx + 1, positionGroup);

            defaultSprite = isOffense ? defaultSpriteOffense : defaultSpriteDefense;

            UpdateSlotVisual();
        }

        public void SetTargetPosition(Vector3 localPos)
        {
            localPos.z = -1f;
            targetLocalPos = localPos;
        }

        public virtual Vector3 GetPosition(CardPositionSlot slot)
        {
            return transform.position;
        }
        public static BoardSlot GetNearest(Vector3 pos)
        {
            BoardSlot nearest = null;
            float min_dist = 999f;
            foreach (BoardSlot slot in GetAll())
            {
                float dist = (slot.transform.position - pos).magnitude;
                if (slot.IsInside(pos) && dist < min_dist)
                {
                    min_dist = dist;
                    nearest = slot;
                }
            }
            return nearest;
        }
        public void AssignCard(Card card)
        {
            assignedCard = card;
            Debug.Log($"BoardSlot.AssignCard: slot {assignedSlot.posGroupType}-{assignedSlot.p}, assigned card {card?.uid}");
            UpdateSlotVisual(); // BoardCard handles the visual; we just hide the X/O marker
        }
        public bool IsEmpty()
        {
            return assignedCard == null;
        }
        public void RemoveCard()
        {
            assignedCard = null;
            UpdateSlotVisual();
        }
        public void SetReplacement(GameObject replacement)
        {
            replacementMarker = replacement;
        }
        private void UpdateSlotVisual()
        {
            if (spriteRenderer != null)
            {
                // When card is assigned, hide the slot sprite (red circle)
                // When slot is empty, show the default sprite
                if (assignedCard == null)
                {
                    spriteRenderer.sprite = defaultSprite;
                    spriteRenderer.enabled = true;  // Ensure it's visible when empty
                }
                else
                {
                    // Hide the slot visual completely when a card is assigned
                    spriteRenderer.sprite = null;
                    spriteRenderer.enabled = false;  // Disable renderer to ensure card shows on top
                }
            }

            if (replacementMarker != null)
                replacementMarker.SetActive(assignedCard == null);
        }
        public virtual List<Card> GetSlotCards(Vector3 wpos)
        {
            Game gdata = GameClient.Get().GetGameData();
            CardPositionSlot slot = GetSlot();
            return gdata.GetSlotCards(slot);
        }


        public virtual bool IsInside(Vector3 wpos)
        {
            return bounds.Contains(wpos);
        }

        public static BoardSlot Get(CardPositionSlot slot)
        {
            foreach (BoardSlot bslot in GetAll())
            {
                if (bslot.GetSlot() == slot)
                    return bslot;
            }
            return null;
        }
        
        
    }

    public enum BoardSlotType
    {
        Fixed = 0,              //x,y,p = slot
        PlayerSelf = 5,         //p = client player id
        PlayerOpponent = 7,     //p = client's opponent player id
        FlipX = 10,              //p=0,   x=unchanged for first player,  x=reversed for second player
        FlipY = 11,              //p=0,   y=unchanged for first player,  y=reversed for second player
    }
}