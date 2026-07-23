using UnityEngine;
using UnityEngine.InputSystem;
using UnityVolumeRendering;

namespace SliceAR
{
    /// <summary>
    /// 3D-mode driver: a stable CT-viewer. The volume is fixed; the slice plane snaps to a canonical
    /// anatomical plane (axial/coronal/sagittal) and the camera looks straight down it, so the slice is
    /// always face-on and centred. Tilting the device scrubs the slice depth through the stack ("move
    /// the device to explore" — the Slice-AR interaction, stabilised). In the editor (no gyroscope) the
    /// depth auto-sweeps so slicing is visible without hardware.
    /// </summary>
    [RequireComponent(typeof(SliceController))]
    public class MotionSlicer : MonoBehaviour
    {
        [Tooltip("How far a device tilt scrubs the slice depth (higher = more travel per degree).")]
        public float tiltSensitivity = 0.8f;

        public SliceController.ViewAxis Axis { get; private set; } = SliceController.ViewAxis.Axial;

        private SliceController controller;
        private bool attached;
        private bool gyroActive;

        // Device pitch at the last Recenter; the scrub is measured relative to it so "neutral" is the
        // middle of the stack and the slow rotation-vector drift can be zeroed on demand.
        private Quaternion recenterOffset = Quaternion.identity;

        // Last two-finger pinch distance (pixels), -1 when not pinching.
        private float lastPinchDist = -1f;

        /// <summary>Set up slicing for <paramref name="volume"/>, driven by device tilt.</summary>
        public void Attach(VolumeRenderedObject volume)
        {
            controller = GetComponent<SliceController>();
            if (controller == null)
                controller = gameObject.AddComponent<SliceController>();
            controller.Setup(volume);
            // 3D mode is a CT-viewer: start in Slice (the flat 2D panel) rather than Clip. The Clip
            // cross-section defaults to the volume origin, so starting in Clip showed the volume cut in
            // half ("half image") before the user did anything. (AR keeps the Clip default via ARSlicer.)
            controller.SetMode(SliceController.SliceMode.Slice);
            attached = true;
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

        /// <summary>Make the current device tilt the neutral (mid-stack) pose and clear sensor drift.</summary>
        public void Recenter()
        {
            if (gyroActive && AttitudeSensor.current != null)
                recenterOffset = Quaternion.Inverse(GyroToUnity(AttitudeSensor.current.attitude.ReadValue()));
        }

        /// <summary>Cycle Axial → Coronal → Sagittal → Axial.</summary>
        public void CycleAxis()
        {
            Axis = (SliceController.ViewAxis)(((int)Axis + 1) % 3);
        }

        private void Update()
        {
            if (!attached || controller == null)
                return;

            if (!gyroActive)
                TryEnableGyro();

            float depth01;
            if (gyroActive && AttitudeSensor.current != null)
            {
                // Tilt about the horizontal axis scrubs the depth: neutral (post-Recenter) = mid-stack.
                Quaternion r = recenterOffset * GyroToUnity(AttitudeSensor.current.attitude.ReadValue());
                float pitch = (r * Vector3.forward).y;            // -1 (tilt up) .. +1 (tilt down)
                depth01 = Mathf.Clamp01(0.5f - pitch * tiltSensitivity);
            }
            else if (Application.isEditor)
            {
                // Editor-only convenience: no gyroscope, so slowly sweep the depth.
                depth01 = Mathf.PingPong(Time.time * 0.15f, 1f);
            }
            else
            {
                depth01 = 0.5f;   // on device with no gyro yet: hold the mid slice
            }

            controller.ShowCtSlice(Axis, depth01, Camera.main);
            HandleZoom();
        }

        // Two-finger pinch (device) or mouse wheel (editor) dollies the CT-viewer camera.
        private void HandleZoom()
        {
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
