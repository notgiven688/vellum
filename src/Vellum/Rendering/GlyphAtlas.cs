using System.Text;
using Vellum;

namespace Vellum.Rendering;

internal readonly struct GlyphInfo
{
    // UV coordinates into the atlas texture (0..1)
    public readonly float U0, V0, U1, V1;
    // Size of the glyph bitmap in logical UI units
    public readonly float Width, Height;
    // Offset from pen position to top-left of bitmap in logical UI units
    public readonly float OffsetX, OffsetY;
    // How far to advance the pen after this glyph
    public readonly float AdvanceWidth;

    public GlyphInfo(float u0, float v0, float u1, float v1,
                     float width, float height, float offsetX, float offsetY,
                     float advanceWidth)
    {
        U0 = u0; V0 = v0; U1 = u1; V1 = v1;
        Width = width; Height = height;
        OffsetX = offsetX; OffsetY = offsetY;
        AdvanceWidth = advanceWidth;
    }
}

/// <summary>
/// Packs glyphs for a given font + size into a single RGBA texture atlas.
/// Call Build() once, then query per-codepoint info via TryGetGlyph().
/// </summary>
internal sealed class GlyphAtlas
{
    private readonly Dictionary<int, GlyphInfo> _glyphs = new();
    private readonly HashSet<int> _codepoints = new();
    private readonly TrueTypeFont _font;
    private readonly float _pixelHeight;
    private readonly float _rasterScale;

    public int TextureId { get; private set; } = -1;
    public int AtlasWidth { get; private set; }
    public int AtlasHeight { get; private set; }
    public bool IsLcd { get; private set; }

    public GlyphAtlas(TrueTypeFont font, float pixelHeight, float rasterScale = 1f, bool lcd = true)
    {
        _font = font;
        _pixelHeight = pixelHeight;
        _rasterScale = MathF.Max(1f, rasterScale);
        IsLcd = lcd;
    }

    /// <summary>
    /// Rasterizes all codepoints in <paramref name="codepoints"/>, packs them
    /// into a texture atlas, and uploads it to the renderer.
    /// </summary>
    public void Build(IRenderer renderer, IEnumerable<int> codepoints)
    {
        _codepoints.Clear();
        foreach (int codepoint in codepoints) _codepoints.Add(codepoint);
        Rebuild(renderer);
    }

    public void EnsureGlyphs(IRenderer renderer, IEnumerable<int> codepoints)
    {
        bool needsRebuild = false;
        foreach (int codepoint in codepoints)
            needsRebuild |= _codepoints.Add(codepoint);

        if (needsRebuild) Rebuild(renderer);
    }

    public void EnsureGlyphsForText(IRenderer renderer, string text)
    {
        bool needsRebuild = false;
        foreach (Rune rune in text.EnumerateRunes())
        {
            int codepoint = rune.Value;
            if (codepoint == '\r' || codepoint == '\n') continue;
            needsRebuild |= _codepoints.Add(codepoint);
        }

        if (needsRebuild) Rebuild(renderer);
    }

    public void Destroy(IRenderer renderer)
    {
        if (TextureId >= 0)
        {
            renderer.DestroyTexture(TextureId);
            TextureId = -1;
        }

        AtlasWidth = 0;
        AtlasHeight = 0;
    }

    private void Rebuild(IRenderer renderer)
    {
        if (TextureId >= 0)
            renderer.DestroyTexture(TextureId);

        _glyphs.Clear();

        float rasterPixelHeight = _pixelHeight * _rasterScale;
        float scale = _font.ScaleForPixelHeight(rasterPixelHeight);

        // --- Collect rasterized glyphs ---
        var entries = new List<(int codepoint, byte[] bitmap, int w, int h, int ox, int oy, float advance)>();
        foreach (int cp in _codepoints)
        {
            int glyphIndex = _font.FindGlyphIndex(cp);
            byte[]? bmp = IsLcd
                ? _font.RasterizeGlyphLcd(glyphIndex, scale, out int w, out int h, out int ox, out int oy)
                : _font.RasterizeGlyph(glyphIndex, scale, out w, out h, out ox, out oy);
            var m = _font.GetScaledGlyphMetrics(glyphIndex, scale);
            if (bmp != null && w > 0 && h > 0)
                entries.Add((cp, bmp, w, h, ox, oy, m.AdvanceWidth));
            else
                _glyphs[cp] = new GlyphInfo(0, 0, 0, 0, 0, 0, ox / _rasterScale, oy / _rasterScale,
                    m.AdvanceWidth / _rasterScale);
        }

        if (entries.Count == 0)
        {
            TextureId = renderer.CreateTexture(Array.Empty<byte>(), 0, 0);
            return;
        }

        // --- Shelf packer ---
        // Sort by height descending for better packing efficiency
        entries.Sort((a, b) => b.h.CompareTo(a.h));

        const int Padding = 1;
        int atlasW = 512;
        byte[] atlas;
        int atlasH;

        while (true)
        {
            atlas = TryPack(entries, atlasW, Padding, IsLcd, out atlasH, out var placements)!;
            if (atlas != null)
            {
                // Build glyph info from placements
                foreach (var (cp, _, w, h, ox, oy, advance, px, py) in placements!)
                {
                    float u0 = (float)px / atlasW;
                    float v0 = (float)py / atlasH;
                    float u1 = (float)(px + w) / atlasW;
                    float v1 = (float)(py + h) / atlasH;
                    _glyphs[cp] = new GlyphInfo(
                        u0, v0, u1, v1,
                        w / _rasterScale,
                        h / _rasterScale,
                        ox / _rasterScale,
                        oy / _rasterScale,
                        advance / _rasterScale);
                }
                break;
            }
            atlasW *= 2; // double width and retry
        }

        AtlasWidth = atlasW;
        AtlasHeight = atlasH;
        TextureId = renderer.CreateTexture(atlas, atlasW, atlasH);
    }

