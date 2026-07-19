using UnityEngine;
using UnityVolumeRendering;

namespace SliceAR
{
    /// <summary>
    /// AR-mode pose driver. The volume is anchored in the world; the active slicing representation
    /// (managed by <see cref="SliceController"/>) tracks the physical device (AR camera) pose, so
    /// moving the phone through the anchored volume sweeps the cut/slice through it.
    /// </summary>
    [RequireComponent(typeof(SliceController))]
    public class ARSlicer : MonoBehaviour
    {
        private SliceController controller;
        private Transform arCamera;

        /// <summary>Set up slicing for <paramref name="volume"/>, driven by the AR camera pose.</summary>
        public void Attach(VolumeRenderedObject volume, Transform cameraTransform)
        {
            controller = GetComponent<SliceController>();
            if (controller == null)
                controller = gameObject.AddComponent<SliceController>();
            controller.Setup(volume);
            arCamera = cameraTransform;
        }

        private void Update()
        {
            if (controller == null || arCamera == null)
                return;

            controller.ApplyPose(arCamera.position, arCamera.rotation);
        }
    }
}
