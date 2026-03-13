using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TcgEngine;
using TcgEngine.Client;
using Assets.TcgEngine.Scripts.Gameplay;
using UnityEngine;

/// <summary>
/// Phase-based coordinator for play animations.
/// Populates each BoardSlot's movement queue incrementally as game phases progress.
/// Slots own their movement — this controller just decides WHEN to assign/start/resume them.
/// </summary>
public class PlayAnimationController : MonoBehaviour
{
    public static PlayAnimationController Instance { get; private set; }

    [Header("Default Route Templates (assign in Inspector)")]
    public RouteData defaultRoute_Slant;
    public RouteData defaultRoute_Out;
    public RouteData defaultRoute_Fly;
    public RouteData defaultRoute_Curl;
    public RouteData defaultRoute_Post;

    // ── State ───────────────────────────────────────────────
    private FieldSlotManager fieldSlotMgr;
    private SlotMachineUI slotMachineUI;
    private GamePhase lastPhase = GamePhase.None;
    private int lastTurn = -1;
    private bool routesAssigned;
    private Coroutine resolutionCoroutine;

    // ── Lifecycle ───────────────────────────────────────────

    void Awake()
    {
        Instance = this;
        fieldSlotMgr = GetComponent<FieldSlotManager>();
        slotMachineUI = FindFirstObjectByType<SlotMachineUI>();
    }

    void Start()
    {
        GameClient client = GameClient.Get();
        if (client != null)
            client.onRefreshAll += OnRefreshAll;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        GameClient client = GameClient.Get();
        if (client != null)
            client.onRefreshAll -= OnRefreshAll;
    }

    // ── Phase Detection ─────────────────────────────────────

    private void OnRefreshAll()
    {
        Game g = GameClient.Get()?.GetGameData();
        if (g == null) return;

        GamePhase phase = g.phase;
        bool phaseChanged = phase != lastPhase || g.turn_count != lastTurn;

        if (phaseChanged)
        {
            // Broadcast phase change to all slot queues
            BroadcastPhaseChange(phase);

            // Handle phase transitions
            if (phase == GamePhase.RevealPlayCalls)
                OnRevealPlayCalls(g);
            else if (phase == GamePhase.Resolution)
                OnResolutionStart(g);
            else if (phase == GamePhase.LiveBall)
                OnLiveBallStart(g);
            else if (phase == GamePhase.EndTurn)
                OnEndTurn(g);
            else if (phase == GamePhase.StartTurn)
                OnStartTurn(g);

            lastPhase = phase;
            lastTurn = g.turn_count;
        }
    }

    // ── Phase Handlers ──────────────────────────────────────

    /// <summary>
    /// Play calls revealed — assign post-snap routes from coach to each slot's queue.
    /// Routes won't execute yet (they start with WaitForSignal("snap")).
    /// </summary>
    private void OnRevealPlayCalls(Game g)
    {
        if (fieldSlotMgr == null) return;

        Player offPlayer = g.current_offensive_player;
        Player defPlayer = g.GetCurrentDefensivePlayer();
        PlayType playType = offPlayer?.SelectedPlay ?? PlayType.Huddle;
        if (playType == PlayType.Huddle) return;

        AssignCoachRoutes(offPlayer, playType, isOffense: true);
        // Defense gets routes based on their coverage guess
        PlayType defGuess = defPlayer?.SelectedPlay ?? PlayType.Run;
        AssignCoachRoutes(defPlayer, defGuess, isOffense: false);

        routesAssigned = true;
    }

    /// <summary>
    /// Resolution phase — orchestrate the full play animation sequence with breathing room.
    /// </summary>
    private void OnResolutionStart(Game g)
    {
        if (fieldSlotMgr == null) return;

        fieldSlotMgr.animationLocked = true;

        // Lock slot machine layout so it doesn't auto-minimize
        if (slotMachineUI != null)
            slotMachineUI.LockLayout();

        // Start all queues (they'll pause at WaitForSignal("snap"))
        StartAllQueues();

        if (resolutionCoroutine != null)
            StopCoroutine(resolutionCoroutine);
        resolutionCoroutine = StartCoroutine(ResolutionSequence(g));
    }

