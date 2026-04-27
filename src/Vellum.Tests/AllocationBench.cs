using System.Numerics;
using Vellum;
using Vellum.Rendering;

internal static class AllocationBench
{
    public static void Run(TrueTypeFont font)
    {
        Console.WriteLine();
        Console.WriteLine("Allocation benchmark (bytes per frame, steady-state)");
        Console.WriteLine("-----------------------------------------------------");

        Measure("empty frame", font, static ui => { });
        Measure("50x label (constant text)", font, static ui => { for (int i = 0; i < 50; i++) ui.Label("Hello, world"); });
        Measure("50x label (interpolated)", font, static ui => { for (int i = 0; i < 50; i++) ui.Label($"Item {i}: value {i * 7}"); });
        Measure("50x label (wrapped)", font, static ui => { for (int i = 0; i < 50; i++) ui.Label("The quick brown fox jumps over the lazy dog and a few more words besides", maxWidth: 200, wrap: TextWrapMode.WordWrap); });
        Measure("50x button", font, static ui => { for (int i = 0; i < 50; i++) ui.Button("Click me"); });
        Measure("50x checkbox", font, static ui =>
        {
            bool b = false;
            for (int i = 0; i < 50; i++) ui.Checkbox("Toggle me", ref b);
        });
        Measure("text field", font, static ui =>
        {
            string s = "hello";
            ui.TextField("name", ref s, 200);
        });
        Measure("text area (10 lines)", font, static ui =>
        {
            string s = "line 1\nline 2\nline 3\nline 4\nline 5\nline 6\nline 7\nline 8\nline 9\nline 10";
            ui.TextArea("notes", ref s, 200, 200);
        });
        Measure("horizontal+width nested", font, static ui =>
        {
            for (int i = 0; i < 20; i++)
            {
                using (ui.Horizontal())
                {
                    using (ui.Width(100)) ui.Label("Left");
                    using (ui.Width(100)) ui.Label("Right");
                }
            }
        });
    }

    private static void Measure(string name, TrueTypeFont font, Action<Ui> scene)
    {
        const int Warmup = 20;
        const int Iterations = 200;

        var renderer = new NoopRenderer();
        var ui = new Ui(renderer)
        {
            Font = font,
            DefaultFontSize = 16f,
            Lcd = false,
        };

        for (int i = 0; i < Warmup; i++)
            ui.Frame(800, 600, Vector2.Zero, false, scene);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long before = GC.GetAllocatedBytesForCurrentThread();
        int gen0Before = GC.CollectionCount(0);

        for (int i = 0; i < Iterations; i++)
            ui.Frame(800, 600, Vector2.Zero, false, scene);

        long after = GC.GetAllocatedBytesForCurrentThread();
        int gen0After = GC.CollectionCount(0);
        double bytesPerFrame = (double)(after - before) / Iterations;

        Console.WriteLine($"  {name,-32} {bytesPerFrame,10:N1} B/frame  (gen0 collections: {gen0After - gen0Before})");
    }
}

internal sealed class NoopRenderer : IRenderer
{
    private int _nextTextureId = 1;

    public void BeginFrame(RenderFrameInfo frame) { }
    public void Render(RenderList renderList) { }
    public void EndFrame() { }
    public int CreateTexture(byte[] rgba, int width, int height) => _nextTextureId++;
    public void DestroyTexture(int textureId) { }
}
