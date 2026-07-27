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
    /// The second line reports the safe-area insets. Whether Unity actually surfaces Android's system-bar
    /// insets in <see cref="Screen.safeArea"/> cannot be checked anywhere but on a device, and the whole
    /// edge-to-edge layout rests on it, so the numbers are shown rather than assumed. It rides along on
    /// the existing debug toggle and costs nothing when that is off.
    ///
    /// Self-contained: <see cref="SliceModeUI"/> adds it at runtime, so it exists in both scenes with
    /// no scene wiring. Hidden unless <see cref="VolumeSession.ShowPerfHUD"/> is on.
    /// </summary>
    public class PerfHUD : MonoBehaviour
    {
        private const float SmoothingFactor = 0.1f;   // EMA weight on the newest frame
        private const float WorstWindow = 3f;         // seconds before the worst-frame figure resets
        private const float TextInterval = 0.25f;     // throttle label rebuilds; they are not free

        // A 0.1-weighted average takes roughly 30 frames to catch up, which at 12 FPS is over two seconds.
        // Switching quality or mode moves the true frame time by multiples, so anyone who changes a setting
        // and reads the number straight away sees a value still travelling from the previous one. Snap once
        // the frame time has clearly moved AND stayed moved for a few frames -- long enough that a one-off
        // hitch (the render targets reallocating on a render-scale change costs about a second) does not
        // yank the average with it.
        private const int OutlierFramesToSnap = 3;
        private const float OutlierRatio = 1.6f;

        private Text label;
        private GameObject backing;

        private float smoothedMs;
        private float worstMs;
        private float worstResetAt;
        private float nextTextAt;
        private int outlierFrames;

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

            if (smoothedMs <= 0f)
            {
                smoothedMs = ms;
            }
            else
            {
                bool outlier = ms > smoothedMs * OutlierRatio || ms < smoothedMs / OutlierRatio;
                outlierFrames = outlier ? outlierFrames + 1 : 0;

                if (outlierFrames >= OutlierFramesToSnap)
                {
                    smoothedMs = ms;      // the workload really did change; stop averaging across the change
                    outlierFrames = 0;
                }
                else
                {
                    smoothedMs = Mathf.Lerp(smoothedMs, ms, SmoothingFactor);
                }
            }

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
                Vector4 inset = SafeArea.Insets();
                label.text = fps + " FPS   " + smoothedMs.ToString("0.0") + " ms   worst "
                             + worstMs.ToString("0.0") + " ms\n"
                             + "safe L" + inset.x + " B" + inset.y + " R" + inset.z + " T" + inset.w
                             + "  of " + Screen.width + "x" + Screen.height;
            }
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("PerfHUDCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;   // above the tracking hint, below the disclaimer modal
            // Scaled like the rest of the UI. Without this the HUD's offsets are raw pixels while the
            // Import button's are reference units, so "below the Import button" would only be true on a
            // 1080-wide screen and the two would drift into each other on anything else.
            OrientationScaler.Attach(canvasGO);
            var uiRoot = SafeArea.RootUnder(canvasGO.transform);

            // Top-left, below the Import button (which ends at 240), where nothing else draws.
            var backingGO = new GameObject("PerfBacking");
            backingGO.transform.SetParent(uiRoot, false);
            var img = backingGO.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.55f);
            img.raycastTarget = false;
            var brt = backingGO.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f);
            brt.pivot = new Vector2(0f, 1f);
            brt.sizeDelta = new Vector2(520f, 112f);
            brt.anchoredPosition = new Vector2(36f, -270f);
            backing = backingGO;
            backingGO.SetActive(false);

            var labelGO = new GameObject("PerfText");
            labelGO.transform.SetParent(uiRoot, false);
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
            lrt.sizeDelta = new Vector2(520f, 112f);
            lrt.anchoredPosition = new Vector2(36f, -270f);
            labelGO.SetActive(false);
        }
    }
}
