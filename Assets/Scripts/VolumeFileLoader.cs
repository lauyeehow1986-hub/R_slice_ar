using System;
using System.Collections;
using System.IO;
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

        [Header("Behaviour")]
        public bool loadOnStart = true;

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
        /// Import the RAW dataset and create its rendered object, invoking <paramref name="onDone"/>
        /// with the result (or null on failure). Exposed so other modes (e.g. AR) can reuse it.
        /// </summary>
        public IEnumerator Load(Action<VolumeRenderedObject> onDone)
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

            var importer = new RawDatasetImporter(localPath, dimX, dimY, dimZ,
                                                  contentFormat, endianness, skipBytes);
            VolumeDataset dataset = importer.Import();
            if (dataset == null)
            {
                onDone(null);
                yield break;
            }

            dataset.RecalculateBounds();
            spawned = VolumeObjectFactory.CreateObject(dataset);
            spawned.transform.SetParent(transform, false);
            onDone(spawned);
        }
    }
}
