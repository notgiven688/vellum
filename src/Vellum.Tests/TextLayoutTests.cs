using System.Text;
using Vellum.Rendering;
using Xunit;

namespace Vellum.Tests;

public sealed class TextLayoutTests
{
    [Fact]
    public void MeasureSingleLine_Tracks_Caret_Positions_And_HitTesting()
    {
        var renderer = new UiTestRenderer();
        GlyphAtlas atlas = UiTestSupport.CreateAtlas(renderer, "A B");
        var scratch = new TextLayoutScratch();

        TextLineMetrics metrics = TextLayout.MeasureSingleLine(scratch, "A B", atlas);

        Assert.Equal(3, metrics.GraphemeCount);
        Assert.Equal(0, metrics.GetCharIndex(0));
        Assert.Equal(1, metrics.GetCharIndex(1));
        Assert.Equal(2, metrics.GetCharIndex(2));
        Assert.Equal(3, metrics.GetCharIndex(3));

        float c0 = metrics.GetCaretX(0);
        float c1 = metrics.GetCaretX(1);
        float c2 = metrics.GetCaretX(2);
        float c3 = metrics.GetCaretX(3);

        Assert.True(c0 <= c1 && c1 <= c2 && c2 <= c3);
        Assert.Equal(0, metrics.HitTest(-1f));
        Assert.Equal(metrics.GraphemeCount, metrics.HitTest(metrics.Width + 1f));
        Assert.Equal(0, metrics.HitTest(((c0 + c1) * 0.5f) - 0.01f));
        Assert.Equal(1, metrics.HitTest(((c0 + c1) * 0.5f) + 0.01f));
        Assert.Equal(1, metrics.HitTest(((c1 + c2) * 0.5f) - 0.01f));
        Assert.Equal(2, metrics.HitTest(((c1 + c2) * 0.5f) + 0.01f));
    }

    [Fact]
    public void MeasureSingleLine_Replaces_Invalid_Surrogates()
    {
        var renderer = new UiTestRenderer();
        GlyphAtlas atlas = UiTestSupport.CreateAtlas(
            renderer,
            ['A', Rune.ReplacementChar.Value, 'B']);
        var scratch = new TextLayoutScratch();
        string text = "A\uD800B";

        TextLineMetrics metrics = TextLayout.MeasureSingleLine(scratch, text, atlas);
        TextLayoutResult layout = TextLayout.Layout(
            scratch,
            text,
            atlas,
            null,
            TextWrapMode.NoWrap,
            TextOverflowMode.Visible,
            1,
            "...");

        Assert.Equal(3, metrics.GraphemeCount);
        Assert.Equal(0, metrics.GetCharIndex(0));
        Assert.Equal(1, metrics.GetCharIndex(1));
        Assert.Equal(2, metrics.GetCharIndex(2));
        Assert.Equal(3, metrics.GetCharIndex(3));
        Assert.Equal(3, layout.Glyphs.Length);
        Assert.Equal('A', layout.Glyphs[0].Codepoint);
        Assert.Equal(Rune.ReplacementChar.Value, layout.Glyphs[1].Codepoint);
        Assert.Equal('B', layout.Glyphs[2].Codepoint);
    }

    [Fact]
    public void Layout_Preserves_Crlf_And_Empty_Lines()
    {
        var renderer = new UiTestRenderer();
        GlyphAtlas atlas = UiTestSupport.CreateAtlas(renderer, "AB");
        var scratch = new TextLayoutScratch();
        string text = "A\r\n\r\nB";

        TextLayoutResult layout = TextLayout.Layout(
            scratch,
            text,
            atlas,
            null,
            TextWrapMode.NoWrap,
            TextOverflowMode.Visible,
            int.MaxValue,
            "...");

        FontVMetrics vm = atlas.GetScaledVMetrics();
        float lineHeight = MathF.Ceiling(vm.Ascent - vm.Descent);
        float lineAdvance = MathF.Ceiling(vm.Ascent - vm.Descent + vm.LineGap);
        float expectedHeight = lineHeight + 2f * lineAdvance;

        Assert.False(layout.Truncated);
        Assert.Null(layout.ClipWidth);
        Assert.Equal(2, layout.Glyphs.Length);
        Assert.InRange(layout.Height, expectedHeight - 0.01f, expectedHeight + 0.01f);
    }

    [Fact]
    public void Layout_WordWrap_With_MaxLines_Uses_Ellipsis_And_ClipWidth()
    {
        var renderer = new UiTestRenderer();
        string ellipsisText = UiFonts.DefaultSans.FindGlyphIndex(0x2026) != 0 ? "\u2026" : "...";
        GlyphAtlas atlas = UiTestSupport.CreateAtlas(renderer, "Alpha Beta Gamma Delta " + ellipsisText);
        var scratch = new TextLayoutScratch();
        float maxWidth = TextLayout.MeasureSingleLine(scratch, "Alpha", atlas).Width + 0.5f;

        TextLayoutResult layout = TextLayout.Layout(
            scratch,
            "Alpha Beta Gamma Delta",
            atlas,
            maxWidth,
            TextWrapMode.WordWrap,
            TextOverflowMode.Ellipsis,
            2,
            ellipsisText);

        FontVMetrics vm = atlas.GetScaledVMetrics();
        float lineHeight = MathF.Ceiling(vm.Ascent - vm.Descent);
        float lineAdvance = MathF.Ceiling(vm.Ascent - vm.Descent + vm.LineGap);
        float expectedHeight = lineHeight + lineAdvance;

        Assert.True(layout.Truncated);
        Assert.True(layout.ClipWidth.HasValue);
        Assert.InRange(layout.ClipWidth!.Value, maxWidth - 0.001f, maxWidth + 0.001f);
        Assert.InRange(layout.Width, 0f, maxWidth + 0.001f);
        Assert.InRange(layout.Height, expectedHeight - 0.01f, expectedHeight + 0.01f);
        Assert.True(layout.Glyphs.Length > 0);
        Assert.Equal(ellipsisText[^1], (char)layout.Glyphs[^1].Codepoint);
    }

    [Fact]
    public void MeasureSingleLine_Applies_Kerning_In_Reported_Width()
    {
        var renderer = new UiTestRenderer();
        GlyphAtlas atlas = UiTestSupport.CreateAtlas(renderer, "AV");
        var scratch = new TextLayoutScratch();

        TextLineMetrics metrics = TextLayout.MeasureSingleLine(scratch, "AV", atlas);
        Assert.True(atlas.TryGetGlyph('A', out GlyphInfo a));
        Assert.True(atlas.TryGetGlyph('V', out GlyphInfo v));

        float expected = a.AdvanceWidth + atlas.GetKernAdvance('A', 'V') + v.AdvanceWidth;
        Assert.InRange(metrics.Width, expected - 0.01f, expected + 0.01f);
    }
}
