using UnityEngine;

namespace SliceAR
{
    /// <summary>
    /// Keeps a RectTransform inside <see cref="Screen.safeArea"/> so UI never lands under a status bar,
    /// navigation bar, or display cutout.
    ///
    /// This matters from target API 36 (Android 16), which draws every app edge-to-edge and no longer
    /// honours the opt-out. Before that the system reserved the bar space and Unity's window started
    /// below it; from 36 the window is the whole display, so a control anchored 40 px from the top edge
    /// sits underneath the clock.
    ///
    /// Only UI with fixed offsets from a screen edge belongs in here. Anything positioned from
    /// <c>WorldToScreenPoint</c> — the annotation markers, the off-screen volume arrow — must stay on a
    /// full-screen canvas, because insetting it would shift the markers off the anatomy they label.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        /// <summary>
        /// Smallest fraction of the screen a safe area is allowed to claim before we treat the report as
        /// nonsense and ignore it. A wrong inset costs some wasted margin; a zero-sized one would collapse
        /// every control in the scene to nothing, leaving an app with no visible UI and no way back.
        /// Android's bars and cutouts together never come close to half the display.
        /// </summary>
        private const float MinPlausibleFraction = 0.5f;

        private RectTransform rect;
        private Rect appliedArea;
        private int appliedWidth;
        private int appliedHeight;

        /// <summary>
        /// Creates a safe-area-tracking child under <paramref name="parent"/> and returns it, for use as
        /// the parent of edge-anchored UI. Stretches to fill, so children keep the anchors and offsets
        /// they would have used against the canvas — they are simply measured from the safe rect instead.
        /// </summary>
        public static RectTransform RootUnder(Transform parent)
        {
            var go = new GameObject("SafeArea");
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            go.AddComponent<SafeArea>();
            return rt;
        }

        /// <summary>Current insets in pixels (left, bottom, right, top) — for the on-device readout.</summary>
        public static Vector4 Insets()
        {
            Rect area = Screen.safeArea;
            return new Vector4(area.xMin, area.yMin,
                               Screen.width - area.xMax, Screen.height - area.yMax);
        }

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
            Apply();
        }

        // Polled rather than event-driven: Unity raises no notification when the safe area changes, and it
        // does change at runtime -- on rotation, and on some devices a frame or two after launch once the
        // window has been laid out. Comparing two rects per frame is not worth optimising away.
        private void Update()
        {
            if (Screen.safeArea != appliedArea || Screen.width != appliedWidth || Screen.height != appliedHeight)
                Apply();
        }

        private void Apply()
        {
            int w = Screen.width;
            int h = Screen.height;
            if (w <= 0 || h <= 0)
                return;

            Rect area = Screen.safeArea;
            if (area.width < w * MinPlausibleFraction || area.height < h * MinPlausibleFraction)
                return;

            appliedArea = area;
            appliedWidth = w;
            appliedHeight = h;

            // Normalized anchors rather than pixel offsets: the canvases scale with screen size, so an
            // anchor fraction stays correct while a pixel offset would need the scaler's factor applied.
            rect.anchorMin = new Vector2(area.xMin / w, area.yMin / h);
            rect.anchorMax = new Vector2(area.xMax / w, area.yMax / h);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
