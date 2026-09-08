using UnityEngine;
using DG.Tweening;
using TcgEngine.Client;

/// <summary>
/// Animates the football on the field during play resolution.
/// Singleton on the BallMarker child of LOSMarker (z = -0.5).
/// Called from PlayAnimationController.ResolutionSequence.
/// </summary>
public class BallManager : MonoBehaviour
{
    public enum BallVisualState { Hidden, Held, InFlight, LooseBall, OnGround }

    [Header("References")]
    public SpriteRenderer ballSprite;
    public BallSpriteSheet spriteSheet;

    [Header("Settings")]
    [Tooltip("Offset from slot center when held (local to slot)")]
    public Vector3 heldOffset = new Vector3(0.15f, -0.1f, -0.05f);
    [Tooltip("Scale multiplier at peak of throw arc")]
    public float throwArcScale = 1.3f;

    // State
    private BallVisualState currentState = BallVisualState.Hidden;
    private Transform attachTarget;
    private Sprite[] activeFrames;
    private float activeFPS;
    private float frameTimer;
    private int frameIndex;
    private Tween activeTween;
    private Sequence activeSequence;

    // Singleton
    private static BallManager _instance;
    public static BallManager Instance => _instance;

    void Awake()
    {
        _instance = this;
        if (ballSprite == null)
            ballSprite = GetComponent<SpriteRenderer>();
        ResetToLOS();
    }

