using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using TcgEngine.Gameplay;
using DG.Tweening;

namespace TcgEngine.UI
{
    /// <summary>
    /// Ability spotlight with two modes:
    /// 1) Immediate: glow + banner on ability fire (LiveBall / out-of-play phases)
    /// 2) Showcase: pre-snap playback of the play's impact records (Game.play_impacts) —
    ///    camera zoom + glow + value banner per impacted card, sorted football-chronologically.
    /// </summary>
    public class AbilitySpotlight : MonoBehaviour
    {
        [Header("Glow")]
        public Color offenseGlow = new Color(0.3f, 1f, 0.45f, 1f);
        public Color defenseGlow = new Color(1f, 0.45f, 0.45f, 1f);
        public float glowFadeIn = 0.15f;
        public float glowFadeOut = 0.3f;

        [Header("Scale Punch")]
        public float punchScale = 0.1f;
        public float punchDuration = 0.3f;

        [Header("Banner")]
        public float bannerYOffset = 120f;
        public float bannerFadeOut = 0.3f;

        [Header("Showcase (Bullet-Time)")]
        public float showcaseSlowMultiplier = 5f;  // route speed multiplier (5x = 20% speed)
        public float showcaseCameraZoom = 0.85f;
        public float showcaseZoomDur = 0.25f;
        public float showcaseHoldDur = 2.0f;
        public float showcasePerAbility = 2.4f;     // total real-time per ability

        private Canvas overlayCanvas;
        private readonly List<SpotlightBanner> bannerPool = new List<SpotlightBanner>();

        private readonly List<ShowcaseEntry> pendingShowcases = new List<ShowcaseEntry>();

        public struct ShowcaseEntry
        {
            public AbilityData ability;
            public Card caster;
            public BoardCard boardCard;
            public PlayerPositionGrp posGroup;
            public bool isOffense;
            public int sortKey;
            public string valueLine; // "+5 RUN" etc. from PlayImpact; empty for non-stat abilities
        }

        private class SpotlightBanner
        {
            public GameObject root;
            public Text effectText;
            public Text triggerText;
            public CanvasGroup group;
            public Image background;
            public Image accentBar;
            public BoardCard trackedCard;
        }

        void Awake()
        {
            BuildCanvas();
        }

        void Start()
        {
            GameClient client = GameClient.Get();
            if (client != null)
            {
                client.onAbilityStart += OnAbilityStart;
                client.onAbilityEnd += OnAbilityEnd;
            }
        }

        void OnDestroy()
        {
            GameClient client = GameClient.Get();
            if (client != null)
            {
                client.onAbilityStart -= OnAbilityStart;
                client.onAbilityEnd -= OnAbilityEnd;
            }
        }

        void Update()
        {
            if (Camera.main == null) return;

            foreach (var banner in bannerPool)
            {
                if (banner.trackedCard == null || !banner.root.activeSelf) continue;
                Vector3 screenPos = Camera.main.WorldToScreenPoint(banner.trackedCard.transform.position);
                screenPos.y += bannerYOffset * (Screen.height / 1080f);
                banner.root.transform.position = screenPos;
            }
        }

        // ── Ability Events (immediate mode only) ─────────────

        private static bool InPlayWindow(Game g)
        {
            // The impact-showcase pipeline owns these phases — no immediate flashes
            return g != null && (g.phase == GamePhase.RevealPlayCalls
                || g.phase == GamePhase.SlotSpin
                || g.phase == GamePhase.Resolution);
        }

        private void OnAbilityStart(AbilityData ability, Card caster)
        {
            if (ability == null || caster == null) return;
            if (InPlayWindow(GameClient.Get()?.GetGameData())) return;

            BoardCard bcard = BoardCard.Get(caster.uid);
            if (bcard == null) return;

            ShowImmediate(ability, caster, bcard);
        }

        private void OnAbilityEnd(AbilityData ability, Card caster)
        {
            if (ability == null || caster == null) return;
            if (InPlayWindow(GameClient.Get()?.GetGameData())) return;

            BoardCard bcard = BoardCard.Get(caster.uid);
            if (bcard == null) return;

            bcard.spotlightActive = false;

            SpriteRenderer glow = bcard.card_glow;
            if (glow != null)
            {
                glow.DOKill();
                glow.DOFade(0f, glowFadeOut).SetLink(bcard.gameObject);
            }

            foreach (var banner in bannerPool)
            {
                if (banner.trackedCard == bcard && banner.root.activeSelf)
                {
                    var b = banner;
                    b.group.DOKill();
                    b.group.DOFade(0f, bannerFadeOut)
                        .SetLink(b.root)
                        .OnComplete(() =>
                        {
                            b.trackedCard = null;
                            b.root.SetActive(false);
                        });
                }
            }
        }

