using System.Numerics;
using System.Reflection;
using Vellum;
using Vellum.Rendering;
using Xunit;

namespace Vellum.Tests;

public sealed class VellumTests
{
    private static TrueTypeFont Font => UiFonts.DefaultSans;
    private static string OutputDir => Path.Combine(FindRepositoryRoot(), "artifacts", "Vellum.Tests", "pgm");

    [Fact]
    public void Painter() => RunPainterTests();

    [Fact]
    public void UiText() => RunUiTextTests(Font);

    [Fact]
    public void UiTextInput() => RunUiTextInputTests(Font);

    [Fact]
    public void UiTextArea() => RunUiTextAreaTests(Font);

    [Fact]
    public void UiLayout() => RunUiLayoutTests(Font);

    [Fact]
    public void UiCanvas() => RunUiCanvasTests(Font);

    [Fact]
    public void UiPlatform() => RunUiPlatformTests(Font);

    [Fact]
    public void UiMouse() => RunUiMouseTests(Font);

    [Fact]
    public void UiFocus() => RunUiFocusTests(Font);

    [Fact]
    public void UiSemantics() => RunUiSemanticsTests(Font);

    [Fact]
    public void UiWidgets() => RunUiWidgetTests(Font);

    [Fact]
    public void UiSliders() => RunUiSliderTests(Font);

    [Fact]
    public void UiScroll() => RunUiScrollTests(Font);

    [Fact]
    public void UiPopups() => RunUiPopupTests(Font);

    [Fact]
    public void UiLifetime() => RunUiLifetimeTests(Font);

    [Fact]
    public void UiRendering() => RunUiRenderingTests(Font);

    [Fact]
    public void PublicApiBoundary() => RunPublicApiBoundaryTests();

    [Fact]
    public void FontRasterizerWritesDiagnosticPgms()
    {
        string outputDir = OutputDir;
        Directory.CreateDirectory(outputDir);

        TrueTypeFont font = Font;
        var vm = font.GetFontVMetrics();
        Console.WriteLine($"Font vmetrics: ascent={vm.Ascent} descent={vm.Descent} lineGap={vm.LineGap}");

        float[] sizes = [12f, 24f, 48f];
        string text = "Hello, World!";

        foreach (float size in sizes)
        {
            float scale = font.ScaleForPixelHeight(size);
            Console.WriteLine($"\n--- {size}px (scale={scale:F5}) ---");

            foreach (char ch in text)
            {
                int glyphIndex = font.FindGlyphIndex(ch);
                var bitmap = font.RasterizeGlyph(glyphIndex, scale, out int w, out int h, out int ox, out int oy);

                Console.WriteLine($"  '{ch}' glyph={glyphIndex} bitmap={w}x{h} offset=({ox},{oy})");

                if (bitmap != null)
                {
                    string name = ch == ' ' ? "space" : ch == ',' ? "comma" : ch == '!' ? "excl" : ch.ToString();
                    WritePgm(Path.Combine(outputDir, $"{(int)size}px_{name}.pgm"), bitmap, w, h);
                }
            }

            RenderLine(font, text, scale, size, outputDir);
        }

        Assert.True(File.Exists(Path.Combine(outputDir, "48px_line.pgm")));
    }

