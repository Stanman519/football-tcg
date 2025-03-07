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

    public void OnBeginDrag(PointerEventData eventData)
    {
        draggingCard = gameObject;
        originalPosition = draggingCard.transform.position;

        HighlightValidSlots();
    }

    public void OnDrag(PointerEventData eventData)
    {
        draggingCard.transform.position = Camera.main.ScreenToWorldPoint(new Vector3(eventData.position.x, eventData.position.y, 10));
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (validSlot != null)
        {
            SnapToSlot(validSlot, eventData);
        }
        else
        {
            draggingCard.transform.position = originalPosition;
        }

        ResetSlotHighlights();
    }

    private void HighlightValidSlots()
    {
        PlayerPositionGrp position = draggingCard.GetComponent<Card>().Data.playerPosition;
        List<BoardSlot> slots = FindFirstObjectByType<FieldSlotManager>().GetSlotsForPosition(position);

        foreach (BoardSlot slot in slots)
        {
            slot.HighlightSlot();
        }
    }

    private void ResetSlotHighlights()
    {
        List<BoardSlot> slots = FindFirstObjectByType<FieldSlotManager>().GetSlotsForPosition(draggingCard.GetComponent<Card>().Data.playerPosition);
        foreach (BoardSlot slot in slots)
        {
            slot.UnhighlightSlot();
        }
    }

    private void SnapToSlot(BoardSlot slot, PointerEventData eventData)
    {
        draggingCard.transform.position = slot.transform.position;
        slot.OnDrop(eventData);
    }
}
