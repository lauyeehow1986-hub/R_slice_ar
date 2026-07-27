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
        private GameObject modal;
        private Text footerText, bodyText, ackLabel;   // kept so a language change can re-render them

        private void OnEnable()  { Loc.LanguageChanged += RefreshTexts; }
        private void OnDisable() { Loc.LanguageChanged -= RefreshTexts; }

        private void Start()
        {
            EnsureEventSystem();
            BuildFooter();
            if (!VolumeSession.DisclaimerAcknowledged)
                BuildModal();
        }

        // Re-render the disclaimer text (and re-pick a glyph-appropriate font) after a language change.
        private void RefreshTexts()
        {
            if (footerText != null) { footerText.font = AppFont.Get(); footerText.text = Loc.T("disclaimer.footer"); }
            if (bodyText != null)   { bodyText.font = AppFont.Get(); bodyText.text = Loc.T("disclaimer.body"); }
            if (ackLabel != null)   { ackLabel.font = AppFont.Get(); ackLabel.text = Loc.T("disclaimer.ack"); }
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
            // 30 px below the top edge is squarely behind the clock once the app draws edge-to-edge, and
            // this is the one line that legally has to stay readable — so it hangs off the safe area.
            go.transform.SetParent(SafeArea.RootUnder(canvas.transform), false);
            var text = go.AddComponent<Text>();
            text.font = AppFont.Get();
            text.fontSize = 30;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            text.text = Loc.T("disclaimer.footer");
            footerText = text;
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

            // The dim stays full-screen — a backdrop that stopped short of the system bars would leave two
            // bright strips across a modal. Only the content inside it is held to the safe area.
            var content = SafeArea.RootUnder(bg.transform);

            // Title is the brand name — not localized.
            AddText(content, "Title", "Slice-AR", 64, FontStyle.Bold, Color.white,
                new Vector2(0.5f, 0.78f), new Vector2(0f, 0f), new Vector2(960f, 120f));

            bodyText = AddText(content, "Body", Loc.T("disclaimer.body"), 40, FontStyle.Normal,
                new Color(0.92f, 0.92f, 0.92f),
                new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(920f, 760f));

            ackLabel = MakeButton(content, "AckButton", new Vector2(0.5f, 0.14f),
                new Vector2(640f, 150f), Acknowledge);
            ackLabel.text = Loc.T("disclaimer.ack");
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
            text.font = AppFont.Get();
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
    }
}
