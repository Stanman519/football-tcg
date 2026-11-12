using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace TcgEngine.Client
{
    /// <summary>
    /// Game Camera has some useful features such as shake
    /// Or raycasting the mouse to a world/screen position
    /// </summary>

    public class GameCamera : MonoBehaviour
    {
        [Tooltip("Lerp speed for camera to follow LOSMarker Y position.")]
        public float cameraFollowSpeed = 2.5f;

        private float shake_timer = 0f;
        private float shake_intensity = 1f;

        private Camera cam;
        private Vector3 shake_vector = Vector3.zero;
        private Vector3 start_pos;

        // Focus/zoom system
        private float baseOrthoSize;
        private Tween focusTween;
        private bool isFocused;

        private static GameCamera instance;

        void Awake()
        {
            instance = this;
            start_pos = transform.position;
            cam = GetComponent<Camera>();
            if (cam != null && cam.orthographic)
                baseOrthoSize = cam.orthographicSize;
        }

        void Update()
        {
            // Follow LOSMarker Y so the line of scrimmage stays centered
            if (LOSMarker.Instance != null)
            {
                start_pos.y = Mathf.Lerp(start_pos.y,
                    LOSMarker.Instance.transform.position.y,
                    cameraFollowSpeed * Time.deltaTime);
            }

            // While focused, DOTween controls position/size — skip manual updates
            if (isFocused) return;

            //Shake FX
            if (shake_timer > 0f)
            {
                shake_timer -= Time.deltaTime;
                shake_vector = new Vector3(Mathf.Cos(shake_timer * Mathf.PI * 16f) * 0.02f, Mathf.Sin(shake_timer * Mathf.PI * 12f) * 0.01f, 0f);
                transform.position = start_pos + shake_vector * shake_intensity;
            }
            else
            {
                transform.position = start_pos;
            }
        }

        public void Shake(float intensity = 1f, float duration = 1f)
        {
            shake_intensity = intensity;
            shake_timer = duration;
        }

        public Vector2 MouseToPercent(Vector3 mouse_pos)
        {
            float x = mouse_pos.x / Screen.width;
            float y = mouse_pos.y / Screen.height;
            return new Vector2(x, y);
        }

        public Ray MouseToRay(Vector3 mouse_pos)
        {
            return cam.ScreenPointToRay(mouse_pos);
        }

        public Vector3 MouseToWorld(Vector2 mouse_pos, float distance = 10f)
        {
            Vector3 wpos = cam.ScreenToWorldPoint(new Vector3(mouse_pos.x, mouse_pos.y, distance));
            return wpos;
        }

        public static Camera GetCamera()
        {
            if (instance != null)
                return instance.cam;
            return null;
        }

        // ── Focus/Zoom ─────────────────────────────────────

        /// <summary>Zoom camera toward a world target. zoomPct=0.85 means 85% of base ortho size.</summary>
        public void FocusOn(Vector3 worldTarget, float zoomPct = 0.85f, float dur = 0.3f)
        {
            if (cam == null || !cam.orthographic || baseOrthoSize <= 0f) return;

            focusTween?.Kill();
            isFocused = true;

            // Nudge position ~30% toward target
            Vector3 nudge = Vector3.Lerp(start_pos, worldTarget, 0.3f);
            nudge.z = start_pos.z; // preserve Z

            float targetSize = baseOrthoSize * zoomPct;
            float d = TimeController.Instance != null ? TimeController.Instance.Scale(dur) : dur;

            Sequence seq = DOTween.Sequence();
            seq.Join(cam.DOOrthoSize(targetSize, d).SetEase(Ease.OutQuad));
            seq.Join(transform.DOMove(nudge, d).SetEase(Ease.OutQuad));
            seq.SetLink(gameObject);
            focusTween = seq;
        }

        /// <summary>
        /// Zoom OUT (never in) so both world points are on screen. No-op when they already are.
        /// Used for long gains/returns; ReleaseFocus() brings the camera back.
        /// </summary>
        public bool FitPlay(Vector3 worldA, Vector3 worldB, float marginYards = 6f, float dur = 0.7f, float maxZoomOut = 2.4f)
        {
            if (cam == null || !cam.orthographic || baseOrthoSize <= 0f) return false;

            float lo = Mathf.Min(worldA.y, worldB.y) - marginYards;
            float hi = Mathf.Max(worldA.y, worldB.y) + marginYards;
            float viewLo = start_pos.y - baseOrthoSize;
            float viewHi = start_pos.y + baseOrthoSize;
            if (!isFocused && lo >= viewLo && hi <= viewHi) return false; // already visible

            // Keep the normal view in frame too, so the zoom reads as "pull back", not a jump cut
            lo = Mathf.Min(lo, viewLo + baseOrthoSize * 0.5f);
            hi = Mathf.Max(hi, viewHi - baseOrthoSize * 0.5f);
            float size = Mathf.Clamp((hi - lo) * 0.5f, baseOrthoSize, baseOrthoSize * maxZoomOut);
            Vector3 pos = start_pos;
            pos.y = (hi + lo) * 0.5f;

            focusTween?.Kill();
            isFocused = true;
            Sequence seq = DOTween.Sequence();
            seq.Join(cam.DOOrthoSize(size, dur).SetEase(Ease.InOutSine));
            seq.Join(transform.DOMove(pos, dur).SetEase(Ease.InOutSine));
            seq.SetLink(gameObject);
            focusTween = seq;
            return true;
        }

        /// <summary>Return camera to base position and ortho size.</summary>
        public void ReleaseFocus(float dur = 0.3f)
        {
            if (!isFocused) return;

            focusTween?.Kill();
            isFocused = false;

            float d = TimeController.Instance != null ? TimeController.Instance.Scale(dur) : dur;

            Sequence seq = DOTween.Sequence();
            seq.Join(cam.DOOrthoSize(baseOrthoSize, d).SetEase(Ease.OutQuad));
            seq.Join(transform.DOMove(start_pos, d).SetEase(Ease.OutQuad));
            seq.SetLink(gameObject);
            focusTween = seq;
        }

        public bool IsFocused => isFocused;

        public static GameCamera Get()
        {
            return instance;
        }
    }
}