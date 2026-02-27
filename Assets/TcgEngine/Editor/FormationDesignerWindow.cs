#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using TcgEngine;
using Assets.TcgEngine.Scripts.Gameplay;

/// <summary>
/// Unity Editor Window: First&amp;Long → Formation Designer
///
/// Paste an ASCII grid and click "Parse" to generate a FormationData or RouteData asset.
///
/// ── GRID FORMATS ───────────────────────────────────────────────────────────
///
/// OFFENSE / DEFENSE (half-field, 21 rows × 31 cols)
///   Col 15 is the center of the field (col index 0-30).
///   Row 0 is the line of scrimmage.
///   - xFraction = col / 30.0 - 0.5
///   - Offense: yardsFromLOS = -rowIndex   (negative = own backfield)
///   - Defense: yardsFromLOS = (totalRows-1 - rowIndex)   (positive = opponent side)
///
/// Example offense grid (columns 0-30):
///   W-------------W---W-------------W-   row 0  (WRs at LOS)
///   -----------OO---OO-----------R----   row 1  (OL + TE)
///   ---------------Q------------------   row 2  (QB)
///
/// Example defense grid:
///   ---B-----------------------B------   row 0  (safeties deep)
///   ------L-----------L---------------   row 8  (LBs)
///   ---D-----D-------D-----D----------   row 18 (DL near LOS)
///
/// FULL-FIELD ROUTE (variable rows, multiple keyframes)
///   "===" marks the line of scrimmage.
///   Above "===" = own backfield (negative yardsFromLOS).
///   Below "===" = opponent side (positive yardsFromLOS).
///   "--- FRAME N t=X.X ---" starts a new keyframe.
///
///   --- FRAME 0 t=0.0 ---
///   W-------------W---W-------------W-   (pre-snap)
///   ===================================   LOS
///   --- FRAME 1 t=1.5 ---
///   ----------W-----------W-----------   (routes)
///   ===================================
///   ------W-------------------W-------
///
/// ── LETTER MAP ─────────────────────────────────────────────────────────────
///   Q=QB  W=WR  R=RB_TE  O=OL  K=K  D=DL  L=LB  B=DB
/// ───────────────────────────────────────────────────────────────────────────
/// </summary>
public class FormationDesignerWindow : EditorWindow
{
    private enum GridType { Offense, Defense, FullFieldRoute }

    private GridType gridType = GridType.Offense;
    private string gridText = "";
    private string assetPath = "Assets/TcgEngine/Resources/Formations/new_formation.asset";
    private Vector2 scroll;

    private static readonly Dictionary<char, PlayerPositionGrp> LetterMap =
        new Dictionary<char, PlayerPositionGrp>
        {
            { 'Q', PlayerPositionGrp.QB    },
            { 'W', PlayerPositionGrp.WR    },
            { 'R', PlayerPositionGrp.RB_TE },
            { 'O', PlayerPositionGrp.OL    },
            { 'K', PlayerPositionGrp.K     },
            { 'D', PlayerPositionGrp.DL    },
            { 'L', PlayerPositionGrp.LB    },
            { 'B', PlayerPositionGrp.DB    },
        };

    // -------------------------------------------------------

    [MenuItem("First&Long/Formation Designer")]
    public static void ShowWindow()
    {
        GetWindow<FormationDesignerWindow>("Formation Designer");
    }

