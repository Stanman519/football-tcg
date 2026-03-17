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
/// Routes are generated PROCEDURALLY after yardage calculation (Resolution phase),
/// not during RevealPlayCalls. Formations (pre-snap positions) remain coach-driven.
///
/// Timing: ChoosePlay → Formations set → Slots spin → Yardage calculated
///   → Routes generated procedurally → Pre-snap beat → Snap → Play animates
/// </summary>
public class PlayAnimationController : MonoBehaviour
{
    public static PlayAnimationController Instance { get; private set; }

    [Header("Pre-Snap Timing")]
    [Tooltip("Seconds for the pre-snap 'hut hut' beat")]
    public float preSnapBeatDuration = 1.2f;
    [Tooltip("Seconds for audible animation when hot route triggers")]
    public float audibleDuration = 0.8f;

    // ── State ───────────────────────────────────────────────
    private FieldSlotManager fieldSlotMgr;
    private SlotMachineUI slotMachineUI;
    private GamePhase lastPhase = GamePhase.None;
    private int lastTurn = -1;
    private bool routesAssigned;
    private Coroutine resolutionCoroutine;

    // Route plans from procedural generation (kept for audible detection)
    private List<ProceduralRouteGenerator.SlotRoutePlan> currentRoutePlans;
    private bool hasAudible;

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
            BroadcastPhaseChange(phase);

            if (phase == GamePhase.Resolution)
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
    /// Resolution phase — yardage is calculated. Generate routes procedurally,
    /// then orchestrate the full play animation with pre-snap beat.
    /// </summary>
    private void OnResolutionStart(Game g)
    {
        if (fieldSlotMgr == null) return;

        fieldSlotMgr.animationLocked = true;

        if (slotMachineUI != null)
            slotMachineUI.LockLayout();

        // Generate procedural routes now that yardage is known
        Player offPlayer = g.current_offensive_player;
        PlayType playType = offPlayer?.SelectedPlay ?? PlayType.Huddle;
        if (playType != PlayType.Huddle)
        {
            float fw = fieldSlotMgr != null ? fieldSlotMgr.fieldWidth : 53.3f;
            currentRoutePlans = ProceduralRouteGenerator.PlanOffenseRoutes(g, offPlayer, playType, fw);
            hasAudible = currentRoutePlans.Any(p => p.route.wasHotRouted && p.isTargetReceiver);
            AssignProceduralRoutes(offPlayer, g);
        }

        // Start all queues (they'll pause at WaitForSignal("snap"))
        StartAllQueues();

        if (resolutionCoroutine != null)
            StopCoroutine(resolutionCoroutine);
        resolutionCoroutine = StartCoroutine(ResolutionSequence(g));
    }

    private IEnumerator ResolutionSequence(Game g)
    {
        // 1. Let the scene settle
        yield return new WaitForSeconds(0.5f);

        // 2. Minimize slot machine
        if (slotMachineUI != null)
            slotMachineUI.MinimizeAnimated(0.5f);
        yield return new WaitForSeconds(0.7f);

        // 3. Pre-snap beat ("hut hut")
        yield return new WaitForSeconds(preSnapBeatDuration);

        // 4. Audible — if a hot route was needed, brief visual beat
        if (hasAudible)
            yield return new WaitForSeconds(audibleDuration);

        // 5. Snap the ball — routes begin
        BroadcastSignal("snap");

        // Dynamic wait: computed from longest assigned route
        float routeWait = ComputeLongestRouteTime();
        yield return new WaitForSeconds(routeWait);

        // 6. Beat before yardage
        yield return new WaitForSeconds(0.3f);

        // 7. Ball carrier yardage movement (for run plays, route already includes yardage)
        Player offPlayer = g.current_offensive_player;
        Player defPlayer = g.GetCurrentDefensivePlayer();
        PlayType playType = offPlayer?.SelectedPlay ?? PlayType.Huddle;
        int yardage = g.yardage_this_play;
        bool isPass = (playType == PlayType.ShortPass || playType == PlayType.LongPass);

        // For pass plays, target receiver's route already includes net yardage via GenerateForTarget.
        // For run plays, the run route already includes net yardage via GenerateRunRoute.
        // Only add explicit yardage movement if no procedural route was assigned (fallback).
        if (!routesAssigned)
        {
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
        }

        // 8. Beat before pursuit
        yield return new WaitForSeconds(0.3f);

        // 9. Defense tracks toward ball carrier
        BoardSlot carrierForPursuit = FindSlotForCardUid(offPlayer, g.ball_carrier_uid ?? g.target_receiver_uid);
        float pursuitDur = 0f;
        if (carrierForPursuit != null)
            pursuitDur = EnqueueDefenseTracking(defPlayer, carrierForPursuit);
        yield return new WaitForSeconds(pursuitDur);

        resolutionCoroutine = null;
    }

