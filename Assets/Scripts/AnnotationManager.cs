using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityVolumeRendering;

namespace SliceAR
{
    /// <summary>
    /// Annotation tool: labelled markers pinned to points in the volume, with per-dataset persistence and
    /// a two-marker distance measurement in millimetres.
    ///
    /// Placement is "tap on the slice": in Add mode a screen tap is raycast onto the plane currently shown
    /// (from <see cref="SliceController.TryGetActivePlane"/>) and, if it lands inside the volume, a marker
    /// is stored in the volume container's local space so it tracks the anatomy in both AR and 3D. Markers
    /// are drawn as screen-space dots + labels (no 3D materials, so nothing to strip). Two selected markers
    /// in Measure mode show a line and their separation in mm (from <see cref="VolumeSession.PhysicalSizeMm"/>).
    ///
    /// Self-contained — <see cref="SliceModeUI"/> adds it at runtime; it finds the volume lazily since the
    /// dataset loads asynchronously.
    /// </summary>
    public class AnnotationManager : MonoBehaviour
    {
        private enum Mode { View, Add, Measure }

        private Transform container;      // volume mesh container (local space markers live in)
        private SliceController controller;
        private Camera cam;
        private string datasetId;
        private AnnotationList data;
        private Mode mode = Mode.View;

        private readonly List<string> selected = new List<string>();

        // Per-marker screen-space views.
        private class View { public Image dot; public Text label; }
        private readonly Dictionary<string, View> views = new Dictionary<string, View>();

        private Canvas markerCanvas;   // raw-pixel canvas for dots/labels/measure line
        private RectTransform markerRoot;
        private Image measureLine;
        private Text measureLabel;

        private Text addBtn, measureBtn, deleteBtn;
        private InputField renameField;

        private void Start()
        {
            cam = Camera.main;
            BuildUI();
        }

        private void Update()
        {
            if (cam == null)
                cam = Camera.main;

            if (!EnsureVolume())
                return;

            HandleTap();
            UpdateMarkerViews();
            UpdateMeasure();
        }

        // Find the loaded volume (async) and, once present, load this dataset's annotations.
        private bool EnsureVolume()
        {
            if (container != null && controller != null)
                return true;

            var vol = Object.FindObjectOfType<VolumeRenderedObject>();
            if (vol == null || vol.meshRenderer == null)
                return false;
            container = vol.meshRenderer.transform;
            controller = Object.FindObjectOfType<SliceController>();

            datasetId = VolumeSession.DatasetId;
            data = AnnotationStore.Load(datasetId);
            RebuildAllViews();
            return controller != null;
        }

        // ---- Input ------------------------------------------------------------------------------------

        private void HandleTap()
        {
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame)
                return;

            Vector2 sp = pointer.position.ReadValue();
            if (IsOverUI())
                return;

            if (mode == Mode.Add)
            {
                TryPlaceMarker(sp);
                return;
            }

            // View / Measure: tap selects the nearest marker under the finger.
            string hit = MarkerAtScreen(sp);
            if (hit != null)
                ToggleSelect(hit);
        }

        private static bool IsOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void TryPlaceMarker(Vector2 screenPos)
        {
            if (controller == null || cam == null || container == null)
                return;
            if (!controller.TryGetActivePlane(out Vector3 planePoint, out Vector3 planeNormal))
                return;

            Ray ray = cam.ScreenPointToRay(screenPos);
            float denom = Vector3.Dot(planeNormal, ray.direction);
            if (Mathf.Abs(denom) < 1e-5f)
                return;
            float t = Vector3.Dot(planeNormal, planePoint - ray.origin) / denom;
            if (t <= 0f)
                return;

            Vector3 world = ray.origin + ray.direction * t;
            Vector3 local = container.InverseTransformPoint(world);
            // Reject taps that miss the volume (the mesh cube is [-0.5..0.5] on each axis).
            if (Mathf.Abs(local.x) > 0.52f || Mathf.Abs(local.y) > 0.52f || Mathf.Abs(local.z) > 0.52f)
                return;

            var a = new Annotation
            {
                id = System.Guid.NewGuid().ToString("N").Substring(0, 8),
                label = "M" + (data.items.Count + 1),
                localPos = local,
            };
            data.items.Add(a);
            AddView(a);
            SelectSingle(a.id);
            Save();
        }

        private string MarkerAtScreen(Vector2 sp)
        {
            string best = null;
            float bestDist = 70f;   // pixels
            foreach (var a in data.items)
            {
                Vector3 s = cam.WorldToScreenPoint(container.TransformPoint(a.localPos));
                if (s.z <= 0f)
                    continue;
                float d = Vector2.Distance(sp, new Vector2(s.x, s.y));
                if (d < bestDist)
                {
                    bestDist = d;
                    best = a.id;
                }
            }
            return best;
        }

        // ---- Selection --------------------------------------------------------------------------------

        private void ToggleSelect(string id)
        {
            if (selected.Contains(id))
                selected.Remove(id);
            else
            {
                selected.Add(id);
                // Measure needs at most two; View keeps a single selection.
                int max = mode == Mode.Measure ? 2 : 1;
                while (selected.Count > max)
                    selected.RemoveAt(0);
            }
            SyncRenameField();
        }

