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
                var slicer = GetComponent<MotionSlicer>();
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
                    dataset = ImportDicom(ExtractZip(VolumeImportRequest.dicomZipPath, "import_dicom"));
                    if (dataset != null)
                    {
                        // The DICOM importer can encode patient L/R handedness as a NEGATIVE scale
                        // axis. That mirror propagates to the volume transform and flips the child
                        // slicing plane's basis, so the slice flings off to a corner as the device
                        // rotates. Keep the (anisotropic) magnitudes, drop the mirror.
                        dataset.scale = new Vector3(
                            Mathf.Abs(dataset.scale.x),
                            Mathf.Abs(dataset.scale.y),
                            Mathf.Abs(dataset.scale.z));
                        importerProvidedScale = true;
                    }
                }
                VolumeImportRequest.Clear();
            }
            else
            {
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
            spawned = VolumeObjectFactory.CreateObject(dataset);
            spawned.transform.SetParent(transform, false);

            ApplyTransferFunction(spawned);

            onDone(spawned);
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
            if (transferFunctionPreset == TFPreset.PluginDefault)
                return;

            var tf = ScriptableObject.CreateInstance<UnityVolumeRendering.TransferFunction>();
            tf.colourControlPoints.Clear();
            tf.alphaControlPoints.Clear();

            if (transferFunctionPreset == TFPreset.CT)
            {
                // Placed from a CT histogram: air near 0, a soft-tissue spike ~0.32, thin bone tail >0.75.
                tf.colourControlPoints.Add(new TFColourControlPoint(0.00f, new Color(0f, 0f, 0f)));
                tf.colourControlPoints.Add(new TFColourControlPoint(0.11f, new Color(0f, 0f, 0f)));        // air → black
                tf.colourControlPoints.Add(new TFColourControlPoint(0.20f, new Color(0.18f, 0.16f, 0.15f)));// skin/edge
                tf.colourControlPoints.Add(new TFColourControlPoint(0.32f, new Color(0.42f, 0.38f, 0.34f)));// soft tissue → grey
                tf.colourControlPoints.Add(new TFColourControlPoint(0.50f, new Color(0.62f, 0.57f, 0.50f)));
                tf.colourControlPoints.Add(new TFColourControlPoint(0.72f, new Color(0.88f, 0.84f, 0.76f)));// cancellous bone
                tf.colourControlPoints.Add(new TFColourControlPoint(0.85f, new Color(1f, 0.98f, 0.93f)));  // cortical bone
                tf.colourControlPoints.Add(new TFColourControlPoint(1.00f, new Color(1f, 1f, 1f)));

                tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.00f, 0.00f));
                tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.11f, 0.00f));   // air transparent
                tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.22f, 0.02f));
                tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.32f, 0.06f));   // soft tissue faint → see-through
                tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.50f, 0.15f));
                tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.70f, 0.55f));   // bone becomes solid
                tf.alphaControlPoints.Add(new TFAlphaControlPoint(1.00f, 0.95f));
            }
            else // MRI
            {
                // Placed from an MRI histogram: ~53% air at 0, all tissue packed into ~0.05..0.55.
                // Window that band across black→white (MRI has no bright "bone" tail; bone is dark).
                tf.colourControlPoints.Add(new TFColourControlPoint(0.00f, new Color(0f, 0f, 0f)));
                tf.colourControlPoints.Add(new TFColourControlPoint(0.04f, new Color(0f, 0f, 0f)));         // background → black
                tf.colourControlPoints.Add(new TFColourControlPoint(0.12f, new Color(0.14f, 0.14f, 0.15f)));// low signal
                tf.colourControlPoints.Add(new TFColourControlPoint(0.24f, new Color(0.48f, 0.48f, 0.50f)));// brain tissue → grey
                tf.colourControlPoints.Add(new TFColourControlPoint(0.36f, new Color(0.74f, 0.74f, 0.76f)));// brighter tissue
                tf.colourControlPoints.Add(new TFColourControlPoint(0.50f, new Color(0.95f, 0.95f, 0.97f)));// fat/scalp → near white
                tf.colourControlPoints.Add(new TFColourControlPoint(0.62f, new Color(1f, 1f, 1f)));
                tf.colourControlPoints.Add(new TFColourControlPoint(1.00f, new Color(1f, 1f, 1f)));

                tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.00f, 0.00f));
                tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.05f, 0.00f));   // air transparent
                tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.14f, 0.06f));   // faint tissue
                tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.25f, 0.22f));   // brain tissue
                tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.40f, 0.55f));
                tf.alphaControlPoints.Add(new TFAlphaControlPoint(0.55f, 0.85f));
                tf.alphaControlPoints.Add(new TFAlphaControlPoint(1.00f, 0.95f));
            }

            tf.GenerateTexture();
            volume.SetTransferFunction(tf);
        }
    }
}
