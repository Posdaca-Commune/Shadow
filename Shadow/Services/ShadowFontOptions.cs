using System.Collections.Generic;
using Avalonia.Media;

namespace Shadow.Services;

/// <summary>
/// Keeps Inter for Latin UI text while forcing Simplified Chinese CJK glyphs
/// onto Microsoft YaHei, avoiding Japanese/Korean-looking fallbacks after language switches.
/// </summary>
internal static class ShadowFontOptions
{
    // CJK punctuation, Ext-A, Unified Ideographs, compatibility ideographs, half/fullwidth forms.
    private static readonly UnicodeRange SimplifiedChineseRange = UnicodeRange.Parse(
        "U+3000-303F,U+3400-4DBF,U+4E00-9FFF,U+F900-FAFF,U+FF00-FFEF");

    public static FontManagerOptions Create()
    {
        return new FontManagerOptions
        {
            FontFallbacks = CreateCjkFallbacks(),
        };
    }

    private static IReadOnlyList<FontFallback> CreateCjkFallbacks()
    {
        return
        [
            CreateFallback("Microsoft YaHei UI"),
            CreateFallback("Microsoft YaHei"),
            CreateFallback("微软雅黑"),
            CreateFallback("Segoe UI"),
        ];
    }

    private static FontFallback CreateFallback(string familyName)
    {
        return new FontFallback
        {
            FontFamily = new FontFamily(familyName),
            UnicodeRange = SimplifiedChineseRange,
        };
    }
}
