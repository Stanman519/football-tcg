#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// Rebuilds the PlayCallPanel hierarchy with a compact football-themed look.
/// Menu: First&Long → Rebuild Play Call Panel
/// </summary>
public static class PlayCallPanelBuilder
{
    // ── Colors ──────────────────────────────────────────────────────────────
    static readonly Color C_BG          = new Color(0.06f, 0.10f, 0.06f, 0.94f);
    static readonly Color C_HEADER      = new Color(0.91f, 0.39f, 0.10f, 1.00f); // orange
    static readonly Color C_BTN_NORMAL  = new Color(0.13f, 0.22f, 0.34f, 1.00f); // steel blue
    static readonly Color C_BTN_HOVER   = new Color(0.20f, 0.34f, 0.52f, 1.00f);
    static readonly Color C_BTN_PRESS   = new Color(0.08f, 0.14f, 0.22f, 1.00f);
    static readonly Color C_ENHANCER_BG = new Color(0.10f, 0.16f, 0.10f, 1.00f); // darker green
    static readonly Color C_ENHANCER_BD = new Color(0.91f, 0.39f, 0.10f, 0.35f); // faint orange border
    static readonly Color C_TEXT        = Color.white;
    static readonly Color C_SUBTEXT     = new Color(0.70f, 0.82f, 0.88f, 1.00f);
    static readonly Color C_HINT        = new Color(0.55f, 0.65f, 0.55f, 1.00f); // muted green-gray

    // ── Sizes ────────────────────────────────────────────────────────────────
    const float PANEL_W      = 440f;
    const float PANEL_H      = 185f;
    const float HEADER_H     = 32f;
    const float BTNS_H       = 95f;
    const float ENHANCER_H   = 42f;
    const float PAD          = 7f;
    const float SPACING      = 5f;
    const int   TITLE_FS     = 13;
    const int   BTN_MAIN_FS  = 16;
    const int   BTN_SUB_FS   = 10;
    const int   ENH_FS       = 11;

