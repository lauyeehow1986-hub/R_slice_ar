using UnityEngine;
using UnityEngine.InputSystem;
using UnityVolumeRendering;

namespace SliceAR
{
    /// <summary>
    /// 3D-mode pose driver. Pivots the active slicing representation (managed by
    /// <see cref="SliceController"/>) at the volume centre and orients it from the device
    /// gyroscope ("move the device to define the cut" — the core Slice-AR interaction). In the
    /// editor, where there is no gyroscope, it slowly auto-sweeps so the slicing is visible
    /// without hardware.
    /// </summary>
    [RequireComponent(typeof(SliceController))]
    public class MotionSlicer : MonoBehaviour
    {
        [Tooltip("Degrees/second for the editor auto-sweep fallback (used when no gyroscope).")]
        public float editorSweepSpeed = 30f;

        private SliceController controller;
        private Transform pivot;
        private bool gyroActive;
        private Quaternion sweepRotation = Quaternion.identity;

        /// <summary>Set up slicing for <paramref name="volume"/>, driven by device motion.</summary>
        public void Attach(VolumeRenderedObject volume)
        {
            controller = GetComponent<SliceController>();
            if (controller == null)
                controller = gameObject.AddComponent<SliceController>();
            controller.Setup(volume);
            pivot = volume.transform;

            if (AttitudeSensor.current != null)
            {
                InputSystem.EnableDevice(AttitudeSensor.current);
                gyroActive = true;
            }
        }

        private void Update()
        {
            if (controller == null || pivot == null)
                return;

            Quaternion rotation;
            if (gyroActive && AttitudeSensor.current != null)
            {
                rotation = GyroToUnity(AttitudeSensor.current.attitude.ReadValue());
            }
            else
            {
                sweepRotation *= Quaternion.AngleAxis(editorSweepSpeed * Time.deltaTime, Vector3.up);
                rotation = sweepRotation;
            }

            controller.ApplyPose(pivot.position, rotation);
        }

        // Convert a right-handed gyroscope attitude into Unity's left-handed world space.
        private static Quaternion GyroToUnity(Quaternion q)
        {
            return Quaternion.Euler(90f, 0f, 0f) * new Quaternion(q.x, q.y, -q.z, -q.w);
        }
    }
}
