#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

/// <summary>
/// Rebuilds the PlayCallPanel to match the Figma "Project-inches" design (850x640).
/// Layout: Left column (coaches), Right column (scouting + play cards + enhancer + confirm).
/// Timer overlay at top-center. Green left accent border.
/// Menu: First&Long > Rebuild Play Call Panel
/// </summary>
public static class PlayCallPanelBuilder
{
    // ── Figma Colors ─────────────────────────────────────────────────────
    static readonly Color C_PANEL_BG     = HexColor("#131313");
    static readonly Color C_GREEN        = HexColor("#AAFFDC");
    static readonly Color C_GREEN_DARK   = HexColor("#00654B");
    static readonly Color C_RED          = HexColor("#FF716C");
    static readonly Color C_ORANGE       = HexColor("#FF8C00");
    static readonly Color C_TILE_BG      = HexColor("#1A1A1A");
    static readonly Color C_TILE_BORDER  = new Color(0.282f, 0.282f, 0.278f, 0.3f);  // rgba(72,72,71,0.3)
    static readonly Color C_DEF_HDR_BG   = new Color(1f, 0.443f, 0.424f, 0.1f);       // rgba(255,113,108,0.1)
    static readonly Color C_DEF_HDR_BORDER = new Color(1f, 0.443f, 0.424f, 0.4f);     // rgba(255,113,108,0.4)
    static readonly Color C_COACH_BORDER_RED  = new Color(1f, 0.443f, 0.424f, 0.3f);
    static readonly Color C_COACH_BORDER_GRN  = new Color(0.667f, 1f, 0.863f, 0.3f);
    static readonly Color C_COACH_BG     = HexColor("#20201F");
    static readonly Color C_INFO_BG      = HexColor("#1A1A1A");
    static readonly Color C_TIMER_BG     = HexColor("#20201F");
    static readonly Color C_TEXT_WHITE   = Color.white;
    static readonly Color C_TEXT_MUTED   = HexColor("#ADAAAA");
    static readonly Color C_ENH_BORDER   = new Color(0.282f, 0.282f, 0.278f, 0.3f);
    static readonly Color C_ENH_TEXT     = HexColor("#484847");
    static readonly Color C_PREVIEW_BG   = Color.black;
    static readonly Color C_RED_LABEL_BG = new Color(1f, 0.443f, 0.424f, 0.9f);
    static readonly Color C_GREEN_LABEL_BG = new Color(0.667f, 1f, 0.863f, 0.9f);

    // ── Figma Sizes ──────────────────────────────────────────────────────
    const float PANEL_W = 850f, PANEL_H = 640f;
    const float LEFT_COL_W = 128f;
    const float AVATAR_H = 112f, INFO_H = 34f;
    const float GAP = 16f;
    const float SCOUTING_H = 66f;
    const float CARD_H = 294f;
    const float ENH_H = 56f;
    const float CONFIRM_H = 60f;
    const float TIMER_W = 98f, TIMER_H = 41f;
    const float BORDER_W = 4f;
    const float PAD = 24f;
    const float COACH_GAP = 16f;

    // ── Font paths ───────────────────────────────────────────────────────
    const string FONT_SPACE = "Assets/TcgEngine/Fonts/SpaceGrotesk-Bold SDF.asset";
    const string FONT_INTER_BOLD = "Assets/TcgEngine/Fonts/Inter-Bold SDF.asset";
    const string FONT_INTER_MED = "Assets/TcgEngine/Fonts/Inter-Medium SDF.asset";
    const string FONT_NOTO = "Assets/TcgEngine/Fonts/NotoSans-Condensed SDF.asset";

    static TMP_FontAsset fontSpace, fontInterBold, fontInterMed, fontNoto;

