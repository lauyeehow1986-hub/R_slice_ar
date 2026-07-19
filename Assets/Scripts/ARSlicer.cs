using UnityEngine;
using UnityVolumeRendering;

namespace SliceAR
{
    /// <summary>
    /// AR-mode cross-section driver. The volume is anchored in the world; the cutting plane
    /// tracks the physical device (AR camera) pose, so moving the phone through the anchored
    /// volume sweeps the cross-section — the AR equivalent of the gyro-driven <see cref="MotionSlicer"/>.
    /// </summary>
    public class ARSlicer : MonoBehaviour
    {
        [Tooltip("Orientation offset applied to the camera rotation so the cut plane aligns with " +
                 "the device screen plane. Tune on-device.")]
        public Vector3 planeOrientationOffset = new Vector3(90f, 0f, 0f);

        private Transform planeTransform;
        private Transform arCamera;

        /// <summary>Spawn a cross-section plane for <paramref name="volume"/> driven by the AR camera.</summary>
        public void Attach(VolumeRenderedObject volume, Transform cameraTransform)
        {
            VolumeObjectFactory.SpawnCrossSectionPlane(volume);
            var plane = Object.FindObjectOfType<CrossSectionPlane>();
            if (plane != null)
                planeTransform = plane.transform;
            arCamera = cameraTransform;
        }

        private void Update()
        {
            if (planeTransform == null || arCamera == null)
                return;

            planeTransform.SetPositionAndRotation(
                arCamera.position,
                arCamera.rotation * Quaternion.Euler(planeOrientationOffset));
        }
    }
}
