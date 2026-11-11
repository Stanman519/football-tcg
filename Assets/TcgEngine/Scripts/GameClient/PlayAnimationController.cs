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
public partial class PlayAnimationController : MonoBehaviour
{
    public static PlayAnimationController Instance { get; private set; }

    [Header("Pre-Snap Timing")]
    [Tooltip("Seconds for the pre-snap 'hut hut' beat")]
    public float preSnapBeatDuration = 1.2f;
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
    private Coroutine readyRetryCoroutine;

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
        if (abilitySpotlight == null) // not in scene — self-builds its canvas in Awake
            abilitySpotlight = new GameObject("AbilitySpotlight").AddComponent<AbilitySpotlight>();
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
            else if (lastPhase == GamePhase.Resolution || lastPhase == GamePhase.LiveBall || lastPhase == GamePhase.EndTurn)
                // Dead-ball plays (TD, incomplete...) go Resolution -> ChoosePlayers with no EndTurn/StartTurn
                // refresh in between; without this the field stays animationLocked and the next play
                // animates from stale positions (benched slots, old ball spot)
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
        if (fieldSlotMgr == null)
        {
            // Broken client must not stall the ready-gated Resolution phase
            SendResolutionReady();
            return;
        }

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
            AssignDefenseRoles(g, offPlayer, g.GetCurrentDefensivePlayer());
        }

        // Start all queues (they'll pause at WaitForSignal("snap"))
        StartAllQueues();

        if (resolutionCoroutine != null)
            StopCoroutine(resolutionCoroutine);
        resolutionCoroutine = StartCoroutine(ResolutionSequence(g));
    }

    private IEnumerator ResolutionSequence(Game g)
    {
        PlayCtx ctx = BuildPlayContext(g);
        var ball = ctx.ball;
        Player offPlayer = ctx.offPlayer;
        BoardSlot qbSlot = ctx.qbSlot;

        // 1. Reels just finished flashing (Resolution starts after full spin) — one readable beat, full size
        yield return FieldWait(0.9f);

        // 2. Minimize slot machine
        if (slotMachineUI != null)
            slotMachineUI.MinimizeAnimated(0.5f);
        yield return FieldWait(0.7f);

        // 3. IMPACT SHOWCASES — zoom each player whose ability shaped this play, w/ value banner
        if (abilitySpotlight != null)
        {
            abilitySpotlight.BuildFromImpacts(g);
            if (abilitySpotlight.HasPendingShowcases)
                yield return abilitySpotlight.FlushAndProcess(bulletTime: false);
        }

        // Let stragglers finish lining up (after a possession change slots are still jogging in from the bench)
        float lineupWait = 0f;
        while (lineupWait < 3f && AnySlotStillMoving())
        {
            lineupWait += Time.deltaTime;
            yield return null;
        }

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

        // 6. Snap the ball — routes + defense roles begin
        BroadcastSignal("snap");

        // 7. Outcome choreography: ball event + outcome + pursuit + tackle + aftermath
        yield return StartCoroutine(RunOutcomeChoreography(ctx));

        // Big gain flash — fires here, right as the play ends (TD/INT flash inside the scripts)
        if (ctx.yardage >= 20 && !ctx.isTouchdown && g.last_fail_event == FailPlayEventType.None)
            BigPlayOverlay.Get()?.ShowEvent($"BIG PLAY  +{ctx.yardage} YDS", new Color(0.2f, 0.9f, 0.3f));

        // Fallback: no procedural route assigned (shouldn't happen for real plays)
        if (!routesAssigned && ctx.carrierSlot != null && ctx.yardage != 0)
        {
            ctx.carrierSlot.MoveQueue.Enqueue(SlotMovementSegment.MoveBy(
                new Vector2(0, ctx.yardage),
                SpeedTier.Sprint,
                "yardage"
            ));
            yield return WaitForQueuesToFinish(10f);
        }

        // Outcome beat: name the fail event and WHO caused it before the recap numbers
        if (g.last_fail_event != FailPlayEventType.None && abilitySpotlight != null)
        {
            Card culprit = null;
            if (!string.IsNullOrEmpty(g.fail_event_caster_uid))
            {
                foreach (Player p in g.players)
                {
                    culprit = p.cards_board.FirstOrDefault(c => c.uid == g.fail_event_caster_uid);
                    if (culprit != null) break;
                }
            }
            bool turnover = g.turnover_pending;
            string headline = g.last_fail_event switch
            {
                FailPlayEventType.TackleForLoss => "TACKLE FOR LOSS",
                FailPlayEventType.Sack => "SACK",
                FailPlayEventType.QBFumble => "QB FUMBLE",
                FailPlayEventType.BattedPass => "PASS BATTED",
                FailPlayEventType.TippedPass => "PASS TIPPED",
                FailPlayEventType.Interception => "INTERCEPTION",
                FailPlayEventType.IncompletePass => "INCOMPLETE",
                FailPlayEventType.RunnerFumble => "FUMBLE",
                _ => g.last_fail_event.ToString().ToUpper(),
            };
            string desc = turnover ? "Turnover" : (ctx.yardage != 0 ? $"{ctx.yardage:+#;-#;0} yds" : "No gain");
            CardData cd = culprit != null ? culprit.CardData : null;
            abilitySpotlight.ShowBeat(cd, culprit != null ? "CAUSED BY" : "PLAY RESULT",
                cd != null ? cd.title : "", headline, desc, abilitySpotlight.DefenseColor);
            yield return new WaitForSeconds(2.2f);
            abilitySpotlight.HideBeat();
            yield return new WaitForSeconds(0.35f);
        }

        // Post-play yardage summary
        var summaryPanel = YardageSummaryPanel.Get();
        if (summaryPanel != null)
        {
            summaryPanel.ShowSummary(g);
            yield return new WaitForSeconds(summaryPanel.fadeInDur + summaryPanel.holdDur + summaryPanel.fadeOutDur);
        }

        // Animation done — tell server (Resolution is ready-gated); retry covers dropped-while-resolving
        SendResolutionReady();

        resolutionCoroutine = null;
    }

    private void SendResolutionReady()
    {
        GameClient.Get()?.SendAction(GameAction.PlayerReadyPhase);
        if (readyRetryCoroutine != null)
            StopCoroutine(readyRetryCoroutine);
        readyRetryCoroutine = StartCoroutine(ResolutionReadyRetry());
    }

    private IEnumerator ResolutionReadyRetry()
    {
        while (GameClient.Get()?.GetGameData()?.phase == GamePhase.Resolution)
        {
            yield return new WaitForSeconds(2f);
            if (GameClient.Get()?.GetGameData()?.phase == GamePhase.Resolution)
                GameClient.Get()?.SendAction(GameAction.PlayerReadyPhase);
        }
        readyRetryCoroutine = null;
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

    private void StopResolutionCoroutines()
    {
        // Timeout path can still flip phase mid-animation — don't let the stale sequence keep running
        if (resolutionCoroutine != null)
        {
            StopCoroutine(resolutionCoroutine);
            resolutionCoroutine = null;
        }
        if (readyRetryCoroutine != null)
        {
            StopCoroutine(readyRetryCoroutine);
            readyRetryCoroutine = null;
        }
    }

    private void OnLiveBallStart(Game g)
    {
        // Queues with WaitForPhase(LiveBall) auto-resume via BroadcastPhaseChange
    }

    private void OnEndTurn(Game g)
    {
        StopResolutionCoroutines();
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
        StopResolutionCoroutines();
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

        // Credited tackler goes all the way to the carrier; the rest close 60%
        Game g = GameClient.Get()?.GetGameData();
        BoardSlot tacklerSlot = FindSlotForCardUid(defPlayer, g?.tackler_uid);

        foreach (var grp in defGroups)
        {
            var slots = fieldSlotMgr.GetSlotsForPosition(grp, defPlayer.player_id);
            foreach (BoardSlot slot in slots)
            {
                Vector3 current = slot.transform.localPosition;
                float closeFraction = slot == tacklerSlot ? 1f : 0.6f;
                Vector3 trackPos = Vector3.Lerp(current, carrierLocal, closeFraction);
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
        // Start EVERY slot incl. defense — empty queues arm and self-start when
        // pursuit/live-ball segments arrive later (Start is a no-op on running queues)
        foreach (BoardSlot slot in BoardSlot.GetAll())
            slot.MoveQueue.Start();
    }

    /// <summary>Completion-driven wait: all movement queues idle (paused counts as done). Capped vs livelock.</summary>
    private IEnumerator WaitForQueuesToFinish(float maxSeconds)
    {
        float t = 0f;
        while (t < maxSeconds)
        {
            bool anyRunning = false;
            foreach (BoardSlot s in BoardSlot.GetAll())
            {
                if (s.MoveQueue.IsActive) { anyRunning = true; break; }
            }
            if (!anyRunning) yield break;
            t += Time.deltaTime;
            yield return null;
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

    // ── Live Ball Event Handling ─────────────────────────────

    private static bool AnySlotStillMoving()
    {
        foreach (BoardSlot bs in FindObjectsByType<BoardSlot>(FindObjectsSortMode.None))
        {
            if (bs.MoveQueue != null && bs.MoveQueue.HasWork)
                return true;
        }
        return false;
    }

    private struct LiveBallBeat
    {
        public LiveBallEventType type;
        public string offCardId, defCardId;
        public int p1, p2;
    }

    private readonly Queue<LiveBallBeat> liveBallBeats = new Queue<LiveBallBeat>();
    private Coroutine liveBallBeatRoutine;
    private bool liveBallAnyCard;

    // Server fires every LiveBall event in one frame. Queue them and play ONE beat at a time
    // (panel + field move + hold) so the player can follow what each card did.
    // Durations come from LiveBallBeats; the server holds EndTurn for the same total.
    private void OnLiveBallEventReceived(LiveBallEventType type, string offCardId, string defCardId, int p1, int p2)
    {
        if (type == LiveBallEventType.Reveal)
        {
            liveBallBeats.Clear();
            liveBallAnyCard = !string.IsNullOrEmpty(offCardId) || !string.IsNullOrEmpty(defCardId);
        }

        liveBallBeats.Enqueue(new LiveBallBeat { type = type, offCardId = offCardId, defCardId = defCardId, p1 = p1, p2 = p2 });
        if (liveBallBeatRoutine == null)
            liveBallBeatRoutine = StartCoroutine(RunLiveBallBeats());
    }

    private IEnumerator RunLiveBallBeats()
    {
        while (liveBallBeats.Count > 0)
        {
            LiveBallBeat beat = liveBallBeats.Dequeue();
            float dur = LiveBallBeats.Duration(beat.type, liveBallAnyCard);
            Game g = GameClient.Get()?.GetGameData();
            if (g == null || dur <= 0f) continue;

            CardData offData = string.IsNullOrEmpty(beat.offCardId) ? null : CardData.Get(beat.offCardId);
            CardData defData = string.IsNullOrEmpty(beat.defCardId) ? null : CardData.Get(beat.defCardId);
            Color offCol = abilitySpotlight != null ? abilitySpotlight.OffenseColor : Color.green;
            Color defCol = abilitySpotlight != null ? abilitySpotlight.DefenseColor : Color.red;
            Color neutral = new Color(1f, 0.85f, 0.3f);
            bool panel = true;

            switch (beat.type)
            {
                case LiveBallEventType.Reveal:
                    panel = false; // LiveBallRevealPanel owns this beat
                    break;

                case LiveBallEventType.BothPass:
                    ShowLiveBallBeat(null, "LIVE BALL", "No cards played", "PLAY STANDS", "", neutral);
                    break;

                case LiveBallEventType.NegateOffense:
                    ShowLiveBallBeat(defData, "DEFENSE  -  LIVE BALL", CardTitle(defData), "NEGATED",
                        $"Cancels {CardTitle(offData)}", defCol);
                    break;

                case LiveBallEventType.NegateDefense:
                    ShowLiveBallBeat(offData, "OFFENSE  -  LIVE BALL", CardTitle(offData), "IMMUNE",
                        "Defensive effects ignored", offCol);
                    break;

                case LiveBallEventType.BallSecurity:
                    ShowLiveBallBeat(offData, "OFFENSE  -  LIVE BALL", CardTitle(offData), "FUMBLE PREVENTED",
                        $"Stops {CardTitle(defData)}", offCol);
                    break;

                case LiveBallEventType.FumbleRecovery:
                    ShowLiveBallBeat(defData, "FUMBLE ATTEMPT", CardTitle(defData), "OFFENSE RECOVERS",
                        $"Grit: offense {beat.p1} vs defense {beat.p2}", offCol);
                    break;

                case LiveBallEventType.FumbleTurnover:
                    ShowLiveBallBeat(defData, "FUMBLE ATTEMPT", CardTitle(defData), "FUMBLE!",
                        $"Grit: offense {beat.p1} vs defense {beat.p2}", defCol);
                    break;

                case LiveBallEventType.YardageDelta:
                {
                    // Source card arrives in the off slot regardless of side; side = its card type
                    bool srcIsDef = offData != null && offData.type == CardType.DefLiveBall;
                    int delta = beat.p1;
                    ShowLiveBallBeat(offData, (srcIsDef ? "DEFENSE" : "OFFENSE") + "  -  LIVE BALL", CardTitle(offData),
                        $"{delta:+#;-#;0} YDS", $"Play total: {beat.p2} yds", srcIsDef ? defCol : offCol);
                    Debug.Log($"[LiveBall.Anim] YardageDelta {delta:+#;-#;0} from {beat.offCardId} -> total {beat.p2}");
                    AnimateCarrierDelta(g, delta);
                    break;
                }

                case LiveBallEventType.PostLBResolved:
                {
                    Debug.Log($"[LiveBall.Anim] PostLBResolved pre={beat.p1} final={beat.p2} turnover={g.turnover_pending}");
                    if (g.turnover_pending)
                    {
                        ShowLiveBallBeat(null, "LIVE BALL", "Result", "TURNOVER",
                            g.live_ball_return_yards > 0 ? $"Returned {g.live_ball_return_yards} yds" : "", defCol);
                        AnimateLiveBallTurnover(g);
                    }
                    else
                    {
                        int diff = beat.p2 - beat.p1;
                        ShowLiveBallBeat(null, "LIVE BALL", "Result", $"{beat.p2:+#;-#;0} YDS",
                            diff != 0 ? $"Was {beat.p1}, live ball {diff:+#;-#;0}" : "No change", neutral);
                    }
                    break;
                }
            }

            float hold = panel ? Mathf.Max(0.2f, dur - 0.35f) : dur;
            yield return new WaitForSeconds(hold);
            if (panel)
            {
                abilitySpotlight?.HideBeat();
                yield return new WaitForSeconds(0.35f);
            }
        }
        liveBallBeatRoutine = null;
    }

    private void ShowLiveBallBeat(CardData card, string label, string title, string headline, string desc, Color color)
    {
        if (abilitySpotlight != null)
            abilitySpotlight.ShowBeat(card, label, title, headline, desc, color);
    }

    private static string CardTitle(CardData cd)
    {
        if (cd == null) return "";
        return !string.IsNullOrEmpty(cd.title) ? cd.title : cd.id;
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