    [MenuItem("First&Long/Rebuild Play Call Panel")]
    static void Build()
    {
        var panelObj = GameObject.Find("PlayCallPanel");
        if (panelObj == null)
        {
            EditorUtility.DisplayDialog("Rebuild Panel",
                "No GameObject named 'PlayCallPanel' found in the scene.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Rebuild Play Call Panel",
                "Delete all children and rebuild to Figma design?", "Rebuild", "Cancel"))
            return;

        // Load fonts
        fontSpace = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_SPACE);
        fontInterBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_INTER_BOLD);
        fontInterMed = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_INTER_MED);
        fontNoto = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_NOTO);

        if (fontSpace == null) Debug.LogWarning("[Builder] Missing font: " + FONT_SPACE);
        if (fontInterBold == null) Debug.LogWarning("[Builder] Missing font: " + FONT_INTER_BOLD);

        Undo.RecordObject(panelObj, "Rebuild PlayCallPanel");

        while (panelObj.transform.childCount > 0)
            Undo.DestroyObjectImmediate(panelObj.transform.GetChild(0).gameObject);

        // Panel RectTransform
        var panelRect = panelObj.GetComponent<RectTransform>();
        if (panelRect == null) panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(PANEL_W, PANEL_H);
        panelRect.anchoredPosition = Vector2.zero;

        // ── Background with green left border ────────────────────────────
        var bg = MakeRect("PanelBg", panelObj.transform);
        Stretch(bg);
        bg.gameObject.AddComponent<Image>().color = C_PANEL_BG;

        // Green left accent border (4px)
        var leftBorder = MakeRect("LeftBorder", bg.transform);
        leftBorder.anchorMin = new Vector2(0, 0);
        leftBorder.anchorMax = new Vector2(0, 1);
        leftBorder.pivot = new Vector2(0, 0.5f);
        leftBorder.sizeDelta = new Vector2(BORDER_W, 0);
        leftBorder.anchoredPosition = Vector2.zero;
        leftBorder.gameObject.AddComponent<Image>().color = C_GREEN;

        // ── Header ("CHOOSE YOUR PLAY") ──────────────────────────────────
        var headerRect = MakeRect("Header", bg.transform);
        headerRect.anchorMin = new Vector2(0, 1);
        headerRect.anchorMax = new Vector2(0, 1);
        headerRect.pivot = new Vector2(0, 1);
        headerRect.sizeDelta = new Vector2(300, 36);
        headerRect.anchoredPosition = new Vector2(PAD, -PAD);

        // Green bar before title
        var headerBar = MakeRect("HeaderBar", headerRect.transform);
        headerBar.anchorMin = new Vector2(0, 0.1f);
        headerBar.anchorMax = new Vector2(0, 0.9f);
        headerBar.pivot = new Vector2(0, 0.5f);
        headerBar.sizeDelta = new Vector2(8, 0);
        headerBar.anchoredPosition = Vector2.zero;
        headerBar.gameObject.AddComponent<Image>().color = C_GREEN;

        var headerTxt = AddTmpText(headerRect, "CHOOSE YOUR PLAY", fontSpace, 30,
            C_TEXT_WHITE, TextAlignmentOptions.MidlineLeft);
        var headerTxtRect = headerTxt.GetComponent<RectTransform>();
        headerTxtRect.anchorMin = Vector2.zero;
        headerTxtRect.anchorMax = Vector2.one;
        headerTxtRect.offsetMin = new Vector2(16, 0);
        headerTxtRect.offsetMax = Vector2.zero;
        headerTxt.fontStyle = FontStyles.Bold | FontStyles.Italic;
        headerTxt.characterSpacing = -3f;

        // ── Timer (top center, overlapping panel edge) ───────────────────
        var timerRoot = MakeRect("TimerOverlay", bg.transform);
        timerRoot.anchorMin = new Vector2(0.5f, 1);
        timerRoot.anchorMax = new Vector2(0.5f, 1);
        timerRoot.pivot = new Vector2(0.5f, 1);
        timerRoot.sizeDelta = new Vector2(TIMER_W, TIMER_H + 8);
        timerRoot.anchoredPosition = Vector2.zero;

        var timerBg = MakeRect("TimerBg", timerRoot.transform);
        timerBg.anchorMin = new Vector2(0, 0);
        timerBg.anchorMax = new Vector2(1, 1);
        timerBg.pivot = new Vector2(0.5f, 1);
        timerBg.offsetMin = new Vector2(0, 0);
        timerBg.offsetMax = new Vector2(0, -8);
        var timerBgImg = timerBg.gameObject.AddComponent<Image>();
        timerBgImg.color = C_TIMER_BG;

        // Timer border (bottom, left, right in green)
        var timerOutline = timerBg.gameObject.AddComponent<Outline>();
        timerOutline.effectColor = C_COACH_BORDER_GRN;
        timerOutline.effectDistance = new Vector2(1, 1);

        var timerTxt = AddTmpText(timerBg, "15S", fontSpace, 24,
            C_GREEN, TextAlignmentOptions.Center);
        var timerTxtRect = timerTxt.GetComponent<RectTransform>();
        Stretch(timerTxtRect);
        timerTxt.characterSpacing = 5f;

        // ── Content Body ─────────────────────────────────────────────────
        float contentTop = 68f; // below header
        var contentBody = MakeRect("ContentBody", bg.transform);
        contentBody.anchorMin = new Vector2(0, 0);
        contentBody.anchorMax = new Vector2(1, 1);
        contentBody.pivot = new Vector2(0.5f, 0.5f);
        contentBody.offsetMin = new Vector2(BORDER_W + PAD, PAD);
        contentBody.offsetMax = new Vector2(-PAD, -contentTop);

        // ── LEFT COLUMN: Stacked Coaches ─────────────────────────────────
        var leftCol = MakeRect("LeftColumn", contentBody.transform);
        leftCol.anchorMin = new Vector2(0, 0);
        leftCol.anchorMax = new Vector2(0, 1);
        leftCol.pivot = new Vector2(0, 1);
        leftCol.sizeDelta = new Vector2(LEFT_COL_W, 0);
        leftCol.anchoredPosition = Vector2.zero;

        var defCoachUI = BuildCoachCard(leftCol.transform, "DefCoachCard", true, 0);
        var myCoachUI = BuildCoachCard(leftCol.transform, "MyCoachCard", false, -(AVATAR_H + INFO_H + 8 + COACH_GAP));

        // ── RIGHT COLUMN ─────────────────────────────────────────────────
        float rightX = LEFT_COL_W + GAP;
        var rightCol = MakeRect("RightColumn", contentBody.transform);
        rightCol.anchorMin = new Vector2(0, 0);
        rightCol.anchorMax = new Vector2(1, 1);
        rightCol.pivot = new Vector2(0, 1);
        rightCol.offsetMin = new Vector2(rightX, 0);
        rightCol.offsetMax = Vector2.zero;

        // ── Scouting Header (3 columns) ──────────────────────────────────
        var scoutingArea = MakeRect("ScoutingHeader", rightCol.transform);
        scoutingArea.anchorMin = new Vector2(0, 1);
        scoutingArea.anchorMax = new Vector2(1, 1);
        scoutingArea.pivot = new Vector2(0, 1);
        scoutingArea.sizeDelta = new Vector2(0, SCOUTING_H);
        scoutingArea.anchoredPosition = Vector2.zero;

        var scoutHlg = scoutingArea.gameObject.AddComponent<HorizontalLayoutGroup>();
        scoutHlg.spacing = GAP;
        scoutHlg.childControlWidth = scoutHlg.childControlHeight = true;
        scoutHlg.childForceExpandWidth = scoutHlg.childForceExpandHeight = true;

        string[] defLabels = { "RUN DEFENSE", "SHORT DEFENSE", "LONG DEFENSE" };
        string[] defValues = { "0", "0", "0" };
        var scoutLabels = new TextMeshProUGUI[3];
        var scoutValues = new TextMeshProUGUI[3];
        for (int i = 0; i < 3; i++)
        {
            var col = MakeRect($"ScoutCol{i}", scoutingArea.transform);
            col.gameObject.AddComponent<LayoutElement>();

            var colBg = col.gameObject.AddComponent<Image>();
            colBg.color = C_DEF_HDR_BG;

            // Bottom border (red accent)
            var borderBottom = MakeRect("BorderBottom", col.transform);
            borderBottom.anchorMin = new Vector2(0, 0);
            borderBottom.anchorMax = new Vector2(1, 0);
            borderBottom.pivot = new Vector2(0.5f, 0);
            borderBottom.sizeDelta = new Vector2(0, 2);
            borderBottom.anchoredPosition = Vector2.zero;
            borderBottom.gameObject.AddComponent<Image>().color = C_DEF_HDR_BORDER;

            // Label
            var lbl = AddTmpText(col, defLabels[i], fontSpace, 8,
                new Color(1f, 0.443f, 0.424f, 0.6f), TextAlignmentOptions.Top);
            var lblRect = lbl.GetComponent<RectTransform>();
            lblRect.anchorMin = new Vector2(0, 0.5f);
            lblRect.anchorMax = new Vector2(1, 1);
            lblRect.offsetMin = new Vector2(0, 0);
            lblRect.offsetMax = new Vector2(0, -8);
            lbl.characterSpacing = 2f;
            scoutLabels[i] = lbl;

            // Value (big number)
            var val = AddTmpText(col, defValues[i], fontSpace, 24,
                C_TEXT_WHITE, TextAlignmentOptions.Bottom);
            var valRect = val.GetComponent<RectTransform>();
            valRect.anchorMin = new Vector2(0, 0);
            valRect.anchorMax = new Vector2(1, 0.55f);
            valRect.offsetMin = new Vector2(0, 10);
            valRect.offsetMax = Vector2.zero;
            scoutValues[i] = val;
        }

        // ── Play Cards (3 columns) ───────────────────────────────────────
        float cardsY = -(SCOUTING_H + GAP);
        var cardsArea = MakeRect("PlayCards", rightCol.transform);
        cardsArea.anchorMin = new Vector2(0, 1);
        cardsArea.anchorMax = new Vector2(1, 1);
        cardsArea.pivot = new Vector2(0, 1);
        cardsArea.sizeDelta = new Vector2(0, CARD_H);
        cardsArea.anchoredPosition = new Vector2(0, cardsY);

        var cardsHlg = cardsArea.gameObject.AddComponent<HorizontalLayoutGroup>();
        cardsHlg.spacing = GAP;
        cardsHlg.childControlWidth = cardsHlg.childControlHeight = true;
        cardsHlg.childForceExpandWidth = cardsHlg.childForceExpandHeight = true;

        var (runBtn, runImg, runYardTxt, runDefTxt, runPreview, runIncompleteTxt) =
            MakePlayCard(cardsArea.transform, "RunCard", "RUN", false);
        var (shortBtn, shortImg, shortYardTxt, shortDefTxt, shortPreview, shortIncompleteTxt) =
            MakePlayCard(cardsArea.transform, "ShortPassCard", "SHORT PASS", true);
        var (longBtn, longImg, longYardTxt, longDefTxt, longPreview, longIncompleteTxt) =
            MakePlayCard(cardsArea.transform, "LongPassCard", "LONG PASS", true);

        // ── Enhancer Slot ────────────────────────────────────────────────
        float enhY = cardsY - CARD_H - GAP;
        var enhArea = MakeRect("EnhancerRow", rightCol.transform);
        enhArea.anchorMin = new Vector2(0, 1);
        enhArea.anchorMax = new Vector2(1, 1);
        enhArea.pivot = new Vector2(0, 1);
        enhArea.sizeDelta = new Vector2(0, ENH_H);
        enhArea.anchoredPosition = new Vector2(0, enhY);

        var enhBgImg = enhArea.gameObject.AddComponent<Image>();
        enhBgImg.color = new Color(0, 0, 0, 0.5f);
        // Dashed effect via outline for now (proper dashed sprite can replace later)
        var enhOutline = enhArea.gameObject.AddComponent<Outline>();
        enhOutline.effectColor = C_ENH_BORDER;
        enhOutline.effectDistance = new Vector2(2, 2);

        // Plus icon placeholder + text
        var enhTextRect = MakeRect("EnhancerHint", enhArea.transform);
        Stretch(enhTextRect);
        var enhHintTxt = AddTmpText(enhTextRect, "\u2295  PLAY ENHANCER (optional)", fontSpace, 10,
            C_ENH_TEXT, TextAlignmentOptions.Center);
        enhHintTxt.characterSpacing = 2f;

        // Enhancer card name (hidden by default, shown when card placed)
        var enhNameRect = MakeRect("EnhancerCardName", enhArea.transform);
        Stretch(enhNameRect);
        var enhNameTxt = AddTmpText(enhNameRect, "", fontSpace, 13,
            C_TEXT_WHITE, TextAlignmentOptions.Center);

        // Clear button
        var clearBtn = MakeRect("ClearButton", enhArea.transform);
        clearBtn.anchorMin = new Vector2(1, 0.5f);
        clearBtn.anchorMax = new Vector2(1, 0.5f);
        clearBtn.pivot = new Vector2(1, 0.5f);
        clearBtn.sizeDelta = new Vector2(30, 30);
        clearBtn.anchoredPosition = new Vector2(-6, 0);
        var clearImg = clearBtn.gameObject.AddComponent<Image>();
        clearImg.color = new Color(0.8f, 0.2f, 0.2f, 0.8f);
        clearBtn.gameObject.AddComponent<Button>().targetGraphic = clearImg;
        var clearTxtRect = MakeRect("X", clearBtn.transform);
        Stretch(clearTxtRect);
        AddTmpText(clearTxtRect, "X", fontInterBold, 14, C_TEXT_WHITE, TextAlignmentOptions.Center);
        clearBtn.gameObject.SetActive(false);

        var enhSlot = enhArea.gameObject.AddComponent<PlayEnhancerSlot>();
        enhSlot.displayText = enhHintTxt;

        // ── Confirm Button ───────────────────────────────────────────────
        float confirmY = enhY - ENH_H - 8;
        var confirmArea = MakeRect("ConfirmArea", rightCol.transform);
        confirmArea.anchorMin = new Vector2(0, 1);
        confirmArea.anchorMax = new Vector2(1, 1);
        confirmArea.pivot = new Vector2(0, 1);
        confirmArea.sizeDelta = new Vector2(0, CONFIRM_H);
        confirmArea.anchoredPosition = new Vector2(0, confirmY);

        var confirmBtn = MakeRect("ConfirmButton", confirmArea.transform);
        confirmBtn.anchorMin = new Vector2(0.15f, 0);
        confirmBtn.anchorMax = new Vector2(0.85f, 1);
        confirmBtn.pivot = new Vector2(0.5f, 0.5f);
        confirmBtn.offsetMin = confirmBtn.offsetMax = Vector2.zero;

        var confirmImg = confirmBtn.gameObject.AddComponent<Image>();
        confirmImg.color = C_GREEN;
        var confirmBtnComp = confirmBtn.gameObject.AddComponent<Button>();
        confirmBtnComp.targetGraphic = confirmImg;
        confirmBtnComp.interactable = false;

        // Bottom shadow effect
        var confirmShadow = confirmBtn.gameObject.AddComponent<Shadow>();
        confirmShadow.effectColor = new Color(0, 0.396f, 0.294f, 0.2f);
        confirmShadow.effectDistance = new Vector2(0, -4);

        var confirmTxtRect = MakeRect("Label", confirmBtn.transform);
        Stretch(confirmTxtRect);
        var confirmTxt = AddTmpText(confirmTxtRect, "CONFIRM SELECTION", fontSpace, 16,
            C_GREEN_DARK, TextAlignmentOptions.Center);
        confirmTxt.characterSpacing = 3f;

        // ── Footer Accent (bottom-right gradient bar) ────────────────────
        var footer = MakeRect("FooterAccent", bg.transform);
        footer.anchorMin = new Vector2(1, 0);
        footer.anchorMax = new Vector2(1, 0);
        footer.pivot = new Vector2(1, 0);
        footer.sizeDelta = new Vector2(128, 4);
        footer.anchoredPosition = Vector2.zero;
        var footerImg = footer.gameObject.AddComponent<Image>();
        footerImg.color = C_GREEN;
        // Gradient would ideally use a texture, but solid green is a close match

        // ── Wire PlayCallUIScript ────────────────────────────────────────
        var uiScript = Object.FindFirstObjectByType<PlayCallUIScript>();
        if (uiScript != null)
        {
            Undo.RecordObject(uiScript, "Wire PlayCallUIScript");
            uiScript.playCallPanel = panelObj;
            uiScript.headerText = headerTxt;
            uiScript.runButton = runBtn;
            uiScript.shortPassButton = shortBtn;
            uiScript.longPassButton = longBtn;
            uiScript.runButtonImage = runImg;
            uiScript.shortPassButtonImage = shortImg;
            uiScript.longPassButtonImage = longImg;
            uiScript.confirmButton = confirmBtnComp;
            uiScript.confirmButtonImage = confirmImg;
            uiScript.timerText = timerTxt;
            uiScript.enhancerSlot = enhSlot;
            uiScript.enhancerCardName = enhNameTxt;
            uiScript.enhancerClearButton = clearBtn.gameObject;
            uiScript.myCoachPanel = myCoachUI;
            uiScript.opponentCoachPanel = defCoachUI;
            uiScript.runYardageLabel = runYardTxt;
            uiScript.shortYardageLabel = shortYardTxt;
            uiScript.longYardageLabel = longYardTxt;
            uiScript.runDefenseText = runDefTxt;
            uiScript.shortDefenseText = shortDefTxt;
            uiScript.longDefenseText = longDefTxt;
            uiScript.scoutingDefLabels = scoutLabels;
            uiScript.scoutingDefValues = scoutValues;
            uiScript.incompleteTexts = new[] { runIncompleteTxt, shortIncompleteTxt, longIncompleteTxt };
            uiScript.runFormationPreview = runPreview.GetComponent<FormationPreviewUI>();
            uiScript.shortFormationPreview = shortPreview.GetComponent<FormationPreviewUI>();
            uiScript.longFormationPreview = longPreview.GetComponent<FormationPreviewUI>();
            EditorUtility.SetDirty(uiScript);
        }
        else
        {
            Debug.LogWarning("[Builder] PlayCallUIScript not found — wire refs manually.");
        }

        EditorUtility.SetDirty(panelObj);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Done",
            "PlayCallPanel rebuilt (850x640 Figma layout).\nSave the scene (Ctrl+S).", "OK");
    }

    // ── Play Card Factory ────────────────────────────────────────────────

    static (Button btn, Image img, TextMeshProUGUI yardTxt, TextMeshProUGUI defTxt,
            RectTransform previewRect, TextMeshProUGUI incompleteTxt)
        MakePlayCard(Transform parent, string name, string title, bool hasIncomplete)
    {
        var root = MakeRect(name, parent);
        root.gameObject.AddComponent<LayoutElement>();

        var img = root.gameObject.AddComponent<Image>();
        img.color = C_TILE_BG;

        // Border via Outline
        var outline = root.gameObject.AddComponent<Outline>();
        outline.effectColor = C_TILE_BORDER;
        outline.effectDistance = new Vector2(1, 1);

        var btn = root.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f),
            pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f),
            selectedColor = Color.white,
            disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f),
            colorMultiplier = 1f,
            fadeDuration = 0.07f,
        };

        // Yardage label (top, e.g. "+4 yds")
        var yardRect = MakeRect("YardageLabel", root);
        yardRect.anchorMin = new Vector2(0, 1);
        yardRect.anchorMax = new Vector2(1, 1);
        yardRect.pivot = new Vector2(0.5f, 1);
        yardRect.sizeDelta = new Vector2(0, 14);
        yardRect.anchoredPosition = new Vector2(0, -16);
        var yardTxt = AddTmpText(yardRect, "+0 yds", fontInterBold, 9,
            C_GREEN, TextAlignmentOptions.Center);
        yardTxt.characterSpacing = 2f;
        yardTxt.fontStyle = FontStyles.UpperCase;

        // Title (e.g. "RUN", "SHORT PASS")
        var titleRect = MakeRect("Title", root);
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.sizeDelta = new Vector2(0, 20);
        titleRect.anchoredPosition = new Vector2(0, -34);
        AddTmpText(titleRect, title, fontSpace, 20,
            C_TEXT_WHITE, TextAlignmentOptions.Center);

        // Formation preview (black box)
        var previewRect = MakeRect("FormationPreview", root);
        previewRect.anchorMin = new Vector2(0.08f, 1);
        previewRect.anchorMax = new Vector2(0.92f, 1);
        previewRect.pivot = new Vector2(0.5f, 1);
        previewRect.sizeDelta = new Vector2(0, 160);
        previewRect.anchoredPosition = new Vector2(0, -66);
        previewRect.gameObject.AddComponent<Image>().color = C_PREVIEW_BG;
        // Border on preview
        var prevOutline = previewRect.gameObject.AddComponent<Outline>();
        prevOutline.effectColor = new Color(0.282f, 0.282f, 0.278f, 0.1f);
        prevOutline.effectDistance = new Vector2(1, 1);
        previewRect.gameObject.AddComponent<FormationPreviewUI>();

        // Defense text (not used in Figma layout directly, but kept for data)
        var defTxtRect = MakeRect("DefText", root);
        defTxtRect.anchorMin = new Vector2(0, 0);
        defTxtRect.anchorMax = new Vector2(1, 0);
        defTxtRect.pivot = new Vector2(0.5f, 0);
        defTxtRect.sizeDelta = new Vector2(0, 1);
        defTxtRect.anchoredPosition = Vector2.zero;
        defTxtRect.gameObject.SetActive(false);
        var defTxt = AddTmpText(defTxtRect, "", fontInterBold, 1, Color.clear, TextAlignmentOptions.Center);

        // Incomplete section (pass cards only)
        TextMeshProUGUI incompleteTxt = null;
        if (hasIncomplete)
        {
            var incArea = MakeRect("IncompleteSection", root);
            incArea.anchorMin = new Vector2(0.06f, 0);
            incArea.anchorMax = new Vector2(0.94f, 0);
            incArea.pivot = new Vector2(0.5f, 0);
            incArea.sizeDelta = new Vector2(0, 51);
            incArea.anchoredPosition = new Vector2(0, 8);

            // "Incomplete:" label
            var incLabel = MakeRect("IncLabel", incArea.transform);
            incLabel.anchorMin = new Vector2(0, 1);
            incLabel.anchorMax = new Vector2(1, 1);
            incLabel.pivot = new Vector2(0.5f, 1);
            incLabel.sizeDelta = new Vector2(0, 12);
            incLabel.anchoredPosition = Vector2.zero;
            AddTmpText(incLabel, "Incomplete:", fontInterBold, 8,
                C_TEXT_MUTED, TextAlignmentOptions.Center);

            // Text box
            var incBox = MakeRect("IncBox", incArea.transform);
            incBox.anchorMin = new Vector2(0, 0);
            incBox.anchorMax = new Vector2(1, 1);
            incBox.pivot = new Vector2(0.5f, 0);
            incBox.offsetMin = new Vector2(0, 0);
            incBox.offsetMax = new Vector2(0, -16);
            incBox.gameObject.AddComponent<Image>().color = Color.black;
            var incBoxOutline = incBox.gameObject.AddComponent<Outline>();
            incBoxOutline.effectColor = C_TILE_BORDER;
            incBoxOutline.effectDistance = new Vector2(1, 1);

            var incTxtRect = MakeRect("IncText", incBox.transform);
            Stretch(incTxtRect);
            incompleteTxt = AddTmpText(incTxtRect, "", fontNoto ?? fontInterMed, 10,
                new Color(1f, 0.443f, 0.424f, 0.8f), TextAlignmentOptions.Center);
        }

        return (btn, img, yardTxt, defTxt, previewRect, incompleteTxt);
    }

    // ── Coach Card Factory ───────────────────────────────────────────────

    static CoachCardUI BuildCoachCard(Transform parent, string name, bool isDefense, float yOffset)
    {
        var root = MakeRect(name, parent);
        root.anchorMin = new Vector2(0, 1);
        root.anchorMax = new Vector2(1, 1);
        root.pivot = new Vector2(0, 1);
        root.sizeDelta = new Vector2(0, AVATAR_H + INFO_H + 8);
        root.anchoredPosition = new Vector2(0, yOffset);

        var coachUI = root.gameObject.AddComponent<CoachCardUI>();
        Color accentColor = isDefense ? C_RED : C_GREEN;
        Color borderColor = isDefense ? C_COACH_BORDER_RED : C_COACH_BORDER_GRN;
        Color labelBg = isDefense ? C_RED_LABEL_BG : C_GREEN_LABEL_BG;
        Color labelTextColor = isDefense ? HexColor("#490006") : C_GREEN_DARK;
        string labelText = isDefense ? "DEF COACH" : "MY COACH";

        // Avatar area (128x112)
        var avatarArea = MakeRect("AvatarArea", root.transform);
        avatarArea.anchorMin = new Vector2(0, 1);
        avatarArea.anchorMax = new Vector2(1, 1);
        avatarArea.pivot = new Vector2(0, 1);
        avatarArea.sizeDelta = new Vector2(0, AVATAR_H);
        avatarArea.anchoredPosition = Vector2.zero;

        var avatarBg = avatarArea.gameObject.AddComponent<Image>();
        avatarBg.color = C_COACH_BG;
        var avatarOutline = avatarArea.gameObject.AddComponent<Outline>();
        avatarOutline.effectColor = borderColor;
        avatarOutline.effectDistance = new Vector2(1, 1);

        // Coach image (fills avatar area, will be grayscale at runtime)
        var imgRect = MakeRect("CoachImage", avatarArea.transform);
        Stretch(imgRect);
        imgRect.offsetMin = new Vector2(1, 1);
        imgRect.offsetMax = new Vector2(-1, -1);
        var coachImg = imgRect.gameObject.AddComponent<Image>();
        coachImg.color = Color.white;
        // Apply grayscale material if available
        var grayscaleMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/TcgEngine/Materials/UI/GrayscaleUI.mat");
        if (grayscaleMat != null) coachImg.material = grayscaleMat;
        coachUI.card_image = coachImg;

        // Label overlay at bottom of avatar
        var labelOverlay = MakeRect("LabelOverlay", avatarArea.transform);
        labelOverlay.anchorMin = new Vector2(0, 0);
        labelOverlay.anchorMax = new Vector2(1, 0);
        labelOverlay.pivot = new Vector2(0.5f, 0);
        labelOverlay.sizeDelta = new Vector2(0, 16);
        labelOverlay.offsetMin = new Vector2(1, 0);
        labelOverlay.offsetMax = new Vector2(-1, 16);
        labelOverlay.gameObject.AddComponent<Image>().color = labelBg;

        var posLabel = AddTmpText(labelOverlay, labelText, fontSpace, 8,
            labelTextColor, TextAlignmentOptions.Center);
        posLabel.characterSpacing = 2f;
        coachUI.position_label = posLabel;

        // Info strip (below avatar, with colored left border)
        var infoStrip = MakeRect("InfoStrip", root.transform);
        infoStrip.anchorMin = new Vector2(0, 1);
        infoStrip.anchorMax = new Vector2(1, 1);
        infoStrip.pivot = new Vector2(0, 1);
        infoStrip.sizeDelta = new Vector2(0, INFO_H);
        infoStrip.anchoredPosition = new Vector2(0, -(AVATAR_H + 8));
        infoStrip.gameObject.AddComponent<Image>().color = C_INFO_BG;

        // Colored left border on info strip
        var infoBorder = MakeRect("InfoBorder", infoStrip.transform);
        infoBorder.anchorMin = new Vector2(0, 0);
        infoBorder.anchorMax = new Vector2(0, 1);
        infoBorder.pivot = new Vector2(0, 0.5f);
        infoBorder.sizeDelta = new Vector2(2, 0);
        infoBorder.anchoredPosition = Vector2.zero;
        infoBorder.gameObject.AddComponent<Image>().color = accentColor;

        // Coach name
        var nameRect = MakeRect("CoachName", infoStrip.transform);
        nameRect.anchorMin = new Vector2(0, 0.5f);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.pivot = new Vector2(0, 0.5f);
        nameRect.offsetMin = new Vector2(8, 0);
        nameRect.offsetMax = new Vector2(-4, -3);
        coachUI.card_title = AddTmpText(nameRect, "COACH", fontSpace, 7,
            C_TEXT_WHITE, TextAlignmentOptions.MidlineLeft);

        // Ability text
        var abilityRect = MakeRect("AbilityText", infoStrip.transform);
        abilityRect.anchorMin = new Vector2(0, 0);
        abilityRect.anchorMax = new Vector2(1, 0.5f);
        abilityRect.pivot = new Vector2(0, 0.5f);
        abilityRect.offsetMin = new Vector2(8, 2);
        abilityRect.offsetMax = new Vector2(-4, 0);
        coachUI.card_text = AddTmpText(abilityRect, "", fontInterMed ?? fontInterBold, 8,
            C_TEXT_MUTED, TextAlignmentOptions.MidlineLeft);

        return coachUI;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    static RectTransform MakeRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<RectTransform>();
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI AddTmpText(RectTransform parent, string content,
        TMP_FontAsset font, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        var go = new GameObject("Text");
        Undo.RegisterCreatedObjectUndo(go, "Create Text");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        Stretch(rt);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.font = font;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    static Color HexColor(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
        return Color.magenta;
    }
}
#endif
