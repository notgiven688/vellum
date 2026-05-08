using System.ComponentModel.Design.Serialization;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using Vellum;
using Vellum.Rendering;
using Raylib_cs;

using RlMouseButton = Raylib_cs.MouseButton;

namespace Vellum.Web;

public partial class Application
{
    private const int InitialWindowWidth = 1280;
    private const int InitialWindowHeight = 820;
    private const float TargetFrameBudgetMs = 1000f / 60f;
    private static readonly TableColumn[] s_metricsColumns =
    [
        new("Metric"),
        new("Value", 92f, UiAlign.End)
    ];
    private static RaylibRenderer? s_renderer;
    private static Ui? s_ui;
    private static int s_checkerTexture;
    private static DemoState s_state = new();
    private static bool s_initialized;
    private static System.Numerics.Vector2 s_browserMousePosition;
    private static int s_browserMouseButtons;
    private static float s_browserWheelY;
    private static RenderFrameInfo s_browserFrame = new(InitialWindowWidth, InitialWindowHeight);
    private static double s_previousBrowserFrameTimestampMs = double.NaN;

    public static void Main(string[] args)
    {
        if (args.Contains("--bench"))
        {
            RunHeadlessBench();
            return;
        }

        Initialize();

        if (OperatingSystem.IsBrowser())
            return;

        while (!Raylib.WindowShouldClose())
            UpdateFrame();

        Shutdown();
    }

    private static void Initialize()
    {
        if (s_initialized)
            return;

        if (!OperatingSystem.IsBrowser())
            Raylib.SetTargetFPS(60);

        ConfigFlags flags = ConfigFlags.ResizableWindow;
        if (!OperatingSystem.IsBrowser())
            flags |= ConfigFlags.Msaa4xHint;
        Raylib.SetConfigFlags(flags);
        Raylib.InitWindow(InitialWindowWidth, InitialWindowHeight, "Vellum Web");

        s_renderer = new RaylibRenderer();
        s_checkerTexture = s_renderer.CreateTexture(CreateCheckerRgba(), 16, 16);
        s_ui = new Ui(s_renderer)
        {
            DefaultFontSize = 18f,
            Lcd = !OperatingSystem.IsBrowser(),
            Platform = new RaylibUiPlatform()
        };
        s_state = new DemoState();
        s_initialized = true;
    }