    static string FindRepositoryRoot()
    {
        string? current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "LICENSE")) && Directory.Exists(Path.Combine(current, "src")))
                return current;

            current = Directory.GetParent(current)?.FullName;
        }

        return Directory.GetCurrentDirectory();
    }

    static void WritePgm(string path, byte[] pixels, int width, int height)
    {
        using var f = new StreamWriter(path);
        f.WriteLine("P2");
        f.WriteLine($"{width} {height}");
        f.WriteLine("255");
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (x > 0) f.Write(' ');
                f.Write(pixels[y * width + x]);
            }
            f.WriteLine();
        }
    }

    static void RunUiTextTests(TrueTypeFont font)
    {
        int passed = 0, failed = 0;

        void Check(string name, bool condition)
        {
            Assert.True(condition, name);
            Console.WriteLine($"  PASS  {name}");
            passed++;
        }

        Console.WriteLine("=== Ui Text Tests ===");

        var renderer = new TestRenderer();
        var ui = new Ui(renderer)
        {
            Font = font,
            DefaultFontSize = 18f,
            Lcd = false
        };

        Response unicode = default;
        ui.Frame(800, 600, Vector2.Zero, false, frame =>
        {
            unicode = frame.Label("Grüße 𝄞");
        });
        Check("unicode label has size", unicode.W > 0 && unicode.H > 0);
        Check("embedded default font resolves em dash", UiFonts.DefaultSans.FindGlyphIndex(0x2014) != 0);

        int textureCallsAfterAscii = renderer.CreateTextureCalls;
        ui.Frame(800, 600, Vector2.Zero, false, frame =>
        {
            frame.Label("Привет");
        });
        Check("atlas expands for non-ascii text", renderer.CreateTextureCalls > textureCallsAfterAscii);

        Response ellipsis = default;
        ui.Frame(800, 600, Vector2.Zero, false, frame =>
        {
            ellipsis = frame.Label("Alpha Beta Gamma Delta", maxWidth: 70, overflow: TextOverflowMode.Ellipsis);
        });
        Check("ellipsis label width is constrained", ellipsis.W <= 70.5f && ellipsis.W > 0);

        Response clipped = default;
        ui.Frame(800, 600, Vector2.Zero, false, frame =>
        {
            clipped = frame.Label("Alpha Beta Gamma Delta", maxWidth: 70, overflow: TextOverflowMode.Clip);
        });
        Check("clip label width is constrained", clipped.W <= 70.5f && clipped.W > 0);

        Response wrapped = default;
        ui.Frame(800, 600, Vector2.Zero, false, frame =>
        {
            wrapped = frame.Label("Alpha Beta Gamma Delta", maxWidth: 70, wrap: TextWrapMode.WordWrap);
        });
        Check("wrapped label width is constrained", wrapped.W <= 70.5f && wrapped.W > 0);
        Check("wrapped label grows vertically", wrapped.H > ellipsis.H);

        Response wrappedEllipsis = default;
        ui.Frame(800, 600, Vector2.Zero, false, frame =>
        {
            wrappedEllipsis = frame.Label(
                "Alpha Beta Gamma Delta Epsilon Zeta",
                maxWidth: 70,
                wrap: TextWrapMode.WordWrap,
                overflow: TextOverflowMode.Ellipsis,
                maxLines: 2);
        });
        Check("wrapped ellipsis width is constrained", wrappedEllipsis.W <= 70.5f && wrappedEllipsis.W > 0);
        Check("wrapped ellipsis is multiline", wrappedEllipsis.H > ellipsis.H);
        Check("wrapped ellipsis respects max lines", wrappedEllipsis.H < wrapped.H * 2);

        Console.WriteLine($"Ui text: {passed} passed, {failed} failed\n");
    }

    static void RunPainterTests()
    {
        int passed = 0, failed = 0;

        void Check(string name, bool condition)
        {
            Assert.True(condition, name);
            Console.WriteLine($"  PASS  {name}");
            passed++;
        }

        Console.WriteLine("=== Painter Tests ===");

        {
            var painter = new Painter();
            painter.FillRect(0, 0, 20, 10, Color.Red);
            painter.FillRect(24, 0, 20, 10, Color.Green);

            Check("solid fills batch into one command", painter.RenderList.Commands.Count == 1);
            Check(
                "solid fills emit expected quad geometry",
                painter.RenderList.Vertices.Count == 8 &&
                painter.RenderList.Indices.Count == 12 &&
                painter.RenderList.Commands[0].TextureId == RenderTextureIds.Solid);
            Check(
                "solid fills emit front-facing triangles",
                AllTrianglesFrontFacing(painter.RenderList));
        }

        {
            var painter = new Painter();
            painter.PushClip(0, 0, 20, 20);
            painter.PushClip(10, 8, 20, 20);
            painter.FillRect(8, 8, 10, 10, Color.White);

            var cmd = painter.RenderList.Commands.Single();
            Check(
                "nested clips intersect in draw command metadata",
                cmd.HasClip && cmd.ClipRect == new ClipRect(10, 8, 10, 12));
        }

        {
            var painter = new Painter();
            painter.FillRect(0, 0, 10, 10, Color.White);
            painter.PushClip(0, 0, 8, 8);
            painter.FillRect(4, 0, 10, 10, Color.White);
            painter.PopClip();
            painter.FillRect(24, 0, 10, 10, Color.White);

            Check("clip changes split batching into separate commands", painter.RenderList.Commands.Count == 3);
        }

        {
            var painter = new Painter();
            painter.StrokeRect(0, 0, 40, 20, Color.Blue, 2, radius: 6);

            Check(
                "rounded stroke emits ring geometry",
                painter.RenderList.Commands.Count == 1 &&
                painter.RenderList.Vertices.Count > 8 &&
                painter.RenderList.Indices.Count > 24);
            Check(
                "rounded stroke emits front-facing triangles",
                AllTrianglesFrontFacing(painter.RenderList));
        }

        {
            var painter = new Painter();
            painter.StrokeRect(0, 0, 40, 20, Color.Blue, 3, radius: 2);

            int innerOffset = painter.RenderList.Vertices.Count / 2;
            int uniqueInnerPoints = painter.RenderList.Vertices
                .Skip(innerOffset)
                .Select(vertex => vertex.Pos)
                .Distinct()
                .Count();

            Check(
                "square inner stroke keeps a subdivided perimeter",
                uniqueInnerPoints > 8);
        }

        {
            var painter = new Painter();
            painter.AddTexturedQuad(0, 0, 10, 10, 7, 0, 0, 1, 1, Color.White);
            painter.AddTexturedQuad(12, 0, 10, 10, 7, 0, 0, 1, 1, Color.White);
            painter.AddTexturedQuad(24, 0, 10, 10, 7, 0, 0, 1, 1, Color.White, lcd: true);

            Check(
                "textured quads batch by texture and lcd mode",
                painter.RenderList.Commands.Count == 2 &&
                painter.RenderList.Commands[0].TextureId == 7 &&
                !painter.RenderList.Commands[0].Lcd &&
                painter.RenderList.Commands[0].IndexCount == 12 &&
                painter.RenderList.Commands[1].Lcd &&
                painter.RenderList.Commands[1].IndexCount == 6);
        }

        Console.WriteLine($"Painter: {passed} passed, {failed} failed\n");
    }

    static void RunPublicApiBoundaryTests()
    {
        int passed = 0, failed = 0;

        void Check(string name, bool condition, string? detail = null)
        {
            Assert.True(condition, detail is null ? name : $"{name}: {detail}");
            Console.WriteLine($"  PASS  {name}");
            passed++;
        }

        Console.WriteLine("=== Public API Boundary Tests ===");

        string[] expected =
        [
            "Vellum.EdgeInsets",
            "Vellum.DockingState",
            "Vellum.FontVMetrics",
            "Vellum.GlyphMetrics",
            "Vellum.IUiPlatform",
            "Vellum.NullUiPlatform",
            "Vellum.Response",
            "Vellum.ScaledGlyphMetrics",
            "Vellum.TableColumn",
            "Vellum.TextOverflowMode",
            "Vellum.TextWrapMode",
            "Vellum.Theme",
            "Vellum.ThemePresets",
            "Vellum.TrueTypeFont",
            "Vellum.Ui",
            "Vellum.Ui+DisabledScopeHandle",
            "Vellum.Ui+IdScopeHandle",
            "Vellum.Ui+LayoutScopeHandle",
            "Vellum.Ui+TableBuilder",
            "Vellum.Ui+TableRowBuilder",
            "Vellum.UiAlign",
            "Vellum.UiCanvas",
            "Vellum.UiCursor",
            "Vellum.UiFonts",
            "Vellum.UiInputState",
            "Vellum.UiKey",
            "Vellum.UiId",
            "Vellum.UiMouseButton",
            "Vellum.UiWidgetKind",
            "Vellum.WindowState",
            "Vellum.Rendering.ClipRect",
            "Vellum.Rendering.Color",
            "Vellum.Rendering.DrawCommand",
            "Vellum.Rendering.DrawVertex",
            "Vellum.Rendering.IRenderer",
            "Vellum.Rendering.RenderFrameInfo",
            "Vellum.Rendering.RenderList",
            "Vellum.Rendering.RenderTextureIds"
        ];

        string[] actual = typeof(Ui).Assembly.GetExportedTypes()
            .Where(type => type.Namespace is "Vellum" or "Vellum.Rendering" || type.DeclaringType == typeof(Ui))
            .Select(type => type.FullName!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        string[] sortedExpected = expected.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        var missing = sortedExpected.Except(actual, StringComparer.Ordinal).ToArray();
        var unexpected = actual.Except(sortedExpected, StringComparer.Ordinal).ToArray();

        Check("public surface matches the reviewed API boundary", missing.Length == 0 && unexpected.Length == 0,
            missing.Length == 0 && unexpected.Length == 0
                ? null
                : $"missing [{string.Join(", ", missing)}], unexpected [{string.Join(", ", unexpected)}]");

        Check("Painter remains internal", typeof(Painter).IsNotPublic);
        Check("GlyphAtlas remains internal", typeof(GlyphAtlas).IsNotPublic);
        Check("GlyphInfo remains internal", typeof(GlyphInfo).IsNotPublic);

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer);
            ui.Frame(new RenderFrameInfo(320, 180, 640, 360), Vector2.Zero, false, static _ => { });

            Check("render frame info reaches the renderer",
                renderer.LastFrame.LogicalWidth == 320 &&
                renderer.LastFrame.LogicalHeight == 180 &&
                renderer.LastFrame.FramebufferWidth == 640 &&
                renderer.LastFrame.FramebufferHeight == 360 &&
                MathF.Abs(renderer.LastFrame.ScaleX - 2f) < 0.001f &&
                MathF.Abs(renderer.LastFrame.ScaleY - 2f) < 0.001f);

            Check("text raster scale follows framebuffer scale by default",
                MathF.Abs(ui.TextRasterScale - 2f) < 0.001f);

            ui.AutoTextRasterScale = false;
            ui.TextRasterScale = 1.25f;
            ui.Frame(new RenderFrameInfo(320, 180, 960, 540), Vector2.Zero, false, static _ => { });

            Check("manual text raster scale can be preserved",
                MathF.Abs(ui.TextRasterScale - 1.25f) < 0.001f);
        }

        Console.WriteLine($"Public API boundaries: {passed} passed, {failed} failed\n");
    }

    static void RunUiTextInputTests(TrueTypeFont font)
    {
        int passed = 0, failed = 0;

        void Check(string name, bool condition)
        {
            Assert.True(condition, name);
            Console.WriteLine($"  PASS  {name}");
            passed++;
        }

        Console.WriteLine("=== Ui Text Input Tests ===");

        var renderer = new TestRenderer();
        var platform = new TestUiPlatform();
        var ui = new Ui(renderer)
        {
            Font = font,
            DefaultFontSize = 18f,
            Lcd = false,
            Platform = platform
        };

        string fieldText = "";
        Response field = default;

        void Frame(Vector2 mouse, bool mouseDown, UiInputState input = default)
        {
            ui.Frame(800, 600, mouse, mouseDown, input, frame =>
            {
                field = frame.TextField("name", ref fieldText, 160, placeholder: "Name");
            });
        }

        void FocusAtEnd()
        {
            var mouse = new Vector2(170, 24);
            Frame(mouse, true);
            Frame(mouse, false);
        }

        Frame(new Vector2(20, 24), true);
        Frame(new Vector2(20, 24), false);
        Frame(Vector2.Zero, false, Input(text: "Grüße"));
        Check("text field inserts typed text", fieldText == "Grüße");
        Check("text field reports requested width", MathF.Abs(field.W - 160) < 0.1f && field.H > 0);

        fieldText = "a\u0308b";
        FocusAtEnd();
        Frame(Vector2.Zero, false, Input(keys: new[] { UiKey.Backspace }));
        Frame(Vector2.Zero, false, Input(keys: new[] { UiKey.Backspace }));
        Check("backspace deletes whole grapheme clusters", fieldText == string.Empty);

        fieldText = "Beta";
        FocusAtEnd();
        Frame(Vector2.Zero, false, Input(keys: new[] { UiKey.Home }));
        Frame(Vector2.Zero, false, Input(text: "X"));
        Check("home moves caret to the start", fieldText == "XBeta");

        Frame(Vector2.Zero, false, Input(keys: new[] { UiKey.End }));
        Frame(Vector2.Zero, false, Input(text: "Y"));
        Check("end moves caret to the end", fieldText == "XBetaY");

        fieldText = "Gamma";
        FocusAtEnd();
        Frame(Vector2.Zero, false, Input(ctrl: true, keys: new[] { UiKey.A }));
        Frame(Vector2.Zero, false, Input(text: "Hi"));
        Check("ctrl+a selection is replaced by typed text", fieldText == "Hi");

        fieldText = "Delta";
        FocusAtEnd();
        Frame(Vector2.Zero, false, Input(shift: true, keys: new[] { UiKey.Left }));
        Frame(Vector2.Zero, false, Input(text: "X"));
        Check("shift+left selection is replaced by typed text", fieldText == "DeltX");

        fieldText = "Test";
        FocusAtEnd();
        Frame(Vector2.Zero, false, Input(keys: new[] { UiKey.Home }));
        Frame(Vector2.Zero, false, Input(keys: new[] { UiKey.Delete }));
        Check("delete removes the grapheme after the caret", fieldText == "est");

        fieldText = "Copy";
        FocusAtEnd();
        Frame(Vector2.Zero, false, Input(keys: new[] { UiKey.Home }));
        Frame(Vector2.Zero, false, Input(shift: true, keys: new[] { UiKey.Right }));
        Frame(Vector2.Zero, false, Input(shift: true, keys: new[] { UiKey.Right }));
        Frame(Vector2.Zero, false, Input(ctrl: true, keys: new[] { UiKey.C }));
        Check("ctrl+c copies selected text to clipboard", platform.ClipboardText == "Co" && fieldText == "Copy");

        Frame(Vector2.Zero, false, Input(ctrl: true, keys: new[] { UiKey.X }));
        Check("ctrl+x cuts selected text", platform.ClipboardText == "Co" && fieldText == "py");

        platform.ClipboardText = "ZZ";
        FocusAtEnd();
        Frame(Vector2.Zero, false, Input(ctrl: true, keys: new[] { UiKey.V }));
        Check("ctrl+v pastes clipboard text", fieldText == "pyZZ");

        fieldText = "Submit";
        FocusAtEnd();
        Frame(Vector2.Zero, false, Input(keys: new[] { UiKey.Enter }));
        Check("enter submits the text field", field.Submitted && !field.Focused && fieldText == "Submit");

        fieldText = "Base";
        FocusAtEnd();
        Frame(Vector2.Zero, false, Input(text: "X"));
        Frame(Vector2.Zero, false, Input(keys: new[] { UiKey.Escape }));
        Check("escape cancels and restores the edit session text", field.Cancelled && field.Changed && fieldText == "Base");

        Console.WriteLine($"Ui text input: {passed} passed, {failed} failed\n");
    }

    static void RunUiTextAreaTests(TrueTypeFont font)
    {
        int passed = 0, failed = 0;

        void Check(string name, bool condition)
        {
            Assert.True(condition, name);
            Console.WriteLine($"  PASS  {name}");
            passed++;
        }

        Console.WriteLine("=== Ui Text Area Tests ===");

        var renderer = new TestRenderer();
        var platform = new TestUiPlatform();
        var ui = new Ui(renderer)
        {
            Font = font,
            DefaultFontSize = 18f,
            Lcd = false,
            Platform = platform
        };

        string areaText = "";
        Response area = default;

        void Frame(Vector2 mouse, bool mouseDown, UiInputState input = default)
        {
            ui.Frame(800, 600, mouse, mouseDown, input, frame =>
            {
                area = frame.TextArea("notes", ref areaText, 220, 120, placeholder: "Notes");
            });
        }

        void FocusAtEnd()
        {
            var mouse = new Vector2(220, 110);
            Frame(mouse, true);
            Frame(mouse, false);
        }

        Frame(new Vector2(20, 24), true);
        Frame(new Vector2(20, 24), false);
        Frame(Vector2.Zero, false, Input(text: "Alpha"));
        Frame(Vector2.Zero, false, Input(keys: new[] { UiKey.Enter }));
        Frame(Vector2.Zero, false, Input(text: "Beta"));
        Check("enter inserts a newline in the text area", areaText == "Alpha\nBeta" && !area.Submitted);

        areaText = "aaaaa\naaaaa";
        FocusAtEnd();
        Frame(Vector2.Zero, false, Input(keys: new[] { UiKey.Up }));
        Frame(Vector2.Zero, false, Input(text: "X"));
        Check("up moves the caret to the previous line", areaText == "aaaaaX\naaaaa");

        Frame(Vector2.Zero, false, Input(ctrl: true, keys: new[] { UiKey.Home }));
        Frame(Vector2.Zero, false, Input(keys: new[] { UiKey.Down }));
        Frame(Vector2.Zero, false, Input(text: "Y"));
        Check("down moves the caret to the next line", areaText == "aaaaaX\nYaaaaa");

        areaText = "One\nTwo";
        FocusAtEnd();
        Frame(Vector2.Zero, false, Input(ctrl: true, keys: new[] { UiKey.Home }));
        Frame(Vector2.Zero, false, Input(keys: new[] { UiKey.Down }));
        Frame(Vector2.Zero, false, Input(keys: new[] { UiKey.Home }));
        Frame(Vector2.Zero, false, Input(keys: new[] { UiKey.Backspace }));
        Check("backspace at line start removes the preceding newline", areaText == "OneTwo");

        areaText = "Submit";
        FocusAtEnd();
        Frame(Vector2.Zero, false, Input(ctrl: true, keys: new[] { UiKey.Enter }));
        Check("ctrl+enter submits the text area", area.Submitted && !area.Focused && areaText == "Submit");

        areaText = "Base";
        FocusAtEnd();
        Frame(Vector2.Zero, false, Input(keys: new[] { UiKey.Enter }));
        Frame(Vector2.Zero, false, Input(text: "More"));
        Frame(Vector2.Zero, false, Input(keys: new[] { UiKey.Escape }));
        Check("escape cancels and restores multiline edits", area.Cancelled && area.Changed && areaText == "Base");

        Console.WriteLine($"Ui text area: {passed} passed, {failed} failed\n");
    }

    static void RunUiLayoutTests(TrueTypeFont font)
    {
        int passed = 0, failed = 0;

        void Check(string name, bool condition)
        {
            Assert.True(condition, name);
            Console.WriteLine($"  PASS  {name}");
            passed++;
        }

        Console.WriteLine("=== Ui Layout Tests ===");

        var renderer = new TestRenderer();
        var ui = new Ui(renderer)
        {
            Font = font,
            DefaultFontSize = 18f,
            Lcd = false
        };

        string rootText = "";
        float rootAvailable = 0;
        Response rootField = default;

        ui.Frame(400, 300, Vector2.Zero, false, frame =>
        {
            rootAvailable = frame.AvailableWidth;
            rootField = frame.TextField("root", ref rootText, frame.AvailableWidth);
        });
        Check("root available width respects outer padding", MathF.Abs(rootAvailable - 368) < 0.1f);
        Check("fill-width text field uses the full available width", MathF.Abs(rootField.W - rootAvailable) < 0.1f);

        string rowText = "";
        float rowRemaining = 0;
        Response rowField = default;

        ui.Frame(400, 300, Vector2.Zero, false, frame =>
        {
            frame.Row(frame =>
            {
                frame.Label("Name:");
                rowRemaining = frame.AvailableWidth;
                rowField = frame.TextField("row", ref rowText, frame.AvailableWidth);
            });
        });
        Check("horizontal layout exposes the remaining width", rowRemaining > 0 && MathF.Abs(rowField.W - rowRemaining) < 0.1f);
        Check("horizontal fill-width stays inside the parent bounds", rowField.X + rowField.W <= 400 - ui.RootPadding + 0.1f);

        string centeredText = "";
        Response centeredField = default;

        ui.Frame(400, 300, Vector2.Zero, false, frame =>
        {
            frame.MaxWidth(180, frame =>
            {
                centeredField = frame.TextField("centered", ref centeredText, frame.AvailableWidth);
            }, align: UiAlign.Center);
        });
        float expectedCenterX = ui.RootPadding + (368 - 180) * 0.5f;
        Check("center-aligned max-width group positions content correctly", MathF.Abs(centeredField.X - expectedCenterX) < 0.1f && MathF.Abs(centeredField.W - 180) < 0.1f);

        Response rightButton = default;
        ui.Frame(400, 300, Vector2.Zero, false, frame =>
        {
            frame.FixedWidth(140, frame =>
            {
                rightButton = frame.Button("Right", width: frame.AvailableWidth);
            }, align: UiAlign.End);
        });
        float expectedRightX = 400 - ui.RootPadding - 140;
        Check("right-aligned width group positions content at the end", MathF.Abs(rightButton.X - expectedRightX) < 0.1f && MathF.Abs(rightButton.W - 140) < 0.1f);

        string clampedText = "";
        Response clampedField = default;
        ui.Frame(400, 300, Vector2.Zero, false, frame =>
        {
            frame.MaxWidth(600, frame =>
            {
                clampedField = frame.TextField("clamped", ref clampedText, frame.AvailableWidth);
            });
        });
        Check("max-width clamps to the available width", MathF.Abs(clampedField.W - rootAvailable) < 0.1f);

        float splitterPaneWidth = 120f;
        Response splitter = default, splitterRight = default;

        void SplitterFrame(Vector2 mouse, bool mouseDown, UiInputState input = default)
        {
            ui.Frame(400, 300, mouse, mouseDown, input, frame =>
            {
                using (frame.Row())
                {
                    using (frame.FixedWidth(splitterPaneWidth))
                        frame.Label("Left");

                    splitter = frame.Splitter("layout-splitter", ref splitterPaneWidth, 80f, 180f, thickness: 8f, height: 44f);

                    using (frame.FixedWidth(frame.AvailableWidth))
                        splitterRight = frame.Button("Right", width: frame.AvailableWidth);
                }
            });
        }

        SplitterFrame(Vector2.Zero, false);
        Vector2 splitterMouse = new(splitter.X + splitter.W * 0.5f, splitter.Y + splitter.H * 0.5f);
        SplitterFrame(splitterMouse, false);
        SplitterFrame(splitterMouse, true);
        SplitterFrame(splitterMouse + new Vector2(24f, 0f), true);
        Check("splitter drag updates caller-owned pane size", splitter.Changed && MathF.Abs(splitterPaneWidth - 144f) < 0.1f);

        SplitterFrame(splitterMouse + new Vector2(24f, 0f), false);
        SplitterFrame(Vector2.Zero, false, Input(keys: new[] { UiKey.Right }));
        Check("focused splitter supports keyboard resizing", splitter.Changed && MathF.Abs(splitterPaneWidth - 152f) < 0.1f);
        Check("splitter participates in horizontal layout", splitterRight.X > splitter.X + splitter.W);

        Console.WriteLine($"Ui layout: {passed} passed, {failed} failed\n");
    }

    static void RunUiCanvasTests(TrueTypeFont font)
    {
        int passed = 0, failed = 0;

        void Check(string name, bool condition)
        {
            Assert.True(condition, name);
            Console.WriteLine($"  PASS  {name}");
            passed++;
        }

        Console.WriteLine("=== Ui Canvas Tests ===");

        var renderer = new TestRenderer();
        var ui = new Ui(renderer)
        {
            Font = font,
            DefaultFontSize = 18f,
            Lcd = false
        };

        Response canvasResponse = default;
        Vector2 measured = Vector2.Zero;
        bool hovered = false;
        bool hitInside = false;
        bool hitOutside = false;

        ui.Frame(420, 260, new Vector2(60, 48), false, frame =>
        {
            frame.Spacing(10);
            canvasResponse = frame.Canvas(180, 80, canvas =>
            {
                measured = canvas.MeasureText("Canvas");
                canvas.DrawRect(0, 0, canvas.Width, canvas.Height, frame.Theme.PanelBg, frame.Theme.ButtonBorder, frame.Theme.BorderWidth, frame.Theme.BorderRadius);
                canvas.FillRect(10, 12, 60, 18, frame.Theme.Accent, radius: 6);
                canvas.DrawText("Canvas", 12, 38, color: frame.Theme.TextPrimary);
                hovered = canvas.Hovered;
                hitInside = canvas.HitTest(0, 0, 60, 30);
                hitOutside = canvas.HitTest(200, 0, 40, 20);
            });
        });

        Check("canvas reports reserved size", MathF.Abs(canvasResponse.W - 180) < 0.1f && MathF.Abs(canvasResponse.H - 80) < 0.1f);
        Check("canvas hover uses local hit-testing", canvasResponse.Hovered && hovered && hitInside && !hitOutside);
        Check("canvas can measure text", measured.X > 0 && measured.Y > 0);
        Check("canvas drawing reaches the render list", HasVertexColor(renderer.LastRenderList, ui.Theme.Accent) && HasVertexColor(renderer.LastRenderList, ui.Theme.TextPrimary));
        Check(
            "canvas drawing is clipped to its region",
            renderer.LastRenderList != null &&
            renderer.LastRenderList.Commands.Any(cmd =>
                cmd.HasClip &&
                MathF.Abs(cmd.ClipRect.X - canvasResponse.X) < 0.1f &&
                MathF.Abs(cmd.ClipRect.Y - canvasResponse.Y) < 0.1f &&
                MathF.Abs(cmd.ClipRect.Width - canvasResponse.W) < 0.1f &&
                MathF.Abs(cmd.ClipRect.Height - canvasResponse.H) < 0.1f));

        Console.WriteLine($"Ui canvas: {passed} passed, {failed} failed\n");
    }

    static void RunUiPlatformTests(TrueTypeFont font)
    {
        int passed = 0, failed = 0;

        void Check(string name, bool condition)
        {
            Assert.True(condition, name);
            Console.WriteLine($"  PASS  {name}");
            passed++;
        }

        Console.WriteLine("=== Ui Platform Tests ===");

        var renderer = new TestRenderer();
        var platform = new TestUiPlatform();
        var ui = new Ui(renderer)
        {
            Font = font,
            DefaultFontSize = 18f,
            Lcd = false,
            Platform = platform
        };

        ui.Frame(800, 600, new Vector2(20, 20), false, frame =>
        {
            frame.Button("Hover");
        });
        Check("button hover requests pointing hand cursor", platform.LastCursor == UiCursor.PointingHand);

        string text = "";
        ui.Frame(800, 600, new Vector2(20, 24), false, frame =>
        {
            frame.TextField("field", ref text, 160);
        });
        Check("text field hover requests ibeam cursor", platform.LastCursor == UiCursor.IBeam);

        float wheelY = 0;
        ui.Frame(800, 600, Vector2.Zero, false, Input(wheel: new Vector2(0, 2)), frame =>
        {
            wheelY = frame.WheelDelta.Y;
            frame.Label("Wheel");
        });
        Check("wheel delta is exposed through ui", MathF.Abs(wheelY - 2) < 0.001f);

        Console.WriteLine($"Ui platform: {passed} passed, {failed} failed\n");
    }

    static void RunUiMouseTests(TrueTypeFont font)
    {
        int passed = 0, failed = 0;

        void Check(string name, bool condition)
        {
            Assert.True(condition, name);
            Console.WriteLine($"  PASS  {name}");
            passed++;
        }

        Console.WriteLine("=== Ui Mouse Tests ===");

        var renderer = new TestRenderer();
        var ui = new Ui(renderer)
        {
            Font = font,
            DefaultFontSize = 18f,
            Lcd = false
        };

        bool leftPressed = false;
        bool leftReleased = false;
        bool rightPressed = false;
        bool rightDown = false;
        bool doubleClicked = false;
        bool dragging = false;
        bool hasDragStart = false;
        Vector2 mouseDelta = default;
        Vector2 dragStart = default;
        Vector2 dragDelta = default;

        void Frame(Vector2 mouse, UiInputState input)
        {
            ui.Frame(400, 300, mouse, input, frame =>
            {
                leftPressed = frame.IsMousePressed(UiMouseButton.Left);
                leftReleased = frame.IsMouseReleased(UiMouseButton.Left);
                rightPressed = frame.IsMousePressed(UiMouseButton.Right);
                rightDown = frame.IsMouseDown(UiMouseButton.Right);
                doubleClicked = frame.IsMouseDoubleClicked(UiMouseButton.Left);
                mouseDelta = frame.MouseDelta;
                hasDragStart = frame.TryGetDragStart(UiMouseButton.Left, out dragStart);
                dragDelta = frame.GetDragDelta(UiMouseButton.Left);
                dragging = frame.IsDragging(UiMouseButton.Left);
                frame.Label("Mouse");
            });
        }

        Frame(new Vector2(10, 10), Input(mouseButtons: new[] { UiMouseButton.Left }, timeSeconds: 1.0));
        Check("left mouse press is reported", leftPressed && !leftReleased);

        Frame(new Vector2(18, 20), Input(mouseButtons: new[] { UiMouseButton.Left }, timeSeconds: 1.02));
        Check("mouse delta is exposed", MathF.Abs(mouseDelta.X - 8) < 0.001f && MathF.Abs(mouseDelta.Y - 10) < 0.001f);
        Check("drag baseline tracks the press origin", hasDragStart &&
            MathF.Abs(dragStart.X - 10) < 0.001f &&
            MathF.Abs(dragStart.Y - 10) < 0.001f &&
            MathF.Abs(dragDelta.X - 8) < 0.001f &&
            MathF.Abs(dragDelta.Y - 10) < 0.001f &&
            dragging);

        Frame(new Vector2(18, 20), Input(timeSeconds: 1.05));
        Check("left mouse release is reported", leftReleased && !leftPressed);

        Frame(new Vector2(30, 30), Input(mouseButtons: new[] { UiMouseButton.Right }, timeSeconds: 1.10));
        Check("right mouse press is exposed", rightPressed && rightDown);

        Frame(new Vector2(30, 30), Input(timeSeconds: 1.15));
        Frame(new Vector2(10, 10), Input(mouseButtons: new[] { UiMouseButton.Left }, timeSeconds: 1.20));
        Check("double click is reported on the second press", doubleClicked);

        Console.WriteLine($"Ui mouse: {passed} passed, {failed} failed\n");
    }

    static void RunUiFocusTests(TrueTypeFont font)
    {
        int passed = 0, failed = 0;

        void Check(string name, bool condition)
        {
            Assert.True(condition, name);
            Console.WriteLine($"  PASS  {name}");
            passed++;
        }

        Console.WriteLine("=== Ui Focus Tests ===");

        var renderer = new TestRenderer();
        var ui = new Ui(renderer)
        {
            Font = font,
            DefaultFontSize = 18f,
            Lcd = false
        };

        string firstText = "First";
        string disabledText = "Disabled";
        string secondText = "Second";
        Response first = default, disabled = default, button = default, second = default;

        void Frame(UiInputState input = default)
        {
            ui.Frame(800, 600, Vector2.Zero, false, input, frame =>
            {
                first = frame.TextField("first", ref firstText, 140);
                disabled = frame.TextField("disabled", ref disabledText, 140, enabled: false);
                button = frame.Button("Go");
                second = frame.TextField("second", ref secondText, 140);
            });
        }

        Frame(Input(keys: new[] { UiKey.Tab }));
        Frame();
        Check("tab focuses the first focusable widget", first.Focused && !button.Focused && !second.Focused);

        Frame(Input(keys: new[] { UiKey.Tab }));
        Frame();
        Check("tab skips disabled controls", button.Focused && !disabled.Focused);

        Frame(Input(keys: new[] { UiKey.Tab }));
        Frame();
        Check("tab advances to the next focusable control", second.Focused);

        Frame(Input(shift: true, keys: new[] { UiKey.Tab }));
        Frame();
        Check("shift+tab moves focus backward", button.Focused && !second.Focused);

        Frame(Input(keys: new[] { UiKey.Space }));
        Check("space activates a focused button", button.Clicked);
        Check("disabled text field reports disabled state", disabled.Disabled && !disabled.Hovered && !disabled.Focused);

        Console.WriteLine($"Ui focus: {passed} passed, {failed} failed\n");
    }

    static void RunUiSemanticsTests(TrueTypeFont font)
    {
        int passed = 0, failed = 0;

        void Check(string name, bool condition)
        {
            Assert.True(condition, name);
            Console.WriteLine($"  PASS  {name}");
            passed++;
        }

        Console.WriteLine("=== Ui Semantics Tests ===");

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            string firstText = "First";
            string secondText = "Second";
            Response first = default, second = default;

            ui.Frame(800, 600, Vector2.Zero, false, frame =>
            {
                frame.RequestFocus(UiWidgetKind.TextField, "second");
                first = frame.TextField("first", ref firstText, 140);
                second = frame.TextField("second", ref secondText, 140);
            });

            Check("request focus moves focus to a later widget in the same frame", !first.Focused && second.Focused);

            ui.ClearFocus();
            Response late = default;
            ui.Frame(800, 600, Vector2.Zero, false, frame =>
            {
                frame.RequestFocus(UiWidgetKind.TextField, "late");
                frame.Label("Waiting");
            });
            ui.Frame(800, 600, Vector2.Zero, false, frame =>
            {
                late = frame.TextField("late", ref secondText, 140);
            });

            Check("request focus persists until the widget appears", late.Focused);
        }

        {
            var renderer = new TestRenderer();
            var platform = new TestUiPlatform();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false,
                Platform = platform
            };

            string lockedText = "Locked";
            Response readOnlyField = default;

            void ReadOnlyFrame(Vector2 mouse, bool mouseDown, UiInputState input = default)
            {
                ui.Frame(800, 600, mouse, mouseDown, input, frame =>
                {
                    readOnlyField = frame.TextField("locked", ref lockedText, 180, readOnly: true);
                });
            }

            var mouse = new Vector2(170, 24);
            ReadOnlyFrame(mouse, true);
            ReadOnlyFrame(mouse, false);
            ReadOnlyFrame(Vector2.Zero, false, Input(text: "X"));
            Check("read-only text field can focus but ignores typed edits", readOnlyField.Focused && readOnlyField.ReadOnly && lockedText == "Locked");

            ReadOnlyFrame(Vector2.Zero, false, Input(ctrl: true, keys: new[] { UiKey.A }));
            ReadOnlyFrame(Vector2.Zero, false, Input(ctrl: true, keys: new[] { UiKey.C }));
            Check("read-only text field still supports selection and copy", platform.ClipboardText == "Locked");
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            Response button = default;
            ui.Frame(800, 600, Vector2.Zero, false, Input(keys: new[] { UiKey.Space }), frame =>
            {
                frame.RequestFocus(UiWidgetKind.Button, "Go");
                button = frame.Button("Go");
            });

            Check("response activated covers keyboard button activation", button.Activated && button.Clicked);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            Response first = default, second = default;
            ui.Frame(800, 600, Vector2.Zero, false, Input(keys: new[] { UiKey.Space }), frame =>
            {
                using (frame.Id(1))
                {
                    frame.RequestFocus(UiWidgetKind.Button, "Delete");
                    first = frame.Button("Delete");
                }

                using (frame.Id(2))
                {
                    second = frame.Button("Delete");
                }
            });

            Check("id scopes disambiguate same-label widgets", first.Activated && !second.Activated);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            Response disabled = default, enabled = default, conditionallyEnabled = default;
            ui.Frame(800, 600, Vector2.Zero, false, frame =>
            {
                using (frame.Disabled())
                {
                    disabled = frame.Button("Disabled");
                }

                enabled = frame.Button("Enabled");

                using (frame.Disabled(false))
                {
                    conditionallyEnabled = frame.Button("Conditionally enabled");
                }
            });

            Check("disabled scope handles restore enabled state",
                disabled.Disabled && !enabled.Disabled && !conditionallyEnabled.Disabled);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            Response primary = default, secondary = default;
            ui.Frame(800, 600, Vector2.Zero, false, Input(keys: new[] { UiKey.Space }), frame =>
            {
                frame.RequestFocus(UiWidgetKind.Button, "save-secondary");
                primary = frame.Button("Save", id: "save-primary");
                secondary = frame.Button("Save", id: "save-secondary");
            });

            Check("explicit widget ids disambiguate same-label widgets", !primary.Activated && secondary.Activated);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            string? inheritedId = null;
            Response first = default, second = default;
            ui.Frame(800, 600, Vector2.Zero, false, Input(keys: new[] { UiKey.Space }), frame =>
            {
                frame.RequestFocus(UiWidgetKind.Button, "Alpha");
                first = frame.Button("Alpha", id: inheritedId);
                second = frame.Button("Beta", id: inheritedId);
            });

            Check("null string widget ids fall back to label identity", first.Activated && !second.Activated);
        }

