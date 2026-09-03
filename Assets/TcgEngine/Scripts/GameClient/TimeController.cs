using UnityEngine;
using DG.Tweening;

namespace TcgEngine.Client
{
    /// <summary>
    /// Global animation speed controller for field-level animations (routes, ball, camera).
    /// Does NOT affect UI panels (PlayCallRevealPanel, LiveBallCardZone, etc.).
    /// Usage: TimeController.Instance.Scale(duration) or TimeController.Instance.ScaledWait(seconds).
    /// </summary>
    public class TimeController : MonoBehaviour
    {
        public static TimeController Instance { get; private set; }

        /// <summary>
        /// Current field time scale. 1 = normal, 0.2 = 20% speed (slow-mo), 2 = double speed.
        /// </summary>
        public float FieldTimeScale { get; private set; } = 1f;

        private Tween transitionTween;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            transitionTween?.Kill();
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Smoothly transition to a target time scale.
        /// targetScale: 0.2 = 20% speed (dramatic slow-mo), 1 = normal.
        /// transitionDur: real-time seconds for the lerp.
        /// </summary>
        public void SetBulletTime(float targetScale, float transitionDur = 0.3f)
        {
            targetScale = Mathf.Clamp(targetScale, 0.01f, 10f);
            transitionTween?.Kill();

            if (transitionDur <= 0.01f)
            {
                FieldTimeScale = targetScale;
                return;
            }

            transitionTween = DOTween.To(
                () => FieldTimeScale,
                x => FieldTimeScale = x,
                targetScale,
                transitionDur
            ).SetEase(Ease.InOutQuad).SetUpdate(true); // SetUpdate(true) = unscaled time
        }

        /// <summary>Snap back to normal speed (1.0).</summary>
        public void ResetSpeed(float transitionDur = 0.2f)
        {
            SetBulletTime(1f, transitionDur);
        }

        /// <summary>
        /// Scale a duration for field animations.
        /// Returns longer durations when slowed (FieldTimeScale &lt; 1).
        /// Example: Scale(0.5f) at FieldTimeScale=0.25 → 2.0f (4x slower).
        /// </summary>
        public float Scale(float duration)
        {
            if (FieldTimeScale <= 0.01f) return duration * 100f;
            return duration / FieldTimeScale;
        }

        /// <summary>
        /// Coroutine helper: returns WaitForSeconds with scaled duration.
        /// </summary>
        public WaitForSeconds ScaledWait(float seconds)
        {
            return new WaitForSeconds(Scale(seconds));
        }
    }
}
