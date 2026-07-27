using UnityEngine;
using UnityVolumeRendering;

namespace SliceAR
{
    /// <summary>
    /// Owns both cross-section representations for a volume and toggles between them:
    ///  - <b>Clip</b>: a <see cref="CrossSectionPlane"/> that cuts the volume open (3D cut-away).
    ///  - <b>Slice</b>: a <see cref="SlicingPlane"/> that paints the flat 2D slice image on the plane
    ///    (classic CT/MRI viewer), optionally hiding the 3D volume for a clean read.
    /// A pose driver (<see cref="ARSlicer"/> in AR, <see cref="MotionSlicer"/> in 3D) calls
    /// <see cref="ApplyPose"/> each frame; the UI calls <see cref="ToggleMode"/>.
    /// </summary>
    public class SliceController : MonoBehaviour
    {
        public enum SliceMode { Clip, Slice }

        [Tooltip("Orientation offset for the Clip cross-section plane (tune on-device).")]
        public Vector3 clipOffsetEuler = new Vector3(90f, 0f, 0f);

        [Tooltip("Orientation offset for the 2D slice plane so its face points at the viewer " +
                 "(90° stands the plane up to face the camera; tune on-device).")]
        public Vector3 sliceOffsetEuler = new Vector3(90f, 0f, 0f);

        [Tooltip("Local scale of the 2D slice quad (bigger = covers more of the volume).")]
        public float slicePlaneScale = 1.0f;

        // CT-viewer in-plane rotation (degrees about the view axis) so the slice reads upright instead
        // of lying on its side. Baked as a constant rather than a serialized field ON PURPOSE: a newly
        // added serialized field deserialises to the C# type-default (0) in a scene-serialized
        // component — NOT its initializer — so a field would silently be 0 in the build while looking
        // correct in the editor. 90° is verified upright for all three canonical planes.
        private const float InPlaneSpinDeg = 90f;

        [Tooltip("Hide the 3D volume while in Slice mode for a clean flat 2D-slice (CT-viewer) read. " +
                 "Off keeps the volume visible for context.")]
        public bool hideVolumeInSliceMode = true;

        [Tooltip("In Slice mode, keep the slice plane anchored at the volume centre and control only " +
                 "its angle with the device, so you can stand back and watch the slice. Off makes the " +
                 "slice plane ride the device position (like the clip plane).")]
        public bool anchorSliceAtVolumeCentre = true;

        public SliceMode Mode { get; private set; } = SliceMode.Clip;

        private VolumeRenderedObject volume;
        private CrossSectionPlane crossSection;
        private SlicingPlane slicingPlane;

        /// <summary>Canonical viewing planes for the 3D CT-viewer (defined against the volume's
        /// anatomical axes; only truly anatomical for DICOM, geometric otherwise).</summary>
        public enum ViewAxis { Axial, Coronal, Sagittal }

        // 3D CT-viewer state: the volume stays FIXED (UVR slices from the plane's pose *relative to*
        // the volume, so rotating the volume would just re-show the same slice — see git history).
        // Instead the slice plane snaps to a canonical anatomical plane, the device scrubs its depth,
        // and the camera is placed to look straight down that plane's normal so the slice reads
        // face-on and centred. AR is unaffected (it still uses ApplyPose).
        // Auto-framed camera distance is recomputed every frame from the extents *in the current view
        // plane* (so switching axes reframes correctly); user pinch/scroll adjusts zoomFactor, a
        // multiplier applied on top so it survives the reframe. camDistance stays as the last framed
        // value only for ZoomBy's fallback.
        private float camDistance;
        private float zoomFactor = 1f;
        private const float MinCamDistance = 0.2f;
        private const float MaxCamDistance = 8f;

        // Clip-mode orbit: the camera swings around the volume centre by these angles (degrees) so the
        // user can rotate the 3D cut-away with a finger drag. Defaults give a pleasant 3/4 view. Yaw is
        // about the view `up`, pitch about the view `right`; pitch is clamped to avoid flipping over the
        // poles. Slice mode ignores these (it stays face-on).
        private float orbitYaw = 22f;
        private float orbitPitch = -27f;