#if DEBUG
        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer) { Font = font, DefaultFontSize = 18f, Lcd = false };

            bool layoutLeak = false;
            try
            {
                ui.Frame(800, 600, Vector2.Zero, false, frame => { frame.Row(); });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("scope leak"))
            {
                layoutLeak = true;
            }
            Check("missing using on Row() is detected as a scope leak", layoutLeak);

            bool idLeak = false;
            try
            {
                ui.Frame(800, 600, Vector2.Zero, false, frame => { frame.Id("orphan"); });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("scope leak"))
            {
                idLeak = true;
            }
            Check("missing using on Id() is detected as a scope leak", idLeak);

            bool disabledLeak = false;
            try
            {
                ui.Frame(800, 600, Vector2.Zero, false, frame => { frame.Disabled(); });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("scope leak"))
            {
                disabledLeak = true;
            }
            Check("missing using on Disabled() is detected as a scope leak", disabledLeak);
        }
#endif

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            string text = "Ada";
            Response button = default, field = default;
            ui.Frame(800, 600, Vector2.Zero, false, frame =>
            {
                button = frame.Button("Name");
                field = frame.TextField("Name", ref text, 180);
            });

            Check("widget kind disambiguates same-label widgets", button.W > 0f && field.W > 0f);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            ui.OpenPopup("Settings");
            Response button = default;
            bool popupRendered = false;
            ui.Frame(800, 600, Vector2.Zero, false, frame =>
            {
                button = frame.Button("Settings");
                popupRendered = frame.Popup("Settings", button, 160, 120, popup => popup.Label("Popup content"));
            });

            Check("popup ids do not collide with same-label widgets", button.W > 0f && popupRendered);
        }

#if DEBUG
        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                ui.Frame(800, 600, Vector2.Zero, false, frame =>
                {
                    frame.Button("Duplicate");
                    frame.Button("Duplicate");
                });
            });

            Check("duplicate widget ids throw in debug builds",
                ex.Message.Contains("Duplicate Vellum widget id", StringComparison.Ordinal));
        }