        // ── Immediate Mode (existing behavior) ───────────────

        private void ShowImmediate(AbilityData ability, Card caster, BoardCard bcard)
        {
            Game g = GameClient.Get()?.GetGameData();
            bool isOffense = g != null && g.current_offensive_player != null
                             && caster.player_id == g.current_offensive_player.player_id;
            Color glowColor = isOffense ? offenseGlow : defenseGlow;

            bcard.spotlightActive = true;

            SpriteRenderer glow = bcard.card_glow;
            if (glow != null)
            {
                glow.DOKill();
                glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
                glow.DOFade(1f, glowFadeIn).SetLink(bcard.gameObject);
            }

            bcard.transform.DOKill();
            bcard.transform.DOPunchScale(Vector3.one * punchScale, punchDuration, 6, 0.5f)
                .SetLink(bcard.gameObject);

            var display = AbilityTextGenerator.Generate(ability);
            string effectLine = !string.IsNullOrEmpty(display.effect) ? display.effect
                : (!string.IsNullOrEmpty(ability.title) ? ability.title : ability.id);

            if (!string.IsNullOrEmpty(effectLine))
            {
                SpotlightBanner banner = GetOrCreateBanner();
                banner.trackedCard = bcard;
                banner.effectText.text = effectLine;
                banner.triggerText.text = display.trigger ?? "";
                banner.triggerText.gameObject.SetActive(!string.IsNullOrEmpty(display.trigger));
                banner.accentBar.color = glowColor;
                banner.group.alpha = 1f;
                banner.root.SetActive(true);
            }
        }

        // ── Showcase Mode ─────────────────────────────────────

        public bool HasPendingShowcases => pendingShowcases.Count > 0;

        /// <summary>Short label for a PlayImpact's status delta, e.g. "+5 RUN", "-2 DEEP COV".</summary>
        private static string ImpactValueLine(int statusType, int value)
        {
            if (statusType == 0 || value == 0) return "";
            string label = (StatusType)statusType switch
            {
                StatusType.AddedRunBonus => "RUN",
                StatusType.AddedShortPassBonus => "SHORT PASS",
                StatusType.AddedDeepPassBonus => "DEEP PASS",
                StatusType.AddedRunCoverageBonus => "RUN COV",
                StatusType.AddedShortPassCoverageBonus => "SHORT COV",
                StatusType.AddedDeepPassCoverageBonus => "DEEP COV",
                _ => "",
            };
            if (label == "") return "";
            return $"{value:+#;-#;0} {label}";
        }

        /// <summary>
        /// Build showcase entries from the server's per-ability impact records for this play.
        /// Called by PlayAnimationController at Resolution start (pre-snap).
        /// </summary>
        public void BuildFromImpacts(Game g)
        {
            pendingShowcases.Clear();
            if (g == null || g.play_impacts == null) return;

            PlayType pt = g.current_offensive_player?.SelectedPlay ?? PlayType.Huddle;

            foreach (var impact in g.play_impacts)
            {
                Card caster = null;
                foreach (var p in g.players)
                {
                    caster = p.cards_board.FirstOrDefault(c => c.uid == impact.caster_uid);
                    if (caster != null) break;
                }
                if (caster == null) continue;

                BoardCard bcard = BoardCard.Get(caster.uid);
                if (bcard == null) continue;

                AbilityData ability = AbilityData.Get(impact.ability_id);
                if (ability == null) continue;

                bool isOff = g.current_offensive_player != null
                             && caster.player_id == g.current_offensive_player.player_id;
                PlayerPositionGrp grp = caster.Data != null ? caster.Data.playerPosition : PlayerPositionGrp.NONE;

                pendingShowcases.Add(new ShowcaseEntry
                {
                    ability = ability,
                    caster = caster,
                    boardCard = bcard,
                    posGroup = grp,
                    isOffense = isOff,
                    sortKey = ShowcaseSortKey(grp, isOff, pt),
                    valueLine = ImpactValueLine(impact.status_type, impact.value),
                });
            }
        }

