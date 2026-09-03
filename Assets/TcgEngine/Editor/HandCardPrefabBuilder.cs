using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using TcgEngine.UI;
using TcgEngine.Client;
using System.IO;

public class HandCardPrefabBuilder
{
    private static readonly string spritePath = "Sprites/CardFrame/";
    private static readonly string fontPath = "Assets/TcgEngine/Fonts/";

    // Design colors
    private static readonly Color colMint = new Color32(0xAA, 0xFF, 0xDC, 0xFF);
    private static readonly Color colTextSecondary = new Color32(0xAD, 0xAA, 0xAA, 0xFF);
    private static readonly Color colDarkBg = new Color32(0x1A, 0x1A, 0x1A, 0xFF);
    private static readonly Color colAbilityBg = new Color32(0x13, 0x13, 0x13, 0xFF);
    private static readonly Color colBadgeBg = new Color32(0x26, 0x26, 0x26, 0xFF);
    private static readonly Color colSlashBar = new Color32(0x48, 0x48, 0x47, 0x66); // 40% opacity

    [MenuItem("First&Long/Build Slot Icon Prefab")]
    public static void BuildSlotIconPrefab()
    {
        var go = new GameObject("SlotRequirementIcon", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(14, 14);
        var img = go.AddComponent<Image>();
        img.preserveAspect = true;
        img.raycastTarget = false;
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = 14;
        le.preferredHeight = 14;

        string path = "Assets/TcgEngine/Prefabs/UI/SlotRequirementIcon.prefab";
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log("[HandCardBuilder] Slot icon prefab saved to " + path);
    }

    [MenuItem("First&Long/Build New HandCard Prefab")]
    public static void Build()
    {
        // Load fonts
        var fontBold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath + "SpaceGrotesk-Bold SDF.asset");
        var fontBody = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath + "Inter-Medium SDF.asset");
        if (fontBold == null) { Debug.LogError("[HandCardBuilder] SpaceGrotesk-Bold SDF not found"); return; }
        if (fontBody == null) { Debug.LogError("[HandCardBuilder] Inter-Medium SDF not found"); return; }

        // Root: HandCard 252x360
        var root = new GameObject("HandCard", typeof(RectTransform), typeof(CanvasGroup));
        var rootRT = root.GetComponent<RectTransform>();
        rootRT.sizeDelta = new Vector2(252, 360);
        rootRT.pivot = new Vector2(0.5f, 0.5f);

        // Card background image
        var rootImg = root.AddComponent<Image>();
        rootImg.sprite = LoadSprite("card_bg");
        rootImg.type = Image.Type.Sliced;
        rootImg.color = colDarkBg;

        // === GLOW ===
        var glow = CreateChild(root, "Glow");
        StretchFill(glow);
        var glowRT = glow.GetComponent<RectTransform>();
        glowRT.offsetMin = new Vector2(-6, -6);
        glowRT.offsetMax = new Vector2(6, 6);
        var glowImg = glow.AddComponent<Image>();
        glowImg.sprite = LoadSprite("card_bg");
        glowImg.type = Image.Type.Sliced;
        glowImg.color = new Color(0, 1, 1, 0.4f);
        glowImg.enabled = false;
        glowImg.raycastTarget = false;

        // === PLAYER SECTION ===
        var playerSection = CreateChild(root, "PlayerSection");
        StretchFill(playerSection);

        // -- Card Art Area (top 53% = ~191px of 360)
        var artArea = CreateChild(playerSection, "CardArtArea");
        var artAreaRT = artArea.GetComponent<RectTransform>();
        artAreaRT.anchorMin = new Vector2(0, 0.47f);
        artAreaRT.anchorMax = new Vector2(1, 1);
        artAreaRT.offsetMin = Vector2.zero;
        artAreaRT.offsetMax = Vector2.zero;
        artArea.AddComponent<Mask>().showMaskGraphic = false;
        var artAreaImg = artArea.AddComponent<Image>();
        artAreaImg.color = colAbilityBg;
        artAreaImg.raycastTarget = false;

