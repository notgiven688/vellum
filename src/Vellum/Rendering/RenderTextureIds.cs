namespace Vellum.Rendering;

/// <summary>
/// Reserved texture ids emitted by Vellum render commands.
/// </summary>
public static class RenderTextureIds
{
    /// <summary>
    /// Reserved texture id for solid-color geometry. Backends should render this
    /// with a private 1x1 white texture or an equivalent solid-fill path.
    /// </summary>
    public const int Solid = 0;
}
