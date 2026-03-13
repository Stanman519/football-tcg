















































































#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TcgEngine;
using Assets.TcgEngine.Scripts.Gameplay;

/// <summary>
/// Drag-and-drop editor for assigning RouteData assets to a coach's slots per play type.
/// Menu: First&Long → Coach Route Setup
///
/// Select a CoachCardData asset, pick a play type, and drag RouteData assets
/// onto each position/slot cell to assign post-snap routes.
/// </summary>
public class CoachRouteSetupWindow : EditorWindow
{
    private CoachCardData targetCoach;
    private PlayType selectedPlay = PlayType.Run;
    private bool showOffense = true;
    private Vector2 scroll;

    private static readonly PlayerPositionGrp[] OffensePositions =
    {
        PlayerPositionGrp.QB, PlayerPositionGrp.WR, PlayerPositionGrp.RB_TE, PlayerPositionGrp.OL,
    };

    private static readonly PlayerPositionGrp[] DefensePositions =
    {
        PlayerPositionGrp.DL, PlayerPositionGrp.LB, PlayerPositionGrp.DB,
    };

    private static readonly (PlayerPositionGrp pos, string label, Color color)[] PosStyle =
    {
        (PlayerPositionGrp.QB,    "QB",  new Color(1.00f, 0.85f, 0.00f)),
        (PlayerPositionGrp.WR,    "WR",  new Color(0.00f, 0.85f, 1.00f)),
        (PlayerPositionGrp.RB_TE, "RB/TE", new Color(0.20f, 0.90f, 0.30f)),
        (PlayerPositionGrp.OL,    "OL",  new Color(1.00f, 0.55f, 0.00f)),
        (PlayerPositionGrp.DL,    "DL",  new Color(1.00f, 0.20f, 0.20f)),
        (PlayerPositionGrp.LB,    "LB",  new Color(1.00f, 0.55f, 0.55f)),
        (PlayerPositionGrp.DB,    "DB",  new Color(0.55f, 0.55f, 1.00f)),
    };
    private static Dictionary<PlayerPositionGrp, (string label, Color color)> _posStyle;

    [MenuItem("First&Long/Coach Route Setup")]
    public static void ShowWindow() =>
        GetWindow<CoachRouteSetupWindow>("Coach Route Setup");

    void OnEnable() => BuildLookup();

