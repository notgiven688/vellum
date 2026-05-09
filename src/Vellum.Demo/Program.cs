using System.Numerics;
using System.Text;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Vellum.Rendering;
using Vector2i = OpenTK.Mathematics.Vector2i;

namespace Vellum.Demo;

internal static class Program
{
    private static readonly Vector2i InitialWindowSize = new(1280, 820);

    private static void Main()
    {
        ConfigureGlfwDpiHints();

        var gameSettings = GameWindowSettings.Default;
        gameSettings.UpdateFrequency = 60.0;

        var settings = NativeWindowSettings.Default;
        settings.ClientSize = InitialWindowSize;
        settings.Title = "Vellum Demo";
        settings.APIVersion = new Version(3, 3);
        settings.Profile = ContextProfile.Core;
        settings.Flags = ContextFlags.ForwardCompatible;
        settings.NumberOfSamples = 4;

        using var window = new OpenTkDemoWindow(gameSettings, settings);
        window.Run();
    }

    private static void ConfigureGlfwDpiHints()
    {
        Platform? preferredPlatform = ResolvePreferredGlfwPlatform();
        if (preferredPlatform.HasValue)
            GLFW.InitHint(InitHintPlatform.Platform, preferredPlatform.Value);

        if (!GLFW.Init() && preferredPlatform.HasValue)
        {
            GLFW.Terminate();
            GLFW.InitHint(InitHintPlatform.Platform, Platform.Any);
            GLFW.Init();
        }

        GLFW.WindowHint(WindowHintBool.ScaleToMonitor, true);
        GLFW.WindowHint(WindowHintBool.ScaleFramebuffer, true);
    }

    private static Platform? ResolvePreferredGlfwPlatform()
    {
        string? requested = Environment.GetEnvironmentVariable("VELLUM_GLFW_PLATFORM");
        if (string.Equals(requested, "x11", StringComparison.OrdinalIgnoreCase))
            return Platform.X11;
        if (string.Equals(requested, "wayland", StringComparison.OrdinalIgnoreCase))
            return Platform.Wayland;
        if (string.Equals(requested, "any", StringComparison.OrdinalIgnoreCase))
            return Platform.Any;

        return OperatingSystem.IsLinux() &&
               !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))
            ? Platform.Wayland
            : null;
    }
}

internal sealed class OpenTkDemoWindow : GameWindow
{
    private readonly HashSet<UiKey> _pressedKeys = new();
    private readonly HashSet<UiMouseButton> _downMouseButtons = new();
    private readonly StringBuilder _textInput = new();
    private readonly DemoState _state = new();
    private Vector2 _wheelDelta;
    private OpenTkRenderer? _renderer;
    private Ui? _ui;
    private int _checkerTexture = -1;

    public OpenTkDemoWindow(GameWindowSettings gameWindowSettings, NativeWindowSettings nativeWindowSettings)
        : base(gameWindowSettings, nativeWindowSettings)
    {
    }

    protected override void OnLoad()
    {
        base.OnLoad();
        VSync = VSyncMode.On;

        _renderer = new OpenTkRenderer();
        _checkerTexture = _renderer.CreateTexture(DemoScene.CreateCheckerRgba(), 16, 16);
        _ui = new Ui(_renderer)
        {
            Platform = new OpenTkUiPlatform(this),
            FontStack = UiFont.Merge(
                UiFont.Source(UiFonts.DefaultSans),
                UiFont.Source(MaterialSymbols.Font, offsetY: 4f)),
            DefaultFontSize = 18f,
            Lcd = true
        };
    }

    protected override void OnUnload()
    {
        _ui?.Dispose();
        _renderer?.Dispose();
        base.OnUnload();
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        if (_ui is null)
            return;

        UpdateDemoMetrics(args.Time);

        _ui.Theme = _state.ResolveTheme();
        UiInputState input = BuildInput();
        var mouse = new Vector2(MouseState.Position.X, MouseState.Position.Y);
        RenderFrameInfo frame = GetCurrentRenderFrameInfo();
        _ui.Frame(
            frame,
            mouse,
            input,
            new DemoFrameContext(_state, _checkerTexture, frame.LogicalHeight),
            static (root, context) => DemoScene.DrawRoot(root, context));

        SwapBuffers();
        _pressedKeys.Clear();
        _textInput.Clear();
        _wheelDelta = default;
    }

    private unsafe RenderFrameInfo GetCurrentRenderFrameInfo()
    {
        GLFW.GetWindowSize(WindowPtr, out int logicalWidth, out int logicalHeight);
        GLFW.GetFramebufferSize(WindowPtr, out int framebufferWidth, out int framebufferHeight);

        if (logicalWidth <= 0 || logicalHeight <= 0)
        {
            logicalWidth = ClientSize.X;
            logicalHeight = ClientSize.Y;
        }

        if (framebufferWidth <= 0 || framebufferHeight <= 0)
        {
            framebufferWidth = FramebufferSize.X;
            framebufferHeight = FramebufferSize.Y;
        }

        return new RenderFrameInfo(logicalWidth, logicalHeight, framebufferWidth, framebufferHeight);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        _textInput.Append(e.AsString);
    }

