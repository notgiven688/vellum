using System.Numerics;
using Vellum.Rendering;

namespace Vellum.SoftwareRendering;

/// <summary>
/// Pure C# renderer that rasterizes Vellum draw lists into an RGBA8 framebuffer.
/// </summary>
public sealed class SoftwareRenderer : IRenderer, IDisposable
{
    private readonly Dictionary<int, SoftwareTexture> _textures = new();
    private int _nextTextureId = 1;
    private RenderFrameInfo _frame;
    private bool _disposed;

    /// <summary>Creates a software renderer with a scale-1 framebuffer.</summary>
    public SoftwareRenderer(int width, int height, Color? clearColor = null)
    {
        if (width < 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 0) throw new ArgumentOutOfRangeException(nameof(height));

        Width = width;
        Height = height;
        ClearColor = clearColor ?? Color.Transparent;
        Pixels = new byte[width * height * 4];
        _frame = new RenderFrameInfo(width, height);
    }

    /// <summary>Framebuffer width in pixels.</summary>
    public int Width { get; private set; }

    /// <summary>Framebuffer height in pixels.</summary>
    public int Height { get; private set; }

    /// <summary>Color used to clear the framebuffer at the start of <see cref="Render"/>.</summary>
    public Color ClearColor { get; set; }

    /// <summary>Framebuffer pixels as tightly packed RGBA8 rows.</summary>
    public byte[] Pixels { get; private set; }

    /// <inheritdoc />
    public void BeginFrame(RenderFrameInfo frame)
    {
        ThrowIfDisposed();
        _frame = frame.Normalized();
        EnsureFramebuffer(_frame.FramebufferWidth, _frame.FramebufferHeight);
    }

    /// <inheritdoc />
    public void Render(RenderList renderList)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(renderList);

        Clear(ClearColor);

