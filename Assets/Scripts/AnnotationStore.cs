using System.IO;
using UnityEngine;

namespace SliceAR
{
    /// <summary>
    /// Loads and saves a dataset's <see cref="AnnotationList"/> as JSON under
    /// <c>persistentDataPath/annotations/&lt;datasetId&gt;.json</c>, so markers persist across runs and
    /// each dataset keeps its own set. Failures are non-fatal (annotations are additive, never block a load).
    /// </summary>
    public static class AnnotationStore
    {
        private static string Dir => Path.Combine(Application.persistentDataPath, "annotations");

        private static string PathFor(string datasetId) => Path.Combine(Dir, Sanitize(datasetId) + ".json");

        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "default";
            foreach (char c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s;
        }

        public static AnnotationList Load(string datasetId)
        {
            try
            {
                string p = PathFor(datasetId);
                if (File.Exists(p))
                {
                    var list = JsonUtility.FromJson<AnnotationList>(File.ReadAllText(p));
                    if (list != null)
                    {
                        if (list.items == null)
                            list.items = new System.Collections.Generic.List<Annotation>();
                        return list;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("AnnotationStore.Load: " + e.Message);
            }
            return new AnnotationList();
        }

        public static void Save(string datasetId, AnnotationList list)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(PathFor(datasetId), JsonUtility.ToJson(list));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("AnnotationStore.Save: " + e.Message);
            }
        }
    }
}
