using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;
using Vellum.Rendering;

namespace Vellum;

/// <summary>
/// Result of a widget invocation — bounds + interaction state.
/// </summary>
public readonly struct Response
{
    /// <summary>Left edge of the widget bounds in logical pixels.</summary>
    public readonly float X;
    /// <summary>Top edge of the widget bounds in logical pixels.</summary>
    public readonly float Y;
    /// <summary>Width of the widget bounds in logical pixels.</summary>
    public readonly float W;
    /// <summary>Height of the widget bounds in logical pixels.</summary>
    public readonly float H;
    /// <summary>Whether the pointer hovered the widget this frame.</summary>
    public readonly bool Hovered;
    /// <summary>Whether the widget is actively pressed this frame.</summary>
    public readonly bool Pressed;
    /// <summary>Whether the widget completed a pointer click this frame.</summary>
    public readonly bool Clicked;
    /// <summary>Whether the widget has keyboard focus.</summary>
    public readonly bool Focused;
    /// <summary>Whether the widget changed its bound value this frame.</summary>
    public readonly bool Changed;
    /// <summary>Whether the widget submitted an edit or action this frame.</summary>
    public readonly bool Submitted;
    /// <summary>Whether the widget cancelled an edit this frame.</summary>
    public readonly bool Cancelled;
    /// <summary>Whether the widget was disabled.</summary>
    public readonly bool Disabled;
    /// <summary>Whether the widget was read-only.</summary>
    public readonly bool ReadOnly;
    /// <summary>Whether the widget toggled a boolean/open state this frame.</summary>
    public readonly bool Toggled;
    /// <summary>Whether the widget opened this frame.</summary>
    public readonly bool Opened;
    /// <summary>Whether the widget closed this frame.</summary>
    public readonly bool Closed;
    /// <summary>Whether the widget was activated by pointer or keyboard submission.</summary>
    public bool Activated => Clicked || Submitted;
    /// <summary>Whether an open/collapsed state changed this frame.</summary>
    public bool OpenChanged => Opened || Closed;

    /// <summary>
    /// Creates a widget response.
    /// </summary>
    public Response(
        float x,
        float y,
        float w,
        float h,
        bool hovered,
        bool pressed,
        bool clicked,
        bool focused = false,
        bool changed = false,
        bool submitted = false,
        bool cancelled = false,
        bool disabled = false,
        bool readOnly = false,
        bool toggled = false,
        bool opened = false,
        bool closed = false)
    {
        X = x;
        Y = y;
        W = w;
        H = h;
        Hovered = hovered;
        Pressed = pressed;
        Clicked = clicked;
        Focused = focused;
        Changed = changed;
        Submitted = submitted;
        Cancelled = cancelled;
        Disabled = disabled;
        ReadOnly = readOnly;
        Toggled = toggled;
        Opened = opened;
        Closed = closed;
    }
}

/// <summary>
/// Horizontal alignment for constrained layout scopes.
/// </summary>
public enum UiAlign
{
    /// <summary>Align content to the start of the available width.</summary>
    Start,
    /// <summary>Center content inside the available width.</summary>
    Center,
    /// <summary>Align content to the end of the available width.</summary>
    End
}

/// <summary>
/// Persistent state for a floating Vellum window.
/// </summary>
public sealed class WindowState
{
    /// <summary>Current top-left window position in logical pixels.</summary>
    public Vector2 Position;
    /// <summary>Current window size for resizable windows.</summary>
    public Vector2 Size;
    /// <summary>Minimum size for resizable windows.</summary>
    public Vector2 MinSize = new(160f, 80f);
    /// <summary>Reserved maximum size for future window constraints.</summary>
    public Vector2 MaxSize;
    /// <summary>Whether the window is open and should be rendered.</summary>
    public bool Open = true;
    /// <summary>Whether the window body is collapsed.</summary>
    public bool Collapsed;

    /// <summary>Creates window state at the default position.</summary>
    public WindowState()
    {
    }

    /// <summary>Creates window state with an initial position.</summary>
    public WindowState(Vector2 position, bool open = true, bool collapsed = false)
    {
        Position = position;
        Open = open;
        Collapsed = collapsed;
    }

    /// <summary>Creates window state with an initial position and size.</summary>
    public WindowState(Vector2 position, Vector2 size, bool open = true, bool collapsed = false)
    {
        Position = position;
        Size = size;
        Open = open;
        Collapsed = collapsed;
    }
}

/// <summary>
/// Identifies a Vellum widget kind. Used by <see cref="Ui.RequestFocus(UiWidgetKind, UiId)"/>
/// to disambiguate widgets that share a label across kinds.
/// </summary>
public enum UiWidgetKind
{
    /// <summary>A clickable button.</summary>
    Button = 1,
    /// <summary>A checkbox bound to a boolean.</summary>
    Checkbox,
    /// <summary>An on/off switch bound to a boolean.</summary>
    Switch,
    /// <summary>A radio button.</summary>
    RadioButton,
    /// <summary>A selectable row.</summary>
    Selectable,
    /// <summary>A menu opened from a menu bar or as a submenu.</summary>
    Menu,
    /// <summary>A menu item row.</summary>
    MenuItem,
    /// <summary>A combo box.</summary>
    ComboBox,
    /// <summary>A floating-point slider.</summary>
    Slider,
    /// <summary>An integer slider.</summary>
    SliderInt,
    /// <summary>A floating-point drag field.</summary>
    DragFloat,
    /// <summary>An integer drag field.</summary>
    DragInt,
    /// <summary>A collapsing header.</summary>
    CollapsingHeader,
    /// <summary>A single-line text field.</summary>
    TextField,
    /// <summary>A multi-line text area.</summary>
    TextArea,
    /// <summary>A draggable splitter.</summary>
    Splitter,
    /// <summary>A tab bar.</summary>
    TabBar,
    /// <summary>A tab inside a tab bar.</summary>
    Tab,
    /// <summary>An expandable tree node.</summary>
    TreeNode,
    /// <summary>A leaf row in a tree.</summary>
    TreeLeaf,
    /// <summary>A vertical scroll area.</summary>
    ScrollArea,
    /// <summary>A scroll area that scrolls in both directions.</summary>
    ScrollAreaBoth,
    /// <summary>A floating window.</summary>
    Window,
    /// <summary>A popup container.</summary>
    Popup
}

/// <summary>
/// Immediate-mode GUI context. Layout scopes are opened with lambdas
/// (Row / Column), widgets are methods that return a Response.
/// </summary>
public sealed partial class Ui : IDisposable
{
    private enum LayoutDir { Vertical, Horizontal }

    private struct LayoutScope
    {
        public float OriginX, OriginY;
        public float CursorX, CursorY;
        public LayoutDir Dir;
        public float MaxExtent; // max width (vertical) or max height (horizontal)
        public float WidthConstraint;
        public bool HasWidthConstraint;
        public bool Empty;
        public float PendingGap;
        public bool HasPendingGap;
        public float DefaultGap;
        public bool HasDefaultGap;
        public float AlignOffsetX;
        public bool ReserveWidth;
    }

    private sealed class WidgetStateEntry
    {
        public required object State;
        public int LastSeenFrame;
    }

    private readonly struct ControlVisuals
    {
        public readonly Color Fill;
        public readonly Color Border;
        public readonly Color Foreground;

        public ControlVisuals(Color fill, Color border, Color foreground)
        {
            Fill = fill;
            Border = border;
            Foreground = foreground;
        }
    }