    void OnGUI()
    {
        GUILayout.Label("Coach Route Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space(3);

        // Coach picker
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Coach Asset:", GUILayout.Width(85));
        targetCoach = (CoachCardData)EditorGUILayout.ObjectField(
            targetCoach, typeof(CoachCardData), false);
        EditorGUILayout.EndHorizontal();

        if (targetCoach == null)
        {
            EditorGUILayout.HelpBox("Select a CoachCardData asset above.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(4);

        // Offense / Defense toggle
        EditorGUILayout.BeginHorizontal();
        GUI.backgroundColor = showOffense ? new Color(0.6f, 1f, 0.6f) : Color.white;
        if (GUILayout.Button("Offense", GUILayout.Height(24))) showOffense = true;
        GUI.backgroundColor = !showOffense ? new Color(1f, 0.6f, 0.6f) : Color.white;
        if (GUILayout.Button("Defense", GUILayout.Height(24))) showOffense = false;
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(2);

        // Play type tabs
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Play:", GUILayout.Width(32));
        foreach (PlayType pt in new[] { PlayType.Run, PlayType.ShortPass, PlayType.LongPass })
        {
            bool sel = selectedPlay == pt;
            GUI.backgroundColor = sel ? new Color(0.8f, 0.8f, 1f) : Color.white;
            if (GUILayout.Button(pt.ToString(), GUILayout.Height(22)))
                selectedPlay = pt;
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "Drag a RouteData asset from the Project window onto a slot cell to assign it.\n" +
            "Click the  ✕  button to clear a slot's route.",
            MessageType.None);
        EditorGUILayout.Space(3);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawSlotGrid();
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(6);
        if (GUILayout.Button("Save Changes", GUILayout.Height(28)))
        {
            EditorUtility.SetDirty(targetCoach);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Coach Route Setup", "Saved.", "OK");
        }
    }

    // -------------------------------------------------------------------------

    private void DrawSlotGrid()
    {
        var positions = showOffense ? OffensePositions : DefensePositions;
        var routeArray = showOffense ? targetCoach.offenseRoutes : targetCoach.defenseRoutes;

        // Build a working dictionary for easy lookup + mutation
        var routeMap = BuildRouteMap(routeArray);

        bool changed = false;

        foreach (var posGroup in positions)
        {
            if (!_posStyle.TryGetValue(posGroup, out var style)) continue;

            EditorGUILayout.BeginVertical("box");
            GUI.contentColor = style.color;
            GUILayout.Label($"  {style.label}", EditorStyles.boldLabel);
            GUI.contentColor = Color.white;

            // How many slots does this coach allow for this position?
            int maxSlots = targetCoach.GetMaxCards(posGroup);
            if (maxSlots <= 0) maxSlots = 3; // fallback

            EditorGUILayout.BeginHorizontal();
            for (int slotIdx = 0; slotIdx < maxSlots; slotIdx++)
            {
                EditorGUILayout.BeginVertical(GUILayout.Width(160));
                GUILayout.Label($"Slot {slotIdx}", EditorStyles.centeredGreyMiniLabel);

                var key = (selectedPlay, posGroup, slotIdx);
                routeMap.TryGetValue(key, out RouteData current);

                // Drop target rect
                Rect dropRect = GUILayoutUtility.GetRect(150, 36);
                GUI.backgroundColor = current != null ? style.color * 0.8f : new Color(0.3f, 0.3f, 0.3f);
                GUI.Box(dropRect, current != null ? current.name : "— none —", EditorStyles.helpBox);
                GUI.backgroundColor = Color.white;

                // Handle drag-and-drop onto this rect
                if (HandleDrop(dropRect, out RouteData dropped))
                {
                    routeMap[key] = dropped;
                    changed = true;
                }

                // Clear button
                EditorGUILayout.BeginHorizontal();
                if (current != null)
                {
                    if (GUILayout.Button("✕ Clear", EditorStyles.miniButton))
                    {
                        routeMap.Remove(key);
                        changed = true;
                    }
                    if (GUILayout.Button("Select", EditorStyles.miniButton))
                        Selection.activeObject = current;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2);
        }

        // Write changes back to the array
        if (changed)
        {
            ApplyRouteMap(routeMap, ref (showOffense ? ref targetCoach.offenseRoutes : ref targetCoach.defenseRoutes));
            EditorUtility.SetDirty(targetCoach);
            Repaint();
        }
    }

    // -------------------------------------------------------------------------

    private bool HandleDrop(Rect dropRect, out RouteData result)
    {
        result = null;
        Event e = Event.current;
        if (!dropRect.Contains(e.mousePosition)) return false;

        if (e.type == EventType.DragUpdated)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            e.Use();
        }
        else if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is RouteData rd) { result = rd; e.Use(); return true; }
            }
        }
        return false;
    }

    // -------------------------------------------------------------------------

    private Dictionary<(PlayType, PlayerPositionGrp, int), RouteData> BuildRouteMap(CoachRouteEntry[] arr)
    {
        var map = new Dictionary<(PlayType, PlayerPositionGrp, int), RouteData>();
        if (arr == null) return map;
        foreach (var e in arr)
            if (e.route != null)
                map[(e.playType, e.posGroup, e.slotIndex)] = e.route;
        return map;
    }

    private void ApplyRouteMap(
        Dictionary<(PlayType, PlayerPositionGrp, int), RouteData> map,
        ref CoachRouteEntry[] arr)
    {
        var list = new List<CoachRouteEntry>();
        // Preserve entries for OTHER play types / sides that aren't being edited
        if (arr != null)
        {
            foreach (var e in arr)
            {
                // Skip entries for the current play type — we'll rewrite them from the map
                if (e.playType == selectedPlay) continue;
                list.Add(e);
            }
        }
        // Add current play type entries from map
        foreach (var kvp in map)
        {
            if (kvp.Key.Item1 != selectedPlay) continue;
            list.Add(new CoachRouteEntry
            {
                playType = kvp.Key.Item1,
                posGroup = kvp.Key.Item2,
                slotIndex = kvp.Key.Item3,
                route = kvp.Value
            });
        }
        arr = list.ToArray();
    }

    private static void BuildLookup()
    {
        _posStyle = new Dictionary<PlayerPositionGrp, (string, Color)>();
        foreach (var p in PosStyle)
            _posStyle[p.pos] = (p.label, p.color);
    }
}
#endif
