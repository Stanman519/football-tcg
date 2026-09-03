using TcgEngine.Client;
using TcgEngine;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using TcgEngine.Gameplay;

public class PlayEnhancerSlot : MonoBehaviour, IDropHandler
{
    [HideInInspector] public TextMeshProUGUI displayText;

    private static readonly Color C_HINT     = new Color(0.55f, 0.65f, 0.55f, 1f);
    private static readonly Color C_OCCUPIED = new Color(0.91f, 0.39f, 0.10f, 1f);

    private Card storedCard = null;

    public Card GetCard() => storedCard;

    public void OnDrop(PointerEventData eventData)
    {
        HandCard handCard = eventData.pointerDrag?.GetComponent<HandCard>();
        if (handCard == null) return;

        Card card = handCard.GetCard();
        if (card == null) return;

        // Notify PlayCallUIScript — it may reject if incompatible
        var uiScript = FindFirstObjectByType<PlayCallUIScript>();
        if (uiScript != null)
        {
            storedCard = card;
            uiScript.SetEnhancerCard(card);
            // If UI script cleared us (rejected), storedCard will be null via Clear()
            if (storedCard == null)
            {
                // Rejected — don't consume the drag
                return;
            }
        }
        else
        {
            storedCard = card;
        }

        RefreshDisplay();
        handCard.EndDrag();
    }

    public void SetCard(Card card)
    {
        storedCard = card;
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
            displayText.text  = "\u2295  Drop enhancer here";
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
