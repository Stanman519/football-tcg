using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TcgEngine;
using TcgEngine.Client;
using Assets.TcgEngine.Scripts.Gameplay;

/// <summary>
/// Which visual formation to use for the slots and board cards.
/// </summary>
public enum FieldFormation
{
    Huddle,
    Offense_Run,
    Offense_ShortPass,
    Offense_LongPass,
    Defense_vs_Run,
    Defense_vs_Pass,
    LiveBall,
}

/// <summary>
/// Manages all BoardSlot instances on the field.
///
/// Formation positions are defined in FOOTBALL COORDINATES:
///   x: fraction of field width from center  (-0.5 = left sideline, 0 = center, 0.5 = right sideline)
///   y: yards from line of scrimmage          (negative = offensive backfield, positive = defensive side)
///
/// At runtime these are converted to world-space local positions (1 unit = 1 yard):
///   localX = xFraction * fieldWidth
///   localY = yardsFromLOS * 1.0f   (1 unit = 1 yard, no extra factor)
///
/// To tune the look: adjust fieldWidth on FieldSlotManager (controls width spread).
/// Depth relationships are automatic since 1 unit = 1 yard.
/// </summary>
public class FieldSlotManager : MonoBehaviour
{
    [Header("References")]
    public GameObject slotPrefab;
    public GameObject boardCardPrefab; // legacy reference, not used here
    public Transform slotParent;       // Assign LOSMarker in Inspector

    [Header("Field Dimensions")]
    public float fieldWidth = 53.3f;   // Full field width in world units (yards)
    public float slotScale = 0.5f;     // Local scale applied to each spawned slot

    [Header("Formation Assets (assign in Inspector; overrides hardcoded data)")]
    public FormationData formation_HuddleOffense;
    public FormationData formation_HuddleDefense;
    public FormationData formation_OffenseRun;
    public FormationData formation_OffenseShortPass;
    public FormationData formation_OffenseLongPass;
    public FormationData formation_DefenseVsRun;
    public FormationData formation_DefenseVsPass;
    public FormationData formation_LiveBall;

    public static FieldSlotManager Instance { get; private set; }

    // slotMap[playerId][posGroup] = ordered list (index 0 = first slot, 1 = second, ...)
    private Dictionary<int, Dictionary<PlayerPositionGrp, List<BoardSlot>>> slotMap
        = new Dictionary<int, Dictionary<PlayerPositionGrp, List<BoardSlot>>>();

    // Formation data: [formation][posGroup][slotIndex] = (xFraction, yardsFromLOS)
    // xFraction: fraction of fieldWidth from center  (-0.5 = left, 0 = center, 0.5 = right)
    // yardsFromLOS: negative = offensive backfield,  positive = defensive side
    private Dictionary<FieldFormation, Dictionary<PlayerPositionGrp, Vector2[]>> formationData;

    private GamePhase lastPhase = GamePhase.None;
    private int lastBoardCount = -1;

    // -------------------------------------------------------

    void Awake()
    {
        Instance = this;
        BuildFormationData();
    }

    void Update()
    {
        if (!GameClient.Get()?.IsReady() ?? false) return;
        Game g = GameClient.Get().GetGameData();
        if (g == null) return;

        int boardCount = g.players?.Sum(p => p.cards_board.Count) ?? 0;

        if (g.phase != lastPhase || boardCount != lastBoardCount)
        {
            lastPhase = g.phase;
            lastBoardCount = boardCount;
            ApplyFormations(g);
        }
    }

    // -------------------------------------------------------
    // Coordinate conversion
    //
    // Football coords → LOSMarker-local world units (1 unit = 1 yard).
    // All slots are children of slotParent (LOSMarker), so local Y = yards from LOS.

    private Vector3 ToLocalPos(Vector2 footballCoord)
    {
        return new Vector3(
            footballCoord.x * fieldWidth,  // xFraction × full field width
            footballCoord.y,               // yardsFromLOS — 1 unit = 1 yard
            -0.1f
        );
    }