    [JSExport]
    public static void UpdateFrame(double browserFrameTimestampMs = double.NaN)
    {
        if (s_ui is null || s_renderer is null)
            return;

        DemoState state = s_state;
        Ui ui = s_ui;

        ui.Theme = state.ResolveTheme();
        state.UiCpuTimeMs = (float)ui.LastCpuFrameMs;
        state.SmoothedUiCpuTimeMs = state.SmoothedUiCpuTimeMs <= 0f
            ? state.UiCpuTimeMs
            : state.SmoothedUiCpuTimeMs + (state.UiCpuTimeMs - state.SmoothedUiCpuTimeMs) * 0.12f;
        state.FrameTimeMs = GetFrameTimeMs(browserFrameTimestampMs);
        state.SmoothedFrameTimeMs = state.SmoothedFrameTimeMs <= 0f
            ? state.FrameTimeMs
            : state.SmoothedFrameTimeMs + (state.FrameTimeMs - state.SmoothedFrameTimeMs) * 0.12f;
        state.Fps = state.FrameTimeMs > 0f
            ? (int)MathF.Round(1000f / state.FrameTimeMs)
            : Raylib.GetFPS();
        state.HeapSizeBytes = GC.GetTotalMemory(forceFullCollection: false);
        state.TotalAllocatedBytes = GC.GetTotalAllocatedBytes(precise: false);
        state.Gen0Collections = GC.CollectionCount(0);
        state.Gen1Collections = GC.CollectionCount(1);
        state.Gen2Collections = GC.CollectionCount(2);
        state.TotalGcPauseDuration = GC.GetTotalPauseDuration();
        state.GcPauseDeltaMs = Math.Max(0, (state.TotalGcPauseDuration - state.PreviousTotalGcPauseDuration).TotalMilliseconds);
        state.PreviousTotalGcPauseDuration = state.TotalGcPauseDuration;
        state.GcPauseTotalMs = state.TotalGcPauseDuration.TotalMilliseconds;
        state.GcPausePercentage = GC.GetGCMemoryInfo().PauseTimePercentage;

        var mp = OperatingSystem.IsBrowser() ? s_browserMousePosition : Raylib.GetMousePosition();
        RenderFrameInfo frame = OperatingSystem.IsBrowser()
            ? s_browserFrame
            : new RenderFrameInfo(Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
        UiInputState input = CollectUiInput();

        ui.Frame(frame, mp, input, new DemoFrameContext(state, s_checkerTexture, frame.LogicalHeight), static (root, context) =>
        {
            DrawRoot(root, context);
        });
    }

    private static float GetFrameTimeMs(double browserFrameTimestampMs)
    {
        if (!OperatingSystem.IsBrowser())
            return Raylib.GetFrameTime() * 1000f;

        if (!double.IsFinite(browserFrameTimestampMs))
            return 1000f / 60f;

        if (!double.IsFinite(s_previousBrowserFrameTimestampMs))
        {
            s_previousBrowserFrameTimestampMs = browserFrameTimestampMs;
            return 1000f / 60f;
        }

        double delta = browserFrameTimestampMs - s_previousBrowserFrameTimestampMs;
        s_previousBrowserFrameTimestampMs = browserFrameTimestampMs;

        return delta > 0 && double.IsFinite(delta)
            ? (float)delta
            : 1000f / 60f;
    }

    [JSExport]
    public static void Resize(int logicalWidth, int logicalHeight, int framebufferWidth, int framebufferHeight)
    {
        s_browserFrame = new RenderFrameInfo(logicalWidth, logicalHeight, framebufferWidth, framebufferHeight).Normalized();
        if (s_initialized)
            Raylib.SetWindowSize(s_browserFrame.FramebufferWidth, s_browserFrame.FramebufferHeight);
    }

    [JSExport]
    public static void SetTheme(bool lightTheme)
    {
        s_state.SelectedTheme = lightTheme ? 1 : 0;
    }

    [JSExport]
    public static void SetPointerState(float x, float y, int buttons, float wheelY)
    {
        s_browserMousePosition = new System.Numerics.Vector2(x, y);
        s_browserMouseButtons = buttons;
        s_browserWheelY += wheelY;
    }

    [JSExport]
    public static void Shutdown()
    {
        s_ui?.Dispose();
        s_renderer?.Shutdown();
        s_ui = null;
        s_renderer = null;

        if (s_initialized)
            Raylib.CloseWindow();

        s_initialized = false;
    }

static void DrawRoot(Ui root, DemoFrameContext context)
{
    root.FillViewport(root.Theme.SurfaceBg);

    using(root.MaxWidth(1040, UiAlign.Center))
    {
        DemoState state = context.State;
        root.Docking = state.Docking;
        state.MenuOpenedThisFrame = false;
        state.QuickMenuButton = default;

        bool wideLayout = root.AvailableWidth >= 760f;
        const float SectionGap = 12f;

        Response menuBar = DrawDemoMenuBar(root, state);
        root.Spacing(SectionGap);
        Response headerPanel = DrawHeaderPanel(root, state, wideLayout);

        root.Spacing(SectionGap);
        float bodyRegionHeight = MathF.Max(260f, context.ScreenHeight - root.RootPadding * 2f - menuBar.H - headerPanel.H - SectionGap * 3f);
        float maxDockHeight = MathF.Max(110f, bodyRegionHeight - 140f);
        float dockHeight = MathF.Min(Math.Clamp(bodyRegionHeight * 0.64f, 220f, 320f), maxDockHeight);
        root.DockSpace("mainDock", root.AvailableWidth, dockHeight);
        ApplyDefaultDocking(root, state);

        root.Spacing(SectionGap);
        float bodyHeight = MathF.Max(140f, bodyRegionHeight - dockHeight);
        root.ScrollArea(
            "demoBody",
            root.AvailableWidth,
            bodyHeight,
            (State: state, WideLayout: wideLayout, CheckerTexture: context.CheckerTexture),
            static (body, bodyContext) => DrawBody(body, bodyContext.State, bodyContext.WideLayout, bodyContext.CheckerTexture));

        root.Window(
            "Inspector",
            state.InspectorWindow,
            280,
            state,
            static (window, state) => DrawInspector(window, state),
            resizable: true,
            id: "inspector");
        root.Window(
            "Metrics",
            state.MetricsWindow,
            260,
            state,
            static (window, state) => DrawMetricsWindow(window, state),
            resizable: true,
            id: "metrics");
        root.Window(
            "Theme",
            state.ThemeWindow,
            320,
            state,
            static (window, state) => DrawThemeWindow(window, state),
            resizable: true,
            id: "theme-editor");
        for (int i = 0; i < state.ExtraMetricsWindows.Count; i++)
        {
            root.Window(
                $"Metrics {i + 2}",
                state.ExtraMetricsWindows[i],
                260,
                state,
                static (window, state) => DrawMetricsWindow(window, state),
                resizable: true,
                id: $"metrics-{i + 2}");
        }

        root.Popup("quickMenu", state.QuickMenuButton, 220, 180, popup => DrawQuickMenu(popup, state));
    }


}

static void ApplyDefaultDocking(Ui root, DemoState state)
{
    if (state.DefaultDockingApplied)
        return;

    bool metricsDocked = root.DockWindow("mainDock", "metrics", DockPlacement.Center);
    bool themeDocked = root.DockWindow("mainDock", "theme-editor", DockPlacement.Right);
    state.DefaultDockingApplied = metricsDocked && themeDocked;
}

static Response DrawDemoMenuBar(Ui host, DemoState state)
{
    return host.MenuBar(host.AvailableWidth, state, static (bar, state) =>
    {
        bar.Menu("App", state, static (menu, state) =>
        {
            if (menu.MenuItem("Increment clicks", closeOnActivate: true, shortcut: "Ctrl+I").Clicked)
                state.ClickCount++;

            if (menu.MenuItem("Reset clicks", closeOnActivate: true, shortcut: "Ctrl+R").Clicked)
                state.ClickCount = 0;

            if (menu.MenuItem("Show inspector", closeOnActivate: true, shortcut: "F12").Clicked)
            {
                state.InspectorWindow.Open = true;
                state.InspectorWindow.Collapsed = false;
            }

            if (menu.MenuItem("Show metrics", closeOnActivate: true).Clicked)
            {
                state.MetricsWindow.Open = true;
                state.MetricsWindow.Collapsed = false;
            }

            if (menu.MenuItem("Add metrics window", closeOnActivate: true).Clicked)
                state.AddMetricsWindow();

            menu.MenuSeparator();

            menu.Menu("Action", state, static (submenu, state) =>
            {
                if (submenu.MenuItem("Set action 1", selected: state.SelectedAction == 1, closeOnActivate: true).Clicked)
                    state.SelectedAction = 1;

                if (submenu.MenuItem("Set action 5", selected: state.SelectedAction == 5, closeOnActivate: true).Clicked)
                    state.SelectedAction = 5;

                if (submenu.MenuItem("Set action 10", selected: state.SelectedAction == 10, closeOnActivate: true).Clicked)
                    state.SelectedAction = 10;

                if (submenu.MenuItem("Clear action", selected: state.SelectedAction < 0, closeOnActivate: true).Clicked)
                    state.SelectedAction = -1;
            });
        }, popupWidth: 260f);

        bar.Menu("Theme", state, static (menu, state) =>
        {
            DrawThemeMenuItem(menu, state, 0, "Dark", new Vellum.Rendering.Color(80, 91, 112), "Alt+1");
            DrawThemeMenuItem(menu, state, 1, "Light", new Vellum.Rendering.Color(224, 196, 122), "Alt+2");
            menu.MenuSeparator();

            if (menu.MenuItem("Show editor", closeOnActivate: true).Clicked)
            {
                state.ThemeWindow.Open = true;
                state.ThemeWindow.Collapsed = false;
            }

            if (menu.MenuItem("Reset edited theme", closeOnActivate: true).Clicked)
                state.ResetThemeToSelectedPreset();
        });

        bar.Menu("View", state, static (menu, state) =>
        {
            menu.Menu("Density", state, static (submenu, state) =>
            {
                if (submenu.MenuItem("Compact", selected: state.Density == 0, closeOnActivate: true).Clicked)
                    state.Density = 0;

                if (submenu.MenuItem("Comfortable", selected: state.Density == 1, closeOnActivate: true).Clicked)
                    state.Density = 1;

                if (submenu.MenuItem("Relaxed", selected: state.Density == 2, closeOnActivate: true).Clicked)
                    state.Density = 2;
            });

            if (menu.MenuItem("Toggle details", selected: state.DetailsOpen, closeOnActivate: true).Clicked)
                state.DetailsOpen = !state.DetailsOpen;

            if (menu.MenuItem("Reset docking", closeOnActivate: true).Clicked)
                state.ResetDockingWindows();
        });
    });
}

static void DrawThemeMenuItem(Ui menu, DemoState state, int index, string label, Vellum.Rendering.Color swatchColor, string shortcut)
{
    string id = index switch
    {
        0 => "theme-0",
        1 => "theme-1",
        _ => "theme-x"
    };
    var row = (Color: swatchColor, Label: label);
    if (menu.MenuItem(row, static (item, row) =>
    {
        item.Canvas(14f, 14f, row.Color, static (canvas, color) =>
            canvas.FillRect(0f, 0f, canvas.Width, canvas.Height, color, radius: 3f));
        item.Label(row.Label);
    }, id: id, selected: state.SelectedTheme == index, closeOnActivate: true, shortcut: shortcut).Clicked)
    {
        state.SelectedTheme = index;
        state.ResetThemeToSelectedPreset();
    }
}

static Response DrawHeaderPanel(Ui host, DemoState state, bool wideLayout)
{
    return host.Panel(host.AvailableWidth, (State: state, WideLayout: wideLayout), static (header, context) =>
    {
        DemoState state = context.State;

        header.Heading("Vellum Web");
        header.Label(
            "A compact immediate-mode dashboard with framed sections, popups, keyboard navigation, and custom painting.",
            color: header.Theme.TextSecondary,
            maxWidth: header.AvailableWidth,
            wrap: TextWrapMode.WordWrap);
        header.Spacing(10);

        string stats = $"Theme: {ThemeLabel(state.SelectedTheme)} · Density: {DensityLabel(state.Density)} · Action: {(state.SelectedAction > 0 ? state.SelectedAction : 0)}";
        if (context.WideLayout)
        {
            using (header.Row())
            {
                float summaryWidth = MathF.Max(0, MathF.Floor(header.AvailableWidth * 0.52f));
                using (header.FixedWidth(summaryWidth))
                {
                    header.ProgressBar(MathF.Min(1f, state.ClickCount / 10f), header.AvailableWidth, overlay: $"Clicks: {Math.Min(state.ClickCount, 10)}/10");
                    header.Label(
                        $"Theme: {ThemeLabel(state.SelectedTheme)} · Density: {DensityLabel(state.Density)} · Action: {(state.SelectedAction > 0 ? state.SelectedAction : 0)}",
                        color: header.Theme.TextSecondary,
                        maxWidth: header.AvailableWidth,
                        wrap: TextWrapMode.WordWrap);
                }

                using (header.FixedWidth(header.AvailableWidth))
                    DrawQuickActions(header, state);
            }
        }
        else
        {
            header.ProgressBar(MathF.Min(1f, state.ClickCount / 10f), header.AvailableWidth, overlay: $"Clicks: {Math.Min(state.ClickCount, 10)}/10");
            header.Label(stats, color: header.Theme.TextSecondary, maxWidth: header.AvailableWidth, wrap: TextWrapMode.WordWrap);
            header.Spacing(10);
            DrawQuickActions(header, state);
        }
    });
}

static void DrawBody(Ui body, DemoState state, bool wideLayout, int checkerTexture)
{
    if (wideLayout)
    {
        using (body.Row())
        {
            float sidebarWidth = MathF.Min(280f, MathF.Max(236f, body.AvailableWidth * 0.32f));

            using (body.FixedWidth(sidebarWidth))
                DrawSettingsPanel(body, state, checkerTexture);
            using (body.FixedWidth(body.AvailableWidth))
            {
                DrawWorkspacePanel(body, state);
                DrawActivityPanel(body, state);
            }
        }
    }
    else
    {
        DrawSettingsPanel(body, state, checkerTexture);
        DrawWorkspacePanel(body, state);
        DrawActivityPanel(body, state);
    }
}

static void DrawQuickActions(Ui panel, DemoState state)
{
    float rowWidth = MathF.Max(0, MathF.Floor((panel.AvailableWidth - panel.Theme.Gap) * 0.5f));

    if (panel.AvailableWidth >= 280f)
    {
        using (panel.Row())
        {
            if (panel.Button("Click me", width: rowWidth).Clicked) state.ClickCount++;
            if (panel.Button("Reset", width: rowWidth).Clicked) state.ClickCount = 0;
        }

        using (panel.Row())
        {
            if (panel.Button("Focus name", width: rowWidth).Clicked)
                panel.RequestFocus(UiWidgetKind.TextField, "name");

            state.QuickMenuButton = panel.Button("Quick menu", width: rowWidth);
        }
    }
    else
    {
        if (panel.Button("Click me", width: panel.AvailableWidth).Clicked) state.ClickCount++;
        if (panel.Button("Reset", width: panel.AvailableWidth).Clicked) state.ClickCount = 0;
        if (panel.Button("Focus name", width: panel.AvailableWidth).Clicked)
            panel.RequestFocus(UiWidgetKind.TextField, "name");
        state.QuickMenuButton = panel.Button("Quick menu", width: panel.AvailableWidth);
    }

    if (state.QuickMenuButton.Clicked)
    {
        if (panel.IsPopupOpen("quickMenu"))
            panel.ClosePopup("quickMenu");
        else
            panel.OpenPopup("quickMenu");

        state.MenuOpenedThisFrame = true;
    }

    if (!state.InspectorWindow.Open && panel.Button("Show inspector", width: panel.AvailableWidth).Clicked)
    {
        state.InspectorWindow.Open = true;
        state.InspectorWindow.Collapsed = false;
    }
}

static void DrawSettingsPanel(Ui host, DemoState state, int checkerTexture)
{
    host.Panel(host.AvailableWidth, (State: state, CheckerTexture: checkerTexture), static (panel, context) =>
    {
        DemoState state = context.State;

        PanelTitle(panel, "Controls", "Toggles, selection widgets, color editing, and a small texture preview.");
        panel.Checkbox("Enable notifications", ref state.NotificationsEnabled, width: panel.AvailableWidth);
        panel.Checkbox("Enable analytics", ref state.AnalyticsEnabled, width: panel.AvailableWidth);
        panel.Separator();
        panel.Label("Density", color: panel.Theme.TextSecondary);
        panel.RadioValue("Compact", ref state.Density, 0, width: panel.AvailableWidth);
        panel.RadioValue("Comfortable", ref state.Density, 1, width: panel.AvailableWidth);
        panel.RadioValue("Relaxed", ref state.Density, 2, width: panel.AvailableWidth);
        panel.Spacing(4);
        panel.Label($"Volume: {state.Volume:0}%", color: panel.Theme.TextSecondary);
        panel.Slider("Volume", ref state.Volume, 0, 100, panel.AvailableWidth, step: 1, id: "volume");
        panel.Spacing(4);
        using (panel.Row())
        {
            float halfWidth = MathF.Max(0, MathF.Floor((panel.AvailableWidth - panel.Theme.Gap) * 0.5f));
            using (panel.FixedWidth(halfWidth))
            {
                panel.Label("Sensitivity", color: panel.Theme.TextSecondary);
                panel.DragFloat(string.Empty, ref state.Sensitivity, speed: 0.01f, min: 0f, max: 10f, width: panel.AvailableWidth, id: "sensitivity");
            }
            using (panel.FixedWidth(panel.AvailableWidth))
            {
                panel.Label("Max retries", color: panel.Theme.TextSecondary);
                panel.DragInt(string.Empty, ref state.MaxRetries, speed: 0.1f, min: 0, max: 10, width: panel.AvailableWidth, id: "maxRetries");
            }
        }
        panel.Spacing(4);
        panel.Label("Theme", color: panel.Theme.TextSecondary);
        Response themeCombo = panel.ComboBox("theme", DemoState.ThemeOptions, ref state.SelectedTheme, panel.AvailableWidth, maxPopupHeight: 140f);
        panel.Tooltip(themeCombo, "Switch between the built-in dark and light theme presets.");
        panel.Separator();
        panel.ColorPickerPopup("Accent", ref state.AccentColor, panel.AvailableWidth, id: "accentColor");
        panel.Separator();
        panel.Label("Image preview", color: panel.Theme.TextSecondary);
        panel.Image(context.CheckerTexture, panel.AvailableWidth, 88);
    });
}

static void DrawWorkspacePanel(Ui host, DemoState state)
{
    host.Panel(host.AvailableWidth, state, static (panel, state) =>
    {
        PanelTitle(panel, "Workspace", "Tabbed editor, file browser, and profile form.");

        panel.TabBar("workspace-tabs", state, static (bar, state) =>
        {
            bar.Tab("Editor", state, static (page, state) =>
            {
                page.Spacing(8);
                page.Label("Name", color: page.Theme.TextSecondary);
                page.TextField("name", ref state.Name, page.AvailableWidth, placeholder: "Type your name");
                page.Spacing(6);
                page.Label("Notes", color: page.Theme.TextSecondary);
                page.TextArea("notes", ref state.Notes, page.AvailableWidth, 124, placeholder: "Type multiple lines...");
                page.Spacing(6);
                page.Label("Read-only mirror", color: page.Theme.TextSecondary);
                page.TextField("nameMirror", ref state.Name, page.AvailableWidth, readOnly: true);
                page.Spacing(6);

                Response details = page.CollapsingHeader("Details", ref state.DetailsOpen, width: page.AvailableWidth);
                string detailsStatus = details.Opened
                    ? "Details opened this frame."
                    : details.Closed
                        ? "Details closed this frame."
                        : state.DetailsOpen
                            ? "Details are open."
                            : "Details are closed.";
                BodyLabel(page, detailsStatus, details.Opened || details.Closed ? page.Theme.Accent : page.Theme.TextSecondary);
                if (state.DetailsOpen)
                {
                    BodyLabel(page, "This header drives the shared opened/closed/toggled response flags.", page.Theme.TextSecondary);
                    BodyLabel(page, "That gives future collapsing, tree, and combo widgets a consistent transition contract.", page.Theme.TextSecondary);
                }
            });

            bar.Tab("Browser", state, static (page, state) =>
            {
                page.Spacing(8);
                page.Panel(page.AvailableWidth, state, static (frame, state) =>
                {
                    frame.TreeNode("Vellum", state, static (node, state) =>
                    {
                        node.TreeNode("Rendering", state, static (sub, state) =>
                        {
                            if (sub.TreeLeaf("Painter.cs", selected: state.SelectedTreeItem == "Painter").Clicked)
                                state.SelectedTreeItem = "Painter";
                            if (sub.TreeLeaf("RenderList.cs", selected: state.SelectedTreeItem == "RenderList").Clicked)
                                state.SelectedTreeItem = "RenderList";
                            if (sub.TreeLeaf("GlyphAtlas.cs", selected: state.SelectedTreeItem == "GlyphAtlas").Clicked)
                                state.SelectedTreeItem = "GlyphAtlas";
                        }, defaultOpen: true);
                        node.TreeNode("Layout", state, static (sub, state) =>
                        {
                            if (sub.TreeLeaf("EdgeInsets.cs", selected: state.SelectedTreeItem == "EdgeInsets").Clicked)
                                state.SelectedTreeItem = "EdgeInsets";
                            if (sub.TreeLeaf("Theme.cs", selected: state.SelectedTreeItem == "Theme").Clicked)
                                state.SelectedTreeItem = "Theme";
                        });
                        node.TreeNode("Widgets", state, static (sub, state) =>
                        {
                            if (sub.TreeLeaf("Ui.Widgets.cs", selected: state.SelectedTreeItem == "Widgets").Clicked)
                                state.SelectedTreeItem = "Widgets";
                            if (sub.TreeLeaf("Ui.Tree.cs", selected: state.SelectedTreeItem == "Tree").Clicked)
                                state.SelectedTreeItem = "Tree";
                            if (sub.TreeLeaf("Ui.Menus.cs", selected: state.SelectedTreeItem == "Menus").Clicked)
                                state.SelectedTreeItem = "Menus";
                        });
                    }, defaultOpen: true);
                });
            });

            bar.Tab("Profile", state, static (page, state) =>
            {
                page.Spacing(8);
                page.Checkbox("Lock fields", ref state.ProfileLocked);
                page.Spacing(6);
                using (page.Disabled(state.ProfileLocked))
                {
                    page.Panel(page.AvailableWidth, state, static (frame, state) =>
                    {
                        frame.ItemSpacing(6);

                        using (frame.Row())
                        {
                            using (frame.FixedWidth(80f))
                                frame.Label("Name", color: frame.Theme.TextSecondary);
                            frame.Separator();
                            using (frame.FixedWidth(frame.AvailableWidth))
                                frame.TextField("profileName", ref state.Name, frame.AvailableWidth);
                        }

                        using (frame.Row())
                        {
                            using (frame.FixedWidth(80f))
                                frame.Label("Email", color: frame.Theme.TextSecondary);
                            frame.Separator();
                            using (frame.FixedWidth(frame.AvailableWidth))
                                frame.TextField("profileEmail", ref state.Email, frame.AvailableWidth);
                        }

                        using (frame.Row())
                        {
                            using (frame.FixedWidth(80f))
                                frame.Label("Role", color: frame.Theme.TextSecondary);
                            frame.Separator();
                            using (frame.FixedWidth(frame.AvailableWidth))
                                frame.TextField("profileRole", ref state.Role, frame.AvailableWidth);
                        }
                    });
                }
            });
        });
    });
}

static void DrawActivityPanel(Ui host, DemoState state)
{
    host.Panel(host.AvailableWidth, state, static (panel, state) =>
    {
        PanelTitle(panel, "Activity", "Scrollable actions on one side, live state and custom drawing on the other.");

        if (panel.AvailableWidth >= 520f)
        {
            using (panel.Row())
            {
                float leftWidth = MathF.Max(0, MathF.Floor((panel.AvailableWidth - panel.Theme.Gap) * 0.4f));
                using (panel.FixedWidth(leftWidth))
                {
                    panel.Label("Actions", color: panel.Theme.TextSecondary);
                    panel.ScrollArea("actions", panel.AvailableWidth, 236, state, static (actions, state) => DrawActionButtons(actions, state));
                }

                using (panel.FixedWidth(panel.AvailableWidth))
                    DrawStatusCanvas(panel, state);
            }
        }
        else
        {
            panel.Label("Actions", color: panel.Theme.TextSecondary);
            panel.ScrollArea("actions", panel.AvailableWidth, 156, state, static (actions, state) => DrawActionButtons(actions, state));
            DrawStatusCanvas(panel, state);
        }

        panel.Spacing(8);
        panel.Label("Timeline", color: panel.Theme.TextSecondary);
        panel.ScrollAreaBoth("timeline", panel.AvailableWidth, 132, state, static (timeline, state) => DrawTimelinePreview(timeline, state));
        BodyLabel(panel, "The timeline preview scrolls on both axes. Drag either thumb, use the wheel vertically, or hold Shift while wheeling to route motion horizontally.", panel.Theme.TextSecondary);
    });
}

static void DrawActionButtons(Ui actions, DemoState state)
{
    for (int i = 1; i <= 18; i++)
    {
        if (actions.Button($"Action {i}", width: actions.AvailableWidth).Clicked)
            state.SelectedAction = i;
    }
}

static void DrawStatusCanvas(Ui panel, DemoState state)
{
    BodyLabel(panel, $"Hello '{state.Name}' — you have clicked {state.ClickCount} times.");
    BodyLabel(panel, $"Notifications: {(state.NotificationsEnabled ? "on" : "off")} · Analytics: {(state.AnalyticsEnabled ? "on" : "off")}");
    BodyLabel(panel, $"Density: {DensityLabel(state.Density)} · Volume: {state.Volume:0}% · Theme: {ThemeLabel(state.SelectedTheme)}");
    BodyLabel(
        panel,
        state.SelectedAction > 0 ? $"Selected action: {state.SelectedAction}" : "Selected action: none",
        state.SelectedAction > 0 ? panel.Theme.Accent : panel.Theme.TextSecondary);
    panel.Label("Canvas (right-click for actions)", color: panel.Theme.TextSecondary);
    Response canvasArea = panel.Canvas(panel.AvailableWidth, 154, (State: state, Theme: panel.Theme), static (canvas, context) =>
    {
        DemoState state = context.State;
        Theme theme = context.Theme;

        float outerRadius = theme.BorderRadius;
        canvas.DrawRect(
            0,
            0,
            canvas.Width,
            canvas.Height,
            theme.PanelBg,
            theme.PanelBorder,
            theme.BorderWidth,
            outerRadius);

        canvas.DrawText("Custom painter widget", 14, 12, color: theme.TextPrimary);

        float labelX = 14f;
        float barX = 92f;
        float barAreaWidth = MathF.Max(0, canvas.Width - barX - 14f);
        float barH = 14;
        float clicksWidth = MathF.Min(barAreaWidth, state.ClickCount * 18);
        float nameWidth = MathF.Min(barAreaWidth, state.Name.Length * 20);
        float actionWidth = state.SelectedAction > 0 ? MathF.Min(barAreaWidth, state.SelectedAction * 14) : 0;

        canvas.DrawText("Clicks", labelX, 45, size: 14, color: theme.TextSecondary);
        canvas.DrawRect(barX, 48, barAreaWidth, barH, theme.ScrollbarTrack, radius: barH * 0.5f);
        canvas.DrawRect(barX, 48, clicksWidth, barH, theme.Accent, radius: barH * 0.5f);

        canvas.DrawText("Name", labelX, 79, size: 14, color: theme.TextSecondary);
        canvas.DrawRect(barX, 82, barAreaWidth, barH, theme.ScrollbarTrack, radius: barH * 0.5f);
        canvas.DrawRect(barX, 82, nameWidth, barH, theme.ButtonBgHover, radius: barH * 0.5f);

        canvas.DrawText("Action", labelX, 113, size: 14, color: theme.TextSecondary);
        canvas.DrawRect(barX, 116, barAreaWidth, barH, theme.ScrollbarTrack, radius: barH * 0.5f);
        if (actionWidth > 0)
            canvas.DrawRect(barX, 116, actionWidth, barH, theme.FocusBorder, radius: barH * 0.5f);
    });
    panel.ContextMenu("status-canvas", canvasArea, state, static (menu, state) =>
    {
        if (menu.MenuItem("Increment clicks", closeOnActivate: true).Clicked) state.ClickCount++;
        if (menu.MenuItem("Reset clicks", closeOnActivate: true).Clicked) state.ClickCount = 0;
        menu.MenuSeparator();
        if (menu.MenuItem("Toggle notifications", selected: state.NotificationsEnabled, closeOnActivate: true).Clicked)
            state.NotificationsEnabled = !state.NotificationsEnabled;
        if (menu.MenuItem("Toggle analytics", selected: state.AnalyticsEnabled, closeOnActivate: true).Clicked)
            state.AnalyticsEnabled = !state.AnalyticsEnabled;
        menu.MenuSeparator();
        if (menu.MenuItem("Clear action", closeOnActivate: true).Clicked)
            state.SelectedAction = -1;
    });

    BodyLabel(panel, "Tab moves focus, Space clicks buttons, Enter submits, Escape cancels, Ctrl+C/X/V/A edit text.", panel.Theme.TextSecondary);
    BodyLabel(panel, "The actions list is a real clipped scroll area with wheel and draggable scrollbar.", panel.Theme.TextSecondary);
}

static void DrawTimelinePreview(Ui timeline, DemoState state)
{
    const float TimelineWidth = 960f;
    const float TimelineHeight = 208f;
    timeline.Canvas(TimelineWidth, TimelineHeight, (State: state, Theme: timeline.Theme), static (canvas, context) =>
    {
        DemoState state = context.State;
        Theme theme = context.Theme;

        float radius = theme.BorderRadius;
        float headerHeight = 28f;
        float lanesX = 132f;
        float laneWidth = canvas.Width - lanesX - 20f;
        float laneHeight = 28f;
        float laneGap = 10f;
        float lanesY = headerHeight + 22f;
        Vellum.Rendering.Color axisLabelBg = theme.PanelBg.WithAlpha(235);
        Vellum.Rendering.Color axisLabelText = theme.TextPrimary;

        canvas.DrawRect(0, 0, canvas.Width, canvas.Height, theme.PanelBg, theme.PanelBorder, theme.BorderWidth, radius);
        canvas.DrawRect(0, 0, canvas.Width, headerHeight, theme.ButtonBg, radius: radius);
        canvas.DrawText("Timeline preview", 14, 8, size: 15f, color: theme.TextPrimary);
        canvas.DrawText("Oversized content for bidirectional scrolling", 164, 8, size: 14f, color: theme.TextSecondary);

        for (int tick = 0; tick <= 10; tick++)
        {
            float tickX = lanesX + tick * 78f;
            canvas.FillRect(tickX, headerHeight, 1f, canvas.Height - headerHeight - 14f, theme.Separator.WithAlpha(96));
            canvas.FillRect(tickX + 3f, headerHeight + 3f, 32f, 16f, axisLabelBg, radius: 4f);
            canvas.DrawText($"T+{tick}", tickX + 7f, headerHeight + 5f, size: 12f, color: axisLabelText);
        }

        for (int lane = 0; lane < 4; lane++)
        {
            float laneY = lanesY + lane * (laneHeight + laneGap);
            string laneLabel = lane switch
            {
                0 => "Input",
                1 => "Layout",
                2 => "Paint",
                _ => "Present"
            };

            canvas.DrawText(laneLabel, 16f, laneY + 6f, size: 14f, color: theme.TextSecondary);
            canvas.FillRect(lanesX, laneY, laneWidth, laneHeight, theme.ScrollbarTrack, radius: laneHeight * 0.5f);

            for (int slot = 0; slot < 9; slot++)
            {
                int actionIndex = lane * 4 + slot + 1;
                float blockX = lanesX + 20f + slot * 88f + lane * 12f;
                float blockWidth = 42f + ((slot + lane) % 3) * 12f;
                Vellum.Rendering.Color fill = actionIndex == state.SelectedAction
                    ? theme.Accent
                    : slot % 2 == 0
                        ? theme.ButtonBgHover
                        : theme.FocusBorder.WithAlpha(150);

                canvas.FillRect(blockX, laneY + 4f, blockWidth, laneHeight - 8f, fill, radius: 7f);
                canvas.DrawText($"{actionIndex}", blockX + 12f, laneY + 7f, size: 12f, color: theme.TextPrimary);
            }
        }
    });
}

static void DrawInspector(Ui window, DemoState state)
{
    window.Label("Resize from the bottom-right corner.", color: window.Theme.TextSecondary, maxWidth: window.AvailableWidth, wrap: TextWrapMode.WordWrap);
    window.Spacing(4);
    window.Label("Floating window", color: window.Theme.Accent);
    BodyLabel(window, "This behaves more like egui/imgui windows: absolute position, caption, body drag, and title-bar collapse/close controls.");
    using (window.Id("inspectorSwitch"))
        window.Switch("Enable analytics", ref state.AnalyticsEnabled, width: window.AvailableWidth);
    window.Spacing(6);
    window.Menu("Hover menu", state, static (menu, state) =>
    {
        if (menu.MenuItem("Increment clicks", closeOnActivate: true).Clicked)
            state.ClickCount++;

        if (menu.MenuItem("Toggle details", selected: state.DetailsOpen, closeOnActivate: true).Clicked)
            state.DetailsOpen = !state.DetailsOpen;

        menu.Menu("Theme", state, static (submenu, state) =>
        {
            if (submenu.MenuItem("Dark", selected: state.SelectedTheme == 0, closeOnActivate: true).Clicked)
                state.SelectedTheme = 0;

            if (submenu.MenuItem("Light", selected: state.SelectedTheme == 1, closeOnActivate: true).Clicked)
                state.SelectedTheme = 1;
        });
    }, width: window.AvailableWidth, openOnHover: true, openToSide: true);
    window.Separator();
    window.Label($"Clicks: {state.ClickCount}", color: window.Theme.TextSecondary);
    window.Label($"Theme: {ThemeLabel(state.SelectedTheme)}", color: window.Theme.TextSecondary);
    window.Label(state.SelectedAction > 0 ? $"Action: {state.SelectedAction}" : "Action: none", color: window.Theme.TextSecondary);
    window.Separator();
    window.Label("Performance", color: window.Theme.Accent);
    window.Row(static row =>
    {
        row.Spinner(16f, thickness: 2.5f);
        row.Label("Render loop active", color: row.Theme.TextSecondary);
    });
    window.Label($"UI CPU: {state.UiCpuTimeMs:0.00} ms", color: window.Theme.TextSecondary);
    window.Label($"Smoothed UI CPU: {state.SmoothedUiCpuTimeMs:0.00} ms", color: window.Theme.TextSecondary);
    window.Label($"Frame time (capped): {state.FrameTimeMs:0.00} ms", color: window.Theme.TextSecondary);
    window.Label($"FPS: {state.Fps}", color: window.Theme.TextSecondary);
    window.Spacing(4);
    window.CollapsingHeader("Garbage collection", ref state.GarbageCollectionOpen, width: window.AvailableWidth);
    if (state.GarbageCollectionOpen)
    {
        window.Label($"Managed heap: {FormatBytes(state.HeapSizeBytes)}", color: window.Theme.TextSecondary);
        window.Label($"Total allocated: {FormatBytes(state.TotalAllocatedBytes)}", color: window.Theme.TextSecondary);
        window.Label($"Collections: Gen0 {state.Gen0Collections} · Gen1 {state.Gen1Collections} · Gen2 {state.Gen2Collections}", color: window.Theme.TextSecondary);
        window.Label($"GC pause this frame: {state.GcPauseDeltaMs:0.###} ms", color: window.Theme.TextSecondary);
        window.Label($"GC pause total: {state.GcPauseTotalMs:0.###} ms", color: window.Theme.TextSecondary);
        window.Label($"Pause time percentage: {state.GcPausePercentage:0.##}%", color: window.Theme.TextSecondary);
    }
}

static void DrawMetricsWindow(Ui window, DemoState state)
{
    window.Label("Dock companion", color: window.Theme.Accent);
    BodyLabel(window, "Renderer timing, frame counters, and interaction state for the current demo session.", color: window.Theme.TextSecondary);
    window.Separator();
    window.Table("metrics-table", s_metricsColumns, state, static (table, state) =>
    {
        table.Row(state, static (row, state) =>
        {
            row.Cell("UI CPU");
            row.Cell($"{state.UiCpuTimeMs:0.00} ms");
        });

        table.Row(state, static (row, state) =>
        {
            row.Cell("Frame time");
            row.Cell($"{state.FrameTimeMs:0.00} ms");
        });

        table.Row(state, static (row, state) =>
        {
            row.Cell("FPS");
            row.Cell(state.Fps.ToString());
        });
    }, width: window.AvailableWidth);
    window.ProgressBar(MathF.Min(1f, state.SmoothedUiCpuTimeMs / TargetFrameBudgetMs), window.AvailableWidth, overlay: "UI budget");
    window.Separator();
    if (window.Button("Increment clicks", width: window.AvailableWidth).Clicked)
        state.ClickCount++;
    if (window.Button("Reset docking", width: window.AvailableWidth).Clicked)
        state.ResetDockingWindows();
}

static void DrawThemeWindow(Ui window, DemoState state)
{
    Theme theme = state.ResolveTheme();

    window.Label("Live theme editor", color: theme.Accent);
    BodyLabel(window, "Changes are applied to the active demo theme immediately. Reset restores the selected preset.", color: theme.TextSecondary);
    window.Separator();

    int previousTheme = state.SelectedTheme;
    if (window.ComboBox("Preset", DemoState.ThemeOptions, ref state.SelectedTheme, window.AvailableWidth, maxPopupHeight: 140f).Changed &&
        state.SelectedTheme != previousTheme)
    {
        state.ResetThemeToSelectedPreset();
        theme = state.ResolveTheme();
    }

    if (window.Button("Reset selected preset", width: window.AvailableWidth).Clicked)
    {
        state.ResetThemeToSelectedPreset();
        theme = state.ResolveTheme();
    }

    window.Slider("Corner radius", ref theme.BorderRadius, 0f, 14f, window.AvailableWidth, format: "{0:0.0}", id: "border-radius");
    window.Slider("Border width", ref theme.BorderWidth, 0f, 3f, window.AvailableWidth, format: "{0:0.0}", id: "border-width");
    NormalizeThemeShape(theme);
    window.Slider("Gap", ref theme.Gap, 0f, 20f, window.AvailableWidth, format: "{0:0.0}", id: "gap");
    window.Slider("Scrollbar width", ref theme.ScrollbarWidth, 6f, 20f, window.AvailableWidth, format: "{0:0.0}", id: "scrollbar-width");
    window.Slider("Slider height", ref theme.SliderHeight, 14f, 36f, window.AvailableWidth, format: "{0:0.0}", id: "slider-height");
    window.Separator();

    ThemeSection(window, "Surfaces");
    ThemeColor(window, "Surface", ref theme.SurfaceBg, "surface-bg");
    ThemeColor(window, "Panel", ref theme.PanelBg, "panel-bg");
    ThemeColor(window, "Panel border", ref theme.PanelBorder, "panel-border");
    ThemeColor(window, "Scroll area", ref theme.ScrollAreaBg, "scroll-area-bg");
    ThemeColor(window, "Scroll border", ref theme.ScrollAreaBorder, "scroll-area-border");

    ThemeSection(window, "Text");
    ThemeColor(window, "Primary", ref theme.TextPrimary, "text-primary");
    ThemeColor(window, "Secondary", ref theme.TextSecondary, "text-secondary");
    ThemeColor(window, "Muted", ref theme.TextMuted, "text-muted");
    ThemeColor(window, "Window title", ref theme.WindowTitleText, "window-title-text");
    ThemeColor(window, "Title fill", ref theme.WindowTitleBg, "window-title-bg", alpha: true);
    ThemeColor(window, "Title hover", ref theme.WindowTitleBgHover, "window-title-hover", alpha: true);

    ThemeSection(window, "Accent");
    ThemeColor(window, "Accent", ref theme.Accent, "accent");
    ThemeColor(window, "Focus border", ref theme.FocusBorder, "focus-border");

    ThemeSection(window, "Buttons");
    ThemeColor(window, "Button", ref theme.ButtonBg, "button-bg");
    ThemeColor(window, "Button hover", ref theme.ButtonBgHover, "button-hover");
    ThemeColor(window, "Button pressed", ref theme.ButtonBgPressed, "button-pressed");
    ThemeColor(window, "Button border", ref theme.ButtonBorder, "button-border");
    ThemeColor(window, "Button border hover", ref theme.ButtonBorderHover, "button-border-hover");
    ThemeColor(window, "Button border pressed", ref theme.ButtonBorderPressed, "button-border-pressed");

    ThemeSection(window, "Inputs");
    ThemeColor(window, "Text field", ref theme.TextFieldBg, "text-field-bg");
    ThemeColor(window, "Text field hover", ref theme.TextFieldBgHover, "text-field-hover");
    ThemeColor(window, "Text field focus", ref theme.TextFieldBgFocused, "text-field-focus");
    ThemeColor(window, "Field border", ref theme.TextFieldBorder, "text-field-border");
    ThemeColor(window, "Field border focus", ref theme.TextFieldBorderFocused, "text-field-border-focus");
    ThemeColor(window, "Selection", ref theme.TextFieldSelectionBg, "text-selection", alpha: true);
    ThemeColor(window, "Caret", ref theme.TextFieldCaret, "text-field-caret");
    ThemeColor(window, "Placeholder", ref theme.TextFieldPlaceholder, "text-field-placeholder");

    ThemeSection(window, "Selection");
    ThemeColor(window, "Selectable", ref theme.SelectableBg, "selectable-bg");
    ThemeColor(window, "Selectable hover", ref theme.SelectableBgHover, "selectable-hover");
    ThemeColor(window, "Selectable pressed", ref theme.SelectableBgPressed, "selectable-pressed");
    ThemeColor(window, "Selected", ref theme.SelectableBgSelected, "selectable-selected");
    ThemeColor(window, "Border", ref theme.SelectableBorder, "selectable-border");
    ThemeColor(window, "Border hover", ref theme.SelectableBorderHover, "selectable-border-hover");
    ThemeColor(window, "Border pressed", ref theme.SelectableBorderPressed, "selectable-border-pressed");
    ThemeColor(window, "Border selected", ref theme.SelectableBorderSelected, "selectable-border-selected");
    ThemeColor(window, "Indicator", ref theme.SelectableIndicator, "selectable-indicator");

    ThemeSection(window, "Toggles");
    ThemeColor(window, "Toggle", ref theme.ToggleBg, "toggle-bg");
    ThemeColor(window, "Toggle hover", ref theme.ToggleBgHover, "toggle-hover");
    ThemeColor(window, "Toggle pressed", ref theme.ToggleBgPressed, "toggle-pressed");
    ThemeColor(window, "Toggle active", ref theme.ToggleBgActive, "toggle-active");
    ThemeColor(window, "Toggle border", ref theme.ToggleBorder, "toggle-border");
    ThemeColor(window, "Toggle border hover", ref theme.ToggleBorderHover, "toggle-border-hover");
    ThemeColor(window, "Toggle border pressed", ref theme.ToggleBorderPressed, "toggle-border-pressed");
    ThemeColor(window, "Toggle border active", ref theme.ToggleBorderActive, "toggle-border-active");
    ThemeColor(window, "Toggle indicator", ref theme.ToggleIndicator, "toggle-indicator");

    ThemeSection(window, "Scrollbars");
    ThemeColor(window, "Track", ref theme.ScrollbarTrack, "scrollbar-track", alpha: true);
    ThemeColor(window, "Thumb", ref theme.ScrollbarThumb, "scrollbar-thumb", alpha: true);
    ThemeColor(window, "Thumb hover", ref theme.ScrollbarThumbHover, "scrollbar-hover", alpha: true);
    ThemeColor(window, "Thumb active", ref theme.ScrollbarThumbActive, "scrollbar-active", alpha: true);

    ThemeSection(window, "Popups");
    ThemeColor(window, "Popup", ref theme.PopupBg, "popup-bg");
    ThemeColor(window, "Popup border", ref theme.PopupBorder, "popup-border");
    ThemeColor(window, "Modal backdrop", ref theme.ModalBackdrop, "modal-backdrop", alpha: true);
    ThemeColor(window, "Tooltip", ref theme.TooltipBg, "tooltip-bg", alpha: true);
    ThemeColor(window, "Tooltip border", ref theme.TooltipBorder, "tooltip-border", alpha: true);

    ThemeSection(window, "Progress And Plots");
    ThemeColor(window, "Progress bg", ref theme.ProgressBarBg, "progress-bg");
    ThemeColor(window, "Progress border", ref theme.ProgressBarBorder, "progress-border");
    ThemeColor(window, "Progress", ref theme.ProgressBarFill, "progress-fill");
    ThemeColor(window, "Plot bg", ref theme.PlotBg, "plot-bg");
    ThemeColor(window, "Plot border", ref theme.PlotBorder, "plot-border");
    ThemeColor(window, "Plot fill", ref theme.PlotFill, "plot-fill", alpha: true);
    ThemeColor(window, "Separator", ref theme.Separator, "separator", alpha: true);

    ThemeSection(window, "Headers");
    ThemeColor(window, "Header", ref theme.CollapsingHeaderBg, "header-bg", alpha: true);
    ThemeColor(window, "Header hover", ref theme.CollapsingHeaderBgHover, "header-hover", alpha: true);
    ThemeColor(window, "Header pressed", ref theme.CollapsingHeaderBgPressed, "header-pressed", alpha: true);
    ThemeColor(window, "Header open", ref theme.CollapsingHeaderBgOpen, "header-open", alpha: true);

    ThemeSection(window, "Sliders");
    ThemeColor(window, "Slider", ref theme.SliderBg, "slider-bg");
    ThemeColor(window, "Slider hover", ref theme.SliderBgHover, "slider-hover");
    ThemeColor(window, "Slider active", ref theme.SliderBgActive, "slider-active");
    ThemeColor(window, "Slider fill", ref theme.SliderFill, "slider-fill");
    ThemeColor(window, "Slider fill active", ref theme.SliderFillActive, "slider-fill-active");
    ThemeColor(window, "Slider border", ref theme.SliderBorder, "slider-border");
}

static void ThemeSection(Ui window, string label)
{
    window.Spacing(6f);
    window.Label(label, color: window.Theme.Accent);
}

static void NormalizeThemeShape(Theme theme)
{
    theme.BorderWidth = MathF.Max(0f, theme.BorderWidth);
    theme.BorderRadius = MathF.Max(0f, theme.BorderRadius);
    float minPositiveRadius = theme.BorderWidth > 0f ? theme.BorderWidth + 1f : 0f;
    if (theme.BorderRadius > 0f && theme.BorderRadius < minPositiveRadius)
        theme.BorderRadius = minPositiveRadius;
}

static void ThemeColor(Ui window, string label, ref Vellum.Rendering.Color color, string id, bool alpha = true)
    => window.ColorPickerPopup(label, ref color, window.AvailableWidth, pickerWidth: 280f, alpha: alpha, id: id, openOnHover: false);

static void DrawQuickMenu(Ui popup, DemoState state)
{
    popup.Label("Quick actions", color: popup.Theme.Accent);

    if (popup.Button("Increment click count", width: popup.AvailableWidth).Clicked)
    {
        state.ClickCount++;
        popup.CloseAllPopups();
    }

    if (popup.Button("Reset click count", width: popup.AvailableWidth).Clicked)
    {
        state.ClickCount = 0;
        popup.CloseAllPopups();
    }

    if (popup.Button("Set action 1", width: popup.AvailableWidth).Clicked)
    {
        state.SelectedAction = 1;
        popup.CloseAllPopups();
    }

    if (popup.MenuItem("Select theme: Dark", state.SelectedTheme == 0, closeOnActivate: true).Clicked)
    {
        state.SelectedTheme = 0;
    }

    if (popup.MenuItem("Select theme: Light", state.SelectedTheme == 1, closeOnActivate: true).Clicked)
    {
        state.SelectedTheme = 1;
    }

    Response moreButton = popup.Button("More actions", width: popup.AvailableWidth);
    if (moreButton.Clicked && !state.MenuOpenedThisFrame)
    {
        if (popup.IsPopupOpen("more"))
            popup.ClosePopup("more");
        else
            popup.OpenPopup("more");
    }

    popup.Popup("more", moreButton.X + moreButton.W + 6, moreButton.Y, 190, 150, nested => DrawMoreQuickMenu(nested, state));
}

static void DrawMoreQuickMenu(Ui nested, DemoState state)
{
    if (nested.Button("Set action 5", width: nested.AvailableWidth).Clicked)
    {
        state.SelectedAction = 5;
        nested.CloseAllPopups();
    }

    if (nested.Button("Set action 10", width: nested.AvailableWidth).Clicked)
    {
        state.SelectedAction = 10;
        nested.CloseAllPopups();
    }

    if (nested.Button("Clear action", width: nested.AvailableWidth).Clicked)
    {
        state.SelectedAction = -1;
        nested.CloseAllPopups();
    }

    if (nested.MenuItem("Toggle theme", closeOnActivate: true).Clicked)
        state.SelectedTheme = state.SelectedTheme == 0 ? 1 : 0;
}

static void DrawSettingsDialog(Ui modal, DemoState state)
{
    modal.Label("Settings dialog", color: modal.Theme.Accent);
    modal.Label(
        "This is a modal popup. The backdrop is dimmed, and clicking outside the dialog does not dismiss it.",
        color: modal.Theme.TextSecondary,
        maxWidth: modal.AvailableWidth,
        wrap: TextWrapMode.WordWrap);
    modal.Separator();

    modal.Checkbox("Enable notifications", ref state.NotificationsEnabled, width: modal.AvailableWidth);
    modal.Switch("Enable analytics", ref state.AnalyticsEnabled, width: modal.AvailableWidth);
    modal.Spacing(4);

    modal.Label("Density", color: modal.Theme.TextSecondary);
    modal.RadioValue("Compact", ref state.Density, 0, width: modal.AvailableWidth);
    modal.RadioValue("Comfortable", ref state.Density, 1, width: modal.AvailableWidth);
    modal.RadioValue("Relaxed", ref state.Density, 2, width: modal.AvailableWidth);
    modal.Spacing(4);

    modal.Label("Theme", color: modal.Theme.TextSecondary);
    modal.ComboBox("modalTheme", DemoState.ThemeOptions, ref state.SelectedTheme, modal.AvailableWidth, maxPopupHeight: 140f);
    modal.Spacing(8);

    using (modal.Row())
    {
        float halfWidth = MathF.Max(0, MathF.Floor((modal.AvailableWidth - modal.Theme.Gap) * 0.5f));

        if (modal.Button("Cancel", width: halfWidth).Clicked)
            modal.CloseCurrentPopup();

        if (modal.Button("Apply", width: modal.AvailableWidth).Clicked)
            modal.CloseCurrentPopup();
    }
}

static void BodyLabel(Ui panel, string text, Vellum.Rendering.Color? color = null)
    => panel.Label(text, color: color, maxWidth: panel.AvailableWidth, wrap: TextWrapMode.WordWrap);

static void PanelTitle(Ui panel, string title, string subtitle)
{
    panel.Label(title, color: panel.Theme.Accent);
    panel.Label(subtitle, color: panel.Theme.TextSecondary, maxWidth: panel.AvailableWidth, wrap: TextWrapMode.WordWrap);
}

static UiInputState CollectUiInput()
{
    var keys = new HashSet<UiKey>();
    var mouseButtons = new HashSet<UiMouseButton>();
    AddKey(keys, UiKey.Left, KeyboardKey.Left);
    AddKey(keys, UiKey.Right, KeyboardKey.Right);
    AddKey(keys, UiKey.Up, KeyboardKey.Up);
    AddKey(keys, UiKey.Down, KeyboardKey.Down);
    AddKey(keys, UiKey.Home, KeyboardKey.Home);
    AddKey(keys, UiKey.End, KeyboardKey.End);
    AddKey(keys, UiKey.Tab, KeyboardKey.Tab);
    AddKey(keys, UiKey.Enter, KeyboardKey.Enter);
    AddKey(keys, UiKey.Escape, KeyboardKey.Escape);
    AddKey(keys, UiKey.Space, KeyboardKey.Space);
    AddKey(keys, UiKey.Backspace, KeyboardKey.Backspace);
    AddKey(keys, UiKey.Delete, KeyboardKey.Delete);
    AddKey(keys, UiKey.A, KeyboardKey.A);
    AddKey(keys, UiKey.C, KeyboardKey.C);
    AddKey(keys, UiKey.V, KeyboardKey.V);
    AddKey(keys, UiKey.X, KeyboardKey.X);

    string textInput = CollectTextInput();
    bool shift = Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);
    bool ctrl = Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl);
    bool alt = Raylib.IsKeyDown(KeyboardKey.LeftAlt) || Raylib.IsKeyDown(KeyboardKey.RightAlt);
    System.Numerics.Vector2 wheelDelta;
    if (OperatingSystem.IsBrowser())
    {
        wheelDelta = new System.Numerics.Vector2(0, s_browserWheelY);
        s_browserWheelY = 0;
        AddBrowserMouseButton(mouseButtons, UiMouseButton.Left, 1);
        AddBrowserMouseButton(mouseButtons, UiMouseButton.Right, 2);
        AddBrowserMouseButton(mouseButtons, UiMouseButton.Middle, 4);
    }
    else
    {
        wheelDelta = new System.Numerics.Vector2(0, Raylib.GetMouseWheelMove());
        AddMouseButton(mouseButtons, UiMouseButton.Left, RlMouseButton.Left);
        AddMouseButton(mouseButtons, UiMouseButton.Right, RlMouseButton.Right);
        AddMouseButton(mouseButtons, UiMouseButton.Middle, RlMouseButton.Middle);
    }

