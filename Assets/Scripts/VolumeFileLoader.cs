using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityVolumeRendering;

namespace SliceAR
{
    /// <summary>
    /// Loads a bundled RAW volume from <c>StreamingAssets</c> and renders it via
    /// UnityVolumeRendering, then hands it to the 3D-mode <see cref="MotionSlicer"/>.
    ///
    /// On Android the file lives compressed inside the APK (a <c>jar:</c> URL), so
    /// <see cref="System.IO.File"/> cannot read it directly; it is first copied to
    /// <see cref="Application.persistentDataPath"/> with <see cref="UnityWebRequest"/> and the
    /// file-based <see cref="RawDatasetImporter"/> reads that copy. In the editor / on desktop the
    /// StreamingAssets path is already a real file, so it is imported in place.
    ///
    /// Falls back to <see cref="SampleVolumeGenerator"/>'s synthetic sphere if the dataset is
    /// missing or unreadable, so the app still works without a bundled volume.
    ///
    /// AR mode reuses <see cref="Load"/> directly (with <see cref="loadOnStart"/> off) and does its
    /// own anchoring, so no <see cref="MotionSlicer"/> is required on the object.
    /// </summary>
    public class VolumeFileLoader : MonoBehaviour
    {
        [Header("Dataset (in Assets/StreamingAssets)")]
        [Tooltip("RAW file name inside StreamingAssets.")]
        public string fileName = "CThead_256x256x113_int16_be.raw";
        public int dimX = 256;
        public int dimY = 256;
        public int dimZ = 113;
        public DataContentFormat contentFormat = DataContentFormat.Uint16;
        public Endianness endianness = Endianness.BigEndian;
        [Tooltip("Bytes of header to skip before the voxel data (0 for a headerless RAW).")]
        public int skipBytes = 0;

        [Tooltip("Physical voxel size in millimetres per data axis (x,y,z). Used to render the volume " +
                 "at its true proportions instead of a cube. MRHead is anisotropic: 1,1,1.3.")]
        public Vector3 voxelSizeMm = new Vector3(1f, 1f, 1.3f);

        [Header("Behaviour")]
        public bool loadOnStart = true;

        /// <summary>Which built-in transfer-function preset to apply after loading.</summary>
        public enum TFPreset { PluginDefault, CT, MRI }

        [Tooltip("Transfer function to apply after loading. CT: air→black, soft tissue→grey, " +
                 "bone→white. MRI: window the (bone-dark) intensity band across black→white. " +
                 "PluginDefault leaves the renderer's default (tends to blow out to white).")]
        public TFPreset transferFunctionPreset = TFPreset.MRI;

        [Tooltip("If the dataset is missing/unreadable, generate the synthetic sphere instead " +
                 "(requires a SampleVolumeGenerator on the same object).")]
        public bool fallbackToSynthetic = true;

        private VolumeRenderedObject spawned;

        private IEnumerator Start()
        {
            if (loadOnStart)
                yield return LoadAndAttach();
        }

        /// <summary>Load the dataset and attach the 3D motion slicer to it.</summary>
        public IEnumerator LoadAndAttach()
        {
            VolumeRenderedObject volume = null;
            yield return Load(v => volume = v);

            if (volume == null && fallbackToSynthetic)
            {
                var gen = GetComponent<SampleVolumeGenerator>();
                if (gen != null)
                {
                    Debug.LogWarning("VolumeFileLoader: dataset unavailable — using synthetic volume.");
                    volume = gen.Generate();
                }
            }

            if (volume != null)
            {
                // A scene-serialized MotionSlicer component can fail to instantiate in an IL2CPP build
                // even though it deserialises fine in the editor. If it is missing, recreate it at
                // runtime (the type still lives in the player assembly) so 3D slicing works regardless.
                var slicer = GetComponent<MotionSlicer>();
                if (slicer == null)
                    slicer = gameObject.AddComponent<MotionSlicer>();
                if (slicer != null)
                    slicer.Attach(volume);
            }
            else
            {
                Debug.LogError("VolumeFileLoader: no volume could be loaded.");
            }
        }

