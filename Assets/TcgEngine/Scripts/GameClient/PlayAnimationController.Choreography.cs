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
/// Per-outcome play choreography (M3). Beats: Development → BallEvent → Outcome →
/// Pursuit&Tackle → Aftermath, driven by server outcome facts (tackler_uid,
/// fail_event_caster_uid, interceptor_uid...). Pre-snap beats live in ResolutionSequence.
/// </summary>
public partial class PlayAnimationController
{
    // ── Context ─────────────────────────────────────────────

    private class PlayCtx
    {
        public Game g;
        public Player offPlayer, defPlayer;
        public PlayType playType;
        public bool isPass;
        public int yardage;
        public FailPlayEventType failEvent;
        public BallManager ball;
        public BoardSlot qbSlot, carrierSlot, receiverSlot, tacklerSlot, failCasterSlot;
        public bool isTouchdown;
        public float fw;
    }

    private PlayCtx BuildPlayContext(Game g)
    {
        Player off = g.current_offensive_player;
        Player def = g.GetCurrentDefensivePlayer();
        PlayType pt = off?.SelectedPlay ?? PlayType.Huddle;
        var qbSlots = fieldSlotMgr?.GetSlotsForPosition(PlayerPositionGrp.QB, off.player_id);
        return new PlayCtx
        {
            g = g,
            offPlayer = off,
            defPlayer = def,
            playType = pt,
            isPass = pt == PlayType.ShortPass || pt == PlayType.LongPass,
            yardage = g.yardage_this_play,
            failEvent = g.last_fail_event,
            ball = BallManager.Instance,
            qbSlot = qbSlots != null && qbSlots.Count > 0 ? qbSlots[0] : null,
            carrierSlot = FindSlotForCardUid(off, g.ball_carrier_uid),
            receiverSlot = FindSlotForCardUid(off, g.target_receiver_uid),
            tacklerSlot = FindSlotForCardUid(def, g.tackler_uid),
            failCasterSlot = FindSlotForCardUid(def, g.fail_event_caster_uid),
            isTouchdown = g.raw_ball_on + g.yardage_this_play >= 100 && !g.turnover_pending,
            fw = fieldSlotMgr != null ? fieldSlotMgr.fieldWidth : 53.3f,
        };
    }

    // ── Defense roles (snap-gated, assigned alongside offense routes) ──

    /// <summary>
    /// Rushers (DL) drive the pocket / surge the line, LBs flow, DBs backpedal or come downhill.
    /// All gated on "snap"; pursuit segments append after these and run sequentially.
    /// </summary>
    private void AssignDefenseRoles(Game g, Player offPlayer, Player defPlayer)
    {
        if (fieldSlotMgr == null || defPlayer == null) return;

        PlayType pt = offPlayer?.SelectedPlay ?? PlayType.Huddle;
        bool isPass = pt == PlayType.ShortPass || pt == PlayType.LongPass;
        float fw = fieldSlotMgr.fieldWidth;

        var qbSlots = fieldSlotMgr.GetSlotsForPosition(PlayerPositionGrp.QB, offPlayer.player_id);
        BoardSlot qb = qbSlots != null && qbSlots.Count > 0 ? qbSlots[0] : null;

        foreach (var grp in new[] { PlayerPositionGrp.DL, PlayerPositionGrp.LB, PlayerPositionGrp.DB })
        {
            var slots = fieldSlotMgr.GetSlotsForPosition(grp, defPlayer.player_id);
            foreach (BoardSlot slot in slots)
            {
                // Deeper into the defensive backfield = away from LOS on this defender's side
                float deep = Mathf.Sign(slot.transform.localPosition.y);
                if (deep == 0) deep = 1f;

                var q = slot.MoveQueue;
                q.Enqueue(SlotMovementSegment.WaitForSignal("snap"));

                switch (grp)
                {
                    case PlayerPositionGrp.DL:
                        if (isPass && qb != null)
                        {
                            // Pocket rush: drive most of the way to the QB's spot
                            Vector3 target = Vector3.Lerp(slot.transform.localPosition, qb.transform.localPosition, 0.65f);
                            q.Enqueue(SlotMovementSegment.MoveTo(new Vector2(target.x / fw, target.y), 1.1f, Ease.InQuad, "role"));
                        }
                        else
                        {
                            // Run: surge into the line
                            q.Enqueue(SlotMovementSegment.MoveBy(new Vector2(0, -deep * 1.5f), 0.5f, Ease.OutQuad, "role"));
                        }
                        break;

                    case PlayerPositionGrp.LB:
                        if (isPass)
                            q.Enqueue(SlotMovementSegment.MoveBy(new Vector2(0, deep * 2f), 0.8f, Ease.InOutQuad, "role")); // zone drop
                        else
                            q.Enqueue(SlotMovementSegment.MoveBy(new Vector2(0, -deep * 2f), 0.7f, Ease.OutQuad, "role")); // flow downhill
                        break;

                    case PlayerPositionGrp.DB:
                        if (isPass)
                            q.Enqueue(SlotMovementSegment.MoveBy(new Vector2(0, deep * 3.5f), 1.0f, Ease.InOutQuad, "role")); // backpedal cushion
                        else
                        {
                            q.Enqueue(SlotMovementSegment.MoveBy(Vector2.zero, 0.4f, Ease.Linear, "role")); // read
                            q.Enqueue(SlotMovementSegment.MoveBy(new Vector2(0, -deep * 2f), 0.6f, Ease.OutQuad, "role")); // come downhill
                        }
                        break;
                }
            }
        }
    }

