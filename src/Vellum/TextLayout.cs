using System.Buffers;
using System.Text;
using Vellum.Rendering;

namespace Vellum;

/// <summary>
/// Text wrapping mode for labels and canvas text.
/// </summary>
public enum TextWrapMode
{
    /// <summary>Render text as a single line unless explicit line breaks are present.</summary>
    NoWrap,
    /// <summary>Wrap text at word boundaries when it exceeds the available width.</summary>
    WordWrap
}

/// <summary>
/// Behavior for text that exceeds its maximum width or line count.
/// </summary>
public enum TextOverflowMode
{
    /// <summary>Allow text to draw past the requested width.</summary>
    Visible,
    /// <summary>Clip overflowing text to the requested width.</summary>
    Clip,
    /// <summary>Replace overflowing text with an ellipsis when possible.</summary>
    Ellipsis
}

internal readonly struct TextGlyphPlacement
{
    public readonly int Codepoint;
    public readonly float X, Y;

    public TextGlyphPlacement(int codepoint, float x, float y)
    {
        Codepoint = codepoint;
        X = x;
        Y = y;
    }
}

internal readonly struct TextLayoutResult
{
    private readonly TextLayoutScratch? _scratch;
    private readonly int _glyphStart;
    private readonly int _glyphCount;

    public GlyphAtlas Atlas { get; }
    public float Width { get; }
    public float Height { get; }
    public float? ClipWidth { get; }
    public bool Truncated { get; }
    public ReadOnlySpan<TextGlyphPlacement> Glyphs
        => _scratch is null ? default : _scratch.Glyphs.AsSpan(_glyphStart, _glyphCount);

    public TextLayoutResult(
        GlyphAtlas atlas,
        float width,
        float height,
        float? clipWidth,
        bool truncated,
        TextLayoutScratch? scratch,
        int glyphStart,
        int glyphCount)
    {
        Atlas = atlas;
        Width = width;
        Height = height;
        ClipWidth = clipWidth;
        Truncated = truncated;
        _scratch = scratch;
        _glyphStart = glyphStart;
        _glyphCount = glyphCount;
    }
}

internal readonly struct TextLineMetrics
{
    private readonly int[] _charIndices;
    private readonly float[] _caretPositions;
    private readonly int _start;

    public float Width { get; }
    public float Height { get; }
    public int GraphemeCount { get; }
    public ReadOnlySpan<float> CaretPositions
        => _caretPositions is null ? default : _caretPositions.AsSpan(_start, GraphemeCount + 1);

    public TextLineMetrics(
        float width,
        float height,
        int graphemeCount,
        int[] charIndices,
        float[] caretPositions,
        int start)
    {
        Width = width;
        Height = height;
        GraphemeCount = graphemeCount;
        _charIndices = charIndices;
        _caretPositions = caretPositions;
        _start = start;
    }

    public int GetCharIndex(int graphemeIndex)
    {
        if (_charIndices is null) return 0;
        int clamped = Math.Clamp(graphemeIndex, 0, GraphemeCount);
        return _charIndices[_start + clamped];
    }

    public float GetCaretX(int graphemeIndex)
    {
        if (_caretPositions is null) return 0;
        int clamped = Math.Clamp(graphemeIndex, 0, GraphemeCount);
        return _caretPositions[_start + clamped];
    }

    public int HitTest(float x)
    {
        if (x <= 0) return 0;
        if (x >= Width) return GraphemeCount;

        for (int i = 0; i < GraphemeCount; i++)
        {
            float midpoint = (_caretPositions[_start + i] + _caretPositions[_start + i + 1]) * 0.5f;
            if (x < midpoint) return i;
        }

        return GraphemeCount;
    }
}

