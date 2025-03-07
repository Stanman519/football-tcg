using TcgEngine.Client;
using TcgEngine;
using UnityEngine;
using UnityEngine.EventSystems;
using Assets.TcgEngine.Scripts.Gameplay;

public class PlayEnhancerSlot : MonoBehaviour, IDropHandler
{
    private Card storedCard = null;

    public void OnDrop(PointerEventData eventData)
    {
        HandCard draggedCard = HandCard.GetDrag();
        if (draggedCard != null)
        {
            storedCard = draggedCard.GetCard();
            PlayCallManager playManager = FindFirstObjectByType<PlayCallManager>();
            playManager.SetEnhancerCard(storedCard);
        }
    }
}