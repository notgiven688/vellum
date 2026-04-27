namespace Vellum;

/// <summary>
/// Four-sided spacing value used by themes and layout helpers.
/// </summary>
public readonly struct EdgeInsets
{
    /// <summary>Top inset in logical pixels.</summary>
    public readonly float Top;
    /// <summary>Right inset in logical pixels.</summary>
    public readonly float Right;
    /// <summary>Bottom inset in logical pixels.</summary>
    public readonly float Bottom;
    /// <summary>Left inset in logical pixels.</summary>
    public readonly float Left;

    /// <summary>Creates equal insets on all four sides.</summary>
    public EdgeInsets(float all) { Top = Right = Bottom = Left = all; }

    /// <summary>Creates vertical and horizontal insets.</summary>
    public EdgeInsets(float vertical, float horizontal) { Top = Bottom = vertical; Left = Right = horizontal; }

    /// <summary>Creates explicit top, right, bottom, and left insets.</summary>
    public EdgeInsets(float top, float right, float bottom, float left) { Top = top; Right = right; Bottom = bottom; Left = left; }

    /// <summary>Zero insets.</summary>
    public static readonly EdgeInsets Zero = new(0);

    /// <summary>Total horizontal inset: left plus right.</summary>
    public float Horizontal => Left + Right;

    /// <summary>Total vertical inset: top plus bottom.</summary>
    public float Vertical => Top + Bottom;
}