    private IEnumerator ResolutionSequence(Game g)
    {
        // 1. Let the scene settle after phase change
        yield return new WaitForSeconds(0.5f);

        // 2. Animate slot machine to mini position
        if (slotMachineUI != null)
            slotMachineUI.MinimizeAnimated(0.5f);
        yield return new WaitForSeconds(0.7f);

        // FUTURE: QB cadence hook ("Ready... Set... Hut!")

        // 3. Snap the ball — routes begin
        BroadcastSignal("snap");

        // Dynamic route wait: computed from longest assigned route
        float routeWait = ComputeLongestRouteTime();
        yield return new WaitForSeconds(routeWait);

        // 4. Beat before yardage
        yield return new WaitForSeconds(0.3f);

        // 5. Ball carrier yardage movement
        Player offPlayer = g.current_offensive_player;
        Player defPlayer = g.GetCurrentDefensivePlayer();
        int yardage = g.yardage_this_play;

        float yardageDur = PlayerSpeed.Duration(Mathf.Abs(yardage), SpeedTier.Sprint);
        BoardSlot carrierSlot = FindSlotForCardUid(offPlayer, g.ball_carrier_uid);
        if (carrierSlot != null && yardage != 0)
        {
            carrierSlot.MoveQueue.Enqueue(SlotMovementSegment.MoveBy(
                new Vector2(0, yardage),
                SpeedTier.Sprint,
                "yardage"
            ));
        }
        yield return new WaitForSeconds(yardageDur);

        // 6. Beat before pursuit
        yield return new WaitForSeconds(0.3f);

        // 7. Defense tracks toward ball carrier
        float pursuitDur = 0f;
        if (carrierSlot != null)
            pursuitDur = EnqueueDefenseTracking(defPlayer, carrierSlot);
        yield return new WaitForSeconds(pursuitDur);

        // FUTURE: Announcer hook ("Complete pass to WR1!")

        resolutionCoroutine = null;
    }

    private void OnLiveBallStart(Game g)
    {
        // Queues that had WaitForPhase(LiveBall) will auto-resume via BroadcastPhaseChange
        // Players can now play live ball cards that modify slot queues via AppendToSlot
    }

    private void OnEndTurn(Game g)
    {
        ClearAllQueues();
        if (fieldSlotMgr != null)
            fieldSlotMgr.animationLocked = false;
        routesAssigned = false;
    }

    private void OnStartTurn(Game g)
    {
        ClearAllQueues();
        if (fieldSlotMgr != null)
            fieldSlotMgr.animationLocked = false;
        routesAssigned = false;
    }

    // ── Route Assignment ────────────────────────────────────

    private void AssignCoachRoutes(Player player, PlayType playType, bool isOffense)
    {
        if (player?.head_coach == null || fieldSlotMgr == null) return;

        // Play enhancer route override replaces ALL coach routes for this play
        RouteData enhancerOverride = player.PlayEnhancer?.Data?.routeOverride;

        var routeDict = isOffense ? player.head_coach.offenseRoutes : player.head_coach.defenseRoutes;

        // Get all position groups for this player
        var allSlots = BoardSlot.GetAll().Where(s => s.player_id == player.player_id).ToList();

        int fallbackIndex = 0;
        foreach (BoardSlot slot in allSlots)
        {
            slot.MoveQueue.Clear();

            // Priority 1: play enhancer overrides the whole scheme
            RouteData route = enhancerOverride;

            // Priority 2: coach route for this play type + position + slot
            if (route == null)
            {
                if (routeDict != null && routeDict.TryGetValue(playType, out var posDict))
                    posDict.TryGetValue((slot.player_position_type, slot.slotIndex), out route);
            }

            // Priority 3: card's personal receiverRoute
            if (route == null)
            {
                Game g = GameClient.Get()?.GetGameData();
                Card card = FindCardInSlot(g, player, slot);
                if (card?.Data?.receiverRoute != null)
                    route = card.Data.receiverRoute;
            }

            // Fallback: default route templates (for receivers only)
            if (route == null && isOffense &&
                (slot.player_position_type == PlayerPositionGrp.WR ||
                 slot.player_position_type == PlayerPositionGrp.RB_TE))
            {
                route = GetDefaultRoute(fallbackIndex++);
            }

            if (route == null) continue;

            // Convert route to segments, prepend snap wait
            var segments = RouteConverter.ToSegments(route, slot, "coach");
            if (segments.Count == 0) continue;

            slot.MoveQueue.Enqueue(SlotMovementSegment.WaitForSignal("snap"));
            slot.MoveQueue.EnqueueRange(segments);
        }
    }

    private RouteData GetDefaultRoute(int index)
    {
        RouteData[] defaults = { defaultRoute_Slant, defaultRoute_Out, defaultRoute_Fly, defaultRoute_Curl, defaultRoute_Post };
        var available = defaults.Where(r => r != null).ToArray();
        if (available.Length == 0) return null;
        return available[index % available.Length];
    }

    // ── Defense Tracking ────────────────────────────────────

