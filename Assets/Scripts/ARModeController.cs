using UnityEngine;
using UnityVolumeRendering;

namespace SliceAR
{
    /// <summary>
    /// Sets up the AR slicing experience: generates the volume, anchors it in the world a short
    /// distance in front of the user, scales it to a tabletop size, and drives the cross-section
    /// plane from the AR camera (device) pose via <see cref="ARSlicer"/>.
    /// Requires an AR Foundation rig in the scene (AR Session + XR Origin with an AR camera).
    /// </summary>
    [RequireComponent(typeof(SampleVolumeGenerator))]
    public class ARModeController : MonoBehaviour
    {
        [Tooltip("Distance in metres in front of the camera to anchor the volume on start.")]
        public float anchorDistance = 0.6f;

        [Tooltip("Edge size of the anchored volume, in metres.")]
        public float arScale = 0.3f;

        private void Start()
        {
            var generator = GetComponent<SampleVolumeGenerator>();
            generator.generateOnStart = false;

            var volume = generator.Generate();

            Transform cam = Camera.main != null ? Camera.main.transform : null;
            Vector3 anchor = cam != null
                ? cam.position + cam.forward * anchorDistance
                : Vector3.forward * anchorDistance;

            volume.transform.position = anchor;
            volume.transform.localScale = Vector3.one * arScale;

            var slicer = gameObject.GetComponent<ARSlicer>();
            if (slicer == null)
                slicer = gameObject.AddComponent<ARSlicer>();
            slicer.Attach(volume, cam);
        }
    }
}
