using UnityEngine;
using UnityEngine.UI;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine.UI
{
    /// <summary>
    /// Always-visible top scoreboard strip — two rows:
    ///
    ///  Row 1:  [OFF 14]  1st Half  •  3rd down  •  8 plays left  [DEF 7]
    ///  Row 2:  OFF +12        NET +4        DEF -8
    ///          (shown during Resolution and LiveBall phases only)
    ///
    /// Inspector wiring (all optional — auto-built if left null):
    ///   teamAScore / teamBScore   — score Text fields
    ///   halfText                  — "1st Half" / "2nd Half"
    ///   downText                  — "1st down" / "2nd down" etc.
    ///   playsLeftText             — "8 plays left"
    ///   ballPositionBar           — RectTransform width tracks raw_ball_on 0–100
    ///   possessionArrowA/B        — GameObjects shown for the team with possession
    ///   offTallyText              — OFF subtotal (row 2)
    ///   netTallyText              — NET yardage (row 2)
    ///   defTallyText              — DEF subtotal (row 2)
    ///   tallyRow                  — parent row 2 GameObject to show/hide
    /// </summary>
    public class ScoreboardHUD : MonoBehaviour
    {
        [Header("Row 1 — Score")]
        public Text teamAScore;
        public Text teamBScore;

        [Header("Row 1 — Game Info")]
        public Text halfText;
        public Text downText;
        public Text playsLeftText;

        [Header("Row 1 — Field Position")]
        public RectTransform ballPositionBar;

        [Header("Row 1 — Possession")]
        public GameObject possessionArrowA;
        public GameObject possessionArrowB;

        [Header("Row 2 — Subtotals (shown during Resolution / LiveBall)")]
        public Text offTallyText;
        public Text netTallyText;
        public Text defTallyText;
        public GameObject tallyRow;

        // Self-contained canvas refs
        private bool selfBuilt = false;
        private Text builtScoreA, builtScoreB, builtHalf, builtDown, builtPlaysLeft;
        private Text builtOffTally, builtNetTally, builtDefTally;
        private GameObject builtTallyRow;

        void Awake()
        {
            bool hasAnyRef = teamAScore != null || halfText != null;
            if (!hasAnyRef)
                BuildSelfContainedHUD();
        }

        /// <summary>Called by GameFeedbackUI every time game data refreshes.</summary>
        public void Refresh(Game g, Player p0, Player p1)
        {
            if (g == null) return;

            // Resolve refs
            Text scoreA    = teamAScore    ?? builtScoreA;
            Text scoreB    = teamBScore    ?? builtScoreB;
            Text half      = halfText      ?? builtHalf;
            Text down      = downText      ?? builtDown;
            Text playsLeft = playsLeftText ?? builtPlaysLeft;
            Text offTally  = offTallyText  ?? builtOffTally;
            Text netTally  = netTallyText  ?? builtNetTally;
            Text defTally  = defTallyText  ?? builtDefTally;
            GameObject row2 = tallyRow    ?? builtTallyRow;

            // --- Score ---
            if (scoreA != null) scoreA.text = p0 != null ? p0.points.ToString() : "0";
            if (scoreB != null) scoreB.text = p1 != null ? p1.points.ToString() : "0";

            // --- Half ---
            if (half != null) half.text = g.current_half == 1 ? "1st Half" : "2nd Half";

            // --- Down (no yardage — always goal to go) ---
            if (down != null)
            {
                string downStr = g.current_down switch
                {
                    1 => "1st down",
                    2 => "2nd down",
                    3 => "3rd down",
                    4 => "4th down",
                    _ => $"{g.current_down}th down"
                };
                down.text = downStr;
            }

            // --- Plays left ---
            if (playsLeft != null) playsLeft.text = $"{g.plays_left_in_half} plays left";

            // --- Field position bar ---
            if (ballPositionBar != null)
            {
                float t = Mathf.Clamp01(g.raw_ball_on / 100f);
                Vector2 max = ballPositionBar.anchorMax;
                max.x = t;
                ballPositionBar.anchorMax = max;
            }

            // --- Possession arrows ---
            int offId = g.current_offensive_player?.player_id ?? -1;
            if (possessionArrowA != null) possessionArrowA.SetActive(offId == 0);
            if (possessionArrowB != null) possessionArrowB.SetActive(offId == 1);

            // --- Row 2: subtotals ---
            bool showTally = g.phase == GamePhase.Resolution || g.phase == GamePhase.LiveBall
                          || g.phase == GamePhase.SlotSpin;

            if (row2 != null) row2.SetActive(showTally);

            if (showTally && p0 != null && p1 != null)
                RefreshTally(g, p0, p1, offTally, netTally, defTally);
        }

        private void RefreshTally(Game g, Player p0, Player p1,
                                   Text offText, Text netText, Text defText)
        {
            bool p0isOff = g.current_offensive_player?.player_id == 0;
            Player offP = p0isOff ? p0 : p1;
            Player defP = p0isOff ? p1 : p0;

            // Determine active play type from offensive player's selection
            PlayType pt = offP?.SelectedPlay ?? PlayType.Run;

            int offTotal = 0, defTotal = 0;

            // Coach base
            if (offP?.head_coach?.baseOffenseYardage != null &&
                offP.head_coach.baseOffenseYardage.ContainsKey(pt))
                offTotal += offP.head_coach.baseOffenseYardage[pt];

            // Offensive card contributions
            if (offP != null)
            {
                foreach (var c in offP.cards_board)
                {
                    if (c.Data == null) continue;
                    offTotal += pt switch
                    {
                        PlayType.Run       => c.Data.run_bonus,
                        PlayType.ShortPass => c.Data.short_pass_bonus,
                        PlayType.LongPass  => c.Data.deep_pass_bonus,
                        _                  => 0
                    };
                    // Add any active status bonuses
                    offTotal += pt switch
                    {
                        PlayType.Run       => c.GetStatusValue(StatusType.AddedRunBonus),
                        PlayType.ShortPass => c.GetStatusValue(StatusType.AddedShortPassBonus),
                        PlayType.LongPass  => c.GetStatusValue(StatusType.AddedDeepPassBonus),
                        _                  => 0
                    };
                }
            }

            // Defensive card contributions
            if (defP != null)
            {
                foreach (var c in defP.cards_board)
                {
                    if (c.Data == null) continue;
                    defTotal += pt switch
                    {
                        PlayType.Run       => c.Data.run_coverage_bonus,
                        PlayType.ShortPass => c.Data.short_pass_coverage_bonus,
                        PlayType.LongPass  => c.Data.deep_pass_coverage_bonus,
                        _                  => 0
                    };
                    defTotal += pt switch
                    {
                        PlayType.Run       => c.GetStatusValue(StatusType.AddedRunCoverageBonus),
                        PlayType.ShortPass => c.GetStatusValue(StatusType.AddedShortPassCoverageBonus),
                        PlayType.LongPass  => c.GetStatusValue(StatusType.AddedDeepPassCoverageBonus),
                        _                  => 0
                    };
                }
            }

            int net = offTotal - defTotal;

            if (offText != null)
            {
                offText.text = $"OFF  +{offTotal}";
                offText.color = new Color(0.3f, 1f, 0.45f);
            }
            if (defText != null)
            {
                defText.text = $"DEF  -{defTotal}";
                defText.color = new Color(1f, 0.45f, 0.45f);
            }
            if (netText != null)
            {
                netText.text = net >= 0 ? $"NET  +{net}" : $"NET  {net}";
                netText.color = net >= 0 ? Color.white : new Color(1f, 0.6f, 0.3f);
            }
        }

        // -----------------------------------------------------------------------
        // Self-contained HUD builder
        // -----------------------------------------------------------------------
        private void BuildSelfContainedHUD()
        {
            if (selfBuilt) return;
            selfBuilt = true;

            Canvas c = gameObject.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 50;
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // ---- Row 1 background ----
            GameObject row1 = new GameObject("ScoreRow1");
            row1.transform.SetParent(transform, false);
            Image bg1 = row1.AddComponent<Image>();
            bg1.color = new Color(0.05f, 0.05f, 0.08f, 0.90f);
            RectTransform row1RT = row1.GetComponent<RectTransform>();
            row1RT.anchorMin = new Vector2(0f, 0.94f);
            row1RT.anchorMax = new Vector2(1f, 1f);
            row1RT.offsetMin = row1RT.offsetMax = Vector2.zero;

            // Team A score (left)
            builtScoreA = MakeText(row1.transform, "ScoreA", font, 20, Color.white,
                new Vector2(0f, 0f), new Vector2(0.14f, 1f), TextAnchor.MiddleCenter);
            builtScoreA.text = "OFF  0";

            // Half (center-left)
            builtHalf = MakeText(row1.transform, "Half", font, 14, new Color(0.9f, 0.85f, 0.5f),
                new Vector2(0.25f, 0f), new Vector2(0.42f, 1f), TextAnchor.MiddleCenter);
            builtHalf.text = "1st Half";

            // Down (center)
            builtDown = MakeText(row1.transform, "Down", font, 16, Color.white,
                new Vector2(0.40f, 0f), new Vector2(0.60f, 1f), TextAnchor.MiddleCenter);
            builtDown.text = "1st down";

            // Plays left (center-right)
            builtPlaysLeft = MakeText(row1.transform, "PlaysLeft", font, 13, new Color(0.7f, 0.7f, 0.7f),
                new Vector2(0.60f, 0f), new Vector2(0.78f, 1f), TextAnchor.MiddleCenter);
            builtPlaysLeft.text = "11 plays left";

            // Team B score (right)
            builtScoreB = MakeText(row1.transform, "ScoreB", font, 20, Color.white,
                new Vector2(0.86f, 0f), new Vector2(1f, 1f), TextAnchor.MiddleCenter);
            builtScoreB.text = "DEF  0";

            // ---- Row 2 background ----
            builtTallyRow = new GameObject("ScoreRow2");
            builtTallyRow.transform.SetParent(transform, false);
            Image bg2 = builtTallyRow.AddComponent<Image>();
            bg2.color = new Color(0.03f, 0.03f, 0.07f, 0.88f);
            RectTransform row2RT = builtTallyRow.GetComponent<RectTransform>();
            row2RT.anchorMin = new Vector2(0f, 0.885f);
            row2RT.anchorMax = new Vector2(1f, 0.94f);
            row2RT.offsetMin = row2RT.offsetMax = Vector2.zero;

            // Separator line
            GameObject sep = new GameObject("Sep");
            sep.transform.SetParent(transform, false);
            Image sepImg = sep.AddComponent<Image>();
            sepImg.color = new Color(0.35f, 0.35f, 0.45f, 0.6f);
            RectTransform sepRT = sep.GetComponent<RectTransform>();
            sepRT.anchorMin = new Vector2(0f, 0.9385f);
            sepRT.anchorMax = new Vector2(1f, 0.940f);
            sepRT.offsetMin = sepRT.offsetMax = Vector2.zero;

            builtOffTally = MakeText(builtTallyRow.transform, "OffTally", font, 13, new Color(0.3f, 1f, 0.45f),
                new Vector2(0.05f, 0f), new Vector2(0.35f, 1f), TextAnchor.MiddleCenter);
            builtOffTally.text = "OFF  +0";

            builtNetTally = MakeText(builtTallyRow.transform, "NetTally", font, 14, Color.white,
                new Vector2(0.38f, 0f), new Vector2(0.62f, 1f), TextAnchor.MiddleCenter);
            builtNetTally.text = "NET  +0";

            builtDefTally = MakeText(builtTallyRow.transform, "DefTally", font, 13, new Color(1f, 0.45f, 0.45f),
                new Vector2(0.65f, 0f), new Vector2(0.95f, 1f), TextAnchor.MiddleCenter);
            builtDefTally.text = "DEF  -0";

            builtTallyRow.SetActive(false);
        }

        private Text MakeText(Transform parent, string name, Font font, int size, Color color,
                              Vector2 anchorMin, Vector2 anchorMax, TextAnchor anchor)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.supportRichText = false;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return t;
        }
    }
}
