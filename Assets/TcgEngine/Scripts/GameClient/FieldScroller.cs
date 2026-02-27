using UnityEngine;
using TcgEngine.Client;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine.Client
{
    /// <summary>
    /// Scrolls the field panel vertically whenever raw_ball_on changes,
    /// keeping the current line of scrimmage centered on screen.
    ///
    /// Setup:
    ///   1. Attach this component to any GameObject in the Game scene.
    ///   2. Assign the fieldPanel RectTransform (the panel that contains the field
    ///      background, BoardSlots, and any other field art).
    ///   3. Tune pixelsPerYard so a 10-yard gain looks right on your canvas.
    ///
    /// Y=0 on the panel = yard 50 (midfield). The formula shifts the panel by
    /// (raw_ball_on - 50) * pixelsPerYard so the LOS always lands at screen center.
    ///
    /// Because BoardSlots are children of the fieldPanel, scrolling the panel
    /// also moves all slots and (via BoardCard.GetTargetPos) all played cards.
    /// </summary>
    public class FieldScroller : MonoBehaviour
    {
        [Tooltip("The RectTransform of the field panel to scroll.")]
        public RectTransform fieldPanel;

        [Tooltip("How many UI units to scroll per yard gained/lost.")]
        public float pixelsPerYard = 10f;

        [Tooltip("Lerp speed for the scroll animation.")]
        public float scrollSpeed = 2.5f;

        public static FieldScroller Instance { get; private set; }

        private float targetY;
        private int lastBallOn = -1;

        void Awake()
        {
            Instance = this;
        }

        // Y=0 on the fieldPanel = yard 50 (midfield). Offense is at screen bottom (negative Y),
        // defense at screen top (positive Y). To bring a yard line to screen center we move the
        // panel in the OPPOSITE direction to that yard line's position on the panel.
        //
        //   ball at 25 (own 25) → (50-25)*ppy = +1000 → panel UP   → own 25 at center ✓
        //   ball at 50 (midfield)→ (50-50)*ppy = 0    → no shift   → midfield at center ✓
        //   ball at 75 (opp 25) → (50-75)*ppy = -1000 → panel DOWN → opp 25 at center ✓
        private float TargetYForBallOn(int ballOn) => (50 - ballOn) * pixelsPerYard;

        void Start()
        {
            if (fieldPanel == null) return;

            // Snap immediately so there's no lerp from Y=0 on startup.
            Game g = GameClient.Get()?.GetGameData();
            int startBallOn = g != null ? g.raw_ball_on : 25;
            lastBallOn = startBallOn;
            targetY = TargetYForBallOn(startBallOn);

            Vector2 ap = fieldPanel.anchoredPosition;
            ap.y = targetY;
            fieldPanel.anchoredPosition = ap;
        }

        void Update()
        {
            if (fieldPanel == null) return;

            Game g = GameClient.Get()?.GetGameData();
            if (g == null) return;

            if (g.raw_ball_on != lastBallOn)
            {
                lastBallOn = g.raw_ball_on;
                targetY = TargetYForBallOn(g.raw_ball_on);
            }

            Vector2 ap = fieldPanel.anchoredPosition;
            ap.y = Mathf.Lerp(ap.y, targetY, scrollSpeed * Time.deltaTime);
            fieldPanel.anchoredPosition = ap;
        }
    }
}