        private void SelectSingle(string id)
        {
            selected.Clear();
            selected.Add(id);
            SyncRenameField();
        }

        private Annotation Find(string id)
        {
            foreach (var a in data.items)
                if (a.id == id)
                    return a;
            return null;
        }

        private void Save() => AnnotationStore.Save(datasetId, data);

        // ---- Marker views -----------------------------------------------------------------------------

        private void RebuildAllViews()
        {
            foreach (var v in views.Values)
            {
                if (v.dot != null) Destroy(v.dot.gameObject);
                if (v.label != null) Destroy(v.label.gameObject);
            }
            views.Clear();
            if (data != null)
                foreach (var a in data.items)
                    AddView(a);
        }

        private void AddView(Annotation a)
        {
            var dotGO = new GameObject("Dot_" + a.id);
            dotGO.transform.SetParent(markerRoot, false);
            var dot = dotGO.AddComponent<Image>();
            dot.color = new Color(1f, 0.85f, 0.2f, 0.95f);
            dot.rectTransform.sizeDelta = new Vector2(30f, 30f);

            var labelGO = new GameObject("Label_" + a.id);
            labelGO.transform.SetParent(markerRoot, false);
            var label = labelGO.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 34;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = new Color(1f, 0.9f, 0.4f);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.rectTransform.sizeDelta = new Vector2(260f, 44f);
            label.rectTransform.pivot = new Vector2(0f, 0.5f);

            views[a.id] = new View { dot = dot, label = label };
        }

