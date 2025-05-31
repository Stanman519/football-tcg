using Assets.TcgEngine.Scripts.Gameplay;
using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;
using TcgEngine.Client;

namespace Assets.TcgEngine.Scripts.UI
{
    public class PlayCardDropZone : MonoBehaviour, IDropHandler
    {
        public void OnDrop(PointerEventData eventData)
        {
            HandCard handCard = eventData.pointerDrag?.GetComponent<HandCard>();
            if (handCard == null) return;

            Card card = handCard.GetCard();
            if (card == null || card.Data == null) return;

            PlayerPositionGrp position = card.Data.playerPosition;
            List<BoardSlot> slots = FieldSlotManager.Instance.GetSlotsForPosition(position);

            foreach (BoardSlot slot in slots)
            {
                if (slot.IsEmpty())
                {
                    GameClient.Get().PlayCard(card, slot.GetSlot());
                    return;
                }
            }

            Debug.Log("No open slot for position: " + position);
        }
    }
}
