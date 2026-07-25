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

        private VolumeRenderedObject anchoredVolume;
        private GameObject anchorGO;
        private Transform camT;

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

            camT = Camera.main != null ? Camera.main.transform : null;
            anchoredVolume = volume;
            AnchorInFront();

            var slicer = gameObject.GetComponent<ARSlicer>();
            if (slicer == null)
                slicer = gameObject.AddComponent<ARSlicer>();
            slicer.Attach(volume, camT);

            // Edge arrow pointing back to the anchored volume once the user moves away from it.
            if (GetComponent<ARVolumeIndicator>() == null)
                gameObject.AddComponent<ARVolumeIndicator>();
        }

        /// <summary>
        /// (Re)anchor the volume a short distance in front of the camera. Attaching it to an ARAnchor keeps
        /// ARCore correcting its pose in lock-step with the world so it doesn't drift; a raw world position
        /// slides "by itself" as ARCore refines its map. A fresh anchor is created each call (an existing
        /// anchor's pose is owned by the AR subsystem and shouldn't be moved by hand), replacing the old one.
        /// Needs an enabled ARAnchorManager on the XR Origin (present in the AR scene).
        /// </summary>
        private void AnchorInFront()
        {
            if (anchoredVolume == null)
                return;

            Vector3 anchorPos = camT != null
                ? camT.position + camT.forward * anchorDistance
                : Vector3.forward * anchorDistance;

            var newAnchor = new GameObject("VolumeAnchor");
            newAnchor.transform.SetPositionAndRotation(anchorPos, Quaternion.identity);
            newAnchor.AddComponent<ARAnchor>();

            anchoredVolume.transform.SetParent(newAnchor.transform, false);
            anchoredVolume.transform.localPosition = Vector3.zero;
            anchoredVolume.transform.localRotation = Quaternion.identity;
            anchoredVolume.transform.localScale = Vector3.one * arScale;

            if (anchorGO != null)
                Destroy(anchorGO);
            anchorGO = newAnchor;
        }

        /// <summary>Bring the volume back in front of the user (the Recenter button in AR mode).</summary>
        public void Recenter()
        {
            if (camT == null)
                camT = Camera.main != null ? Camera.main.transform : null;
            AnchorInFront();
        }
    }
}
