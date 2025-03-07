using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using TcgEngine;
using UnityEngine.Rendering.Universal;
using Assets.TcgEngine.Scripts.Gameplay;

public enum PlayType
{
    Huddle,
    Run,
    ShortPass,
    LongPass
}

public class PlayCallManager : MonoBehaviour
{
    public GameObject playCallPanel; // The main panel that pops up
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
        playCallPanel.SetActive(false);

        runButton.onClick.AddListener(() => SelectPlay(PlayType.Run));
        shortPassButton.onClick.AddListener(() => SelectPlay(PlayType.ShortPass));
        longPassButton.onClick.AddListener(() => SelectPlay(PlayType.LongPass));
        confirmButton.onClick.AddListener(ConfirmPlaySelection);
    }

    public void OpenPlayCallMenu()
    {
        playCallPanel.SetActive(true);
        isPlayLocked = false;
        selectedPlay = PlayType.Huddle; // Reset
        selectedEnhancer = null;
    }

    private void SelectPlay(PlayType play)
    {
        if (isPlayLocked) return; // Don't allow changing after confirmation

        selectedPlay = play;
        Debug.Log("Selected Play: " + play);
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

        isPlayLocked = true;
        playCallPanel.SetActive(false);

        // Send choice to GameClient for syncing with opponent
        GameClient.Get().SendPlaySelection(selectedPlay, selectedEnhancer);
    }
}