    return new UiInputState(
        textInput,
        keys.Count > 0 ? keys : null,
        wheelDelta,
        shift,
        ctrl,
        alt,
        meta: false,
        downMouseButtons: mouseButtons.Count > 0 ? mouseButtons : null,
        timeSeconds: Raylib.GetTime());
}

static string DensityLabel(int density) => density switch
{
    0 => "Compact",
    1 => "Comfortable",
    2 => "Relaxed",
    _ => "Unknown"
};

static string ThemeLabel(int theme) => theme switch
{
    0 => "Dark",
    1 => "Light",
    _ => "Unknown"
};

static string FormatBytes(long bytes)
{
    string[] units = ["B", "KB", "MB", "GB", "TB"];
    double value = Math.Max(0, bytes);
    int unitIndex = 0;

    while (value >= 1024 && unitIndex < units.Length - 1)
    {
        value /= 1024;
        unitIndex++;
    }

    return unitIndex == 0
        ? $"{value:0} {units[unitIndex]}"
        : $"{value:0.00} {units[unitIndex]}";
}

static byte[] CreateCheckerRgba()
{
    const int size = 16;
    var pixels = new byte[size * size * 4];
    for (int y = 0; y < size; y++)
    {
        for (int x = 0; x < size; x++)
        {
            bool dark = ((x / 4) + (y / 4)) % 2 == 0;
            byte value = dark ? (byte)84 : (byte)154;
            int index = (y * size + x) * 4;
            pixels[index + 0] = value;
            pixels[index + 1] = dark ? (byte)102 : (byte)172;
            pixels[index + 2] = dark ? (byte)128 : (byte)196;
            pixels[index + 3] = 255;
        }
    }

    return pixels;
}

