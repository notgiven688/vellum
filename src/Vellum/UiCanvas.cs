using System.Numerics;
using Vellum.Rendering;

namespace Vellum;

/// <summary>
/// Immediate drawing surface reserved inside a Vellum layout.
/// </summary>
/// <remarks>
/// Use this from <see cref="Ui.Canvas(float, float, Action{UiCanvas}, bool)"/> callbacks
/// for custom widgets and visualizations.
/// </remarks>
public sealed class UiCanvas
{
    private readonly Ui _ui;
    private readonly float _originX;
    private readonly float _originY;

    internal UiCanvas(Ui ui, float originX, float originY, float width, float height)
    {
        _ui = ui;
        _originX = originX;
        _originY = originY;
        Width = width;
        Height = height;
    }

    /// <summary>Canvas left edge in the parent UI coordinate space.</summary>
    public float X => _originX;
    /// <summary>Canvas top edge in the parent UI coordinate space.</summary>
    public float Y => _originY;
    /// <summary>Canvas width in logical pixels.</summary>
    public float Width { get; }
    /// <summary>Canvas height in logical pixels.</summary>
    public float Height { get; }
    /// <summary>Canvas size in logical pixels.</summary>
    public Vector2 Size => new(Width, Height);
    /// <summary>Pointer position relative to the canvas origin.</summary>
    public Vector2 MousePosition => _ui.MousePosition - new Vector2(_originX, _originY);
    /// <summary>Pointer delta in logical pixels for the current frame.</summary>
    public Vector2 MouseDelta => _ui.MouseDelta;
    /// <summary>Whether the pointer is inside the canvas bounds and active hit clip.</summary>
    public bool Hovered => HitTest(0, 0, Width, Height);

    /// <summary>Returns whether the pointer is inside a rectangle relative to the canvas origin.</summary>
    public bool HitTest(float x, float y, float width, float height)
        => _ui.HitTestAbsolute(_originX + x, _originY + y, width, height);

    /// <summary>Returns whether a mouse button is currently held.</summary>
    public bool IsMouseDown(UiMouseButton button) => _ui.IsMouseDown(button);
    /// <summary>Returns whether a mouse button was pressed this frame.</summary>
    public bool IsMousePressed(UiMouseButton button) => _ui.IsMousePressed(button);
    /// <summary>Returns whether a mouse button was released this frame.</summary>
    public bool IsMouseReleased(UiMouseButton button) => _ui.IsMouseReleased(button);
    /// <summary>Returns whether a mouse button was double-clicked this frame.</summary>
    public bool IsMouseDoubleClicked(UiMouseButton button) => _ui.IsMouseDoubleClicked(button);

    /// <summary>Draws a filled and/or stroked rectangle relative to the canvas origin.</summary>
    public void DrawRect(
        float x,
        float y,
        float width,
        float height,
        Color fill,
        Color stroke = default,
        float strokeWidth = 0f,
        float radius = 0f)
        => _ui.Painter.DrawRect(_originX + x, _originY + y, width, height, fill, stroke, strokeWidth, radius);

    /// <summary>Draws a filled rectangle relative to the canvas origin.</summary>
    public void FillRect(float x, float y, float width, float height, Color color, float radius = 0f)
        => _ui.Painter.FillRect(_originX + x, _originY + y, width, height, color, radius);

    /// <summary>Draws a stroked rectangle relative to the canvas origin.</summary>
    public void StrokeRect(float x, float y, float width, float height, Color color, float strokeWidth = 1f, float radius = 0f)
        => _ui.Painter.StrokeRect(_originX + x, _originY + y, width, height, color, strokeWidth, radius);

    /// <summary>Draws a texture by backend texture id relative to the canvas origin.</summary>
    public void DrawImage(
        int textureId,
        float x,
        float y,
        float width,
        float height,
        Color? tint = null)
    {
        Color resolvedTint = tint ?? Color.White;
        _ui.Painter.AddTexturedQuad(_originX + x, _originY + y, width, height, textureId, 0, 0, 1, 1, resolvedTint);
    }

    /// <summary>Pushes a clipping rectangle relative to the canvas origin.</summary>
    public void PushClip(float x, float y, float width, float height)
        => _ui.Painter.PushClip(_originX + x, _originY + y, width, height);

    /// <summary>Pops the most recent canvas clipping rectangle.</summary>
    public void PopClip()
        => _ui.Painter.PopClip();

    /// <summary>Draws text relative to the canvas origin.</summary>
    public void DrawText(
        string text,
        float x,
        float y,
        float? size = null,
        Color? color = null,
        float? maxWidth = null,
        TextWrapMode wrap = TextWrapMode.NoWrap,
        TextOverflowMode overflow = TextOverflowMode.Visible,
        int maxLines = int.MaxValue)
        => _ui.DrawCanvasText(text, _originX + x, _originY + y, size, color, maxWidth, wrap, overflow, maxLines);

    /// <summary>Measures text using Vellum's current font and wrapping rules.</summary>
    public Vector2 MeasureText(
        string text,
        float? size = null,
        float? maxWidth = null,
        TextWrapMode wrap = TextWrapMode.NoWrap,
        TextOverflowMode overflow = TextOverflowMode.Visible,
        int maxLines = int.MaxValue)
        => _ui.MeasureCanvasText(text, size, maxWidth, wrap, overflow, maxLines);
}
