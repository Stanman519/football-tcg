using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TcgEngine.Client;
using TcgEngine;
using Assets.TcgEngine.Scripts.Gameplay;

public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private GameObject draggingCard;
    private Vector3 originalPosition;
    private BoardSlot validSlot;
    private HandCard draggingHandCard; // Track HandCard for drag state sync

    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log($"OnBeginDrag: {gameObject.name}");
        draggingCard = gameObject;
        originalPosition = draggingCard.transform.position;
        
        // NEW: Check if this card can even be dragged (offensive card on offense, defensive on defense)
        Card cardComp = draggingCard.GetComponent<Card>();
        if (cardComp != null)
        {
            Game game = GameClient.Get().GetGameData();
            Player current_player = game.GetPlayer(GameClient.Get().GetPlayerID());
            bool is_offensive_player = (current_player.player_id == game.current_offensive_player.player_id);
            
            bool card_is_offensive = System.Array.Exists(game.offensive_pos_grps, pos => pos == cardComp.Data.playerPosition);
            bool card_is_defensive = System.Array.Exists(game.defensive_pos_grps, pos => pos == cardComp.Data.playerPosition);
            
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
        
        // Mark HandCard as dragging so IsDrag() stays true while event is active
        draggingHandCard = draggingCard?.GetComponent<HandCard>();
        if (draggingHandCard != null)
        {
            draggingHandCard.StartDrag();
        }
        
        HighlightValidSlots();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (draggingCard == null)
        {
            Debug.Log("OnDrag called but draggingCard is null");
            return;
        }

        Debug.Log($"OnDrag: {draggingCard?.name}, Position: {eventData.position}");
        draggingCard.transform.position = Camera.main.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, 10));

        // Update validSlot by raycasting to nearest BoardSlot under cursor
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, 10));
        BoardSlot nearest = BoardSlot.GetNearest(worldPos);
        if (nearest != null && nearest.IsValidDragTarget())
        {
            if (validSlot != nearest)
            {
                Debug.Log($"Valid slot under cursor: {nearest.name}");
                validSlot = nearest;
            }
        }
        else
        {
            if (validSlot != null)
            {
                Debug.Log("No longer over a valid slot");
                validSlot = null;
            }
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Debug.Log($"OnEndDrag: {draggingCard?.name}, validSlot: {(validSlot != null ? validSlot.name : "null")}");
        if (validSlot != null)
        {
            SnapToSlot(validSlot, eventData);
        }
        else
        {
            Debug.Log("No valid slot found, returning card to original position.");
            if (draggingCard != null)
                draggingCard.transform.position = originalPosition;
        }

        ResetSlotHighlights();
        
        // ALWAYS clear HandCard drag state, even on failed drop
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
        var cardComp = draggingCard.GetComponent<Card>();
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
        List<BoardSlot> slots = FindFirstObjectByType<FieldSlotManager>().GetSlotsForPosition(draggingCard.GetComponent<Card>().Data.playerPosition, draggingCard.GetComponent<Card>().player_id);
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
