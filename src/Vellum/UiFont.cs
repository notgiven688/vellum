using System.Runtime.CompilerServices;

namespace Vellum;

/// <summary>
/// One source font in a logical UI font stack, with optional visual adjustment.
/// </summary>
public readonly struct UiFontSource : IEquatable<UiFontSource>
{
    /// <summary>Underlying TrueType font.</summary>
    public TrueTypeFont Font { get; }
    /// <summary>Relative scale applied when rasterizing this source.</summary>
    public float Scale { get; }
    /// <summary>Horizontal glyph offset in logical pixels.</summary>
    public float OffsetX { get; }
    /// <summary>Vertical glyph offset in logical pixels. Positive values move glyphs down.</summary>
    public float OffsetY { get; }

    /// <summary>
    /// Creates a font source. Scale is relative to the requested UI text size.
    /// </summary>
    public UiFontSource(TrueTypeFont font, float scale = 1f, float offsetX = 0f, float offsetY = 0f)
    {
        ArgumentNullException.ThrowIfNull(font);
        if (!float.IsFinite(scale) || scale <= 0f)
            throw new ArgumentOutOfRangeException(nameof(scale), "Font source scale must be finite and greater than zero.");
        if (!float.IsFinite(offsetX))
            throw new ArgumentOutOfRangeException(nameof(offsetX), "Font source offset must be finite.");
        if (!float.IsFinite(offsetY))
            throw new ArgumentOutOfRangeException(nameof(offsetY), "Font source offset must be finite.");

        Font = font;
        Scale = scale;
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    /// <inheritdoc />
    public bool Equals(UiFontSource other)
        => ReferenceEquals(Font, other.Font) &&
           Scale.Equals(other.Scale) &&
           OffsetX.Equals(other.OffsetX) &&
           OffsetY.Equals(other.OffsetY);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is UiFontSource other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(RuntimeHelpers.GetHashCode(Font));
        hash.Add(Scale);
        hash.Add(OffsetX);
        hash.Add(OffsetY);
        return hash.ToHashCode();
    }
}

/// <summary>
/// Logical UI font made from one or more TrueType fonts.
/// </summary>
public sealed class UiFont : IEquatable<UiFont>
{
    internal readonly UiFontSource[] Sources;

    private UiFont(UiFontSource[] sources)
    {
        Sources = sources;
    }

    /// <summary>
    /// Number of font sources in this logical font.
    /// </summary>
    public int Count => Sources.Length;

    /// <summary>
    /// Gets a font source by priority. Source 0 is the primary font.
    /// </summary>
    public TrueTypeFont this[int index] => Sources[index].Font;

    /// <summary>
    /// Creates a logical font from a single TrueType font.
    /// </summary>
    public static UiFont From(TrueTypeFont font)
    {
        return new UiFont([Source(font)]);
    }

    /// <summary>
    /// Creates a logical font from a single adjusted font source.
    /// </summary>
    public static UiFont From(UiFontSource source)
    {
        return new UiFont([source]);
    }

    /// <summary>
    /// Creates an adjusted source for use with <see cref="Merge(UiFontSource[])"/>.
    /// </summary>
    public static UiFontSource Source(TrueTypeFont font, float scale = 1f, float offsetX = 0f, float offsetY = 0f)
        => new(font, scale, offsetX, offsetY);

    /// <summary>
    /// Gets an adjusted font source by priority. Source 0 is the primary font.
    /// </summary>
    public UiFontSource GetSource(int index)
    {
        return Sources[index];
    }

    /// <summary>
    /// Creates a logical font by merging font sources. Earlier sources win when multiple fonts contain the same codepoint.
    /// </summary>
    public static UiFont Merge(params TrueTypeFont[] fonts)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        if (fonts.Length == 0)
            throw new ArgumentException("At least one font source is required.", nameof(fonts));

        var sources = new UiFontSource[fonts.Length];
        for (int i = 0; i < fonts.Length; i++)
        {
            TrueTypeFont font = fonts[i] ?? throw new ArgumentNullException(nameof(fonts), "Font sources cannot contain null.");
            sources[i] = Source(font);
        }

        return new UiFont(sources);
    }

    /// <summary>
    /// Creates a logical font by merging adjusted font sources. Earlier sources win when multiple fonts contain the same codepoint.
    /// </summary>
    public static UiFont Merge(params UiFontSource[] sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Length == 0)
            throw new ArgumentException("At least one font source is required.", nameof(sources));

        var copied = new UiFontSource[sources.Length];
        for (int i = 0; i < sources.Length; i++)
            copied[i] = sources[i];

        return new UiFont(copied);
    }

    /// <summary>
    /// Finds the first non-zero glyph index for a Unicode codepoint across the merged sources.
    /// </summary>
    public int FindGlyphIndex(int codepoint)
    {
        for (int i = 0; i < Sources.Length; i++)
        {
            int glyphIndex = Sources[i].Font.FindGlyphIndex(codepoint);
            if (glyphIndex != 0)
                return glyphIndex;
        }

        return 0;
    }

    /// <inheritdoc />
    public bool Equals(UiFont? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null || Sources.Length != other.Sources.Length) return false;

        for (int i = 0; i < Sources.Length; i++)
        {
            if (!Sources[i].Equals(other.Sources[i]))
                return false;
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is UiFont other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        for (int i = 0; i < Sources.Length; i++)
            hash.Add(Sources[i]);
        return hash.ToHashCode();
    }
}
