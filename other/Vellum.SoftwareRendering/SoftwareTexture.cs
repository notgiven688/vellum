using System.Numerics;
using Vellum.Rendering;

namespace Vellum.SoftwareRendering;

internal sealed class SoftwareTexture
{
    private readonly byte[] _rgba;

    public SoftwareTexture(int width, int height, byte[] rgba)
    {
        Width = width;
        Height = height;
        _rgba = (byte[])rgba.Clone();
    }

    public int Width { get; }

    public int Height { get; }

    public Color SampleNearest(Vector2 uv)
    {
        if (Width <= 0 || Height <= 0)
            return Color.Transparent;

        float u = Math.Clamp(uv.X, 0f, 1f);
        float v = Math.Clamp(uv.Y, 0f, 1f);
        int x = Math.Clamp((int)MathF.Floor(u * Width), 0, Width - 1);
        int y = Math.Clamp((int)MathF.Floor(v * Height), 0, Height - 1);
        int offset = (y * Width + x) * 4;
        return new Color(_rgba[offset], _rgba[offset + 1], _rgba[offset + 2], _rgba[offset + 3]);
    }
}
