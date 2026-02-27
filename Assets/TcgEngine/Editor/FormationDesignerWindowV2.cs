#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using TcgEngine;
using Assets.TcgEngine.Scripts.Gameplay;

/// <summary>
/// Visual point-and-click formation designer.
/// Menu: First&Long → Formation Designer (Visual)
///
/// The grid represents the field in world space:
///   Top rows    = opponent territory (positive yardsFromLOS)
///   Yellow row  = Line of Scrimmage (yardsFromLOS = 0)
///   Bottom rows = own backfield (negative yardsFromLOS)
///   Col 15      = field center (xFraction = 0)
///
/// SlotIndex is assigned left-to-right within each row, top-to-bottom.
/// So the leftmost QB placed = QB slot 0, leftmost WR = WR slot 0, etc.
/// </summary>
public class FormationDesignerWindowV2 : EditorWindow
{
    private const int   COLS     = 31;
    private const int   ROWS     = 41;   // yardsFromLOS -20 (row 40) to +20 (row 0)
    private const int   LOS_ROW  = 20;   // row index where yardsFromLOS = 0
    private const float CELL_W   = 18f;
    private const float CELL_H   = 16f;

    private PlayerPositionGrp selectedPos = PlayerPositionGrp.QB;
    private readonly PlayerPositionGrp[,] grid = new PlayerPositionGrp[ROWS, COLS];
    private string assetPath = "Assets/TcgEngine/Resources/Formations/new_formation.asset";
    private Vector2 scroll;

    // Position metadata: (enum, display letter, color)
    private static readonly (PlayerPositionGrp pos, char letter, Color color)[] PosInfo =
    {
        (PlayerPositionGrp.QB,    'Q', new Color(1.00f, 0.85f, 0.00f)),  // gold
        (PlayerPositionGrp.WR,    'W', new Color(0.00f, 0.85f, 1.00f)),  // cyan
        (PlayerPositionGrp.RB_TE, 'R', new Color(0.20f, 0.90f, 0.30f)),  // green
        (PlayerPositionGrp.OL,    'O', new Color(1.00f, 0.55f, 0.00f)),  // orange
        (PlayerPositionGrp.K,     'K', new Color(0.80f, 0.20f, 0.90f)),  // purple
        (PlayerPositionGrp.DL,    'D', new Color(1.00f, 0.20f, 0.20f)),  // red
        (PlayerPositionGrp.LB,    'L', new Color(1.00f, 0.55f, 0.55f)),  // pink
        (PlayerPositionGrp.DB,    'B', new Color(0.55f, 0.55f, 1.00f)),  // blue
    };

    private static Dictionary<PlayerPositionGrp, char>  _letterOf;
    private static Dictionary<PlayerPositionGrp, Color> _colorOf;

    // -----------------------------------------------------------------------

    [MenuItem("First&Long/Formation Designer (Visual)")]
    public static void ShowWindow() =>
        GetWindow<FormationDesignerWindowV2>("Formation Designer");

    void OnEnable()
    {
        BuildLookups();
        ClearGrid();
        // Scroll so LOS row starts near vertical center of the visible area
        scroll = new Vector2(0, Mathf.Max(0, LOS_ROW * CELL_H - 220f));
    }

    // -----------------------------------------------------------------------
    // Main GUI

