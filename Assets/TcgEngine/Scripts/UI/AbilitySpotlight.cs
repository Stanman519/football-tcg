using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TcgEngine.Client;
using Assets.TcgEngine.Scripts.Gameplay;
using DG.Tweening;

namespace TcgEngine.UI
{
    /// <summary>
    /// Marvel Snap-style spotlight: when an ability fires, the caster card
    /// gets a glow pulse, scale bump, and floating title banner.
    /// Self-wires via GameClient events — no manual hookup needed beyond
    /// living on a GameObject in the Game scene.
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
        public float bannerYOffset = 120f; // px above card center (above stat labels)
        public float bannerFadeOut = 0.3f;

        private Canvas overlayCanvas;
        private readonly List<SpotlightBanner> bannerPool = new List<SpotlightBanner>();

        private class SpotlightBanner
        {
            public GameObject root;
            public Text text;
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
                screenPos.y += bannerYOffset;
                banner.root.transform.position = screenPos;
            }
        }

        // -------------------------------------------------------------------

        private void OnAbilityStart(AbilityData ability, Card caster)
        {
            if (ability == null || caster == null) return;

            BoardCard bcard = BoardCard.Get(caster.uid);
            if (bcard == null) return; // hand/deck ability — skip

            Game g = GameClient.Get()?.GetGameData();
            bool isOffense = g != null && g.current_offensive_player != null
                             && caster.player_id == g.current_offensive_player.player_id;
            Color glowColor = isOffense ? offenseGlow : defenseGlow;

            // Flag to prevent hover glow from fighting us
            bcard.spotlightActive = true;

            // Glow pulse
            SpriteRenderer glow = bcard.card_glow;
            if (glow != null)
            {
                glow.DOKill();
                glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
                glow.DOFade(1f, glowFadeIn).SetLink(bcard.gameObject);
            }

            // Scale punch
            bcard.transform.DOKill();
            bcard.transform.DOPunchScale(Vector3.one * punchScale, punchDuration, 6, 0.5f)
                .SetLink(bcard.gameObject);

            // Title banner
            string title = !string.IsNullOrEmpty(ability.title) ? ability.title : ability.id;
            if (!string.IsNullOrEmpty(title))
            {
                SpotlightBanner banner = GetOrCreateBanner();
                banner.trackedCard = bcard;
                banner.text.text = title;
                banner.accentBar.color = glowColor;
                banner.group.alpha = 1f;
                banner.root.SetActive(true);
            }
        }

        private void OnAbilityEnd(AbilityData ability, Card caster)
        {
            if (ability == null || caster == null) return;

            BoardCard bcard = BoardCard.Get(caster.uid);
            if (bcard == null) return;

            bcard.spotlightActive = false;

            // Fade glow out
            SpriteRenderer glow = bcard.card_glow;
            if (glow != null)
            {
                glow.DOKill();
                glow.DOFade(0f, glowFadeOut).SetLink(bcard.gameObject);
            }

            // Fade and release banners tracking this card
            foreach (var banner in bannerPool)
            {
                if (banner.trackedCard == bcard && banner.root.activeSelf)
                {
                    var b = banner; // capture for lambda
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

        // -------------------------------------------------------------------
        // Banner pool (mirrors BoardStatOverlay pattern)
        // -------------------------------------------------------------------

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
            // Root
            GameObject root = new GameObject("SpotlightBanner");
            root.transform.SetParent(overlayCanvas.transform, false);

            CanvasGroup cg = root.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            // Background pill
            Image bg = root.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.88f);

            RectTransform rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160f, 30f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            // Accent bar (left edge, colored by offense/defense)
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

            // Text child
            GameObject textGo = new GameObject("Title");
            textGo.transform.SetParent(root.transform, false);
            Text t = textGo.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 13;
            t.fontStyle = FontStyle.Bold;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.supportRichText = false;
            RectTransform trt = textGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8f, 0f);
            trt.offsetMax = new Vector2(-4f, 0f);

            root.SetActive(false);

            var banner = new SpotlightBanner
            {
                root = root,
                text = t,
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
            overlayCanvas = gameObject.GetComponent<Canvas>();
            if (overlayCanvas == null)
            {
                overlayCanvas = gameObject.AddComponent<Canvas>();
                overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                overlayCanvas.sortingOrder = 60; // above BoardStatOverlay (55)
                gameObject.AddComponent<CanvasScaler>();
            }
        }
    }
}