        // The world-space plane the user currently sees (position + normal), cached each frame so the
        // annotation tool can raycast a screen tap onto exactly the slice/cut on screen. Set by
        // ShowCtSlice (3D) and ApplyPose (AR).
        private Vector3 activePlanePoint;
        private Vector3 activePlaneNormal = Vector3.forward;
        private bool hasActivePlane;

        /// <summary>The plane currently shown on screen (for tap-to-place annotations). Returns false
        /// until a slice has been positioned at least once.</summary>
        public bool TryGetActivePlane(out Vector3 point, out Vector3 normal)
        {
            point = activePlanePoint;
            normal = activePlaneNormal;
            return hasActivePlane;
        }

        /// <param name="showCutIndicator">Keep the cross-section plane's outline visible. AR passes true
        /// (in Clip mode it shows where the cutting plane is as you steer the device through the volume —
        /// aiming feedback); the 3D CT-viewer passes false (there the box just floats confusingly, and the
        /// cut face is already visible on the volume).</param>
        public void Setup(VolumeRenderedObject vol, bool showCutIndicator = false)
        {
            volume = vol;

            VolumeObjectFactory.SpawnCrossSectionPlane(vol);
            crossSection = Object.FindObjectOfType<CrossSectionPlane>();
            // The cut is applied via the CrossSectionManager (matrix), not the renderer, so toggling the
            // renderer only shows/hides the outline gizmo — the cut itself is unaffected either way.
            if (crossSection != null && !showCutIndicator)
                foreach (var r in crossSection.GetComponentsInChildren<Renderer>(true))
                    r.enabled = false;

            slicingPlane = vol.CreateSlicingPlane();
            if (slicingPlane != null)
            {
                slicingPlane.transform.localScale = Vector3.one * slicePlaneScale;
                // Swap the slice material to our transparent variant so out-of-volume / air fragments
                // don't paint opaque black over the AR camera passthrough (Slice mode in AR otherwise
                // looked like it had lost the camera feed). Preserves the material's textures/matrices
                // (same property names); SlicingPlane.Update keeps feeding _planeMat/_parentInverseMat.
                var smr = slicingPlane.GetComponent<MeshRenderer>();
                var transparentSlice = Shader.Find("SliceAR/SliceRenderingTransparent");
                if (smr != null && smr.sharedMaterial != null && transparentSlice != null)
                    smr.sharedMaterial.shader = transparentSlice;
            }

            ApplyModeVisibility();
            SetShading(VolumeSession.GradientShading);   // carry the choice across scene reloads
            ApplyRenderQuality();
        }

        /// <summary>Push the current <see cref="VolumeSession.Quality"/> preset onto the volume. Called at
        /// setup and whenever the user cycles the setting.</summary>
        public void ApplyRenderQuality()
        {
            RenderQuality.Apply(volume);
        }

        /// <summary>
        /// Enable/disable gradient (normal-based) lighting on the 3D direct volume render. Uses a camera
        /// headlight (<see cref="UnityVolumeRendering.LightSource.ActiveCamera"/>) so it needs no scene light
        /// and behaves the same in 3D and AR. Only affects the DVR (Clip mode / the AR volume); the flat
        /// Slice image is unlit by design. The lit shader variant is a <c>multi_compile</c>, so it always
        /// ships in the build (no variant-collection needed). The gradient texture is generated on first
        /// enable (async in UVR), so the very first toggle-on may take a moment on device.
        /// </summary>
        public void SetShading(bool enabled)
        {
            if (volume == null)
                return;
            volume.SetLightSource(UnityVolumeRendering.LightSource.ActiveCamera);
            volume.SetLightingEnabled(enabled);
        }

