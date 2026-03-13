using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using TcgEngine;
using TcgEngine.Client;
using Assets.TcgEngine.Scripts.Gameplay;

/// <summary>
/// Live ball card drop zone. Appears during GamePhase.LiveBall.
/// Player drags a live ball card from hand onto this zone to play it (auto-readies).
/// Pass button readies the player without playing a card.
/// Wire up in the scene: add this script to a panel under GameCanvas.
/// </summary>
public class LiveBallCardZone : MonoBehaviour, IDropHandler
{
    [Header("References")]
    public Button passButton;
    public TextMeshProUGUI statusText;

    private CanvasGroup canvasGroup;

    private static LiveBallCardZone _instance;
    public static LiveBallCardZone Get() => _instance;

    void Awake()
    {
        _instance = this;
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    void Start()
    {
        if (passButton != null)
            passButton.onClick.AddListener(OnClickPass);

        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Update()
    {
        GameClient client = GameClient.Get();
        if (client == null || !client.IsReady()) return;

        Game g = client.GetGameData();
        Player player = client.GetPlayer();
        if (g == null || player == null) return;

        bool liveBallPhase = g.phase == GamePhase.LiveBall;
        bool alreadyReady = player.IsReadyForPhase(GamePhase.LiveBall);

        if (liveBallPhase && !alreadyReady)
        {
            gameObject.SetActive(true);
            if (statusText != null)
                statusText.text = player.LiveBallCard != null
                    ? "Card selected — waiting for opponent..."
                    : "Ball is live!\nDrop a Live Ball card here, or Pass.";
        }
        else if (liveBallPhase && alreadyReady)
        {
            // Stay visible but dim while waiting for opponent
            if (canvasGroup != null) canvasGroup.alpha = 0.5f;
            if (statusText != null)
                statusText.text = "Waiting for opponent...";
            if (passButton != null) passButton.interactable = false;
        }
        else
        {
            gameObject.SetActive(false);
            if (canvasGroup != null) canvasGroup.alpha = 1f;
            if (passButton != null) passButton.interactable = true;
        }
    }

    // ── Drop handler ────────────────────────────────────────

    public void OnDrop(PointerEventData eventData)
    {
        GameClient client = GameClient.Get();
        if (client == null) return;

        Game g = client.GetGameData();
        Player player = client.GetPlayer();
        if (g == null || player == null) return;
        if (g.phase != GamePhase.LiveBall) return;
        if (player.IsReadyForPhase(GamePhase.LiveBall)) return;

        HandCard dragged = HandCard.GetDrag();
        if (dragged == null) return;

        Card card = dragged.GetCard();
        if (card == null || !card.CardData.IsLiveBall()) return;

        // Play the card (server sets player.LiveBallCard)
        client.PlayCard(card, new CardPositionSlot());

        // Auto-ready — player has made their choice
        ConfirmReady(client, player, g);
    }

    // ── Pass button ─────────────────────────────────────────

    public void OnClickPass()
    {
        GameClient client = GameClient.Get();
        if (client == null) return;

        Game g = client.GetGameData();
        Player player = client.GetPlayer();
        if (g == null || player == null) return;
        if (g.phase != GamePhase.LiveBall) return;
        if (player.IsReadyForPhase(GamePhase.LiveBall)) return;

        ConfirmReady(client, player, g);
    }

    // ── Shared ready logic ───────────────────────────────────

    private void ConfirmReady(GameClient client, Player player, Game g)
    {
        player.SetReadyForPhase(g.phase, true);
        client.SendAction(GameAction.PlayerReadyPhase);
        Debug.Log($"[LiveBall] Player {player.player_id} confirmed ready.");
    }
}
