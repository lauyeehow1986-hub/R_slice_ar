using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace SliceAR
{
    /// <summary>
    /// Builds a screen-space button at runtime that toggles the <see cref="SliceController"/>
    /// between Clip and Slice modes. Self-contained so no manual UI wiring is needed in a scene.
    /// </summary>
    public class SliceModeUI : MonoBehaviour
    {
        private SliceController controller;
        private MotionSlicer motionSlicer;
        private Text label;
        private Text axisLabel;
        private Text sceneSwitchLabel;  // top-right: switch between the AR and 3D scenes
        private Text lutLabel;          // top-right: cycle the colour LUT (transfer-function palette)
        private Text langLabel;         // top-right: cycle the UI language
        private Text shadingLabel;      // top-right: toggle gradient (normal-based) shading
        private Text recenterLabel;     // bottom-centre: recenter (kept for language refresh)
        private SliceController.SliceMode? shownMode;   // last mode rendered on the Mode button

        // Anatomical orientation markers (DICOM only): one label per screen edge showing which
        // patient direction (R/L/A/P/S/I) points that way for the slice currently on screen.
        private Text markTop, markBottom, markLeft, markRight;
        private Transform orientFrame;   // the volume's LPS-oriented container transform
        private Camera viewCamera;

        private void OnEnable()  { Loc.LanguageChanged += RefreshTexts; }
        private void OnDisable() { Loc.LanguageChanged -= RefreshTexts; }

        private void Start()
        {
            BuildUI();
            UpdateLabel();
        }

        private void Update()
        {
            UpdateOrientationMarkers();
            UpdateAxisLabel();
            // The 3D scene enters Slice mode only once the volume finishes loading (MotionSlicer.Attach),
            // and AR's SliceController is added at runtime — both happen after the initial UpdateLabel, so
            // refresh the Mode label whenever the live mode differs from what's shown (else it reads stale,
            // e.g. "Mode: Clip" while actually in Slice with the Axis button visible).
            EnsureController();
            if (controller != null && shownMode != controller.Mode)
                UpdateLabel();
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("SliceModeCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            canvasGO.AddComponent<GraphicRaycaster>();

            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            // Mode toggle, bottom-centre. Kept narrower than the screen so the annotation controls have
            // clear space in the bottom-left corner (they used to overlap the wider buttons).
            label = MakeButton(canvasGO.transform, "ModeButton",
                new Vector2(0f, 140f), new Vector2(420f, 160f), OnClick);

            // Recenter, bottom-centre just above the mode button — sets the current tilt as the
            // mid-stack neutral and clears accumulated sensor drift.
            recenterLabel = MakeButton(canvasGO.transform, "RecenterButton",
                new Vector2(0f, 320f), new Vector2(420f, 140f), OnRecenter);
            recenterLabel.text = Loc.T("recenter");

            // Axis cycle (Axial/Coronal/Sagittal), above Recenter.
            axisLabel = MakeButton(canvasGO.transform, "AxisButton",
                new Vector2(0f, 480f), new Vector2(420f, 140f), OnCycleAxis);

            // Anatomical edge markers (hidden until a DICOM slice is on screen).
            markTop    = MakeEdgeLabel(canvasGO.transform, "MarkTop",    new Vector2(0.5f, 1f), new Vector2(0f, -110f));
            markBottom = MakeEdgeLabel(canvasGO.transform, "MarkBottom", new Vector2(0.5f, 0f), new Vector2(0f, 520f));
            markLeft   = MakeEdgeLabel(canvasGO.transform, "MarkLeft",   new Vector2(0f, 0.5f), new Vector2(70f, 0f));
            markRight  = MakeEdgeLabel(canvasGO.transform, "MarkRight",  new Vector2(1f, 0.5f), new Vector2(-70f, 0f));

            // Scene switch (top-right): the app has two scenes — ARMode (walk the device through a
            // volume anchored in the room) and ThreeDMode (the stable CT-viewer). Nothing else lets
            // the user move between them, so without this button whichever scene ships as build-index
            // 0 is the only one reachable.
            sceneSwitchLabel = MakeButton(canvasGO.transform, "SceneSwitchButton",
                Vector2.zero, new Vector2(320f, 120f), OnSwitchScene);
            var ssrt = sceneSwitchLabel.transform.parent.GetComponent<RectTransform>();
            ssrt.anchorMin = ssrt.anchorMax = new Vector2(1f, 1f);
            ssrt.pivot = new Vector2(1f, 1f);
            ssrt.anchoredPosition = new Vector2(-40f, -150f);
            var ssimg = sceneSwitchLabel.transform.parent.GetComponent<Image>();
            if (ssimg != null) ssimg.color = new Color(0.10f, 0.30f, 0.45f, 0.85f);
            UpdateSceneSwitchLabel();

            // Colour-LUT picker (top-right, below the scene switch): cycles the transfer-function
            // palette (Grayscale / Hot Metal / Rainbow / Cool) and re-applies it to the loaded volume.
            lutLabel = MakeButton(canvasGO.transform, "LutButton",
                Vector2.zero, new Vector2(320f, 120f), OnCycleLut);
            var lrt = lutLabel.transform.parent.GetComponent<RectTransform>();
            lrt.anchorMin = lrt.anchorMax = new Vector2(1f, 1f);
            lrt.pivot = new Vector2(1f, 1f);
            lrt.anchoredPosition = new Vector2(-40f, -290f);
            var limg = lutLabel.transform.parent.GetComponent<Image>();
            if (limg != null) limg.color = new Color(0.30f, 0.20f, 0.40f, 0.85f);
            lutLabel.fontSize = 34;
            UpdateLutLabel();

            // Language picker (top-right, below the LUT button): cycles the UI language. Selection is
            // static + persisted, so it carries across scene switches and app restarts.
            langLabel = MakeButton(canvasGO.transform, "LangButton",
                Vector2.zero, new Vector2(320f, 120f), OnCycleLanguage);
            var glrt = langLabel.transform.parent.GetComponent<RectTransform>();
            glrt.anchorMin = glrt.anchorMax = new Vector2(1f, 1f);
            glrt.pivot = new Vector2(1f, 1f);
            glrt.anchoredPosition = new Vector2(-40f, -430f);
            var glimg = langLabel.transform.parent.GetComponent<Image>();
            if (glimg != null) glimg.color = new Color(0.20f, 0.35f, 0.25f, 0.85f);
            langLabel.fontSize = 34;
            langLabel.text = Loc.DisplayName(Loc.Current);

            // Gradient-shading toggle (top-right, below the language button): normal-based lighting on the
            // 3D volume (Clip mode / AR). Choice held in VolumeSession so it survives scene switches.
            shadingLabel = MakeButton(canvasGO.transform, "ShadingButton",
                Vector2.zero, new Vector2(320f, 120f), OnToggleShading);
            var shrt = shadingLabel.transform.parent.GetComponent<RectTransform>();
            shrt.anchorMin = shrt.anchorMax = new Vector2(1f, 1f);
            shrt.pivot = new Vector2(1f, 1f);
            shrt.anchoredPosition = new Vector2(-40f, -570f);
            var shimg = shadingLabel.transform.parent.GetComponent<Image>();
            if (shimg != null) shimg.color = new Color(0.40f, 0.30f, 0.15f, 0.85f);
            shadingLabel.fontSize = 34;
            UpdateShadingLabel();

            // The "not for diagnosis" disclaimer is required in-app; add its self-contained UI here so
            // no scene wiring is needed (shows once per launch + a persistent footer).
            if (GetComponent<DisclaimerUI>() == null)
                gameObject.AddComponent<DisclaimerUI>();

            // Annotation tool (markers + measurement) — self-contained, finds the volume itself.
            if (GetComponent<AnnotationManager>() == null)
                gameObject.AddComponent<AnnotationManager>();
        }

        private void OnCycleLut()
        {
            var values = (ColorLUT[])System.Enum.GetValues(typeof(ColorLUT));
            VolumeSession.ColorLUT = values[(((int)VolumeSession.ColorLUT) + 1) % values.Length];

            var tf = TransferFunctions.Build(VolumeSession.WindowPreset, VolumeSession.ColorLUT);
            // Route through the controller so the flat slice plane's TF texture is refreshed too, not
            // just the volume material (otherwise the LUT never changes in Slice mode).
            EnsureController();
            if (controller != null)
                controller.SetTransferFunction(tf);
            else
            {
                var vol = Object.FindObjectOfType<UnityVolumeRendering.VolumeRenderedObject>();
                if (vol != null)
                    vol.SetTransferFunction(tf);
            }
            UpdateLutLabel();
        }

        private void UpdateLutLabel()
        {
            if (lutLabel == null)
                return;
            // "LUT" is kept as the (international) technical abbreviation; only the palette name localizes.
            lutLabel.text = "LUT: " + Loc.T("lut." + VolumeSession.ColorLUT.ToString().ToLowerInvariant());
        }

        private void OnToggleShading()
        {
            VolumeSession.GradientShading = !VolumeSession.GradientShading;
            EnsureController();
            if (controller != null)
                controller.SetShading(VolumeSession.GradientShading);
            UpdateShadingLabel();
        }

        private void UpdateShadingLabel()
        {
            if (shadingLabel == null)
                return;
            shadingLabel.text = Loc.T("shading") + ": " +
                Loc.T(VolumeSession.GradientShading ? "state.on" : "state.off");
        }

        private void OnCycleLanguage()
        {
            // Fires Loc.LanguageChanged, which routes back into RefreshTexts (this component is subscribed)
            // and into the disclaimer + annotation UIs so every label re-renders in the new language.
            Loc.CycleLanguage();
        }

        // Re-render every managed label (and re-pick a glyph-appropriate font) after a language change.
        private void RefreshTexts()
        {
            if (recenterLabel != null) { recenterLabel.font = AppFont.Get(); recenterLabel.text = Loc.T("recenter"); }
            if (langLabel != null)     { langLabel.font = AppFont.Get(); langLabel.text = Loc.DisplayName(Loc.Current); }
            if (shadingLabel != null)  { shadingLabel.font = AppFont.Get(); UpdateShadingLabel(); }
            if (label != null)      label.font = AppFont.Get();
            if (axisLabel != null)  axisLabel.font = AppFont.Get();
            if (sceneSwitchLabel != null) sceneSwitchLabel.font = AppFont.Get();
            if (lutLabel != null)   lutLabel.font = AppFont.Get();
            UpdateLabel();
            UpdateAxisLabel();
            UpdateSceneSwitchLabel();
            UpdateLutLabel();
        }

        private void OnSwitchScene()
        {
            string active = SceneManager.GetActiveScene().name;
            string target = active == "ThreeDMode" ? "ARMode" : "ThreeDMode";
            SceneManager.LoadScene(target);
        }

        private void UpdateSceneSwitchLabel()
        {
            if (sceneSwitchLabel == null)
                return;
            // Label names the destination, not the current scene.
            sceneSwitchLabel.text = SceneManager.GetActiveScene().name == "ThreeDMode"
                ? Loc.T("scene.ar") : Loc.T("scene.3d");
        }

        /// <summary>Create a small fixed anatomical-marker label anchored to a screen edge.</summary>
        private static Text MakeEdgeLabel(Transform parent, string name, Vector2 anchor, Vector2 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = AppFont.Get();
            text.fontSize = 52;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.85f, 0.2f);   // amber, like a viewer's overlay
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(120f, 90f);
            go.SetActive(false);
            return text;
        }

        // Show which patient direction points to each screen edge for the on-screen slice. Only
        // meaningful for DICOM (known LPS orientation) in Slice mode; hidden otherwise.
        private void UpdateOrientationMarkers()
        {
            if (markTop == null)
                return;

            EnsureController();
            bool show = VolumeSession.IsDicomOriented
                        && controller != null
                        && controller.Mode == SliceController.SliceMode.Slice;

            if (show && orientFrame == null)
            {
                var vol = Object.FindObjectOfType<UnityVolumeRendering.VolumeRenderedObject>();
                if (vol != null && vol.meshRenderer != null)
                    orientFrame = vol.meshRenderer.transform;   // LPS-oriented container
            }
            if (viewCamera == null)
                viewCamera = Camera.main;

            if (!show || orientFrame == null || viewCamera == null)
            {
                SetMarkersActive(false);
                return;
            }

            // Patient axes in the LPS-oriented container's local frame (importer assumes standard
            // axial LPS: +X→Left, +Y→Posterior, +Z→Superior). TransformVector carries the importer's
            // handedness flip and the live turntable rotation into world space.
            Vector3 left      = orientFrame.TransformVector(Vector3.right).normalized;
            Vector3 posterior = orientFrame.TransformVector(Vector3.up).normalized;
            Vector3 superior  = orientFrame.TransformVector(Vector3.forward).normalized;

            var dirs = new (Vector3 dir, string tag)[]
            {
                (left, "L"), (-left, "R"),
                (posterior, "P"), (-posterior, "A"),
                (superior, "S"), (-superior, "I"),
            };

            Vector3 camR = viewCamera.transform.right;
            Vector3 camU = viewCamera.transform.up;
            markRight.text  = BestTag(dirs, camR);
            markLeft.text   = BestTag(dirs, -camR);
            markTop.text    = BestTag(dirs, camU);
            markBottom.text = BestTag(dirs, -camU);
            SetMarkersActive(true);
        }

        // Pick the anatomical direction that projects most strongly along the given screen axis.
        private static string BestTag((Vector3 dir, string tag)[] dirs, Vector3 axis)
        {
            float best = float.NegativeInfinity;
            string tag = "";
            foreach (var d in dirs)
            {
                float dot = Vector3.Dot(d.dir, axis);
                if (dot > best) { best = dot; tag = d.tag; }
            }
            return tag;
        }

        private void SetMarkersActive(bool on)
        {
            markTop.gameObject.SetActive(on);
            markBottom.gameObject.SetActive(on);
            markLeft.gameObject.SetActive(on);
            markRight.gameObject.SetActive(on);
        }

        /// <summary>Create a bottom-anchored button with a centred text label and return that label.</summary>
        private static Text MakeButton(Transform parent, string name, Vector2 anchoredPos,
                                       Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var btnGO = new GameObject(name);
            btnGO.transform.SetParent(parent, false);
            var img = btnGO.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.55f);
            var btn = btnGO.AddComponent<Button>();
            var rt = btnGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var txtGO = new GameObject("Label");
            txtGO.transform.SetParent(btnGO.transform, false);
            var text = txtGO.AddComponent<Text>();
            text.font = AppFont.Get();
            text.fontSize = 48;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            var trt = txtGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            btn.onClick.AddListener(onClick);
            return text;
        }

        private void OnRecenter()
        {
            // 3D: reset the tilt neutral (mid-stack). AR: re-anchor the volume back in front of the user.
            EnsureMotionSlicer();
            if (motionSlicer != null)
                motionSlicer.Recenter();

            var arController = Object.FindObjectOfType<ARModeController>();
            if (arController != null)
                arController.Recenter();
        }

        private void OnCycleAxis()
        {
            EnsureMotionSlicer();
            if (motionSlicer != null)
                motionSlicer.CycleAxis();
            UpdateAxisLabel();
        }

        private void EnsureMotionSlicer()
        {
            if (motionSlicer == null)
                motionSlicer = Object.FindObjectOfType<MotionSlicer>();
        }

        private void UpdateAxisLabel()
        {
            if (axisLabel == null)
                return;
            EnsureMotionSlicer();
            // Only meaningful in 3D mode (MotionSlicer present) and Slice mode; hide otherwise.
            bool show = motionSlicer != null && controller != null && controller.Mode == SliceController.SliceMode.Slice;
            axisLabel.transform.parent.gameObject.SetActive(show);
            if (show)
                axisLabel.text = Loc.T("axis") + ": " + Loc.T("axis." + motionSlicer.Axis.ToString().ToLowerInvariant());
        }

        private void OnClick()
        {
            EnsureController();
            if (controller != null)
                controller.ToggleMode();
            UpdateLabel();
        }

        private void UpdateLabel()
        {
            EnsureController();
            if (label != null)
                label.text = controller != null
                    ? Loc.T("mode") + ": " + Loc.T("mode." + controller.Mode.ToString().ToLowerInvariant())
                    : Loc.T("mode");
            if (controller != null)
                shownMode = controller.Mode;
        }

        private void EnsureController()
        {
            if (controller == null)
                controller = Object.FindObjectOfType<SliceController>();
        }
    }
}
