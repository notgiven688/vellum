namespace Vellum;

/// <summary>
/// Unscaled horizontal advance and bounding box metrics for a glyph.
/// </summary>
public readonly struct GlyphMetrics
{
    /// <summary>Horizontal pen advance in unscaled font units.</summary>
    public readonly int AdvanceWidth;
    /// <summary>Horizontal bearing from pen position to glyph bounds in unscaled font units.</summary>
    public readonly int LeftSideBearing;
    /// <summary>Minimum X bound in unscaled font units.</summary>
    public readonly int X0;
    /// <summary>Minimum Y bound in unscaled font units.</summary>
    public readonly int Y0;
    /// <summary>Maximum X bound in unscaled font units.</summary>
    public readonly int X1;
    /// <summary>Maximum Y bound in unscaled font units.</summary>
    public readonly int Y1;

    /// <summary>Creates unscaled glyph metrics.</summary>
    public GlyphMetrics(int advanceWidth, int leftSideBearing, int x0, int y0, int x1, int y1)
    {
        AdvanceWidth = advanceWidth;
        LeftSideBearing = leftSideBearing;
        X0 = x0; Y0 = y0; X1 = x1; Y1 = y1;
    }
}

/// <summary>
/// Pixel-scaled glyph metrics including bitmap bounds.
/// </summary>
public readonly struct ScaledGlyphMetrics
{
    /// <summary>Horizontal pen advance in logical pixels.</summary>
    public readonly float AdvanceWidth;
    /// <summary>Horizontal bearing from pen position to glyph bounds in logical pixels.</summary>
    public readonly float LeftSideBearing;
    /// <summary>Left bitmap bound in pixels.</summary>
    public readonly int BitmapX0;
    /// <summary>Top bitmap bound in pixels.</summary>
    public readonly int BitmapY0;
    /// <summary>Right bitmap bound in pixels.</summary>
    public readonly int BitmapX1;
    /// <summary>Bottom bitmap bound in pixels.</summary>
    public readonly int BitmapY1;

    /// <summary>Creates scaled glyph metrics.</summary>
    public ScaledGlyphMetrics(float advanceWidth, float leftSideBearing, int bx0, int by0, int bx1, int by1)
    {
        AdvanceWidth = advanceWidth;
        LeftSideBearing = leftSideBearing;
        BitmapX0 = bx0; BitmapY0 = by0; BitmapX1 = bx1; BitmapY1 = by1;
    }
}

/// <summary>
/// Font-wide vertical metrics.
/// </summary>
public readonly struct FontVMetrics
{
    /// <summary>Font ascent in logical pixels or scaled font units.</summary>
    public readonly float Ascent;
    /// <summary>Font descent in logical pixels or scaled font units.</summary>
    public readonly float Descent;
    /// <summary>Recommended extra line gap in logical pixels or scaled font units.</summary>
    public readonly float LineGap;

    /// <summary>Creates vertical font metrics.</summary>
    public FontVMetrics(float ascent, float descent, float lineGap)
    {
        Ascent = ascent;
        Descent = descent;
        LineGap = lineGap;
    }
}
