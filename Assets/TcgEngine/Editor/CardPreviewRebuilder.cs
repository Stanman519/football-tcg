using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using TcgEngine.UI;

/// <summary>
/// Rebuilds Assets/TcgEngine/Prefabs/UI/CardUI.prefab to match the new HandCard layout.
/// Run via First&Long > Rebuild Card Preview Prefab
/// </summary>
public class CardPreviewRebuilder
{
    static readonly Color COL_MINT = new Color32(0xAA, 0xFF, 0xDC, 0xFF);
    static readonly Color COL_GRAY = new Color32(0xAD, 0xAA, 0xAA, 0xFF);
    static readonly Color COL_DARK = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
    static readonly Color COL_ABILITY_BG = new Color32(0x13, 0x13, 0x13, 0xFF);
    static readonly Color COL_STAT_STRIP = new Color(0.17f, 0.17f, 0.17f, 0.8f);
    static readonly Color COL_BADGE_BG = new Color32(0x26, 0x26, 0x26, 0xFF);
    static readonly Color COL_DIVIDER = new Color32(0x48, 0x48, 0x47, 0x4D);
    static readonly Color COL_CYAN = new Color32(0x81, 0xEC, 0xFF, 0xFF);
    static readonly Color COL_RED = new Color32(0xFF, 0x71, 0x6C, 0xFF);

    [MenuItem("First&Long/Rebuild Card Preview Prefab")]
    public static void Rebuild()
    {
        string prefabPath = "Assets/TcgEngine/Prefabs/UI/CardUI.prefab";
        var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefabAsset == null) { Debug.LogError("CardUI.prefab not found at " + prefabPath); return; }

        // Load fonts from the HandCard prefab's font GUIDs
        var fontBold = LoadByGUID<TMP_FontAsset>("b19d31dd45afe8d4c9e66a1d28a14487");
        var fontBody = LoadByGUID<TMP_FontAsset>("9ad9fcd47120cae43997f66a02dc31f8");
        if (fontBold == null) { Debug.LogError("Bold font asset not found"); return; }
        if (fontBody == null) fontBody = fontBold; // fallback

        var root = PrefabUtility.LoadPrefabContents(prefabPath);

