#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using TcgEngine;
using Assets.TcgEngine.Scripts.Gameplay;

/// <summary>
/// Visual route designer — draw post-snap movement paths relative to a slot's starting position.
/// Menu: First&Long → Route Designer
///
/// Grid is centered on (0,0) = slot's position at snap.
/// Y axis: positive = toward opponent end zone (forward), negative = backward.
/// X axis: positive = right, negative = left (in yards).
///
/// Click cells in order to lay down waypoints. Lines connect them.
/// Routes are position-agnostic — the same "Slant" asset works for any slot.
/// </summary>
public class RouteDesignerWindow : EditorWindow
{
    // Grid spans -HALF_X to +HALF_X yards wide, -HALF_Y_BACK to +HALF_Y_FWD yards deep
    private const int HALF_X      = 10;   // yards left/right
    private const int HALF_Y_BACK = 5;    // yards backward
    private const int HALF_Y_FWD  = 20;   // yards forward
    private const int COLS        = HALF_X * 2 + 1;             // 21
    private const int ROWS        = HALF_Y_BACK + HALF_Y_FWD + 1; // 26
    private const int CENTER_COL  = HALF_X;   // col index of x=0
    private const int CENTER_ROW  = HALF_Y_FWD; // row index of y=0 (LOS / starting pos)
    private const float CELL_W    = 22f;
    private const float CELL_H    = 18f;

    // Current route being drawn
    private PlayerPositionGrp targetPosGroup = PlayerPositionGrp.WR;
    private int targetSlotIndex = 0;
    private float timePerWaypoint = 0.2f;
    private readonly List<Vector2Int> waypoints = new List<Vector2Int>(); // (col, row) grid coords

    // All finished routes (can be multi-position on one asset)
    private readonly List<DrawnRoute> allRoutes = new List<DrawnRoute>();

    private string assetPath = "Assets/TcgEngine/Resources/Routes/new_route.asset";
    private Vector2 scroll;

    private static readonly (PlayerPositionGrp pos, char letter, Color color)[] PosInfo =
    {
        (PlayerPositionGrp.QB,    'Q', new Color(1.00f, 0.85f, 0.00f)),
        (PlayerPositionGrp.WR,    'W', new Color(0.00f, 0.85f, 1.00f)),
        (PlayerPositionGrp.RB_TE, 'R', new Color(0.20f, 0.90f, 0.30f)),
        (PlayerPositionGrp.OL,    'O', new Color(1.00f, 0.55f, 0.00f)),
        (PlayerPositionGrp.K,     'K', new Color(0.80f, 0.20f, 0.90f)),
        (PlayerPositionGrp.DL,    'D', new Color(1.00f, 0.20f, 0.20f)),
        (PlayerPositionGrp.LB,    'L', new Color(1.00f, 0.55f, 0.55f)),
        (PlayerPositionGrp.DB,    'B', new Color(0.55f, 0.55f, 1.00f)),
    };

    private static Dictionary<PlayerPositionGrp, char>  _letterOf;
    private static Dictionary<PlayerPositionGrp, Color> _colorOf;

    // -----------------------------------------------------------------------

    [MenuItem("First&Long/Route Designer")]
    public static void ShowWindow() =>
        GetWindow<RouteDesignerWindow>("Route Designer");

    void OnEnable()
    {
        BuildLookups();
        // Start scroll so the center row (slot position) is visible
        scroll = new Vector2(0, Mathf.Max(0, CENTER_ROW * CELL_H - 200f));
    }

    // -----------------------------------------------------------------------

    void OnGUI()
    {
        GUILayout.Label("Route Designer  (relative to slot starting position)", EditorStyles.boldLabel);
        EditorGUILayout.Space(3);

        DrawToolbar();
        DrawRouteList();

        EditorGUILayout.Space(3);
        EditorGUILayout.HelpBox(
            "Click cells to draw waypoints in order. Each click = next step in the route.\n" +
            "Orange row = slot's starting position (0,0). Up = forward. Down = backward. Left/Right = lateral.\n" +
            "Click a placed waypoint again to remove it. 'Finish Route' locks it in.",
            MessageType.None);
        EditorGUILayout.Space(2);

        DrawGrid();

        EditorGUILayout.Space(4);
        DrawBottomControls();
    }

    // -----------------------------------------------------------------------

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Position:", GUILayout.Width(55));
        foreach (var info in PosInfo)
        {
            bool selected = targetPosGroup == info.pos;
            GUI.backgroundColor = selected ? info.color : new Color(0.55f, 0.55f, 0.55f);
            if (GUILayout.Button($"{info.letter}", EditorStyles.miniButton, GUILayout.Width(28), GUILayout.Height(22)))
                targetPosGroup = info.pos;
        }
        GUI.backgroundColor = Color.white;