        public IEnumerator FlushAndProcess(bool bulletTime = true)
        {
            if (pendingShowcases.Count == 0)
                yield break;

            // Sort by position group (football chronological order)
            pendingShowcases.Sort((a, b) => a.sortKey.CompareTo(b.sortKey));

            GameCamera cam = GameCamera.Get();
            int total = pendingShowcases.Count(e => e.boardCard != null);
            int shown = 0;

            foreach (var entry in pendingShowcases)
            {
                if (entry.boardCard == null) continue;

                Color glowColor = entry.isOffense ? offenseGlow : defenseGlow;

                // 1. Slow routes via TimeController (skipped pre-snap — field is static)
                if (bulletTime)
                {
                    if (TimeController.Instance != null)
                        TimeController.Instance.SetBulletTime(1f / showcaseSlowMultiplier, 0.15f);
                    else
                        SlotMovementQueue.SetAllSpeed(showcaseSlowMultiplier);
                }

                // 2. Camera zoom to caster
                if (cam != null)
                    cam.FocusOn(entry.boardCard.transform.position, showcaseCameraZoom, showcaseZoomDur);

                // 3. Glow + scale punch
                entry.boardCard.spotlightActive = true;
                SpriteRenderer glow = entry.boardCard.card_glow;
                if (glow != null)
                {
                    glow.DOKill();
                    glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
                    glow.DOFade(1f, glowFadeIn).SetLink(entry.boardCard.gameObject);
                }

                entry.boardCard.transform.DOKill();
                entry.boardCard.transform.DOPunchScale(Vector3.one * punchScale, punchDuration, 6, 0.5f)
                    .SetLink(entry.boardCard.gameObject);

                // 4. Impact panel — fixed screen spot, big type: WHO / WHAT VALUE / WHY
                var display = AbilityTextGenerator.Generate(entry.ability);
                string generatedEffect = display.effect;
                if (string.IsNullOrEmpty(generatedEffect))
                {
                    // Generator had nothing: fall back to printed card text, then a real title (never the raw id)
                    CardData cd = entry.caster.CardData;
                    if (cd != null && !string.IsNullOrEmpty(cd.text)) generatedEffect = cd.text;
                    else if (!string.IsNullOrEmpty(entry.ability.title) && entry.ability.title != entry.ability.id)
                        generatedEffect = entry.ability.title;
                    else generatedEffect = "";
                }
                bool hasValue = !string.IsNullOrEmpty(entry.valueLine);
                shown++;
                ShowImpactPanel(entry, hasValue ? entry.valueLine : "ABILITY", generatedEffect, glowColor, shown, total);

                // 5. Hold (not scaled — showcase timing is independent of field speed)
                yield return new WaitForSeconds(showcaseHoldDur);

                // 6. Release
                HideImpactPanel();

                // Fade glow
                if (glow != null)
                {
                    glow.DOKill();
                    glow.DOFade(0f, glowFadeOut).SetLink(entry.boardCard.gameObject);
                }
                entry.boardCard.spotlightActive = false;

                // Release camera
                if (cam != null)
                    cam.ReleaseFocus(0.2f);

                // Restore route speed
                if (bulletTime)
                {
                    if (TimeController.Instance != null)
                        TimeController.Instance.ResetSpeed(0.15f);
                    else
                        SlotMovementQueue.SetAllSpeed(1f);
                }

                // Brief gap before next ability
                yield return new WaitForSeconds(showcasePerAbility - showcaseHoldDur);
            }

            pendingShowcases.Clear();
        }

        // ── Impact Panel (showcase) ───────────────────────────

        private RectTransform impactRoot;
        private CanvasGroup impactGroup;
        private Image impactArt, impactAccent;
        private Text impactSide, impactName, impactValue, impactDesc, impactCount;
        private static readonly Vector2 ImpactShownPos = new Vector2(70f, 40f);

        private Text MakeText(Transform parent, string name, int size, FontStyle style, Color color,
            Vector2 anchorMin, Vector2 anchorMax, TextAnchor align)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text t = go.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.supportRichText = false;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return t;
        }

        private void BuildImpactPanel()
        {
            GameObject root = new GameObject("ImpactPanel");
            root.transform.SetParent(overlayCanvas.transform, false);
            impactRoot = root.AddComponent<RectTransform>();
            // Left-middle: recap panel's column (recap only shows post-play), clear of mini slots on the right
            impactRoot.anchorMin = impactRoot.anchorMax = new Vector2(0f, 0.5f);
            impactRoot.pivot = new Vector2(0f, 0.5f);
            impactRoot.sizeDelta = new Vector2(760f, 300f);
            impactRoot.anchoredPosition = ImpactShownPos;

            impactGroup = root.AddComponent<CanvasGroup>();
            impactGroup.blocksRaycasts = false;
            impactGroup.interactable = false;
            impactGroup.alpha = 0f;

            Image bg = root.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.06f, 0.1f, 0.94f);
            bg.raycastTarget = false;