        /// <summary>
        /// Apply a transfer function to the volume AND to the flat slice plane. UVR's
        /// <c>SetTransferFunction</c> only rebinds the volume's own material (used by the Clip cut-away
        /// DVR and the AR render), so the slice plane — whose material caches the TF texture from when it
        /// was created — must be refreshed separately, or a runtime LUT change never reaches Slice mode.
        /// </summary>
        public void SetTransferFunction(UnityVolumeRendering.TransferFunction tf)
        {
            if (tf == null)
                return;
            if (volume != null)
                volume.SetTransferFunction(tf);
            if (slicingPlane != null)
            {
                var smr = slicingPlane.GetComponent<MeshRenderer>();
                if (smr != null && smr.sharedMaterial != null)
                    smr.sharedMaterial.SetTexture("_TFTex", tf.GetTexture());
            }
        }

        /// <summary>Place the active slicing representation at the given world pose.</summary>
        public void ApplyPose(Vector3 position, Quaternion rotation)
        {
            // Cache the on-screen plane for tap-to-place annotations: the point is where the active
            // plane sits, the normal faces the camera (the user taps the face they can see).
            var mainCam = Camera.main;
            activePlaneNormal = mainCam != null ? -mainCam.transform.forward : (rotation * Vector3.forward);
            hasActivePlane = true;

            if (Mode == SliceMode.Clip)
            {
                activePlanePoint = position;
                if (crossSection != null)
                    crossSection.transform.SetPositionAndRotation(position, rotation * Quaternion.Euler(clipOffsetEuler));
            }
            else
            {
                if (slicingPlane != null)
                {
                    Quaternion planeRot = rotation * Quaternion.Euler(sliceOffsetEuler);
                    Vector3 slicePos = (anchorSliceAtVolumeCentre && volume != null)
                        ? SliceDepthFor(position, planeRot)
                        : position;
                    activePlanePoint = slicePos;
                    slicingPlane.transform.SetPositionAndRotation(slicePos, planeRot);
                }
            }
        }

        // How far the plane may travel from the centre, as a fraction of the volume's half-extent along the
        // slice normal. Short of 1 so the plane never sits exactly on a face, where the quad's border reads
        // outside the volume and ghosts into frame.
        private const float DepthLimit = 0.9f;

        // Device distance from the centre along the slice normal, in half-extents, over which the plane hands
        // control from the centre anchor to the device. Fully device-driven inside FollowInner, fully centred
        // beyond FollowOuter.
        private const float FollowInner = 0.8f;
        private const float FollowOuter = 1.6f;

        /// <summary>
        /// Where the slice plane sits along its own normal, given the device pose (AR only — 3D drives the
        /// plane through <see cref="ShowCtSlice"/> instead).
        ///
        /// Standing back and watching the slice re-cut as the phone turns is the main way this mode is used,
        /// and the volume centre is the most informative cut, so the centre is held while the device is away
        /// from the volume. Pure device tracking would put the plane at the outer scalp whenever the user
        /// stood at a normal viewing distance, and pure centre anchoring means walking the phone through the
        /// anatomy — the whole point of AR — changes nothing but the angle.
        ///
        /// So the plane comes out to meet the device as it reaches the volume and then rides it through.
        /// Blended across a band rather than switched at a threshold, so there is no jump at the boundary.
        /// </summary>
        private Vector3 SliceDepthFor(Vector3 devicePosition, Quaternion planeRotation)
        {
            Vector3 centre = VolumeCentre();

            // UVR's slice shader samples the volume on the plane's local XZ at y = 0, so the plane's local Y
            // is the slice normal — the one axis along which moving the plane changes which cut is shown.
            Vector3 normal = planeRotation * Vector3.up;

            float half = HalfExtentAlong(normal);
            if (half <= Mathf.Epsilon)
                return centre;

            float along = Vector3.Dot(devicePosition - centre, normal);
            float follow = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(FollowInner, FollowOuter, Mathf.Abs(along) / half));

            return centre + normal * (Mathf.Clamp(along, -half * DepthLimit, half * DepthLimit) * follow);
        }

