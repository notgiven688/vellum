using System.Numerics;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Vellum.Rendering;

namespace Vellum.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class SceneBenchmarks
{
    private const int ViewportWidth = 1280;
    private const int ViewportHeight = 720;
    private const int WarmupFrames = 12;

    private static readonly RenderFrameInfo s_frame = new(ViewportWidth, ViewportHeight);
    private static readonly Vector2 s_mouse = new(-100f, -100f);
    private static readonly UiInputState s_input = new(timeSeconds: 1.0);

    private static readonly Action<Ui, BenchmarkState> s_emptyScene = static (ui, state) =>
    {
        ui.FillViewport(state.Theme.SurfaceBg);
    };

    private static readonly Action<Ui, BenchmarkState> s_labelScene = static (ui, state) =>
    {
        ui.FillViewport(state.Theme.SurfaceBg);
        using (ui.MaxWidth(540f))
        {
            for (int i = 0; i < state.Labels.Length; i++)
                ui.Label(state.Labels[i], maxWidth: ui.AvailableWidth, wrap: TextWrapMode.NoWrap);
        }
    };

    private static readonly Action<Ui, BenchmarkState> s_controlsScene = static (ui, state) =>
    {
        ui.FillViewport(state.Theme.SurfaceBg);
        using (ui.MaxWidth(520f))
        {
            ui.Panel(ui.AvailableWidth, state, static (panel, state) =>
            {
                panel.Heading("Controls");
                panel.Separator();

                for (int i = 0; i < 24; i++)
                {
                    using (panel.Id(i))
                    using (panel.Row())
                    {
                        using (panel.FixedWidth(170f))
                            panel.Checkbox(state.OptionLabels[i], ref state.Toggles[i], width: 170f);

                        using (panel.FixedWidth(170f))
                            panel.Switch(state.SwitchLabels[i], ref state.Switches[i], width: 170f);

                        using (panel.FixedWidth(130f))
                            panel.Button(state.ButtonLabels[i], width: 130f);
                    }
                }

                panel.Separator();
                for (int i = 0; i < 8; i++)
                {
                    using (panel.Id(i))
                        panel.Slider(state.SliderLabels[i], ref state.SliderValues[i], 0f, 1f, panel.AvailableWidth, format: "{0:0.00}");
                }
            });
        }
    };

    private static readonly Action<Ui, BenchmarkState> s_textEditingScene = static (ui, state) =>
    {
        ui.FillViewport(state.Theme.SurfaceBg);
        using (ui.MaxWidth(560f))
        {
            ui.Panel(ui.AvailableWidth, state, static (panel, state) =>
            {
                panel.Heading("Text");
                panel.TextField("Title", ref state.TitleText, panel.AvailableWidth, placeholder: "Name");
                panel.TextField("Search", ref state.SearchText, panel.AvailableWidth, placeholder: "Filter rows");
                panel.TextArea("Notes", ref state.NotesText, panel.AvailableWidth, 220f);
                panel.Separator();
                for (int i = 0; i < 18; i++)
                    panel.Label(state.WrappedLabels[i], maxWidth: panel.AvailableWidth, wrap: TextWrapMode.WordWrap);
            });
        }
    };

    private static readonly Action<Ui, BenchmarkState> s_scrollAreaScene = static (ui, state) =>
    {
        ui.FillViewport(state.Theme.SurfaceBg);
        using (ui.MaxWidth(520f))
        {
            ui.ScrollArea("items", ui.AvailableWidth, 560f, state, static (area, state) =>
            {
                for (int i = 0; i < state.ListItems.Length; i++)
                {
                    using (area.Id(i))
                    {
                        area.Selectable(state.ListItems[i], state.SelectedIndex == i, width: area.AvailableWidth);
                    }
                }
            });
        }
    };

    private static readonly Action<Ui, BenchmarkState> s_tableScene = static (ui, state) =>
    {
        ui.FillViewport(state.Theme.SurfaceBg);
        using (ui.MaxWidth(720f))
        {
            ui.Table("metrics", state.TableColumns, state, static (table, state) =>
            {
                for (int i = 0; i < BenchmarkState.TableRows; i++)
                {
                    int index = i;
                    table.Row(state, static (row, state) =>
                    {
                        row.Cell(state.TableNames[state.CurrentTableRow]);
                        row.Cell(state.TableValues[state.CurrentTableRow]);
                        row.Cell(state.TableStates[state.CurrentTableRow]);
                        row.Cell(state, static (cell, state) =>
                        {
                            cell.ProgressBar(state.TableProgress[state.CurrentTableRow], cell.AvailableWidth, height: 18f);
                        });
                    });

                    state.CurrentTableRow = index + 1;
                }

                state.CurrentTableRow = 0;
            }, width: ui.AvailableWidth, cellPadding: new EdgeInsets(2f, 5f));
        }
    };

