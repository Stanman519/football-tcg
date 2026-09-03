using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TcgEngine.Client;
using TcgEngine;
using TcgEngine.Gameplay;
using TcgEngine.UI;

public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private GameObject draggingCard;
    private BoardSlot validSlot;
    private HandCard draggingHandCard; // Track HandCard for drag state sync

    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log($"OnBeginDrag: {gameObject.name}");
        draggingCard = gameObject;
        
        // NEW: Check if this card can even be dragged (offensive card on offense, defensive on defense)
        Card cardComp = draggingCard.GetComponent<HandCard>()?.GetCard();
        if (cardComp != null)
        {
            Game game = GameClient.Get().GetGameData();
            Player current_player = game.GetPlayer(GameClient.Get().GetPlayerID());
            bool is_offensive_player = (current_player.player_id == game.current_offensive_player.player_id);
            
            // Side from CardType (enhancers/live-ball cards have playerPosition NONE)
            bool card_is_offensive = Game.IsOffenseCardType(cardComp.Data.type);
            bool card_is_defensive = Game.IsDefenseCardType(cardComp.Data.type);
            
            // If card type doesn't match player role, don't allow drag
            if (is_offensive_player && card_is_defensive)
            {
                Debug.Log($"Cannot drag defensive card while on offense!");
                draggingCard = null;
                return;
            }
            if (!is_offensive_player && card_is_offensive)
            {
                Debug.Log($"Cannot drag offensive card while on defense!");
                draggingCard = null;
                return;
            }
        }

        // Block drag if card isn't eligible for current phase
        HandCard hc = draggingCard?.GetComponent<HandCard>();
        if (hc != null)
        {
            CardPlayError err = hc.GetPlayError();
            if (err != CardPlayError.None)
            {
                Debug.Log($"[DragDrop] {hc.GetCard()?.card_id} not draggable: {err}");
                StatChangePopup.Instance?.ShowCustom(hc.transform.position, Game.GetCardPlayErrorText(err), Color.red);
                draggingCard = null;
                return;
            }
        }

        // Mark HandCard as dragging so IsDrag() stays true while event is active
        draggingHandCard = hc;
        if (draggingHandCard != null)
        {
            draggingHandCard.StartDrag();
        }
        
        HighlightValidSlots();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (draggingCard == null)
            return;

        // HandCard.Update handles the visual follow via anchoredPosition — don't touch transform.position here.
        // Just track which BoardSlot (if any) is under the cursor.
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, 10));
        BoardSlot nearest = BoardSlot.GetNearest(worldPos);
        if (nearest != null && nearest.IsValidDragTarget())
        {
            if (validSlot != nearest)
                validSlot = nearest;
        }
        else
        {
            validSlot = null;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        bool handled = false;

        if (draggingHandCard == null)
        {
            Debug.LogWarning("[DragDrop] OnEndDrag: draggingHandCard is null — drag was cancelled?");
        }
        else
        {
            Card card = draggingHandCard.GetCard();
            GameClient client = GameClient.Get();
            Game g = client?.GetGameData();

            if (card?.CardData == null)
            {
                Debug.LogWarning($"[DragDrop] OnEndDrag: card or CardData is null (card={card != null})");
            }
            else if (g == null)
            {
                Debug.LogWarning("[DragDrop] OnEndDrag: game data is null");
            }
            else
            {
                Debug.LogWarning($"[DragDrop] OnEndDrag: card={card.CardData.title} type={card.CardData.type} phase={g.phase}");

                // Player cards during ChoosePlayers still snap to BoardSlot (handled below via validSlot).
                // All other drops route through PlayCardDropZone — phase-aware, self-instantiating.
                bool routeToBoardSlot = card.CardData.IsPlayer() && g.phase == GamePhase.ChoosePlayers;
                if (!routeToBoardSlot)
                {
                    PlayCardDropZone.Get().OnDrop(eventData);
                    handled = true;
                    validSlot = null;
                }
            }
        }

        if (!handled && validSlot != null)
        {
            SnapToSlot(validSlot, eventData);
        }
        // If no valid slot, HandCard.Update will animate the card back to deck_position automatically

        ResetSlotHighlights();

        // Always clear HandCard drag state
        if (draggingHandCard != null)
        {
            draggingHandCard.EndDrag();
            draggingHandCard = null;
        }

        validSlot = null;
        draggingCard = null;
    }

    private void HighlightValidSlots()
    {
        Debug.Log($"HighlightValidSlots for card: {draggingCard?.name}");
        var cardComp = draggingCard.GetComponent<HandCard>()?.GetCard();
        if (cardComp == null)
        {
            Debug.Log("HighlightValidSlots: draggingCard has no Card component");
            return;
        }

        PlayerPositionGrp position = cardComp.Data.playerPosition;
        List<BoardSlot> slots = FindFirstObjectByType<FieldSlotManager>().GetSlotsForPosition(position, cardComp.player_id);

        foreach (BoardSlot slot in slots)
        {
            Debug.Log($"Highlighting slot: {slot.name}");
            slot.HighlightSlot();
        }
    }

    private void ResetSlotHighlights()
    {
        if (draggingCard == null)
        {
            Debug.Log("ResetSlotHighlights: draggingCard is null");
            return;
        }

        Debug.Log($"ResetSlotHighlights for card: {draggingCard?.name}");
        Card resetCard = draggingCard.GetComponent<HandCard>()?.GetCard();
        if (resetCard == null) return;
        List<BoardSlot> slots = FindFirstObjectByType<FieldSlotManager>().GetSlotsForPosition(resetCard.Data.playerPosition, resetCard.player_id);
        foreach (BoardSlot slot in slots)
        {
            Debug.Log($"Unhighlighting slot: {slot.name}");
            slot.UnhighlightSlot();
        }
    }

    private void SnapToSlot(BoardSlot slot, PointerEventData eventData)
    {
        Debug.Log($"SnapToSlot: {slot.name} for card: {draggingCard?.name}");
        draggingCard.transform.position = slot.transform.position;
        slot.OnDrop(eventData);
    }
}
