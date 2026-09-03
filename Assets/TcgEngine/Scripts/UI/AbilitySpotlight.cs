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
    /// 1) Immediate: glow + banner on ability fire (non-Resolution phases)
    /// 2) Buffered showcase: during Resolution, buffers abilities, sorts by position group,
    ///    plays back with slow-mo + camera zoom (bullet-time) after snap.
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
        public float showcaseHoldDur = 1.2f;
        public float showcasePerAbility = 1.8f;     // total real-time per ability

        private Canvas overlayCanvas;
        private readonly List<SpotlightBanner> bannerPool = new List<SpotlightBanner>();

        // Buffering
        private bool isBuffering;
        private readonly List<ShowcaseEntry> pendingShowcases = new List<ShowcaseEntry>();
        private Coroutine showcaseCoroutine;

        public struct ShowcaseEntry
        {
            public AbilityData ability;
            public Card caster;
            public BoardCard boardCard;
            public PlayerPositionGrp posGroup;
            public bool isOffense;
            public int sortKey;
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
                client.onRefreshAll += OnRefreshAll;
            }
        }

        void OnDestroy()
        {
            GameClient client = GameClient.Get();
            if (client != null)
            {
                client.onAbilityStart -= OnAbilityStart;
                client.onAbilityEnd -= OnAbilityEnd;
                client.onRefreshAll -= OnRefreshAll;
            }
        }

        void Update()
        {
            if (Camera.main == null) return;

            foreach (var banner in bannerPool)
            {
                if (banner.trackedCard == null || !banner.root.activeSelf) continue;
                Vector3 screenPos = Camera.main.WorldToScreenPoint(banner.trackedCard.transform.position);
                screenPos.y += bannerYOffset;
                banner.root.transform.position = screenPos;
            }
        }

        // ── Phase tracking for buffer mode ────────────────────

        private void OnRefreshAll()
        {
            Game g = GameClient.Get()?.GetGameData();
            if (g == null) return;

            // Start buffering when Resolution phase begins
            if (g.phase == GamePhase.Resolution && !isBuffering)
            {
                isBuffering = true;
                pendingShowcases.Clear();
            }
            // Stop buffering if we leave Resolution without flushing
            else if (g.phase != GamePhase.Resolution && isBuffering && showcaseCoroutine == null)
            {
                isBuffering = false;
                pendingShowcases.Clear();
            }
        }

        // ── Ability Events ────────────────────────────────────

        private void OnAbilityStart(AbilityData ability, Card caster)
        {
            if (ability == null || caster == null) return;

            BoardCard bcard = BoardCard.Get(caster.uid);
            if (bcard == null) return;

            // If buffering (Resolution phase), queue for later playback
            if (isBuffering)
            {
                Game g = GameClient.Get()?.GetGameData();
                bool isOff = g != null && g.current_offensive_player != null
                             && caster.player_id == g.current_offensive_player.player_id;

                PlayerPositionGrp grp = caster.Data != null ? caster.Data.playerPosition : PlayerPositionGrp.NONE;
                PlayType pt = g?.last_play_type ?? PlayType.Huddle;

                pendingShowcases.Add(new ShowcaseEntry
                {
                    ability = ability,
                    caster = caster,
                    boardCard = bcard,
                    posGroup = grp,
                    isOffense = isOff,
                    sortKey = ShowcaseSortKey(grp, isOff, pt)
                });
                return;
            }

            // Immediate mode (non-Resolution)
            ShowImmediate(ability, caster, bcard);
        }

        private void OnAbilityEnd(AbilityData ability, Card caster)
        {
            if (ability == null || caster == null) return;
            if (isBuffering) return; // showcase handles cleanup

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

        // ── Showcase Mode (Bullet-Time Phase B) ──────────────

        /// <summary>
        /// Called by PlayAnimationController after snap. Sorts buffered abilities
        /// by position group and plays them back with slow-mo + camera zoom.
        /// Returns true if there are abilities to showcase.
        /// </summary>
        public bool HasPendingShowcases => pendingShowcases.Count > 0;

        public IEnumerator FlushAndProcess()
        {
            isBuffering = false;

            if (pendingShowcases.Count == 0)
                yield break;

            // Sort by position group (football chronological order)
            pendingShowcases.Sort((a, b) => a.sortKey.CompareTo(b.sortKey));

            GameCamera cam = GameCamera.Get();

            foreach (var entry in pendingShowcases)
            {
                if (entry.boardCard == null) continue;

                Color glowColor = entry.isOffense ? offenseGlow : defenseGlow;

                // 1. Slow routes via TimeController
                if (TimeController.Instance != null)
                    TimeController.Instance.SetBulletTime(1f / showcaseSlowMultiplier, 0.15f);
                else
                    SlotMovementQueue.SetAllSpeed(showcaseSlowMultiplier);

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

                // 4. Two-line banner
                var display = AbilityTextGenerator.Generate(entry.ability);
                string effectLine = !string.IsNullOrEmpty(display.effect) ? display.effect
                    : (!string.IsNullOrEmpty(entry.ability.title) ? entry.ability.title : entry.ability.id);

                SpotlightBanner banner = null;
                if (!string.IsNullOrEmpty(effectLine))
                {
                    banner = GetOrCreateBanner();
                    banner.trackedCard = entry.boardCard;
                    banner.effectText.text = effectLine;
                    banner.effectText.color = entry.isOffense
                        ? new Color(0.3f, 1f, 0.45f)
                        : new Color(1f, 0.45f, 0.45f);
                    banner.triggerText.text = display.trigger ?? "";
                    banner.triggerText.gameObject.SetActive(!string.IsNullOrEmpty(display.trigger));
                    banner.accentBar.color = glowColor;
                    banner.group.alpha = 1f;
                    banner.root.SetActive(true);
                }

                // 5. Hold (not scaled — showcase timing is independent of field speed)
                yield return new WaitForSeconds(showcaseHoldDur);

                // 6. Fade banner, release
                if (banner != null)
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
                if (TimeController.Instance != null)
                    TimeController.Instance.ResetSpeed(0.15f);
                else
                    SlotMovementQueue.SetAllSpeed(1f);

                // Brief gap before next ability
                yield return new WaitForSeconds(showcasePerAbility - showcaseHoldDur);
            }

            pendingShowcases.Clear();
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
            rt.sizeDelta = new Vector2(360f, 60f);
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
            triggerT.fontSize = 10;
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
            effectT.fontSize = 14;
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
            overlayCanvas.sortingOrder = 60;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }
    }
}