        private void UpdateMarkerViews()
        {
            foreach (var a in data.items)
            {
                if (!views.TryGetValue(a.id, out View v))
                    continue;
                Vector3 s = cam.WorldToScreenPoint(container.TransformPoint(a.localPos));
                bool visible = s.z > 0f;
                v.dot.gameObject.SetActive(visible);
                v.label.gameObject.SetActive(visible);
                if (!visible)
                    continue;

                bool sel = selected.Contains(a.id);
                v.dot.rectTransform.position = new Vector3(s.x, s.y, 0f);
                v.dot.rectTransform.sizeDelta = sel ? new Vector2(44f, 44f) : new Vector2(30f, 30f);
                v.dot.color = sel ? new Color(0.3f, 1f, 0.5f, 0.98f) : new Color(1f, 0.85f, 0.2f, 0.95f);
                v.label.rectTransform.position = new Vector3(s.x + 26f, s.y, 0f);
                v.label.text = a.label;
                v.label.color = sel ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.9f, 0.4f);
            }
        }

        private void UpdateMeasure()
        {
            bool show = mode == Mode.Measure && selected.Count == 2;
            measureLine.gameObject.SetActive(show);
            measureLabel.gameObject.SetActive(show);
            if (!show)
                return;

            Annotation a = Find(selected[0]);
            Annotation b = Find(selected[1]);
            if (a == null || b == null)
                return;

            Vector3 sa = cam.WorldToScreenPoint(container.TransformPoint(a.localPos));
            Vector3 sb = cam.WorldToScreenPoint(container.TransformPoint(b.localPos));
            if (sa.z <= 0f || sb.z <= 0f)
            {
                measureLine.gameObject.SetActive(false);
                measureLabel.gameObject.SetActive(false);
                return;
            }

            Vector2 pa = new Vector2(sa.x, sa.y);
            Vector2 pb = new Vector2(sb.x, sb.y);
            Vector2 mid = (pa + pb) * 0.5f;
            Vector2 dir = pb - pa;
            float len = dir.magnitude;

            measureLine.rectTransform.position = new Vector3(mid.x, mid.y, 0f);
            measureLine.rectTransform.sizeDelta = new Vector2(len, 4f);
            measureLine.rectTransform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

            Vector3 dLocal = a.localPos - b.localPos;
            Vector3 mm = Vector3.Scale(dLocal, VolumeSession.PhysicalSizeMm);
            measureLabel.rectTransform.position = new Vector3(mid.x, mid.y + 30f, 0f);
            measureLabel.text = mm.magnitude.ToString("0.0") + " mm";
        }

        // ---- UI buttons -------------------------------------------------------------------------------

        private void OnToggleAdd()
        {
            mode = mode == Mode.Add ? Mode.View : Mode.Add;
            if (mode != Mode.Measure)
                selected.RemoveRange(0, Mathf.Max(0, selected.Count - 1));
            RefreshButtons();
        }

        private void OnToggleMeasure()
        {
            mode = mode == Mode.Measure ? Mode.View : Mode.Measure;
            selected.Clear();
            SyncRenameField();
            RefreshButtons();
        }

        private void OnDelete()
        {
            if (selected.Count == 0)
                return;
            foreach (string id in selected)
            {
                data.items.RemoveAll(x => x.id == id);
                if (views.TryGetValue(id, out View v))
                {
                    if (v.dot != null) Destroy(v.dot.gameObject);
                    if (v.label != null) Destroy(v.label.gameObject);
                    views.Remove(id);
                }
            }
            selected.Clear();
            SyncRenameField();
            Save();
        }

        private void OnRename(string value)
        {
            if (selected.Count != 1)
                return;
            var a = Find(selected[0]);
            if (a == null)
                return;
            a.label = value;
            Save();
        }

        private void SyncRenameField()
        {
            if (renameField == null)
                return;
            bool one = selected.Count == 1;
            renameField.gameObject.SetActive(one);
            if (one)
            {
                var a = Find(selected[0]);
                renameField.SetTextWithoutNotify(a != null ? a.label : "");
            }
        }

        private void RefreshButtons()
        {
            if (addBtn != null)
                addBtn.transform.parent.GetComponent<Image>().color =
                    mode == Mode.Add ? new Color(0.15f, 0.5f, 0.25f, 0.95f) : PanelColor;
            if (measureBtn != null)
                measureBtn.transform.parent.GetComponent<Image>().color =
                    mode == Mode.Measure ? new Color(0.15f, 0.5f, 0.25f, 0.95f) : PanelColor;
        }

        private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.55f);

        private void BuildUI()
        {
            EnsureEventSystem();

            // Raw-pixel canvas for markers + measure line (position set directly in screen pixels).
            var mcGO = new GameObject("AnnotationMarkerCanvas");
            markerCanvas = mcGO.AddComponent<Canvas>();
            markerCanvas.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
            markerCanvas.sortingOrder = 290;
            mcGO.AddComponent<GraphicRaycaster>();
            markerRoot = new GameObject("Markers").AddComponent<RectTransform>();
            markerRoot.SetParent(mcGO.transform, false);
            markerRoot.anchorMin = Vector2.zero;
            markerRoot.anchorMax = Vector2.one;
            markerRoot.offsetMin = Vector2.zero;
            markerRoot.offsetMax = Vector2.zero;

            measureLine = new GameObject("MeasureLine").AddComponent<Image>();
            measureLine.transform.SetParent(markerRoot, false);
            measureLine.color = new Color(0.3f, 1f, 0.5f, 0.9f);
            measureLine.gameObject.SetActive(false);

            measureLabel = new GameObject("MeasureLabel").AddComponent<Text>();
            measureLabel.transform.SetParent(markerRoot, false);
            measureLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            measureLabel.fontSize = 40;
            measureLabel.fontStyle = FontStyle.Bold;
            measureLabel.alignment = TextAnchor.MiddleCenter;
            measureLabel.color = new Color(0.5f, 1f, 0.6f);
            measureLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            measureLabel.verticalOverflow = VerticalWrapMode.Overflow;
            measureLabel.rectTransform.sizeDelta = new Vector2(240f, 56f);
            measureLabel.gameObject.SetActive(false);

            // Scaled canvas for the control buttons (matches the rest of the UI sizing).
            var bcGO = new GameObject("AnnotationButtonCanvas");
            var canvas = bcGO.AddComponent<Canvas>();
            canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;
            var scaler = bcGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            bcGO.AddComponent<GraphicRaycaster>();

            // Bottom-left stack.
            addBtn = MakeButton(bcGO.transform, "AddMarkerBtn", new Vector2(40f, 40f),
                new Vector2(320f, 130f), OnToggleAdd);
            addBtn.text = "＋ Marker";
            measureBtn = MakeButton(bcGO.transform, "MeasureBtn", new Vector2(40f, 190f),
                new Vector2(320f, 130f), OnToggleMeasure);
            measureBtn.text = "Measure";
            deleteBtn = MakeButton(bcGO.transform, "DeleteBtn", new Vector2(40f, 340f),
                new Vector2(320f, 130f), OnDelete);
            deleteBtn.text = "Delete";

            renameField = MakeInputField(bcGO.transform, new Vector2(40f, 490f), new Vector2(320f, 110f));
            renameField.gameObject.SetActive(false);

            RefreshButtons();
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

        // Bottom-left anchored button; returns its label Text.
        private static Text MakeButton(Transform parent, string name, Vector2 anchoredPos,
                                       Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var btnGO = new GameObject(name);
            btnGO.transform.SetParent(parent, false);
            var img = btnGO.AddComponent<Image>();
            img.color = PanelColor;
            var btn = btnGO.AddComponent<Button>();
            var rt = btnGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var txtGO = new GameObject("Label");
            txtGO.transform.SetParent(btnGO.transform, false);
            var text = txtGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 42;
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

        private InputField MakeInputField(Transform parent, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject("RenameField");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.12f, 0.95f);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var text = textGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 40;
            text.color = Color.white;
            text.supportRichText = false;
            text.alignment = TextAnchor.MiddleLeft;
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(16f, 6f);
            textRT.offsetMax = new Vector2(-16f, -6f);

            var field = go.AddComponent<InputField>();
            field.textComponent = text;
            field.lineType = InputField.LineType.SingleLine;
            field.characterLimit = 24;
            field.onValueChanged.AddListener(OnRename);
            return field;
        }
    }
}