    private static readonly Action<Ui, BenchmarkState> s_plotScene = static (ui, state) =>
    {
        ui.FillViewport(state.Theme.SurfaceBg);
        using (ui.MaxWidth(560f))
        {
            ui.Panel(ui.AvailableWidth, state, static (panel, state) =>
            {
                panel.Heading("Telemetry");
                panel.Histogram(state.HistogramValues, panel.AvailableWidth, 110f, overlay: "frame samples");
                panel.ProgressBar(0.18f, panel.AvailableWidth, overlay: "UI budget");
                panel.ProgressBar(0.62f, panel.AvailableWidth, overlay: "physics");
                panel.ProgressBar(0.41f, panel.AvailableWidth, overlay: "render");
                panel.Separator();
                panel.Image(state.CheckerTexture, panel.AvailableWidth, 120f, tint: new Color(255, 255, 255, 210));
            });
        }
    };

    private static readonly Action<Ui, BenchmarkState> s_colorPickerScene = static (ui, state) =>
    {
        ui.FillViewport(state.Theme.SurfaceBg);
        using (ui.MaxWidth(360f))
        {
            ui.Panel(ui.AvailableWidth, state, static (panel, state) =>
            {
                panel.Heading("Color");
                panel.ColorPicker("Accent", ref state.Accent, panel.AvailableWidth, alpha: true, id: "accent");
                panel.Separator();
                panel.ColorPickerPopup("Panel", ref state.PanelColor, panel.AvailableWidth, pickerWidth: 300f, id: "panel", openOnHover: false);
                panel.ColorPickerPopup("Text", ref state.TextColor, panel.AvailableWidth, pickerWidth: 300f, id: "text", openOnHover: false);
            });
        }
    };

    private static readonly Action<Ui, BenchmarkState> s_windowsDockScene = static (ui, state) =>
    {
        ui.Theme = state.Theme;
        ui.Docking = state.Docking;
        ui.FillViewport(state.Theme.SurfaceBg);
        ui.DockSpace("main-dock", ui.AvailableWidth, 360f);

        ui.Window("Inspector", state.InspectorWindow, 320f, state, static (window, state) =>
        {
            window.Label("Selected object", color: window.Theme.Accent);
            window.TextField("Name", ref state.TitleText, window.AvailableWidth);
            window.Separator();
            for (int i = 0; i < 10; i++)
            {
                using (window.Id(i))
                    window.Slider(state.SliderLabels[i % state.SliderLabels.Length], ref state.SliderValues[i % state.SliderValues.Length], 0f, 1f, window.AvailableWidth, format: "{0:0.00}");
            }
        }, resizable: true, closable: true);

        ui.Window("Metrics", state.MetricsWindow, 300f, state, static (window, state) =>
        {
            window.Table("window-metrics", state.WindowTableColumns, state, static (table, state) =>
            {
                for (int i = 0; i < 8; i++)
                {
                    int index = i;
                    table.Row(state, static (row, state) =>
                    {
                        row.Cell(state.TableNames[state.CurrentTableRow]);
                        row.Cell(state.TableValues[state.CurrentTableRow]);
                    });

                    state.CurrentTableRow = index + 1;
                }

                state.CurrentTableRow = 0;
            }, width: window.AvailableWidth, cellPadding: new EdgeInsets(2f, 5f));
            window.Histogram(state.HistogramValues, window.AvailableWidth, 76f);
        }, resizable: true, closable: true);

        ui.Window("Theme", state.ThemeWindow, 300f, state, static (window, state) =>
        {
            Theme theme = state.Theme;
            window.ColorPickerPopup("Accent", ref theme.Accent, window.AvailableWidth, id: "theme-accent", openOnHover: false);
            window.ColorPickerPopup("Button", ref theme.ButtonBg, window.AvailableWidth, id: "theme-button", openOnHover: false);
            window.Slider("Radius", ref theme.BorderRadius, 0f, 14f, window.AvailableWidth, format: "{0:0.0}");
            window.Slider("Border", ref theme.BorderWidth, 0f, 3f, window.AvailableWidth, format: "{0:0.0}");
            window.Slider("Gap", ref theme.Gap, 0f, 20f, window.AvailableWidth, format: "{0:0.0}");
        }, resizable: true, closable: false);
    };

    private readonly record struct NamedScene(string Name, Action<Ui, BenchmarkState> Draw);

    private static readonly NamedScene[] s_scenes =
    [
        new("EmptyFrame", s_emptyScene),
        new("Labels100", s_labelScene),
        new("ControlsGrid", s_controlsScene),
        new("TextEditing", s_textEditingScene),
        new("ScrollAreaRows", s_scrollAreaScene),
        new("TableRows", s_tableScene),
        new("PlotsAndImage", s_plotScene),
        new("ColorPicker", s_colorPickerScene),
        new("WindowsAndDocking", s_windowsDockScene)
    ];

