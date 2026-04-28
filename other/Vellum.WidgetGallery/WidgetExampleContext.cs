using Vellum.Rendering;
using Vellum.SoftwareRendering;

namespace Vellum.WidgetGallery;

internal sealed class WidgetExampleContext
{
    public WidgetExampleContext(SoftwareRenderer renderer)
    {
        Renderer = renderer;
        CheckerTexture = CreateCheckerTexture(renderer);
    }

    public SoftwareRenderer Renderer { get; }

    public int CheckerTexture { get; }

    private static int CreateCheckerTexture(SoftwareRenderer renderer)
    {
        const int size = 32;
        byte[] pixels = new byte[size * size * 4];
        var a = new Color(82, 92, 108);
        var b = new Color(130, 145, 166);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Color c = ((x / 8 + y / 8) & 1) == 0 ? a : b;
            int offset = (y * size + x) * 4;
            pixels[offset] = c.R;
            pixels[offset + 1] = c.G;
            pixels[offset + 2] = c.B;
            pixels[offset + 3] = c.A;
        }

        return renderer.CreateTexture(pixels, size, size);
    }
}
