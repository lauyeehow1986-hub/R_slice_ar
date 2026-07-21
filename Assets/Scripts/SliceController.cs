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