#endif

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            bool open = false;
            Response header = default;
            float closedWidth = 0;
            float openedWidth = 0;

            void HeaderFrame(Vector2 mouse, bool mouseDown, UiInputState input = default, bool fixedWidth = true)
            {
                ui.Frame(800, 600, mouse, mouseDown, input, frame =>
                {
                    header = fixedWidth
                        ? frame.CollapsingHeader("Section", ref open, width: 180)
                        : frame.CollapsingHeader("Section", ref open);
                });
            }

            HeaderFrame(Vector2.Zero, false, fixedWidth: false);
            closedWidth = header.W;

            var mouse = new Vector2(20, 24);
            HeaderFrame(mouse, true);
            HeaderFrame(mouse, false);
            Check("collapsing header reports open transitions", open && header.Toggled && header.Opened && !header.Closed && header.OpenChanged && header.Changed);

            HeaderFrame(mouse, true);
            HeaderFrame(mouse, false);
            Check("collapsing header reports close transitions", !open && header.Toggled && !header.Opened && header.Closed && header.OpenChanged && header.Changed);

            HeaderFrame(mouse, true, fixedWidth: false);
            HeaderFrame(mouse, false, fixedWidth: false);
            openedWidth = header.W;
            Check("collapsing header keeps a stable intrinsic width across states", MathF.Abs(closedWidth - openedWidth) < 0.01f);
        }

        Console.WriteLine($"Ui semantics: {passed} passed, {failed} failed\n");
    }

    static void RunUiWidgetTests(TrueTypeFont font)
    {
        int passed = 0, failed = 0;

        void Check(string name, bool condition)
        {
            Assert.True(condition, name);
            Console.WriteLine($"  PASS  {name}");
            passed++;
        }

        Console.WriteLine("=== Ui Widget Tests ===");

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            bool isEnabled = false;
            Response checkbox = default;

            void Frame(Vector2 mouse, bool mouseDown, UiInputState input = default, bool enabled = true)
            {
                ui.Frame(320, 180, mouse, mouseDown, input, frame =>
                {
                    checkbox = frame.Checkbox("Enable feature", ref isEnabled, width: 180, enabled: enabled);
                });
            }

            Frame(new Vector2(20, 24), true);
            Frame(new Vector2(20, 24), false);
            Check("checkbox toggles on click", isEnabled && checkbox.Changed && checkbox.Toggled && checkbox.W >= 180f);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            bool isChecked = false;
            Response checkbox = default;

            ui.Frame(320, 180, Vector2.Zero, false, Input(keys: new[] { UiKey.Tab }), frame =>
            {
                checkbox = frame.Checkbox("Keyboard toggle", ref isChecked, width: 180);
            });
            ui.Frame(320, 180, Vector2.Zero, false, Input(keys: new[] { UiKey.Space }), frame =>
            {
                checkbox = frame.Checkbox("Keyboard toggle", ref isChecked, width: 180);
            });

            Check("space toggles a focused checkbox", isChecked && checkbox.Changed && checkbox.Focused);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            bool isChecked = false;
            Response checkbox = default;

            ui.Frame(320, 180, new Vector2(20, 24), true, frame =>
            {
                checkbox = frame.Checkbox("Disabled", ref isChecked, width: 180, enabled: false);
            });
            ui.Frame(320, 180, new Vector2(20, 24), false, frame =>
            {
                checkbox = frame.Checkbox("Disabled", ref isChecked, width: 180, enabled: false);
            });

            Check("disabled checkbox does not toggle", !isChecked && !checkbox.Changed && checkbox.Disabled);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            bool selected = true;
            Response selectable = default;

            ui.Frame(320, 180, Vector2.Zero, false, frame =>
            {
                selectable = frame.Selectable("System", selected, width: 180);
            });

            bool selectableVisuals =
                selectable.W >= 180f &&
                renderer.LastRenderList != null &&
                HasVertexColor(renderer.LastRenderList, ui.Theme.SelectableBgSelected) &&
                HasVertexColor(renderer.LastRenderList, ui.Theme.SelectableIndicator);

            Check("selected selectable uses highlighted visuals", selectableVisuals);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            int textureId = renderer.CreateTexture(new byte[] { 255, 255, 255, 255 }, 1, 1);
            Response image = default;

            ui.Frame(320, 180, Vector2.Zero, false, frame =>
            {
                image = frame.Image(textureId, 64, 40, tint: ui.Theme.Accent);
            });

            bool submittedTexture =
                image.W > 0 &&
                image.H > 0 &&
                renderer.LastRenderList != null &&
                renderer.LastRenderList.Commands.Any(cmd => cmd.TextureId == textureId);

            Check("image widget emits textured geometry", submittedTexture);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            int selected = 0;
            Response radio = default;

            void Frame(Vector2 mouse, bool mouseDown)
            {
                ui.Frame(320, 180, mouse, mouseDown, frame =>
                {
                    radio = frame.RadioValue("Warm", ref selected, 1, width: 180);
                });
            }

            Frame(new Vector2(20, 24), true);
            Frame(new Vector2(20, 24), false);
            Check("radio value selects a new option on click", selected == 1 && radio.Changed && radio.Toggled);

            Frame(new Vector2(20, 24), true);
            Frame(new Vector2(20, 24), false);
            Check("radio value does not report changed when re-clicking the selected option", selected == 1 && !radio.Changed);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            bool clicked = false;
            Response menu = default;

            ui.Frame(320, 180, new Vector2(20, 24), true, frame =>
            {
                menu = frame.MenuItem("Open recent", selected: false, width: 180);
            });
            ui.Frame(320, 180, new Vector2(20, 24), false, frame =>
            {
                menu = frame.MenuItem("Open recent", selected: false, width: 180);
                clicked = menu.Clicked;
            });

            Check("menu item clicks like a selectable row", clicked && menu.W >= 180f);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            bool clicked = false;
            Response menu = default;
            Response customLabel = default;

            ui.Frame(320, 180, new Vector2(20, 24), true, frame =>
            {
                menu = frame.MenuItem("Custom row", (item, text) =>
                {
                    customLabel = item.Label(text);
                }, id: "custom-menu-item", width: 180);
            });
            ui.Frame(320, 180, new Vector2(20, 24), false, frame =>
            {
                menu = frame.MenuItem("Custom row", (item, text) =>
                {
                    customLabel = item.Label(text);
                }, id: "custom-menu-item", width: 180);
                clicked = menu.Clicked;
            });

            Check("custom menu item uses named id and renders content", clicked && customLabel.W > 0f && menu.W >= 180f);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            var ex = Assert.Throws<ArgumentException>(() =>
            {
                ui.Frame(320, 180, Vector2.Zero, false, frame =>
                {
                    frame.MenuItem("Custom row", static (item, text) => item.Label(text));
                });
            });

            Check("custom menu item requires a named id", ex.ParamName == "id");
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            Response longItem = default;
            void Frame(Vector2 mouse)
            {
                ui.Frame(480, 240, mouse, false, frame =>
                {
                    frame.Menu("Select Demo Scene", popup =>
                    {
                        popup.MenuItem("Demo 00 - Basic shapes");
                        longItem = popup.MenuItem("Demo 01 - A much longer menu entry that should widen the popup");
                    }, width: 140f, openOnHover: true, openToSide: true);
                });
            }

            Frame(new Vector2(20, 24));
            bool hasBounds = ui.TryGetChildPopupBounds(UiWidgetKind.Menu, "Select Demo Scene", "menu", out _, out _, out float popupW, out _);
            Check("side-opening menu auto-sizes to long content", hasBounds && longItem.W > 180f && popupW > 180f);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            string[] options = ["System", "Warm", "Cool"];
            int selected = 2;
            Response combo = default;

            void Frame(Vector2 mouse, bool mouseDown, UiInputState input = default)
            {
                ui.Frame(360, 220, mouse, mouseDown, input, frame =>
                {
                    combo = frame.ComboBox("theme", options, ref selected, 180);
                });
            }

            Frame(new Vector2(20, 24), true);
            Frame(new Vector2(20, 24), false);
            Check("combo box opens on click", combo.Focused && ui.IsChildPopupOpen(UiWidgetKind.ComboBox, "theme", "popup"));

            bool popupBoundsKnown = ui.TryGetChildPopupBounds(UiWidgetKind.ComboBox, "theme", "popup", out float px, out float py, out _, out _);
            Frame(new Vector2(px + 12, py + 56), true);
            Frame(new Vector2(px + 12, py + 56), false);
            Frame(Vector2.Zero, false);
            Check("combo box selects an item and closes", popupBoundsKnown && selected == 0 && !ui.IsChildPopupOpen(UiWidgetKind.ComboBox, "theme", "popup") && combo.Changed);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            string[] options = ["Same", "Other", "Same"];
            int selected = 1;
            Response combo = default;

            void Frame(Vector2 mouse, bool mouseDown)
            {
                ui.Frame(360, 220, mouse, mouseDown, frame =>
                {
                    combo = frame.ComboBox("duplicates", options, ref selected, 180);
                });
            }

            Frame(new Vector2(20, 24), true);
            Frame(new Vector2(20, 24), false);
            bool popupBoundsKnown = ui.TryGetChildPopupBounds(UiWidgetKind.ComboBox, "duplicates", "popup", out _, out _, out _, out _);

            Check("combo box duplicate labels keep distinct row ids",
                popupBoundsKnown && selected == 1 && ui.IsChildPopupOpen(UiWidgetKind.ComboBox, "duplicates", "popup") && combo.Focused);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            string[] options = ["System", "Warm", "Cool", "Mono"];
            int selected = 1;
            Response combo = default;

            void Frame(UiInputState input = default)
            {
                ui.Frame(360, 220, Vector2.Zero, false, input, frame =>
                {
                    combo = frame.ComboBox("theme/keyboard", options, ref selected, 180);
                });
            }

            Frame(Input(keys: new[] { UiKey.Tab }));
            Frame(Input(keys: new[] { UiKey.Enter }));
            Check("combo box opens from keyboard focus", ui.IsChildPopupOpen(UiWidgetKind.ComboBox, "theme/keyboard", "popup") && combo.Focused);

            Frame(Input(keys: new[] { UiKey.Down }));
            Check("combo box keeps the committed value while only moving the highlight", selected == 1 && ui.IsChildPopupOpen(UiWidgetKind.ComboBox, "theme/keyboard", "popup") && !combo.Changed);

            Frame(Input(keys: new[] { UiKey.Enter }));
            Check("combo box commits the highlighted item on enter", selected == 2 && !ui.IsChildPopupOpen(UiWidgetKind.ComboBox, "theme/keyboard", "popup") && combo.Changed && combo.Focused);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            string[] options = ["System", "Warm", "Cool", "Mono"];
            int selected = 1;
            Response combo = default;

            void Frame(UiInputState input = default)
            {
                ui.Frame(360, 220, Vector2.Zero, false, input, frame =>
                {
                    combo = frame.ComboBox("theme/cancel", options, ref selected, 180);
                });
            }

            Frame(Input(keys: new[] { UiKey.Tab }));
            Frame(Input(keys: new[] { UiKey.Enter }));
            Frame(Input(keys: new[] { UiKey.End }));
            Frame(Input(keys: new[] { UiKey.Escape }));
            Check("combo box escape closes without applying the highlighted item", selected == 1 && !ui.IsChildPopupOpen(UiWidgetKind.ComboBox, "theme/cancel", "popup") && !combo.Changed && combo.Focused);

            Frame(Input(keys: new[] { UiKey.Enter }));
            Frame(Input(keys: new[] { UiKey.Home }));
            Frame(Input(keys: new[] { UiKey.Enter }));
            Check("combo box home jumps to the first item before commit", selected == 0 && !ui.IsChildPopupOpen(UiWidgetKind.ComboBox, "theme/cancel", "popup") && combo.Changed);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            Response panel = default;
            Response button = default;

            ui.Frame(320, 180, new Vector2(40, 40), false, frame =>
            {
                panel = frame.Panel(200, 96, content =>
                {
                    button = content.Button("Inside", width: content.AvailableWidth);
                }, id: "settings");
            });

            Check(
                "panel reserves a framed content region",
                MathF.Abs(panel.W - 200f) < 0.1f &&
                MathF.Abs(panel.H - 96f) < 0.1f &&
                button.X > panel.X &&
                button.Y > panel.Y &&
                button.X + button.W <= panel.X + panel.W + 0.1f);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            Response panel = default;
            Response first = default;
            Response second = default;

            ui.Frame(320, 220, Vector2.Zero, false, frame =>
            {
                panel = frame.Panel(200, content =>
                {
                    first = content.Button("First", width: content.AvailableWidth);
                    second = content.Button("Second", width: content.AvailableWidth);
                });
            });

            float minimumHeight = first.H + second.H + ui.Theme.Gap + ui.Theme.PanelPadding.Vertical + ui.Theme.BorderWidth * 2f;
            Check(
                "auto panel grows to fit its content",
                MathF.Abs(panel.W - 200f) < 0.1f &&
                panel.H >= minimumHeight - 0.1f &&
                second.Y + second.H <= panel.Y + panel.H + 0.1f);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            TableColumn[] columns =
            [
                new("Metric"),
                new("Value", 72f, UiAlign.End),
                new("Live", 64f, UiAlign.Center)
            ];
            Response table = default;
            Response labelCell = default;
            Response valueCell = default;
            Response nestedCell = default;
            Response nestedLabel = default;

            ui.Frame(420, 220, Vector2.Zero, false, frame =>
            {
                table = frame.Table("metrics-table", columns, rows =>
                {
                    rows.Row(row =>
                    {
                        labelCell = row.Cell("Bodies");
                        valueCell = row.Cell("128");
                        row.Cell("yes");
                    });

                    rows.Row(row =>
                    {
                        row.Cell("Frame time");
                        row.Cell("1.82 ms");
                        nestedCell = row.Cell(cell =>
                        {
                            nestedLabel = cell.Label("ok", width: cell.AvailableWidth, align: UiAlign.Center);
                        });
                    });
                }, width: 300f);
            });

            float stretchWidth = 300f - ui.Theme.BorderWidth * 2f - 72f - 64f;
            bool tableLayout =
                MathF.Abs(table.W - 300f) < 0.1f &&
                MathF.Abs(labelCell.W - stretchWidth) < 0.1f &&
                MathF.Abs(valueCell.W - 72f) < 0.1f &&
                valueCell.X > labelCell.X + labelCell.W - 0.1f &&
                MathF.Abs(nestedCell.W - 64f) < 0.1f &&
                nestedLabel.W > 0f;
            bool tableVisuals =
                renderer.LastRenderList != null &&
                HasVertexColor(renderer.LastRenderList, ui.Theme.ButtonBg) &&
                HasVertexColor(renderer.LastRenderList, ui.Theme.SelectableBg) &&
                HasVertexColor(renderer.LastRenderList, ui.Theme.Separator) &&
                HasVertexColor(renderer.LastRenderList, ui.Theme.PanelBorder);

            Check("table lays out stretch and fixed columns", tableLayout && tableVisuals);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            var primaryState = new WindowState(new Vector2(16, 16));
            var secondaryState = new WindowState(new Vector2(48, 48));
            Response primary = default;
            Response secondary = default;

            ui.Frame(360, 260, Vector2.Zero, false, frame =>
            {
                primary = frame.Window("Inspector", primaryState, 160, content =>
                {
                    content.Button("Inside", width: content.AvailableWidth);
                }, id: "primary");

                secondary = frame.Window("Inspector", secondaryState, 160, content =>
                {
                    content.Button("Inside", width: content.AvailableWidth);
                }, id: "secondary");
            });

            Check(
                "explicit window ids disambiguate same-title windows",
                primary.W > 0f &&
                secondary.W > 0f &&
                MathF.Abs(primary.X - secondary.X) > 0.1f &&
                MathF.Abs(primary.Y - secondary.Y) > 0.1f);
        }

#if DEBUG
        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            var firstState = new WindowState(new Vector2(16, 16));
            var secondState = new WindowState(new Vector2(48, 48));
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                ui.Frame(360, 260, Vector2.Zero, false, frame =>
                {
                    frame.Window("Duplicate", firstState, 160, content => content.Label("First"));
                    frame.Window("Duplicate", secondState, 160, content => content.Label("Second"));
                });
            });

            Check("duplicate window title ids throw in debug builds",
                ex.Message.Contains("Window \"Duplicate\"", StringComparison.Ordinal));
        }
