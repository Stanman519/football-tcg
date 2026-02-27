using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using Assets.TcgEngine.Scripts.Gameplay;

// ─────────────────────────────────────────────────────────────────────────────
// ReelUI  — kept for backwards compatibility; updated after each reel stops
// ─────────────────────────────────────────────────────────────────────────────
[Serializable]
public class ReelUI
{
    public Image TopImage;
    public Image MiddleImage;
    public Image BottomImage;
}

// ─────────────────────────────────────────────────────────────────────────────
// SlotMachineUI  — animated spinning slot machine display
//
// Required scene setup:
//   SlotMachinePanel  (RectTransform — this script lives here)
//     ReelContainer   (RectTransform — assign to reelContainer field)
//
// Everything else (reel viewports, scrolling strips, dividers, win-line) is
// built at runtime so no prefabs are needed.
// ─────────────────────────────────────────────────────────────────────────────
[Serializable]
public class SlotMachineUI : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("References")]
    public Transform reelContainer;        // Parent that will hold reel columns

    [Header("Sizing")]
    public float iconSize     = 110f;      // Width and height of one icon cell
    public float reelSpacing  =  18f;      // Gap between reel columns
    public float panelPadding =  20f;      // Internal padding on the slot panel

    [Header("Colours")]
    public Color panelBgColor = new Color(0.28f, 0.28f, 0.28f, 0.95f);  // Medium gray
    public Color frameColor   = new Color(0.85f, 0.65f, 0.10f, 1.00f);  // Gold
    public Color reelBgColor  = new Color(0.20f, 0.20f, 0.20f, 1.00f);  // Slightly darker gray
    public Color dividerColor = new Color(0.80f, 0.60f, 0.10f, 0.40f);  // Dim gold
    public Color winLineColor = new Color(1.00f, 0.90f, 0.20f, 0.80f);  // Bright gold
    public float winLineHeight = 4f;

    [Header("Spin Animation")]
    public float fastSpeed  = 900f;   // px/s during full spin
    public float slowSpeed  = 280f;   // px/s at end of deceleration
    public float decelTime  = 0.45f;  // seconds from fast → slow

    [Header("Layout — Full (SlotSpin phase)")]
    public Vector2 fullAnchorMin = new Vector2(0.15f, 0.20f);
    public Vector2 fullAnchorMax = new Vector2(0.85f, 0.80f);

    [Header("Layout — Mini (all other phases)")]
    public Vector2 miniAnchorMin = new Vector2(0.72f, 0.36f);
    public Vector2 miniAnchorMax = new Vector2(0.98f, 0.68f);

    // ── Runtime ───────────────────────────────────────────────────────────────
    public  List<ReelUI>      reelUIs  = new List<ReelUI>();   // readable by external code
    private List<ReelSpinner> spinners = new List<ReelSpinner>();
    private GameObject        slotMachinePanel;
    private Image             winLineImg;
    private int               stoppedCount;

    // ═════════════════════════════════════════════════════════════════════════
    // Unity lifecycle
    // ═════════════════════════════════════════════════════════════════════════

    // Awake runs before any Start(), so slotMachinePanel is ready even if
    // InitializeReels is called from another script's Start() (e.g. GameLogicService).
    private void Awake()
    {
        slotMachinePanel = reelContainer != null ? reelContainer.parent?.gameObject : null;
    }

    private void Start()
    {
        if (slotMachinePanel != null)
        {
            StylePanel();
            slotMachinePanel.SetActive(true);
            SetLayout(mini: true);
        }

        var client = GameClient.Get();
        if (client != null) client.onRefreshAll += OnGameDataRefreshed;
    }

    private void OnDestroy()
    {
        var client = GameClient.Get();
        if (client != null) client.onRefreshAll -= OnGameDataRefreshed;
    }

    private void Update()
    {
        for (int i = 0; i < spinners.Count; i++)
            spinners[i].Tick(Time.deltaTime);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Public API  (same signatures as original version)
    // ═════════════════════════════════════════════════════════════════════════
    public void InitializeReels(int numReels)
    {
        // Ensure panel reference is set if Awake hasn't run yet
        if (slotMachinePanel == null)
            slotMachinePanel = reelContainer != null ? reelContainer.parent?.gameObject : null;

        if (slotMachinePanel == null || reelContainer == null) return;

        // Clear previous reels
        foreach (Transform child in reelContainer) Destroy(child.gameObject);
        reelUIs.Clear();
        spinners.Clear();

        // Stretch reelContainer to fill the panel
        var cRT = reelContainer.GetComponent<RectTransform>();
        if (cRT != null)
        {
            cRT.anchorMin = Vector2.zero;
            cRT.anchorMax = Vector2.one;
            cRT.offsetMin = Vector2.zero;
            cRT.offsetMax = Vector2.zero;
        }

        // Place reels centered horizontally
        float totalW = numReels * iconSize + (numReels - 1) * reelSpacing;
        float startX = -totalW * 0.5f + iconSize * 0.5f;

        for (int i = 0; i < numReels; i++)
        {
            float xPos            = startX + i * (iconSize + reelSpacing);
            var (rui, spinner)    = BuildReelColumn(i, xPos);
            reelUIs.Add(rui);
            spinners.Add(spinner);
        }

        // Win-line overlay on top of everything
        if (winLineImg != null) Destroy(winLineImg.gameObject);
        winLineImg = BuildWinLine();
        SetWinLineAlpha(0f);
    }

    public void FireReelUI(List<ReelSpriteData> results, List<SlotData> slotData)
    {
        stoppedCount = 0;
        SetWinLineAlpha(0f);

        for (int i = 0; i < results.Count && i < spinners.Count; i++)
        {
            SlotData slot = slotData.First(d => d.id == i);
            int      idx  = i;   // capture for lambda
            spinners[i].Spin(slot.reelIconInventory, results[i], slot.stopDelay,
                             () => OnReelStopped(idx));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Event handlers
    // ═════════════════════════════════════════════════════════════════════════
    private void OnGameDataRefreshed()
    {
        Game gd = GameClient.Get()?.GetGameData();
        if (gd == null || slotMachinePanel == null) return;
        SetLayout(mini: gd.phase != GamePhase.SlotSpin);
    }

    private void OnReelStopped(int reelIndex)
    {
        spinners[reelIndex].CopyFinalToReelUI(reelUIs[reelIndex]);

        stoppedCount++;
        if (stoppedCount >= spinners.Count)
            StartCoroutine(WinLineFlash());
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Layout switching
    // ═════════════════════════════════════════════════════════════════════════
    private void SetLayout(bool mini)
    {
        if (slotMachinePanel == null) return;
        var rt = slotMachinePanel.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin = mini ? miniAnchorMin : fullAnchorMin;
        rt.anchorMax = mini ? miniAnchorMax : fullAnchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Visual construction
    // ═════════════════════════════════════════════════════════════════════════
    private void StylePanel()
    {
        var img = slotMachinePanel.GetComponent<Image>()
               ?? slotMachinePanel.AddComponent<Image>();
        img.color = panelBgColor;

        var ol = slotMachinePanel.GetComponent<Outline>()
              ?? slotMachinePanel.AddComponent<Outline>();
        ol.effectColor    = frameColor;
        ol.effectDistance = new Vector2(4f, 4f);
    }

    // -------------------------------------------------------------------------
    // BuildReelColumn: creates one masked viewport + scrolling strip
    //
    // Strip layout (5 slots, only the 3 middle ones are ever visible):
    //
    //   [Slot 0]  ← hidden ABOVE viewport (incoming from top)
    //   [Slot 1]  ← TOP    visible row
    //   [Slot 2]  ← MIDDLE visible row  (the "win line" row)
    //   [Slot 3]  ← BOTTOM visible row
    //   [Slot 4]  ← hidden BELOW viewport
    //
    // Resting anchoredPosition.y = +iconSize so slots 1‑3 fill the viewport.
    // Spinning decreases y; when y reaches 0, sprites shift down by one and
    // y resets to +iconSize, creating a seamless scroll.
    // -------------------------------------------------------------------------
    private (ReelUI, ReelSpinner) BuildReelColumn(int index, float xPos)
    {
        float viewH = iconSize * 3f;

        // ── Viewport (clips strip to 3-slot window) ───────────────────────
        var vpGO = new GameObject($"Reel_{index}");
        vpGO.transform.SetParent(reelContainer, false);

        var vpRT = vpGO.AddComponent<RectTransform>();
        vpRT.anchorMin        = new Vector2(0.5f, 0.5f);
        vpRT.anchorMax        = new Vector2(0.5f, 0.5f);
        vpRT.pivot            = new Vector2(0.5f, 0.5f);
        vpRT.sizeDelta        = new Vector2(iconSize, viewH);
        vpRT.anchoredPosition = new Vector2(xPos, 0f);

        var vpBg = vpGO.AddComponent<Image>();
        vpBg.color = reelBgColor;

        vpGO.AddComponent<RectMask2D>();   // clips children to this rect

        // ── Scrolling strip ───────────────────────────────────────────────
        var stripGO = new GameObject("Strip");
        stripGO.transform.SetParent(vpGO.transform, false);

        var stripRT = stripGO.AddComponent<RectTransform>();
        stripRT.anchorMin        = new Vector2(0f, 1f);   // anchor: top-left of viewport
        stripRT.anchorMax        = new Vector2(1f, 1f);
        stripRT.pivot            = new Vector2(0f, 1f);
        stripRT.sizeDelta        = new Vector2(0f, iconSize * 5f);
        stripRT.anchoredPosition = new Vector2(0f, iconSize);  // resting: shows slots 1‑3

        // ── 5 slot images ─────────────────────────────────────────────────
        const int SLOTS = 5;
        var slotImgs = new Image[SLOTS];
        for (int s = 0; s < SLOTS; s++)
        {
            var sGO = new GameObject($"Slot_{s}");
            sGO.transform.SetParent(stripGO.transform, false);

            var sRT = sGO.AddComponent<RectTransform>();
            sRT.anchorMin        = new Vector2(0f, 1f);
            sRT.anchorMax        = new Vector2(1f, 1f);
            sRT.pivot            = new Vector2(0f, 1f);
            sRT.sizeDelta        = new Vector2(0f, iconSize);
            sRT.anchoredPosition = new Vector2(0f, -s * iconSize);

            var img = sGO.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget  = false;
            slotImgs[s] = img;
        }

        // ── Thin gold dividers between visible rows ───────────────────────
        // Positioned in viewport-local space so they stay fixed while strip scrolls
        AddDivider(vpGO.transform, -iconSize,       "Divider_TopMid");
        AddDivider(vpGO.transform, -iconSize * 2f,  "Divider_MidBot");

        // ── Result references (kept in sync when reel stops) ──────────────
        var reelUI = new ReelUI
        {
            TopImage    = slotImgs[1],
            MiddleImage = slotImgs[2],
            BottomImage = slotImgs[3]
        };

        var spinner = new ReelSpinner(stripRT, slotImgs, iconSize, fastSpeed, slowSpeed, decelTime);
        return (reelUI, spinner);
    }

    private void AddDivider(Transform parent, float localY, string goName)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(0f, 1f);
        rt.sizeDelta        = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(0f, localY);

        var img = go.AddComponent<Image>();
        img.color         = dividerColor;
        img.raycastTarget = false;
    }

    private Image BuildWinLine()
    {
        // Sibling of reelContainer (direct child of slotMachinePanel)
        var go = new GameObject("WinLine");
        go.transform.SetParent(slotMachinePanel.transform, false);
        go.transform.SetAsLastSibling();   // draws on top

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.04f, 0.5f);   // spans almost full width, centred vertically
        rt.anchorMax = new Vector2(0.96f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(0f, winLineHeight);

        var img = go.AddComponent<Image>();
        img.color         = winLineColor;
        img.raycastTarget = false;
        return img;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Win-line helpers
    // ─────────────────────────────────────────────────────────────────────
    private void SetWinLineAlpha(float a)
    {
        if (winLineImg == null) return;
        Color c = winLineImg.color;
        winLineImg.color = new Color(c.r, c.g, c.b, a);
    }

    private IEnumerator WinLineFlash()
    {
        for (int i = 0; i < 4; i++)
        {
            SetWinLineAlpha(0.90f);
            yield return new WaitForSeconds(0.13f);
            SetWinLineAlpha(0.05f);
            yield return new WaitForSeconds(0.10f);
        }
        SetWinLineAlpha(0.40f);   // settle to a dim ambient glow
    }

    // ═════════════════════════════════════════════════════════════════════════
    // ReelSpinner — per-reel scrolling animation
    // ═════════════════════════════════════════════════════════════════════════
    private class ReelSpinner
    {
        // ── Config ────────────────────────────────────────────────────────
        private readonly RectTransform strip;
        private readonly Image[]       slots;       // 5 image objects
        private readonly float         iconSize;
        private readonly float         fastSpeed;
        private readonly float         slowSpeed;
        private readonly float         decelTime;

        // ── Per-spin state ────────────────────────────────────────────────
        private List<SlotIconData> inventory;
        private float  scrollY;        // strip anchoredPosition.y; iconSize = resting
        private float  currentSpeed;
        private float  elapsed;
        private float  stopAt;         // time (seconds) at which we begin decelerating
        private bool   spinning;
        private bool   stopping;

        private Sprite fTop, fMid, fBot;   // final result sprites
        private Action onStopped;

        // ── Constructor ───────────────────────────────────────────────────
        public ReelSpinner(RectTransform strip, Image[] slots, float iconSize,
                           float fastSpeed, float slowSpeed, float decelTime)
        {
            this.strip     = strip;
            this.slots     = slots;
            this.iconSize  = iconSize;
            this.fastSpeed = fastSpeed;
            this.slowSpeed = slowSpeed;
            this.decelTime = decelTime;
        }

        // ── Public ────────────────────────────────────────────────────────
        public void Spin(List<SlotIconData> inventory, ReelSpriteData result,
                         float stopDelay, Action onStoppedCallback)
        {
            this.inventory  = inventory;
            this.stopAt     = stopDelay;
            this.onStopped  = onStoppedCallback;

            fTop = result.Top?.Image;
            fMid = result.Middle?.Image;
            fBot = result.Bottom?.Image;

            elapsed      = 0f;
            currentSpeed = fastSpeed;
            scrollY      = iconSize;   // resting position: slots 1‑3 are visible
            spinning     = true;
            stopping     = false;

            // Fill all slots with random starting sprites
            for (int i = 0; i < slots.Length; i++)
                slots[i].sprite = RandomSprite();

            Apply();
        }

        public void Tick(float dt)
        {
            if (!spinning) return;
            elapsed += dt;

            // ── Speed control ────────────────────────────────────────────
            if (!stopping && elapsed >= stopAt)
                stopping = true;

            if (stopping)
            {
                // Decelerate smoothly from fastSpeed → slowSpeed over decelTime
                float t = Mathf.Clamp01((elapsed - stopAt) / decelTime);
                currentSpeed = Mathf.Lerp(fastSpeed, slowSpeed, t);
            }

            // ── Advance the strip ────────────────────────────────────────
            scrollY -= currentSpeed * dt;

            if (scrollY <= 0f)
            {
                if (stopping && currentSpeed <= slowSpeed + 15f)
                {
                    // Reel has slowed enough — lock in the final result
                    scrollY      = iconSize;
                    slots[1].sprite = fTop;
                    slots[2].sprite = fMid;
                    slots[3].sprite = fBot;
                    slots[0].sprite = RandomSprite();   // above viewport
                    slots[4].sprite = RandomSprite();   // below viewport
                    spinning = false;
                    stopping = false;
                    Apply();
                    onStopped?.Invoke();
                    return;
                }

                // Normal wrap: shift sprites down by one row, reset strip position
                scrollY += iconSize;
                ShiftStrip();
            }

            Apply();
        }

        // Call after spin stops so that external ReelUI references are in sync
        public void CopyFinalToReelUI(ReelUI rui)
        {
            rui.TopImage.sprite    = fTop;
            rui.MiddleImage.sprite = fMid;
            rui.BottomImage.sprite = fBot;
        }

        // ── Private helpers ───────────────────────────────────────────────

        // Reel scrolls DOWN → each icon descends one row per cycle.
        // Slot 0 (above viewport) enters from the top and conceptually
        // "settles" into Slot 1 after the wrap; everything else shifts down.
        private void ShiftStrip()
        {
            slots[4].sprite = slots[3].sprite;
            slots[3].sprite = slots[2].sprite;
            slots[2].sprite = slots[1].sprite;
            slots[1].sprite = slots[0].sprite;  // the one that just entered from above
            slots[0].sprite = RandomSprite();   // fresh icon ready above the viewport
        }

        private void Apply()
        {
            strip.anchoredPosition = new Vector2(0f, scrollY);
        }

        private Sprite RandomSprite()
        {
            if (inventory == null || inventory.Count == 0) return null;
            return inventory[UnityEngine.Random.Range(0, inventory.Count)].IconSprite;
        }
    }
}