        // Clear all children
        for (int i = root.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

        // Remove all components except RectTransform (reverse order to handle dependencies)
        var comps = root.GetComponents<Component>();
        for (int ci = comps.Length - 1; ci >= 0; ci--)
        {
            if (comps[ci] is RectTransform) continue;
            Object.DestroyImmediate(comps[ci]);
        }

        // Root setup — 480x680 preview card
        var rt = root.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(480, 680);

        root.AddComponent<CanvasRenderer>();
        var rootImg = root.AddComponent<Image>();
        rootImg.color = COL_DARK;
        rootImg.raycastTarget = false;

        // ============ PlayerSection (fills card) ============
        var ps = MakeChild(root, "PlayerSection");
        Stretch(ps);

        // --- Card Art Area (top ~53%) ---
        var artArea = MakeChild(ps, "CardArtArea");
        SetAnchors(artArea, 0, 0.47f, 1, 1);
        artArea.AddComponent<Mask>().showMaskGraphic = false;
        artArea.AddComponent<Image>().color = COL_DARK;

        var cardImg = MakeChild(artArea, "CardImage");
        Stretch(cardImg);
        var ciImg = cardImg.AddComponent<Image>();
        ciImg.preserveAspect = false;
        ciImg.raycastTarget = false;

        var gradOverlay = MakeChild(artArea, "GradientOverlay");
        Stretch(gradOverlay);
        gradOverlay.AddComponent<Image>().color = new Color(0, 0, 0, 0.3f);

        var playerName = MakeChild(artArea, "PlayerName");
        var pnRT = playerName.GetComponent<RectTransform>();
        pnRT.anchorMin = Vector2.zero; pnRT.anchorMax = new Vector2(1, 0);
        pnRT.pivot = Vector2.zero;
        pnRT.anchoredPosition = new Vector2(24, 20);
        pnRT.sizeDelta = new Vector2(-48, 50);
        var nameTMP = AddTMP(playerName, fontBold, 32, COL_MINT, TextAlignmentOptions.BottomLeft);
        nameTMP.enableWordWrapping = false;
        nameTMP.overflowMode = TextOverflowModes.Ellipsis;

        // --- Position Badge (top-left) ---
        var badge = MakeChild(ps, "PositionBadge");
        var bRT = badge.GetComponent<RectTransform>();
        bRT.anchorMin = new Vector2(0, 1); bRT.anchorMax = new Vector2(0, 1);
        bRT.pivot = new Vector2(0, 1);
        bRT.anchoredPosition = new Vector2(-6, 0);
        bRT.sizeDelta = new Vector2(80, 86);
        var pbImg = badge.AddComponent<Image>();
        pbImg.color = new Color32(0x8B, 0x5C, 0xF6, 0xFF);
        pbImg.raycastTarget = false;

        var posText = MakeChild(badge, "PositionText");
        SetAnchors(posText, 0.1f, 0.5f, 0.9f, 0.95f);
        var posTMP = AddTMP(posText, fontBold, 24, Color.white, TextAlignmentOptions.Center);

        var suitIcon = MakeChild(badge, "SuitIcon");
        SetAnchors(suitIcon, 0.2f, 0.08f, 0.8f, 0.48f);
        var siImg = suitIcon.AddComponent<Image>();
        siImg.preserveAspect = true; siImg.raycastTarget = false;

        // --- Stat Strip (below art) ---
        var strip = MakeChild(ps, "StatStrip");
        var sRT = strip.GetComponent<RectTransform>();
        sRT.anchorMin = Vector2.zero; sRT.anchorMax = new Vector2(1, 0);
        sRT.pivot = new Vector2(0.5f, 1);
        sRT.anchoredPosition = new Vector2(0, 320); // at art bottom edge
        sRT.sizeDelta = new Vector2(-32, 72);
        strip.AddComponent<CanvasRenderer>();
        strip.AddComponent<Image>().color = COL_STAT_STRIP;
        var ssHLG = strip.AddComponent<HorizontalLayoutGroup>();
        ssHLG.padding = new RectOffset(12, 12, 6, 6);
        ssHLG.childAlignment = TextAnchor.MiddleCenter;
        ssHLG.childControlWidth = true; ssHLG.childControlHeight = true;
        ssHLG.childForceExpandWidth = true; ssHLG.childForceExpandHeight = true;

        TMP_Text[] statLabels = new TMP_Text[3];
        TMP_Text[] statValues = new TMP_Text[3];
        string[] labelNames = { "RUN", "SHORT", "DEEP" };
        for (int i = 0; i < 3; i++)
        {
            var col = MakeChild(strip, "StatCol" + (i + 1));
            var vlg = col.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = -2; vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            var lbl = MakeChild(col, "Label");
            statLabels[i] = AddTMP(lbl, fontBold, 11, COL_GRAY, TextAlignmentOptions.Center);
            statLabels[i].text = labelNames[i];
            lbl.AddComponent<LayoutElement>().preferredHeight = 18;

            var val = MakeChild(col, "Value");
            statValues[i] = AddTMP(val, fontBold, 24, COL_MINT, TextAlignmentOptions.Center);
            statValues[i].text = "0";
            val.AddComponent<LayoutElement>().preferredHeight = 34;

            if (i < 2)
            {
                var div = MakeChild(strip, "Divider" + i);
                div.AddComponent<CanvasRenderer>();
                div.AddComponent<Image>().color = COL_DIVIDER;
                var dle = div.AddComponent<LayoutElement>();
                dle.preferredWidth = 1; dle.flexibleWidth = 0;
            }
        }

        // --- Badges (bottom of card, below ability panel) ---
        var badges = MakeChild(ps, "Badges");
        var bgRT = badges.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = new Vector2(1, 0);
        bgRT.pivot = new Vector2(0, 0);
        bgRT.anchoredPosition = new Vector2(24, 6);
        bgRT.sizeDelta = new Vector2(-48, 36);
        var bgHLG = badges.AddComponent<HorizontalLayoutGroup>();
        bgHLG.spacing = 12; bgHLG.childAlignment = TextAnchor.MiddleLeft;
        bgHLG.childControlWidth = true; bgHLG.childControlHeight = true;
        bgHLG.childForceExpandWidth = false; bgHLG.childForceExpandHeight = true;

        TMP_Text stamTMP; Image stamIcon;
        BuildBadge(badges, "StaminaBadge", COL_CYAN, fontBold, 140, 30, out stamTMP, out stamIcon);
        TMP_Text gritTMP; Image gritIcon;
        BuildBadge(badges, "GritBadge", COL_RED, fontBold, 110, 30, out gritTMP, out gritIcon);

        // ============ Ability Panel (above badges) ============
        var ap = MakeChild(root, "AbilityPanel");
        var apRT = ap.GetComponent<RectTransform>();
        apRT.anchorMin = Vector2.zero; apRT.anchorMax = new Vector2(1, 0);
        apRT.pivot = new Vector2(0.5f, 0);
        apRT.anchoredPosition = new Vector2(0, 48);
        apRT.sizeDelta = new Vector2(-8, 145);
        ap.AddComponent<CanvasRenderer>();
        ap.AddComponent<Image>().color = COL_ABILITY_BG;

        // Ability Header
        var ah = MakeChild(ap, "AbilityHeader");
        var ahRT = ah.GetComponent<RectTransform>();
        ahRT.anchorMin = new Vector2(0, 1); ahRT.anchorMax = new Vector2(1, 1);
        ahRT.pivot = new Vector2(0.5f, 1);
        ahRT.anchoredPosition = new Vector2(0, -14);
        ahRT.sizeDelta = new Vector2(-32, 24);

        var abilName = MakeChild(ah, "AbilityName");
        Stretch(abilName);
        var anTMP = AddTMP(abilName, fontBold, 14, Color.white, TextAlignmentOptions.Left);

        var slotReqs = MakeChild(ah, "SlotRequirements");
        var srRT = slotReqs.GetComponent<RectTransform>();
        srRT.anchorMin = new Vector2(1, 0); srRT.anchorMax = new Vector2(1, 1);
        srRT.pivot = new Vector2(1, 0.5f);
        srRT.sizeDelta = new Vector2(50, 0);
        var srHLG = slotReqs.AddComponent<HorizontalLayoutGroup>();
        srHLG.spacing = 4; srHLG.childAlignment = TextAnchor.MiddleRight;
        srHLG.childControlWidth = false; srHLG.childControlHeight = false;

        // Ability Text
        var abilText = MakeChild(ap, "AbilityText");
        var atRT = abilText.GetComponent<RectTransform>();
        atRT.anchorMin = Vector2.zero; atRT.anchorMax = Vector2.one;
        atRT.anchoredPosition = new Vector2(0, -16);
        atRT.sizeDelta = new Vector2(-32, -56);
        var atTMP = AddTMP(abilText, fontBody, 12, COL_GRAY, TextAlignmentOptions.TopLeft);
        atTMP.enableWordWrapping = true;

        // ============ Wire CardUI ============
        var cardUI = root.AddComponent<CardUI>();
        cardUI.card_image = ciImg;
        cardUI.card_title = nameTMP;
        cardUI.card_text = atTMP;
        cardUI.stamina = stamTMP;
        cardUI.stamina_icon = stamIcon;
        cardUI.grit = gritTMP;
        cardUI.grit_icon = gritIcon;
        cardUI.position = posTMP;
        cardUI.run_bonus_text = statValues[0];
        cardUI.short_pass_bonus_text = statValues[1];
        cardUI.long_pass_bonus_text = statValues[2];
        cardUI.suit_icon = siImg;
        cardUI.position_badge_bg = pbImg;
        cardUI.ability_name_text = anTMP;
        cardUI.stat_label_1 = statLabels[0];
        cardUI.stat_label_2 = statLabels[1];
        cardUI.stat_label_3 = statLabels[2];
        cardUI.slot_requirement_container = slotReqs.transform;
        cardUI.player_section = ps;
        cardUI.stats = new TraitUI[0];

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        Debug.Log("[CardPreviewRebuilder] CardUI.prefab rebuilt successfully.");
    }