#endif

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            var windowState = new WindowState(new Vector2(16, 16));
            Response window = default;
            Response underlying = default;
            Response inside = default;

            void Frame(Vector2 mouse, UiInputState input = default)
            {
                ui.Frame(320, 220, mouse, false, input, frame =>
                {
                    underlying = frame.Button("Under", width: 220);
                    window = frame.Window("Inspector", windowState, 180, content =>
                    {
                        inside = content.Button("Inside", width: content.AvailableWidth);
                    }, id: "inspector");
                });
            }

            Frame(Vector2.Zero);
            Frame(new Vector2(40, 34));

            Check(
                "window auto sizes and blocks underlying hover",
                MathF.Abs(window.W - 180f) < 0.1f &&
                window.H > inside.H &&
                inside.X > window.X &&
                inside.Y > window.Y &&
                window.Hovered &&
                !underlying.Hovered);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            var windowState = new WindowState(new Vector2(16, 16));
            Response window = default;
            Response inside = default;

            void Frame(Vector2 mouse)
            {
                ui.Frame(320, 220, mouse, false, frame =>
                {
                    window = frame.Window("Plain", windowState, 180, content =>
                    {
                        inside = content.Button("Inside", width: content.AvailableWidth);
                    }, header: false, id: "plain");
                });
            }

            Frame(Vector2.Zero);
            float expectedContentTop = window.Y + ui.Theme.BorderWidth + ui.Theme.PanelPadding.Top;
            Check(
                "window without header starts content at body padding",
                MathF.Abs(inside.Y - expectedContentTop) < 0.1f &&
                window.H < inside.H + 40f);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            var windowState = new WindowState(new Vector2(16, 16), new Vector2(180, 220));
            Response window = default;
            Response inside = default;

            void Frame(Vector2 mouse)
            {
                ui.Frame(320, 260, mouse, false, frame =>
                {
                    window = frame.Window("Inspector", windowState, 180, content =>
                    {
                        inside = content.Button("Inside", width: content.AvailableWidth);
                    }, resizable: true, id: "inspector-width");
                });
            }

            Frame(Vector2.Zero);
            float expectedInnerWidth = window.W - ui.Theme.BorderWidth * 2f - ui.Theme.PanelPadding.Horizontal;
            Check(
                "resizable window does not reserve scrollbar width when body fits",
                MathF.Abs(inside.W - expectedInnerWidth) < 0.1f);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            var windowState = new WindowState(new Vector2(32, 24));
            Response window = default;

            void Frame(Vector2 mouse, UiInputState input = default)
            {
                ui.Frame(320, 220, mouse, false, input, frame =>
                {
                    window = frame.Window("Draggable", windowState, 180, content =>
                    {
                        content.Label("Window body");
                    }, id: "drag");
                });
            }

            Frame(Vector2.Zero);
            Frame(Vector2.Zero);
            Vector2 start = windowState.Position;
            Vector2 dragStart = new(window.X + 12, window.Y + window.H - 10);
            Frame(dragStart, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(dragStart + new Vector2(42, 24), Input(mouseButtons: [UiMouseButton.Left]));
            Frame(dragStart + new Vector2(42, 24));

            Check(
                "window drags from the body",
                MathF.Abs(windowState.Position.X - (start.X + 42)) < 0.1f &&
                MathF.Abs(windowState.Position.Y - (start.Y + 24)) < 0.1f);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            var windowState = new WindowState(new Vector2(32, 24))
            {
                MaxSize = new Vector2(180, 0)
            };
            Response window = default;

            void Frame(Vector2 mouse, UiInputState input = default)
            {
                ui.Frame(420, 260, mouse, input, frame =>
                {
                    window = frame.Window("Limited", windowState, 180, content =>
                    {
                        content.Label("Window body");
                    }, resizable: true, id: "limited-resize");
                });
            }

            Frame(Vector2.Zero);
            Frame(Vector2.Zero);

            Vector2 grip = new(window.X + window.W - 4f, window.Y + window.H - 4f);
            Frame(grip, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(grip + new Vector2(120f, 0f), Input(mouseButtons: [UiMouseButton.Left]));
            Frame(grip + new Vector2(120f, 0f));

            Vector2 probePastVisibleWidth = new(windowState.Position.X + 220f, windowState.Position.Y + 40f);
            ui.BeginFrame(420, 260, probePastVisibleWidth, default(UiInputState));
            ui.Window("Limited", windowState, 180, content =>
            {
                content.Label("Window body");
            }, resizable: true, id: "limited-resize");
            bool capturedPastVisibleWidth = ui.WantsCaptureMouse;
            ui.EndFrame();

            Check(
                "resizable window max width clamps state before capture checks",
                windowState.Size.X <= 180.1f && !capturedPastVisibleWidth);
        }

        {
            var renderer = new TestRenderer();
            var docking = new DockingState();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false,
                Docking = docking,
                RootPadding = 0f
            };

            var windowState = new WindowState(new Vector2(260, 24));
            Response dockSpace = default;
            Response window = default;

            void Frame(Vector2 mouse, UiInputState input = default)
            {
                ui.Frame(520, 260, mouse, false, input, frame =>
                {
                    dockSpace = frame.DockSpace("main", 220, 150);
                    window = frame.Window("Tool", windowState, 180, content =>
                    {
                        content.Label("Dockable body");
                    }, id: "dockable-tool");
                });
            }

            Frame(Vector2.Zero);
            Frame(Vector2.Zero);
            Vector2 dragStart = new(window.X + 18f, window.Y + window.H - 10f);
            Vector2 dropPoint = new(dockSpace.X + dockSpace.W * 0.5f, dockSpace.Y + dockSpace.H * 0.5f);
            Frame(dragStart, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(dropPoint, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(dropPoint);
            Frame(Vector2.Zero);

            Check(
                "floating window docks when released over a dock space",
                docking.WindowSpaces.Count == 1 &&
                MathF.Abs(window.X - dockSpace.X) < 0.1f &&
                MathF.Abs(window.Y - dockSpace.Y) < 0.1f &&
                MathF.Abs(window.W - dockSpace.W) < 0.1f &&
                MathF.Abs(window.H - dockSpace.H) < 0.1f);
        }

        {
            var renderer = new TestRenderer();
            var docking = new DockingState();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false,
                Docking = docking,
                RootPadding = 0f
            };

            var windowState = new WindowState(new Vector2(260, 24));
            Response dockSpace = default;
            Response window = default;
            Response bodyLabel = default;

            void Frame(Vector2 mouse, UiInputState input = default)
            {
                bodyLabel = default;
                ui.Frame(520, 260, mouse, false, input, frame =>
                {
                    dockSpace = frame.DockSpace("main", 220, 150);
                    window = frame.Window("Tool", windowState, 180, content =>
                    {
                        bodyLabel = content.Label("Dockable body");
                    }, id: "collapsed-dockable-tool");
                });
            }

            Frame(Vector2.Zero);
            Frame(Vector2.Zero);
            Vector2 collapseButton = new(window.X + window.W - 31f, window.Y + 11f);
            Frame(collapseButton, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(collapseButton);
            Frame(Vector2.Zero);

            Vector2 dragStart = new(window.X + 18f, window.Y + 10f);
            Vector2 dropPoint = new(dockSpace.X + dockSpace.W * 0.5f, dockSpace.Y + dockSpace.H * 0.5f);
            Frame(dragStart, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(dropPoint, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(dropPoint);
            Frame(Vector2.Zero);

            Check(
                "collapsed floating window expands when docked",
                docking.WindowSpaces.Count == 1 &&
                !windowState.Collapsed &&
                bodyLabel.W > 0f &&
                bodyLabel.H > 0f);
        }

        {
            var renderer = new TestRenderer();
            var docking = new DockingState();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false,
                Docking = docking,
                RootPadding = 0f
            };

            var windowState = new WindowState(new Vector2(260, 24));
            Response dockSpace = default;
            Response window = default;
            Response resetButton = default;

            void Frame(Vector2 mouse, UiInputState input = default)
            {
                ui.Frame(520, 260, mouse, false, input, frame =>
                {
                    dockSpace = frame.DockSpace("main", 220, 150);
                    window = frame.Window("Metrics", windowState, 180, content =>
                    {
                        resetButton = content.Button("Reset docking", width: content.AvailableWidth);
                        if (resetButton.Clicked)
                            docking.Reset();
                    }, id: "dock-reset-metrics");
                });
            }

            Frame(Vector2.Zero);
            Frame(Vector2.Zero);
            Vector2 dragStart = new(window.X + 18f, window.Y + window.H - 10f);
            Vector2 dropPoint = new(dockSpace.X + dockSpace.W * 0.5f, dockSpace.Y + dockSpace.H * 0.5f);
            Frame(dragStart, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(dropPoint, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(dropPoint);
            Frame(Vector2.Zero);

            Vector2 resetPoint = new(resetButton.X + resetButton.W * 0.5f, resetButton.Y + resetButton.H * 0.5f);
            Frame(resetPoint, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(resetPoint);

            Check(
                "docking can reset from docked window content",
                resetButton.Clicked &&
                docking.WindowSpaces.Count == 0 &&
                docking.WindowRects.Count == 0);
        }

        {
            var renderer = new TestRenderer();
            var docking = new DockingState();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false,
                Docking = docking,
                RootPadding = 0f
            };

            var windowState = new WindowState(new Vector2(260, 24));
            Response dockSpace = default;
            Response window = default;
            Response firstRow = default;

            void Frame(Vector2 mouse, UiInputState input = default)
            {
                ui.Frame(420, 220, mouse, false, input, frame =>
                {
                    dockSpace = frame.DockSpace("main", 190, 96);
                    window = frame.Window("Inspector", windowState, 180, content =>
                    {
                        firstRow = content.Label("Row 0");
                        for (int i = 1; i < 18; i++)
                            content.Label($"Row {i}");
                    }, id: "dock-scroll-tool");
                });
            }

            Frame(Vector2.Zero);
            Frame(Vector2.Zero);
            Vector2 dragStart = new(window.X + 18f, window.Y + window.H - 10f);
            Vector2 dropPoint = new(dockSpace.X + dockSpace.W * 0.5f, dockSpace.Y + dockSpace.H * 0.5f);
            Frame(dragStart, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(dropPoint, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(dropPoint);
            Frame(Vector2.Zero);
            Frame(Vector2.Zero);

            float firstRowYBeforeScroll = firstRow.Y;
            Vector2 wheelPoint = new(dockSpace.X + dockSpace.W * 0.5f, dockSpace.Y + dockSpace.H * 0.7f);
            Frame(wheelPoint, Input(wheel: new Vector2(0, -2)));
            Frame(wheelPoint);

            Check(
                "docked window body scrolls when content exceeds the pane",
                firstRow.Y < firstRowYBeforeScroll - 4f &&
                HasVertexColor(renderer.LastRenderList, ui.Theme.ScrollbarThumb));
        }

        {
            var renderer = new TestRenderer();
            var docking = new DockingState();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false,
                Docking = docking,
                RootPadding = 0f
            };

            var windowState = new WindowState(new Vector2(280, 24));
            Response dockSpace = default;
            Response window = default;

            void Frame(Vector2 mouse, UiInputState input = default)
            {
                ui.Frame(560, 300, mouse, false, input, frame =>
                {
                    dockSpace = frame.DockSpace("main", 260, 170);
                    window = frame.Window("Tool", windowState, 180, content =>
                    {
                        content.Label("Dockable body");
                    }, id: "first-edge-tool");
                });
            }

            Frame(Vector2.Zero);
            Frame(Vector2.Zero);
            Vector2 dragStart = new(window.X + 18f, window.Y + window.H - 10f);
            Vector2 edgeDrop = new(dockSpace.X + dockSpace.W - 8f, dockSpace.Y + dockSpace.H * 0.5f);
            Frame(dragStart, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(edgeDrop, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(edgeDrop);
            Frame(Vector2.Zero);

            Check(
                "dropping the first window on a dock edge creates an aligned pane",
                docking.WindowSpaces.Count == 1 &&
                window.X > dockSpace.X + dockSpace.W * 0.35f &&
                window.X + window.W <= dockSpace.X + dockSpace.W + 0.1f &&
                MathF.Abs(window.Y - dockSpace.Y) < 0.1f &&
                MathF.Abs(window.H - dockSpace.H) < 0.1f);
        }

        {
            var renderer = new TestRenderer();
            var docking = new DockingState();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false,
                Docking = docking,
                RootPadding = 0f
            };

            var leftState = new WindowState(new Vector2(280, 24));
            var rightState = new WindowState(new Vector2(300, 92));
            Response dockSpace = default;
            Response left = default;
            Response right = default;

            void Frame(Vector2 mouse, UiInputState input = default)
            {
                ui.Frame(620, 320, mouse, false, input, frame =>
                {
                    dockSpace = frame.DockSpace("main", 260, 170);
                    left = frame.Window("Left", leftState, 180, content =>
                    {
                        content.Label("Left body");
                    }, id: "split-left");
                    right = frame.Window("Right", rightState, 180, content =>
                    {
                        content.Label("Right body");
                    }, id: "split-right");
                });
            }

            Frame(Vector2.Zero);
            Frame(Vector2.Zero);
            Vector2 leftDragStart = new(left.X + 18f, left.Y + left.H - 10f);
            Vector2 centerDrop = new(dockSpace.X + dockSpace.W * 0.5f, dockSpace.Y + dockSpace.H * 0.5f);
            Frame(leftDragStart, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(centerDrop, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(centerDrop);
            Frame(Vector2.Zero);

            Vector2 rightDragStart = new(right.X + 18f, right.Y + right.H - 10f);
            Vector2 edgeDrop = new(dockSpace.X + dockSpace.W - 8f, dockSpace.Y + dockSpace.H * 0.5f);
            Frame(rightDragStart, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(edgeDrop, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(edgeDrop);
            Frame(Vector2.Zero);

            Check(
                "dropping a second window on a dock edge creates a split layout",
                docking.WindowSpaces.Count == 2 &&
                left.X < right.X &&
                left.X >= dockSpace.X - 0.1f &&
                right.X + right.W <= dockSpace.X + dockSpace.W + 0.1f &&
                MathF.Abs(left.Y - right.Y) < 0.1f &&
                MathF.Abs(left.H - right.H) < 0.1f &&
                left.W > 40f &&
                right.W > 40f);
        }

        {
            var renderer = new TestRenderer();
            var docking = new DockingState();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false,
                Docking = docking,
                RootPadding = 0f
            };

            var windowState = new WindowState(new Vector2(260, 24));
            Response dockSpace = default;
            Response window = default;

            void Frame(Vector2 mouse, UiInputState input = default)
            {
                ui.Frame(560, 280, mouse, false, input, frame =>
                {
                    dockSpace = frame.DockSpace("main", 220, 150);
                    window = frame.Window("Tool", windowState, 180, content =>
                    {
                        content.Label("Dockable body");
                    }, id: "detach-tool");
                });
            }

            Frame(Vector2.Zero);
            Frame(Vector2.Zero);
            Vector2 dragStart = new(window.X + 18f, window.Y + window.H - 10f);
            Vector2 dropPoint = new(dockSpace.X + dockSpace.W * 0.5f, dockSpace.Y + dockSpace.H * 0.5f);
            Frame(dragStart, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(dropPoint, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(dropPoint);
            Frame(Vector2.Zero);

            Vector2 tabGrab = new(dockSpace.X + 24f, dockSpace.Y + 12f);
            Vector2 floatPoint = new(350f, 70f);
            Frame(tabGrab, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(floatPoint, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(floatPoint);
            Frame(Vector2.Zero);

            Check(
                "dragging a dock tab out restores a floating window",
                docking.WindowSpaces.Count == 0 &&
                window.W > 0f &&
                window.H > 0f &&
                MathF.Abs(window.X - dockSpace.X) > 1f);

            Vector2 detachedStart = windowState.Position;
            Vector2 detachedGrab = new(window.X + 18f, window.Y + 12f);
            Vector2 detachedDelta = new(38f, 22f);
            Frame(detachedGrab, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(detachedGrab + detachedDelta, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(detachedGrab + detachedDelta);

            Check(
                "detached dock tab remains draggable",
                MathF.Abs(windowState.Position.X - (detachedStart.X + detachedDelta.X)) < 0.1f &&
                MathF.Abs(windowState.Position.Y - (detachedStart.Y + detachedDelta.Y)) < 0.1f);
        }

        {
            var renderer = new TestRenderer();
            var docking = new DockingState();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false,
                Docking = docking,
                RootPadding = 0f
            };

            var windowState = new WindowState(new Vector2(260, 24));
            Response dockSpace = default;
            Response window = default;

            void Frame(Vector2 mouse, UiInputState input = default)
            {
                ui.Frame(560, 280, mouse, false, input, frame =>
                {
                    dockSpace = frame.DockSpace("main", 220, 150);
                    window = frame.Window("Inspector", windowState, 180, content =>
                    {
                        content.Label("Dockable body");
                    }, id: "dock-close-button");
                });
            }

            Frame(Vector2.Zero);
            Frame(Vector2.Zero);
            Vector2 dragStart = new(window.X + 18f, window.Y + window.H - 10f);
            Vector2 dropPoint = new(dockSpace.X + dockSpace.W * 0.5f, dockSpace.Y + dockSpace.H * 0.5f);
            Frame(dragStart, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(dropPoint, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(dropPoint);
            Frame(Vector2.Zero);

            for (float buttonX = dockSpace.X + 170f; buttonX >= dockSpace.X + 70f && windowState.Open; buttonX -= 4f)
            {
                Vector2 closeButton = new(buttonX, dockSpace.Y + 13f);
                Frame(closeButton, Input(mouseButtons: [UiMouseButton.Left]));
                Frame(closeButton);
            }

            Check(
                "docked close button closes closable windows",
                !windowState.Open &&
                docking.WindowSpaces.Count == 0);
        }

        {
            var renderer = new TestRenderer();
            var docking = new DockingState();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false,
                Docking = docking,
                RootPadding = 0f
            };

            var windowState = new WindowState(new Vector2(260, 24));
            Response dockSpace = default;
            Response window = default;

            void Frame(Vector2 mouse, UiInputState input = default)
            {
                ui.Frame(560, 280, mouse, false, input, frame =>
                {
                    dockSpace = frame.DockSpace("main", 220, 150);
                    window = frame.Window("Inspector", windowState, 180, content =>
                    {
                        content.Label("Dockable body");
                    }, id: "dock-undock-button");
                });
            }

            Frame(Vector2.Zero);
            Frame(Vector2.Zero);
            Vector2 dragStart = new(window.X + 18f, window.Y + window.H - 10f);
            Vector2 dropPoint = new(dockSpace.X + dockSpace.W * 0.5f, dockSpace.Y + dockSpace.H * 0.5f);
            Frame(dragStart, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(dropPoint, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(dropPoint);
            Frame(Vector2.Zero);

            for (float buttonX = dockSpace.X + 40f; buttonX <= dockSpace.X + 170f && docking.WindowSpaces.Count > 0 && windowState.Open; buttonX += 4f)
            {
                Vector2 undockButton = new(buttonX, dockSpace.Y + 13f);
                Frame(undockButton, Input(mouseButtons: [UiMouseButton.Left]));
                Frame(undockButton);
            }
            Frame(Vector2.Zero);

            Check(
                "docked undock button restores a floating window",
                windowState.Open &&
                docking.WindowSpaces.Count == 0 &&
                window.W > 0f &&
                window.H > 0f &&
                MathF.Abs(window.X - dockSpace.X) > 1f);
        }

        {
            var renderer = new TestRenderer();
            var docking = new DockingState();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false,
                Docking = docking,
                RootPadding = 0f
            };

            var windowState = new WindowState(new Vector2(260, 24));
            Response dockSpace = default;
            Response window = default;

            void Frame(Vector2 mouse, UiInputState input = default)
            {
                ui.Frame(560, 280, mouse, false, input, frame =>
                {
                    dockSpace = frame.DockSpace("main", 220, 150);
                    window = frame.Window("Inspector", windowState, 180, content =>
                    {
                        content.Label("Dockable body");
                    }, closable: false, id: "dock-no-close-button");
                });
            }

            Frame(Vector2.Zero);
            Frame(Vector2.Zero);
            Vector2 dragStart = new(window.X + 18f, window.Y + window.H - 10f);
            Vector2 dropPoint = new(dockSpace.X + dockSpace.W * 0.5f, dockSpace.Y + dockSpace.H * 0.5f);
            Frame(dragStart, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(dropPoint, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(dropPoint);
            Frame(Vector2.Zero);

            for (float buttonX = dockSpace.X + 170f; buttonX >= dockSpace.X + 70f && windowState.Open && docking.WindowSpaces.Count > 0; buttonX -= 4f)
            {
                Vector2 wouldBeCloseButton = new(buttonX, dockSpace.Y + 13f);
                Frame(wouldBeCloseButton, Input(mouseButtons: [UiMouseButton.Left]));
                Frame(wouldBeCloseButton);
            }

            Check(
                "docked close button is omitted when the window is not closable",
                windowState.Open);
        }

        {
            var renderer = new TestRenderer();
            var docking = new DockingState();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false,
                Docking = docking,
                RootPadding = 0f
            };

            var floatingState = new WindowState(new Vector2(32, 32));
            var dockedState = new WindowState(new Vector2(300, 24));
            Response dockSpace = default;
            Response floating = default;
            Response docked = default;

            void Frame(Vector2 mouse, UiInputState input = default)
            {
                ui.Frame(640, 320, mouse, false, input, frame =>
                {
                    dockSpace = frame.DockSpace("main", 240, 160);
                    floating = frame.Window("Floating", floatingState, 180, content =>
                    {
                        content.Label("Floating body");
                    }, id: "floating-over-docked-pane");
                    docked = frame.Window("Docked", dockedState, 180, content =>
                    {
                        content.Label("Docked body");
                    }, id: "docked-under-floating-window");
                });
            }

            Frame(Vector2.Zero);
            Frame(Vector2.Zero);
            Vector2 dockedDragStart = new(docked.X + 18f, docked.Y + docked.H - 10f);
            Vector2 dropPoint = new(dockSpace.X + dockSpace.W * 0.5f, dockSpace.Y + dockSpace.H * 0.5f);
            Frame(dockedDragStart, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(dropPoint, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(dropPoint);
            Frame(Vector2.Zero);

            Vector2 start = floatingState.Position;
            Vector2 floatingGrab = new(floating.X + 18f, floating.Y + 12f);
            Vector2 floatingDelta = new(260f, 20f);
            Frame(floatingGrab, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(floatingGrab + floatingDelta, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(floatingGrab + floatingDelta);

            Check(
                "floating windows keep input priority over docked panes",
                MathF.Abs(floatingState.Position.X - (start.X + floatingDelta.X)) < 0.1f &&
                MathF.Abs(floatingState.Position.Y - (start.Y + floatingDelta.Y)) < 0.1f);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            var windowState = new WindowState(new Vector2(32, 24));
            Response button = default;

            void Frame(Vector2 mouse, UiInputState input = default)
            {
                ui.Frame(320, 220, mouse, false, input, frame =>
                {
                    frame.Window("Draggable", windowState, 180, content =>
                    {
                        button = content.Button("Inside", width: content.AvailableWidth);
                    }, id: "drag-blocked");
                });
            }

            Frame(Vector2.Zero);
            Frame(Vector2.Zero);
            Vector2 start = windowState.Position;
            Vector2 buttonCenter = new(button.X + button.W * 0.5f, button.Y + button.H * 0.5f);
            Frame(buttonCenter, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(buttonCenter + new Vector2(42, 24), Input(mouseButtons: [UiMouseButton.Left]));
            Frame(buttonCenter + new Vector2(42, 24));

            Check(
                "window does not drag from interactive child widgets",
                MathF.Abs(windowState.Position.X - start.X) < 0.1f &&
                MathF.Abs(windowState.Position.Y - start.Y) < 0.1f);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            var windowState = new WindowState(new Vector2(32, 24), new Vector2(180, 120));
            bool sectionOpen = true;
            Response window = default;
            Response header = default;

            void Frame(Vector2 mouse, bool mouseDown)
            {
                ui.Frame(360, 260, mouse, mouseDown, frame =>
                {
                    window = frame.Window("Scrollable", windowState, 180, content =>
                    {
                        header = content.CollapsingHeader("Timings", ref sectionOpen, width: content.AvailableWidth);
                        if (sectionOpen)
                        {
                            for (int i = 0; i < 18; i++)
                                content.Label($"Row {i}");
                        }
                    }, resizable: true, id: "collapse-scroll");
                });
            }

            Frame(Vector2.Zero, false);
            Vector2 headerCenter = new(header.X + header.W * 0.5f, header.Y + header.H * 0.5f);
            Frame(headerCenter, true);
            Frame(headerCenter, false);
            Frame(headerCenter, false);

            Check("collapsing header stays closed after window scroll-state relayout", !sectionOpen && window.W > 0);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            var windowState = new WindowState(new Vector2(32, 24))
            {
                MaxSize = new Vector2(0, 120)
            };
            Response window = default;
            Response firstRow = default;

            void Frame(Vector2 mouse, UiInputState input = default)
            {
                ui.Frame(360, 260, mouse, false, input, frame =>
                {
                    window = frame.Window("Scrollable", windowState, 180, content =>
                    {
                        firstRow = content.Label("Row 0");
                        for (int i = 1; i < 18; i++)
                            content.Label($"Row {i}");
                    }, id: "scrollable");
                });
            }

            Frame(Vector2.Zero);
            Frame(Vector2.Zero);
            Vector2 start = windowState.Position;
            Vector2 thumbGrab = new(
                window.X + window.W - ui.Theme.BorderWidth - ui.Theme.ScrollbarWidth * 0.5f,
                firstRow.Y + 8f);
            Frame(thumbGrab, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(thumbGrab + new Vector2(0, 28), Input(mouseButtons: [UiMouseButton.Left]));
            Frame(thumbGrab + new Vector2(0, 28));

            Check(
                "window does not drag from scrollbar thumb",
                MathF.Abs(windowState.Position.X - start.X) < 0.1f &&
                MathF.Abs(windowState.Position.Y - start.Y) < 0.1f);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            var windowState = new WindowState(new Vector2(32, 24), new Vector2(180, 120));
            Response window = default;
            Response firstRow = default;

            void Frame(Vector2 mouse, UiInputState input = default)
            {
                ui.Frame(360, 260, mouse, false, input, frame =>
                {
                    window = frame.Window("Scrollable", windowState, 180, content =>
                    {
                        firstRow = content.Label("Row 0");
                        for (int i = 1; i < 18; i++)
                            content.Label($"Row {i}");
                    }, resizable: true, id: "scrollable-resize");
                });
            }

            Frame(Vector2.Zero);
            Frame(Vector2.Zero);
            Vector2 start = windowState.Position;
            Vector2 thumbGrab = new(
                window.X + window.W - ui.Theme.BorderWidth - ui.Theme.ScrollbarWidth * 0.5f,
                firstRow.Y + 8f);
            Frame(thumbGrab, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(thumbGrab + new Vector2(0, 28), Input(mouseButtons: [UiMouseButton.Left]));
            Frame(thumbGrab + new Vector2(0, 28));

            Check(
                "resizable window does not drag from scrollbar thumb",
                MathF.Abs(windowState.Position.X - start.X) < 0.1f &&
                MathF.Abs(windowState.Position.Y - start.Y) < 0.1f);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            var windowState = new WindowState(new Vector2(32, 24));
            Response window = default;

            void Frame(Vector2 mouse, UiInputState input = default)
            {
                ui.Frame(320, 220, mouse, false, input, frame =>
                {
                    window = frame.Window("Collapsible", windowState, 180, content =>
                    {
                        content.Label("Window body");
                        content.Button("Inside", width: content.AvailableWidth);
                    }, id: "collapse");
                });
            }

            Frame(Vector2.Zero);
            Frame(Vector2.Zero);
            float expandedHeight = window.H;
            Vector2 collapseButton = new(window.X + window.W - 31f, window.Y + 11f);
            Frame(collapseButton, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(collapseButton);
            Frame(Vector2.Zero);

            Check(
                "window collapse button hides the body",
                windowState.Collapsed &&
                window.H > 0 &&
                window.H < expandedHeight);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            var windowState = new WindowState(new Vector2(32, 24));
            Response window = default;

            void Frame(Vector2 mouse, UiInputState input = default)
            {
                ui.Frame(320, 220, mouse, false, input, frame =>
                {
                    window = frame.Window("Closable", windowState, 180, content =>
                    {
                        content.Label("Window body");
                    }, id: "close");
                });
            }

            Frame(Vector2.Zero);
            Frame(Vector2.Zero);
            Vector2 closeButton = new(window.X + window.W - 11f, window.Y + 11f);
            Frame(closeButton, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(closeButton);
            Frame(Vector2.Zero);

            Check(
                "window close button hides the window",
                !windowState.Open &&
                MathF.Abs(window.W) < 0.1f &&
                MathF.Abs(window.H) < 0.1f);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            var windowState = new WindowState(new Vector2(32, 24));
            Response window = default;

            void Frame(Vector2 mouse, UiInputState input = default)
            {
                ui.Frame(320, 220, mouse, false, input, frame =>
                {
                    window = frame.Window("Not Closable", windowState, 180, content =>
                    {
                        content.Label("Window body");
                    }, closable: false, id: "fixed-close");
                });
            }

            Frame(Vector2.Zero);
            Frame(Vector2.Zero);
            float expandedHeight = window.H;
            Vector2 unusedTitleSlot = new(window.X + window.W - 56f, window.Y + 16f);
            Frame(unusedTitleSlot, Input(mouseButtons: [UiMouseButton.Left]));
            Frame(unusedTitleSlot);
            Frame(Vector2.Zero);

            Check(
                "window without close button leaves the extra title slot inert",
                windowState.Open &&
                !windowState.Collapsed &&
                MathF.Abs(window.H - expandedHeight) < 0.1f);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            Response separator = default;
            Response progress = default;
            Response histogram = default;
            float[] histogramValues = [0.5f, 1.25f, 0.75f, 1.8f, 0.2f];

            ui.Frame(320, 180, Vector2.Zero, false, frame =>
            {
                separator = frame.Separator(120, 2);
                progress = frame.ProgressBar(1.4f, 180, 20, "140%");
                histogram = frame.Histogram(histogramValues, 180, 40, "max. 1.80 ms", scaleMin: 0f, scaleMax: 2f);
            });

            Check("separator uses the requested size", MathF.Abs(separator.W - 120f) < 0.1f && MathF.Abs(separator.H - 2f) < 0.1f);
            Check("progress bar reports the requested width and minimum height", MathF.Abs(progress.W - 180f) < 0.1f && progress.H >= 20f);
            Check("histogram reports the requested width and minimum height", MathF.Abs(histogram.W - 180f) < 0.1f && histogram.H >= 40f);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            Color color = new(255, 0, 0, 255);
            Response picker = default;

            void Frame(Vector2 mouse, UiInputState input = default, bool requestHexFocus = false)
            {
                ui.Frame(360, 420, mouse, false, input, frame =>
                {
                    if (requestHexFocus)
                    {
                        using (frame.Id("picker"))
                            frame.RequestFocus(UiWidgetKind.TextField, "hex");
                    }

                    picker = frame.ColorPicker(string.Empty, ref color, 180f, id: "picker");
                });
            }

            Frame(Vector2.Zero);
            Check("color picker reports composite bounds", picker.W >= 180f && picker.H > 220f);

            Frame(new Vector2(106f, 72f), Input(mouseButtons: [UiMouseButton.Left]));
            Check(
                "color picker saturation-value square edits rgb",
                picker.Changed &&
                picker.Pressed &&
                color != new Color(255, 0, 0, 255) &&
                color.A == 255);

            Frame(new Vector2(106f, 72f));
            Frame(new Vector2(106f, 168f), Input(mouseButtons: [UiMouseButton.Left]));
            Check("color picker alpha strip edits alpha", picker.Changed && Math.Abs(color.A - 128) <= 2);

            Frame(Vector2.Zero);
            Frame(Vector2.Zero, Input("00FF00AA", ctrl: true, keys: [UiKey.A]), requestHexFocus: true);
            Check("color picker hex field parses rgba", color == new Color(0, 255, 0, 170) && picker.Changed);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            Color color = new(255, 0, 0, 255);
            Response picker = default;

            void Frame(Vector2 mouse, UiInputState input = default)
            {
                ui.Frame(500, 520, mouse, false, input, frame =>
                {
                    picker = frame.ColorPickerPopup("Accent", ref color, 180f, pickerWidth: 180f, id: "picker");
                });
            }

            Frame(new Vector2(24f, 24f), Input(timeSeconds: 0));
            Check(
                "color picker popup opens on hover",
                picker.Opened && ui.IsChildPopupOpen(UiWidgetKind.ColorPickerPopup, "picker", "popup"));

            bool hasPopupBounds = ui.TryGetChildPopupBounds(
                UiWidgetKind.ColorPickerPopup,
                "picker",
                "popup",
                out float popupX,
                out float popupY,
                out _,
                out _);
            Check("color picker popup exposes bounds", hasPopupBounds);

            Vector2 popupSvPoint = new(
                popupX + ui.Theme.BorderWidth + ui.Theme.PopupPadding.Left + 90f,
                popupY + ui.Theme.BorderWidth + ui.Theme.PopupPadding.Top + 56f);
            Frame(popupSvPoint, Input(mouseButtons: [UiMouseButton.Left], timeSeconds: 0.02));
            Frame(popupSvPoint, Input(timeSeconds: 0.03));
            Check("color picker popup applies picker edits", picker.Changed && color != new Color(255, 0, 0, 255));

            Frame(new Vector2(470f, 500f), Input(timeSeconds: 0.5));
            Check("color picker popup closes after hover leaves", !ui.IsChildPopupOpen(UiWidgetKind.ColorPickerPopup, "picker", "popup"));
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            Color color = new(255, 0, 0, 255);
            Response picker = default;

            void Frame(Vector2 mouse, UiInputState input = default)
            {
                ui.Frame(500, 520, mouse, false, input, frame =>
                {
                    picker = frame.ColorPickerPopup(
                        "Accent",
                        ref color,
                        180f,
                        pickerWidth: 180f,
                        id: "click-picker",
                        openOnHover: false);
                });
            }

            Frame(new Vector2(24f, 24f), Input(timeSeconds: 0));
            Check(
                "color picker popup click mode does not open on hover",
                !picker.Opened && !ui.IsChildPopupOpen(UiWidgetKind.ColorPickerPopup, "click-picker", "popup"));

            Frame(new Vector2(24f, 24f), Input(mouseButtons: [UiMouseButton.Left], timeSeconds: 0.01));
            Frame(new Vector2(24f, 24f), Input(timeSeconds: 0.02));
            Check(
                "color picker popup click mode opens on click",
                picker.Opened && ui.IsChildPopupOpen(UiWidgetKind.ColorPickerPopup, "click-picker", "popup"));

            Frame(new Vector2(470f, 500f), Input(mouseButtons: [UiMouseButton.Left], timeSeconds: 0.03));
            Check(
                "color picker popup click mode closes on outside click",
                !ui.IsChildPopupOpen(UiWidgetKind.ColorPickerPopup, "click-picker", "popup"));
        }

        Console.WriteLine($"Ui widgets: {passed} passed, {failed} failed\n");
    }

    static void RunUiSliderTests(TrueTypeFont font)
    {
        int passed = 0, failed = 0;

        void Check(string name, bool condition)
        {
            Assert.True(condition, name);
            Console.WriteLine($"  PASS  {name}");
            passed++;
        }

        Console.WriteLine("=== Ui Slider Tests ===");

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            float value = 0;
            Response slider = default;
            bool changedOnPress = false;

            void Frame(Vector2 mouse, bool mouseDown, UiInputState input = default, bool enabled = true)
            {
                ui.Frame(320, 180, mouse, mouseDown, input, frame =>
                {
                    slider = frame.Slider("Amount", ref value, 0, 100, 180, step: 10, enabled: enabled, id: "amount");
                });
                changedOnPress |= slider.Changed;
            }

            Frame(new Vector2(106, 24), true);
            Frame(new Vector2(106, 24), false);
            Check("slider click snaps value from pointer position", MathF.Abs(value - 50) < 0.1f && changedOnPress && slider.W >= 180f);

            Frame(new Vector2(106, 24), true);
            Frame(new Vector2(188, 24), true);
            Frame(new Vector2(188, 24), false);
            Check("slider drag clamps to the range", MathF.Abs(value - 100) < 0.1f);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            float value = 40;
            Response slider = default;
            Vector2 sliderFocusPoint = Vector2.Zero;

            ui.Frame(320, 180, Vector2.Zero, false, frame =>
            {
                slider = frame.Slider("Keyboard", ref value, 0, 100, 180, step: 5, id: "keyboard");
            });

            float innerWidth = MathF.Max(0, slider.W - ui.Theme.BorderWidth * 2f);
            float blockWidth = ui.Theme.SliderBlockWidthFactor > 0
                ? Math.Clamp(innerWidth * ui.Theme.SliderBlockWidthFactor, 0, innerWidth)
                : MathF.Min(MathF.Max(0, ui.Theme.SliderBlockWidth), innerWidth);
            float normalized = (value - 0f) / 100f;
            float blockTravel = MathF.Max(0, innerWidth - blockWidth);
            float blockCenterX = slider.X + ui.Theme.BorderWidth + normalized * blockTravel + blockWidth * 0.5f;
            sliderFocusPoint = new Vector2(blockCenterX, slider.Y + slider.H * 0.5f);

            ui.Frame(320, 180, sliderFocusPoint, true, frame =>
            {
                slider = frame.Slider("Keyboard", ref value, 0, 100, 180, step: 5, id: "keyboard");
            });
            ui.Frame(320, 180, sliderFocusPoint, false, frame =>
            {
                slider = frame.Slider("Keyboard", ref value, 0, 100, 180, step: 5, id: "keyboard");
            });
            ui.Frame(320, 180, Vector2.Zero, false, Input(keys: new[] { UiKey.Right }), frame =>
            {
                slider = frame.Slider("Keyboard", ref value, 0, 100, 180, step: 5, id: "keyboard");
            });
            Check("focused slider responds to keyboard nudging", MathF.Abs(value - 45) < 0.1f && slider.Changed && slider.Focused);

            ui.Frame(320, 180, Vector2.Zero, false, Input(keys: new[] { UiKey.End }), frame =>
            {
                slider = frame.Slider("Keyboard", ref value, 0, 100, 180, step: 5, id: "keyboard");
            });
            Check("end moves the slider to max", MathF.Abs(value - 100) < 0.1f && slider.Changed);

            ui.Frame(320, 180, Vector2.Zero, false, Input(keys: new[] { UiKey.Home }), frame =>
            {
                slider = frame.Slider("Keyboard", ref value, 0, 100, 180, step: 5, id: "keyboard");
            });
            Check("home moves the slider to min", MathF.Abs(value - 0) < 0.1f && slider.Changed);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            int intValue = 3;
            Response slider = default;

            ui.Frame(320, 180, Vector2.Zero, false, Input(keys: new[] { UiKey.Tab }), frame =>
            {
                slider = frame.SliderInt("Steps", ref intValue, 0, 10, 180, step: 2, id: "steps");
            });
            ui.Frame(320, 180, Vector2.Zero, false, Input(keys: new[] { UiKey.Right }), frame =>
            {
                slider = frame.SliderInt("Steps", ref intValue, 0, 10, 180, step: 2, id: "steps");
            });

            Check("slider int rounds to the stepped value", intValue == 6 && slider.Changed);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            float value = 20;
            Response slider = default;

            ui.Frame(320, 180, new Vector2(188, 24), true, frame =>
            {
                slider = frame.Slider("Disabled", ref value, 0, 100, 180, step: 10, enabled: false, id: "disabled");
            });
            ui.Frame(320, 180, new Vector2(188, 24), false, frame =>
            {
                slider = frame.Slider("Disabled", ref value, 0, 100, 180, step: 10, enabled: false, id: "disabled");
            });

            Check("disabled slider ignores pointer input", MathF.Abs(value - 20) < 0.1f && slider.Disabled && !slider.Changed);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            float value = 1f;
            Response drag = default;
            bool changedDuringDrag = false;

            void Frame(Vector2 mouse, bool mouseDown)
            {
                ui.Frame(320, 180, mouse, mouseDown, frame =>
                {
                    drag = frame.DragFloat("Gain", ref value, speed: 0.1f, min: 0f, max: 5f, width: 180, id: "gain");
                });
                changedDuringDrag |= drag.Changed;
            }

            Frame(new Vector2(40, 24), false);
            Frame(new Vector2(40, 24), true);
            Frame(new Vector2(60, 24), true);
            Frame(new Vector2(60, 24), false);

            Check("drag float updates value from horizontal dragging",
                MathF.Abs(value - 3f) < 0.1f && changedDuringDrag && drag.W >= 180f);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            int value = 3;
            Response drag = default;
            bool changedDuringDrag = false;

            void Frame(Vector2 mouse, bool mouseDown)
            {
                ui.Frame(320, 180, mouse, mouseDown, frame =>
                {
                    drag = frame.DragInt("Count", ref value, speed: 0.25f, min: 0, max: 10, width: 180, id: "count");
                });
                changedDuringDrag |= drag.Changed;
            }

            Frame(new Vector2(40, 24), false);
            Frame(new Vector2(40, 24), true);
            Frame(new Vector2(52, 24), true);
            Frame(new Vector2(52, 24), false);

            Check("drag int updates rounded value from horizontal dragging",
                value == 6 && changedDuringDrag && drag.W >= 180f);
        }

        Console.WriteLine($"Ui slider: {passed} passed, {failed} failed\n");
    }

    static void RunUiScrollTests(TrueTypeFont font)
    {
        int passed = 0, failed = 0;

        void Check(string name, bool condition)
        {
            Assert.True(condition, name);
            Console.WriteLine($"  PASS  {name}");
            passed++;
        }

        Console.WriteLine("=== Ui Scroll Tests ===");

        var renderer = new TestRenderer();
        var ui = new Ui(renderer)
        {
            Font = font,
            DefaultFontSize = 18f,
            Lcd = false
        };

        Response wheelArea = default, wheelFirst = default, wheelSecond = default;

        void WheelFrame(Vector2 mouse, UiInputState input = default, bool mouseDown = false)
        {
            ui.Frame(800, 600, mouse, mouseDown, input, frame =>
            {
                wheelArea = frame.ScrollArea("wheel", 180, 90, frame =>
                {
                    wheelFirst = frame.Button("First");
                    wheelSecond = frame.Button("Second");
                    for (int i = 0; i < 10; i++)
                        frame.Button($"Item {i}");
                });
            });
        }

        WheelFrame(new Vector2(30, 30));
        Check("scroll area initially exposes top content", wheelFirst.Hovered);
        Check("scroll area keeps themed gap between stacked buttons", wheelSecond.Y >= wheelFirst.Y + wheelFirst.H + ui.Theme.Gap - 0.1f);

        WheelFrame(new Vector2(30, 30), Input(wheel: new Vector2(0, 3)));
        Check("wheel at top clamp does not oscillate state", !wheelArea.Changed);

        WheelFrame(new Vector2(30, 30), Input(wheel: new Vector2(0, -3)));
        Check("wheel scrolling changes scroll area state", wheelArea.Changed);

        WheelFrame(new Vector2(30, 30));
        Check("scroll area clip blocks hit-testing for scrolled-off content", !wheelFirst.Hovered);

        WheelFrame(new Vector2(30, 30), Input(wheel: new Vector2(0, -50)));
        WheelFrame(new Vector2(30, 30), Input(wheel: new Vector2(0, -5)));
        Check("wheel at bottom clamp does not oscillate state", !wheelArea.Changed);

        Response dragArea = default, dragFirst = default;

        void DragFrame(Vector2 mouse, bool mouseDown)
        {
            ui.Frame(800, 600, mouse, mouseDown, frame =>
            {
                dragArea = frame.ScrollArea("drag", 180, 90, frame =>
                {
                    dragFirst = frame.Button("First");
                    for (int i = 0; i < 10; i++)
                        frame.Button($"Item {i}");
                });
            });
        }

        DragFrame(Vector2.Zero, false);
        DragFrame(new Vector2(188, 26), true);
        DragFrame(new Vector2(188, 72), true);
        Check("dragging the scrollbar reports a changed scroll area", dragArea.Changed);

        DragFrame(new Vector2(30, 30), false);
        Check("dragged scrollbar keeps content clipped and offset", !dragFirst.Hovered);

        Response panArea = default, panFirst = default;

        void PanFrame(Vector2 mouse, bool mouseDown)
        {
            ui.Frame(800, 600, mouse, mouseDown, frame =>
            {
                panArea = frame.ScrollArea("pan", 180, 90, frame =>
                {
                    panFirst = frame.Label("First");
                    for (int i = 0; i < 16; i++)
                        frame.Label($"Line {i}");
                });
            });
        }

        PanFrame(Vector2.Zero, false);
        PanFrame(new Vector2(30, 70), true);
        PanFrame(new Vector2(30, 20), true);
        Check("dragging scroll area content reports a changed scroll area", panArea.Changed);

        PanFrame(new Vector2(30, 20), false);
        Check("dragged scroll area content keeps content clipped and offset", !panFirst.Hovered);

        Response guardedArea = default, guardedButton = default;

        void GuardedPanFrame(Vector2 mouse, bool mouseDown)
        {
            ui.Frame(800, 600, mouse, mouseDown, frame =>
            {
                guardedArea = frame.ScrollArea("guarded-pan", 180, 90, frame =>
                {
                    guardedButton = frame.Button("First");
                    for (int i = 0; i < 10; i++)
                        frame.Button($"Guarded {i}");
                });
            });
        }

        GuardedPanFrame(Vector2.Zero, false);
        GuardedPanFrame(new Vector2(30, 20), true);
        GuardedPanFrame(new Vector2(30, 1), true);
        Check("drag starting on a child widget does not pan scroll area content", !guardedArea.Changed);
        Check("drag starting on a child widget still lets the child press", guardedButton.Pressed);

        Response panBothArea = default;

        void PanBothFrame(Vector2 mouse, bool mouseDown)
        {
            ui.Frame(800, 600, mouse, mouseDown, frame =>
            {
                panBothArea = frame.ScrollAreaBoth("pan-both", 120, 80, frame =>
                {
                    frame.Canvas(260, 180, static canvas =>
                    {
                        canvas.DrawRect(0, 0, 260, 180, new Color(40, 40, 40));
                    });
                });
            });
        }

        PanBothFrame(Vector2.Zero, false);
        PanBothFrame(new Vector2(70, 60), true);
        PanBothFrame(new Vector2(20, 20), true);
        Check("dragging bidirectional scroll area content reports a changed scroll area", panBothArea.Changed);

        Console.WriteLine($"Ui scroll: {passed} passed, {failed} failed\n");
    }

    static void RunUiPopupTests(TrueTypeFont font)
    {
        int passed = 0, failed = 0;

        void Check(string name, bool condition)
        {
            Assert.True(condition, name);
            Console.WriteLine($"  PASS  {name}");
            passed++;
        }

        Console.WriteLine("=== Ui Popup Tests ===");

        var renderer = new TestRenderer();
        var ui = new Ui(renderer)
        {
            Font = font,
            DefaultFontSize = 18f,
            Lcd = false
        };

        Response anchor = default;
        Response outside = default;
        Response popupPrimary = default;
        Response popupMore = default;
        Response childItem = default;
        bool openMenu = false;
        bool openChild = false;
        bool openEdge = false;

        void Frame(
            Vector2 mouse,
            bool mouseDown,
            UiInputState input = default,
            bool renderMenu = true,
            bool renderEdge = false)
        {
            anchor = default;
            outside = default;
            popupPrimary = default;
            popupMore = default;
            childItem = default;

            ui.Frame(400, 300, mouse, mouseDown, input, frame =>
            {
                frame.Row(frame =>
                {
                    anchor = frame.Button("Menu");
                    outside = frame.Button("Outside");
                });

                if (openMenu)
                {
                    frame.OpenPopup("menu");
                    openMenu = false;
                }

                if (renderMenu)
                {
                    frame.Popup("menu", anchor, 170, 120, frame =>
                    {
                        popupPrimary = frame.Button("Primary");
                        popupMore = frame.Button("More");

                        if (openChild)
                        {
                            frame.OpenPopup("child");
                            openChild = false;
                        }

                        frame.Popup("child", popupMore.X + popupMore.W + 6, popupMore.Y, 120, 90, frame =>
                        {
                            childItem = frame.Button("Child");
                        });
                    });
                }

                if (openEdge)
                {
                    frame.OpenPopup("edge");
                    openEdge = false;
                }

                if (renderEdge)
                {
                    frame.Popup("edge", 360, 280, 120, 120, frame =>
                    {
                        frame.Button("Edge");
                    });
                }
            });
        }

        openMenu = true;
        Frame(Vector2.Zero, false);
        Check("popup opens and renders content", popupPrimary.W > 0 && popupMore.W > 0 && ui.IsPopupOpen("menu"));

        Frame(Vector2.Zero, false);
        Check("popup stays open while declared each frame", popupPrimary.W > 0 && popupMore.W > 0 && ui.IsPopupOpen("menu"));

        var outsideMouse = new Vector2(outside.X + 4, outside.Y + 4);
        Frame(outsideMouse, false);
        Check("open popup blocks underlying hover", !outside.Hovered);

        Frame(outsideMouse, true);
        Check("outside press does not activate underlying controls", !outside.Hovered && !outside.Pressed && !outside.Clicked);

        Frame(outsideMouse, false);
        Check("outside press closes the root popup", !ui.IsPopupOpen("menu") && popupPrimary.W == 0);

        openMenu = true;
        Frame(Vector2.Zero, false);
        Frame(Vector2.Zero, false, renderMenu: false);
        Check("popup closes when omitted from the frame", !ui.IsPopupOpen("menu"));

        openMenu = true;
        Frame(Vector2.Zero, false);
        openChild = true;
        Frame(Vector2.Zero, false);
        Check("nested popup renders child content", childItem.W > 0);

        var parentMouse = new Vector2(popupPrimary.X + 4, popupPrimary.Y + 4);
        Frame(parentMouse, true);
        Check("clicking parent popup area closes the child subtree", popupPrimary.Hovered && childItem.W == 0 && ui.IsPopupOpen("menu"));

        Frame(parentMouse, false);
        Check("root popup stays open after closing a child subtree", ui.IsPopupOpen("menu") && childItem.W == 0);

        openEdge = true;
        Frame(Vector2.Zero, false, renderMenu: false, renderEdge: true);
        bool hasEdgeBounds = ui.TryGetPopupBounds("edge", out float popupX, out float popupY, out float popupW, out float popupH);
        Check("popup bounds are clamped to the viewport", hasEdgeBounds && popupX >= 0 && popupY >= 0 && popupX + popupW <= 400.5f && popupY + popupH <= 300.5f);

        Console.WriteLine($"Ui popup: {passed} passed, {failed} failed\n");
    }

    static void RunUiLifetimeTests(TrueTypeFont font)
    {
        int passed = 0, failed = 0;

        void Check(string name, bool condition)
        {
            Assert.True(condition, name);
            Console.WriteLine($"  PASS  {name}");
            passed++;
        }

        Console.WriteLine("=== Ui Lifetime Tests ===");

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            ui.Frame(300, 200, Vector2.Zero, false, frame =>
            {
                frame.Label("Dispose atlas");
            });

            int createdTextures = renderer.CreateTextureCalls;
            ui.Dispose();
            Check("disposing ui destroys cached atlas textures", createdTextures > 0 && renderer.DestroyTextureCalls == createdTextures);

            bool threwDisposed = false;
            try
            {
                ui.Frame(300, 200, Vector2.Zero, false, frame =>
                {
                    frame.Label("After dispose");
                });
            }
            catch (ObjectDisposedException)
            {
                threwDisposed = true;
            }

            Check("disposed ui rejects new frames", threwDisposed);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false
            };

            Assert.Throws<InvalidOperationException>(() =>
            {
                ui.Frame(300, 200, Vector2.Zero, false, frame =>
                {
                    frame.Label("Before throw");
                    throw new InvalidOperationException("boom");
                });
            });

            Check("frame callback ends the renderer frame when content throws", renderer.EndFrameCalls == 1);
        }

        {
            var renderer = new TestRenderer();
            var ui = new Ui(renderer)
            {
                Font = font,
                DefaultFontSize = 18f,
                Lcd = false,
                StateRetentionFrames = 0
            };

            Response first = default;
            var widgetStatesField = typeof(Ui).GetField("_widgetStates", BindingFlags.Instance | BindingFlags.NonPublic)!;

            void ScrollFrame(Vector2 mouse, UiInputState input = default, bool render = true)
            {
                first = default;
                ui.Frame(400, 260, mouse, false, input, frame =>
                {
                    if (!render) return;

                    frame.ScrollArea("retained", 180, 90, frame =>
                    {
                        first = frame.Button("First");
                        for (int i = 0; i < 10; i++)
                            frame.Button($"Item {i}");
                    });
                });
            }

            ScrollFrame(new Vector2(30, 30), Input(wheel: new Vector2(0, -50)));
            int retainedStateCount = ((System.Collections.IDictionary)widgetStatesField.GetValue(ui)!).Count;

            ScrollFrame(Vector2.Zero, render: false);
            int evictedStateCount = ((System.Collections.IDictionary)widgetStatesField.GetValue(ui)!).Count;
            Check("stale widget state is evicted after disappearing", retainedStateCount > 0 && evictedStateCount == 0);
        }

        Console.WriteLine($"Ui lifetime: {passed} passed, {failed} failed\n");
    }

    static void RunUiRenderingTests(TrueTypeFont font)
    {
        int passed = 0, failed = 0;

        void Check(string name, bool condition)
        {
            Assert.True(condition, name);
            Console.WriteLine($"  PASS  {name}");
            passed++;
        }

        Console.WriteLine("=== Ui Rendering Tests ===");

        var renderer = new TestRenderer();
        var ui = new Ui(renderer)
        {
            Font = font,
            DefaultFontSize = 18f,
            Lcd = false
        };
        ui.Theme.BorderWidth = 2f;
        ui.Theme.BorderRadius = 7f;

        ui.Frame(360, 220, Vector2.Zero, false, frame =>
        {
            frame.Button("Styled");
        });

        bool buttonFrame =
            HasVertexColor(renderer.LastRenderList, ui.Theme.ButtonBg) &&
            HasVertexColor(renderer.LastRenderList, ui.Theme.ButtonBorder);
        Check("button uses themed border and radius", buttonFrame);

        ui.Theme.BorderWidth = 4f;
        ui.Theme.BorderRadius = 2f;
        Response smallRadiusButton = default;
        ui.Frame(360, 220, Vector2.Zero, false, frame =>
        {
            smallRadiusButton = frame.Button("Small radius");
        });

        float rightMostTopFillX = renderer.LastRenderList!.Vertices
            .Where(v => v.Color == ui.Theme.ButtonBg && MathF.Abs(v.Pos.Y - smallRadiusButton.Y) < 0.01f)
            .Select(v => v.Pos.X)
            .DefaultIfEmpty(float.PositiveInfinity)
            .Max();
        Check("positive frame radius keeps a visible inner radius", rightMostTopFillX <= smallRadiusButton.X + smallRadiusButton.W - ui.Theme.BorderWidth - 1f + 0.1f);
        ui.Theme.BorderWidth = 2f;
        ui.Theme.BorderRadius = 7f;

        ui.Frame(360, 220, Vector2.Zero, false, Input(keys: new[] { UiKey.Tab }), frame =>
        {
            frame.Button("Styled");
        });
        ui.Frame(360, 220, Vector2.Zero, false, frame =>
        {
            frame.Button("Styled");
        });

        bool focusedButtonFrame =
            HasVertexColor(renderer.LastRenderList, ui.Theme.ButtonBg) &&
            HasVertexColor(renderer.LastRenderList, ui.Theme.FocusBorder);
        Check("tab-focused button uses focus border styling", focusedButtonFrame);

        string fieldText = "Hello";
        ui.Frame(360, 220, Vector2.Zero, false, frame =>
        {
            frame.TextField("name", ref fieldText, 180);
        });

        bool textFieldFrame =
            HasVertexColor(renderer.LastRenderList, ui.Theme.TextFieldBg) &&
            HasVertexColor(renderer.LastRenderList, ui.Theme.TextFieldBorder);
        Check("text field uses shared frame styling", textFieldFrame);

        ui.Frame(360, 220, Vector2.Zero, false, frame =>
        {
            frame.ScrollArea("styledScroll", 180, 90, inner =>
            {
                for (int i = 0; i < 12; i++)
                    inner.Button($"Item {i}", width: inner.AvailableWidth);
            });
        });

        bool roundedScrollbar = HasVertexColor(renderer.LastRenderList, ui.Theme.ScrollbarThumb);
        Check("scrollbars use rounded rect rendering", roundedScrollbar);

        bool checkedValue = true;
        int radioValue = 1;
        float sliderValue = 45;
        float[] histogramValues = [0.5f, 1.25f, 0.75f, 1.8f, 0.2f];
        ui.Frame(360, 260, Vector2.Zero, false, frame =>
        {
            frame.Checkbox("Enabled", ref checkedValue, width: 180);
            frame.RadioValue("Warm", ref radioValue, 1, width: 180);
            frame.ProgressBar(0.6f, 180, 20, "60%");
            frame.Histogram(histogramValues, 180, 40, "max. 1.80 ms", scaleMin: 0f, scaleMax: 2f);
            frame.Slider("Amount", ref sliderValue, 0, 100, 180, step: 5, id: "slider");
            frame.Selectable("System", true, width: 180);
            frame.MenuItem("Open recent", selected: false, width: 180);
            frame.Image(7, 180, 40, tint: ui.Theme.Accent);
            frame.Separator(180, 2);
        });

        bool widgetPackVisuals =
            HasVertexColor(renderer.LastRenderList, ui.Theme.ToggleIndicator) &&
            HasVertexColor(renderer.LastRenderList, ui.Theme.ProgressBarFill) &&
            HasVertexColor(renderer.LastRenderList, ui.Theme.PlotFill) &&
            HasVertexColor(renderer.LastRenderList, ui.Theme.SliderFill) &&
            HasVertexColor(renderer.LastRenderList, ui.Theme.SelectableBgSelected) &&
            HasVertexColor(renderer.LastRenderList, ui.Theme.Separator) &&
            renderer.LastRenderList?.Commands.Any(cmd => cmd.TextureId == 7) == true;
        Check("checkbox, radio, progress, histogram, and separator use themed colors", widgetPackVisuals);

        {
            var fallbackRenderer = new TestRenderer();
            var fallbackUi = new Ui(fallbackRenderer)
            {
                DefaultFontSize = 18f,
                Lcd = false
            };

            bool rendered = true;
            try
            {
                fallbackUi.Frame(320, 180, Vector2.Zero, false, frame =>
                {
                    frame.Label("Default embedded font");
                });
            }
            catch
            {
                rendered = false;
            }

            Check(
                "ui falls back to embedded default font when Font is null",
                rendered &&
                fallbackRenderer.CreateTextureCalls > 0 &&
                fallbackRenderer.LastRenderList != null &&
                fallbackRenderer.LastRenderList.Commands.Count > 0);
        }

        Console.WriteLine($"Ui rendering: {passed} passed, {failed} failed\n");
    }

    static bool HasVertexColor(RenderList? renderList, Color color)
        => renderList != null && renderList.Vertices.Any(vertex => vertex.Color.Equals(color));

    static bool AllTrianglesFrontFacing(RenderList renderList)
    {
        for (int i = 0; i < renderList.Indices.Count; i += 3)
        {
            var a = renderList.Vertices[(int)renderList.Indices[i]].Pos;
            var b = renderList.Vertices[(int)renderList.Indices[i + 1]].Pos;
            var c = renderList.Vertices[(int)renderList.Indices[i + 2]].Pos;
            if (SignedTriangleArea(a, b, c) >= -0.001f)
                return false;
        }

        return true;
    }

    static float SignedTriangleArea(Vector2 a, Vector2 b, Vector2 c)
        => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    static UiInputState Input(
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

    static void RenderLine(TrueTypeFont font, string text, float scale, float size, string outputDir)
    {
        var vm = font.GetFontVMetrics();
        int ascent = (int)MathF.Ceiling(vm.Ascent * scale);
        int descent = (int)MathF.Floor(vm.Descent * scale);
        int lineHeight = ascent - descent;

        int totalWidth = 0;
        int prevGlyph = 0;
        foreach (char ch in text)
        {
            int gi = font.FindGlyphIndex(ch);
            if (prevGlyph != 0)
                totalWidth += (int)(font.GetKernAdvance(prevGlyph, gi) * scale);
            var m = font.GetScaledGlyphMetrics(gi, scale);
            totalWidth += (int)MathF.Ceiling(m.AdvanceWidth);
            prevGlyph = gi;
        }

        int canvasW = totalWidth + 4;
        int canvasH = lineHeight + 4;
        var canvas = new byte[canvasW * canvasH];

        int penX = 2;
        int baseline = 2 + ascent;
        prevGlyph = 0;

        foreach (char ch in text)
        {
            int gi = font.FindGlyphIndex(ch);
            if (prevGlyph != 0)
                penX += (int)(font.GetKernAdvance(prevGlyph, gi) * scale);

            var bitmap = font.RasterizeGlyph(gi, scale, out int w, out int h, out int ox, out int oy);
            if (bitmap != null)
            {
                int destX = penX + ox;
                int destY = baseline + oy;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        int cx = destX + x;
                        int cy = destY + y;
                        if (cx >= 0 && cx < canvasW && cy >= 0 && cy < canvasH)
                            canvas[cy * canvasW + cx] = Math.Max(canvas[cy * canvasW + cx], bitmap[y * w + x]);
                    }
            }

            var m = font.GetScaledGlyphMetrics(gi, scale);
            penX += (int)MathF.Ceiling(m.AdvanceWidth);
            prevGlyph = gi;
        }

        WritePgm(Path.Combine(outputDir, $"{(int)size}px_line.pgm"), canvas, canvasW, canvasH);
        Console.WriteLine($"  line render: {canvasW}x{canvasH}");
    }

    internal sealed class TestRenderer : IRenderer
    {
        public int CreateTextureCalls { get; private set; }
        public int DestroyTextureCalls { get; private set; }
        public int EndFrameCalls { get; private set; }
        public RenderList? LastRenderList { get; private set; }
        public RenderFrameInfo LastFrame { get; private set; }
        private int _nextTextureId = 1;

        public void BeginFrame(RenderFrameInfo frame)
        {
            LastFrame = frame;
            LastRenderList = null;
        }

        public void Render(RenderList renderList) => LastRenderList = CloneRenderList(renderList);
        public void EndFrame() => EndFrameCalls++;

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

    internal sealed class TestUiPlatform : IUiPlatform
    {
        public string ClipboardText { get; set; } = string.Empty;
        public UiCursor LastCursor { get; set; } = UiCursor.Arrow;

        public string GetClipboardText() => ClipboardText;

        public void SetClipboardText(string text) => ClipboardText = text;

        public void SetCursor(UiCursor cursor) => LastCursor = cursor;
    }
}