    /// <summary>Public accessor for FieldAnimationController.</summary>
    public Vector3 ToLocalPosPublic(Vector2 footballCoord) => ToLocalPos(footballCoord);

    // -------------------------------------------------------
    // Slot generation — call once per player after game data is ready

    public void GenerateSlotsForPlayer(Player player)
    {
        HeadCoachCard coach = player.head_coach;
        if (coach == null || coach.positional_Scheme == null)
        {
            Debug.LogError($"Player {player.player_id} has no head coach assigned!");
            return;
        }

        if (!slotMap.ContainsKey(player.player_id))
            slotMap[player.player_id] = new Dictionary<PlayerPositionGrp, List<BoardSlot>>();

        foreach (var entry in coach.positional_Scheme)
        {
            PlayerPositionGrp posGroup = entry.Key;
            int posMax = entry.Value.pos_max;

            if (posGroup == PlayerPositionGrp.NONE || posMax <= 0) continue;

            bool isDefense = IsDefenseGroup(posGroup);

            if (!slotMap[player.player_id].ContainsKey(posGroup))
                slotMap[player.player_id][posGroup] = new List<BoardSlot>();

            for (int i = 0; i < posMax; i++)
            {
                Vector3 initialPos = GetFormationLocalPos(FieldFormation.Huddle, posGroup, i);

                GameObject slotObj = Instantiate(slotPrefab, Vector3.zero, Quaternion.identity);
                slotObj.transform.SetParent(slotParent, false);
                slotObj.transform.localScale = Vector3.one * slotScale;
                slotObj.transform.localPosition = initialPos;

                BoardSlot slot = slotObj.GetComponent<BoardSlot>();
                slot.Initialize(posGroup, player.player_id, !isDefense, i);
                slot.SetTargetPosition(initialPos);

                slotMap[player.player_id][posGroup].Add(slot);
            }
        }
    }

    // -------------------------------------------------------
    // Formation transitions

    private void ApplyFormations(Game g)
    {
        if (g.players == null) return;

        foreach (Player player in g.players)
        {
            if (!slotMap.ContainsKey(player.player_id)) continue;

            bool isOffense = g.current_offensive_player != null
                && player.player_id == g.current_offensive_player.player_id;

            FieldFormation form = GetFormationForPhase(g, isOffense);
            MoveSlots(player.player_id, form, g, isOffense);
        }
    }

    private void MoveSlots(int playerId, FieldFormation form, Game g = null, bool isOffense = false)
    {
        if (!slotMap.ContainsKey(playerId)) return;

        if (g != null)
        {
            // Priority 1: play enhancer card's formation override
            FormationData overrideData = GetOverrideFormation(g, isOffense);
            if (overrideData != null)
            {
                MoveSlotsByFormationData(playerId, overrideData);
                return;
            }

            // Priority 2: coach base formations
            Player player = g.players?.FirstOrDefault(p => p.player_id == playerId);
            if (player?.head_coach != null)
            {
                PlayType offPlay = g.current_offensive_player?.SelectedPlay ?? PlayType.Huddle;
                var coachFormations = isOffense
                    ? player.head_coach.offenseFormations
                    : player.head_coach.defenseFormations;

                if (coachFormations != null
                    && coachFormations.TryGetValue(offPlay, out FormationData coachData)
                    && coachData != null)
                {
                    MoveSlotsByFormationData(playerId, coachData);
                    return;
                }
            }
        }

        // Priority 3: Inspector-assigned FormationData asset
        FormationData inspectorData = GetInspectorFormation(form, isOffense);
        if (inspectorData != null)
        {
            MoveSlotsByFormationData(playerId, inspectorData);
            return;
        }

        // Priority 4: hardcoded fallback (always present)
        foreach (var posEntry in slotMap[playerId])
        {
            PlayerPositionGrp posGroup = posEntry.Key;
            List<BoardSlot> slots = posEntry.Value;
            for (int i = 0; i < slots.Count; i++)
                slots[i].SetTargetPosition(GetFormationLocalPos(form, posGroup, i));
        }
    }

