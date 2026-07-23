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

        /// <summary>Diagnostic note about how the 3D <see cref="MotionSlicer"/> was obtained at
        /// runtime (found on the object, added at runtime, or the error if it could not be created).
        /// Surfaced by the on-screen build stamp to explain a missing slicer on device.</summary>
        public static string SlicerNote = "";

        /// <summary>Append a load-pipeline checkpoint to persistentDataPath/slicer_diag.txt and mirror
        /// the latest step to <see cref="SlicerNote"/>. The device throttles Unity logcat under
        /// ARCore's spam and a release APK cannot be read via run-as, so a pullable append-only file
        /// is the reliable way to see how far the (possibly aborting) load coroutine got. Never
        /// throws — diagnostics must not affect app behaviour.</summary>
        public static void Diag(string step, bool reset = false)
        {
            SlicerNote = step;
            try
            {
                string path = System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "slicer_diag.txt");
                string line = System.DateTime.Now.ToString("HH:mm:ss.fff") + "  " + step + "\n";
                if (reset)
                    System.IO.File.WriteAllText(path, line);
                else
                    System.IO.File.AppendAllText(path, line);
            }
            catch { /* diagnostics only */ }
        }
    }
}