        /// <summary>
        /// Produce a rendered volume and invoke <paramref name="onDone"/> with it (or null on failure).
        /// If a runtime <see cref="VolumeImportRequest"/> is pending it is consumed here (a user import);
        /// otherwise the bundled StreamingAssets sample is loaded. Exposed so other modes (e.g. AR) reuse it.
        /// </summary>
        public IEnumerator Load(Action<VolumeRenderedObject> onDone)
        {
            VolumeDataset dataset = null;
            // DICOM carries real pixel spacing and sets its own dataset.scale; don't override it.
            bool importerProvidedScale = false;

            if (VolumeImportRequest.HasPending)
            {
                // A user-initiated import (survives the scene reload); take its parameters, then clear.
                var kind = VolumeImportRequest.kind;
                voxelSizeMm = VolumeImportRequest.voxelSizeMm;
                transferFunctionPreset = VolumeImportRequest.tfPreset;
                VolumeSession.IsDicomOriented = kind == VolumeImportRequest.Kind.Dicom;
                // DICOM (and, as a best guess, imported RAW/image sequences) follow the importer's
                // +X=Left, +Y=Posterior, +Z=Superior convention.
                VolumeSession.ResetAxes();

                if (kind == VolumeImportRequest.Kind.ImageSequence)
                {
                    dataset = !string.IsNullOrEmpty(VolumeImportRequest.imageZipPath)
                        ? ImportImageSequence(ExtractZip(VolumeImportRequest.imageZipPath, "import_stack"))
                        : ImportImageSequence(VolumeImportRequest.imagePaths);
                }
                else if (kind == VolumeImportRequest.Kind.Raw)
                {
                    dataset = ImportRaw(VolumeImportRequest.rawPath,
                        VolumeImportRequest.dimX, VolumeImportRequest.dimY, VolumeImportRequest.dimZ,
                        VolumeImportRequest.contentFormat, VolumeImportRequest.endianness,
                        VolumeImportRequest.skipBytes);
                }
                else if (kind == VolumeImportRequest.Kind.Dicom)
                {
                    // Keep the importer's scale/rotation exactly: its negative-X scale is the
                    // deliberate DICOM-LPS -> Unity handedness conversion that preserves radiological
                    // left/right. (Do NOT abs() it — that mirrors the anatomy. Centring in 3D mode is
                    // handled by SliceController.ShowCtSlice via the volume bounds centre, independent
                    // of the scale sign.)
                    dataset = ImportDicom(ExtractZip(VolumeImportRequest.dicomZipPath, "import_dicom"));
                    importerProvidedScale = dataset != null;
                }
                VolumeImportRequest.Clear();
            }
            else
            {
                VolumeSession.IsDicomOriented = false;   // bundled RAW has no orientation metadata
                // The bundled MRHead is a sagittal T1 acquisition: its grid is dataset X=anterior/
                // posterior, Y=superior/inferior, Z=left/right — not the importer convention. Map the
                // patient axes so the CT-viewer's Axial/Coronal/Sagittal planes come out upright.
                VolumeSession.AxisLeft      = new Vector3(0f, 0f, 1f);   //  +Z → Left
                VolumeSession.AxisPosterior = new Vector3(-1f, 0f, 0f);  //  -X → Posterior
                VolumeSession.AxisSuperior  = new Vector3(0f, -1f, 0f);  //  -Y → Superior
                yield return LoadBundledRaw(d => dataset = d);
            }

            if (dataset == null)
            {
                onDone(null);
                yield break;
            }

            dataset.RecalculateBounds();
            if (!importerProvidedScale)
                ApplyVoxelSize(dataset);

            // Build the rendered object, then apply the transfer function. The TF is cosmetic (the
            // volume renders fine without it), so a TF failure must NOT abort the load — otherwise
            // onDone never fires and the slicer never attaches. Both steps are guarded so onDone always
            // runs with whatever we managed to build.
            VolumeRenderedObject obj = null;
            try
            {
                obj = VolumeObjectFactory.CreateObject(dataset);
                obj.transform.SetParent(transform, false);
            }
            catch (Exception e)
            {
                Debug.LogError("VolumeFileLoader: CreateObject failed: " + e);
            }

            if (obj != null)
            {
                try { ApplyTransferFunction(obj); }
                catch (Exception e) { Debug.LogError("VolumeFileLoader: ApplyTransferFunction failed: " + e); }
            }

            spawned = obj;
            onDone(obj);
        }

        /// <summary>Load the bundled headerless RAW from StreamingAssets (copying out of the APK on Android).</summary>
        private IEnumerator LoadBundledRaw(Action<VolumeDataset> onDone)
        {
            string srcPath = Path.Combine(Application.streamingAssetsPath, fileName);
            string localPath = srcPath;

            // On platforms where StreamingAssets is served as a URL (Android APK), copy to a
            // real file first so the file-based importer can open it.
            if (srcPath.Contains("://"))
            {
                localPath = Path.Combine(Application.persistentDataPath, fileName);
                if (!File.Exists(localPath))
                {
                    using (var req = UnityWebRequest.Get(srcPath))
                    {
                        yield return req.SendWebRequest();
                        if (req.result != UnityWebRequest.Result.Success)
                        {
                            Debug.LogError("VolumeFileLoader: could not read StreamingAssets file '" +
                                           fileName + "': " + req.error);
                            onDone(null);
                            yield break;
                        }
                        File.WriteAllBytes(localPath, req.downloadHandler.data);
                    }
                }
            }

            if (!File.Exists(localPath))
            {
                Debug.LogError("VolumeFileLoader: dataset not found at " + localPath);
                onDone(null);
                yield break;
            }

            onDone(ImportRaw(localPath, dimX, dimY, dimZ, contentFormat, endianness, skipBytes));
        }

