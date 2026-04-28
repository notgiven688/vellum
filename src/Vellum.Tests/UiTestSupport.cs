using System.Numerics;
using System.Text;
using Vellum.Rendering;

namespace Vellum.Tests;

internal static class UiTestSupport
{
    public static Ui CreateUi(UiTestRenderer renderer, IUiPlatform? platform = null)
    {
        return new Ui(renderer)
        {
            Font = UiFonts.DefaultSans,
            DefaultFontSize = 18f,
            Lcd = false,
            Platform = platform ?? NullUiPlatform.Instance,
            RootPadding = 0f
        };
    }

    public static UiInputState Input(
        string text = "",
        bool shift = false,
        bool ctrl = false,
        bool alt = false,
        bool meta = false,
        Vector2 wheel = default,
        UiKey[]? keys = null,
        UiMouseButton[]? mouseButtons = null,
        double? timeSeconds = null)
    {
        HashSet<UiKey>? pressed = null;
        if (keys is { Length: > 0 })
            pressed = new HashSet<UiKey>(keys);

        HashSet<UiMouseButton>? downButtons = null;
        if (mouseButtons is { Length: > 0 })
            downButtons = new HashSet<UiMouseButton>(mouseButtons);

        return new UiInputState(text, pressed, wheel, shift, ctrl, alt, meta, downButtons, timeSeconds);
    }

    public static Vector2 Inside(Response response, float inset = 4f)
    {
        float x = response.X + MathF.Min(inset, MathF.Max(0.5f, response.W * 0.5f));
        float y = response.Y + MathF.Min(inset, MathF.Max(0.5f, response.H * 0.5f));
        return new Vector2(x, y);
    }

    public static bool HasVertexColor(RenderList? renderList, Color color)
        => renderList != null && renderList.Vertices.Any(vertex => vertex.Color.Equals(color));

    public static DrawVertex[] VerticesWithColor(RenderList? renderList, Color color)
        => renderList == null
            ? []
            : renderList.Vertices.Where(vertex => vertex.Color.Equals(color)).ToArray();

    public static GlyphAtlas CreateAtlas(
        UiTestRenderer renderer,
        string text,
        float pixelHeight = 18f,
        float rasterScale = 1f,
        bool lcd = false)
    {
        var codepoints = new HashSet<int>();
        foreach (Rune rune in text.EnumerateRunes())
        {
            if (rune.Value == '\r' || rune.Value == '\n')
                continue;

            codepoints.Add(rune.Value);
        }

        return CreateAtlas(renderer, codepoints, pixelHeight, rasterScale, lcd);
    }

    public static GlyphAtlas CreateAtlas(
        UiTestRenderer renderer,
        IEnumerable<int> codepoints,
        float pixelHeight = 18f,
        float rasterScale = 1f,
        bool lcd = false)
    {
        var atlas = new GlyphAtlas(UiFonts.DefaultSans, pixelHeight, rasterScale, lcd);
        atlas.Build(renderer, codepoints);
        return atlas;
    }
}

internal sealed class UiTestRenderer : IRenderer
{
    public int CreateTextureCalls { get; private set; }
    public int DestroyTextureCalls { get; private set; }
    public RenderList? LastRenderList { get; private set; }
    public RenderFrameInfo LastFrame { get; private set; }

    private int _nextTextureId = 1;

    public void BeginFrame(RenderFrameInfo frame)
    {
        LastFrame = frame;
        LastRenderList = null;
    }

    public void Render(RenderList renderList) => LastRenderList = CloneRenderList(renderList);

    public void EndFrame()
    {
    }

    public int CreateTexture(byte[] rgba, int width, int height)
    {
        CreateTextureCalls++;
        return _nextTextureId++;
    }

    public void DestroyTexture(int textureId) => DestroyTextureCalls++;

    private static RenderList CloneRenderList(RenderList source)
    {
        var clone = new RenderList();
        clone.Vertices.AddRange(source.Vertices);
        clone.Indices.AddRange(source.Indices);
        clone.Commands.AddRange(source.Commands);
        return clone;
    }
}

internal sealed class UiTestPlatform : IUiPlatform
{
    public string ClipboardText { get; set; } = string.Empty;
    public UiCursor LastCursor { get; private set; } = UiCursor.Arrow;

    public string GetClipboardText() => ClipboardText;

    public void SetClipboardText(string text) => ClipboardText = text;

    public void SetCursor(UiCursor cursor) => LastCursor = cursor;
}