static string CollectTextInput()
{
    var builder = new StringBuilder();

    while (true)
    {
        int codepoint = Raylib.GetCharPressed();
        if (codepoint == 0) break;
        builder.Append(char.ConvertFromUtf32(codepoint));
    }

    return builder.ToString();
}

static void AddKey(HashSet<UiKey> keys, UiKey uiKey, KeyboardKey raylibKey)
{
    if (Raylib.IsKeyPressed(raylibKey) || Raylib.IsKeyPressedRepeat(raylibKey))
        keys.Add(uiKey);
}

static void AddMouseButton(HashSet<UiMouseButton> buttons, UiMouseButton uiButton, RlMouseButton raylibButton)
{
    if (Raylib.IsMouseButtonDown(raylibButton))
        buttons.Add(uiButton);
}

static void AddBrowserMouseButton(HashSet<UiMouseButton> buttons, UiMouseButton uiButton, int browserButtonMask)
{
    if ((s_browserMouseButtons & browserButtonMask) != 0)
        buttons.Add(uiButton);
}

static void RunHeadlessBench()
{
    var renderer = new NoopBenchRenderer();
    int checkerTextureId = renderer.CreateTexture(new byte[16 * 16 * 4], 16, 16);
    var ui = new Ui(renderer) { DefaultFontSize = 18f, Lcd = true, Platform = new NoopBenchPlatform() };
    var demoState = new DemoState();

    Console.WriteLine($"{"scene",-44} bytes/frame      MB/20s");

    Bench("empty frame",                  static (root, context) => { });
    Bench("menu bar only",                static (root, context) => DrawDemoMenuBar(root, context.State));
    Bench("header panel only",            static (root, context) => DrawHeaderPanel(root, context.State, wideLayout: true));
    Bench("settings panel only",          static (root, context) => DrawSettingsPanel(root, context.State, context.CheckerTexture));
    Bench("workspace panel only",         static (root, context) => DrawWorkspacePanel(root, context.State));
    Bench("activity panel only",          static (root, context) => DrawActivityPanel(root, context.State));
    Bench("  - action buttons (18)",      static (root, context) => DrawActionButtons(root, context.State));
    Bench("  - status canvas",            static (root, context) => DrawStatusCanvas(root, context.State));
    Bench("  - timeline preview",         static (root, context) => DrawTimelinePreview(root, context.State));
    Bench("inspector window only",        static (root, context) =>
    {
        context.State.InspectorWindow.Open = true;
        root.Window(
            "Inspector",
            context.State.InspectorWindow,
            280,
            context.State,
            static (window, state) => DrawInspector(window, state),
            resizable: true,
            id: "inspector");
    });
    Bench("full DrawRoot",                static (root, context) => DrawRoot(root, context));

    void Bench(string name, Action<Ui, DemoFrameContext> scene)
    {
        for (int i = 0; i < 30; i++) RunOneFrame(scene);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long beforeBytes = GC.GetAllocatedBytesForCurrentThread();

        const int Iterations = 200;
        for (int i = 0; i < Iterations; i++) RunOneFrame(scene);

        long afterBytes = GC.GetAllocatedBytesForCurrentThread();
        double bytesPerFrame = (afterBytes - beforeBytes) / (double)Iterations;
        double mbPer20s = bytesPerFrame * 60 * 20 / (1024.0 * 1024.0);
        Console.WriteLine($"{name,-44} {bytesPerFrame,12:N1}    {mbPer20s,8:F1}");
    }

    void RunOneFrame(Action<Ui, DemoFrameContext> scene)
    {
        ui.Theme = demoState.ResolveTheme();
        var input = new UiInputState();
        ui.Frame(800, 600, default, input, new DemoFrameContext(demoState, checkerTextureId, 600), scene);
    }
}

