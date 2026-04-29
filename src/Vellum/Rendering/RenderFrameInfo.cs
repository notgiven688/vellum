namespace Vellum.Rendering;

/// <summary>
/// Describes the logical UI size and physical framebuffer size for one Vellum render frame.
/// </summary>
/// <param name="LogicalWidth">Width of the Vellum layout/input coordinate space in logical pixels.</param>
/// <param name="LogicalHeight">Height of the Vellum layout/input coordinate space in logical pixels.</param>
/// <param name="FramebufferWidth">Width of the render target in physical framebuffer pixels.</param>
/// <param name="FramebufferHeight">Height of the render target in physical framebuffer pixels.</param>
public readonly record struct RenderFrameInfo(
    int LogicalWidth,
    int LogicalHeight,
    int FramebufferWidth,
    int FramebufferHeight)
{
    /// <summary>
    /// Creates a scale-1 frame where logical and framebuffer sizes are identical.
    /// </summary>
    public RenderFrameInfo(int width, int height)
        : this(width, height, width, height)
    {
    }

    /// <summary>
    /// Physical pixels per logical pixel on the X axis.
    /// </summary>
    public float ScaleX => LogicalWidth > 0 ? FramebufferWidth / (float)LogicalWidth : 1f;

    /// <summary>
    /// Physical pixels per logical pixel on the Y axis.
    /// </summary>
    public float ScaleY => LogicalHeight > 0 ? FramebufferHeight / (float)LogicalHeight : 1f;

    /// <summary>
    /// Largest framebuffer scale factor for this frame.
    /// </summary>
    public float MaxScale => MathF.Max(ScaleX, ScaleY);

    /// <summary>
    /// Returns a frame descriptor with non-negative dimensions and positive finite scale values.
    /// </summary>
    public RenderFrameInfo Normalized()
    {
        int logicalWidth = Math.Max(0, LogicalWidth);
        int logicalHeight = Math.Max(0, LogicalHeight);
        int framebufferWidth = FramebufferWidth > 0 ? FramebufferWidth : logicalWidth;
        int framebufferHeight = FramebufferHeight > 0 ? FramebufferHeight : logicalHeight;

        return new RenderFrameInfo(logicalWidth, logicalHeight, framebufferWidth, framebufferHeight);
    }
}