        /// <summary>World-space visual centre of the volume (bounds centre, which accounts for the
        /// importer-baked scale/offset), falling back to the transform origin if unavailable.</summary>
        private Vector3 VolumeCentre()
        {
            if (volume != null && volume.meshRenderer != null)
                return volume.meshRenderer.bounds.center;
            return volume != null ? volume.transform.position : Vector3.zero;
        }

        /// <summary>
        /// 3D CT-viewer driver: snap the slice plane to the given canonical <paramref name="axis"/> at
        /// normalised depth <paramref name="depth01"/> (0..1 across the volume), and place
        /// <paramref name="cam"/> square-on to it so the slice reads face-on and centred. The volume
        /// itself is never moved.
        /// </summary>
        public void ShowCtSlice(ViewAxis axis, float depth01, Camera cam)
        {
            if (volume == null || slicingPlane == null)
                return;

            Transform frame = volume.meshRenderer != null ? volume.meshRenderer.transform : volume.transform;
            // Patient axes in world space. Which dataset-local axis maps to Left/Posterior/Superior
            // depends on how the volume's grid is stored (DICOM convention vs the bundled MRHead's
            // sagittal acquisition), so read the mapping from VolumeSession. TransformVector (not
            // TransformDirection) so the importer's baked scale/handedness is included.
            Vector3 left      = frame.TransformVector(VolumeSession.AxisLeft).normalized;
            Vector3 posterior = frame.TransformVector(VolumeSession.AxisPosterior).normalized;
            Vector3 superior  = frame.TransformVector(VolumeSession.AxisSuperior).normalized;

            Vector3 normal, up;
            switch (axis)
            {
                case ViewAxis.Axial:   normal = superior;  up = -posterior; break; // top-down
                case ViewAxis.Coronal: normal = posterior; up = superior;   break; // front-on
                default:               normal = left;      up = superior;   break; // side-on
            }

            // Never render upside-down: if the chosen up points downward in the world, flip it (and the
            // view normal with it, to keep a right-handed frame).
            if (Vector3.Dot(up, Vector3.up) < 0f)
                up = -up;

            Vector3 right = Vector3.Cross(up, normal).normalized;

            Vector3 centre = VolumeCentre();
            float half = 0.9f * HalfExtentAlong(normal);
            Vector3 slicePos = centre + normal * Mathf.Lerp(-half, half, Mathf.Clamp01(depth01));

            // Cache the on-screen plane for tap-to-place annotations.
            activePlanePoint = slicePos;
            activePlaneNormal = normal;
            hasActivePlane = true;

            // Orient the slice quad. UVR's SliceRenderingShader samples the volume on the plane's local
            // XZ plane while the quad is drawn in local XY, so a 90° rotation about X is required to put
            // the sampled cross-section onto the visible quad (without it every fragment reads outside
            // the volume → all black). The camera looks along `normal` (below), so the plane is built
            // from LookRotation(normal, up); the extra per-axis `spin` about the view normal brings the
            // anatomy upright (the 90°-about-X fold otherwise leaves the image rotated, differently per
            // axis). Calibrated on-device via the *SpinDeg fields.
            Quaternion viewSpin = Quaternion.AngleAxis(InPlaneSpinDeg, normal);
            slicingPlane.transform.SetPositionAndRotation(
                slicePos,
                viewSpin * Quaternion.LookRotation(normal, up) * Quaternion.Euler(90f, 0f, 0f));

            // Also drive the Clip cross-section so 3D Clip mode is a proper anatomical cut-away along the
            // current axis at the current depth. Without this it stays at its spawn default (volume
            // origin), cutting the volume in half regardless of the device — the "half image" artifact.
            if (crossSection != null)
                crossSection.transform.SetPositionAndRotation(
                    slicePos, Quaternion.LookRotation(normal, up) * Quaternion.Euler(clipOffsetEuler));

            if (cam != null)
            {
                // Frame to the on-screen extents (along `right` and `up`), not the volume's largest
                // half-extent. The bundled MR is anisotropic, and the old single cached distance made
                // some axes tiny or let the quad's out-of-volume border ghost into frame.
                float halfH = HalfExtentAlong(right);
                float halfV = HalfExtentAlong(up);
                float aspect = cam.aspect > 0f ? cam.aspect : 1f;
                float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float distV = halfV / tanV;
                float distH = halfH / (tanV * aspect);
                float framed = Mathf.Max(distV, distH) * 1.15f;   // margin so anatomy isn't clipped
                camDistance = Mathf.Clamp(framed * zoomFactor, MinCamDistance, MaxCamDistance);

                // Slice mode: look straight down the view normal so the flat slice reads face-on.
                // Clip mode: view the 3D cut-away from an OBLIQUE angle so the remaining half reads as a
                // 3D object (a straight-down view makes the cut look like a flat "half image").
                if (Mode == SliceMode.Clip)
                {
                    // Orbit the camera around the volume centre by the user-controlled yaw/pitch, starting
                    // from the axis normal, so the 3D cut-away can be spun with a finger (see OrbitBy).
                    Quaternion orbit = Quaternion.AngleAxis(orbitYaw, up) * Quaternion.AngleAxis(orbitPitch, right);
                    Vector3 viewDir = (orbit * normal).normalized;
                    Vector3 camUp = (orbit * up).normalized;
                    cam.transform.position = centre - viewDir * camDistance * 1.35f;
                    cam.transform.rotation = Quaternion.LookRotation(viewDir, camUp);
                }
                else
                {
                    cam.transform.position = centre - normal * camDistance;
                    cam.transform.rotation = Quaternion.LookRotation(normal, up);
                }
            }
        }

