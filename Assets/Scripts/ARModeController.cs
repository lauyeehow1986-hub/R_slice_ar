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
            yield return AnchorInFrontRoutine();

            var slicer = gameObject.GetComponent<ARSlicer>();
            if (slicer == null)
                slicer = gameObject.AddComponent<ARSlicer>();
            slicer.Attach(volume, camT);

            // Edge arrow pointing back to the anchored volume once the user moves away from it.
            if (GetComponent<ARVolumeIndicator>() == null)
                gameObject.AddComponent<ARVolumeIndicator>();
        }

        /// <summary>
        /// (Re)anchor the volume a short distance in front of the camera. A proper <see cref="ARAnchor"/> is
        /// what keeps ARCore correcting the pose in lock-step with the world; content left at a raw world
        /// position slides "by itself" whenever ARCore refines its map (the occasional drift after moving
        /// around a lot). A fresh anchor is created each call — an existing anchor's pose is owned by the AR
        /// subsystem and must not be moved by hand — replacing the previous one.
        ///
        /// Created via <see cref="ARAnchorManager.TryAddAnchorAsync"/>: adding an ARAnchor component to a
        /// GameObject at runtime is the discouraged path, and silently leaves the anchor unregistered (so it
        /// never actually tracks) if the manager isn't ready yet. Falls back to that older path only if the
        /// async request is unavailable or fails, so anchoring still happens either way.
        /// </summary>
        private IEnumerator AnchorInFrontRoutine()
        {
            if (anchoredVolume == null)
                yield break;

            Vector3 anchorPos = camT != null
                ? camT.position + camT.forward * anchorDistance
                : Vector3.forward * anchorDistance;
            var pose = new Pose(anchorPos, Quaternion.identity);

            GameObject newAnchor = null;
            var manager = Object.FindObjectOfType<ARAnchorManager>();
            if (manager != null && manager.enabled)
            {
                var awaiter = manager.TryAddAnchorAsync(pose).GetAwaiter();
                while (!awaiter.IsCompleted)
                    yield return null;

                var result = awaiter.GetResult();
                if (result.status.IsSuccess() && result.value != null)
                    newAnchor = result.value.gameObject;
            }

            if (newAnchor == null)
            {
                // Fallback: the pre-6.x pattern. Still better than no anchor at all.
                newAnchor = new GameObject("VolumeAnchor");
                newAnchor.transform.SetPositionAndRotation(anchorPos, Quaternion.identity);
                newAnchor.AddComponent<ARAnchor>();
            }

            anchoredVolume.transform.SetParent(newAnchor.transform, false);
            anchoredVolume.transform.localPosition = Vector3.zero;
            anchoredVolume.transform.localRotation = Quaternion.identity;
            anchoredVolume.transform.localScale = Vector3.one * arScale;

            if (anchorGO != null && anchorGO != newAnchor)
                Destroy(anchorGO);
            anchorGO = newAnchor;
        }

        /// <summary>Bring the volume back in front of the user (the Recenter button in AR mode).</summary>
        public void Recenter()
        {
            if (camT == null)
                camT = Camera.main != null ? Camera.main.transform : null;
            StartCoroutine(AnchorInFrontRoutine());   // anchor creation is async, so run it as a coroutine
        }
    }
}
