using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityVolumeRendering;

namespace SliceAR
{
    /// <summary>
    /// Runtime UI for importing a dataset from device storage. Offers an image-sequence import
    /// (a folder/selection of PNG/JPG slices) and a headerless-RAW import with a dimensions +
    /// format + voxel-size form (this form is also the "editable voxel size" control). Picking a
    /// file uses <see cref="NativeFilePicker"/>; the chosen parameters are stored in
    /// <see cref="VolumeImportRequest"/> and the scene is reloaded so the volume is rebuilt cleanly.
    /// </summary>
    public class ImportUI : MonoBehaviour
    {
        private static readonly DataContentFormat[] Formats =
        {
            DataContentFormat.Uint8, DataContentFormat.Int8,
            DataContentFormat.Uint16, DataContentFormat.Int16,
            DataContentFormat.Uint32, DataContentFormat.Int32
        };

        private Font font;
        private GameObject panel;

        private InputField voxX, voxY, voxZ;
        private InputField dimX, dimY, dimZ;
        private int formatIndex = 2;                       // Uint16
        private Endianness endian = Endianness.LittleEndian;
        private VolumeFileLoader.TFPreset tfPreset = VolumeFileLoader.TFPreset.MRI;
        private Button formatBtn, endianBtn, tfBtn;
        private Text statusText;

        private void Start()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            BuildUI();
            SetPanelVisible(false);
        }

        private void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("ImportCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            canvasGO.AddComponent<GraphicRaycaster>();

            // "Import" launcher button (top-left, clear of the bottom-centre Mode button).
            var openBtn = CreateButton(canvasGO.transform, "Import", new Vector2(0f, 1f),
                new Vector2(40f, -40f), new Vector2(300f, 130f), () => SetPanelVisible(true));
            var obr = openBtn.GetComponent<RectTransform>();
            obr.pivot = new Vector2(0f, 1f);

            // Modal-ish panel.
            panel = new GameObject("ImportPanel");
            panel.transform.SetParent(canvasGO.transform, false);
            var pImg = panel.AddComponent<Image>();
            pImg.color = new Color(0.05f, 0.05f, 0.07f, 0.96f);
            var prt = panel.GetComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(900f, 1480f);

            float y = 640f;
            CreateLabel(panel.transform, "Import dataset", 44, new Vector2(0f, y), 820f, TextAnchor.MiddleCenter);
            y -= 100f;

            // Shared voxel size row.
            CreateLabel(panel.transform, "Voxel size (mm)", 32, new Vector2(-260f, y), 340f, TextAnchor.MiddleLeft);
            voxX = CreateInput(panel.transform, "1", new Vector2(70f, y), true);
            voxY = CreateInput(panel.transform, "1", new Vector2(230f, y), true);
            voxZ = CreateInput(panel.transform, "1", new Vector2(390f, y), true);
            y -= 90f;

            // Transfer function cycle.
            CreateLabel(panel.transform, "Transfer func", 32, new Vector2(-260f, y), 340f, TextAnchor.MiddleLeft);
            tfBtn = CreateButton(panel.transform, TfLabel(), new Vector2(0.5f, 0.5f), new Vector2(150f, y),
                new Vector2(300f, 80f), CycleTf);
            y -= 120f;

            CreateLabel(panel.transform, "Image sequence — .zip of PNG/JPG slices", 28, new Vector2(0f, y), 820f, TextAnchor.MiddleCenter);
            y -= 80f;
            CreateButton(panel.transform, "Pick image stack (.zip)…", new Vector2(0.5f, 0.5f), new Vector2(0f, y),
                new Vector2(700f, 100f), PickImageSequence);
            y -= 120f;

            CreateLabel(panel.transform, "— or headerless RAW —", 30, new Vector2(0f, y), 820f, TextAnchor.MiddleCenter);
            y -= 80f;

            CreateLabel(panel.transform, "Dimensions", 32, new Vector2(-260f, y), 340f, TextAnchor.MiddleLeft);
            dimX = CreateInput(panel.transform, "256", new Vector2(70f, y), false);
            dimY = CreateInput(panel.transform, "256", new Vector2(230f, y), false);
            dimZ = CreateInput(panel.transform, "130", new Vector2(390f, y), false);
            y -= 90f;

            CreateLabel(panel.transform, "Data type", 32, new Vector2(-260f, y), 300f, TextAnchor.MiddleLeft);
            formatBtn = CreateButton(panel.transform, Formats[formatIndex].ToString(), new Vector2(0.5f, 0.5f),
                new Vector2(60f, y), new Vector2(240f, 80f), CycleFormat);
            endianBtn = CreateButton(panel.transform, EndianLabel(), new Vector2(0.5f, 0.5f),
                new Vector2(330f, y), new Vector2(160f, 80f), CycleEndian);
            y -= 100f;
            CreateButton(panel.transform, "Pick RAW file…", new Vector2(0.5f, 0.5f), new Vector2(0f, y),
                new Vector2(700f, 100f), PickRaw);
            y -= 120f;

            CreateLabel(panel.transform, "— or DICOM (.zip, uncompressed) —", 28, new Vector2(0f, y), 820f, TextAnchor.MiddleCenter);
            y -= 80f;
            CreateButton(panel.transform, "Pick DICOM (.zip)…", new Vector2(0.5f, 0.5f), new Vector2(0f, y),
                new Vector2(700f, 100f), PickDicom);
            y -= 120f;

            statusText = CreateLabel(panel.transform, "", 26, new Vector2(0f, y), 820f, TextAnchor.MiddleCenter);
            y -= 70f;
            CreateButton(panel.transform, "Cancel", new Vector2(0.5f, 0.5f), new Vector2(0f, y),
                new Vector2(300f, 90f), () => SetPanelVisible(false));
        }

