using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

namespace SliceAR
{
    /// <summary>
    /// Temporary on-screen readout for debugging the AR scene-switch (3D-&gt;AR) black/frozen issue. Shows the
    /// live AR session state, why tracking is limited, whether camera frames are still arriving, and the XR
    /// loader init state. This tells us whether a stuck re-entry is a session problem (state never reaches
    /// SessionTracking), a tracking problem (frames arrive but pose is lost), or a rendering problem (session
    /// tracks + frames arrive but passthrough is still black = the camera-background renderer). Added at
    /// runtime by <see cref="ARModeController"/> so it only appears in the AR scene. Remove once diagnosed.
    /// </summary>
    public class ARDiagnosticUI : MonoBehaviour
    {
        private Text text;
        private ARCameraManager cameraManager;
        private bool subscribed;
        private int frameCount;
        private float lastFrameTime = -1f;

        private void Start()
        {
            var canvasGO = new GameObject("ARDiagnosticCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 600;   // above the mode/LUT UI, below the disclaimer modal
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);

            var bgGO = new GameObject("Bg");
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bg = bgGO.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);
            var bgrt = bgGO.GetComponent<RectTransform>();
            bgrt.anchorMin = new Vector2(0f, 1f);
            bgrt.anchorMax = new Vector2(0f, 1f);
            bgrt.pivot = new Vector2(0f, 1f);
            bgrt.anchoredPosition = new Vector2(20f, -620f);
            bgrt.sizeDelta = new Vector2(680f, 300f);

            var txtGO = new GameObject("Text");
            txtGO.transform.SetParent(bgGO.transform, false);
            text = txtGO.AddComponent<Text>();
            text.font = AppFont.Get();
            text.fontSize = 30;
            text.color = new Color(0.4f, 1f, 0.5f);
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var trt = txtGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(16f, 12f);
            trt.offsetMax = new Vector2(-16f, -12f);
        }

        private void Update()
        {
            if (!subscribed)
            {
                cameraManager = Object.FindObjectOfType<ARCameraManager>();
                if (cameraManager != null)
                {
                    cameraManager.frameReceived += OnFrameReceived;
                    subscribed = true;
                }
            }

            if (text == null)
                return;

            var session = Object.FindObjectOfType<ARSession>();
            float sinceFrame = lastFrameTime < 0f ? -1f : Time.time - lastFrameTime;

            text.text =
                "AR DIAG\n" +
                "session.state: " + ARSession.state + "\n" +
                "notTracking: " + ARSession.notTrackingReason + "\n" +
                "session: " + (session == null ? "MISSING" : (session.enabled ? "enabled" : "DISABLED")) + "\n" +
                "camMgr: " + (cameraManager == null ? "MISSING"
                              : (cameraManager.enabled ? "enabled" : "DISABLED")) + "\n" +
                "camFrames: " + frameCount +
                (sinceFrame < 0f ? "  (none yet)" : "  (" + sinceFrame.ToString("0.0") + "s ago)");
        }

        private void OnFrameReceived(ARCameraFrameEventArgs args)
        {
            frameCount++;
            lastFrameTime = Time.time;
        }

        private void OnDestroy()
        {
            if (cameraManager != null)
                cameraManager.frameReceived -= OnFrameReceived;
        }
    }
}