    public bool TryGetGlyph(int codepoint, out GlyphInfo info) =>
        _glyphs.TryGetValue(codepoint, out info);

    public float GetKernAdvance(int cp1, int cp2)
    {
        int g1 = _font.FindGlyphIndex(cp1);
        int g2 = _font.FindGlyphIndex(cp2);
        float scale = _font.ScaleForPixelHeight(_pixelHeight * _rasterScale);
        return _font.GetKernAdvance(g1, g2) * scale / _rasterScale;
    }

    public FontVMetrics GetScaledVMetrics()
    {
        float scale = _font.ScaleForPixelHeight(_pixelHeight * _rasterScale);
        var vm = _font.GetFontVMetrics();
        return new FontVMetrics(
            vm.Ascent * scale / _rasterScale,
            vm.Descent * scale / _rasterScale,
            vm.LineGap * scale / _rasterScale);
    }

    // -------------------------------------------------------------------------
    // Shelf packer
    // -------------------------------------------------------------------------

    private static byte[]? TryPack(
        List<(int cp, byte[] bmp, int w, int h, int ox, int oy, float advance)> entries,
        int atlasW, int padding, bool lcd,
        out int atlasH,
        out List<(int cp, byte[] bmp, int w, int h, int ox, int oy, float advance, int px, int py)>? placements)
    {
        placements = new();
        int curX = padding, curY = padding, rowH = 0;

        foreach (var e in entries)
        {
            if (curX + e.w + padding > atlasW)
            {
                curX = padding;
                curY += rowH + padding;
                rowH = 0;
            }
            if (e.h > rowH) rowH = e.h;
            placements.Add((e.cp, e.bmp, e.w, e.h, e.ox, e.oy, e.advance, curX, curY));
            curX += e.w + padding;
        }

        atlasH = NextPowerOfTwo(curY + rowH + padding);

        if (atlasH > atlasW * 4)
        {
            placements = null;
            return null;
        }

        byte[] rgba = new byte[atlasW * atlasH * 4];
        foreach (var (_, bmp, w, h, _, _, _, px, py) in placements)
        {
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int dst = ((py + y) * atlasW + (px + x)) * 4;
                if (lcd)
                {
                    // bmp is already RGBA with per-channel subpixel coverage
                    int src = (y * w + x) * 4;
                    rgba[dst]     = bmp[src];
                    rgba[dst + 1] = bmp[src + 1];
                    rgba[dst + 2] = bmp[src + 2];
                    rgba[dst + 3] = bmp[src + 3];
                }
                else
                {
                    byte a = AdjustGrayscaleCoverage(bmp[y * w + x]);
                    rgba[dst] = rgba[dst + 1] = rgba[dst + 2] = 255;
                    rgba[dst + 3] = a;
                }
            }
        }
        return rgba;
    }

    private static byte AdjustGrayscaleCoverage(byte coverage)
    {
        if (coverage is 0 or 255) return coverage;

        float normalized = coverage / 255f;
        return (byte)(MathF.Pow(normalized, 1f / 1.45f) * 255f + 0.5f);
    }

    private static int NextPowerOfTwo(int v)
    {
        v--;
        v |= v >> 1; v |= v >> 2; v |= v >> 4; v |= v >> 8; v |= v >> 16;
        return v + 1;
    }
}
