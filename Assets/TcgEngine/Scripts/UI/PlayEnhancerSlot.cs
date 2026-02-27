using TcgEngine.Client;
using TcgEngine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Assets.TcgEngine.Scripts.Gameplay;

public class PlayEnhancerSlot : MonoBehaviour, IDropHandler
{
    [HideInInspector] public Text displayText; // wired by PlayCallPanelBuilder

    private static readonly Color C_HINT     = new Color(0.55f, 0.65f, 0.55f, 1f); // empty prompt
    private static readonly Color C_OCCUPIED = new Color(0.91f, 0.39f, 0.10f, 1f); // orange = card placed

    private Card storedCard = null;

    public void OnDrop(PointerEventData eventData)
    {
        HandCard draggedCard = HandCard.GetDrag();
        if (draggedCard == null) return;

        storedCard = draggedCard.GetCard();
        if (storedCard == null) return;

        // Notify game logic
        var uiScript = FindFirstObjectByType<PlayCallUIScript>();
        if (uiScript != null)
            uiScript.SetEnhancerCard(storedCard);

        // Update display
        RefreshDisplay();
    }

    public void Clear()
    {
        storedCard = null;
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (displayText == null) return;

        if (storedCard == null)
        {
            displayText.text  = "\u2295  Drop play enhancer card here";
            displayText.color = C_HINT;
        }
        else
        {
            string playReq = "";
            if (storedCard.CardData != null && storedCard.CardData.required_plays != null
                && storedCard.CardData.required_plays.Length > 0)
            {
                var plays = storedCard.CardData.required_plays;
                playReq = "  \u2022  " + string.Join(" / ", System.Array.ConvertAll(plays, p => p.ToString()));
            }

            displayText.text  = storedCard.CardData != null
                ? storedCard.CardData.title + playReq
                : storedCard.card_id + playReq;
            displayText.color = C_OCCUPIED;
        }
    }
}
