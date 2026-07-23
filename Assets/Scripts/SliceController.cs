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

        [Tooltip("CT-viewer in-plane rotation (degrees about the view axis) so the slice reads upright " +
                 "instead of lying on its side. Tune on-device if the anatomy is rotated.")]
        public float inPlaneSpinDeg = 90f;

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
        private float camDistance;
        private const float MinCamDistance = 0.4f;
        private const float MaxCamDistance = 6f;

        public void Setup(VolumeRenderedObject vol)
        {
            volume = vol;

            VolumeObjectFactory.SpawnCrossSectionPlane(vol);
            crossSection = Object.FindObjectOfType<CrossSectionPlane>();

            slicingPlane = vol.CreateSlicingPlane();
            if (slicingPlane != null)
                slicingPlane.transform.localScale = Vector3.one * slicePlaneScale;

            ApplyModeVisibility();
        }

        /// <summary>Place the active slicing representation at the given world pose.</summary>
        public void ApplyPose(Vector3 position, Quaternion rotation)
        {
            if (Mode == SliceMode.Clip)
            {
                if (crossSection != null)
                    crossSection.transform.SetPositionAndRotation(position, rotation * Quaternion.Euler(clipOffsetEuler));
            }
            else
            {
                if (slicingPlane != null)
                {
                    // Anchor the slice at the volume centre so it is always inside the volume and
                    // readable from a distance; the device only steers the slice angle. Use the
                    // renderer's world-space bounds centre rather than transform.position: importers
                    // (notably DICOM) bake an offset/scale into the mesh, so the GameObject origin is
                    // not the visual centre — pivoting there flings the slice off to one corner.
                    Vector3 slicePos = (anchorSliceAtVolumeCentre && volume != null)
                        ? VolumeCentre()
                        : position;
                    slicingPlane.transform.SetPositionAndRotation(slicePos, rotation * Quaternion.Euler(sliceOffsetEuler));
                }
            }
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
                case ViewAxis.Axial:   normal = superior;  up = -posterior; break; // top-down, anterior up
                case ViewAxis.Coronal: normal = posterior; up = superior;   break; // front-on, superior up
                default:               normal = left;      up = superior;   break; // side-on, superior up
            }

            // Never render upside-down: if the chosen up points downward in the world, flip it (and the
            // view normal with it, to keep a right-handed frame). Matters for datasets whose stored
            // orientation differs from the assumed anatomical convention (e.g. headerless RAW).
            if (Vector3.Dot(up, Vector3.up) < 0f)
                up = -up;

            Vector3 centre = VolumeCentre();
            float half = 0.9f * HalfExtentAlong(normal);
            Vector3 slicePos = centre + normal * Mathf.Lerp(-half, half, Mathf.Clamp01(depth01));

            // Orient the slice quad so its sampled cross-section faces the view axis. UVR's
            // SliceRenderingShader draws the quad in its local XY plane but SAMPLES the volume on its
            // local XZ plane (y=0), so a 90° rotation about X is REQUIRED to align what is sampled with
            // what is drawn — without it the sampling plane is edge-on to the quad and every fragment
            // reads outside the volume (all black). This must be baked in here rather than relying on
            // the serialized sliceOffsetEuler field: a scene-serialized SliceController deserialises
            // that field to (0,0,0) even though the C# default is (90,0,0), which is exactly why the
            // 3D scene rendered black while the AR path (runtime-added SliceController → C# default)
            // worked. sliceOffsetEuler remains available as an optional extra fine-tune.
            // The baked quad/sampling 90° also rotates the sampled image 90° relative to the camera's
            // up, so the slice appears lying on its side. Spin the plane about the VIEW NORMAL (the
            // quad's facing direction) to bring the anatomy upright — this rotates the displayed slice
            // relative to the fixed camera while keeping the quad square-on (so it stays visible, unlike
            // spinning via sliceOffsetEuler which is about the wrong axis and blacks the slice out).
            Quaternion viewSpin = Quaternion.AngleAxis(inPlaneSpinDeg, normal);
            slicingPlane.transform.SetPositionAndRotation(
                slicePos,
                viewSpin * Quaternion.LookRotation(normal, up) * Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(sliceOffsetEuler));

            if (cam != null)
            {
                if (camDistance <= 0f)
                {
                    // Frame the cross-section: place the camera so the volume's largest half-extent
                    // fills most of the view (pushing the slice quad's empty out-of-volume border off
                    // screen), instead of using the bounding-sphere diagonal (too far → tiny slice).
                    Vector3 e = volume.meshRenderer != null ? volume.meshRenderer.bounds.extents : Vector3.one * 0.5f;
                    float maxHalf = Mathf.Max(e.x, Mathf.Max(e.y, e.z));
                    camDistance = Mathf.Clamp(maxHalf * 2.0f, MinCamDistance, MaxCamDistance);
                }
                cam.transform.position = centre - normal * camDistance;
                cam.transform.rotation = Quaternion.LookRotation(normal, up);
            }
        }

        /// <summary>Dolly the CT-viewer camera in/out (zoom), clamped.</summary>
        public void ZoomBy(float factor)
        {
            if (camDistance <= 0f)
                camDistance = 2.6f;
            camDistance = Mathf.Clamp(camDistance / Mathf.Max(factor, 1e-3f), MinCamDistance, MaxCamDistance);
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