        private VolumeDataset ImportRaw(string path, int dx, int dy, int dz,
                                        DataContentFormat format, Endianness endian, int skip)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogError("VolumeFileLoader: RAW file not found: " + path);
                return null;
            }
            return new RawDatasetImporter(path, dx, dy, dz, format, endian, skip).Import();
        }

        /// <summary>Extract a .zip to a fresh temp subfolder and return the extracted file paths.</summary>
        private string[] ExtractZip(string zipPath, string subDir)
        {
            if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath))
            {
                Debug.LogError("VolumeFileLoader: zip not found: " + zipPath);
                return null;
            }
            string dir = Path.Combine(Application.persistentDataPath, subDir);
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
                Directory.CreateDirectory(dir);

                using (var fs = File.OpenRead(zipPath))
                using (var zip = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Read))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name))   // directory entry
                            continue;
                        string outPath = Path.Combine(dir, entry.Name);
                        using (var es = entry.Open())
                        using (var os = File.Create(outPath))
                            es.CopyTo(os);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("VolumeFileLoader: failed to extract zip: " + e.Message);
                return null;
            }
            return Directory.GetFiles(dir);
        }

        private VolumeDataset ImportImageSequence(string[] paths)
        {
            if (paths == null || paths.Length == 0)
            {
                Debug.LogError("VolumeFileLoader: no images to import.");
                return null;
            }
            // Slice order matters: sort by file name (e.g. slice001, slice002, ...).
            var ordered = paths.OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase);

            var importer = new ImageSequenceImporter();
            var settings = new ImageSequenceImportSettings();
            var series = importer.LoadSeries(ordered, settings).FirstOrDefault();
            if (series == null)
            {
                Debug.LogError("VolumeFileLoader: no supported image series found.");
                return null;
            }
            return importer.ImportSeries(series, settings);
        }

        /// <summary>
        /// Import an (uncompressed) DICOM series from a set of extracted files using UnityVolumeRendering's
        /// managed openDICOM reader (no native libraries — works under IL2CPP/ARM64). JPEG-compressed
        /// DICOM is not supported by this reader. Voxel spacing comes from the DICOM metadata.
        /// </summary>
        private VolumeDataset ImportDicom(string[] paths)
        {
            if (paths == null || paths.Length == 0)
            {
                Debug.LogError("VolumeFileLoader: no DICOM files to import.");
                return null;
            }

            var importer = new DICOMImporter();
            var settings = new ImageSequenceImportSettings();
            var series = importer.LoadSeries(paths, settings).FirstOrDefault();
            if (series == null)
            {
                Debug.LogError("VolumeFileLoader: no DICOM series found in the archive.");
                return null;
            }
            return importer.ImportSeries(series, settings);
        }

        /// <summary>
        /// Set the dataset's per-axis scale from the physical voxel size so the volume renders at its
        /// true proportions. Without this a 256×256×130 volume is drawn as a cube, stretching the short
        /// axis. Normalised so the largest physical extent = 1.
        /// </summary>
        private void ApplyVoxelSize(VolumeDataset dataset)
        {
            Vector3 extents = new Vector3(
                dataset.dimX * Mathf.Max(voxelSizeMm.x, 1e-4f),
                dataset.dimY * Mathf.Max(voxelSizeMm.y, 1e-4f),
                dataset.dimZ * Mathf.Max(voxelSizeMm.z, 1e-4f));
            float maxExt = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
            if (maxExt <= 0f)
                return;
            dataset.scale = extents / maxExt;
        }

        /// <summary>
        /// Replace the plugin's default transfer function with a preset tuned for the data type.
        /// Control points are in normalised intensity [0..1]. Colour drives the flat slice view (its
        /// shader forces alpha=1 and uses only RGB); alpha keeps tissue translucent in the 3D
        /// direct-volume render so denser structure shows through.
        /// </summary>
        private void ApplyTransferFunction(VolumeRenderedObject volume)
        {
            // Record the window so the runtime LUT picker can rebuild the transfer function from it.
            VolumeSession.WindowPreset = transferFunctionPreset;

            // With no window preset and the plain Grayscale LUT there is nothing to add over the
            // renderer's own default, so leave it untouched (historical PluginDefault behaviour).
            if (transferFunctionPreset == TFPreset.PluginDefault && VolumeSession.ColorLUT == ColorLUT.Grayscale)
                return;

            // The window preset sets the luminance/opacity ramp; the colour LUT recolours it. Both the
            // CT/MRI ramps and the palettes live in TransferFunctions so the picker reuses them.
            volume.SetTransferFunction(TransferFunctions.Build(transferFunctionPreset, VolumeSession.ColorLUT));
        }
    }
}