        foreach (DrawCommand command in renderList.Commands)
        {
            if (command.IndexCount <= 0) continue;

            ClipRect clip = command.HasClip
                ? ScaleClip(command.ClipRect).Intersect(new ClipRect(0, 0, Width, Height))
                : new ClipRect(0, 0, Width, Height);
            if (clip.IsEmpty) continue;

            SoftwareTexture? texture = command.TextureId == RenderTextureIds.Solid
                ? null
                : _textures.GetValueOrDefault(command.TextureId);

            int end = Math.Min(renderList.Indices.Count, command.IndexOffset + command.IndexCount);
            for (int i = command.IndexOffset; i + 2 < end; i += 3)
            {
                int i0 = checked((int)renderList.Indices[i]);
                int i1 = checked((int)renderList.Indices[i + 1]);
                int i2 = checked((int)renderList.Indices[i + 2]);
                if ((uint)i0 >= (uint)renderList.Vertices.Count ||
                    (uint)i1 >= (uint)renderList.Vertices.Count ||
                    (uint)i2 >= (uint)renderList.Vertices.Count)
                {
                    continue;
                }

                DrawVertex v0 = ScaleVertex(renderList.Vertices[i0]);
                DrawVertex v1 = ScaleVertex(renderList.Vertices[i1]);
                DrawVertex v2 = ScaleVertex(renderList.Vertices[i2]);
                RasterizeTriangle(v0, v1, v2, texture, command.Lcd, clip);
            }
        }
    }

    /// <inheritdoc />
    public void EndFrame()
    {
        ThrowIfDisposed();
    }

    /// <inheritdoc />
    public int CreateTexture(byte[] rgba, int width, int height)
    {
        ThrowIfDisposed();
        if (width < 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height < 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (rgba.Length != width * height * 4)
            throw new ArgumentException("Texture data must be tightly packed RGBA8.", nameof(rgba));

        int id = _nextTextureId++;
        _textures[id] = new SoftwareTexture(width, height, rgba);
        return id;
    }

    /// <inheritdoc />
    public void DestroyTexture(int textureId)
    {
        ThrowIfDisposed();
        _textures.Remove(textureId);
    }

    /// <summary>Clears the framebuffer to <paramref name="color"/>.</summary>
    public void Clear(Color color)
    {
        for (int i = 0; i < Pixels.Length; i += 4)
        {
            Pixels[i] = color.R;
            Pixels[i + 1] = color.G;
            Pixels[i + 2] = color.B;
            Pixels[i + 3] = color.A;
        }
    }

    /// <summary>Returns the RGBA color of one framebuffer pixel.</summary>
    public Color GetPixel(int x, int y)
    {
        if ((uint)x >= (uint)Width) throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= (uint)Height) throw new ArgumentOutOfRangeException(nameof(y));
        int offset = (y * Width + x) * 4;
        return new Color(Pixels[offset], Pixels[offset + 1], Pixels[offset + 2], Pixels[offset + 3]);
    }

    /// <summary>Writes the current framebuffer to a PNG file.</summary>
    public void SavePng(string path)
    {
        ThrowIfDisposed();
        PngWriter.WriteRgba(path, Pixels, Width, Height);
    }

    /// <summary>Returns the current framebuffer encoded as PNG bytes.</summary>
    public byte[] ToPngBytes()
    {
        ThrowIfDisposed();
        return PngWriter.EncodeRgba(Pixels, Width, Height);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _textures.Clear();
        _disposed = true;
    }

    private void EnsureFramebuffer(int width, int height)
    {
        if (Width == width && Height == height && Pixels.Length == width * height * 4)
            return;

        Width = width;
        Height = height;
        Pixels = new byte[width * height * 4];
    }

    private DrawVertex ScaleVertex(DrawVertex vertex)
        => vertex with
        {
            Pos = new Vector2(vertex.Pos.X * _frame.ScaleX, vertex.Pos.Y * _frame.ScaleY)
        };

    private ClipRect ScaleClip(ClipRect clip)
        => new(
            clip.X * _frame.ScaleX,
            clip.Y * _frame.ScaleY,
            clip.Width * _frame.ScaleX,
            clip.Height * _frame.ScaleY);

    private void RasterizeTriangle(
        DrawVertex v0,
        DrawVertex v1,
        DrawVertex v2,
        SoftwareTexture? texture,
        bool lcd,
        ClipRect clip)
    {
        float minXf = MathF.Min(v0.Pos.X, MathF.Min(v1.Pos.X, v2.Pos.X));
        float minYf = MathF.Min(v0.Pos.Y, MathF.Min(v1.Pos.Y, v2.Pos.Y));
        float maxXf = MathF.Max(v0.Pos.X, MathF.Max(v1.Pos.X, v2.Pos.X));
        float maxYf = MathF.Max(v0.Pos.Y, MathF.Max(v1.Pos.Y, v2.Pos.Y));

        int minX = Math.Max((int)MathF.Floor(minXf), (int)MathF.Floor(clip.X));
        int minY = Math.Max((int)MathF.Floor(minYf), (int)MathF.Floor(clip.Y));
        int maxX = Math.Min((int)MathF.Ceiling(maxXf), (int)MathF.Ceiling(clip.X + clip.Width) - 1);
        int maxY = Math.Min((int)MathF.Ceiling(maxYf), (int)MathF.Ceiling(clip.Y + clip.Height) - 1);
        minX = Math.Clamp(minX, 0, Math.Max(0, Width - 1));
        minY = Math.Clamp(minY, 0, Math.Max(0, Height - 1));
        maxX = Math.Clamp(maxX, 0, Math.Max(0, Width - 1));
        maxY = Math.Clamp(maxY, 0, Math.Max(0, Height - 1));
        if (minX > maxX || minY > maxY) return;

        float area = Edge(v0.Pos, v1.Pos, v2.Pos);
        if (MathF.Abs(area) < 0.00001f) return;

        for (int y = minY; y <= maxY; y++)
        {
            float py = y + 0.5f;
            for (int x = minX; x <= maxX; x++)
            {
                float px = x + 0.5f;
                Vector2 p = new(px, py);
                float w0 = Edge(v1.Pos, v2.Pos, p) / area;
                float w1 = Edge(v2.Pos, v0.Pos, p) / area;
                float w2 = Edge(v0.Pos, v1.Pos, p) / area;

                const float epsilon = -0.0001f;
                if (w0 < epsilon || w1 < epsilon || w2 < epsilon) continue;

                Color tint = InterpolateColor(v0.Color, v1.Color, v2.Color, w0, w1, w2);
                Color src = tint;
                if (texture != null)
                {
                    Vector2 uv = v0.Uv * w0 + v1.Uv * w1 + v2.Uv * w2;
                    Color texel = texture.SampleNearest(uv);
                    if (lcd)
                    {
                        BlendPixelLcd(x, y, tint, texel);
                        continue;
                    }

                    src = Multiply(tint, texel);
                }

                BlendPixel(x, y, src);
            }
        }
    }

    private static float Edge(Vector2 a, Vector2 b, Vector2 c)
        => (c.X - a.X) * (b.Y - a.Y) - (c.Y - a.Y) * (b.X - a.X);

    private static Color InterpolateColor(Color a, Color b, Color c, float wa, float wb, float wc)
        => new(
            ToByte(a.R * wa + b.R * wb + c.R * wc),
            ToByte(a.G * wa + b.G * wb + c.G * wc),
            ToByte(a.B * wa + b.B * wb + c.B * wc),
            ToByte(a.A * wa + b.A * wb + c.A * wc));

    private static Color Multiply(Color tint, Color texel)
        => new(
            (byte)((tint.R * texel.R + 127) / 255),
            (byte)((tint.G * texel.G + 127) / 255),
            (byte)((tint.B * texel.B + 127) / 255),
            (byte)((tint.A * texel.A + 127) / 255));

    private void BlendPixel(int x, int y, Color src)
    {
        if (src.A == 0) return;

        int offset = (y * Width + x) * 4;
        if (src.A == 255)
        {
            Pixels[offset] = src.R;
            Pixels[offset + 1] = src.G;
            Pixels[offset + 2] = src.B;
            Pixels[offset + 3] = 255;
            return;
        }

        int srcA = src.A;
        int invA = 255 - srcA;
        Pixels[offset] = (byte)((src.R * srcA + Pixels[offset] * invA + 127) / 255);
        Pixels[offset + 1] = (byte)((src.G * srcA + Pixels[offset + 1] * invA + 127) / 255);
        Pixels[offset + 2] = (byte)((src.B * srcA + Pixels[offset + 2] * invA + 127) / 255);
        Pixels[offset + 3] = (byte)(srcA + (Pixels[offset + 3] * invA + 127) / 255);
    }

    private void BlendPixelLcd(int x, int y, Color tint, Color coverage)
    {
        if (coverage.A == 0 || tint.A == 0) return;

        int offset = (y * Width + x) * 4;
        int covR = (coverage.R * tint.A + 127) / 255;
        int covG = (coverage.G * tint.A + 127) / 255;
        int covB = (coverage.B * tint.A + 127) / 255;
        int covA = (coverage.A * tint.A + 127) / 255;

        Pixels[offset] = (byte)((tint.R * covR + Pixels[offset] * (255 - covR) + 127) / 255);
        Pixels[offset + 1] = (byte)((tint.G * covG + Pixels[offset + 1] * (255 - covG) + 127) / 255);
        Pixels[offset + 2] = (byte)((tint.B * covB + Pixels[offset + 2] * (255 - covB) + 127) / 255);
        Pixels[offset + 3] = (byte)(covA + (Pixels[offset + 3] * (255 - covA) + 127) / 255);
    }

    private static byte ToByte(float value)
        => (byte)Math.Clamp((int)MathF.Round(value), 0, 255);

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SoftwareRenderer));
    }
}