    private FormationData GetInspectorFormation(FieldFormation form, bool isOffense = true)
    {
        switch (form)
        {
            case FieldFormation.Huddle:             return isOffense ? formation_HuddleOffense : formation_HuddleDefense;
            case FieldFormation.Offense_Run:        return formation_OffenseRun;
            case FieldFormation.Offense_ShortPass:  return formation_OffenseShortPass;
            case FieldFormation.Offense_LongPass:   return formation_OffenseLongPass;
            case FieldFormation.Defense_vs_Run:     return formation_DefenseVsRun;
            case FieldFormation.Defense_vs_Pass:    return formation_DefenseVsPass;
            case FieldFormation.LiveBall:           return formation_LiveBall;
            default:                                return null;
        }
    }

    private FormationData GetOverrideFormation(Game g, bool isOffense)
    {
        Player player = isOffense
            ? g.current_offensive_player
            : g.GetCurrentDefensivePlayer();
        return player?.PlayEnhancer?.CardData?.formationOverride;
    }

    private void MoveSlotsByFormationData(int playerId, FormationData data)
    {
        if (data == null) return;
        foreach (var entry in data.slots)
        {
            var slots = GetSlotsForPosition(entry.posGroup, playerId);
            if (entry.slotIndex < slots.Count)
                slots[entry.slotIndex].SetTargetPosition(
                    ToLocalPos(new Vector2(entry.xFraction, entry.yardsFromLOS)));
        }
    }

    private FieldFormation GetFormationForPhase(Game g, bool isOffense)
    {
        PlayType offPlay = g.current_offensive_player?.SelectedPlay ?? PlayType.Huddle;

        switch (g.phase)
        {
            case GamePhase.LiveBall:
                return FieldFormation.LiveBall;

            case GamePhase.RevealPlayCalls:
            case GamePhase.SlotSpin:
            case GamePhase.Resolution:
                return isOffense
                    ? OffenseFormationForPlay(offPlay)
                    : DefenseFormationForPlay(offPlay);

            default:
                return FieldFormation.Huddle;
        }
    }

    private static FieldFormation OffenseFormationForPlay(PlayType play)
    {
        switch (play)
        {
            case PlayType.Run:       return FieldFormation.Offense_Run;
            case PlayType.ShortPass: return FieldFormation.Offense_ShortPass;
            case PlayType.LongPass:  return FieldFormation.Offense_LongPass;
            default:                 return FieldFormation.Huddle;
        }
    }

    private static FieldFormation DefenseFormationForPlay(PlayType play) =>
        play == PlayType.Run ? FieldFormation.Defense_vs_Run : FieldFormation.Defense_vs_Pass;

    // -------------------------------------------------------
    // Formation position lookup

    private Vector3 GetFormationLocalPos(FieldFormation form, PlayerPositionGrp posGroup, int slotIndex)
    {
        if (formationData.TryGetValue(form, out var posDict))
            if (posDict.TryGetValue(posGroup, out var positions))
            {
                Vector2 fc = slotIndex < positions.Length ? positions[slotIndex] : positions[positions.Length - 1];
                return ToLocalPos(fc);
            }

        if (form != FieldFormation.Huddle)
            return GetFormationLocalPos(FieldFormation.Huddle, posGroup, slotIndex);

        return Vector3.zero;
    }

    // -------------------------------------------------------
    // Formation data
    //
    // Each entry: Vector2(xFraction, yardsFromLOS)
    //
    //   xFraction:    -0.5 = left sideline   0 = center   +0.5 = right sideline
    //   yardsFromLOS:  negative = offensive backfield    positive = defensive side
    //
    // Think of these as real football alignments.
    // The framework converts them to pixels automatically using the field panel size
    // and FieldScroller.pixelsPerYard — so tuning ONE value in the Inspector adjusts
    // all depth relationships simultaneously.