    private readonly struct DeferredUiContent
    {
        private static readonly Action<Ui, object?, object?> InvokeActionContent = InvokeAction;

        private readonly object? _content;
        private readonly object? _state;
        private readonly Action<Ui, object?, object?>? _invoke;

        private DeferredUiContent(object? content, object? state, Action<Ui, object?, object?> invoke)
        {
            _content = content;
            _state = state;
            _invoke = invoke;
        }

        public bool HasContent => _invoke != null;

        public static DeferredUiContent Create(Action<Ui> content)
        {
            ArgumentNullException.ThrowIfNull(content);
            return new DeferredUiContent(content, null, InvokeActionContent);
        }

        public static DeferredUiContent Create<TState>(TState state, Action<Ui, TState> content)
        {
            ArgumentNullException.ThrowIfNull(content);
            return new DeferredUiContent(content, state, DeferredUiContentInvoker<TState>.Invoke);
        }

        public void Invoke(Ui ui)
        {
            if (_invoke == null)
                throw new InvalidOperationException("Deferred UI content was not initialized.");

            _invoke(ui, _content, _state);
        }

        private static void InvokeAction(Ui ui, object? content, object? state)
            => ((Action<Ui>)content!)(ui);
    }

    private static class DeferredUiContentInvoker<TState>
    {
        public static readonly Action<Ui, object?, object?> Invoke = InvokeContent;

        private static void InvokeContent(Ui ui, object? content, object? state)
            => ((Action<Ui, TState>)content!)(ui, (TState)state!);
    }

    private readonly struct ClipRect
    {
        public readonly float X;
        public readonly float Y;
        public readonly float W;
        public readonly float H;

        public ClipRect(float x, float y, float w, float h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }
    }

    private readonly IRenderer _renderer;
    private readonly Painter _framePainter = new();
    private Painter _painter;
    private readonly Dictionary<(TrueTypeFont font, float size, float rasterScale, bool lcd), GlyphAtlas> _atlases = new();
    private readonly Dictionary<int, WidgetStateEntry> _widgetStates = new();
    private readonly Dictionary<int, PopupRequest> _popupRequestsByDepth = new();
    private readonly List<int> _openPopupIds = new();
    private readonly Dictionary<int, ClipRect> _popupRectsPrev = new();
    private readonly Dictionary<int, ClipRect> _popupRectsCurrent = new();
    private readonly HashSet<int> _modalPopupIdsPrev = new();
    private readonly HashSet<int> _modalPopupIdsCurrent = new();
    private readonly HashSet<int> _menuPopupIds = new();
    private readonly Dictionary<int, WindowRuntimeState> _windowRuntimeStates = new();
    private readonly Dictionary<int, WindowRequest> _windowRequests = new();
    private readonly List<int> _windowOrder = new();
    private readonly HashSet<int> _seenWidgetIds = new();

    /// <summary>Mutable theme used by controls rendered after the property is set.</summary>
    public Theme Theme { get; set; } = ThemePresets.Dark();
    /// <summary>Optional platform integration for clipboard and cursor operations.</summary>
    public IUiPlatform Platform { get; set; } = NullUiPlatform.Instance;
    /// <summary>Font used for UI text. When null, Vellum uses <see cref="UiFonts.DefaultSans"/>.</summary>
    public TrueTypeFont? Font { get; set; }
    /// <summary>Default text size in logical pixels.</summary>
    public float DefaultFontSize { get; set; } = 16f;
    /// <summary>Whether LCD/subpixel text rendering may be used when the theme allows it.</summary>
    public bool Lcd { get; set; } = true;
    /// <summary>
    /// Whether each frame updates <see cref="TextRasterScale"/> from <see cref="RenderFrameInfo.MaxScale"/>.
    /// </summary>
    public bool AutoTextRasterScale { get; set; } = true;

    /// <summary>
    /// Scale used when rasterizing text atlases. Keep this at the framebuffer scale for sharp HiDPI text.
    /// </summary>
    public float TextRasterScale { get; set; } = 1f;
    /// <summary>Number of frames to retain disappeared widget state before eviction.</summary>
    public int StateRetentionFrames { get; set; } = 600;
    /// <summary>Maximum interval, in seconds, between clicks for double-click detection.</summary>
    public double DoubleClickIntervalSeconds { get; set; } = 0.35;
    /// <summary>Maximum pointer distance for double-click detection.</summary>
    public float DoubleClickDistance { get; set; } = 6f;
    /// <summary>Pointer distance required before a held pointer counts as dragging.</summary>
    public float DragStartThreshold { get; set; } = 4f;
    /// <summary>Padding around the root layout scope in logical pixels.</summary>
    public float RootPadding { get; set; } = 16f;

    // Frame input
    private float _vpW, _vpH;
    private Vector2 _mouse;
    private Vector2 _mousePrev;
    private Vector2 _mouseDelta;
    private UiInputState _input;
    private readonly bool[] _mouseButtonsDown = new bool[3];
    private readonly bool[] _mouseButtonsPressed = new bool[3];
    private readonly bool[] _mouseButtonsReleased = new bool[3];
    private readonly bool[] _mouseButtonsDoubleClicked = new bool[3];
    private readonly Vector2[] _mousePressOrigins = new Vector2[3];
    private readonly Vector2[] _mouseLastPressPositions = new Vector2[3];
    private readonly double[] _mouseLastPressTimes = new double[3];
    private bool _hasMouseFrame;

    // Layout stack (root is always at index 0)
    private readonly List<LayoutScope> _layouts = new();
    private readonly List<ClipRect> _hitClips = new();
    private readonly TextLayoutScratch _textScratch = new();
    private readonly List<Painter> _deferredPainterPool = new();
    private int _deferredPainterPoolDepth;
    private int[] _graphemeIndexScratch = new int[64];

    // Persistent interaction IDs
    private int _hotId, _activeId, _focusedId;
    private UiCursor _requestedCursor;
    private readonly Stack<int> _idStack = new();
#if DEBUG
    private readonly HashSet<int> _debugRegisteredWidgetIds = new();
    private int _idTrackingDisabledDepth;
#endif

    // Per-frame focus navigation
    private bool _tabNavigationRequested;
    private bool _tabNavigationBackward;
    private int _firstFocusableId;
    private int _lastFocusableId;
    private int _pendingFocusId;
    private int _requestedFocusId;
    private bool _sawFocusedWidget;
    private readonly List<int> _popupContext = new();
    private bool _popupDismissedThisPress;
    private int _windowContextId;
    private bool _menuMeasureOnly;
    private bool _menuMeasureIntrinsicWidth;
    private string? _tooltipText;
    private float _tooltipAnchorX;
    private float _tooltipAnchorY;
    private float _tooltipMaxWidth;
    private float _tooltipFontSize;
    private long _cpuFrameStartTimestamp;
    private int _frameIndex;
    private int _disabledDepth;
    private bool _disposed;

    /// <summary>Creates a UI context that renders through <paramref name="renderer"/>.</summary>
    public Ui(IRenderer renderer)
    {
        _renderer = renderer;
        _painter = _framePainter;
        Array.Fill(_mouseLastPressTimes, double.NegativeInfinity);
    }

    internal Painter Painter => _painter;

    // -------------------------------------------------------------------------
    // Frame lifecycle — ergonomic lambda wrapper plus explicit Begin/End
    // -------------------------------------------------------------------------

    /// <summary>Runs a complete scale-1 UI frame using simple left-button mouse input.</summary>
    public void Frame(int viewportWidth, int viewportHeight, Vector2 mousePos, bool mouseDown, Action<Ui> content)
        => Frame(new RenderFrameInfo(viewportWidth, viewportHeight), mousePos, mouseDown, content);

    /// <summary>Runs a complete UI frame using explicit frame scale and simple left-button mouse input.</summary>
    public void Frame(RenderFrameInfo frame, Vector2 mousePos, bool mouseDown, Action<Ui> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        BeginFrame(frame, mousePos, mouseDown);
        try { content(this); }
        finally { EndFrame(); }
    }