    private CountingRenderer _renderer = null!;
    private Ui _ui = null!;
    private BenchmarkState _state = null!;

    [GlobalSetup]
    public void Setup()
    {
        _renderer = new CountingRenderer();
        _state = new BenchmarkState(_renderer);
        _ui = new Ui(_renderer)
        {
            Font = UiFonts.DefaultSans,
            Theme = _state.Theme,
            DefaultFontSize = 16f,
            Lcd = false,
            RootPadding = 12f
        };

        for (int frame = 0; frame < WarmupFrames; frame++)
        {
            foreach (var scene in s_scenes)
                RunFrame(scene.Draw);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _ui.Dispose();
    }

    [Benchmark(Baseline = true)]
    public int EmptyFrame() => RunFrame(s_emptyScene);

    [Benchmark]
    public int Labels100() => RunFrame(s_labelScene);

    [Benchmark]
    public int ControlsGrid() => RunFrame(s_controlsScene);

    [Benchmark]
    public int TextEditing() => RunFrame(s_textEditingScene);

    [Benchmark]
    public int ScrollAreaRows() => RunFrame(s_scrollAreaScene);

    [Benchmark]
    public int TableRows() => RunFrame(s_tableScene);

    [Benchmark]
    public int PlotsAndImage() => RunFrame(s_plotScene);

    [Benchmark]
    public int ColorPicker() => RunFrame(s_colorPickerScene);

    [Benchmark]
    public int WindowsAndDocking() => RunFrame(s_windowsDockScene);

    public static void Smoke()
    {
        var benchmarks = new SceneBenchmarks();
        benchmarks.Setup();

        try
        {
            foreach (var scene in s_scenes)
                Print(scene.Name, benchmarks.RunFrame(scene.Draw), benchmarks._renderer);
        }
        finally
        {
            benchmarks.Cleanup();
        }

        static void Print(string name, int commands, CountingRenderer renderer)
            => Console.WriteLine($"{name,-18} commands={commands,4} vertices={renderer.VertexCount,6} indices={renderer.IndexCount,6}");
    }

    public static int RunLoop(string sceneName, int iterations)
    {
        var scene = FindScene(sceneName);
        if (scene is null)
        {
            Console.Error.WriteLine($"Unknown scene '{sceneName}'. Available scenes: {string.Join(", ", s_scenes.Select(static s => s.Name))}");
            return 1;
        }

        var benchmarks = new SceneBenchmarks();
        benchmarks.Setup();

        try
        {
            var stopwatch = Stopwatch.StartNew();
            int commandCount = 0;
            for (int i = 0; i < iterations; i++)
                commandCount = benchmarks.RunFrame(scene.Value.Draw);

            stopwatch.Stop();
            double frameMicroseconds = stopwatch.Elapsed.TotalMilliseconds * 1000.0 / Math.Max(1, iterations);
            Console.WriteLine($"{scene.Value.Name}: {iterations:N0} frames in {stopwatch.Elapsed.TotalSeconds:0.###} s ({frameMicroseconds:0.###} us/frame, commands={commandCount})");
            return 0;
        }
        finally
        {
            benchmarks.Cleanup();
        }
    }

    private static NamedScene? FindScene(string name)
    {
        foreach (var scene in s_scenes)
        {
            if (string.Equals(scene.Name, name, StringComparison.OrdinalIgnoreCase))
                return scene;
        }

        return null;
    }

    private int RunFrame(Action<Ui, BenchmarkState> scene)
    {
        _ui.Docking = null;
        _ui.Theme = _state.Theme;
        _ui.Frame(s_frame, s_mouse, s_input, _state, scene);
        return _renderer.CommandCount;
    }

    private sealed class BenchmarkState
    {
        public const int TableRows = 48;

        public readonly Theme Theme = ThemePresets.Dark();
        public readonly DockingState Docking = new();
        public readonly WindowState InspectorWindow = new(new Vector2(40f, 58f), new Vector2(320f, 320f)) { MinSize = new Vector2(220f, 150f) };
        public readonly WindowState MetricsWindow = new(new Vector2(382f, 82f), new Vector2(300f, 260f)) { MinSize = new Vector2(220f, 120f) };
        public readonly WindowState ThemeWindow = new(new Vector2(704f, 72f), new Vector2(300f, 270f)) { MinSize = new Vector2(220f, 140f) };

        public readonly string[] Labels = new string[100];
        public readonly string[] WrappedLabels = new string[18];
        public readonly string[] ListItems = new string[180];
        public readonly string[] OptionLabels = new string[24];
        public readonly string[] SwitchLabels = new string[24];
        public readonly string[] ButtonLabels = new string[24];
        public readonly string[] SliderLabels = new string[10];
        public readonly bool[] Toggles = new bool[24];
        public readonly bool[] Switches = new bool[24];
        public readonly float[] SliderValues = new float[10];
        public readonly float[] HistogramValues = new float[96];
        public readonly TableColumn[] TableColumns =
        [
            new("Name", 150f),
            new("Value", 90f, UiAlign.End),
            new("State", 90f),
            new("Load", 0f)
        ];
        public readonly TableColumn[] WindowTableColumns =
        [
            new("Metric"),
            new("Value", 84f, UiAlign.End)
        ];
        public readonly string[] TableNames = new string[TableRows];
        public readonly string[] TableValues = new string[TableRows];
        public readonly string[] TableStates = new string[TableRows];
        public readonly float[] TableProgress = new float[TableRows];

        public string TitleText = "RigidBody_037";
        public string SearchText = "contacts active";
        public string NotesText =
            "The headless benchmark uses the real Vellum frame lifecycle and a no-op renderer. " +
            "It keeps text, layout, clipping, windows, docking, tables, and color widgets in the measurement.";
        public Color Accent = new(210, 138, 36, 255);
        public Color PanelColor = new(28, 30, 34, 230);
        public Color TextColor = new(220, 225, 232, 255);
        public int SelectedIndex = 42;
        public int CurrentTableRow;
        public int CheckerTexture;

        public BenchmarkState(IRenderer renderer)
        {
            Theme.BorderRadius = 6f;
            Theme.BorderWidth = 1f;
            Theme.Gap = 6f;

            for (int i = 0; i < Labels.Length; i++)
                Labels[i] = $"Entity {i:000}  position=({i * 3 - 140}, {i * 7 - 220})  sleeping={(i & 3) == 0}";

            for (int i = 0; i < WrappedLabels.Length; i++)
                WrappedLabels[i] = $"Channel {i:00}: this row intentionally wraps through the layout engine so text measurement and clipping stay visible in the benchmark.";

            for (int i = 0; i < ListItems.Length; i++)
                ListItems[i] = $"Object {i:000}  type={(i % 3 == 0 ? "dynamic" : i % 3 == 1 ? "static" : "sensor")}  island={i % 11}";

            for (int i = 0; i < OptionLabels.Length; i++)
            {
                OptionLabels[i] = $"Flag {i:00}";
                SwitchLabels[i] = $"Route {i:00}";
                ButtonLabels[i] = $"Apply {i:00}";
                Toggles[i] = (i & 1) == 0;
                Switches[i] = (i % 3) == 0;
            }

            for (int i = 0; i < SliderLabels.Length; i++)
            {
                SliderLabels[i] = $"Gain {i:00}";
                SliderValues[i] = (i + 1) / (float)(SliderLabels.Length + 1);
            }

            for (int i = 0; i < HistogramValues.Length; i++)
            {
                float wave = MathF.Sin(i * 0.24f) * 0.5f + 0.5f;
                float pulse = i % 17 == 0 ? 0.95f : 0.18f;
                HistogramValues[i] = MathF.Min(1f, wave * 0.68f + pulse);
            }

            string[] states = ["Awake", "Sleep", "Cold", "Hot"];
            for (int i = 0; i < TableRows; i++)
            {
                TableNames[i] = $"Body_{i:000}";
                TableValues[i] = $"{(i * 13.37f + 3.5f):0.00}";
                TableStates[i] = states[i % states.Length];
                TableProgress[i] = (i % 19) / 18f;
            }

            CheckerTexture = renderer.CreateTexture(CreateCheckerTexture(), 16, 16);
        }

        private static byte[] CreateCheckerTexture()
        {
            byte[] rgba = new byte[16 * 16 * 4];
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    bool bright = ((x / 4) + (y / 4)) % 2 == 0;
                    byte v = bright ? (byte)230 : (byte)72;
                    int offset = (y * 16 + x) * 4;
                    rgba[offset + 0] = v;
                    rgba[offset + 1] = v;
                    rgba[offset + 2] = v;
                    rgba[offset + 3] = 255;
                }
            }

            return rgba;
        }
    }

    private sealed class CountingRenderer : IRenderer
    {
        private int _nextTextureId = 1;

        public int CommandCount { get; private set; }
        public int VertexCount { get; private set; }
        public int IndexCount { get; private set; }

        public void BeginFrame(RenderFrameInfo frame)
        {
        }

        public void Render(RenderList renderList)
        {
            CommandCount = renderList.Commands.Count;
            VertexCount = renderList.Vertices.Count;
            IndexCount = renderList.Indices.Count;
        }

        public void EndFrame()
        {
        }

        public int CreateTexture(byte[] rgba, int width, int height) => _nextTextureId++;

        public void DestroyTexture(int textureId)
        {
        }
    }
}
