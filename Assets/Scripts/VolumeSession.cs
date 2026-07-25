using UnityEngine;

namespace SliceAR
{
    /// <summary>
    /// Cross-scene facts about the volume currently loaded, set by <see cref="VolumeFileLoader"/> and
    /// read by UI / the 3D CT-viewer. Separate from <see cref="VolumeImportRequest"/> (which is cleared
    /// once consumed).
    /// </summary>
    public static class VolumeSession
    {
        /// <summary>
        /// True when the loaded volume carries a known anatomical orientation (a DICOM series, whose
        /// LPS convention the importer maps to Unity space). Headerless RAW and plain image sequences
        /// have no orientation metadata, so orientation markers would be meaningless for them.
        /// </summary>
        public static bool IsDicomOriented;

        // Which dataset-local axis points to each patient direction, so the 3D CT-viewer can label its
        // planes (Axial/Coronal/Sagittal) and the orientation markers correctly for volumes whose grid
        // is not stored in the DICOM convention. Defaults are the DICOM/importer convention
        // (+X=Left, +Y=Posterior, +Z=Superior); VolumeFileLoader overrides them per dataset (the
        // bundled MRHead is a sagittal acquisition with a different grid order).
        public static Vector3 AxisLeft = Vector3.right;
        public static Vector3 AxisPosterior = Vector3.up;
        public static Vector3 AxisSuperior = Vector3.forward;

        /// <summary>Reset the anatomical axis mapping to the DICOM/importer convention.</summary>
        public static void ResetAxes()
        {
            AxisLeft = Vector3.right;
            AxisPosterior = Vector3.up;
            AxisSuperior = Vector3.forward;
        }

        /// <summary>
        /// The intensity window preset applied to the current volume (set by <see cref="VolumeFileLoader"/>
        /// at load). The runtime LUT picker rebuilds the transfer function from this plus <see cref="ColorLUT"/>,
        /// so recolouring never disturbs the windowing.
        /// </summary>
        public static VolumeFileLoader.TFPreset WindowPreset = VolumeFileLoader.TFPreset.MRI;

        /// <summary>Currently selected colour lookup table. Static so the choice persists across the
        /// scene reload that a dataset import / AR↔3D switch performs.</summary>
        public static ColorLUT ColorLUT = ColorLUT.Grayscale;

        /// <summary>Whether the user has dismissed the "not for diagnosis" disclaimer this run. Static
        /// (not persisted) so the disclaimer shows once per app launch but not on every scene switch.</summary>
        public static bool DisclaimerAcknowledged;

        /// <summary>Stable identifier for the loaded dataset (bundled file name, or an import's file
        /// name). Annotations are stored/loaded keyed by this so each dataset keeps its own markers.</summary>
        public static string DatasetId = "default";

        /// <summary>Full physical extent of the dataset in millimetres (dim × voxel size per axis). Used
        /// to turn marker separations (measured in the mesh-local unit cube) into a distance in mm. For
        /// DICOM this depends on the voxel size in effect, which the user can edit on import.</summary>
        public static Vector3 PhysicalSizeMm = new Vector3(256f, 256f, 169f);
    }
}