    /// <inheritdoc cref="Frame(int, int, Vector2, bool, Action{Ui})" />
    public void Frame<TState>(int viewportWidth, int viewportHeight, Vector2 mousePos, bool mouseDown, TState state, Action<Ui, TState> content)
        => Frame(new RenderFrameInfo(viewportWidth, viewportHeight), mousePos, mouseDown, state, content);

    /// <inheritdoc cref="Frame(RenderFrameInfo, Vector2, bool, Action{Ui})" />
    public void Frame<TState>(RenderFrameInfo frame, Vector2 mousePos, bool mouseDown, TState state, Action<Ui, TState> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        BeginFrame(frame, mousePos, mouseDown);
        try { content(this, state); }
        finally { EndFrame(); }
    }

    /// <summary>Runs a complete scale-1 UI frame using a full input snapshot.</summary>
    public void Frame(
        int viewportWidth,
        int viewportHeight,
        Vector2 mousePos,
        bool mouseDown,
        UiInputState input,
        Action<Ui> content)
        => Frame(new RenderFrameInfo(viewportWidth, viewportHeight), mousePos, mouseDown, input, content);

    /// <summary>Runs a complete UI frame using explicit frame scale and a full input snapshot.</summary>
    public void Frame(
        RenderFrameInfo frame,
        Vector2 mousePos,
        bool mouseDown,
        UiInputState input,
        Action<Ui> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        BeginFrame(frame, mousePos, mouseDown, input);
        try { content(this); }
        finally { EndFrame(); }
    }

    /// <inheritdoc cref="Frame(int, int, Vector2, bool, UiInputState, Action{Ui})" />
    public void Frame<TState>(
        int viewportWidth,
        int viewportHeight,
        Vector2 mousePos,
        bool mouseDown,
        UiInputState input,
        TState state,
        Action<Ui, TState> content)
        => Frame(new RenderFrameInfo(viewportWidth, viewportHeight), mousePos, mouseDown, input, state, content);

    /// <inheritdoc cref="Frame(RenderFrameInfo, Vector2, bool, UiInputState, Action{Ui})" />
    public void Frame<TState>(
        RenderFrameInfo frame,
        Vector2 mousePos,
        bool mouseDown,
        UiInputState input,
        TState state,
        Action<Ui, TState> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        BeginFrame(frame, mousePos, mouseDown, input);
        try { content(this, state); }
        finally { EndFrame(); }
    }

    /// <summary>Runs a complete scale-1 UI frame, deriving left-button state from <paramref name="input"/>.</summary>
    public void Frame(
        int viewportWidth,
        int viewportHeight,
        Vector2 mousePos,
        UiInputState input,
        Action<Ui> content)
        => Frame(new RenderFrameInfo(viewportWidth, viewportHeight), mousePos, input, content);

    /// <summary>Runs a complete UI frame, deriving left-button state from <paramref name="input"/>.</summary>
    public void Frame(
        RenderFrameInfo frame,
        Vector2 mousePos,
        UiInputState input,
        Action<Ui> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        BeginFrame(frame, mousePos, input);
        try { content(this); }
        finally { EndFrame(); }
    }

    /// <inheritdoc cref="Frame(int, int, Vector2, UiInputState, Action{Ui})" />
    public void Frame<TState>(
        int viewportWidth,
        int viewportHeight,
        Vector2 mousePos,
        UiInputState input,
        TState state,
        Action<Ui, TState> content)
        => Frame(new RenderFrameInfo(viewportWidth, viewportHeight), mousePos, input, state, content);

    /// <inheritdoc cref="Frame(RenderFrameInfo, Vector2, UiInputState, Action{Ui})" />
    public void Frame<TState>(
        RenderFrameInfo frame,
        Vector2 mousePos,
        UiInputState input,
        TState state,
        Action<Ui, TState> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        BeginFrame(frame, mousePos, input);
        try { content(this, state); }
        finally { EndFrame(); }
    }

    /// <summary>Begins a scale-1 frame using simple left-button mouse input.</summary>
    public void BeginFrame(int viewportWidth, int viewportHeight, Vector2 mousePos, bool mouseDown)
        => BeginFrame(new RenderFrameInfo(viewportWidth, viewportHeight), mousePos, mouseDown, default);

    /// <summary>Begins a frame using explicit frame scale and simple left-button mouse input.</summary>
    public void BeginFrame(RenderFrameInfo frame, Vector2 mousePos, bool mouseDown)
        => BeginFrame(frame, mousePos, mouseDown, default);

    /// <summary>Begins a scale-1 frame using a full input snapshot.</summary>
    public void BeginFrame(int viewportWidth, int viewportHeight, Vector2 mousePos, bool mouseDown, UiInputState input)
        => BeginFrame(new RenderFrameInfo(viewportWidth, viewportHeight), mousePos, mouseDown, input);

    /// <summary>Begins a frame using explicit frame scale and a full input snapshot.</summary>
    public void BeginFrame(RenderFrameInfo frame, Vector2 mousePos, bool mouseDown, UiInputState input)
    {
        ThrowIfDisposed();
        _cpuFrameStartTimestamp = Stopwatch.GetTimestamp();
        frame = frame.Normalized();

        _frameIndex++;
        _vpW = frame.LogicalWidth;
        _vpH = frame.LogicalHeight;
        if (AutoTextRasterScale)
            TextRasterScale = MathF.Max(1f, frame.MaxScale);
        _mousePrev = _mouse;
        _mouse = mousePos;
        _mouseDelta = _hasMouseFrame ? _mouse - _mousePrev : Vector2.Zero;
        _hasMouseFrame = true;
        _input = input;
        UpdateMouseButtons(mouseDown);

        _idStack.Clear();
        _disabledDepth = 0;
#if DEBUG
        _debugRegisteredWidgetIds.Clear();
        _idTrackingDisabledDepth = 0;
#endif
        _hotId = 0;
        _requestedCursor = UiCursor.Arrow;
        _tabNavigationRequested = _input.IsPressed(UiKey.Tab);
        _tabNavigationBackward = _input.Shift;
        _firstFocusableId = 0;
        _lastFocusableId = 0;
        _pendingFocusId = 0;
        _sawFocusedWidget = false;
        _hitClips.Clear();
        _seenWidgetIds.Clear();
        _textScratch.ResetForFrame();
        _deferredPainterPoolDepth = 0;
        PreparePopupFrame();
        PrepareWindowFrame();
        PrepareTooltipFrame();

        _layouts.Clear();
        _layouts.Add(new LayoutScope
        {
            OriginX = RootPadding,
            OriginY = RootPadding,
            CursorX = RootPadding,
            CursorY = RootPadding,
            Dir = LayoutDir.Vertical,
            WidthConstraint = MathF.Max(0, frame.LogicalWidth - RootPadding * 2),
            HasWidthConstraint = true,
            Empty = true
        });

        _renderer.BeginFrame(frame);
        _painter = _framePainter;
        _painter.Clear();
    }

    /// <summary>Begins a scale-1 frame, deriving left-button state from <paramref name="input"/>.</summary>
    public void BeginFrame(int viewportWidth, int viewportHeight, Vector2 mousePos, UiInputState input)
        => BeginFrame(new RenderFrameInfo(viewportWidth, viewportHeight), mousePos, input.IsMouseDown(UiMouseButton.Left), input);

    /// <summary>Begins a frame, deriving left-button state from <paramref name="input"/>.</summary>
    public void BeginFrame(RenderFrameInfo frame, Vector2 mousePos, UiInputState input)
        => BeginFrame(frame, mousePos, input.IsMouseDown(UiMouseButton.Left), input);

