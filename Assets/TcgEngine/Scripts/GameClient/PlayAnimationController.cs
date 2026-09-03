using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using TcgEngine;
using TcgEngine.Client;
using TcgEngine.Gameplay;
using TcgEngine.UI;
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
    public float preSnapBeatDuration = 2.4f;
    [Tooltip("Seconds for audible animation when hot route triggers")]
    public float audibleDuration = 1.5f;

    // ── State ───────────────────────────────────────────────
    private FieldSlotManager fieldSlotMgr;
    private SlotMachineUI slotMachineUI;
    private BoardStatOverlay boardStatOverlay;
    private AbilitySpotlight abilitySpotlight;
    private GamePhase lastPhase = GamePhase.None;
    private int lastTurn = -1;
    private bool routesAssigned;
    private Coroutine resolutionCoroutine;

    // Route plans from procedural generation (kept for audible detection)
    private List<ProceduralRouteGenerator.SlotRoutePlan> currentRoutePlans;
    private bool hasAudible;

    /// <summary>Scaled WaitForSeconds via TimeController (field animations only).</summary>
    private WaitForSeconds FieldWait(float seconds) =>
        TimeController.Instance != null ? TimeController.Instance.ScaledWait(seconds) : new WaitForSeconds(seconds);

    // ── Lifecycle ───────────────────────────────────────────

    void Awake()
    {
        Instance = this;
        fieldSlotMgr = GetComponent<FieldSlotManager>();
        slotMachineUI = FindFirstObjectByType<SlotMachineUI>();
        boardStatOverlay = FindFirstObjectByType<BoardStatOverlay>();
        abilitySpotlight = FindFirstObjectByType<AbilitySpotlight>();
    }

    void Start()
    {
        GameClient client = GameClient.Get();
        if (client != null)
        {
            client.onRefreshAll += OnRefreshAll;
            client.onLiveBallEvent += OnLiveBallEventReceived;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        GameClient client = GameClient.Get();
        if (client != null)
        {
            client.onRefreshAll -= OnRefreshAll;
            client.onLiveBallEvent -= OnLiveBallEventReceived;
        }
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
        var ball = BallManager.Instance;
        Player offPlayer = g.current_offensive_player;
        Player defPlayer = g.GetCurrentDefensivePlayer();
        PlayType playType = offPlayer?.SelectedPlay ?? PlayType.Huddle;
        int yardage = g.yardage_this_play;
        bool isPass = (playType == PlayType.ShortPass || playType == PlayType.LongPass);
        var failEvent = g.last_fail_event;

        // Find key slots
        var qbSlots = fieldSlotMgr?.GetSlotsForPosition(PlayerPositionGrp.QB, offPlayer.player_id);
        BoardSlot qbSlot = qbSlots != null && qbSlots.Count > 0 ? qbSlots[0] : null;
        BoardSlot carrierSlot = FindSlotForCardUid(offPlayer, g.ball_carrier_uid);
        BoardSlot receiverSlot = FindSlotForCardUid(offPlayer, g.target_receiver_uid);

        // 1. Let the scene settle
        yield return FieldWait(0.5f);

        // 2. Minimize slot machine
        if (slotMachineUI != null)
            slotMachineUI.MinimizeAnimated(0.5f);
        yield return FieldWait(0.7f);

        // 3. PHASE A — Sequential base stat contributions
        Player p0 = g.players != null && g.players.Length > 0 ? g.players[0] : null;
        Player p1 = g.players != null && g.players.Length > 1 ? g.players[1] : null;
        if (boardStatOverlay != null)
            yield return boardStatOverlay.ShowLabelsSequential(g, p0, p1);

        // [BALL] Snap to QB
        if (ball != null && qbSlot != null)
        {
            ball.SnapToQB(qbSlot, 0.3f);
            yield return FieldWait(0.35f);
        }

        // 4. Pre-snap beat ("hut hut") — ball visible at QB
        yield return FieldWait(preSnapBeatDuration);

        // 5. Audible — if a hot route was needed, show banner
        if (hasAudible)
        {
            var hotPlan = currentRoutePlans.FirstOrDefault(
                p => p.route.wasHotRouted && p.isTargetReceiver);
            string receiverName = "";
            if (!string.IsNullOrEmpty(hotPlan.cardUid))
            {
                Card card = offPlayer?.cards_board
                    .FirstOrDefault(c => c.uid == hotPlan.cardUid);
                receiverName = card?.CardData?.title ?? "";
            }
            AudibleCalloutUI.Get()?.Show(receiverName, hotPlan.route.shape, audibleDuration);
            yield return FieldWait(audibleDuration);
        }

        // 6. Snap the ball — routes begin
        BroadcastSignal("snap");

        // [BALL] Post-snap: branch on play type + fail event
        if (ball != null)
        {
            yield return StartCoroutine(AnimateBallForPlay(
                ball, g, playType, failEvent, yardage, isPass,
                qbSlot, carrierSlot, receiverSlot, offPlayer, defPlayer));
        }

        // 7. PHASE B — Ability bullet-time showcase (buffered during phase transition)
        if (abilitySpotlight != null && abilitySpotlight.HasPendingShowcases)
        {
            yield return FieldWait(0.3f);
            yield return abilitySpotlight.FlushAndProcess();
        }

        // Fade out base stat labels now that showcases are done
        if (boardStatOverlay != null)
            boardStatOverlay.StartFadeOut();

        // Dynamic wait: computed from longest assigned route
        float routeWait = ComputeLongestRouteTime();
        yield return FieldWait(routeWait);

        // Beat before yardage
        yield return FieldWait(0.3f);

        // Ball carrier yardage movement (fallback if no procedural route)
        if (!routesAssigned)
        {
            float yardageDur = PlayerSpeed.Duration(Mathf.Abs(yardage), SpeedTier.Sprint);
            if (carrierSlot != null && yardage != 0)
            {
                carrierSlot.MoveQueue.Enqueue(SlotMovementSegment.MoveBy(
                    new Vector2(0, yardage),
                    SpeedTier.Sprint,
                    "yardage"
                ));
            }
            yield return FieldWait(yardageDur);
        }

        // Beat before pursuit
        yield return FieldWait(0.3f);

        // Defense tracks toward ball carrier
        BoardSlot carrierForPursuit = FindSlotForCardUid(offPlayer, g.ball_carrier_uid ?? g.target_receiver_uid);
        float pursuitDur = 0f;
        if (carrierForPursuit != null)
            pursuitDur = EnqueueDefenseTracking(defPlayer, carrierForPursuit);
        yield return FieldWait(pursuitDur);

        // Post-play yardage summary
        var summaryPanel = YardageSummaryPanel.Get();
        if (summaryPanel != null)
        {
            summaryPanel.ShowSummary(g);
            yield return new WaitForSeconds(summaryPanel.fadeInDur + summaryPanel.holdDur + summaryPanel.fadeOutDur);
        }

        resolutionCoroutine = null;
    }

    /// <summary>
    /// Animates the football based on play type and fail event.
    /// Called right after the snap signal.
    /// </summary>
    private IEnumerator AnimateBallForPlay(
        BallManager ball, Game g, PlayType playType, FailPlayEventType failEvent,
        int yardage, bool isPass,
        BoardSlot qbSlot, BoardSlot carrierSlot, BoardSlot receiverSlot,
        Player offPlayer, Player defPlayer)
    {
        // ── SACK: ball stays with QB, QB gets dragged back ──
        if (failEvent == FailPlayEventType.Sack)
        {
            ball.AttachTo(qbSlot);
            yield break;
        }

        // ── RUN PLAYS ──
        if (!isPass)
        {
            // Handoff to ball carrier
            if (carrierSlot != null && carrierSlot != qbSlot)
            {
                yield return FieldWait(0.2f); // brief QB hold
                ball.HandOffTo(carrierSlot, 0.3f);
                yield return FieldWait(0.35f);
            }
            ball.AttachTo(carrierSlot ?? qbSlot);

            // Fumble check
            if (failEvent == FailPlayEventType.RunnerFumble)
            {
                // Wait for carrier to reach ~fumble point, then pop loose
                yield return FieldWait(0.5f);
                ball.FumbleLoose(ball.transform.position, 0.5f);
                yield return FieldWait(0.55f);

                // Recovery
                BoardSlot recovererSlot = ResolveRecovererSlot(g, offPlayer, defPlayer);
                if (recovererSlot != null)
                {
                    ball.RecoverTo(recovererSlot, 0.3f);
                    yield return FieldWait(0.35f);
                }
            }
            yield break;
        }

        // ── PASS PLAYS ──
        // QB holds ball during dropback
        ball.AttachTo(qbSlot);
        yield return FieldWait(0.6f); // dropback time

        // QB Fumble (pre-throw)
        if (failEvent == FailPlayEventType.QBFumble)
        {
            ball.FumbleLoose(ball.transform.position, 0.5f);
            yield return FieldWait(0.55f);
            BoardSlot recovererSlot = ResolveRecovererSlot(g, offPlayer, defPlayer);
            if (recovererSlot != null)
            {
                ball.RecoverTo(recovererSlot, 0.3f);
                yield return FieldWait(0.35f);
            }
            yield break;
        }

        // Determine throw target position
        BoardSlot throwTarget = receiverSlot ?? carrierSlot;
        if (throwTarget == null)
        {
            // No receiver found — ball just stays with QB
            yield break;
        }
        Vector3 targetPos = throwTarget.transform.position;

        // Calculate flight duration based on distance
        float dist = Vector3.Distance(ball.transform.position, targetPos);
        float flightDur = Mathf.Clamp(dist * 0.06f, 0.3f, 0.8f);

        // Throw the ball
        ball.Detach();
        ball.ThrowTo(targetPos, flightDur);
        yield return FieldWait(flightDur + 0.05f);

        // ── INTERCEPTION ──
        if (failEvent == FailPlayEventType.Interception ||
            (failEvent == FailPlayEventType.TippedPass && g.turnover_pending))
        {
            BoardSlot interceptorSlot = FindInterceptorSlot(g, defPlayer);
            if (interceptorSlot != null)
            {
                ball.AttachTo(interceptorSlot);
            }
            yield break;
        }

        // ── INCOMPLETE (including BattedPass, TippedPass non-INT) ──
        if (failEvent == FailPlayEventType.IncompletePass ||
            failEvent == FailPlayEventType.BattedPass ||
            (failEvent == FailPlayEventType.TippedPass && !g.turnover_pending))
        {
            ball.DropIncomplete(targetPos, 0.6f);
            yield break;
        }

        // ── COMPLETE PASS ──
        ball.AttachTo(throwTarget);

        // Runner fumble after catch
        if (failEvent == FailPlayEventType.RunnerFumble)
        {
            yield return FieldWait(0.5f);
            ball.FumbleLoose(ball.transform.position, 0.5f);
            yield return FieldWait(0.55f);
            BoardSlot recovererSlot = ResolveRecovererSlot(g, offPlayer, defPlayer);
            if (recovererSlot != null)
            {
                ball.RecoverTo(recovererSlot, 0.3f);
                yield return FieldWait(0.35f);
            }
        }
    }

    /// <summary>Find the BoardSlot for the interceptor card, or nearest DB.</summary>
    private BoardSlot FindInterceptorSlot(Game g, Player defPlayer)
    {
        if (!string.IsNullOrEmpty(g.interceptor_uid))
        {
            BoardSlot slot = FindSlotForCardUid(defPlayer, g.interceptor_uid);
            if (slot != null) return slot;
        }
        // Fallback: pick nearest DB slot to ball
        var dbSlots = fieldSlotMgr?.GetSlotsForPosition(PlayerPositionGrp.DB, defPlayer.player_id);
        if (dbSlots == null || dbSlots.Count == 0) return null;
        Vector3 ballPos = BallManager.Instance?.transform.position ?? Vector3.zero;
        return dbSlots.OrderBy(s => Vector3.Distance(s.transform.position, ballPos)).First();
    }

    /// <summary>Find the BoardSlot for fumble recoverer.</summary>
    private BoardSlot ResolveRecovererSlot(Game g, Player offPlayer, Player defPlayer)
    {
        // If offense recovered
        if (!string.IsNullOrEmpty(g.fumble_recoverer_uid))
        {
            BoardSlot slot = FindSlotForCardUid(offPlayer, g.fumble_recoverer_uid);
            if (slot != null) return slot;
        }
        // If turnover, pick nearest defensive slot
        if (g.turnover_pending)
        {
            Vector3 ballPos = BallManager.Instance?.transform.position ?? Vector3.zero;
            var allDefSlots = fieldSlotMgr?.GetAllSlotsForPlayer(defPlayer.player_id);
            if (allDefSlots != null && allDefSlots.Count > 0)
                return allDefSlots.OrderBy(s => Vector3.Distance(s.transform.position, ballPos)).First();
        }
        // Offense keeps ball — return to carrier
        BoardSlot carrier = FindSlotForCardUid(offPlayer, g.ball_carrier_uid);
        return carrier;
    }

    private void OnLiveBallStart(Game g)
    {
        // Queues with WaitForPhase(LiveBall) auto-resume via BroadcastPhaseChange
    }

    private void OnEndTurn(Game g)
    {
        ClearAllQueues();
        BallManager.Instance?.ResetToLOS();
        if (fieldSlotMgr != null)
            fieldSlotMgr.animationLocked = false;
        routesAssigned = false;
        currentRoutePlans = null;
        hasAudible = false;
    }

    private void OnStartTurn(Game g)
    {
        ClearAllQueues();
        BallManager.Instance?.ResetToLOS();
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
        int libraryCount = 0, proceduralCount = 0, carrierCount = 0;

        PlayType playType = offPlayer.SelectedPlay;
        float fw = fieldSlotMgr.fieldWidth;
        string carrierUid = g.ball_carrier_uid;
        string targetUid = g.target_receiver_uid;
        bool isRun = (playType == PlayType.Run);

        // Track which slots got card-backed procedural routes (carrier/target)
        var assignedSlots = new HashSet<BoardSlot>();

        // Step 1: Assign routes for ball carrier + target receiver
        //   - Ball carrier: always procedural (run routes encode exact yardage)
        //   - Target receiver (pass): try library first if vertex allows, then procedural fallback
        if (currentRoutePlans != null)
        {
            foreach (var plan in currentRoutePlans)
            {
                bool isCarrier = (plan.cardUid == carrierUid);
                bool isTarget = plan.isTargetReceiver;
                if (!isCarrier && !isTarget) continue;
                if (plan.route.segments == null || plan.route.segments.Count == 0) continue;

                BoardSlot slot = FindSlotForCardUid(offPlayer, plan.cardUid);
                if (slot == null) continue;

                // Target receiver on pass plays: try library route first
                if (isTarget && !isRun)
                {
                    Card slotCard = FindCardInSlot(g, offPlayer, slot);
                    bool isTE = slotCard?.CardData?.playerPosition == PlayerPositionGrp.RB_TE;
                    PlayerPositionGrp grp = slotCard?.CardData?.playerPosition ?? PlayerPositionGrp.WR;
                    RouteContext ctx = RouteLibrary.GetContext(grp, playType, isTE);
                    int lateralDir = RouteLibrary.LateralFromSlot(slot, fw);

                    RouteData libRoute = RouteLibrary.Pick(grp, -1, ctx, lateralDir, g.yardage_this_play);
                    if (libRoute != null)
                    {
                        var segs = RouteConverter.ToSegments(libRoute, slot, "library-target", g.yardage_this_play);
                        if (segs.Count > 0)
                        {
                            slot.MoveQueue.Clear();
                            slot.MoveQueue.Enqueue(SlotMovementSegment.WaitForSignal("snap"));
                            slot.MoveQueue.EnqueueRange(segs);
                            assignedSlots.Add(slot);
                            libraryCount++;
                            Debug.Log($"[RouteAssign] TARGET {grp} used library route '{libRoute.name}' (vertex={libRoute.GetVertexDepth():F1}y, net={g.yardage_this_play}y)");
                            continue; // skip procedural
                        }
                    }
                    // Fall through to procedural below
                }

                slot.MoveQueue.Clear();
                slot.MoveQueue.Enqueue(SlotMovementSegment.WaitForSignal("snap"));
                slot.MoveQueue.EnqueueRange(plan.route.segments);
                assignedSlots.Add(slot);
                carrierCount++;
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
                        libraryCount++;
                        continue;
                    }
                }

                // Procedural fallback
                slot.MoveQueue.EnqueueRange(ProceduralFallback(grp, playType, fw).segments);
                proceduralCount++;
                Debug.Log($"[RouteAssign] FALLBACK {grp}[{si}] ctx={ctx} dir={lateralDir} — no library match");
            }
        }

        routesAssigned = true;
        Debug.Log($"[RouteAssign] carrier/target={carrierCount} library={libraryCount} fallback={proceduralCount} | play={playType}");
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

    // ── Live Ball Event Handling ─────────────────────────────

    private void OnLiveBallEventReceived(LiveBallEventType type, string offCardId, string defCardId, int p1, int p2)
    {
        Game g = GameClient.Get()?.GetGameData();
        if (g == null) return;

        if (type == LiveBallEventType.YardageDelta)
        {
            int delta = p1;
            Debug.Log($"[LiveBall.Anim] YardageDelta {delta:+#;-#;0} from {offCardId} -> total {p2}");
            AnimateCarrierDelta(g, delta);
        }
        else if (type == LiveBallEventType.PostLBResolved)
        {
            int preLB = p1;
            int finalLB = p2;
            Debug.Log($"[LiveBall.Anim] PostLBResolved pre={preLB} final={finalLB} turnover={g.turnover_pending}");
            if (g.turnover_pending)
                AnimateLiveBallTurnover(g);
        }
    }

    private void AnimateCarrierDelta(Game g, int deltaYards)
    {
        if (deltaYards == 0) return;
        Player offPlayer = g.current_offensive_player;
        Player defPlayer = g.GetCurrentDefensivePlayer();
        BoardSlot carrier = FindSlotForCardUid(offPlayer, g.ball_carrier_uid ?? g.target_receiver_uid);
        if (carrier == null) return;

        carrier.MoveQueue.Enqueue(SlotMovementSegment.MoveBy(
            new Vector2(0, deltaYards),
            SpeedTier.Jog,
            $"lb_delta_{deltaYards}"
        ));

        Color popupColor = deltaYards > 0
            ? new Color(0.3f, 1f, 0.45f)
            : new Color(1f, 0.45f, 0.45f);
        StatChangePopup.Instance?.ShowCustom(
            carrier.transform.position,
            $"{deltaYards:+#;-#;0} yd",
            popupColor
        );

        if (defPlayer != null)
            EnqueueDefenseTracking(defPlayer, carrier);
    }

    private void AnimateLiveBallTurnover(Game g)
    {
        BallManager ball = BallManager.Instance;
        if (ball == null) return;

        Vector3 fumblePos = ball.transform.position;
        ball.FumbleLoose(fumblePos, 0.5f);

        Player offPlayer = g.current_offensive_player;
        Player defPlayer = g.GetCurrentDefensivePlayer();
        BoardSlot recovererSlot = ResolveRecovererSlot(g, offPlayer, defPlayer);
        if (recovererSlot != null)
        {
            ball.RecoverTo(recovererSlot, 0.4f);

            int returnYards = g.live_ball_return_yards;
            if (returnYards > 0)
            {
                recovererSlot.MoveQueue.Enqueue(SlotMovementSegment.MoveBy(
                    new Vector2(0, -returnYards),
                    SpeedTier.Sprint,
                    $"lb_return_{returnYards}"
                ));
                StatChangePopup.Instance?.ShowCustom(
                    recovererSlot.transform.position,
                    $"RETURN {returnYards} yd",
                    new Color(1f, 0.85f, 0.3f)
                );
            }
        }
    }
}
