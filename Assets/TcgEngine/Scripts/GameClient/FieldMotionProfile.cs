using UnityEngine;

namespace TcgEngine.Client
{
    /// <summary>
    /// Single source for how fast player tokens move on the field. Presentation speeds, NOT real
    /// football speeds: the screen shows ~31 yards, so real 9 yd/s sprints are unreadable.
    /// Optional asset at Resources/FieldMotionProfile; built-in defaults otherwise.
    /// Tune live with DevTuningPanel (F2), then copy good values into the asset.
    /// </summary>
    [CreateAssetMenu(fileName = "FieldMotionProfile", menuName = "First&Long/Field Motion Profile")]
    public class FieldMotionProfile : ScriptableObject
    {
        [Header("Speeds (yards per second)")]
        public float walkYps = 1.5f;
        public float jogYps = 3.0f;
        public float sprintYps = 5.5f;

        [Header("Feel")]
        [Tooltip("Yards/sec^2. Lower = heavier, slower off the mark.")]
        public float accel = 9f;
        [Tooltip("Yards/sec^2 when braking into a stop or cut.")]
        public float decel = 12f;
        [Tooltip("A move that starts and ends at rest never takes less than this (seconds).")]
        public float minMoveDuration = 0.45f;
        [Tooltip("Same floor for LiveBall yardage moves, so a +3 is watchable.")]
        public float minLiveBallMoveDuration = 0.9f;
        [Tooltip("Per-token speed spread, +/- this fraction. Stops 11 players moving as one.")]
        public float speedVariance = 0.08f;
        [Tooltip("Defender reaction delay range (seconds).")]
        public float reactionMin = 0.05f;
        public float reactionMax = 0.25f;

        [Header("Between plays")]
        [Tooltip("Tokens speed up past a jog if needed to reach the huddle/formation within this many seconds.")]
        public float maxFormationTime = 3.5f;
        [Tooltip("Random start delay spread when jogging back to the huddle (seconds).")]
        public float huddleStagger = 0.5f;

        private static FieldMotionProfile current;

        /// <summary>Runtime copy (never the asset itself, so play-mode slider edits don't dirty it).</summary>
        public static FieldMotionProfile Current
        {
            get
            {
                if (current == null)
                {
                    FieldMotionProfile asset = Resources.Load<FieldMotionProfile>("FieldMotionProfile");
                    current = asset != null ? Instantiate(asset) : CreateInstance<FieldMotionProfile>();
                    current.hideFlags = HideFlags.HideAndDontSave;
                }
                return current;
            }
        }
    }
}