        GUILayout.Space(8);
        GUILayout.Label("Slot:", GUILayout.Width(30));
        targetSlotIndex = EditorGUILayout.IntField(targetSlotIndex, GUILayout.Width(28));
        targetSlotIndex = Mathf.Max(0, targetSlotIndex);

        GUILayout.Space(8);
        GUILayout.Label("Secs/pt:", GUILayout.Width(50));
        timePerWaypoint = EditorGUILayout.FloatField(timePerWaypoint, GUILayout.Width(38));
        timePerWaypoint = Mathf.Max(0.05f, timePerWaypoint);

        EditorGUILayout.EndHorizontal();
    }

    // -----------------------------------------------------------------------

    private void DrawRouteList()
    {
        if (allRoutes.Count == 0 && waypoints.Count == 0) return;

        EditorGUILayout.BeginVertical("box");
        GUILayout.Label($"Routes in this asset  ({allRoutes.Count} finished" +
                        (waypoints.Count > 0 ? $", 1 drawing" : "") + ")", EditorStyles.miniLabel);

        for (int i = 0; i < allRoutes.Count; i++)
        {
            var r = allRoutes[i];
            EditorGUILayout.BeginHorizontal();
            GUI.contentColor = _colorOf.ContainsKey(r.posGroup) ? _colorOf[r.posGroup] : Color.white;
            char letter = _letterOf.ContainsKey(r.posGroup) ? _letterOf[r.posGroup] : '?';
            GUILayout.Label($"  {letter}{r.slotIndex}: {r.waypoints.Count} pts  ({r.timePerWaypoint:F2}s/pt)", GUILayout.Width(160));
            GUI.contentColor = Color.white;
            bool removed = GUILayout.Button("✕", GUILayout.Width(22));
            EditorGUILayout.EndHorizontal();
            if (removed)
            {
                allRoutes.RemoveAt(i);
                Repaint();
                break;
            }
        }

        if (waypoints.Count > 0)
        {
            char curLetter = _letterOf.ContainsKey(targetPosGroup) ? _letterOf[targetPosGroup] : '?';
            GUILayout.Label($"  Drawing: {curLetter}{targetSlotIndex}  ({waypoints.Count} pts placed)", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndVertical();
    }

    // -----------------------------------------------------------------------

    private void DrawGrid()
    {
        float gridW   = 44f + COLS * CELL_W;
        float gridH   = ROWS * CELL_H;
        float visible = Mathf.Min(gridH + 4f, 480f);

        Rect scrollRect = GUILayoutUtility.GetRect(gridW, visible);
        scroll = GUI.BeginScrollView(scrollRect, scroll, new Rect(0, 0, gridW, gridH));

        // Draw cells
        for (int r = 0; r < ROWS; r++)
        {
            int yardsForward = CENTER_ROW - r; // positive = toward opponent end zone
            float y = r * CELL_H;

            // Row label
            string rowLabel = yardsForward == 0 ? " ★0" : yardsForward.ToString("+#;-#;0");
            GUI.Label(new Rect(0, y + 1f, 36f, CELL_H), rowLabel, EditorStyles.miniLabel);

            for (int c = 0; c < COLS; c++)
            {
                int yardsRight = c - CENTER_COL; // positive = right, negative = left
                float x = 38f + c * CELL_W;
                Rect cellRect = new Rect(x, y, CELL_W - 1f, CELL_H - 1f);

                // Background color
                Color bg = GetCellBg(r, c, yardsForward, yardsRight);

                // Check current waypoints
                int wpIndex = waypoints.FindIndex(wp => wp.x == c && wp.y == r);
                bool isCurrentWP = wpIndex >= 0;

                // Check finished routes
                int finRouteIdx = -1;
                int finWpIdx = -1;
                for (int ri = 0; ri < allRoutes.Count; ri++)
                {
                    int fi = allRoutes[ri].waypoints.FindIndex(wp => wp.x == c && wp.y == r);
                    if (fi >= 0) { finRouteIdx = ri; finWpIdx = fi; break; }
                }

                string label = "";
                if (isCurrentWP)
                {
                    bg = _colorOf.ContainsKey(targetPosGroup) ? _colorOf[targetPosGroup] : Color.white;
                    label = (wpIndex + 1).ToString();
                }
                else if (finRouteIdx >= 0)
                {
                    var fr = allRoutes[finRouteIdx];
                    Color fc = _colorOf.ContainsKey(fr.posGroup) ? _colorOf[fr.posGroup] : Color.gray;
                    bg = Color.Lerp(fc, Color.gray, 0.35f);
                    label = (finWpIdx + 1).ToString();
                }

                GUI.backgroundColor = bg;

                if (GUI.Button(cellRect, label, EditorStyles.miniButton))
                {
                    if (isCurrentWP)
                        waypoints.RemoveAt(wpIndex);
                    else
                        waypoints.Add(new Vector2Int(c, r));
                    Repaint();
                }
            }

            // X-axis label at center row
            if (r == CENTER_ROW)
            {
                for (int c = 0; c < COLS; c += 2)
                {
                    int yardsRight = c - CENTER_COL;
                    if (yardsRight == 0) continue;
                    float x = 38f + c * CELL_W;
                    GUI.Label(new Rect(x, y + CELL_H, CELL_W * 2, CELL_H - 2), yardsRight.ToString("+#;-#;0"), EditorStyles.centeredGreyMiniLabel);
                }
            }
        }

        // Draw route lines
        if (waypoints.Count >= 2)
        {
            Color lc = _colorOf.ContainsKey(targetPosGroup) ? _colorOf[targetPosGroup] : Color.white;
            DrawLines(waypoints, lc, false);
        }
        foreach (var route in allRoutes)
        {
            if (route.waypoints.Count >= 2)
            {
                Color lc = _colorOf.ContainsKey(route.posGroup) ? _colorOf[route.posGroup] : Color.gray;
                DrawLines(route.waypoints, Color.Lerp(lc, Color.gray, 0.35f), true);
            }
        }

        GUI.backgroundColor = Color.white;
        GUI.EndScrollView();
    }

    private void DrawLines(List<Vector2Int> pts, Color color, bool dashed)
    {
        Handles.BeginGUI();
        Color old = Handles.color;
        Handles.color = color;
        for (int i = 0; i < pts.Count - 1; i++)
        {
            Vector2 from = CellCenter(pts[i]);
            Vector2 to   = CellCenter(pts[i + 1]);
            Handles.DrawLine(new Vector3(from.x, from.y), new Vector3(to.x, to.y));
            Handles.DrawLine(new Vector3(from.x + 1, from.y), new Vector3(to.x + 1, to.y));
        }
        Handles.color = old;
        Handles.EndGUI();
    }

    private Vector2 CellCenter(Vector2Int gp) =>
        new Vector2(38f + gp.x * CELL_W + CELL_W * 0.5f, gp.y * CELL_H + CELL_H * 0.5f);

    private Color GetCellBg(int r, int c, int yardsForward, int yardsRight)
    {
        if (r == CENTER_ROW)
            return new Color(0.95f, 0.80f, 0.40f); // orange = starting position row
        if (c == CENTER_COL)
            return yardsForward > 0
                ? new Color(0.70f, 0.88f, 0.70f)   // green center column (forward)
                : new Color(0.85f, 0.72f, 0.72f);  // reddish center column (backward)
        return yardsForward > 0
            ? new Color(0.78f, 0.86f, 0.98f)       // blue = forward territory
            : new Color(0.88f, 0.88f, 0.88f);      // grey = backfield
    }

    // -----------------------------------------------------------------------

    private void DrawBottomControls()
    {
        EditorGUILayout.BeginHorizontal();

        GUI.enabled = waypoints.Count >= 1;
        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
        if (GUILayout.Button("Finish Route", GUILayout.Height(26)))
        {
            allRoutes.Add(new DrawnRoute
            {
                posGroup = targetPosGroup,
                slotIndex = targetSlotIndex,
                timePerWaypoint = timePerWaypoint,
                waypoints = new List<Vector2Int>(waypoints)
            });
            waypoints.Clear();
            Repaint();
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        GUI.enabled = waypoints.Count > 0;
        if (GUILayout.Button("Undo Point", GUILayout.Width(90), GUILayout.Height(26)))
        {
            waypoints.RemoveAt(waypoints.Count - 1);
            Repaint();
        }
        GUI.enabled = true;

        if (GUILayout.Button("Clear Drawing", GUILayout.Width(100), GUILayout.Height(26)))
        {
            waypoints.Clear();
            Repaint();
        }

        if (GUILayout.Button("Clear All", GUILayout.Width(80), GUILayout.Height(26)))
        {
            if (EditorUtility.DisplayDialog("Clear All", "Remove all routes?", "Clear", "Cancel"))
            {
                waypoints.Clear();
                allRoutes.Clear();
                Repaint();
            }
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        assetPath = EditorGUILayout.TextField("Asset Path", assetPath);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Load RouteData", GUILayout.Height(26)))
            LoadAsset();

        GUI.enabled = allRoutes.Count > 0;
        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
        if (GUILayout.Button("Save RouteData", GUILayout.Height(26)))
            SaveAsset();
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        EditorGUILayout.EndHorizontal();
    }

    // -----------------------------------------------------------------------
    // Asset I/O

    private void SaveAsset()
    {
        // Each waypoint in a route becomes a keyframe.
        // All routes in this asset share the same keyframe list,
        // with their waypoints merged at matching time indices.

        int maxPts = 0;
        foreach (var r in allRoutes)
            maxPts = Mathf.Max(maxPts, r.waypoints.Count);

        var keyframes = new List<RouteKeyframe>();

        for (int i = 0; i < maxPts; i++)
        {
            var frame = new RouteKeyframe { waypoints = new List<SlotWaypoint>() };
            float maxTime = 0f;

            foreach (var r in allRoutes)
            {
                if (i >= r.waypoints.Count) continue;

                Vector2Int gp = r.waypoints[i];
                float deltaYards = CENTER_ROW - gp.y;   // positive = forward
                float deltaX     = gp.x - CENTER_COL;   // positive = right (in yards)
                float t = i * r.timePerWaypoint;
                maxTime = Mathf.Max(maxTime, t);

                frame.waypoints.Add(new SlotWaypoint
                {
                    posGroup   = r.posGroup,
                    slotIndex  = r.slotIndex,
                    deltaX     = deltaX,
                    deltaYards = deltaYards,
                });
            }

            frame.timeOffset = maxTime;
            keyframes.Add(frame);
        }

        EnsureDirectory(assetPath);
        var existing = AssetDatabase.LoadAssetAtPath<RouteData>(assetPath);

        if (existing != null)
        {
            existing.keyframes = keyframes;
            EditorUtility.SetDirty(existing);
        }
        else
        {
            var asset = ScriptableObject.CreateInstance<RouteData>();
            asset.keyframes = keyframes;
            AssetDatabase.CreateAsset(asset, assetPath);
        }

        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = existing ?? AssetDatabase.LoadAssetAtPath<RouteData>(assetPath);

        EditorUtility.DisplayDialog("Route Designer",
            $"Saved  {allRoutes.Count} routes,  {keyframes.Count} keyframes.\n{assetPath}", "OK");
    }

    private void LoadAsset()
    {
        string path = EditorUtility.OpenFilePanel("Load RouteData",
            "Assets/TcgEngine/Resources/Routes", "asset");
        if (string.IsNullOrEmpty(path)) return;
        if (path.StartsWith(Application.dataPath))
            path = "Assets" + path.Substring(Application.dataPath.Length);

        var loaded = AssetDatabase.LoadAssetAtPath<RouteData>(path);
        if (loaded == null) { EditorUtility.DisplayDialog("Route Designer", "Not a RouteData asset.", "OK"); return; }

        assetPath = path;
        allRoutes.Clear();
        waypoints.Clear();

        // Rebuild drawn routes from keyframes — group by (posGroup, slotIndex)
        var routeMap = new Dictionary<(PlayerPositionGrp, int), List<(Vector2Int, float)>>();

        foreach (var frame in loaded.keyframes)
        {
            foreach (var wp in frame.waypoints)
            {
                var key = (wp.posGroup, wp.slotIndex);
                if (!routeMap.ContainsKey(key))
                    routeMap[key] = new List<(Vector2Int, float)>();

                int col = CENTER_COL + Mathf.RoundToInt(wp.deltaX);
                int row = CENTER_ROW - Mathf.RoundToInt(wp.deltaYards);
                col = Mathf.Clamp(col, 0, COLS - 1);
                row = Mathf.Clamp(row, 0, ROWS - 1);

                routeMap[key].Add((new Vector2Int(col, row), frame.timeOffset));
            }
        }

        foreach (var kvp in routeMap)
        {
            var pts = kvp.Value;
            float avgTime = pts.Count > 1 ? pts[1].Item2 : 0.2f;
            allRoutes.Add(new DrawnRoute
            {
                posGroup       = kvp.Key.Item1,
                slotIndex      = kvp.Key.Item2,
                timePerWaypoint = avgTime,
                waypoints      = pts.ConvertAll(p => p.Item1)
            });
        }

        Repaint();
    }

    // -----------------------------------------------------------------------

    private static void EnsureDirectory(string path)
    {
        string dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(dir) || AssetDatabase.IsValidFolder(dir)) return;
        string[] parts = dir.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }

    private static void BuildLookups()
    {
        if (_letterOf != null) return;
        _letterOf = new Dictionary<PlayerPositionGrp, char>();
        _colorOf  = new Dictionary<PlayerPositionGrp, Color>();
        foreach (var info in PosInfo) { _letterOf[info.pos] = info.letter; _colorOf[info.pos] = info.color; }
    }

    private class DrawnRoute
    {
        public PlayerPositionGrp posGroup;
        public int slotIndex;
        public float timePerWaypoint;
        public List<Vector2Int> waypoints;
    }
}
#endif