    // ── Outcome dispatch ────────────────────────────────────

    private IEnumerator RunOutcomeChoreography(PlayCtx ctx)
    {
        if (ctx.ball == null || ctx.playType == PlayType.Huddle)
        {
            yield return WaitForQueuesToFinish(10f);
            yield break;
        }

        switch (ctx.failEvent)
        {
            case FailPlayEventType.Sack:
                yield return ScriptSack(ctx); break;
            case FailPlayEventType.TackleForLoss:
                yield return ScriptTfl(ctx); break;
            case FailPlayEventType.QBFumble:
                yield return ScriptQbFumble(ctx); break;
            case FailPlayEventType.Interception:
                yield return ScriptInterception(ctx); break;
            case FailPlayEventType.TippedPass:
                if (ctx.g.turnover_pending) yield return ScriptInterception(ctx);
                else yield return ScriptIncomplete(ctx); break;
            case FailPlayEventType.BattedPass:
                yield return ScriptBattedPass(ctx); break;
            case FailPlayEventType.IncompletePass:
                yield return ScriptIncomplete(ctx); break;
            case FailPlayEventType.RunnerFumble:
                yield return ScriptRunnerFumble(ctx); break;
            default:
                if (ctx.isPass) yield return ScriptPassComplete(ctx);
                else yield return ScriptRun(ctx); break;
        }
    }

    // ── Outcome scripts ─────────────────────────────────────

    private IEnumerator ScriptRun(PlayCtx ctx)
    {
        yield return HandoffBeat(ctx);
        yield return WaitForQueuesToFinish(12f); // carrier route encodes net yardage
        yield return PursuitAndTackle(ctx, ctx.carrierSlot ?? ctx.qbSlot);
        yield return CelebrateIfTouchdown(ctx);
    }

    private IEnumerator ScriptPassComplete(PlayCtx ctx)
    {
        yield return DropbackBeat(ctx);

        // Throw at the catch point: wait for the receiver's route to reach its YAC leg (or finish)
        BoardSlot target = ctx.receiverSlot ?? ctx.carrierSlot;
        if (target == null) { yield return WaitForQueuesToFinish(8f); yield break; }
        yield return WaitForRouteCatchPoint(target, 6f);

        yield return ThrowAndCatch(ctx, target);

        yield return WaitForQueuesToFinish(10f); // YAC + everyone else
        yield return PursuitAndTackle(ctx, target);
        yield return CelebrateIfTouchdown(ctx);
    }