    // ── Menu ─────────────────────────────────────────────────────────────────

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
                "Delete all children of PlayCallPanel and rebuild?", "Rebuild", "Cancel"))
            return;

        Undo.RecordObject(panelObj, "Rebuild PlayCallPanel");

        while (panelObj.transform.childCount > 0)
            Undo.DestroyObjectImmediate(panelObj.transform.GetChild(0).gameObject);

        // ── Fix the panel's own RectTransform: center-anchored, fixed size ──
        var panelRect = panelObj.GetComponent<RectTransform>();
        if (panelRect == null) panelRect = panelObj.AddComponent<RectTransform>();
        panelRect.anchorMin        = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax        = new Vector2(0.5f, 0.5f);
        panelRect.pivot            = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta        = new Vector2(PANEL_W, PANEL_H);
        panelRect.anchoredPosition = new Vector2(0f, 30f); // slightly above canvas center

        // ── Background ───────────────────────────────────────────────────────
        var bg = MakeRect("PanelBg", panelObj.transform);
        Stretch(bg);
        bg.gameObject.AddComponent<Image>().color = C_BG;

        // ── Header ───────────────────────────────────────────────────────────
        var header = MakeRect("Header", bg.transform);
        AnchorTop(header, HEADER_H);
        header.gameObject.AddComponent<Image>().color = C_HEADER;

        var titleRect = MakeRect("TitleText", header.transform);
        Stretch(titleRect);
        var titleTxt = AddText(titleRect, "CALL YOUR PLAY", C_TEXT, TITLE_FS, FontStyle.Bold,
                               TextAnchor.MiddleCenter);

        // ── Play buttons row ─────────────────────────────────────────────────
        var btnArea = MakeRect("ButtonRow", bg.transform);
        btnArea.anchorMin = new Vector2(0, 1);
        btnArea.anchorMax = new Vector2(1, 1);
        btnArea.pivot     = new Vector2(0.5f, 1);
        btnArea.sizeDelta = new Vector2(0, BTNS_H);
        btnArea.anchoredPosition = new Vector2(0, -HEADER_H);
        btnArea.offsetMin = new Vector2(PAD,  btnArea.offsetMin.y);
        btnArea.offsetMax = new Vector2(-PAD, btnArea.offsetMax.y);

        var hlg = btnArea.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = SPACING;
        hlg.childControlWidth = hlg.childControlHeight = true;
        hlg.childForceExpandWidth = hlg.childForceExpandHeight = true;

        var runBtn   = MakePlayButton(btnArea.transform, "RunButton",        "RUN",        "Ground game");
        var shortBtn = MakePlayButton(btnArea.transform, "ShortPassButton",  "SHORT PASS", "Under 10 yds");
        var longBtn  = MakePlayButton(btnArea.transform, "LongPassButton",   "LONG PASS",  "Deep route");

        // ── Enhancer slot row ────────────────────────────────────────────────
        var enhArea = MakeRect("EnhancerRow", bg.transform);
        enhArea.anchorMin = new Vector2(0, 0);
        enhArea.anchorMax = new Vector2(1, 0);
        enhArea.pivot     = new Vector2(0.5f, 0);
        enhArea.sizeDelta = new Vector2(0, ENHANCER_H);
        enhArea.anchoredPosition = new Vector2(0, PAD);
        enhArea.offsetMin = new Vector2(PAD,  enhArea.offsetMin.y);
        enhArea.offsetMax = new Vector2(-PAD, enhArea.offsetMax.y);

        var enhBg = enhArea.gameObject.AddComponent<Image>();
        enhBg.color = C_ENHANCER_BG;

        // The slot needs an Outline to suggest it's a drop target
        var outline = enhArea.gameObject.AddComponent<Outline>();
        outline.effectColor    = C_ENHANCER_BD;
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        // Hint text (shows card name when occupied, prompt when empty)
        var enhTextRect = MakeRect("EnhancerText", enhArea.transform);
        Stretch(enhTextRect);
        enhTextRect.offsetMin = new Vector2(8, 0);
        enhTextRect.offsetMax = new Vector2(-8, 0);
        var enhText = AddText(enhTextRect, "⊕  Drop play enhancer card here",
                              C_HINT, ENH_FS, FontStyle.Normal, TextAnchor.MiddleCenter);

        // PlayEnhancerSlot component on the drop area
        var enhSlot = enhArea.gameObject.AddComponent<PlayEnhancerSlot>();
        enhSlot.displayText = enhText;

        // ── Wire PlayCallUIScript references ──────────────────────────────────
        var uiScript = Object.FindFirstObjectByType<PlayCallUIScript>();

        if (uiScript != null)
        {
            Undo.RecordObject(uiScript, "Wire PlayCallUIScript");
            uiScript.runButton          = runBtn;
            uiScript.shortPassButton    = shortBtn;
            uiScript.longPassButton     = longBtn;
            uiScript.playCallPanel      = panelObj;
            uiScript.enhancerDisplayText = enhText;
            EditorUtility.SetDirty(uiScript);
        }
        else
        {
            Debug.LogWarning("[Builder] PlayCallUIScript not found — wire button refs manually.");
        }

        EditorUtility.SetDirty(panelObj);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Done",
            "PlayCallPanel rebuilt.\nSave the scene (Ctrl+S) to keep changes.", "OK");
    }

    // ── Button factory ────────────────────────────────────────────────────────

    static Button MakePlayButton(Transform parent, string name, string main, string sub)
    {
        var root = MakeRect(name, parent);
        var img  = root.gameObject.AddComponent<Image>();
        img.color = C_BTN_NORMAL;

        root.gameObject.AddComponent<LayoutElement>();

        var btn = root.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.colors = new ColorBlock
        {
            normalColor      = C_BTN_NORMAL,
            highlightedColor = C_BTN_HOVER,
            pressedColor     = C_BTN_PRESS,
            selectedColor    = C_BTN_NORMAL,
            disabledColor    = new Color(0.3f, 0.3f, 0.3f, 0.5f),
            colorMultiplier  = 1f,
            fadeDuration     = 0.07f,
        };

        var mainRect = MakeRect("Label", root);
        mainRect.anchorMin = new Vector2(0, 0.42f);
        mainRect.anchorMax = new Vector2(1, 1);
        mainRect.offsetMin = mainRect.offsetMax = Vector2.zero;
        AddText(mainRect, main, C_TEXT, BTN_MAIN_FS, FontStyle.Bold, TextAnchor.LowerCenter);

        var subRect = MakeRect("SubLabel", root);
        subRect.anchorMin = new Vector2(0, 0);
        subRect.anchorMax = new Vector2(1, 0.41f);
        subRect.offsetMin = subRect.offsetMax = Vector2.zero;
        AddText(subRect, sub, C_SUBTEXT, BTN_SUB_FS, FontStyle.Normal, TextAnchor.UpperCenter);

        return btn;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

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

    static void AnchorTop(RectTransform rt, float height)
    {
        rt.anchorMin        = new Vector2(0, 1);
        rt.anchorMax        = new Vector2(1, 1);
        rt.pivot            = new Vector2(0.5f, 1);
        rt.sizeDelta        = new Vector2(0, height);
        rt.anchoredPosition = Vector2.zero;
    }

    static Text AddText(RectTransform parent, string content, Color color,
                        int fontSize, FontStyle style, TextAnchor anchor)
    {
        var t = parent.gameObject.AddComponent<Text>();
        t.text      = content;
        t.color     = color;
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize  = fontSize;
        t.fontStyle = style;
        t.alignment = anchor;
        return t;
    }
}
#endif
