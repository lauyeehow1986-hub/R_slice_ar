using UnityEngine;
using UnityEngine.UI;
using UnityVolumeRendering;

namespace SliceAR
{
    /// <summary>
    /// Shows an arrow at the screen edge pointing toward the anchored volume whenever it is off-screen (or
    /// behind the user), with its distance in metres. The volume stays anchored in the room — that is what
    /// makes AR slicing work, since the user pushes the device into it — so moving around naturally takes it
    /// out of view. This indicator lets the user turn back to it instead of re-anchoring with Recenter.
    ///
    /// Self-contained: <see cref="ARModeController"/> adds it at runtime, so it only exists in the AR scene
    /// and needs no scene wiring. The arrow sprite is generated in code (no art asset to import or strip).
    /// </summary>
    public class ARVolumeIndicator : MonoBehaviour
    {
        // Keep the arrow this far inside the screen edge, and treat the volume as "off screen" once its
        // projected point is within this margin of the edge (so the arrow appears just before it exits).
        private const float EdgeMargin = 120f;

        private VolumeRenderedObject volume;
        private Camera cam;
        private RectTransform arrow;
        private Text distanceLabel;

        private void Start()
        {
            BuildUI();
        }

        private void Update()
        {
            if (arrow == null)
                return;
            if (cam == null)
                cam = Camera.main;
            if (volume == null)
                volume = Object.FindObjectOfType<VolumeRenderedObject>();

            if (cam == null || volume == null)
            {
                SetVisible(false);
                return;
            }

            Vector3 target = TargetWorldPosition();
            Vector3 sp = cam.WorldToScreenPoint(target);

            // Behind the camera projects to a mirrored point with negative z; flip it so the direction we
            // derive still points the correct way round the screen.
            bool behind = sp.z < 0f;
            if (behind)
            {
                sp.x = Screen.width - sp.x;
                sp.y = Screen.height - sp.y;
            }

            bool onScreen = !behind
                            && sp.x >= EdgeMargin && sp.x <= Screen.width - EdgeMargin
                            && sp.y >= EdgeMargin && sp.y <= Screen.height - EdgeMargin;
            if (onScreen)
            {
                SetVisible(false);
                return;
            }

            Vector2 centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 dir = new Vector2(sp.x, sp.y) - centre;
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector2.up;
            dir.Normalize();

            // Push the arrow out from the centre until it meets the inset screen rectangle.
            float halfW = Screen.width * 0.5f - EdgeMargin;
            float halfH = Screen.height * 0.5f - EdgeMargin;
            float sx = Mathf.Abs(dir.x) < 1e-5f ? float.MaxValue : halfW / Mathf.Abs(dir.x);
            float sy = Mathf.Abs(dir.y) < 1e-5f ? float.MaxValue : halfH / Mathf.Abs(dir.y);
            Vector2 pos = centre + dir * Mathf.Min(sx, sy);

            SetVisible(true);
            arrow.position = new Vector3(pos.x, pos.y, 0f);
            // Sprite points up (+Y) by default.
            arrow.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);

            if (distanceLabel != null)
            {
                float metres = Vector3.Distance(cam.transform.position, target);
                distanceLabel.text = metres.ToString("0.0") + " m";
                // Keep the label inside the arrow's edge, offset back toward the screen centre.
                distanceLabel.rectTransform.position = new Vector3(pos.x - dir.x * 70f, pos.y - dir.y * 70f, 0f);
            }
        }

        /// <summary>World point to aim at: the rendered volume's centre, falling back to its transform.</summary>
        private Vector3 TargetWorldPosition()
        {
            var mr = volume.meshRenderer;
            return mr != null ? mr.bounds.center : volume.transform.position;
        }

        private void SetVisible(bool on)
        {
            if (arrow != null && arrow.gameObject.activeSelf != on)
                arrow.gameObject.SetActive(on);
            if (distanceLabel != null && distanceLabel.gameObject.activeSelf != on)
                distanceLabel.gameObject.SetActive(on);
        }

        private void BuildUI()
        {
            var canvasGO = new GameObject("ARVolumeIndicatorCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = UnityEngine.RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 280;   // under the annotation markers/buttons, over the AR view

            var arrowGO = new GameObject("Arrow");
            arrowGO.transform.SetParent(canvasGO.transform, false);
            var img = arrowGO.AddComponent<Image>();
            img.sprite = CreateTriangleSprite();
            img.color = new Color(0.3f, 0.85f, 1f, 0.9f);
            img.raycastTarget = false;
            arrow = arrowGO.GetComponent<RectTransform>();
            arrow.sizeDelta = new Vector2(90f, 90f);
            arrowGO.SetActive(false);

            var labelGO = new GameObject("Distance");
            labelGO.transform.SetParent(canvasGO.transform, false);
            distanceLabel = labelGO.AddComponent<Text>();
            distanceLabel.font = AppFont.Get();
            distanceLabel.fontSize = 32;
            distanceLabel.fontStyle = FontStyle.Bold;
            distanceLabel.alignment = TextAnchor.MiddleCenter;
            distanceLabel.color = new Color(0.3f, 0.85f, 1f, 0.9f);
            distanceLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            distanceLabel.verticalOverflow = VerticalWrapMode.Overflow;
            distanceLabel.raycastTarget = false;
            distanceLabel.rectTransform.sizeDelta = new Vector2(160f, 44f);
            labelGO.SetActive(false);
        }

        /// <summary>Build a solid upward-pointing triangle sprite in code, so no art asset is needed.</summary>
        private static Sprite CreateTriangleSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            var clear = new Color32(255, 255, 255, 0);
            var solid = new Color32(255, 255, 255, 255);
            for (int y = 0; y < size; y++)
            {
                // Width tapers linearly from the full base (y = 0) to the apex (y = size - 1).
                float t = (float)y / (size - 1);
                float halfWidth = (1f - t) * (size * 0.5f);
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = Mathf.Abs(x - size * 0.5f) <= halfWidth ? solid : clear;
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