        // ---- actions ------------------------------------------------------------------------

        private void PickImageSequence()
        {
            if (NativeFilePicker.IsFilePickerBusy())
                return;
            SetStatus("Opening picker…");
            NativeFilePicker.PickFile(path =>
            {
                if (string.IsNullOrEmpty(path))
                {
                    SetStatus("Cancelled.");
                    return;
                }
                VolumeImportRequest.Clear();
                VolumeImportRequest.kind = VolumeImportRequest.Kind.ImageSequence;
                VolumeImportRequest.imageZipPath = path;
                VolumeImportRequest.voxelSizeMm = ReadVoxel();
                VolumeImportRequest.tfPreset = tfPreset;
                ReloadScene();
            });
        }

        private void PickRaw()
        {
            if (NativeFilePicker.IsFilePickerBusy())
                return;
            SetStatus("Opening picker…");
            NativeFilePicker.PickFile(path =>
            {
                if (string.IsNullOrEmpty(path))
                {
                    SetStatus("Cancelled.");
                    return;
                }
                VolumeImportRequest.Clear();
                VolumeImportRequest.kind = VolumeImportRequest.Kind.Raw;
                VolumeImportRequest.rawPath = path;
                VolumeImportRequest.dimX = ParseInt(dimX, 256);
                VolumeImportRequest.dimY = ParseInt(dimY, 256);
                VolumeImportRequest.dimZ = ParseInt(dimZ, 1);
                VolumeImportRequest.contentFormat = Formats[formatIndex];
                VolumeImportRequest.endianness = endian;
                VolumeImportRequest.skipBytes = 0;
                VolumeImportRequest.voxelSizeMm = ReadVoxel();
                VolumeImportRequest.tfPreset = tfPreset;
                ReloadScene();
            });
        }

        private void PickDicom()
        {
            if (NativeFilePicker.IsFilePickerBusy())
                return;
            SetStatus("Opening picker…");
            NativeFilePicker.PickFile(path =>
            {
                if (string.IsNullOrEmpty(path))
                {
                    SetStatus("Cancelled.");
                    return;
                }
                VolumeImportRequest.Clear();
                VolumeImportRequest.kind = VolumeImportRequest.Kind.Dicom;
                VolumeImportRequest.dicomZipPath = path;
                // DICOM provides its own voxel spacing; TF still applies (CT is typical).
                VolumeImportRequest.tfPreset = tfPreset;
                ReloadScene();
            });
        }

        private void ReloadScene()
        {
            SetStatus("Loading…");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private Vector3 ReadVoxel()
        {
            return new Vector3(ParseFloat(voxX, 1f), ParseFloat(voxY, 1f), ParseFloat(voxZ, 1f));
        }

        private void CycleFormat()
        {
            formatIndex = (formatIndex + 1) % Formats.Length;
            SetButtonText(formatBtn, Formats[formatIndex].ToString());
        }

        private void CycleEndian()
        {
            endian = endian == Endianness.LittleEndian ? Endianness.BigEndian : Endianness.LittleEndian;
            SetButtonText(endianBtn, EndianLabel());
        }

        private void CycleTf()
        {
            tfPreset = (VolumeFileLoader.TFPreset)(((int)tfPreset + 1) % 3);
            SetButtonText(tfBtn, TfLabel());
        }

        private string EndianLabel() => endian == Endianness.LittleEndian ? "LE" : "BE";
        private string TfLabel() => tfPreset.ToString();

        private void SetPanelVisible(bool visible)
        {
            if (panel != null)
                panel.SetActive(visible);
        }

        private void SetStatus(string s)
        {
            if (statusText != null)
                statusText.text = s;
        }

        // ---- widget helpers -----------------------------------------------------------------

        private Button CreateButton(Transform parent, string label, Vector2 anchor, Vector2 pos,
                                    Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.16f, 0.20f, 0.28f, 1f);
            var btn = go.AddComponent<Button>();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var txtGO = new GameObject("Text");
            txtGO.transform.SetParent(go.transform, false);
            var t = txtGO.AddComponent<Text>();
            t.font = font;
            t.fontSize = 30;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = label;
            var trt = txtGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;

            btn.onClick.AddListener(onClick);
            return btn;
        }

        private void SetButtonText(Button btn, string s)
        {
            var t = btn.GetComponentInChildren<Text>();
            if (t != null) t.text = s;
        }

        private Text CreateLabel(Transform parent, string text, int size, Vector2 pos, float width, TextAnchor anchor)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = Color.white;
            t.text = text;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, size + 20f);
            return t;
        }

        private InputField CreateInput(Transform parent, string initial, Vector2 pos, bool decimalNumber)
        {
            var go = new GameObject("Input");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.85f, 0.86f, 0.9f, 1f);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(130f, 80f);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var t = textGO.AddComponent<Text>();
            t.font = font;
            t.fontSize = 32;
            t.color = Color.black;
            t.alignment = TextAnchor.MiddleCenter;
            t.supportRichText = false;
            var trt = textGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(10f, 6f);
            trt.offsetMax = new Vector2(-10f, -6f);

            var input = go.AddComponent<InputField>();
            input.targetGraphic = img;
            input.textComponent = t;
            input.contentType = decimalNumber ? InputField.ContentType.DecimalNumber
                                              : InputField.ContentType.IntegerNumber;
            input.text = initial;
            return input;
        }

        private int ParseInt(InputField f, int fallback)
        {
            return f != null && int.TryParse(f.text, out int v) && v > 0 ? v : fallback;
        }

        private float ParseFloat(InputField f, float fallback)
        {
            return f != null && float.TryParse(f.text, out float v) && v > 0f ? v : fallback;
        }
    }
}