    void OnGUI()
    {
        GUILayout.Label("Visual Formation Designer", EditorStyles.boldLabel);
        EditorGUILayout.Space(3);

        DrawToolbar();

        EditorGUILayout.Space(3);
        EditorGUILayout.HelpBox(
            "Select a position above, then click cells to place/remove it.\n" +
            "Yellow row = LOS.  Green = own backfield (negative yards).  Blue = opponent territory (positive yards).\n" +
            "SlotIndex assigned left-to-right, top-to-bottom — so leftmost of each group = slot 0.",
            MessageType.None);
        EditorGUILayout.Space(2);

        DrawGrid();

        EditorGUILayout.Space(4);
        assetPath = EditorGUILayout.TextField("Output Asset Path", assetPath);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear Grid", GUILayout.Width(90), GUILayout.Height(26)))
        {
            if (EditorUtility.DisplayDialog("Clear Grid", "Remove all placed positions?", "Clear", "Cancel"))
                ClearGrid();
        }
        GUI.backgroundColor = new Color(0.6f, 1f, 0.6f);
        if (GUILayout.Button("Create Asset", GUILayout.Height(26)))
            CreateAsset();
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    // -----------------------------------------------------------------------
    // Toolbar — position selector buttons

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Place:", GUILayout.Width(40));
        foreach (var info in PosInfo)
        {
            bool selected = selectedPos == info.pos;
            GUI.backgroundColor = selected ? info.color : new Color(0.55f, 0.55f, 0.55f);
            string label = $"{info.letter}  {info.pos}";
            GUIStyle style = selected ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
            if (GUILayout.Button(label, style, GUILayout.Height(24)))
                selectedPos = info.pos;
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    // -----------------------------------------------------------------------
    // Grid drawing

    private void DrawGrid()
    {
        float gridW   = 32f + COLS * CELL_W;
        float gridH   = ROWS * CELL_H;
        float visible = Mathf.Min(gridH + 4f, 460f);

        Rect scrollRect = GUILayoutUtility.GetRect(gridW, visible);
        scroll = GUI.BeginScrollView(scrollRect, scroll, new Rect(0, 0, gridW, gridH));

        for (int r = 0; r < ROWS; r++)
        {
            float yardsFromLOS = LOS_ROW - r;  // row 0 = +20 yds, row 20 = LOS, row 40 = -20 yds
            float y = r * CELL_H;

            // Yard label on the left
            string yardLabel = yardsFromLOS == 0 ? "LOS" : yardsFromLOS.ToString("+#;-#;0");
            GUI.Label(new Rect(0, y + 1f, 30f, CELL_H), yardLabel, EditorStyles.miniLabel);

            for (int c = 0; c < COLS; c++)
            {
                float x = 32f + c * CELL_W;
                Rect cellRect = new Rect(x, y, CELL_W - 1f, CELL_H - 1f);

                PlayerPositionGrp cellPos = grid[r, c];

                // Background
                Color bg = GetCellBg(r, c, yardsFromLOS);
                if (cellPos != PlayerPositionGrp.NONE)
                    bg = _colorOf[cellPos];

                GUI.backgroundColor = bg;

                string label = cellPos != PlayerPositionGrp.NONE ? _letterOf[cellPos].ToString() : "";

                if (GUI.Button(cellRect, label, EditorStyles.miniButton))
                {
                    grid[r, c] = (grid[r, c] == selectedPos)
                        ? PlayerPositionGrp.NONE
                        : selectedPos;
                    Repaint();
                }
            }
        }

        GUI.backgroundColor = Color.white;
        GUI.EndScrollView();
    }

    private static Color GetCellBg(int r, int c, float yardsFromLOS)
    {
        if (r == LOS_ROW)
            return new Color(0.95f, 0.95f, 0.45f);                         // LOS = yellow
        bool isCenter = (c == 15);
        if (yardsFromLOS < 0)
            return isCenter ? new Color(0.60f, 0.83f, 0.60f) : new Color(0.72f, 0.88f, 0.72f); // backfield = green
        return     isCenter ? new Color(0.60f, 0.72f, 0.95f) : new Color(0.72f, 0.80f, 0.98f); // def terr = blue
    }

    // -----------------------------------------------------------------------
    // Asset creation

    private void CreateAsset()
    {
        var entries  = new List<FormationSlotEntry>();
        var counters = new Dictionary<PlayerPositionGrp, int>();

        // Scan top → bottom, left → right so slotIndex = left-to-right order
        for (int r = 0; r < ROWS; r++)
        {
            float yardsFromLOS = LOS_ROW - r;
            for (int c = 0; c < COLS; c++)
            {
                PlayerPositionGrp pos = grid[r, c];
                if (pos == PlayerPositionGrp.NONE) continue;

                if (!counters.ContainsKey(pos)) counters[pos] = 0;

                entries.Add(new FormationSlotEntry
                {
                    posGroup     = pos,
                    slotIndex    = counters[pos]++,
                    xFraction    = c / 30f - 0.5f,
                    yardsFromLOS = yardsFromLOS,
                });
            }
        }

        if (entries.Count == 0)
        {
            EditorUtility.DisplayDialog("Formation Designer",
                "No positions placed yet — click cells on the grid first.", "OK");
            return;
        }

        EnsureDirectory(assetPath);

        FormationData asset = ScriptableObject.CreateInstance<FormationData>();
        asset.slots = entries;

        AssetDatabase.CreateAsset(asset, assetPath);
        AssetDatabase.SaveAssets();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = asset;

        EditorUtility.DisplayDialog("Formation Designer",
            $"Created FormationData with {entries.Count} slot entries.\n{assetPath}", "OK");
    }

    // -----------------------------------------------------------------------
    // Helpers

    private void ClearGrid()
    {
        for (int r = 0; r < ROWS; r++)
            for (int c = 0; c < COLS; c++)
                grid[r, c] = PlayerPositionGrp.NONE;
        Repaint();
    }

    private static void EnsureDirectory(string path)
    {
        string dir = Path.GetDirectoryName(path).Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(dir)) return;
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
        foreach (var info in PosInfo)
        {
            _letterOf[info.pos] = info.letter;
            _colorOf[info.pos]  = info.color;
        }
    }
}
#endif