    static void BuildBadge(GameObject parent, string name, Color borderColor,
        TMP_FontAsset font, float prefW, float prefH,
        out TMP_Text outText, out Image outIcon)
    {
        var badge = MakeChild(parent, name);
        badge.AddComponent<CanvasRenderer>();
        var bImg = badge.AddComponent<Image>();
        bImg.color = COL_BADGE_BG; bImg.raycastTarget = false;
        var bLE = badge.AddComponent<LayoutElement>();
        bLE.preferredWidth = prefW; bLE.preferredHeight = prefH;
        var bHLG = badge.AddComponent<HorizontalLayoutGroup>();
        bHLG.padding = new RectOffset(8, 8, 4, 4);
        bHLG.spacing = 4; bHLG.childAlignment = TextAnchor.MiddleLeft;
        bHLG.childControlWidth = true; bHLG.childControlHeight = true;
        bHLG.childForceExpandWidth = false; bHLG.childForceExpandHeight = true;

        var ico = MakeChild(badge, "Icon");
        ico.AddComponent<CanvasRenderer>();
        outIcon = ico.AddComponent<Image>();
        outIcon.preserveAspect = true; outIcon.raycastTarget = false;
        var iLE = ico.AddComponent<LayoutElement>();
        iLE.preferredWidth = 16; iLE.preferredHeight = 16;

        var txt = MakeChild(badge, "Text");
        outText = AddTMP(txt, font, 13, Color.white, TextAlignmentOptions.Left);
        var tLE = txt.AddComponent<LayoutElement>();
        tLE.preferredHeight = 20; tLE.flexibleWidth = 1;
    }

    // --- Helpers ---
    static GameObject MakeChild(GameObject parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static void Stretch(GameObject go)
    {
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    static void SetAnchors(GameObject go, float xMin, float yMin, float xMax, float yMax)
    {
        var r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(xMin, yMin);
        r.anchorMax = new Vector2(xMax, yMax);
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
    }

    static TMP_Text AddTMP(GameObject go, TMP_FontAsset font, float size, Color color,
        TextAlignmentOptions align)
    {
        go.AddComponent<CanvasRenderer>();
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    static T LoadByGUID<T>(string guid) where T : Object
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
    }
}