internal sealed class NoopBenchRenderer : IRenderer
{
    private int _nextTextureId = 1;
    public void BeginFrame(RenderFrameInfo frame) { }
    public void Render(RenderList renderList) { }
    public void EndFrame() { }
    public int CreateTexture(byte[] rgba, int width, int height) => _nextTextureId++;
    public void DestroyTexture(int textureId) { }
}

internal sealed class NoopBenchPlatform : IUiPlatform
{
    public string GetClipboardText() => string.Empty;
    public void SetClipboardText(string text) { }
    public void SetCursor(UiCursor cursor) { }
}

readonly record struct DemoFrameContext(DemoState State, int CheckerTexture, int ScreenHeight);

sealed class DemoState
{
    public static readonly string[] ThemeOptions = ["Dark", "Light"];

    public int ClickCount;
    public string Name = "World";
    public string Notes = "This is a multiline text area.\nIt supports selection, caret movement, and scrolling.";
    public int SelectedAction = -1;
    public bool NotificationsEnabled = true;
    public bool AnalyticsEnabled;
    public int Density = 1;
    public float Volume = 65f;
    public float Sensitivity = 1.5f;
    public int MaxRetries = 3;
    public int SelectedTheme;
    private int _resolvedTheme = -1;
    public Theme EditableTheme = ThemePresets.Dark();
    public Vellum.Rendering.Color AccentColor = new(86, 122, 178, 220);
    public bool MenuOpenedThisFrame;
    public bool DetailsOpen;
    public string? SelectedTreeItem;
    public string Email = "pat@example.com";
    public string Role = "Engineer";
    public bool ProfileLocked;
    public bool GarbageCollectionOpen = true;
    public DockingState Docking = new();
    public bool DefaultDockingApplied;
    public WindowState InspectorWindow = new(new System.Numerics.Vector2(720, 76));
    public WindowState MetricsWindow = new(new System.Numerics.Vector2(690, 310));
    public WindowState ThemeWindow = new(new System.Numerics.Vector2(650, 420), new System.Numerics.Vector2(320, 420));
    public List<WindowState> ExtraMetricsWindows = new();
    public Response QuickMenuButton;
    public float UiCpuTimeMs;
    public float SmoothedUiCpuTimeMs;
    public float FrameTimeMs;
    public float SmoothedFrameTimeMs;
    public int Fps;
    public long HeapSizeBytes = GC.GetTotalMemory(forceFullCollection: false);
    public long TotalAllocatedBytes = GC.GetTotalAllocatedBytes(precise: false);
    public int Gen0Collections = GC.CollectionCount(0);
    public int Gen1Collections = GC.CollectionCount(1);
    public int Gen2Collections = GC.CollectionCount(2);
    public TimeSpan TotalGcPauseDuration;
    public TimeSpan PreviousTotalGcPauseDuration;
    public double GcPauseDeltaMs;
    public double GcPauseTotalMs;
    public double GcPausePercentage;