        // Card Image (the player art)
        var cardImage = CreateChild(artArea, "CardImage");
        StretchFill(cardImage);
        var cardImageImg = cardImage.AddComponent<Image>();
        cardImageImg.preserveAspect = true;
        cardImageImg.raycastTarget = false;

        // Gradient overlay
        var gradOverlay = CreateChild(artArea, "GradientOverlay");
        var gradRT = gradOverlay.GetComponent<RectTransform>();
        gradRT.anchorMin = new Vector2(0, 0);
        gradRT.anchorMax = new Vector2(1, 0.6f);
        gradRT.offsetMin = Vector2.zero;
        gradRT.offsetMax = Vector2.zero;
        var gradImg = gradOverlay.AddComponent<Image>();
        gradImg.sprite = LoadSprite("gradient_overlay");
        gradImg.type = Image.Type.Sliced;
        gradImg.raycastTarget = false;

        // Player Name
        var playerName = CreateChild(artArea, "PlayerName");
        var nameRT = playerName.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0, 0);
        nameRT.anchorMax = new Vector2(1, 0);
        nameRT.pivot = new Vector2(0, 0);
        nameRT.anchoredPosition = new Vector2(16, 16);
        nameRT.sizeDelta = new Vector2(-32, 28);
        var nameTMP = playerName.AddComponent<TextMeshProUGUI>();
        nameTMP.font = fontBold;
        nameTMP.fontSize = 18;
        nameTMP.fontStyle = FontStyles.UpperCase;
        nameTMP.color = colMint;
        nameTMP.text = "PLAYER NAME";
        nameTMP.alignment = TextAlignmentOptions.BottomLeft;
        nameTMP.enableWordWrapping = false;
        nameTMP.overflowMode = TextOverflowModes.Ellipsis;
        nameTMP.raycastTarget = false;

        // Accent line under name
        var accentLine = CreateChild(artArea, "AccentLine");
        var alRT = accentLine.GetComponent<RectTransform>();
        alRT.anchorMin = new Vector2(0, 0);
        alRT.anchorMax = new Vector2(0, 0);
        alRT.pivot = new Vector2(0, 0);
        alRT.anchoredPosition = new Vector2(16, 12);
        alRT.sizeDelta = new Vector2(32, 2);
        var alImg = accentLine.AddComponent<Image>();
        alImg.color = colMint;
        alImg.raycastTarget = false;

        // Decorative slashes (top right)
        var slashes = CreateChild(artArea, "DecorativeSlashes");
        var slashRT = slashes.GetComponent<RectTransform>();
        slashRT.anchorMin = new Vector2(1, 1);
        slashRT.anchorMax = new Vector2(1, 1);
        slashRT.pivot = new Vector2(1, 1);
        slashRT.anchoredPosition = new Vector2(-10, -6);
        slashRT.sizeDelta = new Vector2(24, 16);
        var slashHLG = slashes.AddComponent<HorizontalLayoutGroup>();
        slashHLG.spacing = 2;
        slashHLG.childAlignment = TextAnchor.MiddleCenter;
        slashHLG.childForceExpandWidth = false;
        slashHLG.childForceExpandHeight = false;
        for (int i = 0; i < 3; i++)
        {
            var bar = CreateChild(slashes, "Slash" + i);
            var barRT = bar.GetComponent<RectTransform>();
            barRT.sizeDelta = new Vector2(4, 12);
            barRT.localRotation = Quaternion.Euler(0, 0, 12);
            var barImg = bar.AddComponent<Image>();
            barImg.sprite = LoadSprite("slash_bar");
            barImg.color = colSlashBar;
            barImg.raycastTarget = false;
            bar.AddComponent<LayoutElement>().preferredWidth = 4;
            bar.GetComponent<LayoutElement>().preferredHeight = 12;
        }

        // -- Position Badge (top left, skewed)
        var posBadge = CreateChild(playerSection, "PositionBadge");
        var posBadgeRT = posBadge.GetComponent<RectTransform>();
        posBadgeRT.anchorMin = new Vector2(0, 1);
        posBadgeRT.anchorMax = new Vector2(0, 1);
        posBadgeRT.pivot = new Vector2(0, 1);
        posBadgeRT.anchoredPosition = new Vector2(-4, 0);
        posBadgeRT.sizeDelta = new Vector2(56, 60);
        var posBadgeImg = posBadge.AddComponent<Image>();
        posBadgeImg.sprite = LoadSprite("position_badge_bg");
        posBadgeImg.color = new Color32(0x8B, 0x5C, 0xF6, 0xFF); // default purple, overridden by suit
        posBadgeImg.raycastTarget = false;

        // Position text
        var posText = CreateChild(posBadge, "PositionText");
        var posTextRT = posText.GetComponent<RectTransform>();
        posTextRT.anchorMin = new Vector2(0.15f, 0.55f);
        posTextRT.anchorMax = new Vector2(0.85f, 0.95f);
        posTextRT.offsetMin = Vector2.zero;
        posTextRT.offsetMax = Vector2.zero;
        var posTMP = posText.AddComponent<TextMeshProUGUI>();
        posTMP.font = fontBold;
        posTMP.fontSize = 14;
        posTMP.color = Color.white;
        posTMP.text = "WR";
        posTMP.alignment = TextAlignmentOptions.Center;
        posTMP.enableWordWrapping = false;
        posTMP.raycastTarget = false;

        // Suit icon
        var suitIcon = CreateChild(posBadge, "SuitIcon");
        var suitIconRT = suitIcon.GetComponent<RectTransform>();
        suitIconRT.anchorMin = new Vector2(0.25f, 0.1f);
        suitIconRT.anchorMax = new Vector2(0.75f, 0.5f);
        suitIconRT.offsetMin = Vector2.zero;
        suitIconRT.offsetMax = Vector2.zero;
        var suitIconImg = suitIcon.AddComponent<Image>();
        suitIconImg.sprite = LoadSprite("suit_spades");
        suitIconImg.preserveAspect = true;
        suitIconImg.color = Color.white;
        suitIconImg.raycastTarget = false;

        // -- Stat Strip (middle section, below art area)
        var statStrip = CreateChild(playerSection, "StatStrip");
        var statStripRT = statStrip.GetComponent<RectTransform>();
        statStripRT.anchorMin = new Vector2(0, 0);
        statStripRT.anchorMax = new Vector2(1, 0);
        statStripRT.pivot = new Vector2(0.5f, 1);
        statStripRT.anchoredPosition = new Vector2(0, 169); // positioned just below art area (360 - 191 = 169 from bottom)
        statStripRT.sizeDelta = new Vector2(-24, 46);
        var statStripImg = statStrip.AddComponent<Image>();
        statStripImg.sprite = LoadSprite("stat_strip_bg");
        statStripImg.color = Color.white;
        statStripImg.raycastTarget = false;
        var statHLG = statStrip.AddComponent<HorizontalLayoutGroup>();
        statHLG.spacing = 0;
        statHLG.padding = new RectOffset(8, 8, 4, 4);
        statHLG.childAlignment = TextAnchor.MiddleCenter;
        statHLG.childForceExpandWidth = true;
        statHLG.childForceExpandHeight = true;

        // Create 3 stat columns
        string[] statLabels = { "RUN", "SHORT", "DEEP" };
        string[] statValues = { "0", "0", "0" };
        var labelObjs = new TextMeshProUGUI[3];
        var valueObjs = new TextMeshProUGUI[3];

        for (int i = 0; i < 3; i++)
        {
            var col = CreateChild(statStrip, "StatCol" + (i + 1));
            var colVLG = col.AddComponent<VerticalLayoutGroup>();
            colVLG.childAlignment = TextAnchor.MiddleCenter;
            colVLG.childForceExpandWidth = true;
            colVLG.childForceExpandHeight = false;
            colVLG.spacing = -2;

            // Label
            var label = CreateChild(col, "Label");
            var labelTMP = label.AddComponent<TextMeshProUGUI>();
            labelTMP.font = fontBold;
            labelTMP.fontSize = 7;
            labelTMP.fontStyle = FontStyles.UpperCase;
            labelTMP.color = colTextSecondary;
            labelTMP.text = statLabels[i];
            labelTMP.alignment = TextAlignmentOptions.Center;
            labelTMP.characterSpacing = 5;
            labelTMP.raycastTarget = false;
            var labelLE = label.AddComponent<LayoutElement>();
            labelLE.preferredHeight = 12;
            labelObjs[i] = labelTMP;

            // Value
            var val = CreateChild(col, "Value");
            var valTMP = val.AddComponent<TextMeshProUGUI>();
            valTMP.font = fontBold;
            valTMP.fontSize = 14;
            valTMP.color = colMint;
            valTMP.text = statValues[i];
            valTMP.alignment = TextAlignmentOptions.Center;
            valTMP.raycastTarget = false;
            var valLE = val.AddComponent<LayoutElement>();
            valLE.preferredHeight = 20;
            valueObjs[i] = valTMP;

            // Add divider after first two columns
            if (i < 2)
            {
                var div = CreateChild(statStrip, "Divider" + i);
                var divImg = div.AddComponent<Image>();
                divImg.color = new Color32(0x48, 0x48, 0x47, 0x4D);
                divImg.raycastTarget = false;
                var divLE = div.AddComponent<LayoutElement>();
                divLE.preferredWidth = 1;
                divLE.flexibleWidth = 0;
            }
        }

        // -- Grit & Stamina Badges
        var badges = CreateChild(playerSection, "Badges");
        var badgesRT = badges.GetComponent<RectTransform>();
        badgesRT.anchorMin = new Vector2(0, 0);
        badgesRT.anchorMax = new Vector2(1, 0);
        badgesRT.pivot = new Vector2(0, 1);
        badgesRT.anchoredPosition = new Vector2(16, 119);
        badgesRT.sizeDelta = new Vector2(-32, 18);
        var badgesHLG = badges.AddComponent<HorizontalLayoutGroup>();
        badgesHLG.spacing = 10;
        badgesHLG.childAlignment = TextAnchor.MiddleLeft;
        badgesHLG.childForceExpandWidth = false;
        badgesHLG.childForceExpandHeight = true;

        // Stamina badge
        var staminaBadge = CreateStaminaGritBadge(badges, "StaminaBadge", "stamina_pill_bg",
            "stat_lightning", "STAMINA", "88", new Color32(0x81, 0xEC, 0xFF, 0xFF), fontBold);

        // Grit badge
        var gritBadge = CreateStaminaGritBadge(badges, "GritBadge", "grit_pill_bg",
            "stat_shield", "GRIT", "74", new Color32(0xFF, 0x71, 0x6C, 0xFF), fontBold);

        // === ABILITY PANEL (bottom section) ===
        var abilityPanel = CreateChild(root, "AbilityPanel");
        var apRT = abilityPanel.GetComponent<RectTransform>();
        apRT.anchorMin = new Vector2(0, 0);
        apRT.anchorMax = new Vector2(1, 0);
        apRT.pivot = new Vector2(0.5f, 0);
        apRT.anchoredPosition = new Vector2(0, 2);
        apRT.sizeDelta = new Vector2(-4, 96);
        var apImg = abilityPanel.AddComponent<Image>();
        apImg.sprite = LoadSprite("ability_panel_bg");
        apImg.type = Image.Type.Sliced;
        apImg.color = Color.white;
        apImg.raycastTarget = false;

        // Ability header row (name + slot requirements)
        var abilityHeader = CreateChild(abilityPanel, "AbilityHeader");
        var ahRT = abilityHeader.GetComponent<RectTransform>();
        ahRT.anchorMin = new Vector2(0, 1);
        ahRT.anchorMax = new Vector2(1, 1);
        ahRT.pivot = new Vector2(0.5f, 1);
        ahRT.anchoredPosition = new Vector2(0, -10);
        ahRT.sizeDelta = new Vector2(-24, 16);

        // Ability name
        var abilityName = CreateChild(abilityHeader, "AbilityName");
        StretchFill(abilityName);
        var abilityNameTMP = abilityName.AddComponent<TextMeshProUGUI>();
        abilityNameTMP.font = fontBold;
        abilityNameTMP.fontSize = 9;
        abilityNameTMP.fontStyle = FontStyles.UpperCase;
        abilityNameTMP.color = Color.white;
        abilityNameTMP.text = "ABILITY NAME";
        abilityNameTMP.alignment = TextAlignmentOptions.Left;
        abilityNameTMP.enableWordWrapping = false;
        abilityNameTMP.raycastTarget = false;

        // Slot requirements container (right side of header)
        var slotReqContainer = CreateChild(abilityHeader, "SlotRequirements");
        var srRT = slotReqContainer.GetComponent<RectTransform>();
        srRT.anchorMin = new Vector2(1, 0);
        srRT.anchorMax = new Vector2(1, 1);
        srRT.pivot = new Vector2(1, 0.5f);
        srRT.anchoredPosition = Vector2.zero;
        srRT.sizeDelta = new Vector2(40, 0);
        var srHLG = slotReqContainer.AddComponent<HorizontalLayoutGroup>();
        srHLG.spacing = 3;
        srHLG.childAlignment = TextAnchor.MiddleRight;
        srHLG.childForceExpandWidth = false;
        srHLG.childForceExpandHeight = false;

        // Ability text (description)
        var abilityText = CreateChild(abilityPanel, "AbilityText");
        var atRT = abilityText.GetComponent<RectTransform>();
        atRT.anchorMin = new Vector2(0, 0);
        atRT.anchorMax = new Vector2(1, 1);
        atRT.offsetMin = new Vector2(12, 8);
        atRT.offsetMax = new Vector2(-12, -30);
        var abilityTextTMP = abilityText.AddComponent<TextMeshProUGUI>();
        abilityTextTMP.font = fontBody;
        abilityTextTMP.fontSize = 7.5f;
        abilityTextTMP.color = colTextSecondary;
        abilityTextTMP.text = "Ability description text.";
        abilityTextTMP.alignment = TextAlignmentOptions.TopLeft;
        abilityTextTMP.enableWordWrapping = true;
        abilityTextTMP.richText = true;
        abilityTextTMP.raycastTarget = false;

        // === ACCENT CORNER (bottom right) ===
        var accentCorner = CreateChild(root, "AccentCorner");
        var acRT = accentCorner.GetComponent<RectTransform>();
        acRT.anchorMin = new Vector2(1, 0);
        acRT.anchorMax = new Vector2(1, 0);
        acRT.pivot = new Vector2(1, 0);
        acRT.anchoredPosition = Vector2.zero;
        acRT.sizeDelta = new Vector2(42, 42);
        var acImg = accentCorner.AddComponent<Image>();
        acImg.sprite = LoadSprite("accent_corner");
        acImg.raycastTarget = false;

        // === FRAME OVERLAY (for variant frames, stretched) ===
        var frameOverlay = CreateChild(root, "FrameOverlay");
        StretchFill(frameOverlay);
        var frameImg = frameOverlay.AddComponent<Image>();
        frameImg.color = new Color(1, 1, 1, 0); // transparent by default
        frameImg.raycastTarget = false;

        // === ADD COMPONENTS ===
        var cardUI = root.AddComponent<CardUI>();
        var handCard = root.AddComponent<HandCard>();
        root.AddComponent<CardDragHandler>();

        // Wire CardUI references
        cardUI.card_image = cardImageImg;
        cardUI.frame_image = frameImg;
        cardUI.card_title = nameTMP;
        cardUI.card_text = abilityTextTMP;
        cardUI.stamina = staminaBadge.valueTMP;
        cardUI.stamina_icon = staminaBadge.iconImg;
        cardUI.grit = gritBadge.valueTMP;
        cardUI.grit_icon = gritBadge.iconImg;
        cardUI.position = posTMP;
        cardUI.position_icon = null; // no separate position icon in new design
        cardUI.run_bonus_text = valueObjs[0];
        cardUI.short_pass_bonus_text = valueObjs[1];
        cardUI.long_pass_bonus_text = valueObjs[2];
        cardUI.suit_icon = suitIconImg;
        cardUI.position_badge_bg = posBadgeImg;
        cardUI.ability_name_text = abilityNameTMP;
        cardUI.stat_label_1 = labelObjs[0];
        cardUI.stat_label_2 = labelObjs[1];
        cardUI.stat_label_3 = labelObjs[2];
        cardUI.slot_requirement_container = slotReqContainer.transform;
        cardUI.player_section = playerSection;
        cardUI.stats = new TraitUI[0];

        // Wire HandCard references
        handCard.card_glow = glowImg;

        // === SAVE PREFAB ===
        string prefabPath = "Assets/TcgEngine/Prefabs/Gameplay/HandCard.prefab";

        // Back up original
        string backupPath = "Assets/TcgEngine/Prefabs/Gameplay/HandCard_old.prefab";
        if (File.Exists(prefabPath) && !File.Exists(backupPath))
            AssetDatabase.CopyAsset(prefabPath, backupPath);

        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        Debug.Log("[HandCardBuilder] Prefab saved to " + prefabPath);
        if (File.Exists(backupPath))
            Debug.Log("[HandCardBuilder] Original backed up to " + backupPath);
    }

    // === HELPERS ===

    struct BadgeRefs
    {
        public TextMeshProUGUI valueTMP;
        public Image iconImg;
    }

    static BadgeRefs CreateStaminaGritBadge(GameObject parent, string name, string bgSprite,
        string iconSprite, string label, string value, Color accentColor, TMP_FontAsset font)
    {
        var badge = CreateChild(parent, name);
        var badgeImg = badge.AddComponent<Image>();
        badgeImg.sprite = LoadSprite(bgSprite);
        badgeImg.type = Image.Type.Sliced;
        badgeImg.color = Color.white;
        badgeImg.raycastTarget = false;
        var badgeLE = badge.AddComponent<LayoutElement>();
        badgeLE.preferredHeight = 18;
        badgeLE.minWidth = 60;
        var badgeHLG = badge.AddComponent<HorizontalLayoutGroup>();
        badgeHLG.spacing = 2;
        badgeHLG.padding = new RectOffset(8, 6, 2, 2);
        badgeHLG.childAlignment = TextAnchor.MiddleLeft;
        badgeHLG.childForceExpandWidth = false;
        badgeHLG.childForceExpandHeight = false;

        // Icon
        var icon = CreateChild(badge, "Icon");
        var iconImg = icon.AddComponent<Image>();
        iconImg.sprite = LoadSprite(iconSprite);
        iconImg.preserveAspect = true;
        iconImg.color = accentColor;
        iconImg.raycastTarget = false;
        var iconLE = icon.AddComponent<LayoutElement>();
        iconLE.preferredWidth = 10;
        iconLE.preferredHeight = 10;

        // Combined label+value text
        var text = CreateChild(badge, "Text");
        var textTMP = text.AddComponent<TextMeshProUGUI>();
        textTMP.font = font;
        textTMP.fontSize = 8;
        textTMP.fontStyle = FontStyles.UpperCase;
        textTMP.color = Color.white;
        textTMP.text = label + " " + value;
        textTMP.alignment = TextAlignmentOptions.Left;
        textTMP.enableWordWrapping = false;
        textTMP.raycastTarget = false;
        var textLE = text.AddComponent<LayoutElement>();
        textLE.preferredHeight = 14;
        textLE.flexibleWidth = 1;

        return new BadgeRefs { valueTMP = textTMP, iconImg = iconImg };
    }

    static GameObject CreateChild(GameObject parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static void StretchFill(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Sprite LoadSprite(string name)
    {
        return Resources.Load<Sprite>(spritePath + name);
    }
}
