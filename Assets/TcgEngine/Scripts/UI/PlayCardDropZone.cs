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
            Debug.Log($"PlayCardDropZone.OnDrop pointerDrag: {eventData.pointerDrag?.name}");

            HandCard handCard = eventData.pointerDrag?.GetComponent<HandCard>();
            if (handCard == null)
            {
                Debug.Log("PlayCardDropZone.OnDrop: pointerDrag has no HandCard component or pointerDrag is null");
                return;
            }

            Card card = handCard.GetCard();
            if (card == null || card.Data == null)
            {
                Debug.Log("PlayCardDropZone.OnDrop: card or card.Data null");
                // Still end the drag even if card is invalid
                handCard.EndDrag();
                return;
            }

            PlayerPositionGrp position = card.Data.playerPosition;
            int playerId = GameClient.Get().GetPlayerID();
            List<BoardSlot> slots = FieldSlotManager.Instance.GetSlotsForPosition(position, playerId);

            Debug.Log($"PlayCardDropZone.OnDrop: Found slots for position {position}: {string.Join(", ", slots)}");

            bool cardPlayed = false;
            foreach (BoardSlot slot in slots)
            {
                if (slot.IsEmpty())
                {
                    Debug.Log($"PlayCardDropZone: dropping card {card.uid} to slot {slot.assignedSlot.posGroupType}-{slot.assignedSlot.p}");
                    GameClient.Get().PlayCard(card, slot.assignedSlot);
                    cardPlayed = true;
                    break;
                }
                else
                {
                    Debug.Log($"PlayCardDropZone: slot {slot.assignedSlot.posGroupType}-{slot.assignedSlot.p} is not empty");
                }
            }

            if (!cardPlayed)
            {
                Debug.Log("No open slot for position: " + position);
            }

            // Always end the drag after processing the drop
            handCard.EndDrag();
        }
    }
}
