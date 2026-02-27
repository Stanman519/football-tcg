using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using TcgEngine;
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
    [Header("Enhancer Display")]
    public Text enhancerDisplayText; // wired by PlayCallPanelBuilder

    // Flash color shown briefly on the selected button before the panel closes
    static readonly Color C_SELECTED = new Color(0.91f, 0.39f, 0.10f, 1f); // orange
    static readonly Color C_NORMAL   = new Color(0.13f, 0.22f, 0.34f, 1f); // steel blue

    private PlayType selectedPlay = PlayType.Huddle;
    private Card selectedEnhancer = null;
    private bool isPlayLocked = false;
    private bool lastShouldShowPanel = false;

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

        playCallPanel.SetActive(true);
        selectedPlay     = PlayType.Huddle;
        selectedEnhancer = null;
        enhancerSlot?.Clear();

        PlayCallManager manager = playCallPanel.GetComponent<PlayCallManager>();
        if (manager != null) manager.ResetState();
    }

    private void SelectPlay(PlayType play)
    {
        if (isPlayLocked) return;
        selectedPlay = play;
        StartCoroutine(FlashAndConfirm(play));
    }

    private IEnumerator FlashAndConfirm(PlayType play)
    {
        // Highlight the chosen button orange for a brief moment
        Button chosen = play == PlayType.Run       ? runButton
                      : play == PlayType.ShortPass ? shortPassButton
                      :                              longPassButton;

        if (chosen != null)
        {
            var img = chosen.GetComponent<Image>();
            if (img != null) img.color = C_SELECTED;
        }

        yield return new WaitForSeconds(0.22f);

        // Reset color before hiding (not strictly necessary but clean)
        if (chosen != null)
        {
            var img = chosen.GetComponent<Image>();
            if (img != null) img.color = C_NORMAL;
        }

        ConfirmPlaySelection();
    }

    public void SetEnhancerCard(Card card)
    {
        if (isPlayLocked) return;
        selectedEnhancer = card;
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
