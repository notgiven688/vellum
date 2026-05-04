using Vellum.Rendering;
using Xunit;

namespace Vellum.Tests;

public sealed class GlyphAtlasTests
{
    [Fact]
    public void Build_With_Whitespace_Only_Creates_Advance_Only_Glyph()
    {
        var renderer = new UiTestRenderer();
        GlyphAtlas atlas = UiTestSupport.CreateAtlas(renderer, [(int)' '], lcd: false);

        Assert.Equal(0, renderer.CreateTextureCalls);
        Assert.Equal(-1, atlas.TextureId);
        Assert.Equal(0, atlas.AtlasWidth);
        Assert.Equal(0, atlas.AtlasHeight);
        Assert.True(atlas.TryGetGlyph(' ', out GlyphInfo space));
        Assert.Equal(0f, space.Width);
        Assert.Equal(0f, space.Height);
        Assert.True(space.AdvanceWidth > 0f);

        atlas.Destroy(renderer);
        Assert.Equal(0, renderer.DestroyTextureCalls);
    }

    [Fact]
    public void EnsureGlyphs_From_Advance_Only_Atlas_Creates_First_Texture()
    {
        var renderer = new UiTestRenderer();
        GlyphAtlas atlas = UiTestSupport.CreateAtlas(renderer, [(int)' '], lcd: false);

        atlas.EnsureGlyphs(renderer, [(int)'A']);

        Assert.Equal(1, renderer.CreateTextureCalls);
        Assert.Equal(0, renderer.DestroyTextureCalls);
        Assert.True(atlas.TextureId > 0);
        Assert.True(atlas.AtlasWidth > 0);
        Assert.True(atlas.AtlasHeight > 0);
        Assert.True(atlas.TryGetGlyph(' ', out GlyphInfo space));
        Assert.Equal(0f, space.Width);
        Assert.True(atlas.TryGetGlyph('A', out GlyphInfo a));
        Assert.True(a.Width > 0f);
    }

    [Fact]
    public void EnsureGlyphs_Rebuilds_Only_For_New_Codepoints()
    {
        var renderer = new UiTestRenderer();
        GlyphAtlas atlas = UiTestSupport.CreateAtlas(renderer, "AB", lcd: false);

        int firstTexture = atlas.TextureId;
        atlas.EnsureGlyphs(renderer, [(int)'A']);
        atlas.EnsureGlyphs(renderer, [(int)'B']);
        Assert.Equal(1, renderer.CreateTextureCalls);
        Assert.Equal(0, renderer.DestroyTextureCalls);
        Assert.Equal(firstTexture, atlas.TextureId);

        atlas.EnsureGlyphs(renderer, [(int)'C']);

        Assert.Equal(2, renderer.CreateTextureCalls);
        Assert.Equal(1, renderer.DestroyTextureCalls);
        Assert.NotEqual(firstTexture, atlas.TextureId);
        Assert.True(atlas.TryGetGlyph('C', out _));
    }

    [Fact]
    public void EnsureGlyphsForText_Ignores_Newlines()
    {
        var renderer = new UiTestRenderer();
        GlyphAtlas atlas = UiTestSupport.CreateAtlas(renderer, "A", lcd: false);

        atlas.EnsureGlyphsForText(renderer, "\r\n");
        atlas.EnsureGlyphsForText(renderer, "A\r\n");
        Assert.Equal(1, renderer.CreateTextureCalls);
        Assert.Equal(0, renderer.DestroyTextureCalls);

        atlas.EnsureGlyphsForText(renderer, "B\r\n");
        Assert.Equal(2, renderer.CreateTextureCalls);
        Assert.Equal(1, renderer.DestroyTextureCalls);
        Assert.True(atlas.TryGetGlyph('B', out _));
    }

    [Fact]
    public void Destroy_Clears_Metadata_And_Is_Idempotent()
    {
        var renderer = new UiTestRenderer();
        GlyphAtlas atlas = UiTestSupport.CreateAtlas(renderer, "A", lcd: false);

        Assert.True(atlas.TextureId > 0);
        atlas.Destroy(renderer);

        Assert.Equal(-1, atlas.TextureId);
        Assert.Equal(0, atlas.AtlasWidth);
        Assert.Equal(0, atlas.AtlasHeight);
        Assert.Equal(1, renderer.DestroyTextureCalls);

        atlas.Destroy(renderer);
        Assert.Equal(1, renderer.DestroyTextureCalls);
    }

    [Fact]
    public void Metrics_Remain_Stable_Across_Raster_Scales()
    {
        var renderer1 = new UiTestRenderer();
        var renderer2 = new UiTestRenderer();
        GlyphAtlas atlas1 = UiTestSupport.CreateAtlas(renderer1, "AV", pixelHeight: 18f, rasterScale: 1f, lcd: false);
        GlyphAtlas atlas2 = UiTestSupport.CreateAtlas(renderer2, "AV", pixelHeight: 18f, rasterScale: 2f, lcd: false);

        FontVMetrics vm1 = atlas1.GetScaledVMetrics();
        FontVMetrics vm2 = atlas2.GetScaledVMetrics();
        Assert.InRange(vm2.Ascent, vm1.Ascent - 0.001f, vm1.Ascent + 0.001f);
        Assert.InRange(vm2.Descent, vm1.Descent - 0.001f, vm1.Descent + 0.001f);
        Assert.InRange(vm2.LineGap, vm1.LineGap - 0.001f, vm1.LineGap + 0.001f);

        float kern1 = atlas1.GetKernAdvance('A', 'V');
        float kern2 = atlas2.GetKernAdvance('A', 'V');
        Assert.InRange(kern2, kern1 - 0.001f, kern1 + 0.001f);

        Assert.True(atlas1.TryGetGlyph('A', out GlyphInfo a1));
        Assert.True(atlas2.TryGetGlyph('A', out GlyphInfo a2));
        Assert.True(atlas1.TryGetGlyph('V', out GlyphInfo v1));
        Assert.True(atlas2.TryGetGlyph('V', out GlyphInfo v2));
        Assert.InRange(a2.AdvanceWidth, a1.AdvanceWidth - 0.001f, a1.AdvanceWidth + 0.001f);
        Assert.InRange(v2.AdvanceWidth, v1.AdvanceWidth - 0.001f, v1.AdvanceWidth + 0.001f);
    }
}