    /// <summary>Snap to LOS, show as on-ground sprite. Used between plays.</summary>
    public void ResetToLOS()
    {
        KillActive();
        Detach();
        transform.localPosition = Vector3.zero;
        transform.localScale = Vector3.one;
        SetVisualState(BallVisualState.OnGround);
        Show();
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void LateUpdate()
    {
        // Follow attached slot
        if (attachTarget != null)
            transform.position = attachTarget.position + heldOffset;

        // Cycle sprite frames
        if (activeFrames != null && activeFrames.Length > 1 && activeFPS > 0)
        {
            frameTimer += Time.deltaTime;
            if (frameTimer >= 1f / activeFPS)
            {
                frameTimer -= 1f / activeFPS;
                frameIndex = (frameIndex + 1) % activeFrames.Length;
                ballSprite.sprite = activeFrames[frameIndex];
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────

    private float Scaled(float dur) =>
        TimeController.Instance != null ? TimeController.Instance.Scale(dur) : dur;

    // ── Public API ───────────────────────────────────────────────

    /// <summary>Show ball at center, tween to QB slot.</summary>
    public Tween SnapToQB(BoardSlot qbSlot, float duration = 0.3f)
    {
        KillActive();
        Show();
        SetVisualState(BallVisualState.Held);

        // Start at center (x=0, slightly behind QB)
        Vector3 centerPos = qbSlot.transform.position + new Vector3(0, -1f, 0);
        transform.position = centerPos;

        attachTarget = null;
        activeTween = transform.DOMove(qbSlot.transform.position + heldOffset, Scaled(duration))
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject)
            .OnComplete(() => attachTarget = qbSlot.transform);
        return activeTween;
    }

    /// <summary>Ball moves from current position to carrier (run handoff).</summary>
    public Tween HandOffTo(BoardSlot carrierSlot, float duration = 0.3f)
    {
        KillActive();
        Detach();
        SetVisualState(BallVisualState.Held);

        activeTween = transform.DOMove(carrierSlot.transform.position + heldOffset, Scaled(duration))
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject)
            .OnComplete(() => attachTarget = carrierSlot.transform);
        return activeTween;
    }

    /// <summary>Spiral throw from current position to target. Parabolic scale fakes height; ball noses toward the target.</summary>
    public Sequence ThrowTo(Vector3 targetWorldPos, float duration = 0.5f)
    {
        KillActive();
        Detach();
        SetVisualState(BallVisualState.InFlight);

        float d = Scaled(duration);
        Vector3 dir = targetWorldPos - transform.position;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);

        var seq = DOTween.Sequence();
        seq.Append(transform.DOMove(targetWorldPos, d).SetEase(Ease.Linear));
        // Continuous parabolic height: scale = 1 + (peak-1)·sin(πt) — no midpoint kink
        seq.Join(DOVirtual.Float(0f, 1f, d, t =>
        {
            float s = 1f + (throwArcScale - 1f) * Mathf.Sin(Mathf.PI * t);
            transform.localScale = new Vector3(s, s, 1f);
        }).SetEase(Ease.Linear));
        seq.OnComplete(() =>
        {
            transform.localScale = Vector3.one;
            transform.rotation = Quaternion.identity;
        });
        seq.SetLink(gameObject);
        activeSequence = seq;
        return seq;
    }

    /// <summary>Attach ball to a slot — follows it each frame.</summary>
    public void AttachTo(BoardSlot slot)
    {
        if (slot == null) return;
        attachTarget = slot.transform;
        SetVisualState(BallVisualState.Held);
    }

    /// <summary>Attach ball to a transform directly.</summary>
    public void AttachTo(Transform target)
    {
        if (target == null) return;
        attachTarget = target;
        SetVisualState(BallVisualState.Held);
    }

    /// <summary>Stop following any slot.</summary>
    public void Detach()
    {
        attachTarget = null;
    }

    /// <summary>Ball hits the ground — tumble animation at position.</summary>
    public Tween DropIncomplete(Vector3 worldPos, float duration = 0.6f)
    {
        KillActive();
        Detach();
        SetVisualState(BallVisualState.LooseBall);

        // Small bounce: move slightly forward and down
        Vector3 bounceEnd = worldPos + new Vector3(Random.Range(-0.3f, 0.3f), Random.Range(-0.5f, 0f), 0);
        activeTween = transform.DOMove(bounceEnd, Scaled(duration))
            .SetEase(Ease.OutBounce)
            .SetLink(gameObject)
            .OnComplete(() => SetVisualState(BallVisualState.OnGround));
        return activeTween;
    }

    /// <summary>Ball pops loose from carrier — tumble animation.</summary>
    public Tween FumbleLoose(Vector3 fumbleWorldPos, float duration = 0.5f)
    {
        KillActive();
        Detach();
        SetVisualState(BallVisualState.LooseBall);

        // Ball flies out in a random direction from current pos
        Vector3 fumbleDir = new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(-0.3f, 0.3f), 0);
        Vector3 target = transform.position + fumbleDir;
        activeTween = transform.DOMove(target, Scaled(duration))
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject);
        return activeTween;
    }

    /// <summary>After fumble recovery, ball snaps back to recoverer.</summary>
    public Tween RecoverTo(BoardSlot slot, float duration = 0.3f)
    {
        KillActive();
        SetVisualState(BallVisualState.Held);

        activeTween = transform.DOMove(slot.transform.position + heldOffset, Scaled(duration))
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject)
            .OnComplete(() => attachTarget = slot.transform);
        return activeTween;
    }

    public void Hide()
    {
        KillActive();
        Detach();
        currentState = BallVisualState.Hidden;
        if (ballSprite != null)
            ballSprite.enabled = false;
    }

    public void Show()
    {
        if (ballSprite != null)
            ballSprite.enabled = true;
    }

    public BallVisualState CurrentState => currentState;

    // ── Internals ────────────────────────────────────────────────

    private void SetVisualState(BallVisualState state)
    {
        currentState = state;
        if (spriteSheet == null) return;

        switch (state)
        {
            case BallVisualState.Held:
                SetFrames(spriteSheet.heldFrames, spriteSheet.heldFPS);
                break;
            case BallVisualState.InFlight:
                SetFrames(spriteSheet.spiralFrames, spriteSheet.spiralFPS);
                break;
            case BallVisualState.LooseBall:
                // Prefer dedicated loose frames when authored; fall back to tumble
                if (spriteSheet.looseFrames != null && spriteSheet.looseFrames.Length > 0)
                    SetFrames(spriteSheet.looseFrames, spriteSheet.looseFPS);
                else
                    SetFrames(spriteSheet.tumbleFrames, spriteSheet.tumbleFPS);
                break;
            case BallVisualState.OnGround:
                SetFrames(null, 0);
                if (spriteSheet.onGroundSprite != null)
                    ballSprite.sprite = spriteSheet.onGroundSprite;
                break;
            case BallVisualState.Hidden:
                SetFrames(null, 0);
                break;
        }
    }

    private void SetFrames(Sprite[] frames, float fps)
    {
        activeFrames = frames;
        activeFPS = fps;
        frameTimer = 0;
        frameIndex = 0;
        if (frames != null && frames.Length > 0 && ballSprite != null)
            ballSprite.sprite = frames[0];
    }

    private void KillActive()
    {
        activeTween?.Kill();
        activeTween = null;
        activeSequence?.Kill();
        activeSequence = null;
        transform.localScale = Vector3.one;
        transform.rotation = Quaternion.identity;
    }
}
