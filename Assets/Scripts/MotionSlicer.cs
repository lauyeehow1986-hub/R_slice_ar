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

        // Neutral-pose offset applied to the gyro attitude. Recentring sets this so the current
        // device pose maps to "straight on", which also cancels the slow yaw drift that the phone's
        // rotation-vector sensor accumulates over time.
        private Quaternion recenterOffset = Quaternion.identity;

        // Last two-finger pinch distance (pixels), -1 when not pinching.
        private float lastPinchDist = -1f;

        /// <summary>Set up slicing for <paramref name="volume"/>, driven by device motion.</summary>
        public void Attach(VolumeRenderedObject volume)
        {
            controller = GetComponent<SliceController>();
            if (controller == null)
                controller = gameObject.AddComponent<SliceController>();
            controller.Setup(volume);
            pivot = volume.transform;

            TryEnableGyro();
        }

        // The AttitudeSensor may not be enumerated on the frame we attach (notably right after an
        // import scene-reload), so this is retried from Update until it comes online.
        private void TryEnableGyro()
        {
            if (gyroActive || AttitudeSensor.current == null)
                return;

            InputSystem.EnableDevice(AttitudeSensor.current);
            gyroActive = true;
        }

        /// <summary>Make the current device pose the neutral "straight-on" orientation. Also clears
        /// accumulated sensor yaw drift. In the editor (no gyro) this resets the auto-sweep.</summary>
        public void Recenter()
        {
            if (gyroActive && AttitudeSensor.current != null)
                recenterOffset = Quaternion.Inverse(GyroToUnity(AttitudeSensor.current.attitude.ReadValue()));
            else
                sweepRotation = Quaternion.identity;
        }

        private void Update()
        {
            if (controller == null || pivot == null)
                return;

            if (!gyroActive)
                TryEnableGyro();

            Quaternion rotation;
            if (gyroActive && AttitudeSensor.current != null)
            {
                rotation = recenterOffset * GyroToUnity(AttitudeSensor.current.attitude.ReadValue());
            }
            else if (Application.isEditor)
            {
                // Editor-only convenience: no gyroscope, so slowly auto-sweep to show slicing.
                sweepRotation *= Quaternion.AngleAxis(editorSweepSpeed * Time.deltaTime, Vector3.up);
                rotation = sweepRotation;
            }
            else
            {
                // On device with no gyro yet: hold still rather than spinning away.
                HandleZoom();
                return;
            }

            controller.ApplyTurntable(rotation);
            HandleZoom();
        }

        // Two-finger pinch (device) or mouse wheel (editor) scales the volume about its centre.
        private void HandleZoom()
        {
            if (controller == null)
                return;

            var ts = Touchscreen.current;
            if (ts != null)
            {
                Vector2 a = Vector2.zero, b = Vector2.zero;
                int down = 0;
                foreach (var t in ts.touches)
                {
                    if (!t.press.isPressed)
                        continue;
                    if (down == 0) a = t.position.ReadValue();
                    else if (down == 1) b = t.position.ReadValue();
                    down++;
                }

                if (down >= 2)
                {
                    float dist = Vector2.Distance(a, b);
                    if (lastPinchDist > 0f)
                    {
                        float delta = dist - lastPinchDist;
                        if (Mathf.Abs(delta) > 0.5f)
                            controller.ZoomBy(1f + delta * 0.0015f);
                    }
                    lastPinchDist = dist;
                    return;
                }
                lastPinchDist = -1f;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                    controller.ZoomBy(1f + Mathf.Clamp(scroll, -120f, 120f) * 0.0008f);
            }
        }

        // Convert a right-handed gyroscope attitude into Unity's left-handed world space.
        private static Quaternion GyroToUnity(Quaternion q)
        {
            return Quaternion.Euler(90f, 0f, 0f) * new Quaternion(q.x, q.y, -q.z, -q.w);
        }
    }
}