    public DemoState()
    {
        TotalGcPauseDuration = GC.GetTotalPauseDuration();
        PreviousTotalGcPauseDuration = TotalGcPauseDuration;
        GcPauseTotalMs = TotalGcPauseDuration.TotalMilliseconds;
    }

    public void AddMetricsWindow()
    {
        int index = ExtraMetricsWindows.Count;
        ExtraMetricsWindows.Add(new WindowState(new System.Numerics.Vector2(660 - index * 24f, 340 + index * 28f)));
    }

    public void ResetDockingWindows()
    {
        Docking.Reset();
        DefaultDockingApplied = false;
        InspectorWindow.Position = new System.Numerics.Vector2(720, 76);
        MetricsWindow.Position = new System.Numerics.Vector2(690, 310);
        ThemeWindow.Position = new System.Numerics.Vector2(650, 420);
        InspectorWindow.Open = true;
        MetricsWindow.Open = true;
        ThemeWindow.Open = true;
        InspectorWindow.Collapsed = false;
        MetricsWindow.Collapsed = false;
        ThemeWindow.Collapsed = false;

        for (int i = 0; i < ExtraMetricsWindows.Count; i++)
        {
            var window = ExtraMetricsWindows[i];
            window.Position = new System.Numerics.Vector2(660 - i * 24f, 340 + i * 28f);
            window.Open = true;
            window.Collapsed = false;
        }
    }

    public Theme ResolveTheme()
    {
        if (_resolvedTheme != SelectedTheme)
            ResetThemeToSelectedPreset();

        return EditableTheme;
    }

    public void ResetThemeToSelectedPreset()
    {
        EditableTheme = SelectedTheme switch
        {
            1 => ThemePresets.Light(),
            _ => ThemePresets.Dark()
        };
        _resolvedTheme = SelectedTheme;
    }
}

}
