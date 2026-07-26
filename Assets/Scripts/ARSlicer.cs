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
            // Show the cut-plane outline in AR: in Clip mode it marks where the cutting plane sits so the
            // user can aim the device as they push it through the anchored volume. (It only draws in Clip
            // mode; Slice mode keeps a clean passthrough.)
            controller.Setup(volume, showCutIndicator: true);
            arCamera = cameraTransform;
        }

        private void Update()
        {
            if (controller == null)
                return;

            // Camera.main can still be unresolved when Attach runs, since the AR camera is enabled and
            // tagged as the rig comes up. Keep retrying: with no camera ApplyPose never runs at all, so
            // the cross-section stays where VolumeObjectFactory spawned it — at the volume origin — and
            // the volume renders permanently cut in half. Recovering here rather than giving up once is
            // what stops that state from lasting the whole session.
            if (arCamera == null)
            {
                var cam = Camera.main;
                if (cam == null)
                    return;
                arCamera = cam.transform;
            }

            controller.ApplyPose(arCamera.position, arCamera.rotation);
        }
    }
}
