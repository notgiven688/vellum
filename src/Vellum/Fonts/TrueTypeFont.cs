namespace Vellum;

/// <summary>
/// Public entry point for Vellum's built-in TrueType parser/rasterizer.
/// Mirrors the stbtt_fontinfo API surface from stb_truetype.h.
/// </summary>
public sealed class TrueTypeFont
{
    private readonly FontParser _parser;

    /// <summary>
    /// Loads a TrueType font from raw font bytes.
    /// </summary>
    public TrueTypeFont(byte[] data)
    {
        _parser = new FontParser(data);
    }

    /// <summary>
    /// Loads a TrueType font from a file path.
    /// </summary>
    public static TrueTypeFont FromFile(string path) => new(File.ReadAllBytes(path));

    /// <summary>
    /// Returns the scale factor to apply to unscaled font units to get pixels at the given size.
    /// Equivalent to stbtt_ScaleForPixelHeight.
    /// </summary>
    public float ScaleForPixelHeight(float pixelHeight)
    {
        var vm = _parser.GetFontVMetrics();
        return pixelHeight / (vm.Ascent - vm.Descent);
    }

    /// <summary>
    /// Returns scaled vertical metrics for the font.
    /// </summary>
    public FontVMetrics GetFontVMetrics() => _parser.GetFontVMetrics();

    /// <summary>
    /// Finds the glyph index for a Unicode codepoint, or 0 when missing.
    /// </summary>
    public int FindGlyphIndex(int codepoint) => _parser.FindGlyphIndex(codepoint);

    /// <summary>
    /// Returns unscaled metrics for a glyph index.
    /// </summary>
    public GlyphMetrics GetGlyphMetrics(int glyphIndex) => _parser.GetGlyphMetrics(glyphIndex);

    /// <summary>
    /// Returns metrics for a glyph index scaled by <paramref name="scale"/>.
    /// </summary>
    public ScaledGlyphMetrics GetScaledGlyphMetrics(int glyphIndex, float scale)
    {
        var m = _parser.GetGlyphMetrics(glyphIndex);
        int bx0 = (int)MathF.Floor(m.X0 * scale);
        int by0 = (int)MathF.Floor(-m.Y1 * scale); // flip Y: font Y grows up, bitmap Y grows down
        int bx1 = (int)MathF.Ceiling(m.X1 * scale);
        int by1 = (int)MathF.Ceiling(-m.Y0 * scale);
        return new ScaledGlyphMetrics(m.AdvanceWidth * scale, m.LeftSideBearing * scale, bx0, by0, bx1, by1);
    }

    /// <summary>
    /// Returns kerning advance between two glyph indices in unscaled font units.
    /// </summary>
    public int GetKernAdvance(int glyph1, int glyph2) => _parser.GetKernAdvance(glyph1, glyph2);

    /// <summary>
    /// Rasterizes a glyph and returns an alpha-channel bitmap (one byte per pixel).
    /// Width and height of the bitmap are written to the out parameters.
    /// Returns null for glyphs with no outline (e.g. space).
    /// </summary>
    public byte[]? RasterizeGlyph(int glyphIndex, float scale, out int width, out int height, out int offsetX, out int offsetY)
    {
        var m = GetScaledGlyphMetrics(glyphIndex, scale);
        width = m.BitmapX1 - m.BitmapX0;
        height = m.BitmapY1 - m.BitmapY0;
        offsetX = m.BitmapX0;
        offsetY = m.BitmapY0;

        if (width <= 0 || height <= 0) return null;

        var outline = GlyphOutline.Load(_parser, glyphIndex, scale);
        if (outline == null || outline.Contours.Count == 0) return null;

        // Shift outline so that bitmap origin is at (0,0)
        float ox = -m.BitmapX0;
        float oy = -m.BitmapY0;

        return Rasterizer.Rasterize(outline, width, height, ox, oy);
    }

    /// <summary>
    /// LCD subpixel variant. Returns an RGBA bitmap where R/G/B hold per-channel
    /// coverage at the left/centre/right LCD subpixel positions, and A = max(R,G,B).
    /// Width/height are in screen pixels (not subpixels).
    /// </summary>
    public byte[]? RasterizeGlyphLcd(int glyphIndex, float scale,
                                     out int width, out int height,
                                     out int offsetX, out int offsetY)
    {
        var m = GetScaledGlyphMetrics(glyphIndex, scale);
        width   = m.BitmapX1 - m.BitmapX0;
        height  = m.BitmapY1 - m.BitmapY0;
        offsetX = m.BitmapX0;
        offsetY = m.BitmapY0;
        if (width <= 0 || height <= 0) return null;

        var outline = GlyphOutline.Load(_parser, glyphIndex, scale);
        if (outline == null || outline.Contours.Count == 0) return null;

        float ox = -m.BitmapX0;
        float oy = -m.BitmapY0;
        return Rasterizer.RasterizeLcd(outline, width, height, ox, oy);
    }

    /// <summary>
    /// Convenience: rasterize a codepoint directly.
    /// </summary>
    public byte[]? RasterizeCodepoint(int codepoint, float pixelHeight, out int width, out int height, out int offsetX, out int offsetY)
    {
        float scale = ScaleForPixelHeight(pixelHeight);
        int glyphIndex = FindGlyphIndex(codepoint);
        return RasterizeGlyph(glyphIndex, scale, out width, out height, out offsetX, out offsetY);
    }
}
