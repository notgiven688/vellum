using Xunit;

namespace Vellum.Tests;

public sealed class TrueTypeFontTests
{
    private static TrueTypeFont Font => UiFonts.DefaultSans;

    [Fact]
    public void ScaleForPixelHeight_Matches_Font_Vertical_Metrics()
    {
        FontVMetrics vm = Font.GetFontVMetrics();
        float scale = Font.ScaleForPixelHeight(18f);
        float scaledHeight = (vm.Ascent - vm.Descent) * scale;

        Assert.InRange(scaledHeight, 17.999f, 18.001f);
    }

    [Fact]
    public void FindGlyphIndex_Returns_Zero_For_Missing_Codepoints()
    {
        Assert.Equal(0, Font.FindGlyphIndex(0x10FFFF));
    }

    [Fact]
    public void MaterialSymbols_Resolves_Known_Icon_Codepoint()
    {
        Assert.NotEqual(0, UiFonts.MaterialSymbols.FindGlyphIndex(MaterialSymbols.Home[0]));
        Assert.NotEqual(0, UiFonts.MaterialSymbols.FindGlyphIndex(MaterialSymbols.AvgTime[0]));
    }

    [Fact]
    public void MaterialSymbols_Constants_Resolve_In_Bundled_Font()
    {
        var constants = typeof(MaterialSymbols)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .ToArray();

        Assert.Same(UiFonts.MaterialSymbols, MaterialSymbols.Font);
        Assert.True(constants.Length > 4000);
        Assert.NotEqual(0, MaterialSymbols.Font.FindGlyphIndex(MaterialSymbols.Home[0]));
        Assert.NotEqual(0, MaterialSymbols.Font.FindGlyphIndex(MaterialSymbols.Search[0]));
        Assert.NotEqual(0, MaterialSymbols.Font.FindGlyphIndex(MaterialSymbols.Settings[0]));
        Assert.NotEqual(0, MaterialSymbols.Font.FindGlyphIndex(MaterialSymbols.AvgTime[0]));
    }

    [Fact]
    public void UiFont_Merge_Resolves_Fallback_Codepoint()
    {
        int home = MaterialSymbols.Home[0];
        Assert.Equal(0, UiFonts.DefaultSans.FindGlyphIndex(home));

        UiFont merged = UiFont.Merge(UiFonts.DefaultSans, UiFonts.MaterialSymbols);

        Assert.NotEqual(0, merged.FindGlyphIndex(home));
    }

    [Fact]
    public void GetScaledGlyphMetrics_Scales_AdvanceWidth()
    {
        int glyphIndex = Font.FindGlyphIndex('A');
        GlyphMetrics metrics = Font.GetGlyphMetrics(glyphIndex);
        float scale = Font.ScaleForPixelHeight(20f);

        ScaledGlyphMetrics scaled = Font.GetScaledGlyphMetrics(glyphIndex, scale);
        Assert.InRange(scaled.AdvanceWidth, metrics.AdvanceWidth * scale - 0.001f, metrics.AdvanceWidth * scale + 0.001f);
        Assert.InRange(scaled.LeftSideBearing, metrics.LeftSideBearing * scale - 0.001f, metrics.LeftSideBearing * scale + 0.001f);
    }

    [Fact]
    public void RasterizeGlyph_Returns_Null_For_Space()
    {
        int glyphIndex = Font.FindGlyphIndex(' ');
        float scale = Font.ScaleForPixelHeight(18f);

        byte[]? bitmap = Font.RasterizeGlyph(glyphIndex, scale, out int width, out int height, out _, out _);

        Assert.Null(bitmap);
        Assert.Equal(0, width);
        Assert.Equal(0, height);
    }

    [Fact]
    public void RasterizeGlyphLcd_Uses_Max_Channel_As_Alpha()
    {
        int glyphIndex = Font.FindGlyphIndex('A');
        float scale = Font.ScaleForPixelHeight(18f);

        byte[]? bitmap = Font.RasterizeGlyphLcd(glyphIndex, scale, out int width, out int height, out _, out _);

        Assert.NotNull(bitmap);
        Assert.True(width > 0);
        Assert.True(height > 0);
        Assert.Equal(width * height * 4, bitmap!.Length);

        for (int i = 0; i < bitmap.Length; i += 4)
        {
            byte r = bitmap[i];
            byte g = bitmap[i + 1];
            byte b = bitmap[i + 2];
            byte a = bitmap[i + 3];
            Assert.Equal(Math.Max(r, Math.Max(g, b)), a);
        }
    }

    [Fact]
    public void RasterizeCodepoint_Matches_Direct_Glyph_Rasterization()
    {
        const float pixelHeight = 18f;
        float scale = Font.ScaleForPixelHeight(pixelHeight);
        int glyphIndex = Font.FindGlyphIndex('B');

        byte[]? direct = Font.RasterizeGlyph(glyphIndex, scale, out int directW, out int directH, out int directOx, out int directOy);
        byte[]? viaCodepoint = Font.RasterizeCodepoint('B', pixelHeight, out int codepointW, out int codepointH, out int codepointOx, out int codepointOy);

        Assert.Equal(directW, codepointW);
        Assert.Equal(directH, codepointH);
        Assert.Equal(directOx, codepointOx);
        Assert.Equal(directOy, codepointOy);
        Assert.Equal(direct, viaCodepoint);
    }
}
