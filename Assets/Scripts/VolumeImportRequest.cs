using UnityEngine;
using UnityVolumeRendering;

namespace SliceAR
{
    /// <summary>
    /// A one-shot, cross-scene description of a user-initiated dataset import. The import UI fills this
    /// in and reloads the active scene; <see cref="VolumeFileLoader"/> consumes it on the next load
    /// (instead of the bundled sample) and then clears it. Static so it survives the scene reload,
    /// which gives us a clean teardown/rebuild of the volume, planes and AR rig for free.
    /// </summary>
    public static class VolumeImportRequest
    {
        public enum Kind { None, ImageSequence, Raw }

        public static Kind kind = Kind.None;

        // ImageSequence: ordered local file paths (one image per slice).
        public static string[] imagePaths;

        // Raw: a single local file path plus its layout.
        public static string rawPath;
        public static int dimX, dimY, dimZ;
        public static DataContentFormat contentFormat = DataContentFormat.Uint16;
        public static Endianness endianness = Endianness.LittleEndian;
        public static int skipBytes;

        // Applied to whichever kind is loaded.
        public static Vector3 voxelSizeMm = Vector3.one;
        public static VolumeFileLoader.TFPreset tfPreset = VolumeFileLoader.TFPreset.MRI;

        public static bool HasPending => kind != Kind.None;

        public static void Clear()
        {
            kind = Kind.None;
            imagePaths = null;
            rawPath = null;
        }
    }
}
