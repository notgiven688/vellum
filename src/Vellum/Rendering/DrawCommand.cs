namespace Vellum.Rendering;

/// <summary>
/// Describes one contiguous indexed draw call in a <see cref="RenderList"/>.
/// </summary>
/// <param name="TextureId">
/// Backend texture id to bind, or <see cref="RenderTextureIds.Solid"/> for solid-color geometry.
/// </param>
/// <param name="IndexOffset">First index in <see cref="RenderList.Indices"/>.</param>
/// <param name="IndexCount">Number of indices to draw.</param>
/// <param name="ClipRect">Top-left based clipping rectangle for this command.</param>
/// <param name="HasClip">Whether <paramref name="ClipRect"/> should be applied.</param>
/// <param name="Lcd">Whether the command uses an LCD text atlas and needs LCD-aware blending.</param>
public readonly record struct DrawCommand(
    int TextureId,
    int IndexOffset,
    int IndexCount,
    ClipRect ClipRect,
    bool HasClip = false,
    bool Lcd = false);
