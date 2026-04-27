namespace Vellum.Rendering;

/// <summary>
/// Top-left based rectangle used for backend clipping.
/// </summary>
/// <param name="X">Left edge in Vellum logical pixels.</param>
/// <param name="Y">Top edge in Vellum logical pixels.</param>
/// <param name="Width">Rectangle width in Vellum logical pixels.</param>
/// <param name="Height">Rectangle height in Vellum logical pixels.</param>
public readonly record struct ClipRect(float X, float Y, float Width, float Height)
{
    /// <summary>
    /// True when the rectangle has no positive drawable area.
    /// </summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>
    /// Returns the intersection of this rectangle and <paramref name="other"/>.
    /// </summary>
    public ClipRect Intersect(ClipRect other)
    {
        float x = MathF.Max(X, other.X);
        float y = MathF.Max(Y, other.Y);
        float x2 = MathF.Min(X + Width, other.X + other.Width);
        float y2 = MathF.Min(Y + Height, other.Y + other.Height);
        return new ClipRect(x, y, MathF.Max(0, x2 - x), MathF.Max(0, y2 - y));
    }
}