internal sealed class TextLayoutScratch
{
    internal TextGlyphPlacement[] Glyphs = new TextGlyphPlacement[256];
    internal int GlyphsUsed;
    internal TextLayout.TextElementInfo[] Elements = new TextLayout.TextElementInfo[256];
    internal int ElementsUsed;
    internal readonly List<TextLayout.LineSlice> Lines = new();
    internal readonly List<string> Paragraphs = new();
    internal float[] PrefixWidths = new float[16];
    internal int[] LineCharIndices = new int[256];
    internal float[] LineCaretPositions = new float[256];
    internal int LineMetricsUsed;

    public void ResetForFrame()
    {
        GlyphsUsed = 0;
        ElementsUsed = 0;
        LineMetricsUsed = 0;
    }

    internal void AppendGlyph(in TextGlyphPlacement placement)
    {
        if (GlyphsUsed == Glyphs.Length)
            Array.Resize(ref Glyphs, Glyphs.Length * 2);
        Glyphs[GlyphsUsed++] = placement;
    }

    internal void AppendElement(in TextLayout.TextElementInfo element)
    {
        if (ElementsUsed == Elements.Length)
            Array.Resize(ref Elements, Elements.Length * 2);
        Elements[ElementsUsed++] = element;
    }

    internal int ReserveLineMetrics(int count)
    {
        int required = LineMetricsUsed + count;
        if (LineCharIndices.Length < required)
        {
            int newSize = LineCharIndices.Length;
            while (newSize < required) newSize *= 2;
            Array.Resize(ref LineCharIndices, newSize);
            Array.Resize(ref LineCaretPositions, newSize);
        }

        int start = LineMetricsUsed;
        LineMetricsUsed += count;
        return start;
    }
}

internal static class TextLayout
{
    internal readonly struct LineSlice
    {
        public readonly int Start;
        public readonly int Count;

        public LineSlice(int start, int count)
        {
            Start = start;
            Count = count;
        }
    }

    internal readonly struct TextElementInfo
    {
        public readonly int Start;
        public readonly int Length;
        public readonly float Width;
        public readonly int Codepoint;
        public readonly bool IsWhitespace;

        public TextElementInfo(int start, int length, float width, int codepoint, bool isWhitespace)
        {
            Start = start;
            Length = length;
            Width = width;
            Codepoint = codepoint;
            IsWhitespace = isWhitespace;
        }
    }

    public static TextLayoutResult Layout(
        TextLayoutScratch scratch,
        string text,
        GlyphAtlas atlas,
        float? maxWidth,
        TextWrapMode wrap,
        TextOverflowMode overflow,
        int maxLines,
        string ellipsisText)
    {
        int visibleLineLimit = Math.Max(0, maxLines);
        if (visibleLineLimit == 0)
            return new TextLayoutResult(atlas, 0, 0, null, false, scratch, scratch.GlyphsUsed, 0);

        var vm = atlas.GetScaledVMetrics();
        float lineHeight = MathF.Ceiling(vm.Ascent - vm.Descent);
        float lineAdvance = MathF.Ceiling(vm.Ascent - vm.Descent + vm.LineGap);
        float constrainedWidth = maxWidth.HasValue ? MathF.Max(0, maxWidth.Value) : 0;

        int glyphStart = scratch.GlyphsUsed;
        scratch.Lines.Clear();
        BuildLineSlices(scratch, text, atlas, maxWidth, wrap);
        int visibleLineCount = Math.Min(visibleLineLimit, scratch.Lines.Count);
        bool hasHiddenLines = scratch.Lines.Count > visibleLineCount;
        float measuredWidth = 0;
        bool truncated = false;

        for (int lineIndex = 0; lineIndex < visibleLineCount; lineIndex++)
        {
            float baseline = lineIndex * lineAdvance + vm.Ascent;
            bool forceTruncate = hasHiddenLines && lineIndex == visibleLineCount - 1;
            float lineWidth = LayoutLineSlice(
                scratch,
                scratch.Lines[lineIndex],
                atlas,
                maxWidth,
                overflow,
                ellipsisText,
                forceTruncate,
                baseline,
                out bool lineTruncated);

            measuredWidth = MathF.Max(measuredWidth, lineWidth);
            truncated |= lineTruncated;
        }

        float height = visibleLineCount > 0
            ? lineHeight + (visibleLineCount - 1) * lineAdvance
            : 0;
        float? clipWidth = truncated && maxWidth.HasValue && overflow != TextOverflowMode.Visible
            ? constrainedWidth
            : null;

        if (clipWidth.HasValue) measuredWidth = MathF.Min(measuredWidth, clipWidth.Value);
        return new TextLayoutResult(atlas, measuredWidth, height, clipWidth, truncated, scratch, glyphStart, scratch.GlyphsUsed - glyphStart);
    }

