using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using Assets.TcgEngine.Scripts.Gameplay;

namespace TcgEngine
{
    /// <summary>
    /// In-game debug overlay showing live game state.
    /// Add this MonoBehaviour to a Canvas GameObject in the Game scene.
    /// Set the Text reference in the Inspector, or it will auto-create one.
    /// Toggle visibility with the backtick key (`).
    /// </summary>
    public class DebugHUD : MonoBehaviour
    {
        [Header("UI")]
        public Text debugText;  // Assign a UI Text in the Inspector, or leave null to auto-create

        private Canvas canvas;
        private bool visible = true;

        void Awake()
        {
            if (debugText == null)
                CreateFallbackText();
        }

        void Start()
        {
            if (canvas != null)
                canvas.sortingOrder = 999;
        }

        void Update()
        {
            // Toggle with backtick
            if (Input.GetKeyDown(KeyCode.BackQuote))
            {
                visible = !visible;
                if (debugText != null)
                    debugText.transform.parent.gameObject.SetActive(visible);
            }

            if (!visible) return;

            Game g = GameClient.Get()?.GetGameData();
            if (g == null)
            {
                SetText("<color=yellow>Waiting for game data...</color>");
                return;
            }

            Player p0 = g.players != null && g.players.Length > 0 ? g.players[0] : null;
            Player p1 = g.players != null && g.players.Length > 1 ? g.players[1] : null;

            string offense = g.current_offensive_player != null
                ? $"P{g.current_offensive_player.player_id}"
                : "?";

            string p0play = p0 != null ? p0.SelectedPlay.ToString() : "-";
            string p1play = p1 != null ? p1.SelectedPlay.ToString() : "-";

            string slotResult = "—";
            if (g.current_slot_data?.Results != null && g.current_slot_data.Results.Count > 0)
            {
                var parts = new System.Collections.Generic.List<string>();
                foreach (var reel in g.current_slot_data.Results)
                    parts.Add($"[{reel.Top?.IconId}|{reel.Middle?.IconId}|{reel.Bottom?.IconId}]");
                slotResult = string.Join("  ", parts);
            }

            // Coach yardage for current offensive player
            string coachLine = "Coach: n/a";
            Player offPlayer = g.current_offensive_player;
            if (offPlayer?.head_coach?.baseOffenseYardage != null)
            {
                var oy = offPlayer.head_coach.baseOffenseYardage;
                coachLine = $"Coach Off — Run:{(oy.ContainsKey(PlayType.Run) ? oy[PlayType.Run].ToString() : "?")}  " +
                            $"Short:{(oy.ContainsKey(PlayType.ShortPass) ? oy[PlayType.ShortPass].ToString() : "?")}  " +
                            $"Long:{(oy.ContainsKey(PlayType.LongPass) ? oy[PlayType.LongPass].ToString() : "?")}";
            }

            // Coach position limits for both players
            string SchemeStr(Player p)
            {
                if (p?.head_coach?.positional_Scheme == null) return "n/a";
                var s = p.head_coach.positional_Scheme;
                string Get(PlayerPositionGrp pos) => s.ContainsKey(pos) ? s[pos].pos_max.ToString() : "?";
                return $"QB:{Get(PlayerPositionGrp.QB)} RB/TE:{Get(PlayerPositionGrp.RB_TE)} WR:{Get(PlayerPositionGrp.WR)} OL:{Get(PlayerPositionGrp.OL)} | " +
                       $"DL:{Get(PlayerPositionGrp.DL)} LB:{Get(PlayerPositionGrp.LB)} DB:{Get(PlayerPositionGrp.DB)} K:{Get(PlayerPositionGrp.K)}";
            }
            string schemeLine0 = $"P0 Scheme: {SchemeStr(p0)}";
            string schemeLine1 = $"P1 Scheme: {SchemeStr(p1)}";

            // Card bonus totals on board — show offensive stats for offense, coverage stats for defense
            string cardBonusLine = "Cards: —";
            if (p0 != null && p1 != null)
            {
                bool p0isOff = g.current_offensive_player != null && g.current_offensive_player.player_id == 0;
                Player offP = p0isOff ? p0 : p1;
                Player defP = p0isOff ? p1 : p0;

                int offRun = 0, offShort = 0, offDeep = 0;
                int defRunCov = 0, defShortCov = 0, defDeepCov = 0;
                int offNullCount = 0, defNullCount = 0;

                foreach (var c in offP.cards_board)
                {
                    if (c.Data == null) { offNullCount++; continue; }
                    offRun += c.Data.run_bonus; offShort += c.Data.short_pass_bonus; offDeep += c.Data.deep_pass_bonus;
                }
                foreach (var c in defP.cards_board)
                {
                    if (c.Data == null) { defNullCount++; continue; }
                    defRunCov += c.Data.run_coverage_bonus; defShortCov += c.Data.short_pass_coverage_bonus; defDeepCov += c.Data.deep_pass_coverage_bonus;
                }
                string nullWarn = (offNullCount + defNullCount) > 0 ? $" [!{offNullCount + defNullCount} null data]" : "";
                cardBonusLine = $"OFF board({offP.cards_board.Count}): Run+{offRun} Sht+{offShort} Lng+{offDeep}  |  " +
                                $"DEF board({defP.cards_board.Count}): RunCov+{defRunCov} ShtCov+{defShortCov} LngCov+{defDeepCov}{nullWarn}";
            }

            string txt =
                $"<b>=== DEBUG HUD === (` to hide)</b>\n" +
                $"Phase: <b>{g.phase}</b>   State: {g.state}\n" +
                $"Half: {g.current_half}   Plays left: {g.plays_left_in_half}\n" +
                $"Down: <b>{g.current_down}</b>   Ball on: <b>{g.raw_ball_on}</b>   To go: {g.yardage_to_go}\n" +
                $"Offense: <b>{offense}</b>   Turn: {g.turn_count}\n" +
                $"Score — P0: <b>{(p0 != null ? p0.points.ToString() : "?")}</b>   " +
                         $"P1: <b>{(p1 != null ? p1.points.ToString() : "?")}</b>\n" +
                $"Plays — P0: {p0play}   P1: {p1play}\n" +
                $"Hand — P0: {(p0 != null ? p0.cards_hand.Count.ToString() : "?")}   " +
                       $"P1: {(p1 != null ? p1.cards_hand.Count.ToString() : "?")}\n" +
                $"Board — P0: {(p0 != null ? p0.cards_board.Count.ToString() : "?")}   " +
                        $"P1: {(p1 != null ? p1.cards_board.Count.ToString() : "?")}\n" +
                $"Last yardage: {g.last_play_yardage}   Last play: {g.last_play_type}\n" +
                $"{coachLine}\n" +
                $"{schemeLine0}\n" +
                $"{schemeLine1}\n" +
                $"{cardBonusLine}\n" +
                $"Slots: {slotResult}";

            SetText(txt);
        }

        private void SetText(string msg)
        {
            if (debugText != null)
                debugText.text = msg;
        }

        private void CreateFallbackText()
        {
            // Build a self-contained Canvas → Panel → Text stack at runtime
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();

            GameObject panel = new GameObject("DebugPanel");
            panel.transform.SetParent(transform, false);

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.6f);

            RectTransform rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0.55f);
            rt.anchorMax = new Vector2(0.38f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            GameObject textObj = new GameObject("DebugText");
            textObj.transform.SetParent(panel.transform, false);

            debugText = textObj.AddComponent<Text>();
            debugText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            debugText.fontSize = 13;
            debugText.color = Color.white;
            debugText.supportRichText = true;
            debugText.alignment = TextAnchor.UpperLeft;

            RectTransform trt = textObj.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(6, 4);
            trt.offsetMax = new Vector2(-6, -4);
        }
    }
}
