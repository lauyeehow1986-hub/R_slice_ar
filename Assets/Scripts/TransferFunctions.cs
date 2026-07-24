using UnityEngine;
using UnityVolumeRendering;

namespace SliceAR
{
    /// <summary>
    /// Available colour lookup tables (pseudo-colour maps). A LUT maps the volume's *luminance*
    /// (produced by the window preset below) to a colour, so the intensity windowing stays intact
    /// while the palette changes. Grayscale is the plain radiological look; the others are the
    /// classic medical false-colour maps used to bring out subtle intensity differences.
    /// </summary>
    public enum ColorLUT { Grayscale, HotMetal, Rainbow, Cool }

    /// <summary>
    /// Builds <see cref="UnityVolumeRendering.TransferFunction"/>s from two independent choices:
    ///  - a <b>window preset</b> (CT / MRI / linear) that defines the luminance + opacity ramp
    ///    across normalised intensity, and
    ///  - a <b>colour LUT</b> that recolours that luminance.
    /// This keeps the carefully-tuned CT/MRI windows in one place and lets the runtime LUT picker
    /// re-skin them without disturbing the windowing. The slice shader forces alpha = 1 and uses
    /// only RGB, so the colour ramp drives the flat slice; the 3D DVR uses both colour and alpha.
    /// </summary>
    public static class TransferFunctions
    {
        // A luminance control point: normalised intensity -> grey level (0..1), later colourised.
        private struct Lum
        {
            public float t;
            public float v;
            public Lum(float t, float v) { this.t = t; this.v = v; }
        }

        private struct Alpha
        {
            public float t;
            public float a;
            public Alpha(float t, float a) { this.t = t; this.a = a; }
        }

        /// <summary>
        /// Build a transfer function for the given intensity <paramref name="window"/> and colour
        /// <paramref name="lut"/>. The window sets the luminance/opacity ramp; the LUT recolours it.
        /// </summary>
        public static UnityVolumeRendering.TransferFunction Build(VolumeFileLoader.TFPreset window, ColorLUT lut)
        {
            var tf = ScriptableObject.CreateInstance<UnityVolumeRendering.TransferFunction>();
            tf.colourControlPoints.Clear();
            tf.alphaControlPoints.Clear();

            foreach (var l in Luminance(window))
                tf.colourControlPoints.Add(new TFColourControlPoint(l.t, Sample(lut, l.v)));
            foreach (var a in Opacity(window))
                tf.alphaControlPoints.Add(new TFAlphaControlPoint(a.t, a.a));

            tf.GenerateTexture();
            return tf;
        }

        // --- Window presets: luminance + opacity ramps across normalised intensity [0..1] ------------

        private static Lum[] Luminance(VolumeFileLoader.TFPreset window)
        {
            switch (window)
            {
                case VolumeFileLoader.TFPreset.CT:
                    // Air near 0, a soft-tissue spike ~0.32, a thin bright bone tail > 0.75.
                    return new[]
                    {
                        new Lum(0.00f, 0.00f), new Lum(0.11f, 0.00f), new Lum(0.20f, 0.16f),
                        new Lum(0.32f, 0.38f), new Lum(0.50f, 0.57f), new Lum(0.72f, 0.84f),
                        new Lum(0.85f, 0.97f), new Lum(1.00f, 1.00f),
                    };
                case VolumeFileLoader.TFPreset.MRI:
                    // ~half air at 0; all tissue packed into ~0.05..0.55 (no bright bone tail).
                    return new[]
                    {
                        new Lum(0.00f, 0.00f), new Lum(0.04f, 0.00f), new Lum(0.12f, 0.14f),
                        new Lum(0.24f, 0.48f), new Lum(0.36f, 0.74f), new Lum(0.50f, 0.95f),
                        new Lum(0.62f, 1.00f), new Lum(1.00f, 1.00f),
                    };
                default: // PluginDefault -> a plain linear window so a chosen LUT still has a ramp.
                    return new[] { new Lum(0.00f, 0.00f), new Lum(1.00f, 1.00f) };
            }
        }

