using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
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

        [Tooltip("Seconds to wait for plane detection before placing the first anchor without a plane.")]
        public float planeWaitTimeout = 1.5f;

        private VolumeRenderedObject anchoredVolume;
        private GameObject anchorGO;
        private Transform camT;

        private IEnumerator Start()
        {
            // ARMode is build scene 0, so a device without ARCore would otherwise launch straight into a
            // session that can never track: several seconds of nothing, then a volume with no passthrough.
            // The 3D CT-viewer needs no ARCore at all, so hand over to it instead. Only Unsupported is
            // treated as fatal — NeedsInstall means ARCore can still arrive, and the tracking wait below
            // already covers that case.
            yield return ARSession.CheckAvailability();
            if (ARSession.state == ARSessionState.Unsupported)
            {
                VolumeSession.ArUnsupported = true;
                UnityEngine.SceneManagement.SceneManager.LoadScene("ThreeDMode");
                yield break;
            }

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

            // Give plane detection a brief chance to find the floor before the first anchor: a
            // plane-attached anchor is far steadier than a free one (see AnchorInFrontRoutine).
            // Bounded, so a featureless room still starts promptly — just without a plane.
            yield return WaitForPlaneRoutine();

            volume.gameObject.SetActive(true);

            // Anchoring without a camera silently places the volume at the world origin instead of in
            // front of the user (see AnchorInFrontRoutine's fallback), so give Camera.main a bounded
            // chance to resolve as the AR rig comes up.
            float camWait = 0f;
            while (Camera.main == null && camWait < 3f)
            {
                camWait += Time.deltaTime;
                yield return null;
            }

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

            // Tracking-quality hints: names the condition that makes an anchored volume drift away.
            if (GetComponent<ARTrackingHintUI>() == null)
                gameObject.AddComponent<ARTrackingHintUI>();

            // Wireframe box marking where the anchored volume sits, for Slice mode — which hides the
            // volume itself and would otherwise leave nothing to walk towards.
            if (GetComponent<ARVolumeOutline>() == null)
                gameObject.AddComponent<ARVolumeOutline>();
        }

        /// <summary>
        /// (Re)anchor the volume a short distance in front of the camera. A proper <see cref="ARAnchor"/> is
        /// what keeps ARCore correcting the pose in lock-step with the world; content left at a raw world
        /// position slides "by itself" whenever ARCore refines its map (the occasional drift after moving
        /// around a lot). A fresh anchor is created each call — an existing anchor's pose is owned by the AR
        /// subsystem and must not be moved by hand — replacing the previous one.
        ///
        /// Three tiers, best first: <see cref="ARAnchorManager.AttachAnchor"/> onto a detected plane (steadiest
        /// — ARCore refines planes and carries their anchors along), else <see cref="ARAnchorManager.TryAddAnchorAsync"/>
        /// for a free world anchor, else an ARAnchor component. That last tier is the discouraged path: added at
        /// runtime it silently leaves the anchor unregistered (so it never actually tracks) if the manager isn't
        /// ready — kept only so anchoring still happens if the other two are unavailable.
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
                // Preferred: attach to a detected plane. ARCore keeps refining planes as it maps the
                // room and moves plane-attached anchors along with them, so the volume holds its spot
                // even where the feature cloud is thin (blank walls, plain flooring) — the case where
                // a free world anchor can relocalise metres away.
                var planes = Object.FindObjectOfType<ARPlaneManager>();
                ARPlane plane = planes != null ? BestPlaneFor(planes, anchorPos) : null;
                if (plane != null)
                {
                    var attached = manager.AttachAnchor(plane, pose);
                    if (attached != null)
                        newAnchor = attached.gameObject;
                }

                if (newAnchor == null)
                {
                    // No usable plane (detection still warming up, or nothing trackable in view).
                    // A free world anchor is less stable but still better than none.
                    var awaiter = manager.TryAddAnchorAsync(pose).GetAwaiter();
                    while (!awaiter.IsCompleted)
                        yield return null;

                    var result = awaiter.GetResult();
                    if (result.status.IsSuccess() && result.value != null)
                        newAnchor = result.value.gameObject;
                }
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
            anchoredVolume.transform.localScale = Vector3.one * arScale;
            // Set the WORLD rotation, not the local one: the anchor's own rotation depends on which tier
            // produced it (a plane-attached anchor can be aligned to the plane, a free one is not), so
            // keeping localRotation at identity would bring the volume up in a different orientation from
            // one entry or Recenter to the next. Pinning world rotation makes it identical every time,
            // while leaving the volume free to ride the anchor as ARCore corrects it.
            anchoredVolume.transform.rotation = Quaternion.identity;

            if (anchorGO != null && anchorGO != newAnchor)
                Destroy(anchorGO);
            anchorGO = newAnchor;
        }

        /// <summary>Wait (bounded by <see cref="planeWaitTimeout"/>) for at least one tracked plane.</summary>
        private IEnumerator WaitForPlaneRoutine()
        {
            var planes = Object.FindObjectOfType<ARPlaneManager>();
            if (planes == null)
                yield break;

            float waited = 0f;
            while (waited < planeWaitTimeout && !HasTrackedPlane(planes))
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }

        private static bool HasTrackedPlane(ARPlaneManager planes)
        {
            foreach (var p in planes.trackables)
                if (p.trackingState == TrackingState.Tracking)
                    return true;
            return false;
        }

        /// <summary>The tracked plane nearest <paramref name="point"/>, or null if none are tracked yet.</summary>
        private static ARPlane BestPlaneFor(ARPlaneManager planes, Vector3 point)
        {
            ARPlane best = null;
            float bestDist = float.MaxValue;
            foreach (var p in planes.trackables)
            {
                if (p.trackingState != TrackingState.Tracking)
                    continue;
                float d = Vector3.Distance(p.center, point);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = p;
                }
            }
            return best;
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