    public static TextLineMetrics MeasureSingleLine(TextLayoutScratch scratch, string text, GlyphAtlas atlas)
    {
        var vm = atlas.GetScaledVMetrics();
        float lineHeight = MathF.Ceiling(vm.Ascent - vm.Descent);

        int elementsStart = scratch.ElementsUsed;
        int elementsCount = BuildTextElements(scratch, text, atlas);
        int dataStart = scratch.ReserveLineMetrics(elementsCount + 1);
        var charIndices = scratch.LineCharIndices;
        var caretPositions = scratch.LineCaretPositions;

        if (elementsCount == 0)
        {
            charIndices[dataStart] = 0;
            caretPositions[dataStart] = 0;
            return new TextLineMetrics(0, lineHeight, 0, charIndices, caretPositions, dataStart);
        }

        float penX = 0;
        int prevCodepoint = 0;

        for (int i = 0; i < elementsCount; i++)
        {
            var element = scratch.Elements[elementsStart + i];
            charIndices[dataStart + i] = element.Start;
            caretPositions[dataStart + i] = penX;

            if (prevCodepoint != 0 && element.Codepoint != 0)
                penX += atlas.GetKernAdvance(prevCodepoint, element.Codepoint);

            penX += element.Width;
            prevCodepoint = element.Codepoint;
        }

        charIndices[dataStart + elementsCount] = text.Length;
        caretPositions[dataStart + elementsCount] = penX;
        return new TextLineMetrics(penX, lineHeight, elementsCount, charIndices, caretPositions, dataStart);
    }

    private static float LayoutLineSlice(
        TextLayoutScratch scratch,
        LineSlice line,
        GlyphAtlas atlas,
        float? maxWidth,
        TextOverflowMode overflow,
        string ellipsisText,
        bool forceTruncate,
        float baseline,
        out bool truncated)
    {
        float fullWidth = MeasureElements(scratch, line.Start, line.Count, atlas);
        float limit = maxWidth.HasValue ? MathF.Max(0, maxWidth.Value) : 0;
        bool exceedsWidth = maxWidth.HasValue && fullWidth > limit;
        bool needsOverflowHandling = maxWidth.HasValue && overflow != TextOverflowMode.Visible && (forceTruncate || exceedsWidth);
        truncated = forceTruncate || exceedsWidth;

        if (!needsOverflowHandling)
            return AppendElements(scratch, line.Start, line.Count, atlas, baseline);

        if (overflow == TextOverflowMode.Clip)
        {
            AppendElements(scratch, line.Start, line.Count, atlas, baseline);
            return limit;
        }

        int ellipsisStart = scratch.ElementsUsed;
        int ellipsisCount = BuildTextElements(scratch, ellipsisText, atlas);
        float ellipsisWidth = MeasureElements(scratch, ellipsisStart, ellipsisCount, atlas);
        BuildPrefixWidths(scratch, line.Start, line.Count, atlas);

        int fitCount = TrimTrailingWhitespace(scratch, line.Start, line.Count);

        while (fitCount > 0 &&
               MeasureWithSuffix(scratch.PrefixWidths[fitCount],
                   fitCount > 0 ? scratch.Elements[line.Start + fitCount - 1].Codepoint : 0,
                   scratch, ellipsisStart, ellipsisCount, ellipsisWidth, atlas) > limit)
        {
            fitCount--;
            fitCount = TrimTrailingWhitespace(scratch, line.Start, fitCount);
        }

        float penX = AppendElements(scratch, line.Start, fitCount, atlas, baseline);
        int prevCodepoint = fitCount > 0 ? scratch.Elements[line.Start + fitCount - 1].Codepoint : 0;
        penX = AppendElements(scratch, ellipsisStart, ellipsisCount, atlas, baseline, penX, prevCodepoint);
        return MathF.Min(limit, penX);
    }

