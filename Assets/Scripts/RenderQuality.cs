using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityVolumeRendering;

namespace SliceAR
{
    /// <summary>
    /// Render-cost preset for the direct volume renderer.
    ///
    /// On-device measurement showed the volume raymarch is fill-rate bound: the identical volume with
    /// identical shading costs ~35 ms in AR (where it is a small object 0.6 m away) but ~220 ms in the
    /// 3D CT-viewer, where the camera is framed so it fills the screen. Slice mode, which hides the
    /// volume entirely, sits at the 30 FPS cap — so the cost is all in the raymarch, and it scales with
    /// the pixels the volume covers and the samples taken along each ray.
    ///
    /// Two knobs, both of which cut work per covered pixel:
    ///  - <b>sampling rate</b>: UVR's own <c>_SamplingRateMultiplier</c> against its 512-step ceiling.
    ///    512 steps is 2x Nyquist for a 256-cubed volume, so 0.5 (256 steps) is the point where extra
    ///    samples stop adding real detail. The shader compensates opacity by 1/rate, so lowering it
    ///    keeps overall density rather than washing the image out.
    ///  - <b>render scale</b>: URP's resolution multiplier. Quadratic, so it is the stronger lever, and
    ///    a volume render is soft enough that a modest downscale is far less visible than on UI or text
    ///    (the UI canvases are screen-space overlay, so they stay at native resolution regardless).
    /// </summary>
    public enum QualityLevel
    {
        High,
        Medium,
        Low,
    }

    public static class RenderQuality
    {
        /// <summary>
        /// Sampling-rate multiplier per level. UVR clamps this to its 0.2-2.0 shader range.
        ///
        /// Sampling is the more expensive knob to economise on than it first looks. UVR jitters each ray's
        /// start position to break up banding, and the jitter magnitude scales with step size, so
        /// under-sampling does not read as stripes -- it reads as grain, and the grain grows as the volume
        /// is enlarged on screen. Device testing at 0.3 (154 samples, a random offset of ~5.6% of the
        /// volume extent per ray) was visibly grainy when zoomed in, so Low buys samples back and pays for
        /// them with resolution instead.
        /// </summary>
        private static float SamplingFor(QualityLevel level)
        {
            switch (level)
            {
                case QualityLevel.Low: return 0.45f;
                default: return 0.5f;   // Medium and High both -- see the note above RenderScaleFor
            }
        }

        /// <summary>
        /// URP render scale per level. Quadratic, so this is where Low takes its cut -- and above Low it is
        /// the only lever that still does anything.
        ///
        /// Comparing device screenshots of the three presets on the volume region: Low to Medium changes the
        /// image substantially (mean absolute difference 10.9/255, sharpness +34%), but Medium to High barely
        /// registers (1.75/255) while costing 3.7x the frame time -- 75 ms against 278 ms. Past ~256 ray steps
        /// the march is already finer than the screen resolves at these sizes, so the extra samples mostly
        /// re-read the same texels. High therefore drops to Medium's sampling rate and distinguishes itself
        /// on resolution alone, which turns a 278 ms preset into roughly 120 ms for an image nobody could
        /// pick out of a line-up.
        ///
        /// Caveat on that measurement: it came from JPEG screenshots of a volume filling about a third of the
        /// screen, so a 1.75 difference is close to the compression noise floor. It bounds how much High was
        /// buying at normal viewing sizes; it does not prove the sampling rate never matters when zoomed deep
        /// into a large dataset.
        /// </summary>
        private static float RenderScaleFor(QualityLevel level)
        {
            switch (level)
            {
                case QualityLevel.Low: return 0.5f;
                case QualityLevel.Medium: return 0.65f;
                default: return 1f;
            }
        }

        /// <summary>Target frame rate. 30 is the right ceiling for a volume renderer: the raymarch is the
        /// whole cost and doubling it buys nothing diagnostically, while the heat and battery it burns are
        /// real on a phone held up for AR.</summary>
        public const int TargetFrameRate = 30;

        /// <summary>Apply the current level to <paramref name="volume"/> and to the pipeline.</summary>
        public static void Apply(VolumeRenderedObject volume)
        {
            var level = VolumeSession.Quality;

            // Set the cap explicitly rather than inheriting it. Today 3D mode runs at 30 only because the
            // app launches into AR and AR Foundation pins the rate to the ARCore camera's 30 Hz, which then
            // survives the scene switch. Enter 3D first (or reorder the build scenes) and that cap silently
            // disappears, letting Slice mode -- which is idle at the cap -- render uncapped for no benefit.
            if (Application.targetFrameRate != TargetFrameRate)
                Application.targetFrameRate = TargetFrameRate;

            if (volume != null)
                volume.SetSamplingRateMultiplier(SamplingFor(level));

            // Render scale lives on the URP asset, which is a project asset: writing to it in the editor
            // dirties the asset and would bake a downscale into ProjectSettings the moment anyone saves.
            // The measurement that matters happens on hardware anyway, so only touch it in a player.
            if (Application.isEditor)
                return;

            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
                urp.renderScale = RenderScaleFor(level);
        }

        /// <summary>Advance to the next level, wrapping. Returns the new level.</summary>
        public static QualityLevel Cycle()
        {
            var values = (QualityLevel[])System.Enum.GetValues(typeof(QualityLevel));
            VolumeSession.Quality = values[(((int)VolumeSession.Quality) + 1) % values.Length];
            return VolumeSession.Quality;
        }

        /// <summary>Localization key for the current level's short name.</summary>
        public static string LabelKey(QualityLevel level)
        {
            switch (level)
            {
                case QualityLevel.Low: return "quality.low";
                case QualityLevel.Medium: return "quality.med";
                default: return "quality.high";
            }
        }
    }
}
