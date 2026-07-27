using UnityEngine;
using UnityEngine.Rendering;
using UnityVolumeRendering;

namespace SliceAR
{
    /// <summary>
    /// Faint wireframe box around the anchored volume in AR Slice mode.
    ///
    /// Slice mode hides the volume's own renderer (see <see cref="SliceController.hideVolumeInSliceMode"/>),
    /// which leaves a flat slice image floating in the room with nothing around it. The whole interaction
    /// is walking the device into the anatomy, and at that point the anatomy is invisible — so the user is
    /// aiming at something they cannot see, and only finds the edge by passing through it. The box restores
    /// the one cue that matters (where the volume sits and how big it is) without putting the expensive
    /// raymarched render back on screen.
    ///
    /// Clip mode already draws the volume itself plus the cut-plane indicator, so the box is hidden there
    /// rather than added to the clutter.
    ///
    /// Self-contained: <see cref="ARModeController"/> adds it at runtime, so it exists only in the AR scene
    /// and needs no scene wiring.
    /// </summary>
    public class ARVolumeOutline : MonoBehaviour
    {
        // Corner indices: 0-3 the -Y face (counter-clockwise), 4-7 the +Y face directly above them.
        // A single polyline cannot cover all 12 edges of a box without retracing; this tour retraces
        // three, which is the minimum, and costs one LineRenderer instead of twelve.
        private static readonly int[] Tour = { 0, 1, 2, 3, 0, 4, 5, 6, 7, 4, 5, 1, 2, 6, 7, 3 };

        // Line thickness as a fraction of the box's diagonal, so it reads the same whether the volume is
        // at the default tabletop scale or a large imported dataset with an edited voxel size.
        private const float WidthFraction = 0.006f;
        private const float MinWidth = 0.0008f;
        private const float MaxWidth = 0.02f;

        private VolumeRenderedObject volume;
        private SliceController controller;
        private LineRenderer line;
        private Material lineMaterial;
        private readonly Vector3[] corners = new Vector3[8];
        private readonly Vector3[] path = new Vector3[Tour.Length];

        private void Start()
        {
            BuildLine();
        }

        private void OnDestroy()
        {
            if (lineMaterial != null)
                Destroy(lineMaterial);
            if (line != null)
                Destroy(line.gameObject);
        }

        private void Update()
        {
            if (line == null)
                return;

            if (volume == null)
                volume = UnityEngine.Object.FindObjectOfType<VolumeRenderedObject>();
            if (controller == null)
                controller = GetComponent<SliceController>();

            Transform frame = Frame();
            bool show = frame != null && controller != null && controller.Mode == SliceController.SliceMode.Slice;
            if (line.enabled != show)
                line.enabled = show;
            if (!show)
                return;

            // Rebuilt every frame rather than parented to the volume: the anchor's pose is owned by
            // ARCore and gets corrected as it refines its map, and the volume is re-parented outright on
            // Recenter. Eight TransformPoint calls a frame is cheaper than tracking either of those.
            Bounds local = volume.meshRenderer.localBounds;
            Vector3 c = local.center;
            Vector3 e = local.extents;
            corners[0] = frame.TransformPoint(new Vector3(c.x - e.x, c.y - e.y, c.z - e.z));
            corners[1] = frame.TransformPoint(new Vector3(c.x + e.x, c.y - e.y, c.z - e.z));
            corners[2] = frame.TransformPoint(new Vector3(c.x + e.x, c.y - e.y, c.z + e.z));
            corners[3] = frame.TransformPoint(new Vector3(c.x - e.x, c.y - e.y, c.z + e.z));
            corners[4] = frame.TransformPoint(new Vector3(c.x - e.x, c.y + e.y, c.z - e.z));
            corners[5] = frame.TransformPoint(new Vector3(c.x + e.x, c.y + e.y, c.z - e.z));
            corners[6] = frame.TransformPoint(new Vector3(c.x + e.x, c.y + e.y, c.z + e.z));
            corners[7] = frame.TransformPoint(new Vector3(c.x - e.x, c.y + e.y, c.z + e.z));

            for (int i = 0; i < Tour.Length; i++)
                path[i] = corners[Tour[i]];
            line.SetPositions(path);

            float diagonal = Vector3.Distance(corners[0], corners[6]);
            float width = Mathf.Clamp(diagonal * WidthFraction, MinWidth, MaxWidth);
            line.widthMultiplier = width;
        }

        /// <summary>The transform the volume's mesh lives in (the one carrying the importer's baked
        /// scale), or null while no volume is loaded.</summary>
        private Transform Frame()
        {
            if (volume == null || volume.meshRenderer == null)
                return null;
            return volume.meshRenderer.transform;
        }

        private void BuildLine()
        {
            var shader = Resources.Load<Shader>("Shaders/SliceARUnlitLine");
            if (shader == null)
            {
                // Only reachable if the shader asset is missing or failed to compile. The outline is a
                // convenience, so say so once and stay out of the way rather than throwing every frame.
                Debug.LogWarning("ARVolumeOutline: SliceAR/UnlitLine shader not found; outline disabled.");
                enabled = false;
                return;
            }

            var go = new GameObject("ARVolumeOutline");
            line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;          // positions are rebuilt in world space each frame
            line.loop = false;
            line.positionCount = Tour.Length;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            line.alignment = LineAlignment.View;   // ribbon faces the camera, so edges keep an even
                                                   // thickness from any angle the user walks round to
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;

            lineMaterial = new Material(shader);
            line.material = lineMaterial;

            // Faint, and the same cyan as the off-screen indicator arrow, so the two read as one system
            // of "where the volume is" cues. Low alpha on purpose: this is a hint about empty space, and
            // a solid cage would compete with the slice it surrounds.
            var tint = new Color(0.3f, 0.85f, 1f, 0.3f);
            line.startColor = tint;
            line.endColor = tint;
            line.enabled = false;
        }
    }
}
