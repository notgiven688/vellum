using System.Numerics;

namespace Vellum.Rendering;

/// <summary>
/// Geometry builder for the future imgui/egui-style render path.
/// It tessellates high-level shapes into a shared vertex/index list plus
/// draw commands grouped by texture and clip rect.
/// </summary>
internal sealed class Painter
{
    public const int SolidTextureId = RenderTextureIds.Solid;

    private readonly RenderList _renderList;
    private readonly List<ClipRect> _clipStack = new();
    private readonly List<Vector2> _outerPoints = new();
    private readonly List<Vector2> _innerPoints = new();
    private readonly List<Vector2> _trianglePoints = new();

    public Painter()
        : this(new RenderList())
    {
    }

    public Painter(RenderList renderList)
    {
        _renderList = renderList;
    }

    public RenderList RenderList => _renderList;

    public void Clear()
    {
        _renderList.Clear();
        _clipStack.Clear();
    }

    public void Append(RenderList other)
    {
        if (other.Vertices.Count == 0 || other.Indices.Count == 0 || other.Commands.Count == 0)
            return;

        uint vertexBase = (uint)_renderList.Vertices.Count;
        int indexBase = _renderList.Indices.Count;

        _renderList.Vertices.AddRange(other.Vertices);
        for (int i = 0; i < other.Indices.Count; i++)
            _renderList.Indices.Add(other.Indices[i] + vertexBase);

        for (int i = 0; i < other.Commands.Count; i++)
        {
            var command = other.Commands[i] with
            {
                IndexOffset = other.Commands[i].IndexOffset + indexBase
            };
            AppendExistingCommand(command);
        }
    }

    public void CopyClipStackTo(Painter other)
    {
        other._clipStack.Clear();
        other._clipStack.AddRange(_clipStack);
    }

    public void PushClip(float x, float y, float width, float height)
    {
        var next = new ClipRect(x, y, MathF.Max(0, width), MathF.Max(0, height));
        if (_clipStack.Count > 0)
            next = _clipStack[^1].Intersect(next);

        _clipStack.Add(next);
    }

    public void PopClip()
    {
        if (_clipStack.Count == 0)
            throw new InvalidOperationException("Painter.PopClip called with an empty clip stack.");

        _clipStack.RemoveAt(_clipStack.Count - 1);
    }

    public void DrawRect(
        float x,
        float y,
        float width,
        float height,
        Color fill,
        Color stroke = default,
        float strokeWidth = 0f,
        float radius = 0f)
    {
        if (fill.A > 0)
            FillRect(x, y, width, height, fill, radius);

        if (stroke.A > 0 && strokeWidth > 0)
            StrokeRect(x, y, width, height, stroke, strokeWidth, radius);
    }

    public void FillRect(float x, float y, float width, float height, Color color, float radius = 0f)
    {
        if (width <= 0 || height <= 0 || color.A == 0)
            return;

        if (radius <= 0.01f)
        {
            AddQuad(
                new Vector2(x, y),
                new Vector2(x + width, y),
                new Vector2(x + width, y + height),
                new Vector2(x, y + height),
                color,
                SolidTextureId,
                lcd: false);
            return;
        }

        BuildRoundedRectPoints(_outerPoints, x, y, width, height, radius);
        AddConvexPolygon(_outerPoints, color, SolidTextureId, lcd: false);
    }

    public void FillGradientRect(
        float x,
        float y,
        float width,
        float height,
        Color topLeft,
        Color topRight,
        Color bottomRight,
        Color bottomLeft)
    {
        if (width <= 0 || height <= 0 ||
            (topLeft.A == 0 && topRight.A == 0 && bottomRight.A == 0 && bottomLeft.A == 0))
        {
            return;
        }

        AddQuad(
            new Vector2(x, y),
            new Vector2(x + width, y),
            new Vector2(x + width, y + height),
            new Vector2(x, y + height),
            topLeft,
            topRight,
            bottomRight,
            bottomLeft,
            SolidTextureId,
            lcd: false);
    }

    public void StrokeRect(float x, float y, float width, float height, Color color, float strokeWidth, float radius = 0f)
    {
        if (width <= 0 || height <= 0 || color.A == 0 || strokeWidth <= 0)
            return;

        strokeWidth = MathF.Max(0, strokeWidth);
        if (strokeWidth * 2 >= width || strokeWidth * 2 >= height)
        {
            FillRect(x, y, width, height, color, radius);
            return;
        }

        BuildRoundedRectPoints(_outerPoints, x, y, width, height, radius);
        int segments = _outerPoints.Count / 4;
        BuildRoundedRectPoints(
            _innerPoints,
            x + strokeWidth,
            y + strokeWidth,
            width - strokeWidth * 2,
            height - strokeWidth * 2,
            MathF.Max(0, radius - strokeWidth),
            segments);
        AddRing(_outerPoints, _innerPoints, color, SolidTextureId, lcd: false);
    }