    /// <summary>Finishes the current frame and submits its render list to the renderer.</summary>
    public void EndFrame()
    {
        ThrowIfDisposed();
        DebugVerifyScopesClosed();

        RenderQueuedWindows();
        _hitClips.Clear();
        RenderQueuedPopups();
        RenderQueuedTooltip();
        ClearInteractionForMissingWidgets();
        ApplyPendingFocusNavigation();

        if (IsMousePressed(UiMouseButton.Left) && _hotId != _focusedId)
            _focusedId = 0;

        FinalizePopupFrame();
        FinalizeWindowFrame();
        CleanupRetainedWidgetState();
        Platform.SetCursor(_requestedCursor);
        if (!IsMouseDown(UiMouseButton.Left)) _activeId = 0;
        LastCpuFrameMs = Stopwatch.GetElapsedTime(_cpuFrameStartTimestamp).TotalMilliseconds;
        _renderer.Render(_framePainter.RenderList);
        _renderer.EndFrame();
    }

    /// <summary>Releases Vellum-owned textures and clears retained widget state.</summary>
    public void Dispose()
    {
        if (_disposed) return;

        foreach (var atlas in _atlases.Values)
            atlas.Destroy(_renderer);

        _atlases.Clear();
        _widgetStates.Clear();
        _popupRequestsByDepth.Clear();
        _openPopupIds.Clear();
        _popupRectsPrev.Clear();
        _popupRectsCurrent.Clear();
        _modalPopupIdsPrev.Clear();
        _modalPopupIdsCurrent.Clear();
        _menuPopupIds.Clear();
        _windowRuntimeStates.Clear();
        _windowRequests.Clear();
        _windowOrder.Clear();
        _seenWidgetIds.Clear();
        _layouts.Clear();
        _hitClips.Clear();
        _idStack.Clear();
#if DEBUG
        _debugRegisteredWidgetIds.Clear();
        _idTrackingDisabledDepth = 0;
#endif
        _popupContext.Clear();
        _windowContextId = 0;
        _menuMeasureOnly = false;
        _menuMeasureIntrinsicWidth = false;
        _tooltipText = null;
        _tooltipAnchorX = 0;
        _tooltipAnchorY = 0;
        _tooltipMaxWidth = 0;
        _tooltipFontSize = 0;
        Array.Clear(_mouseButtonsDown);
        Array.Clear(_mouseButtonsPressed);
        Array.Clear(_mouseButtonsReleased);
        Array.Clear(_mouseButtonsDoubleClicked);
        _hotId = 0;
        _activeId = 0;
        _focusedId = 0;
        _firstFocusableId = 0;
        _lastFocusableId = 0;
        _pendingFocusId = 0;
        _requestedFocusId = 0;
        _sawFocusedWidget = false;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    // -------------------------------------------------------------------------
    // Layout scopes
    // -------------------------------------------------------------------------

    /// <summary>Remaining width available in the current layout scope.</summary>
    public float AvailableWidth => _layouts.Count > 0 ? GetAvailableWidth(Top) : 0;

    /// <summary>Opens a row layout scope for the callback.</summary>
    public void Row(Action<Ui> content) => Scope(LayoutDir.Horizontal, null, UiAlign.Start, reserveWidth: false, content);
    /// <summary>Opens a column layout scope for the callback.</summary>
    public void Column(Action<Ui> content) => Scope(LayoutDir.Vertical, null, UiAlign.Start, reserveWidth: false, content);

    /// <summary>Opens a fixed-width vertical layout scope for the callback.</summary>
    public void FixedWidth(float width, Action<Ui> content, UiAlign align = UiAlign.Start)
        => Scope(LayoutDir.Vertical, width, align, reserveWidth: true, content);

    /// <summary>Opens a vertical layout scope clamped to the current available width.</summary>
    public void MaxWidth(float maxWidth, Action<Ui> content, UiAlign align = UiAlign.Start)
        => Scope(LayoutDir.Vertical, MathF.Min(MathF.Max(0, maxWidth), AvailableWidth), align, reserveWidth: true, content);

    /// <summary>Opens a row layout scope that is closed when the returned handle is disposed.</summary>
    public LayoutScopeHandle Row() => OpenScope(LayoutDir.Horizontal, null, UiAlign.Start, reserveWidth: false);
    /// <summary>Opens a column layout scope that is closed when the returned handle is disposed.</summary>
    public LayoutScopeHandle Column() => OpenScope(LayoutDir.Vertical, null, UiAlign.Start, reserveWidth: false);
    /// <summary>Opens a fixed-width layout scope that is closed when the returned handle is disposed.</summary>
    public LayoutScopeHandle FixedWidth(float width, UiAlign align = UiAlign.Start)
        => OpenScope(LayoutDir.Vertical, width, align, reserveWidth: true);
    /// <summary>Opens a max-width layout scope that is closed when the returned handle is disposed.</summary>
    public LayoutScopeHandle MaxWidth(float maxWidth, UiAlign align = UiAlign.Start)
        => OpenScope(LayoutDir.Vertical, MathF.Min(MathF.Max(0, maxWidth), AvailableWidth), align, reserveWidth: true);

    private LayoutScopeHandle OpenScope(LayoutDir dir, float? width, UiAlign align, bool reserveWidth)
    {
        BeginScope(dir, width, align, reserveWidth);
        return new LayoutScopeHandle(this);
    }

    private void Scope(LayoutDir dir, float? width, UiAlign align, bool reserveWidth, Action<Ui> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        BeginScope(dir, width, align, reserveWidth);
        try { content(this); }
        finally { EndScope(); }
    }

    private void BeginScope(LayoutDir dir, float? width, UiAlign align, bool reserveWidth)
    {
        float parentAvailableWidth = GetAvailableWidth(Top);
        float scopeWidth = width.HasValue
            ? Math.Clamp(width.Value, 0, parentAvailableWidth)
            : parentAvailableWidth;

        var (baseX, baseY) = Place(0, 0);
        float alignOffsetX = align switch
        {
            UiAlign.Center => MathF.Max(0, (parentAvailableWidth - scopeWidth) * 0.5f),
            UiAlign.End => MathF.Max(0, parentAvailableWidth - scopeWidth),
            _ => 0
        };
        float originX = baseX + alignOffsetX;
        float originY = baseY;
        _layouts.Add(new LayoutScope
        {
            OriginX = originX,
            OriginY = originY,
            CursorX = originX,
            CursorY = originY,
            Dir = dir,
            WidthConstraint = scopeWidth,
            HasWidthConstraint = true,
            Empty = true,
            AlignOffsetX = alignOffsetX,
            ReserveWidth = reserveWidth
        });
    }

    private void EndScope()
    {
        var inner = _layouts[^1];
        _layouts.RemoveAt(_layouts.Count - 1);

        float innerW = inner.Dir == LayoutDir.Horizontal
            ? inner.CursorX - inner.OriginX
            : inner.MaxExtent;
        float innerH = inner.Dir == LayoutDir.Horizontal
            ? inner.MaxExtent
            : inner.CursorY - inner.OriginY;

        float totalW = inner.ReserveWidth
            ? inner.AlignOffsetX + MathF.Max(inner.WidthConstraint, innerW)
            : inner.AlignOffsetX + innerW;
        Advance(totalW, innerH);
    }

    /// <summary>
    /// Disposable handle returned by explicit layout scope methods.
    /// </summary>
    public ref struct LayoutScopeHandle
    {
        private Ui? _ui;

        internal LayoutScopeHandle(Ui ui) => _ui = ui;

        /// <summary>Closes the layout scope.</summary>
        public void Dispose()
        {
            var ui = _ui;
            if (ui is null) return;
            _ui = null;
            ui.EndScope();
        }
    }

    private ref LayoutScope Top => ref CollectionsMarshal.AsSpan(_layouts)[^1];

    internal Painter AcquireDeferredPainter()
    {
        if (_deferredPainterPoolDepth >= _deferredPainterPool.Count)
            _deferredPainterPool.Add(new Painter());
        var p = _deferredPainterPool[_deferredPainterPoolDepth++];
        _painter.CopyClipStackTo(p);
        return p;
    }

    internal void ReleaseDeferredPainter(Painter p)
    {
        p.Clear();
        _deferredPainterPoolDepth--;
    }

    private float GetLeadingGap(in LayoutScope scope)
    {
        if (scope.Empty) return 0;
        if (scope.HasPendingGap) return scope.PendingGap;
        return scope.HasDefaultGap ? scope.DefaultGap : Theme.Gap;
    }

    /// <summary>Sets default item spacing for the current layout scope.</summary>
    public void ItemSpacing(float pixels)
    {
        ref var s = ref Top;
        s.DefaultGap = MathF.Max(0, pixels);
        s.HasDefaultGap = true;
    }

    /// <summary>Whether the current scope is disabled.</summary>
    public bool IsScopeDisabled => _disabledDepth > 0;

    private bool ResolveEnabled(bool enabled) => enabled && _disabledDepth == 0;

    private void EnterDisabledScope(bool disabled)
    {
        if (disabled)
            _disabledDepth++;
    }

    private void ExitDisabledScope(bool disabled)
    {
        if (disabled)
            _disabledDepth--;
    }

    /// <summary>Runs a disabled scope until disposed.</summary>
    public DisabledScopeHandle Disabled() => Disabled(disabled: true);

    /// <summary>Runs a disabled scope until disposed when <paramref name="disabled"/> is true.</summary>
    public DisabledScopeHandle Disabled(bool disabled)
    {
        EnterDisabledScope(disabled);
        return new DisabledScopeHandle(this, disabled);
    }

    /// <summary>Runs content in a disabled scope.</summary>
    public void Disabled(Action<Ui> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using (Disabled())
            content(this);
    }

    /// <inheritdoc cref="Disabled(Action{Ui})" />
    public void Disabled<TState>(TState state, Action<Ui, TState> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using (Disabled())
            content(this, state);
    }

    /// <summary>Runs content in a disabled scope when <paramref name="disabled"/> is true.</summary>
    public void Disabled(bool disabled, Action<Ui> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using (Disabled(disabled))
            content(this);
    }

    /// <inheritdoc cref="Disabled(bool, Action{Ui})" />
    public void Disabled<TState>(bool disabled, TState state, Action<Ui, TState> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using (Disabled(disabled))
            content(this, state);
    }

    /// <summary>Disposable handle returned by explicit disabled scope methods.</summary>
    public ref struct DisabledScopeHandle
    {
        private Ui? _ui;
        private readonly bool _active;

        internal DisabledScopeHandle(Ui ui, bool active)
        {
            _ui = ui;
            _active = active;
        }

        /// <summary>Closes the disabled scope.</summary>
        public void Dispose()
        {
            Ui? ui = _ui;
            if (ui == null)
                return;

            _ui = null;
            ui.ExitDisabledScope(_active);
        }
    }

    private float GetAvailableWidth(in LayoutScope scope)
    {
        float width = scope.HasWidthConstraint
            ? scope.WidthConstraint
            : MathF.Max(0, _vpW - scope.OriginX - RootPadding);

        if (scope.Dir == LayoutDir.Horizontal)
        {
            float gap = GetLeadingGap(scope);
            width -= scope.CursorX - scope.OriginX + gap;
        }

        return MathF.Max(0, width);
    }

    private (float x, float y) Place(float w, float h)
    {
        ref var s = ref Top;
        float gap = GetLeadingGap(s);
        return s.Dir == LayoutDir.Horizontal
            ? (s.CursorX + gap, s.CursorY)
            : (s.CursorX, s.CursorY + gap);
    }

    private void Advance(float w, float h)
    {
        ref var s = ref Top;
        float gap = GetLeadingGap(s);
        s.Empty = false;
        s.PendingGap = 0;
        s.HasPendingGap = false;

        if (s.Dir == LayoutDir.Horizontal)
        {
            s.CursorX += gap + w;
            if (h > s.MaxExtent) s.MaxExtent = h;
        }
        else
        {
            s.CursorY += gap + h;
            if (w > s.MaxExtent) s.MaxExtent = w;
        }
    }

    /// <summary>Adds spacing before the next widget in the current layout scope.</summary>
    public void Spacing(float pixels)
    {
        ref var s = ref Top;
        if (s.Empty)
        {
            if (s.Dir == LayoutDir.Horizontal) s.CursorX += pixels;
            else s.CursorY += pixels;
            return;
        }

        if (s.HasPendingGap)
            s.PendingGap += pixels;
        else
        {
            s.PendingGap = pixels;
            s.HasPendingGap = true;
        }
    }

    // -------------------------------------------------------------------------
    // ID generation
    // -------------------------------------------------------------------------

    private int CurrentIdSeed => _idStack.Count > 0 ? _idStack.Peek() : unchecked((int)0x9e3779b1);

    private int MakeId(ReadOnlySpan<char> label) => HashMix(CurrentIdSeed, UiId.HashString(label));

    private int MakeId(UiId id) => HashMix(CurrentIdSeed, id.Hash);

    private static bool HasSpecifiedId(UiId? id) => id.HasValue && id.Value.IsSpecified;

    private static UiId RequireSpecifiedId(UiId id, string parameterName)
    {
        if (!id.IsSpecified)
            throw new ArgumentException("A non-default UiId is required.", parameterName);

        return id;
    }

    private static UiId ResolveWidgetId(UiId? id, string fallback)
        => HasSpecifiedId(id) ? id!.Value : UiId.FromString(fallback);

    private int MakeWidgetId(UiWidgetKind kind, ReadOnlySpan<char> label)
        => HashMix(HashMix(CurrentIdSeed, UiId.HashInt((int)kind)), UiId.HashString(label));

    private int MakeWidgetId(UiWidgetKind kind, UiId id)
        => HashMix(HashMix(CurrentIdSeed, UiId.HashInt((int)kind)), id.Hash);

    private int MakePopupId(ReadOnlySpan<char> id) => MakeWidgetId(UiWidgetKind.Popup, id);

    private int MakePopupId(UiId id) => MakeWidgetId(UiWidgetKind.Popup, RequireSpecifiedId(id, nameof(id)));

    private int MakeId(int value) => HashMix(CurrentIdSeed, UiId.HashInt(value));

    private int MakeId(long value) => HashMix(CurrentIdSeed, UiId.HashLong(value));

    private int MakeId(ulong value) => HashMix(CurrentIdSeed, UiId.HashLong(unchecked((long)value)));

    private int MakeId(Guid value) => HashMix(CurrentIdSeed, UiId.HashGuid(value));

    private static int MakeChildId(int parentId, ReadOnlySpan<char> child) => HashMix(parentId, UiId.HashString(child));

    private static int HashMix(int a, int b) => (int)((uint)a * 2654435761u ^ (uint)b);

    private void EnterIdScope(ReadOnlySpan<char> name) => _idStack.Push(MakeId(name));

    private void EnterIdScope(UiId id) => _idStack.Push(MakeId(id));

    private void EnterIdScope(int value) => _idStack.Push(MakeId(value));

    private void EnterIdScope(long value) => _idStack.Push(MakeId(value));

    private void EnterIdScope(ulong value) => _idStack.Push(MakeId(value));

    private void EnterIdScope(Guid value) => _idStack.Push(MakeId(value));

    private void ExitIdScope() => _idStack.Pop();

    /// <summary>Runs a nested id scope until disposed.</summary>
    public IdScopeHandle Id(string name) => Id(name.AsSpan());

    /// <summary>Runs a nested id scope until disposed.</summary>
    public IdScopeHandle Id(ReadOnlySpan<char> name)
    {
        EnterIdScope(name);
        return new IdScopeHandle(this);
    }

    /// <summary>Runs content inside a nested id scope.</summary>
    public void Id(string name, Action<Ui> content) => Id(name.AsSpan(), content);

    /// <summary>Runs content inside a nested id scope.</summary>
    public void Id(ReadOnlySpan<char> name, Action<Ui> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using (Id(name))
            content(this);
    }

    /// <summary>Runs content inside a nested id scope.</summary>
    public void Id<TState>(string name, TState state, Action<Ui, TState> content) => Id(name.AsSpan(), state, content);

    /// <summary>Runs content inside a nested id scope.</summary>
    public void Id<TState>(ReadOnlySpan<char> name, TState state, Action<Ui, TState> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using (Id(name))
            content(this, state);
    }

    /// <summary>Runs a nested id scope until disposed.</summary>
    public IdScopeHandle Id(int value)
    {
        EnterIdScope(value);
        return new IdScopeHandle(this);
    }

    /// <summary>Runs content inside a nested id scope.</summary>
    public void Id(int value, Action<Ui> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using (Id(value))
            content(this);
    }

    /// <summary>Runs content inside a nested id scope.</summary>
    public void Id<TState>(int value, TState state, Action<Ui, TState> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using (Id(value))
            content(this, state);
    }

    /// <summary>Runs a nested id scope until disposed.</summary>
    public IdScopeHandle Id(long value)
    {
        EnterIdScope(value);
        return new IdScopeHandle(this);
    }

    /// <summary>Runs content inside a nested id scope.</summary>
    public void Id(long value, Action<Ui> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using (Id(value))
            content(this);
    }

    /// <summary>Runs content inside a nested id scope.</summary>
    public void Id<TState>(long value, TState state, Action<Ui, TState> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using (Id(value))
            content(this, state);
    }

    /// <summary>Runs a nested id scope until disposed.</summary>
    public IdScopeHandle Id(ulong value)
    {
        EnterIdScope(value);
        return new IdScopeHandle(this);
    }

    /// <summary>Runs content inside a nested id scope.</summary>
    public void Id(ulong value, Action<Ui> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using (Id(value))
            content(this);
    }

    /// <summary>Runs content inside a nested id scope.</summary>
    public void Id<TState>(ulong value, TState state, Action<Ui, TState> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using (Id(value))
            content(this, state);
    }

    /// <summary>Runs a nested id scope until disposed.</summary>
    public IdScopeHandle Id(Guid value)
    {
        EnterIdScope(value);
        return new IdScopeHandle(this);
    }

    /// <summary>Runs content inside a nested id scope.</summary>
    public void Id(Guid value, Action<Ui> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using (Id(value))
            content(this);
    }

    /// <summary>Runs content inside a nested id scope.</summary>
    public void Id<TState>(Guid value, TState state, Action<Ui, TState> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        using (Id(value))
            content(this, state);
    }

    /// <summary>Disposable handle returned by explicit id scope methods.</summary>
    public ref struct IdScopeHandle
    {
        private Ui? _ui;

        internal IdScopeHandle(Ui ui) => _ui = ui;

        /// <summary>Closes the id scope.</summary>
        public void Dispose()
        {
            Ui? ui = _ui;
            if (ui == null)
                return;

            _ui = null;
            ui.ExitIdScope();
        }
    }

    /// <summary>Requests keyboard focus for the widget identified by <paramref name="kind"/> and <paramref name="id"/>.</summary>
    /// <remarks>
    /// <paramref name="id"/> is the widget's resolved identifier — its label by default, or the
    /// value passed via the widget's <c>id:</c> parameter when one was supplied. The argument
    /// converts implicitly from <see cref="string"/>, <see cref="int"/>, <see cref="long"/>,
    /// <see cref="ulong"/>, and <see cref="System.Guid"/>.
    /// </remarks>
    public void RequestFocus(UiWidgetKind kind, UiId id)
    {
        _focusedId = 0;
        _pendingFocusId = 0;
        _sawFocusedWidget = false;
        _requestedFocusId = id.IsSpecified ? MakeWidgetId(kind, id) : 0;
    }

    /// <summary>Clears keyboard focus.</summary>
    public void ClearFocus()
    {
        _focusedId = 0;
        _pendingFocusId = 0;
        _requestedFocusId = 0;
        _sawFocusedWidget = false;
    }

    // -------------------------------------------------------------------------
    // Persistent widget state and focus
    // -------------------------------------------------------------------------

    private TState GetState<TState>(int id) where TState : class, new()
    {
        MarkWidgetSeen(id);

        if (_widgetStates.TryGetValue(id, out var entry))
        {
            entry.LastSeenFrame = _frameIndex;
            if (entry.State is TState typed) return typed;
            throw new InvalidOperationException($"Widget id {id} is already associated with {entry.State.GetType().Name}, not {typeof(TState).Name}.");
        }

        var created = new TState();
        _widgetStates[id] = new WidgetStateEntry { State = created, LastSeenFrame = _frameIndex };
        return created;
    }

    private bool RegisterFocusable(int id, bool enabled)
    {
        RegisterWidgetId(id);
        MarkWidgetSeen(id);

        if (!enabled)
        {
            if (_focusedId == id) ClearFocus(id);
            return false;
        }

        if (!CanFocusCurrentContext())
            return false;

        if (_requestedFocusId == id)
        {
            SetFocus(id);
            _requestedFocusId = 0;
            _pendingFocusId = 0;
            _tabNavigationRequested = false;
        }

        if (_firstFocusableId == 0)
            _firstFocusableId = id;
        int previousFocusableId = _lastFocusableId;
        _lastFocusableId = id;

        if (_tabNavigationRequested && _pendingFocusId == 0)
        {
            if (_focusedId == 0)
            {
                if (!_tabNavigationBackward)
                    _pendingFocusId = id;
            }
            else if (_tabNavigationBackward)
            {
                if (id == _focusedId)
                    _pendingFocusId = previousFocusableId != 0 ? previousFocusableId : -1;
            }
            else if (_sawFocusedWidget)
            {
                _pendingFocusId = id;
            }
        }

        if (_focusedId == id)
            _sawFocusedWidget = true;
        return _focusedId == id;
    }

    private void ApplyPendingFocusNavigation()
    {
        if (!_tabNavigationRequested) return;

        if (_firstFocusableId == 0)
        {
            ClearFocus();
            return;
        }

        if (_focusedId == 0)
        {
            SetFocus(_tabNavigationBackward ? _lastFocusableId : _firstFocusableId);
            return;
        }

        if (_pendingFocusId == -1)
        {
            SetFocus(_lastFocusableId);
            return;
        }

        if (_pendingFocusId != 0)
        {
            SetFocus(_pendingFocusId);
            return;
        }

        SetFocus(_tabNavigationBackward ? _lastFocusableId : _firstFocusableId);
    }

    private void SetFocus(int id) => _focusedId = id;

    private void ClearFocus(int id)
    {
        if (_focusedId == id)
            _focusedId = 0;
    }

    private void UpdateMouseButtons(bool legacyPrimaryDown)
    {
        for (int i = 0; i < _mouseButtonsPressed.Length; i++)
        {
            _mouseButtonsPressed[i] = false;
            _mouseButtonsReleased[i] = false;
            _mouseButtonsDoubleClicked[i] = false;
        }

        for (int i = 0; i < _mouseButtonsDown.Length; i++)
        {
            var button = (UiMouseButton)i;
            bool wasDown = _mouseButtonsDown[i];
            bool isDown = _input.DownMouseButtons != null
                ? _input.IsMouseDown(button)
                : button == UiMouseButton.Left && legacyPrimaryDown;

            _mouseButtonsPressed[i] = isDown && !wasDown;
            _mouseButtonsReleased[i] = !isDown && wasDown;
            _mouseButtonsDown[i] = isDown;

            if (_mouseButtonsPressed[i])
            {
                _mousePressOrigins[i] = _mouse;

                if (_input.TimeSeconds.HasValue &&
                    _mouseLastPressTimes[i] > double.NegativeInfinity &&
                    _input.TimeSeconds.Value - _mouseLastPressTimes[i] <= DoubleClickIntervalSeconds &&
                    Vector2.DistanceSquared(_mouse, _mouseLastPressPositions[i]) <= DoubleClickDistance * DoubleClickDistance)
                {
                    _mouseButtonsDoubleClicked[i] = true;
                }

                _mouseLastPressTimes[i] = _input.TimeSeconds ?? double.NegativeInfinity;
                _mouseLastPressPositions[i] = _mouse;
            }
        }
    }

    private static int MouseButtonIndex(UiMouseButton button) => (int)button;

    private bool IsAnyMousePressed()
    {
        for (int i = 0; i < _mouseButtonsPressed.Length; i++)
        {
            if (_mouseButtonsPressed[i]) return true;
        }

        return false;
    }

    private void MarkWidgetSeen(int id)
    {
        if (id != 0)
            _seenWidgetIds.Add(id);
    }

    [Conditional("DEBUG")]
    private void DebugVerifyScopesClosed()
    {
#if DEBUG
        if (_layouts.Count > 1)
            throw new InvalidOperationException(
                $"Vellum scope leak: {_layouts.Count - 1} unclosed Row/Column/FixedWidth/MaxWidth handle(s). " +
                "Wrap layout handles in 'using (...)'.");
        if (_idStack.Count > 0)
            throw new InvalidOperationException(
                $"Vellum scope leak: {_idStack.Count} unclosed Id(...) handle(s). " +
                "Wrap Id handles in 'using (...)'.");
        if (_disabledDepth > 0)
            throw new InvalidOperationException(
                $"Vellum scope leak: {_disabledDepth} unclosed Disabled(...) handle(s). " +
                "Wrap Disabled handles in 'using (...)'.");
#endif
    }

    [Conditional("DEBUG")]
    private void RegisterWidgetId(int id, string? message = null)
    {
#if DEBUG
        if (id == 0 || _idTrackingDisabledDepth > 0)
            return;

        if (_debugRegisteredWidgetIds.Add(id))
            return;

        string suffix = string.IsNullOrWhiteSpace(message) ? string.Empty : $" ({message})";
        throw new InvalidOperationException(
            $"Duplicate Vellum widget id {id}{suffix}. " +
            "Wrap repeated data in ui.Id(...), or pass a named id: value when same-label widgets share a scope.");
#endif
    }

    private void ClearInteractionForMissingWidgets()
    {
        if (_focusedId != 0 && !_seenWidgetIds.Contains(_focusedId))
            _focusedId = 0;
        if (_activeId != 0 && !_seenWidgetIds.Contains(_activeId))
            _activeId = 0;
    }

    private void CleanupRetainedWidgetState()
    {
        if (StateRetentionFrames < 0 || _widgetStates.Count == 0)
            return;

        List<int>? staleIds = null;
        foreach (var pair in _widgetStates)
        {
            if (_frameIndex - pair.Value.LastSeenFrame > StateRetentionFrames)
            {
                staleIds ??= new List<int>();
                staleIds.Add(pair.Key);
            }
        }

        if (staleIds == null) return;

        foreach (int staleId in staleIds)
        {
            _widgetStates.Remove(staleId);
            if (_focusedId == staleId) _focusedId = 0;
            if (_activeId == staleId) _activeId = 0;

            int popupIndex = _openPopupIds.IndexOf(staleId);
            if (popupIndex >= 0)
                _openPopupIds.RemoveRange(popupIndex, _openPopupIds.Count - popupIndex);

            _popupRectsPrev.Remove(staleId);
            _popupRectsCurrent.Remove(staleId);
            _modalPopupIdsPrev.Remove(staleId);
            _modalPopupIdsCurrent.Remove(staleId);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Ui));
    }

