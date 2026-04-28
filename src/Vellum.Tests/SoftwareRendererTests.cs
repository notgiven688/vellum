using System.Numerics;
using Vellum.Rendering;
using Vellum.SoftwareRendering;
using Xunit;

namespace Vellum.Tests;

public sealed class SoftwareRendererTests
{
    [Fact]
    public void SoftwareRendererRasterizesSolidTriangles()
    {
        using var renderer = new SoftwareRenderer(24, 24);
        var list = new RenderList();
        list.Vertices.Add(new DrawVertex(new Vector2(4, 4), Vector2.Zero, Color.Red));
        list.Vertices.Add(new DrawVertex(new Vector2(20, 4), Vector2.Zero, Color.Red));
        list.Vertices.Add(new DrawVertex(new Vector2(4, 20), Vector2.Zero, Color.Red));
        list.Indices.AddRange([0, 1, 2]);
        list.Commands.Add(new DrawCommand(RenderTextureIds.Solid, 0, 3, default));

        renderer.BeginFrame(new RenderFrameInfo(24, 24));
        renderer.Render(list);
        renderer.EndFrame();

        Assert.Equal(Color.Red, renderer.GetPixel(7, 7));
        Assert.Equal(Color.Transparent, renderer.GetPixel(22, 22));
    }

    [Fact]
    public void SoftwareRendererSamplesTexturesAndAppliesClip()
    {
        using var renderer = new SoftwareRenderer(20, 20);
        int texture = renderer.CreateTexture(
        [
            255, 255, 255, 255,
            255, 255, 255, 255,
            255, 255, 255, 255,
            255, 255, 255, 255
        ], 2, 2);

        var list = new RenderList();
        list.Vertices.Add(new DrawVertex(new Vector2(2, 2), new Vector2(0, 0), Color.Blue));
        list.Vertices.Add(new DrawVertex(new Vector2(18, 2), new Vector2(1, 0), Color.Blue));
        list.Vertices.Add(new DrawVertex(new Vector2(18, 18), new Vector2(1, 1), Color.Blue));
        list.Vertices.Add(new DrawVertex(new Vector2(2, 18), new Vector2(0, 1), Color.Blue));
        list.Indices.AddRange([0, 1, 2, 0, 2, 3]);
        list.Commands.Add(new DrawCommand(texture, 0, 6, new ClipRect(4, 4, 8, 8), HasClip: true));

        renderer.BeginFrame(new RenderFrameInfo(20, 20));
        renderer.Render(list);
        renderer.EndFrame();

        Assert.Equal(Color.Blue, renderer.GetPixel(6, 6));
        Assert.Equal(Color.Transparent, renderer.GetPixel(14, 6));
    }

    [Fact]
    public void SoftwareRendererRendersVellumWidgetsAndExportsPng()
    {
        using var renderer = new SoftwareRenderer(260, 120, new Color(30, 30, 30));
        var ui = new Ui(renderer)
        {
            Font = UiFonts.DefaultSans,
            DefaultFontSize = 18f,
            Lcd = false
        };

        bool isChecked = true;
        ui.Frame(260, 120, new Vector2(34, 30), false, frame =>
        {
            frame.Button("Save");
            frame.Checkbox("Enabled", ref isChecked);
        });

        Assert.Contains(renderer.Pixels.Chunk(4), px => px[3] != 0);
        Assert.Contains(renderer.Pixels.Chunk(4), px => px[0] != renderer.ClearColor.R || px[1] != renderer.ClearColor.G || px[2] != renderer.ClearColor.B);

        byte[] png = renderer.ToPngBytes();
        Assert.Equal([137, 80, 78, 71, 13, 10, 26, 10], png[..8]);
        Assert.True(ContainsSequence(png, "IHDR"));
        Assert.True(ContainsSequence(png, "IDAT"));
        Assert.True(ContainsSequence(png, "IEND"));
    }

    [Fact]
    public void SoftwareRendererResizesFromFrameInfo()
    {
        using var renderer = new SoftwareRenderer(1, 1);

        renderer.BeginFrame(new RenderFrameInfo(10, 8, 20, 16));

        Assert.Equal(20, renderer.Width);
        Assert.Equal(16, renderer.Height);
        Assert.Equal(20 * 16 * 4, renderer.Pixels.Length);
    }

    private static bool ContainsSequence(byte[] bytes, string text)
    {
        byte[] needle = System.Text.Encoding.ASCII.GetBytes(text);
        for (int i = 0; i <= bytes.Length - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (bytes[i + j] == needle[j]) continue;
                match = false;
                break;
            }

            if (match) return true;
        }

        return false;
    }
}
