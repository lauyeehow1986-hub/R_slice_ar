using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityVolumeRendering;

namespace SliceAR
{
    /// <summary>
    /// Sets up the AR slicing experience: loads the volume, anchors it in the world a short
    /// distance in front of the user, scales it to a tabletop size, and drives the cross-section
    /// plane from the AR camera (device) pose via <see cref="ARSlicer"/>.
    /// Requires an AR Foundation rig in the scene (AR Session + XR Origin with an AR camera).
    ///
    /// Prefers a bundled dataset via <see cref="VolumeFileLoader"/> when one is present; otherwise
    /// falls back to the synthetic <see cref="SampleVolumeGenerator"/> so AR still works.
    /// </summary>
    public class ARModeController : MonoBehaviour
    {
        [Tooltip("Distance in metres in front of the camera to anchor the volume on start.")]
        public float anchorDistance = 0.6f;

        [Tooltip("Edge size of the anchored volume, in metres.")]
        public float arScale = 0.3f;

        private IEnumerator Start()
        {
            VolumeRenderedObject volume = null;

            var loader = GetComponent<VolumeFileLoader>();
            if (loader != null)
            {
                loader.loadOnStart = false;        // this controller drives the load + anchoring
                yield return loader.Load(v => volume = v);
            }

            if (volume == null)
            {
                var generator = GetComponent<SampleVolumeGenerator>();
                if (generator != null)
                {
                    generator.generateOnStart = false;
                    volume = generator.Generate();
                }
            }

            if (volume == null)
            {
                Debug.LogError("ARModeController: no volume could be loaded.");
                yield break;
            }

            // Wait until AR tracking is established so the camera pose is valid before anchoring.
            // A fast-loading volume (e.g. a small imported RAW) otherwise anchors relative to the
            // origin and appears to jump away the instant tracking starts and the camera pose updates.
            // Hide the volume during the wait to avoid a flash at the origin.
            volume.gameObject.SetActive(false);
            float waited = 0f;
            while (ARSession.state != ARSessionState.SessionTracking && waited < 6f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            // A couple of extra frames so the first tracked camera pose has settled.
            yield return null;
            yield return null;
            volume.gameObject.SetActive(true);

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