    private bool CanFocusCurrentContext()
    {
        if (_popupDismissedThisPress) return false;

        if (_openPopupIds.Count == 0)
            return _popupContext.Count == 0;

        if (_popupContext.Count != _openPopupIds.Count)
            return false;

        for (int i = 0; i < _popupContext.Count; i++)
        {
            if (_popupContext[i] != _openPopupIds[i])
                return false;
        }

        return true;
    }

    private void PushHitClip(float x, float y, float w, float h)
    {
        var next = new ClipRect(x, y, MathF.Max(0, w), MathF.Max(0, h));
        if (_hitClips.Count > 0)
        {
            var parent = _hitClips[^1];
            float ix = MathF.Max(parent.X, next.X);
            float iy = MathF.Max(parent.Y, next.Y);
            float ix2 = MathF.Min(parent.X + parent.W, next.X + next.W);
            float iy2 = MathF.Min(parent.Y + parent.H, next.Y + next.H);
            next = new ClipRect(ix, iy, MathF.Max(0, ix2 - ix), MathF.Max(0, iy2 - iy));
        }

        _hitClips.Add(next);
    }

    private void PopHitClip()
    {
        if (_hitClips.Count > 0)
            _hitClips.RemoveAt(_hitClips.Count - 1);
    }

    private bool MouseInHitClip()
    {
        if (_hitClips.Count == 0) return true;
        var clip = _hitClips[^1];
        return clip.W > 0 &&
               clip.H > 0 &&
               _mouse.X >= clip.X &&
               _mouse.X < clip.X + clip.W &&
               _mouse.Y >= clip.Y &&
               _mouse.Y < clip.Y + clip.H;
    }

