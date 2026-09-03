using TcgEngine.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using TcgEngine;
using TcgEngine.Client;
using TcgEngine.Gameplay;

/// <summary>
/// Live ball HUD overlay. Appears during GamePhase.LiveBall.
/// Drag a live ball card onto the center drop zone to play it (auto-readies).
/// COMMIT PLAY confirms ready (with or without card). PASS/SKIP also confirms ready with no card.
/// Timer auto-passes when it hits 0.
/// Self-builds a ScreenSpaceOverlay canvas — do NOT parent under World Space GameCanvas.
/// </summary>
public class LiveBallCardZone : MonoBehaviour, IDropHandler
{
    // ── Colors ───────────────────────────────────────────────────────
    static readonly Color kMint        = new Color(0.667f, 1f,    0.863f, 1f);
    static readonly Color kMintBorder  = new Color(0.667f, 1f,    0.863f, 0.2f);
    static readonly Color kMintDash    = new Color(0.667f, 1f,    0.863f, 0.4f);
    static readonly Color kHudBg       = new Color(0.035f, 0.035f,0.043f, 0.92f);
    static readonly Color kTimerBg     = new Color(0.125f, 0.125f,0.122f, 1f);
    static readonly Color kTimerBorder = new Color(0.667f, 1f,    0.863f, 0.3f);
    static readonly Color kCommitText  = new Color(0f,     0.392f,0.294f, 1f);
    static readonly Color kPassBg      = new Color(0.094f, 0.094f,0.106f, 0.5f);
    static readonly Color kPassBorder  = new Color(0.153f, 0.153f,0.165f, 1f);
    static readonly Color kStatBg      = new Color(0.149f, 0.149f,0.149f, 0.5f);
    static readonly Color kDimText     = new Color(0.678f, 0.667f,0.667f, 1f);
    static readonly Color kStatusText  = new Color(0.322f, 0.322f,0.357f, 1f);
    static readonly Color kDotMint     = new Color(0.667f, 1f,    0.863f, 1f);
    static readonly Color kDotGray     = new Color(0.153f, 0.153f,0.165f, 1f);

    const float kTimerDuration = 15f;

    // ── State ────────────────────────────────────────────────────────
    float timerRemaining;
    bool timerRunning;
    GamePhase lastPhase = GamePhase.None;

    // ── UI refs ──────────────────────────────────────────────────────
    CanvasGroup hudGroup;
    Image darkOverlay;
    TextMeshProUGUI timerText;
    TextMeshProUGUI fieldPosText;
    Button commitBtn;
    Button passSkipBtn;

    static LiveBallCardZone _instance;
    public static LiveBallCardZone Get()
    {
        if (_instance == null)
        {
            var go = new GameObject("LiveBallCardZone");
            _instance = go.AddComponent<LiveBallCardZone>();
        }
        return _instance;
    }

    // ── Lifecycle ────────────────────────────────────────────────────