        /// <summary>Zoom the CT-viewer camera in/out. Adjusts a multiplier on the auto-framed distance
        /// (recomputed each frame in <see cref="ShowCtSlice"/>) so the zoom persists across depth/axis
        /// changes; &gt;1 factor = zoom in (closer). Clamped to a sane range.</summary>
        public void ZoomBy(float factor)
        {
            zoomFactor = Mathf.Clamp(zoomFactor / Mathf.Max(factor, 1e-3f), 0.25f, 4f);
        }

        /// <summary>Spin the Clip-mode 3D cut-away camera around the volume by the given yaw/pitch deltas
        /// (degrees). No-op visual effect in Slice mode (which stays face-on). Pitch is clamped so the
        /// view can't tumble past the poles.</summary>
        public void OrbitBy(float deltaYaw, float deltaPitch)
        {
            orbitYaw += deltaYaw;
            orbitPitch = Mathf.Clamp(orbitPitch + deltaPitch, -85f, 85f);
        }

        // Half the volume's world extent along the given (unit) direction.
        private float HalfExtentAlong(Vector3 dir)
        {
            if (volume == null || volume.meshRenderer == null)
                return 0.5f;
            Vector3 e = volume.meshRenderer.bounds.extents;
            return Mathf.Abs(e.x * dir.x) + Mathf.Abs(e.y * dir.y) + Mathf.Abs(e.z * dir.z);
        }

        public void ToggleMode()
        {
            SetMode(Mode == SliceMode.Clip ? SliceMode.Slice : SliceMode.Clip);
        }

        public void SetMode(SliceMode mode)
        {
            Mode = mode;
            ApplyModeVisibility();
        }

        private void ApplyModeVisibility()
        {
            bool clip = Mode == SliceMode.Clip;

            // Enabling/disabling the cross-section GameObject registers/unregisters it with the
            // volume's cross-section manager (so the clip only applies in Clip mode).
            if (crossSection != null)
                crossSection.gameObject.SetActive(clip);

            if (slicingPlane != null)
                slicingPlane.gameObject.SetActive(!clip);

            if (volume != null && volume.meshRenderer != null)
                volume.meshRenderer.enabled = clip || !hideVolumeInSliceMode;
        }
    }
}
