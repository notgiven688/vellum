namespace Vellum.Rendering;

/// <summary>
/// Describes the logical UI size and physical framebuffer size for one Vellum render frame.
/// </summary>
/// <param name="LogicalWidth">Width of the Vellum layout/input coordinate space in logical pixels.</param>
/// <param name="LogicalHeight">Height of the Vellum layout/input coordinate space in logical pixels.</param>
/// <param name="FramebufferWidth">Width of the render target in physical framebuffer pixels.</param>
/// <param name="FramebufferHeight">Height of the render target in physical framebuffer pixels.</param>
/// <param name="ScaleX">Physical pixels per logical pixel on the X axis.</param>
/// <param name="ScaleY">Physical pixels per logical pixel on the Y axis.</param>
public readonly record struct RenderFrameInfo(
    int LogicalWidth,
    int LogicalHeight,
    int FramebufferWidth,
    int FramebufferHeight,
    float ScaleX,
    float ScaleY)
{
    /// <summary>
    /// Creates a scale-1 frame where logical and framebuffer sizes are identical.
    /// </summary>
    public RenderFrameInfo(int width, int height)
        : this(width, height, width, height, 1f, 1f)
    {
    }

    /// <summary>
    /// Creates a frame and derives scale from framebuffer size divided by logical size.
    /// </summary>
    public RenderFrameInfo(int logicalWidth, int logicalHeight, int framebufferWidth, int framebufferHeight)
        : this(
            logicalWidth,
            logicalHeight,
            framebufferWidth,
            framebufferHeight,
            logicalWidth > 0 ? framebufferWidth / (float)logicalWidth : 1f,
            logicalHeight > 0 ? framebufferHeight / (float)logicalHeight : 1f)
    {
    }

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

        float scaleX = logicalWidth > 0 && framebufferWidth > 0
            ? framebufferWidth / (float)logicalWidth
            : float.IsFinite(ScaleX) && ScaleX > 0 ? ScaleX : 1f;
        float scaleY = logicalHeight > 0 && framebufferHeight > 0
            ? framebufferHeight / (float)logicalHeight
            : float.IsFinite(ScaleY) && ScaleY > 0 ? ScaleY : 1f;

        return new RenderFrameInfo(logicalWidth, logicalHeight, framebufferWidth, framebufferHeight, scaleX, scaleY);
    }
}