    private static bool PointInRect(in ClipRect rect, Vector2 point)
        => rect.W > 0 &&
           rect.H > 0 &&
           point.X >= rect.X &&
           point.X < rect.X + rect.W &&
           point.Y >= rect.Y &&
           point.Y < rect.Y + rect.H;

    // -------------------------------------------------------------------------
    // Text
    // -------------------------------------------------------------------------

    private TrueTypeFont ResolvedFont => Font ?? UiFonts.DefaultSans;

    private GlyphAtlas GetAtlas(float size)
    {
        var font = ResolvedFont;
        bool useLcd = Lcd && Theme.UseLcdText;
        float rasterScale = MathF.Max(1f, TextRasterScale);
        var key = (font, size, rasterScale, useLcd);
        if (!_atlases.TryGetValue(key, out var atlas))
        {
            atlas = new GlyphAtlas(font, size, rasterScale, useLcd);
            atlas.Build(_renderer, Enumerable.Range(32, 95));
            _atlases[key] = atlas;
        }

        return atlas;
    }

    private TextLayoutResult LayoutText(
        string text,
        float size,
        float? maxWidth = null,
        TextWrapMode wrap = TextWrapMode.NoWrap,
        TextOverflowMode overflow = TextOverflowMode.Visible,
        int maxLines = int.MaxValue)
    {
        var atlas = GetAtlas(size);
        atlas.EnsureGlyphsForText(_renderer, text);
        string ellipsisText = GetEllipsisText();
        if (overflow == TextOverflowMode.Ellipsis)
            atlas.EnsureGlyphsForText(_renderer, ellipsisText);

        return TextLayout.Layout(_textScratch, text, atlas, maxWidth, wrap, overflow, maxLines, ellipsisText);
    }

