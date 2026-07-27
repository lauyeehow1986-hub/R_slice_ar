using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace SliceAR
{
    /// <summary>
    /// Surfaces ARCore's tracking-quality state as a short, actionable hint. When the session cannot
    /// track — a featureless wall, plain flooring, fast device motion — the anchored volume drifts or
    /// relocalises away from where it was placed, which otherwise looks like an unexplained bug. Naming
    /// the cause tells the user what to change; the underlying limit is inherent to ARCore and cannot
    /// be engineered away.
    ///
    /// Self-contained: <see cref="ARModeController"/> adds it at runtime, so it exists only in the AR
    /// scene and needs no scene wiring.
    /// </summary>
    public class ARTrackingHintUI : MonoBehaviour
    {
        // Keep a hint up briefly after the condition clears, so a reason that flickers on and off for a
        // few frames doesn't strobe the label.
        private const float HoldSeconds = 1.5f;

        private Text label;
        private Image backing;
        private string shownKey;
        private float hideAt;

        private void Start()
        {
            BuildUI();
        }

        private void OnEnable()
        {
            Loc.LanguageChanged += RefreshText;
        }

        private void OnDisable()
        {
            Loc.LanguageChanged -= RefreshText;
        }

        private void Update()
        {
            if (label == null)
                return;

            string key = HintKey();
            if (key != null)
            {
                shownKey = key;
                hideAt = Time.time + HoldSeconds;
                RefreshText();
                SetVisible(true);
            }
            else if (Time.time >= hideAt)
            {
                shownKey = null;
                SetVisible(false);
            }
        }

        /// <summary>The localization key for the current tracking problem, or null when tracking is fine.</summary>
        private static string HintKey()
        {
            switch (ARSession.notTrackingReason)
            {
                case NotTrackingReason.InsufficientFeatures: return "track.features";
                case NotTrackingReason.ExcessiveMotion:      return "track.motion";
                case NotTrackingReason.InsufficientLight:    return "track.light";
                case NotTrackingReason.Relocalizing:         return "track.relocalizing";
                case NotTrackingReason.Initializing:         return "track.initializing";
            }

            // Reason is None/Unsupported/CameraUnavailable. Still nudge the user while the session is
            // coming up, since a stationary device never finishes initialising.
            if (ARSession.state == ARSessionState.SessionInitializing)
                return "track.initializing";
            return null;
        }

        private void RefreshText()
        {
            if (label == null || shownKey == null)
                return;
            label.font = AppFont.Get();   // a Text keeps its Font reference, so reassign per language
            label.text = Loc.T(shownKey);
        }

        private void SetVisible(bool on)
        {
            if (label != null && label.gameObject.activeSelf != on)
                label.gameObject.SetActive(on);
            if (backing != null && backing.gameObject.activeSelf != on)
                backing.gameObject.SetActive(on);
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("ARTrackingHintCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 290;   // above the off-screen indicator, below the buttons
            var uiRoot = SafeArea.RootUnder(canvasGO.transform);

            // Sits above the bottom button rows, centred.
            var backingGO = new GameObject("HintBacking");
            backingGO.transform.SetParent(uiRoot, false);
            backing = backingGO.AddComponent<Image>();
            backing.color = new Color(0f, 0f, 0f, 0.55f);
            backing.raycastTarget = false;
            var brt = backingGO.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(820f, 90f);
            brt.anchoredPosition = new Vector2(0f, 560f);
            backingGO.SetActive(false);

            var labelGO = new GameObject("HintText");
            labelGO.transform.SetParent(uiRoot, false);
            label = labelGO.AddComponent<Text>();
            label.font = AppFont.Get();
            label.fontSize = 34;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 0.82f, 0.25f, 1f);   // amber: advisory, not an error
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            var lrt = label.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0f);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.sizeDelta = new Vector2(820f, 90f);
            lrt.anchoredPosition = new Vector2(0f, 560f);
            labelGO.SetActive(false);
        }
    }
}