    /// <summary>Enqueue defense pursuit and return max pursuit duration for coroutine wait.</summary>
    private float EnqueueDefenseTracking(Player defPlayer, BoardSlot targetSlot)
    {
        if (fieldSlotMgr == null || targetSlot == null) return 0f;

        var defGroups = new[] { PlayerPositionGrp.DL, PlayerPositionGrp.LB, PlayerPositionGrp.DB };
        Vector3 carrierLocal = targetSlot.transform.localPosition;
        float maxDur = 0f;

        foreach (var grp in defGroups)
        {
            var slots = fieldSlotMgr.GetSlotsForPosition(grp, defPlayer.player_id);
            foreach (BoardSlot slot in slots)
            {
                Vector3 current = slot.transform.localPosition;
                Vector3 trackPos = Vector3.Lerp(current, carrierLocal, 0.6f);
                float dist = Vector3.Distance(current, trackPos);
                float dur = PlayerSpeed.Duration(dist, SpeedTier.Sprint);
                if (dur > maxDur) maxDur = dur;

                float fieldWidth = fieldSlotMgr.fieldWidth;
                Vector2 footballTarget = new Vector2(trackPos.x / fieldWidth, trackPos.y);

                slot.MoveQueue.Enqueue(SlotMovementSegment.MoveTo(
                    footballTarget,
                    SpeedTier.Sprint,
                    "pursuit"
                ));
            }
        }
        return maxDur;
    }

    // ── Public API for Abilities/Effects ─────────────────────

    /// <summary>Append a movement segment to a specific slot's queue.</summary>
    public void AppendToSlot(BoardSlot slot, SlotMovementSegment segment)
    {
        if (slot == null) return;
        slot.MoveQueue.Enqueue(segment);
    }

    /// <summary>Replace all remaining segments for a slot (e.g., interception reroute).</summary>
    public void ReplaceRemainingForSlot(BoardSlot slot, List<SlotMovementSegment> newSegments)
    {
        if (slot == null) return;
        slot.MoveQueue.ReplaceFromCurrent(newSegments);
    }

    /// <summary>Modify the ball carrier's yardage segment by adding extra yards.</summary>
    public void ModifyBallCarrierYardage(int additionalYards)
    {
        Game g = GameClient.Get()?.GetGameData();
        if (g == null) return;

        BoardSlot carrier = FindSlotForCardUid(g.current_offensive_player, g.ball_carrier_uid);
        if (carrier == null) return;

        if (!carrier.MoveQueue.ModifyLastMoveBy("yardage", new Vector2(0, additionalYards)))
        {
            carrier.MoveQueue.Enqueue(SlotMovementSegment.MoveBy(
                new Vector2(0, additionalYards),
                SpeedTier.Sprint,
                "yardage"
            ));
        }
    }

    /// <summary>Broadcast a named signal to all slot queues.</summary>
    public void BroadcastSignal(string signal)
    {
        foreach (BoardSlot slot in BoardSlot.GetAll())
            slot.MoveQueue.OnSignal(signal);
    }

    // ── Queue Management ────────────────────────────────────

    private void StartAllQueues()
    {
        foreach (BoardSlot slot in BoardSlot.GetAll())
        {
            if (!slot.MoveQueue.IsIdle) continue;
            slot.MoveQueue.Start();
        }
    }

    private void ClearAllQueues()
    {
        foreach (BoardSlot slot in BoardSlot.GetAll())
            slot.MoveQueue.Clear();
    }

    private void BroadcastPhaseChange(GamePhase phase)
    {
        foreach (BoardSlot slot in BoardSlot.GetAll())
            slot.MoveQueue.OnPhaseChanged(phase);
    }

    // ── Slot Lookup ─────────────────────────────────────────

    private BoardSlot FindSlotForCardUid(Player player, string uid)
    {
        if (string.IsNullOrEmpty(uid) || fieldSlotMgr == null || player == null) return null;

        Card card = player.cards_board.FirstOrDefault(c => c.uid == uid);
        if (card == null) return null;

        var slots = fieldSlotMgr.GetSlotsForPosition(card.Data.playerPosition, player.player_id);
        foreach (BoardSlot bs in slots)
        {
            if (bs.assignedSlot == card.slot)
                return bs;
        }
        return slots.Count > 0 ? slots[0] : null;
    }

    private Card FindCardInSlot(Game g, Player player, BoardSlot slot)
    {
        if (g == null || player == null || slot == null) return null;
        var cards = g.GetSlotCards(slot.assignedSlot);
        return cards != null && cards.Count > 0 ? cards[0] : null;
    }

    /// <summary>Compute the longest route time across all assigned slot queues.</summary>
    private float ComputeLongestRouteTime()
    {
        float longest = 1.0f; // minimum 1s even with no routes
        foreach (BoardSlot slot in BoardSlot.GetAll())
        {
            if (slot.MoveQueue.IsIdle) continue;
            float total = slot.MoveQueue.EstimateTotalMoveDuration();
            if (total > longest) longest = total;
        }
        return longest;
    }
}
