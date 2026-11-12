using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TcgEngine.Client;

namespace TcgEngine.UI
{
    /// <summary>
    /// Runtime debug slider panel. Toggle with F2.
    /// Self-builds a scrollable overlay with sliders bound to live game values.
    /// Values reset each restart — no persistence.
    /// </summary>
    public class DevTuningPanel : MonoBehaviour
    {
        private static DevTuningPanel instance;
        public static DevTuningPanel Get() => instance;

        private GameObject panelRoot;
        private bool isVisible;
        private readonly List<SliderEntry> entries = new List<SliderEntry>();

        private class SliderEntry
        {
            public string label;
            public Slider slider;
            public TextMeshProUGUI valueLabel;
            public Action<float> setter;
            public Func<float> getter;
        }

        void Awake()
        {
            instance = this;
            BuildPanel();
            panelRoot.SetActive(false);
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2))
            {
                isVisible = !isVisible;
                panelRoot.SetActive(isVisible);
                if (isVisible) RefreshAllSliders();
            }
        }

        private void RefreshAllSliders()
        {
            foreach (var e in entries)
            {
                if (e.getter != null)
                {
                    e.slider.SetValueWithoutNotify(e.getter());
                    e.valueLabel.text = e.getter().ToString("F2");
                }
            }
        }

        // ── Panel Builder ────────────────────────────────────────

        private void BuildPanel()
        {
            // Canvas
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 990; // dev tool sits above gameplay UI, below DebugHUD (999)
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            gameObject.AddComponent<GraphicRaycaster>();

            // Panel background (right side of screen)
            panelRoot = new GameObject("DevTuningRoot");
            panelRoot.transform.SetParent(transform, false);
            Image bg = panelRoot.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);
            RectTransform rootRT = panelRoot.GetComponent<RectTransform>();
            rootRT.anchorMin = new Vector2(0.65f, 0.05f);
            rootRT.anchorMax = new Vector2(0.98f, 0.95f);
            rootRT.offsetMin = rootRT.offsetMax = Vector2.zero;

            // Title
            GameObject titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panelRoot.transform, false);
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.text = "DEV TUNING (F2)";
            title.fontSize = 16;
            title.fontStyle = FontStyles.Bold;
            title.color = new Color(1f, 0.85f, 0.2f);
            title.alignment = TextAlignmentOptions.Center;
            RectTransform titleRT = titleGo.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0f, 0.94f);
            titleRT.anchorMax = new Vector2(1f, 1f);
            titleRT.offsetMin = titleRT.offsetMax = Vector2.zero;

            // Scroll view
            GameObject scrollGo = new GameObject("ScrollView");
            scrollGo.transform.SetParent(panelRoot.transform, false);
            ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            Image scrollBg = scrollGo.AddComponent<Image>();
            scrollBg.color = Color.clear;
            RectTransform scrollRT = scrollGo.GetComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0.02f, 0.02f);
            scrollRT.anchorMax = new Vector2(0.98f, 0.93f);
            scrollRT.offsetMin = scrollRT.offsetMax = Vector2.zero;
            scroll.viewport = scrollRT;

            // Content container
            GameObject contentGo = new GameObject("Content");
            contentGo.transform.SetParent(scrollGo.transform, false);
            RectTransform contentRT = contentGo.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.offsetMin = contentRT.offsetMax = Vector2.zero;
            scroll.content = contentRT;

            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.padding = new RectOffset(6, 6, 4, 4);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;

            var csf = contentGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Register all tunable sliders
            RegisterSliders(contentGo.transform);
        }

        // ── Slider Registration ──────────────────────────────────

        private void RegisterSliders(Transform parent)
        {
            // BoardStatOverlay
            AddSlider(parent, "Cam Focus Duration", 0.05f, 3f, 1.3f,
                v => { var o = FindAnyObjectByType<BoardStatOverlay>(); if (o) o.cameraFocusDuration = v; },
                () => { var o = FindAnyObjectByType<BoardStatOverlay>(); return o ? o.cameraFocusDuration : 1.3f; });

            AddSlider(parent, "Stat Reveal Cap", 1f, 12f, 4f,
                v => { var o = FindAnyObjectByType<BoardStatOverlay>(); if (o) o.totalRevealCap = v; },
                () => { var o = FindAnyObjectByType<BoardStatOverlay>(); return o ? o.totalRevealCap : 4f; });

            AddSlider(parent, "Hold All Duration", 0.2f, 5f, 1.7f,
                v => { var o = FindAnyObjectByType<BoardStatOverlay>(); if (o) o.holdAllDuration = v; },
                () => { var o = FindAnyObjectByType<BoardStatOverlay>(); return o ? o.holdAllDuration : 1.7f; });

            AddSlider(parent, "Label Fade Duration", 0.2f, 5f, 2.5f,
                v => { var o = FindAnyObjectByType<BoardStatOverlay>(); if (o) o.fadeDuration = v; },
                () => { var o = FindAnyObjectByType<BoardStatOverlay>(); return o ? o.fadeDuration : 2.5f; });

            // GameCamera
            AddSlider(parent, "Cam Follow Speed", 0.5f, 10f, 2.5f,
                v => { var o = GameCamera.Get(); if (o) o.cameraFollowSpeed = v; },
                () => { var o = GameCamera.Get(); return o ? o.cameraFollowSpeed : 2.5f; });

            // PlayAnimationController
            AddSlider(parent, "Pre-Snap Beat", 0.3f, 5f, 2.4f,
                v => { var o = PlayAnimationController.Instance; if (o) o.preSnapBeatDuration = v; },
                () => { var o = PlayAnimationController.Instance; return o ? o.preSnapBeatDuration : 2.4f; });

            AddSlider(parent, "Audible Duration", 0.2f, 3f, 1.5f,
                v => { var o = PlayAnimationController.Instance; if (o) o.audibleDuration = v; },
                () => { var o = PlayAnimationController.Instance; return o ? o.audibleDuration : 1.5f; });

            // Info beats (ability spotlight / recap ledger) — pacing of "what just happened"
            AddSlider(parent, "Ability Beat Hold", 0.5f, 5f, 1.7f,
                v => { var o = FindAnyObjectByType<AbilitySpotlight>(); if (o) { o.showcaseHoldDur = v; o.showcasePerAbility = v + 0.3f; } },
                () => { var o = FindAnyObjectByType<AbilitySpotlight>(); return o ? o.showcaseHoldDur : 1.7f; });

            AddSlider(parent, "Max Ability Beats", 1f, 8f, 3f,
                v => { var o = FindAnyObjectByType<AbilitySpotlight>(); if (o) o.maxShowcases = Mathf.RoundToInt(v); },
                () => { var o = FindAnyObjectByType<AbilitySpotlight>(); return o ? o.maxShowcases : 3f; });

            AddSlider(parent, "Recap Row Stagger", 0.05f, 1.5f, 0.3f,
                v => { var o = YardageSummaryPanel.Get(); if (o) o.rowStagger = v; },
                () => { var o = YardageSummaryPanel.Get(); return o ? o.rowStagger : 0.3f; });

            AddSlider(parent, "Recap Hold", 0.5f, 6f, 2.2f,
                v => { var o = YardageSummaryPanel.Get(); if (o) o.holdDur = v; },
                () => { var o = YardageSummaryPanel.Get(); return o ? o.holdDur : 2.2f; });

            // Field movement (FieldMotionProfile) -- how fast player tokens move
            AddSlider(parent, "Field Speed (master)", 0.25f, 3f, 1f,
                v => { if (TimeController.Instance != null) TimeController.Instance.SetBulletTime(v, 0f); },
                () => TimeController.Instance != null ? TimeController.Instance.FieldTimeScale : 1f);

            AddSlider(parent, "Sprint yd/s", 2f, 12f, 5.5f,
                v => FieldMotionProfile.Current.sprintYps = v,
                () => FieldMotionProfile.Current.sprintYps);

            AddSlider(parent, "Jog yd/s", 1f, 8f, 3f,
                v => FieldMotionProfile.Current.jogYps = v,
                () => FieldMotionProfile.Current.jogYps);

            AddSlider(parent, "Walk yd/s", 0.5f, 4f, 1.5f,
                v => FieldMotionProfile.Current.walkYps = v,
                () => FieldMotionProfile.Current.walkYps);

            AddSlider(parent, "Acceleration", 2f, 30f, 9f,
                v => FieldMotionProfile.Current.accel = v,
                () => FieldMotionProfile.Current.accel);

            AddSlider(parent, "Min Move Time", 0.1f, 2f, 0.45f,
                v => FieldMotionProfile.Current.minMoveDuration = v,
                () => FieldMotionProfile.Current.minMoveDuration);

            AddSlider(parent, "LiveBall Min Move Time", 0.2f, 3f, 0.9f,
                v => FieldMotionProfile.Current.minLiveBallMoveDuration = v,
                () => FieldMotionProfile.Current.minLiveBallMoveDuration);

            AddSlider(parent, "Huddle Max Time", 1f, 8f, 3.5f,
                v => FieldMotionProfile.Current.maxFormationTime = v,
                () => FieldMotionProfile.Current.maxFormationTime);

        }

        // ── Slider Factory ───────────────────────────────────────

        private void AddSlider(Transform parent, string label, float min, float max, float defaultVal,
                               Action<float> setter, Func<float> getter)
        {
            // Row container
            GameObject row = new GameObject(label);
            row.transform.SetParent(parent, false);
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 50f;
            rowLE.flexibleWidth = 1f;

            RectTransform rowRT = row.GetComponent<RectTransform>();

            // Label
            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(row.transform, false);
            var labelTMP = labelGo.AddComponent<TextMeshProUGUI>();
            labelTMP.text = label;
            labelTMP.fontSize = 11;
            labelTMP.color = Color.white;
            labelTMP.alignment = TextAlignmentOptions.Left;
            RectTransform labelRT = labelGo.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0f, 0.55f);
            labelRT.anchorMax = new Vector2(0.7f, 1f);
            labelRT.offsetMin = labelRT.offsetMax = Vector2.zero;

            // Value text
            GameObject valGo = new GameObject("Value");
            valGo.transform.SetParent(row.transform, false);
            var valTMP = valGo.AddComponent<TextMeshProUGUI>();
            valTMP.text = defaultVal.ToString("F2");
            valTMP.fontSize = 11;
            valTMP.color = new Color(0.5f, 1f, 0.5f);
            valTMP.alignment = TextAlignmentOptions.Right;
            RectTransform valRT = valGo.GetComponent<RectTransform>();
            valRT.anchorMin = new Vector2(0.7f, 0.55f);
            valRT.anchorMax = new Vector2(1f, 1f);
            valRT.offsetMin = valRT.offsetMax = Vector2.zero;

            // Slider
            GameObject sliderGo = CreateSlider(row.transform, min, max, defaultVal);
            RectTransform sliderRT = sliderGo.GetComponent<RectTransform>();
            sliderRT.anchorMin = new Vector2(0f, 0f);
            sliderRT.anchorMax = new Vector2(1f, 0.55f);
            sliderRT.offsetMin = sliderRT.offsetMax = Vector2.zero;

            Slider slider = sliderGo.GetComponent<Slider>();
            slider.onValueChanged.AddListener(v =>
            {
                setter(v);
                valTMP.text = v.ToString("F2");
            });

            entries.Add(new SliderEntry
            {
                label = label,
                slider = slider,
                valueLabel = valTMP,
                setter = setter,
                getter = getter
            });
        }

        private GameObject CreateSlider(Transform parent, float min, float max, float defaultVal)
        {
            // Slider root
            GameObject sliderGo = new GameObject("Slider");
            sliderGo.transform.SetParent(parent, false);
            sliderGo.AddComponent<RectTransform>();

            // Background
            GameObject bgGo = new GameObject("Background");
            bgGo.transform.SetParent(sliderGo.transform, false);
            Image bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.25f);
            RectTransform bgRT = bgGo.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = new Vector2(0, 4);
            bgRT.offsetMax = new Vector2(0, -4);

            // Fill area
            GameObject fillAreaGo = new GameObject("FillArea");
            fillAreaGo.transform.SetParent(sliderGo.transform, false);
            RectTransform fillAreaRT = fillAreaGo.AddComponent<RectTransform>();
            fillAreaRT.anchorMin = Vector2.zero;
            fillAreaRT.anchorMax = Vector2.one;
            fillAreaRT.offsetMin = new Vector2(0, 4);
            fillAreaRT.offsetMax = new Vector2(0, -4);

            GameObject fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            Image fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(0.3f, 0.6f, 1f, 0.7f);
            RectTransform fillRT = fillGo.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = fillRT.offsetMax = Vector2.zero;

            // Handle slide area
            GameObject handleAreaGo = new GameObject("HandleSlideArea");
            handleAreaGo.transform.SetParent(sliderGo.transform, false);
            RectTransform handleAreaRT = handleAreaGo.AddComponent<RectTransform>();
            handleAreaRT.anchorMin = Vector2.zero;
            handleAreaRT.anchorMax = Vector2.one;
            handleAreaRT.offsetMin = handleAreaRT.offsetMax = Vector2.zero;

            GameObject handleGo = new GameObject("Handle");
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            Image handleImg = handleGo.AddComponent<Image>();
            handleImg.color = Color.white;
            RectTransform handleRT = handleGo.GetComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(12, 0);
            handleRT.anchorMin = new Vector2(0, 0);
            handleRT.anchorMax = new Vector2(0, 1);

            // Configure slider component
            Slider slider = sliderGo.AddComponent<Slider>();
            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.targetGraphic = handleImg;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = defaultVal;
            slider.wholeNumbers = false;

            return sliderGo;
        }
    }
}
