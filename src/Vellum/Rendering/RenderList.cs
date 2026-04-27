namespace Vellum.Rendering;

/// <summary>
/// Backend-facing draw data produced by Vellum for a single frame.
/// </summary>
/// <remarks>
/// Applications normally do not create or mutate this type. Custom render
/// backends receive it through <see cref="IRenderer.Render(RenderList)"/>.
/// </remarks>
public sealed class RenderList
{
    /// <summary>
    /// Vertex buffer for all draw commands in the frame.
    /// </summary>
    public List<DrawVertex> Vertices { get; } = new();

    /// <summary>
    /// Triangle index buffer for all draw commands in the frame.
    /// </summary>
    public List<uint> Indices { get; } = new();

    /// <summary>
    /// Ordered draw commands describing texture, clip, and index ranges.
    /// </summary>
    public List<DrawCommand> Commands { get; } = new();

    /// <summary>
    /// Clears all frame draw data while keeping list capacity for reuse.
    /// </summary>
    public void Clear()
    {
        Vertices.Clear();
        Indices.Clear();
        Commands.Clear();
    }
}