        private static Alpha[] Opacity(VolumeFileLoader.TFPreset window)
        {
            switch (window)
            {
                case VolumeFileLoader.TFPreset.CT:
                    return new[]
                    {
                        new Alpha(0.00f, 0.00f), new Alpha(0.11f, 0.00f), new Alpha(0.22f, 0.02f),
                        new Alpha(0.32f, 0.06f), new Alpha(0.50f, 0.15f), new Alpha(0.70f, 0.55f),
                        new Alpha(1.00f, 0.95f),
                    };
                case VolumeFileLoader.TFPreset.MRI:
                    return new[]
                    {
                        new Alpha(0.00f, 0.00f), new Alpha(0.05f, 0.00f), new Alpha(0.14f, 0.06f),
                        new Alpha(0.25f, 0.22f), new Alpha(0.40f, 0.55f), new Alpha(0.55f, 0.85f),
                        new Alpha(1.00f, 0.95f),
                    };
                default:
                    return new[]
                    {
                        new Alpha(0.00f, 0.00f), new Alpha(0.10f, 0.00f), new Alpha(0.50f, 0.40f),
                        new Alpha(1.00f, 0.90f),
                    };
            }
        }

        // --- Colour LUTs: map a luminance value to a colour --------------------------------------------

        // Gradient stops (position 0..1 -> colour). Luminance is looked up along the gradient.
        private static readonly (float pos, Color col)[] HotMetal =
        {
            (0.00f, new Color(0f, 0f, 0f)),
            (0.35f, new Color(0.72f, 0.00f, 0.00f)),
            (0.60f, new Color(1.00f, 0.45f, 0.00f)),
            (0.82f, new Color(1.00f, 0.90f, 0.25f)),
            (1.00f, new Color(1.00f, 1.00f, 1.00f)),
        };

        private static readonly (float pos, Color col)[] Rainbow =
        {
            (0.00f, new Color(0f, 0f, 0f)),
            (0.12f, new Color(0.20f, 0.00f, 0.55f)),
            (0.30f, new Color(0.00f, 0.35f, 1.00f)),
            (0.48f, new Color(0.00f, 0.85f, 0.90f)),
            (0.62f, new Color(0.20f, 0.85f, 0.20f)),
            (0.78f, new Color(1.00f, 0.90f, 0.00f)),
            (0.90f, new Color(1.00f, 0.45f, 0.00f)),
            (1.00f, new Color(1.00f, 0.00f, 0.00f)),
        };

        private static readonly (float pos, Color col)[] CoolMap =
        {
            (0.00f, new Color(0f, 0f, 0f)),
            (0.25f, new Color(0.00f, 0.40f, 0.70f)),
            (0.55f, new Color(0.10f, 0.75f, 0.85f)),
            (0.80f, new Color(0.55f, 0.50f, 0.95f)),
            (1.00f, new Color(1.00f, 0.40f, 1.00f)),
        };

        private static Color Sample(ColorLUT lut, float lum)
        {
            lum = Mathf.Clamp01(lum);
            switch (lut)
            {
                case ColorLUT.HotMetal: return SampleGradient(HotMetal, lum);
                case ColorLUT.Rainbow:  return SampleGradient(Rainbow, lum);
                case ColorLUT.Cool:     return SampleGradient(CoolMap, lum);
                default:                return new Color(lum, lum, lum);   // Grayscale
            }
        }

        private static Color SampleGradient((float pos, Color col)[] stops, float t)
        {
            if (t <= stops[0].pos)
                return stops[0].col;
            for (int i = 1; i < stops.Length; i++)
            {
                if (t <= stops[i].pos)
                {
                    float f = Mathf.InverseLerp(stops[i - 1].pos, stops[i].pos, t);
                    return Color.Lerp(stops[i - 1].col, stops[i].col, f);
                }
            }
            return stops[stops.Length - 1].col;
        }
    }
}
