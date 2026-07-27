using UnityEngine;
using UnityEngine.UI;

namespace SliceAR
{
    /// <summary>
    /// Keeps a <see cref="CanvasScaler"/>'s reference resolution the same way round as the screen.
    ///
    /// A CanvasScaler left at its defaults matches width only, which is fine as long as the screen stays
    /// the shape the reference resolution assumes. This app auto-rotates, so it does not: against a
    /// 1080x1920 reference a 2400x1080 landscape screen scales the whole UI by 2400/1080 = 2.22, and the
    /// layout collapses -- buttons twice the intended size, the top-right column running off the bottom
    /// of the screen, the bottom rows pushed into the top half.
    ///
    /// Turning the reference on its side in landscape and switching to Expand puts both orientations at
    /// scale 1.0, so a control is the same physical size whichever way the phone is held. Expand also
    /// guarantees the whole reference area stays on screen on any aspect ratio, which matters from target
    /// API 36: Android 16 ignores an app's orientation restriction on large screens, so landscape has to
    /// work on a tablet whether or not the app asks for it.
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    public class OrientationScaler : MonoBehaviour
    {
        private const float ShortSide = 1080f;
        private const float LongSide = 1920f;

        private CanvasScaler scaler;
        private bool applied;
        private bool appliedLandscape;

        /// <summary>Adds a scaler configured for both orientations to <paramref name="canvasGO"/>.</summary>
        public static CanvasScaler Attach(GameObject canvasGO)
        {
            var cs = canvasGO.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasGO.AddComponent<OrientationScaler>();
            return cs;
        }

        private void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
            Apply();
        }

        private void Update()
        {
            if (!applied || IsLandscape() != appliedLandscape)
                Apply();
        }

        private static bool IsLandscape()
        {
            return Screen.width > Screen.height;
        }

        private void Apply()
        {
            appliedLandscape = IsLandscape();
            applied = true;
            scaler.referenceResolution = appliedLandscape
                ? new Vector2(LongSide, ShortSide)
                : new Vector2(ShortSide, LongSide);
        }
    }
}
