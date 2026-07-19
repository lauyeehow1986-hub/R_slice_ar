using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace SliceAR
{
    /// <summary>
    /// Builds a screen-space button at runtime that toggles the <see cref="SliceController"/>
    /// between Clip and Slice modes. Self-contained so no manual UI wiring is needed in a scene.
    /// </summary>
    public class SliceModeUI : MonoBehaviour
    {
        private SliceController controller;
        private Text label;

        private void Start()
        {
            BuildUI();
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
                es.AddComponent<StandaloneInputModule>();
            }

            var btnGO = new GameObject("ModeButton");
            btnGO.transform.SetParent(canvasGO.transform, false);
            var img = btnGO.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.55f);
            var btn = btnGO.AddComponent<Button>();
            var rt = btnGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 140f);
            rt.sizeDelta = new Vector2(560f, 160f);

            var txtGO = new GameObject("Label");
            txtGO.transform.SetParent(btnGO.transform, false);
            label = txtGO.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 48;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            var trt = txtGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            btn.onClick.AddListener(OnClick);
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
                label.text = controller != null ? "Mode: " + controller.Mode : "Mode";
        }

        private void EnsureController()
        {
            if (controller == null)
                controller = Object.FindObjectOfType<SliceController>();
        }
    }
}
