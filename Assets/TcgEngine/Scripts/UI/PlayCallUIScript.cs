using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using TcgEngine.Client;
using TcgEngine;
using TcgEngine.Gameplay;
using DG.Tweening;

public class PlayCallUIScript : MonoBehaviour
{
    [Header("Panel Root")]
    public GameObject playCallPanel;

    [Header("Play Buttons")]
    public Button runButton;
    public Button shortPassButton;
    public Button longPassButton;
    public Image runButtonImage;
    public Image shortPassButtonImage;
    public Image longPassButtonImage;

    [Header("Confirm")]
    public Button confirmButton;
    public Image confirmButtonImage;

    [Header("Timer")]
    public TextMeshProUGUI timerText;

    [Header("Header")]
    public TextMeshProUGUI headerText;

    [Header("Enhancer")]
    public PlayEnhancerSlot enhancerSlot;
    public TextMeshProUGUI enhancerCardName;
    public GameObject enhancerClearButton;

    [Header("Coach Cards")]
    public CoachCardUI myCoachPanel;
    public CoachCardUI opponentCoachPanel;

    [Header("Offense Yardage Totals")]
    public TextMeshProUGUI runYardageLabel;
    public TextMeshProUGUI shortYardageLabel;
    public TextMeshProUGUI longYardageLabel;

    [Header("Defense Scouting")]
    public TextMeshProUGUI[] scoutingDefLabels;
    public TextMeshProUGUI[] scoutingDefValues;

    [Header("Defense Coverage Totals")]
    public TextMeshProUGUI runDefenseText;
    public TextMeshProUGUI shortDefenseText;
    public TextMeshProUGUI longDefenseText;

    [Header("Incomplete Conditions")]
    public TextMeshProUGUI[] incompleteTexts;

    [Header("Formation Previews")]
    public FormationPreviewUI runFormationPreview;
    public FormationPreviewUI shortFormationPreview;
    public FormationPreviewUI longFormationPreview;

    [Header("Button Sprites (optional)")]
    public Sprite btnNormal;
    public Sprite btnSelected;
    public Sprite btnDisabled;

    // Figma-exact colors
    static readonly Color C_NORMAL    = new Color(0.102f, 0.102f, 0.102f, 1f); // #1A1A1A
    static readonly Color C_SELECTED  = new Color(1f, 0.549f, 0f, 1f);         // #FF8C00
    static readonly Color C_DISABLED  = new Color(0.20f, 0.20f, 0.20f, 0.5f);
    static readonly Color C_CONFIRM   = new Color(0.667f, 1f, 0.863f, 1f);     // #AAFFDC
    static readonly Color C_CONFIRM_OFF = new Color(0.667f, 1f, 0.863f, 0.3f);

    private PlayType selectedPlay = PlayType.Huddle;
    private Card selectedEnhancer = null;
    private bool isPlayLocked = false;
    private bool lastShouldShowPanel = false;
    private GamePhase lastKnownPhase = GamePhase.None;
    private float phaseTimer = 0f;
    private bool timerActive = false;

    const float PHASE_DURATION = 15f;

    void Start()
    {
        if (playCallPanel != null)
            playCallPanel.SetActive(false);

        if (runButton != null)
            runButton.onClick.AddListener(() => SelectPlay(PlayType.Run));
        if (shortPassButton != null)
            shortPassButton.onClick.AddListener(() => SelectPlay(PlayType.ShortPass));
        if (longPassButton != null)
            longPassButton.onClick.AddListener(() => SelectPlay(PlayType.LongPass));
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(ConfirmPlaySelection);

            // Ensure confirm button and ancestors are on UI layer so raycasts hit it
            SetLayerRecursive(confirmButton.gameObject, LayerMask.NameToLayer("UI"));

            // Add EventTrigger as fallback in case Button.onClick doesn't fire
            var trigger = confirmButton.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = confirmButton.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry();
            entry.eventID = EventTriggerType.PointerClick;
            entry.callback.AddListener((_) => ConfirmPlaySelection());
            trigger.triggers.Add(entry);
        }