    private TextLineMetrics MeasureTextLine(string text, float size)
    {
        var atlas = GetAtlas(size);
        atlas.EnsureGlyphsForText(_renderer, text);
        return TextLayout.MeasureSingleLine(_textScratch, text, atlas);
    }

    private string GetEllipsisText()
    {
        if (ResolvedFont.FindGlyphIndex(0x2026) != 0) return "\u2026";
        return "...";
    }

    private float SnapToDevicePixel(float value)
    {
        float scale = MathF.Max(1f, TextRasterScale);
        return MathF.Floor(value * scale + 0.5f) / scale;
    }

    private void DrawTextLayout(TextLayoutResult layout, float x, float y, Color color)
    {
        if (layout.ClipWidth.HasValue)
            _painter.PushClip(SnapToDevicePixel(x), SnapToDevicePixel(y), layout.ClipWidth.Value, layout.Height);

        foreach (var placement in layout.Glyphs)
        {
            if (layout.Atlas.TryGetGlyph(placement.Codepoint, out var glyph) && glyph.Width > 0)
            {
                float glyphX = SnapToDevicePixel(x + placement.X);
                float glyphY = SnapToDevicePixel(y + placement.Y);
                _painter.AddTexturedQuad(
                    glyphX,
                    glyphY,
                    glyph.Width,
                    glyph.Height,
                    layout.Atlas.TextureId,
                    glyph.U0,
                    glyph.V0,
                    glyph.U1,
                    glyph.V1,
                    color,
                    layout.Atlas.IsLcd);
            }
        }

        if (layout.ClipWidth.HasValue)
            _painter.PopClip();
    }

