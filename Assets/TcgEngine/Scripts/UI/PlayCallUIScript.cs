using System;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using TcgEngine;
using UnityEngine.Rendering.Universal;
using Assets.TcgEngine.Scripts.Gameplay;

public class PlayCallUIScript : MonoBehaviour
{
    public GameObject playCallPanel;
    [Header("Offense Buttons")]
    [field: SerializeField]
    public Button runButton;
    public Button shortPassButton;
    public Button longPassButton;
    public PlayEnhancerSlot enhancerSlot;

    private PlayType selectedPlay = PlayType.Huddle;
    private Card selectedEnhancer = null;
    private bool isPlayLocked = false;
    private bool lastShouldShowPanel = false;  // Track state change for logging

    void Start()
    {
        Debug.Log("[PlayCallUIScript] Start() called");
        
        if (playCallPanel != null)
            playCallPanel.SetActive(false);

        if (runButton != null)
            runButton.onClick.AddListener(() => SelectPlay(PlayType.Run));
        if (shortPassButton != null)
            shortPassButton.onClick.AddListener(() => SelectPlay(PlayType.ShortPass));
        if (longPassButton != null)
            longPassButton.onClick.AddListener(() => SelectPlay(PlayType.LongPass));

        if (GameClient.Get() != null)
        {
            GameClient.Get().onRefreshAll += OnGameDataRefreshed;
            Debug.Log("[PlayCallUIScript] Registered with GameClient.onRefreshAll");
        }
        else
        {
            Debug.LogError("[PlayCallUIScript] ERROR: GameClient.Get() returned null!");
        }
        
        Debug.Log("[PlayCallUIScript] Setup complete");
    }

    void Update()
    {
        // Auto show/hide based on current phase
        Game gameData = GameClient.Get()?.GetGameData();
        if (gameData != null)
        {
            Player currentPlayer = GameClient.Get().GetPlayer();
            bool playerHasSelected = currentPlayer != null && currentPlayer.SelectedPlay != PlayType.Huddle;
            bool shouldShowPanel = (gameData.phase == GamePhase.ChoosePlay) && 
                                    !playerHasSelected && 
                                    !isPlayLocked;

            // DEBUG: Only log when shouldShowPanel state changes
            if (shouldShowPanel != lastShouldShowPanel)
            {
                Debug.LogWarning($"[PlayCallUIScript-ChoosePlay] Phase={gameData.phase}, PlayerSelected={playerHasSelected}, IsLocked={isPlayLocked}, ShouldShow={shouldShowPanel}, PanelActive={playCallPanel.activeSelf}");
                lastShouldShowPanel = shouldShowPanel;
            }

            if (shouldShowPanel && !playCallPanel.activeSelf && !isPlayLocked)
            {
                Debug.Log("[PlayCallUIScript] ✓ CONDITIONS MET - Showing panel");
                ShowPlayCallUI();
            }
            else if (!shouldShowPanel && playCallPanel.activeSelf && !isPlayLocked)
            {
                Debug.Log("[PlayCallUIScript] Hiding panel");
                playCallPanel.SetActive(false);
            }
        }
    }

    private void OnGameDataRefreshed()
    {
        Game gameData = GameClient.Get()?.GetGameData();
        if (gameData != null && gameData.phase == GamePhase.ChoosePlay)
        {
            isPlayLocked = false;
            selectedPlay = PlayType.Huddle;
            selectedEnhancer = null;
        }
    }

    public void ShowPlayCallUI()
    {
        if (isPlayLocked)
            return;

        Debug.Log("[PlayCallUIScript] ShowPlayCallUI() - Activating panel");
        playCallPanel.SetActive(true);
        selectedPlay = PlayType.Huddle;
        selectedEnhancer = null;

        // Reset the PlayCallManager state if it exists
        PlayCallManager manager = playCallPanel.GetComponent<PlayCallManager>();
        if (manager != null)
        {
            manager.ResetState();
        }
    }

    private void SelectPlay(PlayType play)
    {
        if (isPlayLocked) return;

        selectedPlay = play;
        Debug.Log("Selected Play: " + play);
        
        // Immediately confirm this selection
        ConfirmPlaySelection();
    }

    public void SetEnhancerCard(Card card)
    {
        if (isPlayLocked) return;

        selectedEnhancer = card;
        Debug.Log("Selected Enhancer: " + (card != null ? card.card_id : "null"));
    }

    private void ConfirmPlaySelection()
    {
        if (selectedPlay == PlayType.Huddle)
        {
            Debug.LogWarning("No play selected!");
            return;
        }

        Debug.Log("[PlayCallUIScript] ConfirmPlaySelection() - Locking play and sending to server");
        isPlayLocked = true;
        playCallPanel.SetActive(false);

        // Send choice to GameClient for syncing with opponent
        GameClient.Get().SendPlaySelection(selectedPlay, selectedEnhancer);
    }

    private void OnDestroy()
    {
        if (GameClient.Get() != null)
            GameClient.Get().onRefreshAll -= OnGameDataRefreshed;
    }
}