        if (enhancerClearButton != null)
        {
            var btn = enhancerClearButton.GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(ClearEnhancer);
        }

        if (GameClient.Get() != null)
            GameClient.Get().onRefreshAll += OnGameDataRefreshed;
    }

    void Update()
    {
        Game gameData = GameClient.Get()?.GetGameData();
        if (gameData == null) return;

        Player currentPlayer = GameClient.Get().GetPlayer();
        bool playerHasSelected = currentPlayer != null && currentPlayer.SelectedPlay != PlayType.Huddle;
        bool shouldShowPanel = (gameData.phase == GamePhase.ChoosePlay)
                                && !playerHasSelected
                                && !isPlayLocked;

        if (shouldShowPanel != lastShouldShowPanel)
            lastShouldShowPanel = shouldShowPanel;

        if (shouldShowPanel && !playCallPanel.activeSelf && !isPlayLocked)
            ShowPlayCallUI();
        else if (!shouldShowPanel && playCallPanel.activeSelf && !isPlayLocked)
        {
            playCallPanel.SetActive(false);
            SetDropZoneRaycast(true);
        }

        // Timer countdown
        if (timerActive && playCallPanel.activeSelf)
        {
            phaseTimer -= Time.deltaTime;
            if (phaseTimer <= 0f)
            {
                phaseTimer = 0f;
                timerActive = false;
                AutoConfirm();
            }
            RefreshTimerDisplay();
        }
    }

    private void OnGameDataRefreshed()
    {
        Game gameData = GameClient.Get()?.GetGameData();
        if (gameData == null) return;

        if (gameData.phase == GamePhase.ChoosePlay && lastKnownPhase != GamePhase.ChoosePlay)
        {
            isPlayLocked = false;
            selectedPlay = PlayType.Huddle;
            selectedEnhancer = null;
        }
        lastKnownPhase = gameData.phase;
    }

    public void ShowPlayCallUI()
    {
        if (isPlayLocked) return;

        SetDropZoneRaycast(false);
        playCallPanel.SetActive(true);
        selectedPlay = PlayType.Huddle;
        selectedEnhancer = null;
        enhancerSlot?.Clear();

        // Reset timer
        phaseTimer = PHASE_DURATION;
        timerActive = true;

        RefreshCoachPanels();
        RefreshYardageTotals();
        RefreshScoutingHeader();
        RefreshIncompleteText();
        RefreshButtonStates();
        RefreshConfirmButton();
        RefreshFormationPreviews();
        RefreshEnhancerDisplay();
    }

    // ── Play Selection ──────────────────────────────────────────────────

    private void SelectPlay(PlayType play)
    {
        if (isPlayLocked) return;

        // Check enhancer compatibility
        if (selectedEnhancer != null && !IsPlayCompatibleWithEnhancer(play))
        {
            ClearEnhancer();
        }

        selectedPlay = play;
        RefreshButtonStates();
        RefreshConfirmButton();
    }

    private void RefreshButtonStates()
    {
        SetButtonState(runButton, runButtonImage, PlayType.Run);
        SetButtonState(shortPassButton, shortPassButtonImage, PlayType.ShortPass);
        SetButtonState(longPassButton, longPassButtonImage, PlayType.LongPass);
    }

    private void SetButtonState(Button btn, Image img, PlayType playType)
    {
        if (btn == null) return;

        bool isDisabled = selectedEnhancer != null && !IsPlayCompatibleWithEnhancer(playType);
        bool isSelected = (selectedPlay == playType);

        btn.interactable = !isDisabled;

        if (img != null)
        {
            if (btnNormal != null && btnSelected != null && btnDisabled != null)
            {
                img.sprite = isDisabled ? btnDisabled : isSelected ? btnSelected : btnNormal;
            }
            else
            {
                img.color = isDisabled ? C_DISABLED : isSelected ? C_SELECTED : C_NORMAL;
            }
        }
    }

    private void RefreshConfirmButton()
    {
        bool canConfirm = selectedPlay != PlayType.Huddle;
        if (confirmButton != null)
            confirmButton.interactable = canConfirm;
        if (confirmButtonImage != null)
            confirmButtonImage.color = canConfirm ? C_CONFIRM : C_CONFIRM_OFF;
    }

    // ── Confirm / Auto-confirm ──────────────────────────────────────────

    private void ConfirmPlaySelection()
    {
        if (selectedPlay == PlayType.Huddle) return;

        isPlayLocked = true;
        timerActive = false;
        playCallPanel.SetActive(false);
        SetDropZoneRaycast(true);
        GameClient.Get().SendPlaySelection(selectedPlay, selectedEnhancer);
    }

    private void AutoConfirm()
    {
        if (selectedPlay == PlayType.Huddle)
        {
            // Pick random play
            PlayType[] options = { PlayType.Run, PlayType.ShortPass, PlayType.LongPass };
            // Filter by enhancer compatibility
            var valid = new List<PlayType>();
            foreach (var p in options)
                if (selectedEnhancer == null || IsPlayCompatibleWithEnhancer(p))
                    valid.Add(p);
            selectedPlay = valid.Count > 0 ? valid[Random.Range(0, valid.Count)] : PlayType.Run;
        }
        ConfirmPlaySelection();
    }

    // ── Timer ───────────────────────────────────────────────────────────

    private void RefreshTimerDisplay()
    {
        if (timerText == null) return;
        int secs = Mathf.CeilToInt(phaseTimer);
        timerText.text = $"{secs}S";
        // Figma: mint green normally, red when low
        timerText.color = secs <= 5 ? new Color(1f, 0.443f, 0.424f, 1f) : new Color(0.667f, 1f, 0.863f, 1f);
    }

    // ── Enhancer ────────────────────────────────────────────────────────

    public void SetEnhancerCard(Card card)
    {
        if (isPlayLocked) return;
        if (card == null) return;

        // If a play is already selected and incompatible, reject the enhancer
        if (selectedPlay != PlayType.Huddle && card.CardData != null
            && card.CardData.required_plays != null && card.CardData.required_plays.Length > 0)
        {
            bool compatible = false;
            foreach (var p in card.CardData.required_plays)
                if (p == selectedPlay) { compatible = true; break; }
            if (!compatible)
            {
                // Reject — card bounces back to hand
                enhancerSlot?.Clear();
                return;
            }
        }

        selectedEnhancer = card;
        RefreshButtonStates();
        RefreshEnhancerDisplay();
    }

    public void ClearEnhancer()
    {
        selectedEnhancer = null;
        enhancerSlot?.Clear();
        RefreshButtonStates();
        RefreshEnhancerDisplay();
    }

    private bool IsPlayCompatibleWithEnhancer(PlayType play)
    {
        if (selectedEnhancer == null) return true;
        var req = selectedEnhancer.CardData?.required_plays;
        if (req == null || req.Length == 0) return true;
        foreach (var p in req)
            if (p == play) return true;
        return false;
    }

    private void RefreshEnhancerDisplay()
    {
        if (enhancerCardName != null)
        {
            if (selectedEnhancer != null && selectedEnhancer.CardData != null)
                enhancerCardName.text = selectedEnhancer.CardData.title;
            else
                enhancerCardName.text = "";
        }

        if (enhancerClearButton != null)
            enhancerClearButton.SetActive(selectedEnhancer != null);
    }

    // ── Coach Panels ────────────────────────────────────────────────────

    private void RefreshCoachPanels()
    {
        Game g = GameClient.Get()?.GetGameData();
        if (g == null) return;
        Player me  = GameClient.Get().GetPlayer();
        Player opp = GameClient.Get().GetOpponentPlayer();
        if (me == null || opp == null) return;
        bool amIOffense = (g.current_offensive_player != null
                           && g.current_offensive_player.player_id == me.player_id);
        myCoachPanel?.SetCoachFromPlayer(me, amIOffense);
        opponentCoachPanel?.SetCoachFromPlayer(opp, !amIOffense);
    }

    // ── Yardage / Coverage Totals ───────────────────────────────────────

    private void RefreshYardageTotals()
    {
        Game g = GameClient.Get()?.GetGameData();
        if (g == null) return;
        Player me  = GameClient.Get().GetPlayer();
        Player opp = GameClient.Get().GetOpponentPlayer();
        if (me == null || opp == null) return;

        bool amIOffense = (g.current_offensive_player != null
                           && g.current_offensive_player.player_id == me.player_id);

        Player offPlayer = amIOffense ? me : opp;
        Player defPlayer = amIOffense ? opp : me;

        // Offense totals: coach base + board cards
        CoachCardData offCoach = LoadCoach(offPlayer);
        CoachCardData defCoach = LoadCoach(defPlayer);

        SetYardageText(runYardageLabel, "RUN",
            (offCoach != null ? offCoach.GetOffenseYardage(PlayType.Run) : 0)
            + offPlayer.GetBoardCardsBaseYardageForPlayType(PlayType.Run, true));

        SetYardageText(shortYardageLabel, "SHORT",
            (offCoach != null ? offCoach.GetOffenseYardage(PlayType.ShortPass) : 0)
            + offPlayer.GetBoardCardsBaseYardageForPlayType(PlayType.ShortPass, true));

        SetYardageText(longYardageLabel, "LONG",
            (offCoach != null ? offCoach.GetOffenseYardage(PlayType.LongPass) : 0)
            + offPlayer.GetBoardCardsBaseYardageForPlayType(PlayType.LongPass, true));

        // Defense totals
        SetDefenseText(runDefenseText, "RUN",
            (defCoach != null ? defCoach.GetDefenseYardage(PlayType.Run) : 0)
            + defPlayer.GetBoardCardsBaseYardageForPlayType(PlayType.Run, false));

        SetDefenseText(shortDefenseText, "SHORT",
            (defCoach != null ? defCoach.GetDefenseYardage(PlayType.ShortPass) : 0)
            + defPlayer.GetBoardCardsBaseYardageForPlayType(PlayType.ShortPass, false));

        SetDefenseText(longDefenseText, "LONG",
            (defCoach != null ? defCoach.GetDefenseYardage(PlayType.LongPass) : 0)
            + defPlayer.GetBoardCardsBaseYardageForPlayType(PlayType.LongPass, false));
    }

    private void SetYardageText(TextMeshProUGUI field, string label, int val)
    {
        if (field != null)
            field.text = $"+{val} yds";
    }

    private void SetDefenseText(TextMeshProUGUI field, string label, int val)
    {
        if (field != null)
            field.text = $"-{val}";
    }

    private CoachCardData LoadCoach(Player p)
    {
        if (p == null || string.IsNullOrEmpty(p.coach_card_id)) return null;
        return Resources.Load<CoachCardData>("Coaches/" + p.coach_card_id);
    }

    // ── Formation Previews ──────────────────────────────────────────────

    private void RefreshFormationPreviews()
    {
        Game g = GameClient.Get()?.GetGameData();
        if (g == null) return;
        Player me = GameClient.Get().GetPlayer();
        if (me == null) return;

        bool amIOffense = (g.current_offensive_player != null
                           && g.current_offensive_player.player_id == me.player_id);

        CoachCardData coach = LoadCoach(amIOffense ? me : GameClient.Get().GetOpponentPlayer());
        if (coach == null) return;

        if (amIOffense)
        {
            SetFormation(runFormationPreview, coach.GetOffenseFormation(PlayType.Run), PlayType.Run, false);
            SetFormation(shortFormationPreview, coach.GetOffenseFormation(PlayType.ShortPass), PlayType.ShortPass, false);
            SetFormation(longFormationPreview, coach.GetOffenseFormation(PlayType.LongPass), PlayType.LongPass, false);
        }
        else
        {
            SetFormation(runFormationPreview, coach.GetDefenseFormation(PlayType.Run), PlayType.Run, true);
            SetFormation(shortFormationPreview, coach.GetDefenseFormation(PlayType.ShortPass), PlayType.ShortPass, true);
            SetFormation(longFormationPreview, coach.GetDefenseFormation(PlayType.LongPass), PlayType.LongPass, true);
        }
    }

    private void SetFormation(FormationPreviewUI preview, FormationData formation, PlayType playType, bool isDefense)
    {
        if (preview == null || formation == null) return;

        Game g = GameClient.Get()?.GetGameData();
        Player me = GameClient.Get().GetPlayer();
        bool amIOffense = (g?.current_offensive_player != null
                           && g.current_offensive_player.player_id == me.player_id);
        CoachCardData coach = LoadCoach(me);

        preview.SetFormation(formation, playType, isDefense, coach);
    }

    // ── Scouting Header ──────────────────────────────────────────────────

    private void RefreshScoutingHeader()
    {
        Game g = GameClient.Get()?.GetGameData();
        if (g == null) return;
        Player me = GameClient.Get().GetPlayer();
        Player opp = GameClient.Get().GetOpponentPlayer();
        if (me == null || opp == null) return;

        bool amIOffense = g.current_offensive_player != null
                          && g.current_offensive_player.player_id == me.player_id;
        Player defPlayer = amIOffense ? opp : me;

        // Coach-only defense coverage for scouting
        CoachCardData defCoach = LoadCoach(defPlayer);
        int runDef = defCoach != null ? defCoach.GetDefenseYardage(PlayType.Run) : 0;
        int shortDef = defCoach != null ? defCoach.GetDefenseYardage(PlayType.ShortPass) : 0;
        int longDef = defCoach != null ? defCoach.GetDefenseYardage(PlayType.LongPass) : 0;

        string[] labels = { "RUN DEFENSE", "SHORT DEFENSE", "LONG DEFENSE" };
        int[] values = { runDef, shortDef, longDef };

        if (scoutingDefLabels != null)
            for (int i = 0; i < scoutingDefLabels.Length && i < labels.Length; i++)
                if (scoutingDefLabels[i] != null) scoutingDefLabels[i].text = labels[i];

        if (scoutingDefValues != null)
            for (int i = 0; i < scoutingDefValues.Length && i < values.Length; i++)
                if (scoutingDefValues[i] != null) scoutingDefValues[i].text = values[i].ToString();
    }

    // ── Incomplete Text ──────────────────────────────────────────────────

    private void RefreshIncompleteText()
    {
        // Placeholder: show replacement-QB rules
        // Index 0=Run (no incomplete), 1=ShortPass, 2=LongPass
        string[] texts = {
            "",                                    // Run: no incomplete
            "1+ Wrench: Incomplete",               // Short pass
            "1+ Wrench OR no Football: Incomplete" // Long pass
        };

        if (incompleteTexts != null)
            for (int i = 0; i < incompleteTexts.Length && i < texts.Length; i++)
                if (incompleteTexts[i] != null) incompleteTexts[i].text = texts[i];
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

    private void SetDropZoneRaycast(bool enabled)
    {
        var dropZone = FindFirstObjectByType<TcgEngine.UI.PlayCardDropZone>();
        if (dropZone == null) return;
        var img = dropZone.GetComponent<UnityEngine.UI.Image>();
        if (img != null) img.raycastTarget = enabled;
    }

    // ── Cleanup ─────────────────────────────────────────────────────────

    private void OnDestroy()
    {
        if (GameClient.Get() != null)
            GameClient.Get().onRefreshAll -= OnGameDataRefreshed;
    }
}