    private void RequestCursor(UiCursor cursor)
        => _requestedCursor = cursor;

    // -------------------------------------------------------------------------
    // Hit test
    // -------------------------------------------------------------------------

    private bool PointIn(float x, float y, float w, float h)
        => _mouse.X >= x && _mouse.X < x + w &&
           _mouse.Y >= y && _mouse.Y < y + h &&
           MouseInHitClip() &&
           CanHitCurrentContext();

    private bool CanHitCurrentContext()
    {
        if (_popupContext.Count == 0)
        {
            if (_openPopupIds.Count != 0 || _popupDismissedThisPress)
                return false;

            int topHitWindowId = GetTopHitWindowId();
            if (_windowContextId == 0)
                return topHitWindowId == 0;

            return topHitWindowId == 0 || topHitWindowId == _windowContextId;
        }

        if (_popupContext.Count > _openPopupIds.Count)
            return false;

        for (int i = 0; i < _popupContext.Count; i++)
        {
            if (_popupContext[i] != _openPopupIds[i])
                return false;
        }

        return GetDeepestHitPopupDepth() == _popupContext.Count;
    }

    // -------------------------------------------------------------------------

    /// <summary>Internal id of the hovered widget, or 0 when none.</summary>
    public int HotId => _hotId;
    /// <summary>Internal id of the active pointer/keyboard widget, or 0 when none.</summary>
    public int ActiveId => _activeId;
    /// <summary>Internal id of the focused widget, or 0 when none.</summary>
    public int FocusedId => _focusedId;
    /// <summary>Whether the UI wants to capture mouse input from the host.</summary>
    public bool WantsCaptureMouse =>
        _openPopupIds.Count != 0 ||
        _activeId != 0 ||
        _hotId != 0 ||
        GetDeepestHitPopupDepth() != 0 ||
        GetTopHitWindowId() != 0;
    /// <summary>Whether the UI wants to capture keyboard input from the host.</summary>
    public bool WantsCaptureKeyboard => _openPopupIds.Count != 0 || _focusedId != 0;
    /// <summary>CPU time spent building the previous frame, in milliseconds.</summary>
    public double LastCpuFrameMs { get; private set; }
    /// <summary>Current mouse position in logical pixels.</summary>
    public Vector2 MousePosition => _mouse;
    /// <summary>Mouse movement delta in logical pixels.</summary>
    public Vector2 MouseDelta => _mouseDelta;
    /// <summary>Mouse wheel delta from the current input snapshot.</summary>
    public Vector2 WheelDelta => _input.WheelDelta;

    internal bool HitTestAbsolute(float x, float y, float width, float height)
        => PointIn(x, y, width, height);

    internal void DrawCanvasText(
        string text,
        float x,
        float y,
        float? size = null,
        Color? color = null,
        float? maxWidth = null,
        TextWrapMode wrap = TextWrapMode.NoWrap,
        TextOverflowMode overflow = TextOverflowMode.Visible,
        int maxLines = int.MaxValue)
    {
        float resolvedSize = size ?? DefaultFontSize;
        Color resolvedColor = color ?? Theme.TextPrimary;
        var layout = LayoutText(text, resolvedSize, maxWidth, wrap, overflow, maxLines);
        DrawTextLayout(layout, x, y, resolvedColor);
    }

    internal Vector2 MeasureCanvasText(
        string text,
        float? size = null,
        float? maxWidth = null,
        TextWrapMode wrap = TextWrapMode.NoWrap,
        TextOverflowMode overflow = TextOverflowMode.Visible,
        int maxLines = int.MaxValue)
    {
        float resolvedSize = size ?? DefaultFontSize;
        var layout = LayoutText(text, resolvedSize, maxWidth, wrap, overflow, maxLines);
        return new Vector2(layout.Width, layout.Height);
    }

    /// <summary>Returns whether a mouse button is currently held.</summary>
    public bool IsMouseDown(UiMouseButton button) => _mouseButtonsDown[MouseButtonIndex(button)];

    /// <summary>Returns whether a mouse button was pressed this frame.</summary>
    public bool IsMousePressed(UiMouseButton button) => _mouseButtonsPressed[MouseButtonIndex(button)];

    /// <summary>Returns whether a mouse button was released this frame.</summary>
    public bool IsMouseReleased(UiMouseButton button) => _mouseButtonsReleased[MouseButtonIndex(button)];

    /// <summary>Returns whether a mouse button was double-clicked this frame.</summary>
    public bool IsMouseDoubleClicked(UiMouseButton button) => _mouseButtonsDoubleClicked[MouseButtonIndex(button)];

    /// <summary>Gets the position where a current or just-released drag started.</summary>
    public bool TryGetDragStart(UiMouseButton button, out Vector2 origin)
    {
        int index = MouseButtonIndex(button);
        if (_mouseButtonsDown[index] || _mouseButtonsReleased[index])
        {
            origin = _mousePressOrigins[index];
            return true;
        }

        origin = default;
        return false;
    }

    /// <summary>Returns drag delta from the press origin for a mouse button.</summary>
    public Vector2 GetDragDelta(UiMouseButton button)
        => TryGetDragStart(button, out var origin) ? _mouse - origin : Vector2.Zero;

    /// <summary>Returns whether a mouse button has moved past the drag threshold.</summary>
    public bool IsDragging(UiMouseButton button)
    {
        int index = MouseButtonIndex(button);
        return _mouseButtonsDown[index] &&
               Vector2.DistanceSquared(_mouse, _mousePressOrigins[index]) >= DragStartThreshold * DragStartThreshold;
    }

    /// <summary>Fills the whole current viewport.</summary>
    public void FillViewport(Color color, float radius = 0f)
        => _framePainter.FillRect(0, 0, _vpW, _vpH, color, radius);
}
