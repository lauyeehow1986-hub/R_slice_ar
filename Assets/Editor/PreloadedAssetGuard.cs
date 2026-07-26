using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SliceAR.EditorTools
{
    /// <summary>
    /// Guarantees the three assets AR depends on are in <c>preloadedAssets</c> at build time.
    ///
    /// The player loads XR from a preloaded <c>XRGeneralSettings</c> and draws camera passthrough with two
    /// preloaded ARCore background shaders. If that list is empty when a build runs, the app still compiles,
    /// installs and launches -- it just has no XR session and a black passthrough. There is no error to read,
    /// which is why this has cost days on this project before.
    ///
    /// The list does not stay put on its own. Any C# write to PlayerSettings followed by
    /// <c>AssetDatabase.SaveAssets()</c> empties it, and the editor's in-memory copy can diverge from the
    /// saved asset (observed: 3 entries on disk, 0 in memory -- and the build reads memory). Rather than
    /// catch that by eye before each commit, re-assert it as part of every build.
    /// </summary>
    public class PreloadedAssetGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        // GUIDs rather than paths: these live in package/generated locations that move between Unity versions.
        private static readonly string[] RequiredGuids =
        {
            "e027d43591f282e4ab77e5a5133b4333",   // XRGeneralSettings "Android XR Settings"
            "c9f956787b1d945e7b36e0516201fc76",   // Unlit/ARCoreBackground
            "0945859e5a1034c2cb6dce53cb4fb899",   // Unlit/ARCoreBackground/AfterOpaques
        };

        public void OnPreprocessBuild(BuildReport report)
        {
            Restore();
        }

        /// <summary>Add any missing required asset, preserving whatever else is already there.</summary>
        [MenuItem("Slice-AR/Restore Preloaded Assets")]
        public static void Restore()
        {
            var preloaded = new List<Object>(PlayerSettings.GetPreloadedAssets());
            preloaded.RemoveAll(o => o == null);   // stale refs to deleted assets serialise as null

            bool changed = false;
            foreach (string guid in RequiredGuids)
            {
                Object asset = Resolve(guid);
                if (asset == null)
                {
                    Debug.LogError("PreloadedAssetGuard: could not resolve " + guid +
                                   ". AR will not work in this build.");
                    continue;
                }
                if (!preloaded.Contains(asset))
                {
                    preloaded.Add(asset);
                    changed = true;
                }
            }

            if (!changed)
                return;

            PlayerSettings.SetPreloadedAssets(preloaded.ToArray());
            AssetDatabase.SaveAssets();
            Debug.Log("PreloadedAssetGuard: restored preloaded assets (now " + preloaded.Count + ").");
        }

        /// <summary>Local file ID of the editor-only settings container. Preloading this instead of the
        /// runtime object is the specific mistake that breaks AR: the player cannot deserialise it and
        /// reports a missing script and a serialization-layout mismatch rather than anything about XR.</summary>
        private const long EditorOnlySettingsFileId = 11400000L;

        /// <summary>
        /// The runtime object at a GUID. The XR settings GUID resolves to a file holding BOTH the editor-only
        /// <c>XRGeneralSettingsPerBuildTarget</c> (the main asset, and the wrong answer) and the runtime
        /// <c>XRGeneralSettings</c> sub-asset. Matching the type name loosely picks the wrong one, so require
        /// an exact runtime type and reject anything editor-side.
        /// </summary>
        private static Object Resolve(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path))
                return null;

            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o == null)
                    continue;

                System.Type type = o.GetType();
                if (type.FullName.StartsWith("UnityEditor."))
                    continue;
                if (!(o is Shader) && type.Name != "XRGeneralSettings")
                    continue;

                // Belt and braces: even a correctly-typed object is wrong if it is the editor container.
                string outGuid;
                long fileId;
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out outGuid, out fileId) &&
                    fileId == EditorOnlySettingsFileId)
                    continue;

                return o;
            }
            return null;
        }
    }
}