    public void AddTexturedQuad(
        float x,
        float y,
        float width,
        float height,
        int textureId,
        float u0,
        float v0,
        float u1,
        float v1,
        Color tint,
        bool lcd = false)
    {
        if (width <= 0 || height <= 0 || tint.A == 0)
            return;

        AddQuad(
            new Vector2(x, y),
            new Vector2(x + width, y),
            new Vector2(x + width, y + height),
            new Vector2(x, y + height),
            tint,
            textureId,
            lcd,
            new Vector2(u0, v0),
            new Vector2(u1, v0),
            new Vector2(u1, v1),
            new Vector2(u0, v1));
    }

    public void FillTriangle(Vector2 a, Vector2 b, Vector2 c, Color color, int textureId = SolidTextureId, bool lcd = false)
    {
        if (color.A == 0)
            return;

        _trianglePoints.Clear();
        _trianglePoints.Add(a);
        _trianglePoints.Add(b);
        _trianglePoints.Add(c);
        AddConvexPolygon(_trianglePoints, color, textureId, lcd);
    }

    private void AddQuad(
        Vector2 topLeft,
        Vector2 topRight,
        Vector2 bottomRight,
        Vector2 bottomLeft,
        Color color,
        int textureId,
        bool lcd,
        Vector2? uvTopLeft = null,
        Vector2? uvTopRight = null,
        Vector2? uvBottomRight = null,
        Vector2? uvBottomLeft = null)
    {
        int indexOffset = _renderList.Indices.Count;
        uint vertexBase = (uint)_renderList.Vertices.Count;

        _renderList.Vertices.Add(new DrawVertex(topLeft, uvTopLeft ?? Vector2.Zero, color));
        _renderList.Vertices.Add(new DrawVertex(topRight, uvTopRight ?? Vector2.Zero, color));
        _renderList.Vertices.Add(new DrawVertex(bottomRight, uvBottomRight ?? Vector2.Zero, color));
        _renderList.Vertices.Add(new DrawVertex(bottomLeft, uvBottomLeft ?? Vector2.Zero, color));

        _renderList.Indices.Add(vertexBase);
        _renderList.Indices.Add(vertexBase + 3);
        _renderList.Indices.Add(vertexBase + 2);
        _renderList.Indices.Add(vertexBase);
        _renderList.Indices.Add(vertexBase + 2);
        _renderList.Indices.Add(vertexBase + 1);

        AppendCommand(textureId, lcd, indexOffset, 6);
    }

    private void AddQuad(
        Vector2 topLeft,
        Vector2 topRight,
        Vector2 bottomRight,
        Vector2 bottomLeft,
        Color topLeftColor,
        Color topRightColor,
        Color bottomRightColor,
        Color bottomLeftColor,
        int textureId,
        bool lcd,
        Vector2? uvTopLeft = null,
        Vector2? uvTopRight = null,
        Vector2? uvBottomRight = null,
        Vector2? uvBottomLeft = null)
    {
        int indexOffset = _renderList.Indices.Count;
        uint vertexBase = (uint)_renderList.Vertices.Count;

        _renderList.Vertices.Add(new DrawVertex(topLeft, uvTopLeft ?? Vector2.Zero, topLeftColor));
        _renderList.Vertices.Add(new DrawVertex(topRight, uvTopRight ?? Vector2.Zero, topRightColor));
        _renderList.Vertices.Add(new DrawVertex(bottomRight, uvBottomRight ?? Vector2.Zero, bottomRightColor));
        _renderList.Vertices.Add(new DrawVertex(bottomLeft, uvBottomLeft ?? Vector2.Zero, bottomLeftColor));

        _renderList.Indices.Add(vertexBase);
        _renderList.Indices.Add(vertexBase + 3);
        _renderList.Indices.Add(vertexBase + 2);
        _renderList.Indices.Add(vertexBase);
        _renderList.Indices.Add(vertexBase + 2);
        _renderList.Indices.Add(vertexBase + 1);

        AppendCommand(textureId, lcd, indexOffset, 6);
    }

    private void AddConvexPolygon(List<Vector2> points, Color color, int textureId, bool lcd)
    {
        if (points.Count < 3 || color.A == 0)
            return;

        int indexOffset = _renderList.Indices.Count;
        uint vertexBase = (uint)_renderList.Vertices.Count;
        Vector2 center = Vector2.Zero;
        for (int i = 0; i < points.Count; i++)
            center += points[i];
        center /= points.Count;

        _renderList.Vertices.Add(new DrawVertex(center, Vector2.Zero, color));
        for (int i = 0; i < points.Count; i++)
            _renderList.Vertices.Add(new DrawVertex(points[i], Vector2.Zero, color));

        for (int i = 0; i < points.Count; i++)
        {
            uint current = vertexBase + 1u + (uint)i;
            uint next = vertexBase + 1u + (uint)((i + 1) % points.Count);
            _renderList.Indices.Add(vertexBase);
            _renderList.Indices.Add(next);
            _renderList.Indices.Add(current);
        }

        AppendCommand(textureId, lcd, indexOffset, points.Count * 3);
    }

