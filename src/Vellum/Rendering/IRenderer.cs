namespace Vellum.Rendering;

/// <summary>
/// Implemented by host backends that render Vellum draw data.
/// </summary>
/// <remarks>
/// Vellum produces geometry in logical pixels with a top-left origin. Backends use
/// <see cref="RenderFrameInfo"/> to map that data to the physical framebuffer.
/// </remarks>
public interface IRenderer
{
    /// <summary>
    /// Prepares the backend for a new frame with explicit logical and framebuffer dimensions.
    /// </summary>
    void BeginFrame(RenderFrameInfo frame);

    /// <summary>
    /// Renders the frame draw data produced by Vellum.
    /// </summary>
    /// <remarks>
    /// Draw commands must be submitted in order. Vertices and clip rectangles are
    /// expressed in logical pixels; convert clipping to framebuffer pixels when
    /// required by the graphics API.
    /// </remarks>
    void Render(RenderList renderList);

    /// <summary>
    /// Finishes the frame. This may be empty if presentation is owned by the windowing layer.
    /// </summary>
    void EndFrame();

    /// <summary>
    /// Uploads tightly packed RGBA8 pixels and returns a non-zero backend texture id.
    /// </summary>
    int CreateTexture(byte[] rgba, int width, int height);

    /// <summary>
    /// Releases a texture id previously returned by <see cref="CreateTexture"/>.
    /// </summary>
    void DestroyTexture(int textureId);
}
