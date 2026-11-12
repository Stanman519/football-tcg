using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine;
using TcgEngine.Gameplay;

/// <summary>
/// Draws X (offense) or O (defense) markers at formation positions inside a RectTransform.
/// Draws route lines for skill positions (RB on run, WR/TE/RB on pass).
/// </summary>
public class FormationPreviewUI : MonoBehaviour
{
    [Header("Settings")]
    public Color offenseColor = new Color(0.95f, 0.95f, 0.95f, 1f);
    public Color defenseColor = new Color(0.7f, 0.85f, 1f, 1f);
    public Color routeColor   = new Color(0.91f, 0.39f, 0.10f, 0.8f);
    public float markerSize   = 8f;
    public float routeWidth   = 1.5f;

    private RectTransform rect;
    private List<GameObject> markers = new List<GameObject>();

    void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    private Coroutine deferred;

    public void SetFormation(FormationData formation, PlayType playType, bool isDefense, CoachCardData coach)
    {
        if (deferred != null) { StopCoroutine(deferred); deferred = null; }
        ClearMarkers();
        if (rect == null) rect = GetComponent<RectTransform>(); // SetFormation can beat Awake on first show
        if (formation == null || rect == null) return;

        // First time the panel opens the layout hasn't run: the rect is 0x0 and every marker
        // lands in one corner. Force a layout pass; if it still has no size, draw next frame.
        if (rect.rect.width < 1f || rect.rect.height < 1f)
        {
            Canvas.ForceUpdateCanvases();
            if (rect.parent is RectTransform parentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
        }
        if (rect.rect.width < 1f || rect.rect.height < 1f)
        {
            if (isActiveAndEnabled)
                deferred = StartCoroutine(DrawWhenSized(formation, playType, isDefense, coach));
            return;
        }

        Draw(formation, playType, isDefense, coach);
    }

    private System.Collections.IEnumerator DrawWhenSized(FormationData formation, PlayType playType, bool isDefense, CoachCardData coach)
    {
        for (int i = 0; i < 10 && (rect.rect.width < 1f || rect.rect.height < 1f); i++)
            yield return null;
        deferred = null;
        ClearMarkers();
        if (rect.rect.width >= 1f && rect.rect.height >= 1f)
            Draw(formation, playType, isDefense, coach);
    }

    private void Draw(FormationData formation, PlayType playType, bool isDefense, CoachCardData coach)
    {
        float w = rect.rect.width;
        float h = rect.rect.height;

        foreach (var slot in formation.slots)
        {
            Vector2 pos = SlotToLocal(slot, w, h, isDefense);
            bool showRoute = ShouldShowRoute(slot.posGroup, playType, isDefense);
            CreateMarker(pos, isDefense, slot.posGroup);

            if (showRoute && coach != null)
            {
                RouteData route = isDefense
                    ? coach.GetDefenseRoute(playType, slot.posGroup, slot.slotIndex)
                    : coach.GetOffenseRoute(playType, slot.posGroup, slot.slotIndex);
                if (route != null)
                    DrawRoute(pos, route, slot, w, h, isDefense);
            }
        }
    }

    private Vector2 SlotToLocal(FormationSlotEntry slot, float w, float h, bool isDefense)
    {
        // xFraction: -0.5..+0.5 → map to 0..w
        float x = (slot.xFraction + 0.5f) * w;
        // yardsFromLOS: negative = backfield (bottom), positive = opponent (top)
        // Normalize: assume -20..+20 yard range → 0..h
        float yNorm = (slot.yardsFromLOS + 20f) / 40f;
        if (isDefense) yNorm = 1f - yNorm; // flip for defense perspective
        float y = yNorm * h;
        return new Vector2(x, y);
    }

    private bool ShouldShowRoute(PlayerPositionGrp pos, PlayType playType, bool isDefense)
    {
        if (isDefense) return pos == PlayerPositionGrp.DB || pos == PlayerPositionGrp.LB;

        switch (playType)
        {
            case PlayType.Run:
                return pos == PlayerPositionGrp.RB_TE;
            case PlayType.ShortPass:
            case PlayType.LongPass:
                return pos == PlayerPositionGrp.WR || pos == PlayerPositionGrp.RB_TE;
            default:
                return false;
        }
    }

    private void CreateMarker(Vector2 localPos, bool isDefense, PlayerPositionGrp posGroup)
    {
        var go = new GameObject("Marker");
        go.transform.SetParent(transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = localPos;
        rt.sizeDelta = new Vector2(markerSize, markerSize);

        var txt = go.AddComponent<TMPro.TextMeshProUGUI>();
        txt.text = isDefense ? "O" : "X";
        txt.fontSize = markerSize * 1.2f;
        txt.alignment = TMPro.TextAlignmentOptions.Center;
        txt.color = isDefense ? defenseColor : offenseColor;

        markers.Add(go);
    }

    private void DrawRoute(Vector2 startPos, RouteData route, FormationSlotEntry slot, float w, float h, bool isDefense)
    {
        if (route.keyframes == null || route.keyframes.Count == 0) return;

        Vector2 prev = startPos;
        foreach (var kf in route.keyframes)
        {
            foreach (var wp in kf.waypoints)
            {
                if (wp.posGroup != slot.posGroup || wp.slotIndex != slot.slotIndex) continue;

                // deltaX/deltaYards are relative offsets in yards
                float dx = (wp.deltaX / 53.3f) * w;  // 53.3 yard field width
                float dy = (wp.deltaYards / 40f) * h;
                if (isDefense) dy = -dy;

                Vector2 next = startPos + new Vector2(dx, dy);
                CreateLine(prev, next);
                prev = next;
            }
        }
    }

    private void CreateLine(Vector2 from, Vector2 to)
    {
        var go = new GameObject("RouteLine");
        go.transform.SetParent(transform, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = new Vector2(0f, 0.5f);

        Vector2 dir = to - from;
        float len = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        rt.anchoredPosition = from;
        rt.sizeDelta = new Vector2(len, routeWidth);
        rt.localRotation = Quaternion.Euler(0, 0, angle);

        var img = go.AddComponent<Image>();
        img.color = routeColor;

        markers.Add(go);
    }

    private void ClearMarkers()
    {
        foreach (var m in markers)
            if (m != null) Destroy(m);
        markers.Clear();
    }
}