    private void OnLiveBallStart(Game g)
    {
        // Queues with WaitForPhase(LiveBall) auto-resume via BroadcastPhaseChange
    }

    private void OnEndTurn(Game g)
    {
        ClearAllQueues();
        if (fieldSlotMgr != null)
            fieldSlotMgr.animationLocked = false;
        routesAssigned = false;
        currentRoutePlans = null;
        hasAudible = false;
    }

    private void OnStartTurn(Game g)
    {
        ClearAllQueues();
        if (fieldSlotMgr != null)
            fieldSlotMgr.animationLocked = false;
        routesAssigned = false;
        currentRoutePlans = null;
        hasAudible = false;
    }

    // ── Route Assignment (Library-first, procedural fallback) ──

    /// <summary>
    /// Assign routes to ALL offensive board slots.
    /// Ball carrier + target receiver use procedural routes (encode net yardage).
    /// Everyone else uses hand-authored RouteLibrary routes, with procedural fallback.
    /// </summary>
    private void AssignProceduralRoutes(Player offPlayer, Game g)
    {
        if (offPlayer == null || fieldSlotMgr == null) return;

        RouteLibrary.Initialize();

        PlayType playType = offPlayer.SelectedPlay;
        float fw = fieldSlotMgr.fieldWidth;
        string carrierUid = g.ball_carrier_uid;
        string targetUid = g.target_receiver_uid;
        bool isRun = (playType == PlayType.Run);

        // Track which slots got card-backed procedural routes (carrier/target)
        var assignedSlots = new HashSet<BoardSlot>();

        // Step 1: Assign procedural routes for ball carrier + target receiver
        if (currentRoutePlans != null)
        {
            foreach (var plan in currentRoutePlans)
            {
                // Only use procedural for carrier and target — they encode net yardage
                bool isCarrier = (plan.cardUid == carrierUid);
                bool isTarget = plan.isTargetReceiver;
                if (!isCarrier && !isTarget) continue;
                if (plan.route.segments == null || plan.route.segments.Count == 0) continue;

                BoardSlot slot = FindSlotForCardUid(offPlayer, plan.cardUid);
                if (slot == null) continue;

                slot.MoveQueue.Clear();
                slot.MoveQueue.Enqueue(SlotMovementSegment.WaitForSignal("snap"));
                slot.MoveQueue.EnqueueRange(plan.route.segments);
                assignedSlots.Add(slot);
            }
        }

        // Step 2: Iterate ALL offensive slots — library route for non-carrier/non-target
        var offGroups = new[] { PlayerPositionGrp.QB, PlayerPositionGrp.WR, PlayerPositionGrp.RB_TE, PlayerPositionGrp.OL };
        foreach (var grp in offGroups)
        {
            var slots = fieldSlotMgr.GetSlotsForPosition(grp, offPlayer.player_id);
            for (int si = 0; si < slots.Count; si++)
            {
                BoardSlot slot = slots[si];
                if (assignedSlots.Contains(slot)) continue;
                if (slot.MoveQueue.Count > 0) continue;

                // Determine if this slot has a TE card (for RB_TE group disambiguation)
                bool isTE = false;
                Card slotCard = FindCardInSlot(g, offPlayer, slot);
                if (slotCard != null && slotCard.CardData != null)
                    isTE = (slotCard.CardData.playerPosition == PlayerPositionGrp.RB_TE &&
                            slotCard.CardData.type == CardType.OffensivePlayer); // TE heuristic: later slots in RB_TE

                // Determine context and lateral direction
                RouteContext ctx = RouteLibrary.GetContext(grp, playType, isTE);
                int lateralDir = RouteLibrary.LateralFromSlot(slot, fw);
                int olSlot = (grp == PlayerPositionGrp.OL) ? si : -1;

                // Try library first
                RouteData libraryRoute = RouteLibrary.Pick(grp, olSlot, ctx, lateralDir);

                slot.MoveQueue.Enqueue(SlotMovementSegment.WaitForSignal("snap"));

                if (libraryRoute != null)
                {
                    var segments = RouteConverter.ToSegments(libraryRoute, slot, "library");
                    if (segments.Count > 0)
                    {
                        slot.MoveQueue.EnqueueRange(segments);
                        continue;
                    }
                }

                // Procedural fallback
                slot.MoveQueue.EnqueueRange(ProceduralFallback(grp, playType, fw).segments);
            }
        }

        routesAssigned = true;
    }