    private static void BuildLineSlices(TextLayoutScratch scratch, string text, GlyphAtlas atlas, float? maxWidth, TextWrapMode wrap)
    {
        scratch.Paragraphs.Clear();
        SplitLines(scratch.Paragraphs, text);

        for (int i = 0; i < scratch.Paragraphs.Count; i++)
        {
            string paragraph = scratch.Paragraphs[i];
            int start = scratch.ElementsUsed;
            int count = BuildTextElements(scratch, paragraph, atlas);

            if (wrap == TextWrapMode.WordWrap && maxWidth.HasValue && maxWidth.Value > 0)
                BuildWrappedLineSlices(scratch, scratch.Lines, start, count, atlas, MathF.Max(0, maxWidth.Value));
            else
                scratch.Lines.Add(new LineSlice(start, count));
        }
    }

    private static void SplitLines(List<string> sink, string text)
    {
        int start = 0;

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n') continue;

            int length = i - start;
            if (length > 0 && text[i - 1] == '\r') length--;
            sink.Add(text.Substring(start, Math.Max(0, length)));
            start = i + 1;
        }

        int tailLength = text.Length - start;
        if (tailLength > 0 && text[^1] == '\r') tailLength--;
        sink.Add(text.Substring(start, Math.Max(0, tailLength)));
    }

    private static void BuildWrappedLineSlices(TextLayoutScratch scratch, List<LineSlice> sink, int elementsStart, int elementsCount, GlyphAtlas atlas, float maxWidth)
    {
        if (elementsCount == 0)
        {
            sink.Add(new LineSlice(elementsStart, 0));
            return;
        }

        int start = 0;
        while (start < elementsCount)
        {
            int end = start;
            int prevCodepoint = 0;
            float width = 0;
            int lastWhitespace = -1;

            while (end < elementsCount)
            {
                var element = scratch.Elements[elementsStart + end];
                float candidate = width;
                if (prevCodepoint != 0 && element.Codepoint != 0)
                    candidate += atlas.GetKernAdvance(prevCodepoint, element.Codepoint);
                candidate += element.Width;

                if (candidate <= maxWidth || end == start)
                {
                    width = candidate;
                    if (element.IsWhitespace) lastWhitespace = end;
                    prevCodepoint = element.Codepoint;
                    end++;
                    continue;
                }

                break;
            }

            if (end >= elementsCount)
            {
                sink.Add(new LineSlice(elementsStart + start, elementsCount - start));
                break;
            }

            if (lastWhitespace >= start)
            {
                int count = TrimTrailingWhitespace(scratch, elementsStart + start, (lastWhitespace + 1) - start);
                sink.Add(new LineSlice(elementsStart + start, count));

                start = lastWhitespace + 1;
                while (start < elementsCount && scratch.Elements[elementsStart + start].IsWhitespace) start++;
                continue;
            }

            int forcedCount = Math.Max(1, end - start);
            sink.Add(new LineSlice(elementsStart + start, forcedCount));
            start += forcedCount;
        }
    }

    private static int BuildTextElements(TextLayoutScratch scratch, string text, GlyphAtlas atlas)
    {
        int initialUsed = scratch.ElementsUsed;
        var span = text.AsSpan();
        int charIndex = 0;

        while (charIndex < text.Length)
        {
            int runeStart = charIndex;
            OperationStatus status = Rune.DecodeFromUtf16(span.Slice(charIndex), out Rune rune, out int consumed);
            if (status != OperationStatus.Done)
            {
                rune = Rune.ReplacementChar;
                consumed = 1;
            }

            int codepoint = rune.Value;
            float width = 0;
            if (atlas.TryGetGlyph(codepoint, out var glyph)) width += glyph.AdvanceWidth;
            bool isWhitespace = Rune.IsWhiteSpace(rune);

            scratch.AppendElement(new TextElementInfo(runeStart, consumed, width, codepoint, isWhitespace));
            charIndex += consumed;
        }

        return scratch.ElementsUsed - initialUsed;
    }

    private static float MeasureElements(TextLayoutScratch scratch, int start, int count, GlyphAtlas atlas)
    {
        BuildPrefixWidths(scratch, start, count, atlas);
        return scratch.PrefixWidths[count];
    }

    private static void BuildPrefixWidths(TextLayoutScratch scratch, int start, int count, GlyphAtlas atlas)
    {
        if (scratch.PrefixWidths.Length < count + 1)
        {
            int newSize = scratch.PrefixWidths.Length;
            while (newSize < count + 1) newSize *= 2;
            scratch.PrefixWidths = new float[newSize];
        }

        var prefixWidths = scratch.PrefixWidths;
        prefixWidths[0] = 0;
        int prevCodepoint = 0;

        for (int i = 0; i < count; i++)
        {
            var element = scratch.Elements[start + i];
            float width = prefixWidths[i];
            if (prevCodepoint != 0 && element.Codepoint != 0)
                width += atlas.GetKernAdvance(prevCodepoint, element.Codepoint);

            prefixWidths[i + 1] = width + element.Width;
            prevCodepoint = element.Codepoint;
        }
    }

    private static int TrimTrailingWhitespace(TextLayoutScratch scratch, int start, int count)
    {
        while (count > 0 && scratch.Elements[start + count - 1].IsWhitespace) count--;
        return count;
    }

    private static float MeasureWithSuffix(
        float prefixWidth,
        int prefixLastCodepoint,
        TextLayoutScratch scratch,
        int suffixStart,
        int suffixCount,
        float suffixWidth,
        GlyphAtlas atlas)
    {
        if (suffixCount == 0) return prefixWidth;

        float width = prefixWidth;
        int firstCodepoint = scratch.Elements[suffixStart].Codepoint;
        if (prefixLastCodepoint != 0 && firstCodepoint != 0)
            width += atlas.GetKernAdvance(prefixLastCodepoint, firstCodepoint);

        return width + suffixWidth;
    }

    private static float AppendElements(
        TextLayoutScratch scratch,
        int start,
        int count,
        GlyphAtlas atlas,
        float baseline)
        => AppendElements(scratch, start, count, atlas, baseline, penX: 0, prevCodepoint: 0);

    private static float AppendElements(
        TextLayoutScratch scratch,
        int start,
        int count,
        GlyphAtlas atlas,
        float baseline,
        float penX,
        int prevCodepoint)
    {
        for (int i = 0; i < count; i++)
        {
            var element = scratch.Elements[start + i];
            int codepoint = element.Codepoint;
            if (prevCodepoint != 0) penX += atlas.GetKernAdvance(prevCodepoint, codepoint);

            if (atlas.TryGetGlyph(codepoint, out var glyph))
            {
                if (glyph.Width > 0)
                    scratch.AppendGlyph(new TextGlyphPlacement(codepoint, penX + glyph.OffsetX, baseline + glyph.OffsetY));

                penX += glyph.AdvanceWidth;
            }

            prevCodepoint = codepoint;
        }

        return penX;
    }
}
