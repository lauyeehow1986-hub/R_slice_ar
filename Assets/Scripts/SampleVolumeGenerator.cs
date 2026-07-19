using UnityEngine;
using UnityVolumeRendering;

namespace SliceAR
{
    /// <summary>
    /// Phase 1 placeholder: builds a synthetic test volume at runtime and renders it via
    /// UnityVolumeRendering. Replaced by real dataset loading (bundled / DICOM) in later phases.
    /// </summary>
    public class SampleVolumeGenerator : MonoBehaviour
    {
        [Range(16, 128)] public int resolution = 64;
        public Vector3 rotationEuler = new Vector3(15f, 25f, 0f);

        [Tooltip("Generate the volume automatically on Start (3D mode). Disable when an external " +
                 "controller drives generation, e.g. the AR mode controller.")]
        public bool generateOnStart = true;

        private VolumeRenderedObject spawned;

        private void Start()
        {
            if (!generateOnStart)
                return;

            var volume = Generate();
            var slicer = GetComponent<MotionSlicer>();
            if (slicer != null)
                slicer.Attach(volume);
        }

        public VolumeRenderedObject Generate()
        {
            int n = resolution;
            var ds = ScriptableObject.CreateInstance<VolumeDataset>();
            ds.datasetName = "SyntheticSphere";
            ds.dimX = ds.dimY = ds.dimZ = n;
            ds.data = new float[n * n * n];

            for (int z = 0; z < n; z++)
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float fx = (x / (float)(n - 1)) * 2f - 1f;
                float fy = (y / (float)(n - 1)) * 2f - 1f;
                float fz = (z / (float)(n - 1)) * 2f - 1f;
                float r = Mathf.Sqrt(fx * fx + fy * fy + fz * fz);
                float outer = Mathf.Clamp01(1f - r * 1.1f);
                float cx = fx - 0.25f, cy = fy - 0.1f, cz = fz;
                float core = Mathf.Clamp01(1f - Mathf.Sqrt(cx * cx + cy * cy + cz * cz) * 3f);
                ds.data[x + y * n + z * n * n] = Mathf.Max(outer * 0.5f, core);
            }

            ds.FixDimensions();
            ds.RecalculateBounds();

            spawned = VolumeObjectFactory.CreateObject(ds);
            spawned.transform.SetParent(transform, false);
            spawned.transform.localRotation = Quaternion.Euler(rotationEuler);
            return spawned;
        }
    }
}
