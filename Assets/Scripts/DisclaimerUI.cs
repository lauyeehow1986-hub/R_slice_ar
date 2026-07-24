using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace SliceAR
{
    /// <summary>
    /// Shows the "not for diagnosis" disclaimer the app is required to state (CLAUDE.md / the original
    /// app's disclaimer): a full-screen modal on first launch that the user must acknowledge, plus a
    /// small persistent footer line that stays on screen the whole session.
    ///
    /// Self-contained — <see cref="SliceModeUI"/> adds it at runtime, so no scene wiring is needed. The
    /// modal shows once per app launch (tracked by <see cref="VolumeSession.DisclaimerAcknowledged"/>,
    /// which survives scene switches) rather than on every scene load.
    /// </summary>
    public class DisclaimerUI : MonoBehaviour
    {
        private const string Body =
            "Slice-AR is an educational and research tool for exploring 3D medical volume data.\n\n" +
            "It is NOT a medical device and must NOT be used for diagnosis, treatment, or any clinical " +
            "decision-making.\n\n" +
            "Bundled datasets are de-identified or synthetic. Only load data you are authorised to use — " +
            "never real patient studies without consent.";

        private const string Footer = "For education & research only — not for diagnostic use";

        private GameObject modal;

        private void Start()
        {
            EnsureEventSystem();
            BuildFooter();
            if (!VolumeSession.DisclaimerAcknowledged)
                BuildModal();
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }
        }

        private static Canvas NewCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;   // above the mode/LUT UI
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        // Small always-on line at the top-centre so the disclaimer is stated for the whole session.
        private void BuildFooter()
        {
            var canvas = NewCanvas("DisclaimerFooterCanvas", 500);
            var go = new GameObject("FooterText");
            go.transform.SetParent(canvas.transform, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 30;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            text.text = Footer;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -30f);
            rt.sizeDelta = new Vector2(1000f, 60f);
        }

        // Full-screen acknowledge-to-continue modal shown once per launch.
        private void BuildModal()
        {
            var canvas = NewCanvas("DisclaimerModalCanvas", 1000);
            modal = canvas.gameObject;

            // Dim backdrop that also blocks clicks to the UI beneath it.
            var bg = new GameObject("Backdrop");
            bg.transform.SetParent(canvas.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.92f);
            var bgrt = bg.GetComponent<RectTransform>();
            bgrt.anchorMin = Vector2.zero;
            bgrt.anchorMax = Vector2.one;
            bgrt.offsetMin = Vector2.zero;
            bgrt.offsetMax = Vector2.zero;

            AddText(bg.transform, "Title", "Slice-AR", 64, FontStyle.Bold, Color.white,
                new Vector2(0.5f, 0.78f), new Vector2(0f, 0f), new Vector2(960f, 120f));

            AddText(bg.transform, "Body", Body, 40, FontStyle.Normal, new Color(0.92f, 0.92f, 0.92f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(920f, 760f));

            var btnLabel = MakeButton(bg.transform, "AckButton", new Vector2(0.5f, 0.14f),
                new Vector2(640f, 150f), Acknowledge);
            btnLabel.text = "I Understand";
        }

        private void Acknowledge()
        {
            VolumeSession.DisclaimerAcknowledged = true;
            if (modal != null)
                Destroy(modal);
        }

        private static Text AddText(Transform parent, string name, string content, int size,
                                    FontStyle style, Color color, Vector2 anchor, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = color;
            text.text = content;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            return text;
        }

        private static Text MakeButton(Transform parent, string name, Vector2 anchor, Vector2 size,
                                       UnityEngine.Events.UnityAction onClick)
        {
            var btnGO = new GameObject(name);
            btnGO.transform.SetParent(parent, false);
            var img = btnGO.AddComponent<Image>();
            img.color = new Color(0.10f, 0.45f, 0.30f, 1f);
            var btn = btnGO.AddComponent<Button>();
            var rt = btnGO.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;

            var txtGO = new GameObject("Label");
            txtGO.transform.SetParent(btnGO.transform, false);
            var text = txtGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
    }
}