    /// <summary>
    /// Generate a procedural fallback route for a position group + play type.
    /// Used only when RouteLibrary has no matching asset.
    /// </summary>
    private static ProceduralRouteGenerator.GeneratedRoute ProceduralFallback(
        PlayerPositionGrp grp, PlayType playType, float fw)
    {
        bool isPass = (playType == PlayType.ShortPass || playType == PlayType.LongPass);

        if (isPass)
        {
            switch (grp)
            {
                case PlayerPositionGrp.QB:
                    return ProceduralRouteGenerator.Generate(RouteShape.DropBack, 7f, 0, fw);
                case PlayerPositionGrp.OL:
                    return ProceduralRouteGenerator.Generate(RouteShape.Block, 1f, 0, fw);
                case PlayerPositionGrp.WR:
                    RouteShape wrShape = RouteShapeData.PickRandom(PlayerPositionGrp.WR, playType) ?? RouteShape.Go;
                    return ProceduralRouteGenerator.Generate(wrShape, Random.Range(8f, 12f), 0, fw);
                default:
                    RouteShape rbShape = RouteShapeData.PickRandom(PlayerPositionGrp.RB_TE, playType) ?? RouteShape.Flat;
                    return ProceduralRouteGenerator.Generate(rbShape, Random.Range(4f, 6f), 0, fw);
            }
        }

        return ProceduralRouteGenerator.Generate(RouteShape.Block, 1f, 0, fw);
    }

    // ── Defense Tracking ────────────────────────────────────

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

    public void AppendToSlot(BoardSlot slot, SlotMovementSegment segment)
    {
        if (slot == null) return;
        slot.MoveQueue.Enqueue(segment);
    }

    public void ReplaceRemainingForSlot(BoardSlot slot, List<SlotMovementSegment> newSegments)
    {
        if (slot == null) return;
        slot.MoveQueue.ReplaceFromCurrent(newSegments);
    }

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

    public void BroadcastSignal(string signal)
    {
        foreach (BoardSlot slot in BoardSlot.GetAll())
            slot.MoveQueue.OnSignal(signal);
    }

    /// <summary>Get the current route plans (for external queries like audible UI).</summary>
    public List<ProceduralRouteGenerator.SlotRoutePlan> GetCurrentRoutePlans() => currentRoutePlans;

    /// <summary>Whether the current play has a hot-routed audible.</summary>
    public bool HasAudible => hasAudible;

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

    private float ComputeLongestRouteTime()
    {
        float longest = 1.0f;
        foreach (BoardSlot slot in BoardSlot.GetAll())
        {
            if (slot.MoveQueue.IsIdle) continue;
            float total = slot.MoveQueue.EstimateTotalMoveDuration();
            if (total > longest) longest = total;
        }
        return longest;
    }
}
