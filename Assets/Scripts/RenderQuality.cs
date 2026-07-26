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
        /// <summary>Sampling-rate multiplier per level. UVR clamps this to its 0.2-2.0 shader range.</summary>
        private static float SamplingFor(QualityLevel level)
        {
            switch (level)
            {
                case QualityLevel.Low: return 0.3f;
                case QualityLevel.Medium: return 0.5f;
                default: return 1f;
            }
        }

        /// <summary>URP render scale per level.</summary>
        private static float RenderScaleFor(QualityLevel level)
        {
            switch (level)
            {
                case QualityLevel.Low: return 0.6f;
                case QualityLevel.Medium: return 0.8f;
                default: return 1f;
            }
        }

        /// <summary>Apply the current level to <paramref name="volume"/> and to the pipeline.</summary>
        public static void Apply(VolumeRenderedObject volume)
        {
            var level = VolumeSession.Quality;

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
