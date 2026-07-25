using UnityEngine;

namespace SliceAR
{
    /// <summary>
    /// Supplies the font for the code-built UI, chosen for the current language's script. The legacy UGUI
    /// <c>Text</c> uses Unity's built-in "LegacyRuntime.ttf" (Arial), which has no CJK or Tamil glyphs — so
    /// Chinese / Japanese / Tamil would render as empty boxes. We instead load an OS font that covers the
    /// active language's script via <see cref="Font.CreateDynamicFontFromOSFont(string[],int)"/>, listing
    /// Android system fonts first (present on device) and Windows fonts after (so text still previews in the
    /// editor). CJK and Tamil fonts also contain Latin glyphs, so digits, "mm", and the R/L/A/P/S/I markers
    /// keep rendering when one of those languages is active.
    ///
    /// The chosen font is cached and only rebuilt when the language changes (a Text keeps a reference to the
    /// Font object it was given, so callers must reassign <see cref="Get"/> to their Text on a language
    /// change — the localized UI components do this in their refresh handlers).
    ///
    /// NOTE: on-device glyph coverage depends on which Noto fonts the specific Android build ships. This is
    /// the one localization item that must be smoke-tested on hardware; if CJK/Tamil show as boxes, the fix
    /// is to bundle a Noto subset TTF and load it here instead.
    /// </summary>
    public static class AppFont
    {
        private static Font cached;
        private static Language cachedLang = (Language)(-1);

        /// <summary>Font appropriate for the current language; rebuilt when the language changes.</summary>
        public static Font Get()
        {
            if (cached != null && cachedLang == Loc.Current)
                return cached;
            cachedLang = Loc.Current;
            cached = Build(Loc.Current);
            return cached;
        }

        private static Font Build(Language lang)
        {
            try
            {
                var f = Font.CreateDynamicFontFromOSFont(Candidates(lang), 48);
                if (f != null)
                    return f;
            }
            catch { /* fall through to the built-in font */ }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        // First name that resolves on the platform wins. Android names first, then Windows-editor names.
        private static string[] Candidates(Language lang)
        {
            switch (lang)
            {
                case Language.JA:
                    return new[] { "Noto Sans CJK JP", "Noto Sans JP", "Yu Gothic UI", "Yu Gothic",
                                   "Meiryo", "MS Gothic", "sans-serif" };
                case Language.ZH:
                    return new[] { "Noto Sans CJK SC", "Noto Sans SC", "Microsoft YaHei UI",
                                   "Microsoft YaHei", "SimSun", "sans-serif" };
                case Language.TA:
                    return new[] { "Noto Sans Tamil", "Noto Serif Tamil", "Nirmala UI", "Latha",
                                   "sans-serif" };
                default:
                    return new[] { "Roboto", "Noto Sans", "Segoe UI", "Arial", "Helvetica", "sans-serif" };
            }
        }
    }
}