    private IEnumerator ScriptSack(PlayCtx ctx)
    {
        ctx.ball.AttachTo(ctx.qbSlot);
        yield return FieldWait(0.5f); // dropback underway

        // Sacker beats his block and closes to the sack spot (net yardage behind LOS)
        BoardSlot sacker = ctx.failCasterSlot ?? ctx.tacklerSlot;
        float qbX = (ctx.qbSlot != null ? ctx.qbSlot.transform.localPosition.x : 0f) / ctx.fw;
        if (sacker != null)
            sacker.MoveQueue.Enqueue(SlotMovementSegment.MoveTo(new Vector2(qbX, ctx.yardage), SpeedTier.Sprint, "sack"));
        // QB stumbles back to the sack spot
        ctx.qbSlot?.MoveQueue.Enqueue(SlotMovementSegment.MoveTo(new Vector2(qbX, ctx.yardage), 0.4f, Ease.OutQuad, "sack"));

        yield return WaitForQueuesToFinish(8f);
        ContactPop(sacker, ctx.qbSlot);
        Popup(ctx.qbSlot, "SACK!", new Color(1f, 0.45f, 0.45f));
        yield return FieldWait(0.5f);
    }

    private IEnumerator ScriptTfl(PlayCtx ctx)
    {
        yield return HandoffBeat(ctx);

        // TFL man shoots the gap to meet the carrier at the (negative) end spot
        BoardSlot shooter = ctx.failCasterSlot ?? ctx.tacklerSlot;
        BoardSlot carrier = ctx.carrierSlot ?? ctx.qbSlot;
        if (shooter != null && carrier != null)
        {
            float carrierX = carrier.transform.localPosition.x / ctx.fw;
            shooter.MoveQueue.Enqueue(SlotMovementSegment.MoveTo(new Vector2(carrierX, ctx.yardage), SpeedTier.Sprint, "tfl"));
        }

        yield return WaitForQueuesToFinish(10f);
        ContactPop(shooter, carrier);
        Popup(carrier, "TACKLE FOR LOSS", new Color(1f, 0.45f, 0.45f));
        yield return FieldWait(0.5f);
    }

    private IEnumerator ScriptQbFumble(PlayCtx ctx)
    {
        ctx.ball.AttachTo(ctx.qbSlot);
        yield return FieldWait(0.6f); // dropback

        ctx.ball.FumbleLoose(ctx.ball.transform.position, 0.5f);
        Popup(ctx.qbSlot, "FUMBLE!", new Color(1f, 0.75f, 0.3f));
        FumbleContestCluster(ctx, ctx.ball.transform.position);
        yield return FieldWait(0.9f);

        BoardSlot recoverer = ResolveRecovererSlot(ctx.g, ctx.offPlayer, ctx.defPlayer);
        if (recoverer != null)
        {
            ctx.ball.RecoverTo(recoverer, 0.3f);
            yield return FieldWait(0.35f);
        }
        yield return WaitForQueuesToFinish(6f);
        // Ball is live — the LiveBall phase (M5) resolves who keeps it
    }