    private void AddRing(List<Vector2> outer, List<Vector2> inner, Color color, int textureId, bool lcd)
    {
        if (outer.Count < 3 || inner.Count != outer.Count || color.A == 0)
            return;

        int indexOffset = _renderList.Indices.Count;
        uint vertexBase = (uint)_renderList.Vertices.Count;

        for (int i = 0; i < outer.Count; i++)
            _renderList.Vertices.Add(new DrawVertex(outer[i], Vector2.Zero, color));
        for (int i = 0; i < inner.Count; i++)
            _renderList.Vertices.Add(new DrawVertex(inner[i], Vector2.Zero, color));

        for (int i = 0; i < outer.Count; i++)
        {
            uint outer0 = vertexBase + (uint)i;
            uint outer1 = vertexBase + (uint)((i + 1) % outer.Count);
            uint inner0 = vertexBase + (uint)outer.Count + (uint)i;
            uint inner1 = vertexBase + (uint)outer.Count + (uint)((i + 1) % inner.Count);

            _renderList.Indices.Add(outer0);
            _renderList.Indices.Add(inner1);
            _renderList.Indices.Add(outer1);
            _renderList.Indices.Add(outer0);
            _renderList.Indices.Add(inner0);
            _renderList.Indices.Add(inner1);
        }

        AppendCommand(textureId, lcd, indexOffset, outer.Count * 6);
    }

    private void AppendCommand(int textureId, bool lcd, int indexOffset, int indexCount)
    {
        bool hasClip = _clipStack.Count > 0;
        ClipRect clip = hasClip ? _clipStack[^1] : default;
        AppendExistingCommand(new DrawCommand(textureId, indexOffset, indexCount, clip, hasClip, lcd));
    }

    private void AppendExistingCommand(DrawCommand command)
    {
        if (_renderList.Commands.Count > 0)
        {
            var last = _renderList.Commands[^1];
            if (last.TextureId == command.TextureId &&
                last.Lcd == command.Lcd &&
                last.HasClip == command.HasClip &&
                (!command.HasClip || last.ClipRect.Equals(command.ClipRect)) &&
                last.IndexOffset + last.IndexCount == command.IndexOffset)
            {
                _renderList.Commands[^1] = last with { IndexCount = last.IndexCount + command.IndexCount };
                return;
            }
        }

        _renderList.Commands.Add(command);
    }

    private static void BuildRoundedRectPoints(List<Vector2> dst, float x, float y, float width, float height, float radius, int? forcedSegments = null)
    {
        dst.Clear();
        if (width <= 0 || height <= 0)
            return;

        float clampedRadius = Math.Clamp(radius, 0, MathF.Min(width, height) * 0.5f);
        if (clampedRadius <= 0.01f)
        {
            if (forcedSegments.HasValue)
            {
                AppendSubdividedRect(dst, x, y, width, height, Math.Max(1, forcedSegments.Value));
                return;
            }

            dst.Add(new Vector2(x, y));
            dst.Add(new Vector2(x + width, y));
            dst.Add(new Vector2(x + width, y + height));
            dst.Add(new Vector2(x, y + height));
            return;
        }

        int segments = Math.Max(1, forcedSegments ?? GetCornerSegments(clampedRadius));
        AppendArc(dst, new Vector2(x + width - clampedRadius, y + clampedRadius), clampedRadius, -MathF.PI * 0.5f, 0, segments);
        AppendArc(dst, new Vector2(x + width - clampedRadius, y + height - clampedRadius), clampedRadius, 0, MathF.PI * 0.5f, segments);
        AppendArc(dst, new Vector2(x + clampedRadius, y + height - clampedRadius), clampedRadius, MathF.PI * 0.5f, MathF.PI, segments);
        AppendArc(dst, new Vector2(x + clampedRadius, y + clampedRadius), clampedRadius, MathF.PI, MathF.PI * 1.5f, segments);
    }

    private static void AppendSubdividedRect(List<Vector2> dst, float x, float y, float width, float height, int segments)
    {
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            dst.Add(new Vector2(x + width, y + height * t));
        }

        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            dst.Add(new Vector2(x + width * (1 - t), y + height));
        }

        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            dst.Add(new Vector2(x, y + height * (1 - t)));
        }

        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            dst.Add(new Vector2(x + width * t, y));
        }
    }

    private static void AppendArc(List<Vector2> dst, Vector2 center, float radius, float startAngle, float endAngle, int segments)
    {
        for (int i = 0; i <= segments; i++)
        {
            if (dst.Count > 0 && i == 0)
                continue;

            float t = (float)i / segments;
            float angle = startAngle + (endAngle - startAngle) * t;
            dst.Add(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius);
        }
    }

    private static int GetCornerSegments(float radius)
        => Math.Clamp((int)MathF.Ceiling(radius), 4, 12);
}