    private void OnGUI()
    {
        GUILayout.Label("Formation Designer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        gridType = (GridType)EditorGUILayout.EnumPopup("Grid Type", gridType);
        assetPath = EditorGUILayout.TextField("Output Asset Path", assetPath);

        EditorGUILayout.Space();
        GUILayout.Label("ASCII Grid:", EditorStyles.boldLabel);

        // Show template hint based on current type
        EditorGUILayout.HelpBox(GetTemplate(), MessageType.None);

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(220));
        gridText = EditorGUILayout.TextArea(gridText, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        if (GUILayout.Button("Parse & Create Asset", GUILayout.Height(32)))
            ParseAndCreate();
    }

    // -------------------------------------------------------
    // Parsing

    private void ParseAndCreate()
    {
        if (string.IsNullOrWhiteSpace(gridText))
        {
            EditorUtility.DisplayDialog("Formation Designer", "Grid text is empty.", "OK");
            return;
        }

        try
        {
            switch (gridType)
            {
                case GridType.Offense:
                    CreateFormationAsset(ParseHalfField(gridText, isDefense: false), isDefense: false);
                    break;
                case GridType.Defense:
                    CreateFormationAsset(ParseHalfField(gridText, isDefense: true), isDefense: true);
                    break;
                case GridType.FullFieldRoute:
                    CreateRouteAsset(ParseFullFieldRoute(gridText));
                    break;
            }
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("Formation Designer", "Parse error:\n" + ex.Message, "OK");
            Debug.LogException(ex);
        }
    }

    // ── Half-field (FormationData) ────────────────────────────────────────

    private List<FormationSlotEntry> ParseHalfField(string text, bool isDefense)
    {
        string[] rawLines = text.Split('\n');

        // Strip comment lines (starting with #) and blank lines
        var lines = new List<string>();
        foreach (string line in rawLines)
        {
            string trimmed = line.TrimEnd();
            if (!trimmed.StartsWith("#") && trimmed.Length > 0)
                lines.Add(trimmed);
        }

        if (lines.Count == 0)
            throw new Exception("No grid rows found after stripping comments.");

        int totalRows = lines.Count;
        var counters = new Dictionary<PlayerPositionGrp, int>();
        var entries  = new List<FormationSlotEntry>();

        for (int rowIdx = 0; rowIdx < lines.Count; rowIdx++)
        {
            string row = lines[rowIdx];
            float yardsFromLOS = isDefense
                ? (totalRows - 1 - rowIdx)   // row 0 = deepest, last row = LOS
                : -rowIdx;                    // row 0 = LOS, lower rows = deeper backfield

            for (int colIdx = 0; colIdx < row.Length; colIdx++)
            {
                char c = char.ToUpper(row[colIdx]);
                if (!LetterMap.TryGetValue(c, out PlayerPositionGrp posGroup)) continue;

                if (!counters.ContainsKey(posGroup)) counters[posGroup] = 0;
                int slotIdx = counters[posGroup]++;

                float xFraction = colIdx / 30f - 0.5f;

                entries.Add(new FormationSlotEntry
                {
                    posGroup     = posGroup,
                    slotIndex    = slotIdx,
                    xFraction    = xFraction,
                    yardsFromLOS = yardsFromLOS,
                });
            }
        }

        return entries;
    }

    // ── Full-field route (RouteData) ──────────────────────────────────────

    private List<RouteKeyframe> ParseFullFieldRoute(string text)
    {
        var keyframes = new List<RouteKeyframe>();

        string[] rawLines = text.Split('\n');
        var framePattern = new Regex(@"---\s*FRAME\s+\d+\s+t=([\d.]+)\s*---", RegexOptions.IgnoreCase);
        var losPattern   = new Regex(@"^=+$");

        RouteKeyframe currentFrame = null;
        int  losRow         = -1;   // row index of the "===" marker in the current frame's block
        var  frameLines     = new List<string>();

        void FlushFrame()
        {
            if (currentFrame == null || frameLines.Count == 0) return;

            // Find "===" in frameLines
            int localLos = -1;
            for (int i = 0; i < frameLines.Count; i++)
                if (losPattern.IsMatch(frameLines[i].Trim())) { localLos = i; break; }

            if (localLos < 0) localLos = frameLines.Count; // treat all rows as backfield

            var counters = new Dictionary<PlayerPositionGrp, int>();

            for (int rowIdx = 0; rowIdx < frameLines.Count; rowIdx++)
            {
                string row = frameLines[rowIdx];
                if (losPattern.IsMatch(row.Trim())) continue; // skip LOS marker

                float yardsFromLOS = rowIdx < localLos
                    ? -(localLos - rowIdx)    // above LOS = backfield
                    :  (rowIdx - localLos);   // below LOS = opponent side

                for (int colIdx = 0; colIdx < row.Length; colIdx++)
                {
                    char c = char.ToUpper(row[colIdx]);
                    if (!LetterMap.TryGetValue(c, out PlayerPositionGrp posGroup)) continue;

                    if (!counters.ContainsKey(posGroup)) counters[posGroup] = 0;
                    int slotIdx = counters[posGroup]++;

                    float xFraction = colIdx / 30f - 0.5f;

                    currentFrame.waypoints.Add(new SlotWaypoint
                    {
                        posGroup     = posGroup,
                        slotIndex    = slotIdx,
                        xFraction    = xFraction,
                        yardsFromLOS = yardsFromLOS,
                    });
                }
            }

            keyframes.Add(currentFrame);
            currentFrame = null;
            frameLines.Clear();
        }

        foreach (string rawLine in rawLines)
        {
            string line = rawLine.TrimEnd();
            if (line.StartsWith("#")) continue;

            Match m = framePattern.Match(line);
            if (m.Success)
            {
                FlushFrame();
                float t = float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                currentFrame = new RouteKeyframe { timeOffset = t, waypoints = new List<SlotWaypoint>() };
                frameLines.Clear();
                continue;
            }

            if (currentFrame != null)
                frameLines.Add(line);
        }

        FlushFrame();
        return keyframes;
    }

    // -------------------------------------------------------
    // Asset creation

    private void CreateFormationAsset(List<FormationSlotEntry> entries, bool isDefense)
    {
        EnsureDirectory(assetPath);

        FormationData asset = ScriptableObject.CreateInstance<FormationData>();
        asset.isDefense = isDefense;
        asset.slots = entries;

        AssetDatabase.CreateAsset(asset, assetPath);
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = asset;

        Debug.Log($"[FormationDesigner] Created FormationData at {assetPath} — {entries.Count} slot entries.");
        EditorUtility.DisplayDialog("Formation Designer",
            $"Created FormationData with {entries.Count} entries.\n{assetPath}", "OK");
    }

    private void CreateRouteAsset(List<RouteKeyframe> keyframes)
    {
        EnsureDirectory(assetPath);

        RouteData asset = ScriptableObject.CreateInstance<RouteData>();
        asset.keyframes = keyframes;

        AssetDatabase.CreateAsset(asset, assetPath);
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = asset;

        int total = 0;
        foreach (var f in keyframes) total += f.waypoints.Count;
        Debug.Log($"[FormationDesigner] Created RouteData at {assetPath} — {keyframes.Count} keyframe(s), {total} waypoints.");
        EditorUtility.DisplayDialog("Formation Designer",
            $"Created RouteData with {keyframes.Count} keyframe(s).\n{assetPath}", "OK");
    }

    private static void EnsureDirectory(string assetPath)
    {
        string dir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(dir))
        {
            string[] parts = dir.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }

    // -------------------------------------------------------
    // Template text shown in the help box

    private string GetTemplate()
    {
        switch (gridType)
        {
            case GridType.Offense:
                return
                    "Offense half-field template (21 rows × 31 cols, col 15 = center):\n" +
                    "  W-------------W---W-------------W-   row 0 LOS (WRs wide)\n" +
                    "  -----------OO---OO-----------R----   row 1 (OL + TE)\n" +
                    "  ---------------Q------------------   row 2 (QB)\n" +
                    "Letters: Q=QB  W=WR  R=RB_TE  O=OL  K=K  D=DL  L=LB  B=DB\n" +
                    "yardsFromLOS = -rowIndex  (row 0 = LOS, deeper rows = further into backfield)";

            case GridType.Defense:
                return
                    "Defense half-field template (rows top=deep, bottom=LOS):\n" +
                    "  ---B-----------------------B------   row 0 (safeties, ~20 yds deep)\n" +
                    "  ------L-----------L---------------   row 8 (LBs)\n" +
                    "  ---D-----D-------D-----D----------   row 18 (DL at LOS)\n" +
                    "yardsFromLOS = (totalRows-1 - rowIndex)  (last row = 0 = LOS)";

            case GridType.FullFieldRoute:
                return
                    "Full-field route template (=== marks LOS, FRAME headers mark keyframes):\n" +
                    "  --- FRAME 0 t=0.0 ---\n" +
                    "  W-------------W---W-------------W-\n" +
                    "  ===================================   LOS\n" +
                    "  --- FRAME 1 t=1.5 ---\n" +
                    "  ----------W-----------W-----------\n" +
                    "  ===================================\n" +
                    "  ------W-------------------W-------";

            default: return "";
        }
    }
}
#endif
