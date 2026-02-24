using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using TcgEngine;
using UnityEngine.Rendering.Universal;
using Assets.TcgEngine.Scripts.Gameplay;

public class PlayCallManager : MonoBehaviour
{
    public Button runButton;
    public Button shortPassButton;
    public Button longPassButton;
    public Button confirmButton;
    public PlayEnhancerSlot enhancerSlot; // A UI slot for play enhancer card

    private PlayType selectedPlay = PlayType.Huddle; // Default to Huddle before selection
    private Card selectedEnhancer = null; // Play Enhancer Card

    private bool isPlayLocked = false;

    void Start()
    {
        Debug.Log("[PlayCallManager] Start() called");

        if (runButton == null)
            Debug.LogError("[PlayCallManager] ERROR: runButton is not assigned in Inspector!");
        else
            Debug.Log("[PlayCallManager] runButton assigned ✓");

        if (shortPassButton == null)
            Debug.LogError("[PlayCallManager] ERROR: shortPassButton is not assigned in Inspector!");
        else
            Debug.Log("[PlayCallManager] shortPassButton assigned ✓");

        if (longPassButton == null)
            Debug.LogError("[PlayCallManager] ERROR: longPassButton is not assigned in Inspector!");
        else
            Debug.Log("[PlayCallManager] longPassButton assigned ✓");

        if (confirmButton == null)
            Debug.LogError("[PlayCallManager] ERROR: confirmButton is not assigned in Inspector!");
        else
            Debug.Log("[PlayCallManager] confirmButton assigned ✓");

        if (runButton != null)
            runButton.onClick.AddListener(() => SelectPlay(PlayType.Run));
        if (shortPassButton != null)
            shortPassButton.onClick.AddListener(() => SelectPlay(PlayType.ShortPass));
        if (longPassButton != null)
            longPassButton.onClick.AddListener(() => SelectPlay(PlayType.LongPass));
        if (confirmButton != null)
            confirmButton.onClick.AddListener(ConfirmPlaySelection);

        Debug.Log("[PlayCallManager] Setup complete");
    }

    private void SelectPlay(PlayType play)
    {
        if (isPlayLocked) return; // Don't allow changing after confirmation

        selectedPlay = play;
        Debug.Log("Selected Play: " + play);
    }

    public void ShowPlayCallMenu()
    {
        // This method can be called by GameManager if needed
        ResetState();
    }

    public void SetEnhancerCard(Card card)
    {
        if (isPlayLocked) return;

        selectedEnhancer = card;
        Debug.Log("Selected Enhancer: " + card.card_id);
    }

    private void ConfirmPlaySelection()
    {
        if (selectedPlay == PlayType.Huddle)
        {
            Debug.LogWarning("No play selected!");
            return;
        }

        Debug.Log("[PlayCallManager] ConfirmPlaySelection() - Sending play selection to server");
        isPlayLocked = true;

        // Send choice to GameClient for syncing with opponent
        GameClient.Get().SendPlaySelection(selectedPlay, selectedEnhancer);
    }

    public void ResetState()
    {
        selectedPlay = PlayType.Huddle;
        selectedEnhancer = null;
        isPlayLocked = false;
        Debug.Log("[PlayCallManager] State reset for new play call");
    }
}
