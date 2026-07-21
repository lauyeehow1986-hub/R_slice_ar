namespace SliceAR
{
    /// <summary>
    /// Cross-scene facts about the volume currently loaded, set by <see cref="VolumeFileLoader"/> and
    /// read by UI. Separate from <see cref="VolumeImportRequest"/> (which is cleared once consumed).
    /// </summary>
    public static class VolumeSession
    {
        /// <summary>
        /// True when the loaded volume carries a known anatomical orientation (a DICOM series, whose
        /// LPS convention the importer maps to Unity space). Headerless RAW and plain image sequences
        /// have no orientation metadata, so orientation markers would be meaningless for them.
        /// </summary>
        public static bool IsDicomOriented;
    }
}
