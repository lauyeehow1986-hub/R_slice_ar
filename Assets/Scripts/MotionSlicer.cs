using UnityEngine;
using UnityVolumeRendering;

namespace SliceAR
{
    /// <summary>
    /// Drives a UnityVolumeRendering <see cref="CrossSectionPlane"/> from device motion.
    /// On a device with a gyroscope the plane's orientation follows the device attitude
    /// ("move the device to define the cut" — the core Slice-AR interaction). In the editor,
    /// where there is no gyroscope, it slowly auto-sweeps so the slicing is visible without
    /// hardware. Phase 3 will replace the gyro source with the AR camera pose.
    /// </summary>
    public class MotionSlicer : MonoBehaviour
    {
        [Tooltip("Degrees/second for the editor auto-sweep fallback (used when no gyroscope).")]
        public float editorSweepSpeed = 30f;

        private Transform planeTransform;
        private Transform pivot;
        private bool gyroActive;

        /// <summary>Spawn a cross-section plane for <paramref name="volume"/> and start driving it.</summary>
        public void Attach(VolumeRenderedObject volume)
        {
            VolumeObjectFactory.SpawnCrossSectionPlane(volume);
            var plane = Object.FindObjectOfType<CrossSectionPlane>();
            if (plane != null)
                planeTransform = plane.transform;
            pivot = volume.transform;

            if (SystemInfo.supportsGyroscope)
            {
                Input.gyro.enabled = true;
                gyroActive = true;
            }
        }

        private void Update()
        {
            if (planeTransform == null || pivot == null)
                return;

            // Pivot the cutting plane at the volume centre.
            planeTransform.position = pivot.position;

            if (gyroActive)
                planeTransform.rotation = GyroToUnity(Input.gyro.attitude);
            else
                planeTransform.Rotate(Vector3.up, editorSweepSpeed * Time.deltaTime, Space.World);
        }

        // Convert a right-handed gyroscope attitude into Unity's left-handed world space.
        private static Quaternion GyroToUnity(Quaternion q)
        {
            return Quaternion.Euler(90f, 0f, 0f) * new Quaternion(q.x, q.y, -q.z, -q.w);
        }
    }
}