    private IEnumerator ScriptInterception(PlayCtx ctx)
    {
        yield return DropbackBeat(ctx);

        BoardSlot target = ctx.receiverSlot ?? ctx.carrierSlot;
        if (target != null)
            yield return WaitForRouteCatchPoint(target, 6f);

        // Interceptor breaks on the ball BEFORE it arrives
        BoardSlot interceptor = FindInterceptorSlot(ctx.g, ctx.defPlayer);
        Vector3 catchLocal = (target != null ? target.transform.localPosition
                             : ctx.qbSlot != null ? ctx.qbSlot.transform.localPosition + new Vector3(0, 8f, 0)
                             : Vector3.zero);
        if (interceptor != null)
            interceptor.MoveQueue.Enqueue(SlotMovementSegment.MoveTo(new Vector2(catchLocal.x / ctx.fw, catchLocal.y), SpeedTier.Sprint, "int"));

        // Throw into the break
        Vector3 catchWorld = fieldSlotMgr.transform.TransformPoint(new Vector3(catchLocal.x, catchLocal.y, -1f));
        float dist = Vector3.Distance(ctx.ball.transform.position, catchWorld);
        float flight = Mathf.Clamp(dist * 0.06f, 0.3f, 0.8f);
        ctx.ball.Detach();
        ctx.ball.ThrowTo(catchWorld, flight);
        yield return FieldWait(flight + 0.05f);
        yield return WaitForQueuesToFinish(4f); // interceptor arrives

        if (interceptor != null)
        {
            ctx.ball.AttachTo(interceptor);
            Popup(interceptor, "INTERCEPTED!", new Color(1f, 0.45f, 0.45f));
            BigPlayOverlay.Get()?.ShowEvent("INTERCEPTION!", Color.red);
            yield return FieldWait(0.4f);

            // Return run toward the offense's end zone, with the nearest offense players chasing
            int returnDist = Mathf.Clamp(Mathf.Abs(ctx.yardage), 2, 30);
            interceptor.MoveQueue.Enqueue(SlotMovementSegment.MoveBy(new Vector2(0, -returnDist), SpeedTier.Sprint, "int_return"));

            Vector3 endLocal = interceptor.transform.localPosition + new Vector3(0, -returnDist, 0);
            foreach (BoardSlot chaser in NearestSlots(ctx.offPlayer, interceptor.transform.localPosition, 2))
                EnqueueConverge(chaser, endLocal, 0.85f, SpeedTier.Sprint, "chase");

            yield return WaitForQueuesToFinish(8f);
            ContactPop(NearestSlots(ctx.offPlayer, interceptor.transform.localPosition, 1).FirstOrDefault(), interceptor);
        }
        yield return FieldWait(0.4f);
    }

    private IEnumerator ScriptIncomplete(PlayCtx ctx)
    {
        yield return DropbackBeat(ctx);

        BoardSlot target = ctx.receiverSlot ?? ctx.carrierSlot;
        if (target != null)
            yield return WaitForRouteCatchPoint(target, 6f);

        Vector3 targetWorld = target != null ? target.transform.position
            : ctx.ball.transform.position + new Vector3(0, 5f, 0);

        // Contesting defender closes on the catch point
        BoardSlot contester = ctx.failCasterSlot ?? NearestSlots(ctx.defPlayer, ToLocal(targetWorld), 1).FirstOrDefault();
        if (contester != null && target != null)
            EnqueueConverge(contester, target.transform.localPosition, 0.9f, SpeedTier.Sprint, "contest");

        float dist = Vector3.Distance(ctx.ball.transform.position, targetWorld);
        float flight = Mathf.Clamp(dist * 0.06f, 0.3f, 0.8f);
        ctx.ball.Detach();
        ctx.ball.ThrowTo(targetWorld, flight);
        yield return FieldWait(flight + 0.05f);

        ContactPop(contester, target);
        ctx.ball.DropIncomplete(targetWorld, 0.6f);
        Popup(target ?? ctx.qbSlot, "INCOMPLETE", new Color(0.8f, 0.8f, 0.8f));
        yield return WaitForQueuesToFinish(6f);
        yield return FieldWait(0.4f);
    }

    private IEnumerator ScriptBattedPass(PlayCtx ctx)
    {
        yield return DropbackBeat(ctx);

        // Batted at the line: DL gets a hand up right in front of the QB
        BoardSlot batter = ctx.failCasterSlot;
        Vector3 losSpot = ctx.qbSlot != null
            ? new Vector3(ctx.qbSlot.transform.localPosition.x, 0.5f, -1f)
            : Vector3.zero;
        if (batter != null)
            EnqueueConverge(batter, losSpot, 1f, SpeedTier.Sprint, "bat");
        yield return WaitForQueuesToFinish(4f);

        Vector3 batWorld = fieldSlotMgr.transform.TransformPoint(losSpot);
        ctx.ball.Detach();
        ctx.ball.ThrowTo(batWorld, 0.25f);
        yield return FieldWait(0.3f);
        if (batter != null) ContactPop(batter, null);
        ctx.ball.DropIncomplete(batWorld, 0.5f);
        Popup(batter ?? ctx.qbSlot, "BATTED DOWN", new Color(0.8f, 0.8f, 0.8f));
        yield return WaitForQueuesToFinish(6f);
        yield return FieldWait(0.4f);
    }

