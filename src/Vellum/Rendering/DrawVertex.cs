using System.Numerics;

namespace Vellum.Rendering;

/// <summary>
/// One backend-facing vertex in Vellum's frame render list.
/// </summary>
/// <param name="Pos">Top-left based position in Vellum logical pixels.</param>
/// <param name="Uv">Normalized texture coordinate.</param>
/// <param name="Color">Straight RGBA vertex tint.</param>
public readonly record struct DrawVertex(Vector2 Pos, Vector2 Uv, Color Color) : IEqualityComparer<DrawVertex>
{
    /// <inheritdoc />
    public bool Equals(DrawVertex x, DrawVertex y)
    {
        return x.Pos.Equals(y.Pos) && x.Uv.Equals(y.Uv) && x.Color.Equals(y.Color);
    }

    /// <inheritdoc />
    public int GetHashCode(DrawVertex obj)
    {
        return HashCode.Combine(obj.Pos, obj.Uv, obj.Color);
    }
}
