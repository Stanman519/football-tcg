using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;

public enum GameState
{
    TitleScreen,
    PlayCall,
    SlotSpin,
    Resolution,
    EndOfPlay,
    Halftime,
    GameOver
}

public class GameManager : MonoBehaviour
{
    // Singleton reference (if you choose to do so)
    public static GameManager Instance;

    // Current game state
    [SerializeField] private GameState currentGameState = GameState.TitleScreen;

    // References to other managers
    [Header("Core Managers")]
/*    public DeckManager deckManagerOffense;
    public DeckManager deckManagerDefense;
    public FieldManager fieldManager;
    public SlotMachineManager slotMachineManager;*/
    public PlayCallUIScript playcallUIManager;

    // Example: track downs, halves, possessions
    private int currentDown = 1;
    private int half = 1;
    private int teamOnOffense = 1;
    private string offensivePlayCall = "";
    private string defensiveCoverage = "";
    public TextMeshProUGUI displayPlayCall;

    private void Awake()
    {
        // Simple Singleton pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Optionally preserve this between scene loads
        DontDestroyOnLoad(gameObject);

        // Initialize other systems if needed
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // You might begin with a TitleScreen or directly start the game
        TransitionToState(GameState.PlayCall);

    }


    /// <summary>
    /// Call this to transition from one GameState to another.
    /// </summary>
    public void TransitionToState(GameState newState)
    {
        currentGameState = newState;
        switch (newState)
        {
            case GameState.TitleScreen:
                HandleTitleScreen();
                break;
            case GameState.PlayCall:
                HandlePlayCall();
                break;
            case GameState.SlotSpin:
                HandleSlotSpin();
                break;
            case GameState.Resolution:
                StartCoroutine(HandleResolution());
                break;
            case GameState.EndOfPlay:
                HandleEndOfPlay();
                break;
            case GameState.Halftime:
                HandleHalftime();
                break;
            case GameState.GameOver:
                HandleGameOver();
                break;
        }
    }

    private void HandleTitleScreen()
    {
        // Show UI for title or let player press start
        
    }

    public void OnOffensePlaySelected(string selection)
    {
        offensivePlayCall = selection;
        displayPlayCall.text = selection;
    }

    private void HandlePlayCall()
    {
        // Offense chooses run/short pass/long pass
        // Defense chooses coverage
        // Possibly place "enhancer" cards
        playcallUIManager.ShowPlayCallUI();

        // The UI calls back when the selection is made
        // Then we go to slot spin:
        // TransitionToState(GameState.SlotSpin);
    }

    private void HandleSlotSpin()
    {
        // Let the slotMachineManager do the spin
        //slotMachineManager.SpinSlots();

        // When spin is done, slotMachineManager calls OnSlotsResult
    }

    // Called from SlotMachineManager when spin is complete
    public void OnSlotsResult()
    {
        // Possibly store spin result, then proceed to resolution
        TransitionToState(GameState.Resolution);
    }

    private IEnumerator HandleResolution()
    {
        // Step 1: Evaluate cards that trigger after slot spin
        // Step 2: Check coverage correctness, yardage modifications, fumbles, etc.
        // Step 3: Move the ball on FieldManager
        // Step 4: Check if there's a turnover or if next down is set

        yield return null; // do your logic, possibly yield between steps if you want animations

        // Once resolution is done, transition to EndOfPlay
        TransitionToState(GameState.EndOfPlay);
    }

    private void HandleEndOfPlay()
    {
        // Update the down, check if it's 4th down over or next down, etc.
        // Possibly check if half ended or if there's a turnover to switch offense/defense

        // If half ended:
        //    TransitionToState(GameState.Halftime);
        // else 
        //    TransitionToState(GameState.PlayCall);
    }

    private void HandleHalftime()
    {
        // Maybe some UI or stats screens
        // Then resume the second half
        // half = 2;
        // TransitionToState(GameState.PlayCall);
    }

    private void HandleGameOver()
    {
        // Show final score, etc.
        /*uiManager.ShowGameOver();*/
    }
}