    private IEnumerator ScriptRunnerFumble(PlayCtx ctx)
    {
        if (ctx.isPass)
        {
            yield return DropbackBeat(ctx);
            BoardSlot target = ctx.receiverSlot ?? ctx.carrierSlot;
            if (target != null)
            {
                yield return WaitForRouteCatchPoint(target, 6f);
                yield return ThrowAndCatch(ctx, target);
            }
        }
        else
        {
            yield return HandoffBeat(ctx);
        }

        // Carrier gets hit at the fumble spot
        yield return WaitForQueuesToFinish(10f);
        BoardSlot carrier = ctx.carrierSlot ?? ctx.receiverSlot ?? ctx.qbSlot;
        BoardSlot forcer = ctx.failCasterSlot ?? ctx.tacklerSlot;
        if (forcer != null && carrier != null)
        {
            EnqueueConverge(forcer, carrier.transform.localPosition, 1f, SpeedTier.Sprint, "hit");
            yield return WaitForQueuesToFinish(5f);
            ContactPop(forcer, carrier);
        }

        ctx.ball.FumbleLoose(ctx.ball.transform.position, 0.5f);
        Popup(carrier, "FUMBLE!", new Color(1f, 0.75f, 0.3f));
        FumbleContestCluster(ctx, ctx.ball.transform.position);
        yield return FieldWait(0.9f);

        BoardSlot recoverer = ResolveRecovererSlot(ctx.g, ctx.offPlayer, ctx.defPlayer);
        if (recoverer != null)
        {
            ctx.ball.RecoverTo(recoverer, 0.3f);
            yield return FieldWait(0.35f);
        }
        yield return WaitForQueuesToFinish(6f);
        // Ball live — LiveBall phase (M5) decides the turnover
    }

    // ── Shared beats ────────────────────────────────────────

    private IEnumerator HandoffBeat(PlayCtx ctx)
    {
        if (ctx.carrierSlot != null && ctx.carrierSlot != ctx.qbSlot)
        {
            yield return FieldWait(0.2f); // brief QB hold
            ctx.ball.HandOffTo(ctx.carrierSlot, 0.3f);
            yield return FieldWait(0.35f);
        }
        ctx.ball.AttachTo(ctx.carrierSlot ?? ctx.qbSlot);
    }

    private IEnumerator DropbackBeat(PlayCtx ctx)
    {
        ctx.ball.AttachTo(ctx.qbSlot);
        yield return FieldWait(0.6f);
    }

    private IEnumerator ThrowAndCatch(PlayCtx ctx, BoardSlot target)
    {
        Vector3 targetPos = target.transform.position;
        float dist = Vector3.Distance(ctx.ball.transform.position, targetPos);
        float flight = Mathf.Clamp(dist * 0.06f, 0.3f, 0.8f);
        ctx.ball.Detach();
        ctx.ball.ThrowTo(targetPos, flight);
        yield return FieldWait(flight + 0.05f);
        ctx.ball.AttachTo(target);
    }