    protected override void OnKeyDown(KeyboardKeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (TryMapKey(e.Key, out UiKey key))
            _pressedKeys.Add(key);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        _wheelDelta += new Vector2(e.OffsetX, e.OffsetY);
    }

    private void UpdateDemoMetrics(double frameSeconds)
    {
        _state.UiCpuTimeMs = (float)(_ui?.LastCpuFrameMs ?? 0);
        _state.SmoothedUiCpuTimeMs = _state.SmoothedUiCpuTimeMs <= 0f
            ? _state.UiCpuTimeMs
            : _state.SmoothedUiCpuTimeMs + (_state.UiCpuTimeMs - _state.SmoothedUiCpuTimeMs) * 0.12f;
        _state.FrameTimeMs = (float)(frameSeconds * 1000.0);
        _state.SmoothedFrameTimeMs = _state.SmoothedFrameTimeMs <= 0f
            ? _state.FrameTimeMs
            : _state.SmoothedFrameTimeMs + (_state.FrameTimeMs - _state.SmoothedFrameTimeMs) * 0.12f;
        _state.Fps = frameSeconds > 0 ? (int)Math.Round(1.0 / frameSeconds) : 0;
        _state.HeapSizeBytes = GC.GetTotalMemory(forceFullCollection: false);
        _state.TotalAllocatedBytes = GC.GetTotalAllocatedBytes(precise: false);
        _state.Gen0Collections = GC.CollectionCount(0);
        _state.Gen1Collections = GC.CollectionCount(1);
        _state.Gen2Collections = GC.CollectionCount(2);
        _state.TotalGcPauseDuration = GC.GetTotalPauseDuration();
        _state.GcPauseDeltaMs = Math.Max(0, (_state.TotalGcPauseDuration - _state.PreviousTotalGcPauseDuration).TotalMilliseconds);
        _state.PreviousTotalGcPauseDuration = _state.TotalGcPauseDuration;
        _state.GcPauseTotalMs = _state.TotalGcPauseDuration.TotalMilliseconds;
        _state.GcPausePercentage = GC.GetGCMemoryInfo().PauseTimePercentage;
    }

    private UiInputState BuildInput()
    {
        _downMouseButtons.Clear();
        if (MouseState.IsButtonDown(MouseButton.Left)) _downMouseButtons.Add(UiMouseButton.Left);
        if (MouseState.IsButtonDown(MouseButton.Right)) _downMouseButtons.Add(UiMouseButton.Right);
        if (MouseState.IsButtonDown(MouseButton.Middle)) _downMouseButtons.Add(UiMouseButton.Middle);

        bool shift = KeyboardState.IsKeyDown(Keys.LeftShift) || KeyboardState.IsKeyDown(Keys.RightShift);
        bool ctrl = KeyboardState.IsKeyDown(Keys.LeftControl) || KeyboardState.IsKeyDown(Keys.RightControl);
        bool alt = KeyboardState.IsKeyDown(Keys.LeftAlt) || KeyboardState.IsKeyDown(Keys.RightAlt);
        bool meta = KeyboardState.IsKeyDown(Keys.LeftSuper) || KeyboardState.IsKeyDown(Keys.RightSuper);

        string? textInput = _textInput.Length > 0 ? _textInput.ToString() : null;

        return new UiInputState(
            textInput,
            _pressedKeys.Count > 0 ? _pressedKeys : null,
            _wheelDelta,
            shift,
            ctrl,
            alt,
            meta,
            _downMouseButtons.Count > 0 ? _downMouseButtons : null,
            GLFW.GetTime());
    }

    private static bool TryMapKey(Keys key, out UiKey uiKey)
    {
        uiKey = key switch
        {
            Keys.Left => UiKey.Left,
            Keys.Right => UiKey.Right,
            Keys.Up => UiKey.Up,
            Keys.Down => UiKey.Down,
            Keys.Home => UiKey.Home,
            Keys.End => UiKey.End,
            Keys.Tab => UiKey.Tab,
            Keys.Enter => UiKey.Enter,
            Keys.KeyPadEnter => UiKey.Enter,
            Keys.Escape => UiKey.Escape,
            Keys.Space => UiKey.Space,
            Keys.Backspace => UiKey.Backspace,
            Keys.Delete => UiKey.Delete,
            Keys.A => UiKey.A,
            Keys.C => UiKey.C,
            Keys.V => UiKey.V,
            Keys.X => UiKey.X,
            _ => default
        };

        return key is Keys.Left or Keys.Right or Keys.Up or Keys.Down
            or Keys.Home or Keys.End or Keys.Tab or Keys.Enter or Keys.KeyPadEnter
            or Keys.Escape or Keys.Space or Keys.Backspace or Keys.Delete
            or Keys.A or Keys.C or Keys.V or Keys.X;
    }
}
