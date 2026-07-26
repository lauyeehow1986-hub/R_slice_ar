using UnityEngine;
using UnityEngine.UI;

namespace SliceAR
{
    /// <summary>
    /// On-device frame-time readout: smoothed FPS, smoothed milliseconds, and the worst frame in a
    /// rolling window. Editor timings say little about a mobile GPU raymarching a volume, so the only
    /// meaningful way to judge the cost of gradient shading, Clip vs Slice, or AR vs 3D is to read it
    /// on the phone.
    ///
    /// The worst-frame figure matters more than the average here: a volume raycaster that averages 45
    /// FPS but spikes to 90 ms feels broken in AR, where the passthrough makes every hitch visible.
    ///
    /// Self-contained: <see cref="SliceModeUI"/> adds it at runtime, so it exists in both scenes with
    /// no scene wiring. Hidden unless <see cref="VolumeSession.ShowPerfHUD"/> is on.
    /// </summary>
    public class PerfHUD : MonoBehaviour
    {
        private const float SmoothingFactor = 0.1f;   // EMA weight on the newest frame
        private const float WorstWindow = 3f;         // seconds before the worst-frame figure resets
        private const float TextInterval = 0.25f;     // throttle label rebuilds; they are not free

        private Text label;
        private GameObject backing;

        private float smoothedMs;
        private float worstMs;
        private float worstResetAt;
        private float nextTextAt;

        private void Start()
        {
            BuildUI();
        }

        private void Update()
        {
            if (label == null)
                return;

            bool show = VolumeSession.ShowPerfHUD;
            if (backing != null && backing.activeSelf != show)
                backing.SetActive(show);
            if (label.gameObject.activeSelf != show)
                label.gameObject.SetActive(show);
            if (!show)
                return;

            // Unscaled: this measures real elapsed wall time, independent of any timeScale.
            float ms = Time.unscaledDeltaTime * 1000f;
            smoothedMs = smoothedMs <= 0f ? ms : Mathf.Lerp(smoothedMs, ms, SmoothingFactor);

            if (Time.unscaledTime >= worstResetAt)
            {
                worstMs = ms;
                worstResetAt = Time.unscaledTime + WorstWindow;
            }
            else if (ms > worstMs)
            {
                worstMs = ms;
            }

            if (Time.unscaledTime >= nextTextAt)
            {
                nextTextAt = Time.unscaledTime + TextInterval;
                int fps = Mathf.RoundToInt(1000f / Mathf.Max(smoothedMs, 0.001f));
                label.text = fps + " FPS   " + smoothedMs.ToString("0.0") + " ms   worst "
                             + worstMs.ToString("0.0") + " ms";
            }
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("PerfHUDCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;   // above the tracking hint, below the disclaimer modal

            // Top-left, below the Import button, where nothing else draws in either scene.
            var backingGO = new GameObject("PerfBacking");
            backingGO.transform.SetParent(canvasGO.transform, false);
            var img = backingGO.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.55f);
            img.raycastTarget = false;
            var brt = backingGO.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f);
            brt.pivot = new Vector2(0f, 1f);
            brt.sizeDelta = new Vector2(520f, 64f);
            brt.anchoredPosition = new Vector2(36f, -230f);
            backing = backingGO;
            backingGO.SetActive(false);

            var labelGO = new GameObject("PerfText");
            labelGO.transform.SetParent(canvasGO.transform, false);
            label = labelGO.AddComponent<Text>();
            label.font = AppFont.Get();
            label.fontSize = 30;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.55f, 1f, 0.55f, 1f);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            var lrt = label.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 1f);
            lrt.pivot = new Vector2(0f, 1f);
            lrt.sizeDelta = new Vector2(520f, 64f);
            lrt.anchoredPosition = new Vector2(36f, -230f);
            labelGO.SetActive(false);
        }
    }
}