            GameObject accentGo = new GameObject("Accent");
            accentGo.transform.SetParent(root.transform, false);
            impactAccent = accentGo.AddComponent<Image>();
            impactAccent.raycastTarget = false;
            RectTransform art = accentGo.GetComponent<RectTransform>();
            art.anchorMin = new Vector2(0f, 0f); art.anchorMax = new Vector2(0f, 1f);
            art.pivot = new Vector2(0f, 0.5f); art.sizeDelta = new Vector2(10f, 0f);

            GameObject artGo = new GameObject("Art");
            artGo.transform.SetParent(root.transform, false);
            impactArt = artGo.AddComponent<Image>();
            impactArt.preserveAspect = true;
            impactArt.raycastTarget = false;
            RectTransform irt = artGo.GetComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.03f, 0.06f); irt.anchorMax = new Vector2(0.31f, 0.94f);
            irt.offsetMin = irt.offsetMax = Vector2.zero;

            Color grey = new Color(0.75f, 0.78f, 0.85f);
            impactSide  = MakeText(root.transform, "Side", 26, FontStyle.Bold, grey,
                new Vector2(0.34f, 0.82f), new Vector2(0.80f, 0.96f), TextAnchor.MiddleLeft);
            impactCount = MakeText(root.transform, "Count", 24, FontStyle.Normal, grey,
                new Vector2(0.80f, 0.82f), new Vector2(0.97f, 0.96f), TextAnchor.MiddleRight);
            impactName  = MakeText(root.transform, "Name", 38, FontStyle.Bold, Color.white,
                new Vector2(0.34f, 0.64f), new Vector2(0.97f, 0.83f), TextAnchor.MiddleLeft);
            impactValue = MakeText(root.transform, "Value", 76, FontStyle.Bold, Color.white,
                new Vector2(0.34f, 0.33f), new Vector2(0.97f, 0.66f), TextAnchor.MiddleLeft);
            impactValue.resizeTextForBestFit = true;
            impactValue.resizeTextMinSize = 30; impactValue.resizeTextMaxSize = 76;
            impactDesc  = MakeText(root.transform, "Desc", 30, FontStyle.Normal, new Color(0.9f, 0.9f, 0.9f),
                new Vector2(0.34f, 0.05f), new Vector2(0.97f, 0.33f), TextAnchor.UpperLeft);
            impactDesc.resizeTextForBestFit = true;
            impactDesc.resizeTextMinSize = 20; impactDesc.resizeTextMaxSize = 30;
        }

        private void ShowImpactPanel(ShowcaseEntry entry, string headline, string desc, Color sideColor, int index, int total)
        {
            if (impactRoot == null) BuildImpactPanel();

            CardData cd = entry.caster.CardData;
            Sprite art = cd != null ? cd.GetFullArt(entry.caster.VariantData) : null;
            impactArt.sprite = art;
            impactArt.enabled = art != null;

            impactAccent.color = sideColor;
            impactSide.text = (entry.isOffense ? "OFFENSE" : "DEFENSE") + "  -  " + entry.posGroup.ToString().Replace('_', '/');
            impactSide.color = sideColor;
            impactName.text = cd != null && !string.IsNullOrEmpty(cd.title) ? cd.title : entry.caster.card_id;
            impactValue.text = headline;
            impactValue.color = sideColor;
            impactDesc.text = desc;
            impactCount.text = total > 1 ? $"{index}/{total}" : "";

            impactGroup.DOKill();
            impactRoot.DOKill();
            impactRoot.anchoredPosition = ImpactShownPos + new Vector2(-120f, 0f);
            impactRoot.DOAnchorPos(ImpactShownPos, 0.25f).SetEase(Ease.OutQuad).SetLink(impactRoot.gameObject);
            impactGroup.DOFade(1f, 0.2f).SetEase(Ease.OutQuad).SetLink(impactRoot.gameObject);
            impactValue.transform.DOKill();
            impactValue.transform.localScale = Vector3.one;
            impactValue.transform.DOPunchScale(Vector3.one * 0.15f, 0.35f, 5, 0.5f).SetLink(impactRoot.gameObject);
        }

        private void HideImpactPanel()
        {
            if (impactGroup == null) return;
            impactGroup.DOKill();
            impactGroup.DOFade(0f, bannerFadeOut).SetEase(Ease.OutQuad).SetLink(impactRoot.gameObject);
        }

        // ── Sort Key ──────────────────────────────────────────

        /// <summary>
        /// Position group sort for showcase order.
        /// Pass: OL→QB→DL→RB_TE→LB→WR→DB
        /// Run:  OL→RB_TE→DL→QB→LB→DB→WR
        /// </summary>
        private static int ShowcaseSortKey(PlayerPositionGrp grp, bool isOff, PlayType pt)
        {
            bool isPass = (pt == PlayType.ShortPass || pt == PlayType.LongPass);

            if (isPass)
            {
                return grp switch
                {
                    PlayerPositionGrp.OL => isOff ? 0 : 50,
                    PlayerPositionGrp.QB => isOff ? 1 : 50,
                    PlayerPositionGrp.DL => isOff ? 50 : 2,
                    PlayerPositionGrp.RB_TE => isOff ? 3 : 50,
                    PlayerPositionGrp.LB => isOff ? 50 : 4,
                    PlayerPositionGrp.WR => isOff ? 5 : 50,
                    PlayerPositionGrp.DB => isOff ? 50 : 6,
                    _ => 50
                };
            }
            else // Run
            {
                return grp switch
                {
                    PlayerPositionGrp.OL => isOff ? 0 : 50,
                    PlayerPositionGrp.RB_TE => isOff ? 1 : 50,
                    PlayerPositionGrp.DL => isOff ? 50 : 2,
                    PlayerPositionGrp.QB => isOff ? 3 : 50,
                    PlayerPositionGrp.LB => isOff ? 50 : 4,
                    PlayerPositionGrp.DB => isOff ? 50 : 5,
                    PlayerPositionGrp.WR => isOff ? 6 : 50,
                    _ => 50
                };
            }
        }

        // ── Banner Pool ───────────────────────────────────────

        private SpotlightBanner GetOrCreateBanner()
        {
            foreach (var b in bannerPool)
            {
                if (b.trackedCard == null && !b.root.activeSelf)
                    return b;
            }
            return CreateBanner();
        }

        private SpotlightBanner CreateBanner()
        {
            GameObject root = new GameObject("SpotlightBanner");
            root.transform.SetParent(overlayCanvas.transform, false);

            CanvasGroup cg = root.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            Image bg = root.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.88f);

            RectTransform rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(560f, 104f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            // Accent bar (left edge)
            GameObject accentGo = new GameObject("Accent");
            accentGo.transform.SetParent(root.transform, false);
            Image accent = accentGo.AddComponent<Image>();
            accent.color = Color.white;
            RectTransform art = accentGo.GetComponent<RectTransform>();
            art.anchorMin = new Vector2(0f, 0f);
            art.anchorMax = new Vector2(0f, 1f);
            art.pivot = new Vector2(0f, 0.5f);
            art.sizeDelta = new Vector2(4f, 0f);
            art.anchoredPosition = Vector2.zero;

            // Trigger text (small, grey, top line)
            GameObject triggerGo = new GameObject("TriggerText");
            triggerGo.transform.SetParent(root.transform, false);
            Text triggerT = triggerGo.AddComponent<Text>();
            triggerT.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            triggerT.fontSize = 22;
            triggerT.fontStyle = FontStyle.Normal;
            triggerT.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            triggerT.alignment = TextAnchor.LowerCenter;
            triggerT.supportRichText = false;
            RectTransform trigRT = triggerGo.GetComponent<RectTransform>();
            trigRT.anchorMin = new Vector2(0f, 0.5f);
            trigRT.anchorMax = new Vector2(1f, 1f);
            trigRT.offsetMin = new Vector2(8f, 0f);
            trigRT.offsetMax = new Vector2(-4f, -2f);

            // Effect text (larger, colored, bottom line)
            GameObject effectGo = new GameObject("EffectText");
            effectGo.transform.SetParent(root.transform, false);
            Text effectT = effectGo.AddComponent<Text>();
            effectT.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            effectT.fontSize = 32;
            effectT.fontStyle = FontStyle.Bold;
            effectT.color = Color.white;
            effectT.alignment = TextAnchor.UpperCenter;
            effectT.supportRichText = false;
            RectTransform effRT = effectGo.GetComponent<RectTransform>();
            effRT.anchorMin = new Vector2(0f, 0f);
            effRT.anchorMax = new Vector2(1f, 0.5f);
            effRT.offsetMin = new Vector2(8f, 2f);
            effRT.offsetMax = new Vector2(-4f, 0f);

            root.SetActive(false);

            var banner = new SpotlightBanner
            {
                root = root,
                effectText = effectT,
                triggerText = triggerT,
                group = cg,
                background = bg,
                accentBar = accent,
                trackedCard = null
            };
            bannerPool.Add(banner);
            return banner;
        }

        private void BuildCanvas()
        {
            overlayCanvas = gameObject.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = 78; // above YardageSummary 75, below PlayCallReveal 80
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }
    }
}
