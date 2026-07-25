using System;
using System.Collections.Generic;
using UnityEngine;

namespace SliceAR
{
    /// <summary>
    /// A single labelled marker pinned to a point in the volume. <see cref="localPos"/> is in the
    /// volume container's local (mesh) space — the unit cube [-0.5..0.5] on each axis — so the marker
    /// stays glued to the same anatomy as the volume is moved/anchored (AR) or held fixed (3D).
    /// </summary>
    [Serializable]
    public class Annotation
    {
        public string id;
        public string label;
        public Vector3 localPos;
    }

    /// <summary>Serialisable container for a dataset's markers (JsonUtility needs a wrapper type).</summary>
    [Serializable]
    public class AnnotationList
    {
        public List<Annotation> items = new List<Annotation>();
    }
}