    void Awake()
    {
        _instance = this;
        BuildOverlay();
        SetHudVisible(false);
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    void Update()
    {
        GameClient client = GameClient.Get();
        if (client == null || !client.IsReady()) return;
        Game g = client.GetGameData();
        Player player = client.GetPlayer();
        if (g == null || player == null) return;

        bool isLiveBall  = g.phase == GamePhase.LiveBall;
        bool alreadyReady = player.IsReadyForPhase(GamePhase.LiveBall);

        // Detect phase entry to reset timer
        if (isLiveBall && lastPhase != GamePhase.LiveBall)
        {
            timerRemaining = kTimerDuration;
            timerRunning   = true;
        }
        lastPhase = g.phase;

        if (isLiveBall)
        {
            SetHudVisible(true);
            UpdateStatTrack(g);

            if (!alreadyReady)
            {
                hudGroup.alpha = 1f;
                if (commitBtn != null)  commitBtn.interactable  = true;
                if (passSkipBtn != null) passSkipBtn.interactable = true;

                if (timerRunning)
                {
                    timerRemaining -= Time.deltaTime;
                    int secs = Mathf.Max(0, Mathf.CeilToInt(timerRemaining));
                    if (timerText != null) timerText.text = $"{secs}S";
                    if (timerRemaining <= 0f)
                    {
                        timerRunning = false;
                        ConfirmReady(client, player, g);
                    }
                }
            }
            else
            {
                hudGroup.alpha = 0.5f;
                if (commitBtn != null)  commitBtn.interactable  = false;
                if (passSkipBtn != null) passSkipBtn.interactable = false;
                timerRunning = false;
                if (timerText != null) timerText.text = "—";
            }
        }
        else
        {
            SetHudVisible(false);
            timerRunning   = false;
            timerRemaining = kTimerDuration;
            if (timerText != null) timerText.text = $"{Mathf.CeilToInt(kTimerDuration)}S";
        }
    }

    void UpdateStatTrack(Game g)
    {
        if (fieldPosText == null) return;
        int bp = g.raw_ball_on;
        if (bp <= 0)
            fieldPosText.text = "OWN END ZONE";
        else if (bp <= 50)
            fieldPosText.text = $"OWN {bp}-YARD LINE";
        else if (bp < 100)
            fieldPosText.text = $"OPP {100 - bp}-YARD LINE";
        else
            fieldPosText.text = "OPP END ZONE";
    }

    // ── IDropHandler (called directly by CardDragHandler) ────────────

    public void OnDrop(PointerEventData eventData)
    {
        HandCard handCard = eventData.pointerDrag?.GetComponent<HandCard>();
        if (handCard == null) return;

        GameClient client = GameClient.Get();
        if (client == null) return;
        Game g = client.GetGameData();
        Player player = client.GetPlayer();
        if (g == null || player == null) return;
        if (g.phase != GamePhase.LiveBall) return;
        if (player.IsReadyForPhase(GamePhase.LiveBall)) return;

        Card card = handCard.GetCard();
        if (card == null || !card.CardData.IsLiveBall()) return;

        CardPlayError err = g.GetCardPlayError(card, CardPositionSlot.None);
        if (err != CardPlayError.None)
        {
            StatChangePopup.Instance?.ShowCustom(handCard.transform.position, Game.GetCardPlayErrorText(err), Color.red);
            handCard.EndDrag();
            return;
        }

        client.PlayCard(card, new CardPositionSlot());
        ConfirmReady(client, player, g);
        handCard.EndDrag();
    }

    // ── Button handlers ──────────────────────────────────────────────

    void OnClickCommitPlay()
    {
        GameClient client = GameClient.Get();
        if (client == null) return;
        Game g = client.GetGameData();
        Player player = client.GetPlayer();
        if (g == null || player == null) return;
        if (g.phase != GamePhase.LiveBall) return;
        if (player.IsReadyForPhase(GamePhase.LiveBall)) return;
        ConfirmReady(client, player, g);
    }

    void OnClickPassSkip()
    {
        GameClient client = GameClient.Get();
        if (client == null) return;
        Game g = client.GetGameData();
        Player player = client.GetPlayer();
        if (g == null || player == null) return;
        if (g.phase != GamePhase.LiveBall) return;
        if (player.IsReadyForPhase(GamePhase.LiveBall)) return;
        ConfirmReady(client, player, g);
    }

    void ConfirmReady(GameClient client, Player player, Game g)
    {
        player.SetReadyForPhase(g.phase, true);
        client.SendAction(GameAction.PlayerReadyPhase);
        timerRunning = false;
    }

    // ── Visibility ───────────────────────────────────────────────────

    void SetHudVisible(bool visible)
    {
        if (hudGroup == null) return;
        hudGroup.alpha          = visible ? 1f : 0f;
        hudGroup.interactable   = visible;
        hudGroup.blocksRaycasts = visible;

        if (darkOverlay != null)
            darkOverlay.color = visible ? new Color(0, 0, 0, 0.4f) : Color.clear;
    }

    // ── Build overlay ─────────────────────────────────────────────────

    void BuildOverlay()
    {
        // Root: Canvas (ScreenSpaceOverlay), no CanvasGroup here so dark overlay is truly passthrough
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        // Full-screen dark overlay (no raycast blocking)
        var overlayGO = UIChild("DarkOverlay", gameObject);
        darkOverlay = overlayGO.AddComponent<Image>();
        darkOverlay.raycastTarget = false;
        darkOverlay.color = Color.clear;
        Stretch(overlayGO);

        // HUD bar — anchored to bottom, full width, 255px tall
        var hudGO = UIChild("HUDBar", gameObject);
        var hudRT = RT(hudGO);
        hudRT.anchorMin      = new Vector2(0, 0);
        hudRT.anchorMax      = new Vector2(1, 0);
        hudRT.pivot          = new Vector2(0.5f, 0);
        hudRT.anchoredPosition = Vector2.zero;
        hudRT.sizeDelta      = new Vector2(0, 255);

        var hudBg = hudGO.AddComponent<Image>();
        hudBg.color = kHudBg;
        hudBg.raycastTarget = false;

        hudGroup = hudGO.AddComponent<CanvasGroup>();

        // 2px mint top border
        var topBorderGO = UIChild("TopBorder", hudGO);
        var tbRT = RT(topBorderGO);
        tbRT.anchorMin = new Vector2(0, 1); tbRT.anchorMax = new Vector2(1, 1);
        tbRT.pivot = new Vector2(0.5f, 1);
        tbRT.anchoredPosition = Vector2.zero;
        tbRT.sizeDelta = new Vector2(0, 2);
        var tbImg = topBorderGO.AddComponent<Image>();
        tbImg.color = kMintBorder;
        tbImg.raycastTarget = false;

        // Timer badge (hangs from top-center of HUD bar)
        BuildTimerBadge(hudGO);

        // Content row — 24px horizontal padding, starts 16px below top, 192px tall
        var contentGO = UIChild("ContentRow", hudGO);
        var contentRT = RT(contentGO);
        contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.anchoredPosition = new Vector2(0, -16);
        contentRT.sizeDelta = new Vector2(-48, 192); // -24px each side

        BuildStatTrack(contentGO);
        BuildDropZone(contentGO);
        BuildRightActions(contentGO);

        // Bottom decorative line
        BuildBottomLine(hudGO);
    }

    void BuildTimerBadge(GameObject parent)
    {
        var timerGO = UIChild("TimerBadge", parent);
        var tRT = RT(timerGO);
        tRT.anchorMin = new Vector2(0.5f, 1); tRT.anchorMax = new Vector2(0.5f, 1);
        tRT.pivot = new Vector2(0.5f, 0); // bottom of badge touches top of HUD
        tRT.anchoredPosition = new Vector2(0, 2);
        tRT.sizeDelta = new Vector2(97, 41);

        var bg = timerGO.AddComponent<Image>();
        bg.color = kTimerBg;
        bg.raycastTarget = false;

        // Bottom+side mint border (4 thin images)
        Image bdrL = BorderImg(UIChild("BL", timerGO), new Vector2(0,0), new Vector2(0,1), new Vector2(0,0.5f), new Vector2(1,0),  new Vector2(0, 0));
        Image bdrR = BorderImg(UIChild("BR", timerGO), new Vector2(1,0), new Vector2(1,1), new Vector2(1,0.5f), new Vector2(1,0),  new Vector2(0, 0));
        Image bdrB = BorderImg(UIChild("BB", timerGO), new Vector2(0,0), new Vector2(1,0), new Vector2(0.5f,0), new Vector2(0, 1), new Vector2(0, 0));
        bdrL.color = bdrR.color = bdrB.color = kTimerBorder;

        var textGO = UIChild("TimerText", timerGO);
        Stretch(textGO, 25, 4, 25, 5);
        timerText = textGO.AddComponent<TextMeshProUGUI>();
        timerText.text = "15S";
        timerText.fontSize = 24;
        timerText.color = kMint;
        timerText.fontStyle = FontStyles.Bold;
        timerText.alignment = TextAlignmentOptions.Center;
        timerText.characterSpacing = 24;
        timerText.raycastTarget = false;
    }

    // Helper: creates a 1px border image on one edge of a rect
    Image BorderImg(GameObject go, Vector2 ancMin, Vector2 ancMax, Vector2 piv, Vector2 sizeDeltaXY, Vector2 aPos)
    {
        var rt = RT(go);
        rt.anchorMin = ancMin; rt.anchorMax = ancMax; rt.pivot = piv;
        rt.anchoredPosition = aPos;
        // For left/right borders: sizeDelta=(1,0) meaning 1px wide, full height
        // For bottom border: sizeDelta=(0,1) meaning full width, 1px tall
        rt.sizeDelta = sizeDeltaXY;
        var img = go.AddComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    void BuildStatTrack(GameObject parent)
    {
        var statGO = UIChild("StatTrack", parent);
        var sRT = RT(statGO);
        sRT.anchorMin = new Vector2(0, 0.5f); sRT.anchorMax = new Vector2(0, 0.5f);
        sRT.pivot = new Vector2(0, 0.5f);
        sRT.anchoredPosition = Vector2.zero;
        sRT.sizeDelta = new Vector2(290, 68);

        var statBg = statGO.AddComponent<Image>();
        statBg.color = kStatBg;
        statBg.raycastTarget = false;

        // 4px mint left border
        var lbGO = UIChild("LeftBorder", statGO);
        var lbRT = RT(lbGO);
        lbRT.anchorMin = new Vector2(0,0); lbRT.anchorMax = new Vector2(0,1);
        lbRT.pivot = new Vector2(0,0.5f);
        lbRT.anchoredPosition = Vector2.zero;
        lbRT.sizeDelta = new Vector2(4, 0);
        var lbImg = lbGO.AddComponent<Image>();
        lbImg.color = kMint;
        lbImg.raycastTarget = false;

        // "CURRENT PLAY YARDAGE" label
        var labelGO = UIChild("Label", statGO);
        Stretch(labelGO, 16, 12, 12, 0);
        var labelTmp = labelGO.AddComponent<TextMeshProUGUI>();
        labelTmp.text = "CURRENT PLAY YARDAGE";
        labelTmp.fontSize = 9;
        labelTmp.color = kDimText;
        labelTmp.fontStyle = FontStyles.Bold;
        labelTmp.characterSpacing = 9;
        labelTmp.alignment = TextAlignmentOptions.Left;
        labelTmp.raycastTarget = false;

        // Field position value
        var valGO = UIChild("FieldPos", statGO);
        Stretch(valGO, 16, 28, 12, 12);
        fieldPosText = valGO.AddComponent<TextMeshProUGUI>();
        fieldPosText.text = "—";
        fieldPosText.fontSize = 18;
        fieldPosText.color = Color.white;
        fieldPosText.fontStyle = FontStyles.Bold;
        fieldPosText.alignment = TextAlignmentOptions.Left;
        fieldPosText.raycastTarget = false;
    }

    void BuildDropZone(GameObject parent)
    {
        // Outer container — centered in content row
        var outerGO = UIChild("DropZoneOuter", parent);
        var outerRT = RT(outerGO);
        outerRT.anchorMin = new Vector2(0.5f, 0.5f); outerRT.anchorMax = new Vector2(0.5f, 0.5f);
        outerRT.pivot = new Vector2(0.5f, 0.5f);
        outerRT.anchoredPosition = Vector2.zero;
        outerRT.sizeDelta = new Vector2(160, 192);

        // Dark background (raycastTarget=true so it can receive clicks, though drops are manual)
        var bgImg = outerGO.AddComponent<Image>();
        bgImg.color = new Color(0, 0, 0, 0.4f);
        bgImg.raycastTarget = true;

        // Solid mint outline border (approximate dashed with solid — 2px each edge)
        AddEdge(outerGO, "BorderTop",    new Vector2(0, 1),    new Vector2(1, 1),    new Vector2(0.5f, 1), new Vector2(0, 2));
        AddEdge(outerGO, "BorderBottom", new Vector2(0, 0),    new Vector2(1, 0),    new Vector2(0.5f, 0), new Vector2(0, 2));
        AddEdge(outerGO, "BorderLeft",   new Vector2(0, 0),    new Vector2(0, 1),    new Vector2(0, 0.5f), new Vector2(2, 0));
        AddEdge(outerGO, "BorderRight",  new Vector2(1, 0),    new Vector2(1, 1),    new Vector2(1, 0.5f), new Vector2(2, 0));

        // Corner L-accents (8×8, mint)
        AddCorner(outerGO, "TL", new Vector2(-72f,  88f));
        AddCorner(outerGO, "TR", new Vector2( 72f,  88f));
        AddCorner(outerGO, "BL", new Vector2(-72f, -88f));
        AddCorner(outerGO, "BR", new Vector2( 72f, -88f));

        // "DROP LIVE BALL" text
        var dropGO = UIChild("DropText", outerGO);
        var dropRT = RT(dropGO);
        dropRT.anchorMin = new Vector2(0, 0.5f); dropRT.anchorMax = new Vector2(1, 0.5f);
        dropRT.pivot = new Vector2(0.5f, 1);
        dropRT.anchoredPosition = new Vector2(0, -4);
        dropRT.sizeDelta = new Vector2(0, 15);
        var dropTmp = dropGO.AddComponent<TextMeshProUGUI>();
        dropTmp.text = "DROP LIVE BALL";
        dropTmp.fontSize = 10;
        dropTmp.color = kMint;
        dropTmp.fontStyle = FontStyles.Bold;
        dropTmp.alignment = TextAlignmentOptions.Center;
        dropTmp.characterSpacing = -5;
        dropTmp.raycastTarget = false;

        // "CARD HERE" subtext
        var subGO = UIChild("SubText", outerGO);
        var subRT = RT(subGO);
        subRT.anchorMin = new Vector2(0, 0.5f); subRT.anchorMax = new Vector2(1, 0.5f);
        subRT.pivot = new Vector2(0.5f, 1);
        subRT.anchoredPosition = new Vector2(0, -21);
        subRT.sizeDelta = new Vector2(0, 10);
        var subTmp = subGO.AddComponent<TextMeshProUGUI>();
        subTmp.text = "CARD HERE";
        subTmp.fontSize = 8;
        subTmp.color = kDimText;
        subTmp.fontStyle = FontStyles.Bold;
        subTmp.alignment = TextAlignmentOptions.Center;
        subTmp.characterSpacing = 8;
        subTmp.raycastTarget = false;
    }

    void AddEdge(GameObject parent, string name, Vector2 aMin, Vector2 aMax, Vector2 piv, Vector2 sizeDelta)
    {
        var go = UIChild(name, parent);
        var rt = RT(go);
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = piv;
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = sizeDelta;
        var img = go.AddComponent<Image>();
        img.color = kMintDash;
        img.raycastTarget = false;
    }

    void AddCorner(GameObject parent, string name, Vector2 aPos)
    {
        var go = UIChild(name, parent);
        var rt = RT(go);
        rt.anchorMin = new Vector2(0.5f, 0.5f); rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = aPos;
        rt.sizeDelta = new Vector2(8, 8);
        var img = go.AddComponent<Image>();
        img.color = kMint;
        img.raycastTarget = false;
    }

    void BuildRightActions(GameObject parent)
    {
        var actGO = UIChild("RightActions", parent);
        var actRT = RT(actGO);
        actRT.anchorMin = new Vector2(1, 0.5f); actRT.anchorMax = new Vector2(1, 0.5f);
        actRT.pivot = new Vector2(1, 0.5f);
        actRT.anchoredPosition = Vector2.zero;
        actRT.sizeDelta = new Vector2(290, 108);

        // COMMIT PLAY — top, full width, 58px
        var commitGO = UIChild("CommitPlayBtn", actGO);
        var commitRT = RT(commitGO);
        commitRT.anchorMin = new Vector2(0, 1); commitRT.anchorMax = new Vector2(1, 1);
        commitRT.pivot = new Vector2(0.5f, 1);
        commitRT.anchoredPosition = Vector2.zero;
        commitRT.sizeDelta = new Vector2(0, 58);
        var commitBg = commitGO.AddComponent<Image>();
        commitBg.color = kMint;
        commitBtn = commitGO.AddComponent<Button>();
        commitBtn.targetGraphic = commitBg;
        commitBtn.onClick.AddListener(OnClickCommitPlay);
        var commitTextGO = UIChild("Text", commitGO);
        Stretch(commitTextGO, 24, 0, 24, 0);
        var commitTmp = commitTextGO.AddComponent<TextMeshProUGUI>();
        commitTmp.text = "COMMIT PLAY";
        commitTmp.fontSize = 18;
        commitTmp.color = kCommitText;
        commitTmp.fontStyle = FontStyles.Bold;
        commitTmp.alignment = TextAlignmentOptions.Center;
        commitTmp.characterSpacing = -9;
        commitTmp.raycastTarget = false;

        // PASS / SKIP — below commit, left-aligned, 134×38
        var passGO = UIChild("PassSkipBtn", actGO);
        var passRT = RT(passGO);
        passRT.anchorMin = new Vector2(0, 1); passRT.anchorMax = new Vector2(0, 1);
        passRT.pivot = new Vector2(0, 1);
        passRT.anchoredPosition = new Vector2(0, -70);
        passRT.sizeDelta = new Vector2(134, 38);
        var passBg = passGO.AddComponent<Image>();
        passBg.color = kPassBg;
        passSkipBtn = passGO.AddComponent<Button>();
        passSkipBtn.targetGraphic = passBg;
        passSkipBtn.onClick.AddListener(OnClickPassSkip);

        // Pass border (1px outline)
        AddEdge(passGO, "BorderT", new Vector2(0,1), new Vector2(1,1), new Vector2(0.5f,1), new Vector2(0,1));
        AddEdge(passGO, "BorderB", new Vector2(0,0), new Vector2(1,0), new Vector2(0.5f,0), new Vector2(0,1));
        AddEdge(passGO, "BorderL", new Vector2(0,0), new Vector2(0,1), new Vector2(0,0.5f), new Vector2(1,0));
        AddEdge(passGO, "BorderR", new Vector2(1,0), new Vector2(1,1), new Vector2(1,0.5f), new Vector2(1,0));
        foreach (Transform t in passGO.transform)
        {
            var img = t.GetComponent<Image>();
            if (img != null && t.name.StartsWith("Border")) img.color = kPassBorder;
        }

        var passTextGO = UIChild("Text", passGO);
        Stretch(passTextGO, 25, 0, 15, 0);
        var passTmp = passTextGO.AddComponent<TextMeshProUGUI>();
        passTmp.text = "PASS / SKIP";
        passTmp.fontSize = 14;
        passTmp.color = Color.white;
        passTmp.fontStyle = FontStyles.Bold;
        passTmp.alignment = TextAlignmentOptions.Left;
        passTmp.characterSpacing = -7;
        passTmp.raycastTarget = false;
    }

    void BuildBottomLine(GameObject parent)
    {
        var lineGO = UIChild("BottomLine", parent);
        var lineRT = RT(lineGO);
        lineRT.anchorMin = new Vector2(0, 0); lineRT.anchorMax = new Vector2(1, 0);
        lineRT.pivot = new Vector2(0.5f, 0);
        lineRT.anchoredPosition = new Vector2(0, 16);
        lineRT.sizeDelta = new Vector2(-48, 14);

        // Left dots: mint(6) | gray(32) | gray(6)
        float[] lWidths = { 6, 32, 6 };
        Color[] lColors = { kDotMint, kDotGray, kDotGray };
        float xL = 0;
        for (int i = 0; i < 3; i++)
        {
            var d = UIChild($"DL{i}", lineGO);
            var dRT = RT(d);
            dRT.anchorMin = new Vector2(0, 0.5f); dRT.anchorMax = new Vector2(0, 0.5f);
            dRT.pivot = new Vector2(0, 0.5f);
            dRT.anchoredPosition = new Vector2(xL, 0);
            dRT.sizeDelta = new Vector2(lWidths[i], 4);
            var dImg = d.AddComponent<Image>();
            dImg.color = lColors[i];
            dImg.raycastTarget = false;
            xL += lWidths[i] + 4;
        }

        // Status text (centered)
        var statusGO = UIChild("StatusText", lineGO);
        Stretch(statusGO);
        var sTmp = statusGO.AddComponent<TextMeshProUGUI>();
        sTmp.text = "SYSTEM STATUS: LIVE_BALL_ACTIVE // P04";
        sTmp.fontSize = 9;
        sTmp.color = kStatusText;
        sTmp.fontStyle = FontStyles.Bold;
        sTmp.alignment = TextAlignmentOptions.Center;
        sTmp.characterSpacing = 9;
        sTmp.raycastTarget = false;

        // Right dots: gray(6) | gray(32) | mint(6)
        float[] rWidths = { 6, 32, 6 };
        Color[] rColors = { kDotGray, kDotGray, kDotMint };
        float xR = 0;
        for (int i = 2; i >= 0; i--)
        {
            var d = UIChild($"DR{i}", lineGO);
            var dRT = RT(d);
            dRT.anchorMin = new Vector2(1, 0.5f); dRT.anchorMax = new Vector2(1, 0.5f);
            dRT.pivot = new Vector2(1, 0.5f);
            dRT.anchoredPosition = new Vector2(-xR, 0);
            dRT.sizeDelta = new Vector2(rWidths[i], 4);
            var dImg = d.AddComponent<Image>();
            dImg.color = rColors[i];
            dImg.raycastTarget = false;
            xR += rWidths[i] + 4;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────

    GameObject UIChild(string name, GameObject parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    RectTransform RT(GameObject go) => go.GetComponent<RectTransform>();

    void Stretch(GameObject go, float left = 0, float top = 0, float right = 0, float bottom = 0)
    {
        var rt = RT(go);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }
}