    private void BuildFormationData()
    {
        formationData = new Dictionary<FieldFormation, Dictionary<PlayerPositionGrp, Vector2[]>>
        {
            // ── HUDDLE ──────────────────────────────────────────────────────
            // Everyone clusters 10–13 yards into their own backfield.
            // Slots: QB×1  WR×3  RB_TE×2  OL×5  K×1  DL×2  LB×2  DB×3
            [FieldFormation.Huddle] = new Dictionary<PlayerPositionGrp, Vector2[]>
            {
                [PlayerPositionGrp.QB]    = new[] { F( 0.00f, -11f) },
                [PlayerPositionGrp.WR]    = new[] { F(-0.18f, -12f), F( 0.00f, -12.5f), F( 0.18f, -12f) },
                [PlayerPositionGrp.RB_TE] = new[] { F(-0.08f, -13f), F( 0.08f, -13f) },
                [PlayerPositionGrp.OL]    = new[] { F(-0.20f, -12f), F(-0.10f, -12f), F( 0.00f, -12f), F( 0.10f, -12f), F( 0.20f, -12f) },
                [PlayerPositionGrp.K]     = new[] { F( 0.00f, -13f) },
                [PlayerPositionGrp.DL]    = new[] { F(-0.10f,  11f), F( 0.10f,  11f) },
                [PlayerPositionGrp.LB]    = new[] { F(-0.08f,  12f), F( 0.08f,  12f) },
                [PlayerPositionGrp.DB]    = new[] { F(-0.18f,  13f), F( 0.00f,  13.5f), F( 0.18f,  13f) },
            },

            // ── OFFENSE: RUN  (I-formation / Power) ─────────────────────────
            // QB under center, RB behind, TE wing right, 3rd WR slot left, OL×5 on line.
            [FieldFormation.Offense_Run] = new Dictionary<PlayerPositionGrp, Vector2[]>
            {
                [PlayerPositionGrp.QB]    = new[] { F( 0.00f, -1.5f) },
                [PlayerPositionGrp.WR]    = new[] { F(-0.42f,  0.0f), F( 0.42f,  0.0f), F(-0.30f,  0.0f) },
                [PlayerPositionGrp.RB_TE] = new[] { F( 0.00f, -5.5f), F( 0.32f,  0.0f) },
                [PlayerPositionGrp.OL]    = new[] { F(-0.20f,  0.0f), F(-0.10f,  0.0f), F( 0.00f,  0.0f), F( 0.10f,  0.0f), F( 0.20f,  0.0f) },
                [PlayerPositionGrp.K]     = new[] { F( 0.00f, -13f) },
            },

            // ── OFFENSE: SHORT PASS  (Shotgun / 3-WR) ───────────────────────
            // QB shotgun, 3 WRs split, RB/TE as checkdowns, 5 OL.
            [FieldFormation.Offense_ShortPass] = new Dictionary<PlayerPositionGrp, Vector2[]>
            {
                [PlayerPositionGrp.QB]    = new[] { F( 0.00f, -5.0f) },
                [PlayerPositionGrp.WR]    = new[] { F(-0.42f,  0.0f), F( 0.42f,  0.0f), F(-0.22f,  0.0f) },
                [PlayerPositionGrp.RB_TE] = new[] { F(-0.12f, -4.0f), F( 0.12f, -4.0f) },
                [PlayerPositionGrp.OL]    = new[] { F(-0.20f,  0.0f), F(-0.10f,  0.0f), F( 0.00f,  0.0f), F( 0.10f,  0.0f), F( 0.20f,  0.0f) },
                [PlayerPositionGrp.K]     = new[] { F( 0.00f, -13f) },
            },

            // ── OFFENSE: LONG PASS  (Air Raid / 3-WR Spread) ────────────────
            // Deep shotgun, 3 WRs wide, RB/TE as slot options, 5 OL.
            [FieldFormation.Offense_LongPass] = new Dictionary<PlayerPositionGrp, Vector2[]>
            {
                [PlayerPositionGrp.QB]    = new[] { F( 0.00f, -7.0f) },
                [PlayerPositionGrp.WR]    = new[] { F(-0.45f,  0.0f), F( 0.45f,  0.0f), F( 0.00f,  0.0f) },
                [PlayerPositionGrp.RB_TE] = new[] { F(-0.28f,  0.0f), F( 0.28f,  0.0f) },
                [PlayerPositionGrp.OL]    = new[] { F(-0.20f,  0.0f), F(-0.10f,  0.0f), F( 0.00f,  0.0f), F( 0.10f,  0.0f), F( 0.20f,  0.0f) },
                [PlayerPositionGrp.K]     = new[] { F( 0.00f, -13f) },
            },

            // ── DEFENSE: vs RUN  (4-3 look) ─────────────────────────────────
            // 2 DL tight, 2 LB shallow, 3 DB: 2 CBs wide + 1 safety deep center.
            [FieldFormation.Defense_vs_Run] = new Dictionary<PlayerPositionGrp, Vector2[]>
            {
                [PlayerPositionGrp.DL] = new[] { F(-0.15f, 1.0f), F( 0.15f, 1.0f) },
                [PlayerPositionGrp.LB] = new[] { F(-0.18f, 4.0f), F( 0.18f, 4.0f) },
                [PlayerPositionGrp.DB] = new[] { F(-0.38f, 6.0f), F( 0.38f, 6.0f), F( 0.00f, 9.0f) },
            },

            // ── DEFENSE: vs PASS  (Cover 2 Nickel) ──────────────────────────
            // CBs press wide, 2 safeties split deep, LBs drop into zones.
            // CBs press wide, 2 safeties split deep, LBs drop into zones.
            [FieldFormation.Defense_vs_Pass] = new Dictionary<PlayerPositionGrp, Vector2[]>
            {
                [PlayerPositionGrp.DL] = new[] { F(-0.18f, 1.0f), F( 0.18f, 1.0f) },
                [PlayerPositionGrp.LB] = new[] { F(-0.15f, 3.0f), F( 0.15f, 3.0f) },
                [PlayerPositionGrp.DB] = new[] { F(-0.42f, 5.0f), F( 0.42f, 5.0f), F( 0.00f, 9.0f) }, // 2 CBs + safety
            },

            // ── LIVE BALL  (routes running, pursuit angles) ──────────────────
            // Slots: QB×1  WR×3  RB_TE×2  OL×5  DL×2  LB×2  DB×3
            [FieldFormation.LiveBall] = new Dictionary<PlayerPositionGrp, Vector2[]>
            {
                [PlayerPositionGrp.QB]    = new[] { F( 0.00f, -12f) },
                [PlayerPositionGrp.WR]    = new[] { F(-0.43f, -3.0f), F( 0.43f, -3.0f), F(-0.22f,  2.0f) },
                [PlayerPositionGrp.RB_TE] = new[] { F(-0.08f, -1.0f), F( 0.28f,  3.0f) },
                [PlayerPositionGrp.OL]    = new[] { F(-0.20f,  0.0f), F(-0.10f,  0.0f), F( 0.00f,  0.0f), F( 0.10f,  0.0f), F( 0.20f,  0.0f) },
                [PlayerPositionGrp.K]     = new[] { F( 0.00f, -13f) },
                [PlayerPositionGrp.DL]    = new[] { F(-0.15f,  2.0f), F( 0.15f,  2.0f) },
                [PlayerPositionGrp.LB]    = new[] { F(-0.22f,  7.0f), F( 0.22f,  7.0f) },
                [PlayerPositionGrp.DB]    = new[] { F(-0.42f, 10.0f), F( 0.42f, 10.0f), F( 0.00f, 13.0f) },
            },
        };
    }

    // Shorthand for formation entries
    private static Vector2 F(float xFraction, float yardsFromLOS) => new Vector2(xFraction, yardsFromLOS);

    // -------------------------------------------------------
    // Helpers

    private static bool IsDefenseGroup(PlayerPositionGrp pos) =>
        pos == PlayerPositionGrp.DL || pos == PlayerPositionGrp.LB || pos == PlayerPositionGrp.DB;

    public List<BoardSlot> GetSlotsForPosition(PlayerPositionGrp position, int playerId)
    {
        if (slotMap.ContainsKey(playerId) && slotMap[playerId].ContainsKey(position))
            return slotMap[playerId][position];
        return new List<BoardSlot>();
    }
}