    /// <summary>Wait until the target's route reaches its YAC leg (catch point) or the queue drains.</summary>
    private IEnumerator WaitForRouteCatchPoint(BoardSlot target, float maxSeconds)
    {
        float t = 0f;
        while (t < maxSeconds)
        {
            var q = target.MoveQueue;
            if (q.IsIdle || q.IsPaused) yield break;
            if (q.CurrentSourceTag == "yac") yield break;
            t += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator PursuitAndTackle(PlayCtx ctx, BoardSlot carrier)
    {
        if (carrier == null) yield break;
        yield return FieldWait(0.25f);
        EnqueueDefenseTracking(ctx.defPlayer, carrier);
        yield return WaitForQueuesToFinish(10f);

        if (!ctx.isTouchdown && ctx.tacklerSlot != null)
        {
            ContactPop(ctx.tacklerSlot, carrier);
            yield return FieldWait(0.35f);
        }
    }

    private IEnumerator CelebrateIfTouchdown(PlayCtx ctx)
    {
        if (!ctx.isTouchdown) yield break;
        BoardSlot carrier = ctx.carrierSlot ?? ctx.receiverSlot ?? ctx.qbSlot;
        if (carrier != null)
        {
            carrier.transform.DOKill(true);
            carrier.transform.DOPunchScale(Vector3.one * 0.4f, 0.6f, 6, 0.5f).SetLink(carrier.gameObject);
            Popup(carrier, "TOUCHDOWN!", new Color(1f, 0.85f, 0.3f));
        }
        BigPlayOverlay.Get()?.ShowEvent("TOUCHDOWN!", new Color(1f, 0.75f, 0f));
        yield return FieldWait(1.0f);

        // Scoring beat: who scored and how (same who / value / why panel as abilities)
        if (abilitySpotlight != null)
        {
            Game g = GameClient.Get()?.GetGameData();
            Card scorer = null;
            string uid = g?.ball_carrier_uid ?? g?.target_receiver_uid;
            if (g != null && !string.IsNullOrEmpty(uid) && ctx.offPlayer != null)
                scorer = ctx.offPlayer.cards_board.FirstOrDefault(c => c != null && c.uid == uid);
            CardData cd = scorer?.CardData;
            string how = g != null && g.current_offensive_player != null
                && g.current_offensive_player.SelectedPlay == PlayType.Run ? "run" : "pass";
            abilitySpotlight.ShowBeat(cd, "SCORE", cd != null ? cd.title : "", "TOUCHDOWN",
                $"{ctx.yardage}-yd {how}", new Color(1f, 0.75f, 0f));
            yield return new WaitForSeconds(1.8f);
            abilitySpotlight.HideBeat();
            yield return new WaitForSeconds(0.35f);
        }
    }

    // ── Helpers ─────────────────────────────────────────────

    private void FumbleContestCluster(PlayCtx ctx, Vector3 ballWorldPos)
    {
        Vector3 ballLocal = ToLocal(ballWorldPos);
        foreach (BoardSlot s in NearestSlots(ctx.offPlayer, ballLocal, 2))
            EnqueueConverge(s, ballLocal, 0.85f, SpeedTier.Sprint, "scramble");
        foreach (BoardSlot s in NearestSlots(ctx.defPlayer, ballLocal, 2))
            EnqueueConverge(s, ballLocal, 0.85f, SpeedTier.Sprint, "scramble");
    }

    private void EnqueueConverge(BoardSlot slot, Vector3 targetLocal, float fraction, SpeedTier tier, string tag)
    {
        if (slot == null) return;
        Vector3 to = Vector3.Lerp(slot.transform.localPosition, targetLocal, fraction);
        float fw = fieldSlotMgr != null ? fieldSlotMgr.fieldWidth : 53.3f;
        slot.MoveQueue.Enqueue(SlotMovementSegment.MoveTo(new Vector2(to.x / fw, to.y), tier, tag));
    }

    private List<BoardSlot> NearestSlots(Player player, Vector3 localPos, int count)
    {
        if (player == null || fieldSlotMgr == null) return new List<BoardSlot>();
        var all = fieldSlotMgr.GetAllSlotsForPlayer(player.player_id);
        if (all == null) return new List<BoardSlot>();
        return all.OrderBy(s => Vector3.Distance(s.transform.localPosition, localPos))
                  .Take(count).ToList();
    }

    private Vector3 ToLocal(Vector3 worldPos) =>
        fieldSlotMgr != null ? fieldSlotMgr.transform.InverseTransformPoint(worldPos) : worldPos;

    private void ContactPop(BoardSlot a, BoardSlot b)
    {
        foreach (var s in new[] { a, b })
        {
            if (s == null) continue;
            s.transform.DOKill(true);
            s.transform.DOPunchScale(Vector3.one * 0.25f, 0.3f, 8, 0.6f).SetLink(s.gameObject);
        }
    }

    private void Popup(BoardSlot at, string text, Color color)
    {
        if (at == null) return;
        StatChangePopup.Instance?.ShowCustom(at.transform.position, text, color);
    }
}
