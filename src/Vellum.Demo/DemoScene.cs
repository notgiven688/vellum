using System.Numerics;
using Vellum.Rendering;

namespace Vellum.Demo;

internal static class DemoScene
{
    public static void DrawRoot(Ui root, DemoFrameContext context)
    {
        root.FillViewport(root.Theme.SurfaceBg);

        using (root.MaxWidth(1040, UiAlign.Center))
        {
            DemoState state = context.State;
            state.MenuOpenedThisFrame = false;
            state.QuickMenuButton = default;

            bool wideLayout = root.AvailableWidth >= 760f;
            const float SectionGap = 12f;

            Response menuBar = DrawDemoMenuBar(root, state);
            root.Spacing(SectionGap);
            Response headerPanel = DrawHeaderPanel(root, state, wideLayout);

            root.Spacing(SectionGap);
            float bodyHeight = MathF.Max(220f, context.ScreenHeight - root.RootPadding * 2f - menuBar.H - headerPanel.H - SectionGap * 2f);
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
            root.Popup("quickMenu", state.QuickMenuButton, 220, 180, popup => DrawQuickMenu(popup, state));
        }
    }

    public static Theme ResolveDemoTheme(int selectedTheme) => selectedTheme switch
    {
        1 => ThemeCache.Light,
        _ => ThemeCache.Dark
    };

    public static byte[] CreateCheckerRgba()
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

    private static Response DrawDemoMenuBar(Ui host, DemoState state)
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
            });

            bar.Menu("Theme", state, static (menu, state) =>
            {
                DrawThemeMenuItem(menu, state, 0, "Dark", new Color(80, 91, 112), "Alt+1");
                DrawThemeMenuItem(menu, state, 1, "Light", new Color(224, 196, 122), "Alt+2");
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
            });
        });
    }

    private static void DrawThemeMenuItem(Ui menu, DemoState state, int index, string label, Color swatchColor, string shortcut)
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
        }
    }

    private static Response DrawHeaderPanel(Ui host, DemoState state, bool wideLayout)
    {
        return host.Panel(host.AvailableWidth, (State: state, WideLayout: wideLayout), static (header, context) =>
        {
            DemoState state = context.State;

            header.Heading("Vellum Demo");
            header.Label(
                "A compact immediate-mode dashboard with framed sections, popups, keyboard navigation, and custom painting.",
                color: header.Theme.TextSecondary,
                maxWidth: header.AvailableWidth,
                wrap: TextWrapMode.WordWrap);
            header.Spacing(10);

            string stats = $"Theme: {ThemeLabel(state.SelectedTheme)} - Density: {DensityLabel(state.Density)} - Action: {(state.SelectedAction > 0 ? state.SelectedAction : 0)}";
            if (context.WideLayout)
            {
                using (header.Row())
                {
                    float summaryWidth = MathF.Max(0, MathF.Floor(header.AvailableWidth * 0.52f));
                    using (header.FixedWidth(summaryWidth))
                    {
                        header.ProgressBar(MathF.Min(1f, state.ClickCount / 10f), header.AvailableWidth, overlay: $"Clicks: {Math.Min(state.ClickCount, 10)}/10");
                        header.Label(stats, color: header.Theme.TextSecondary, maxWidth: header.AvailableWidth, wrap: TextWrapMode.WordWrap);
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

    private static void DrawBody(Ui body, DemoState state, bool wideLayout, int checkerTexture)
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

    private static void DrawQuickActions(Ui panel, DemoState state)
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

    private static void DrawSettingsPanel(Ui host, DemoState state, int checkerTexture)
    {
        host.Panel(host.AvailableWidth, (State: state, CheckerTexture: checkerTexture), static (panel, context) =>
        {
            DemoState state = context.State;

            PanelTitle(panel, "Controls", "Toggles, selection widgets, and a small texture preview.");
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
            panel.Label("Image preview", color: panel.Theme.TextSecondary);
            panel.Image(context.CheckerTexture, panel.AvailableWidth, 88);
        });
    }

    private static void DrawWorkspacePanel(Ui host, DemoState state)
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

    private static void DrawActivityPanel(Ui host, DemoState state)
    {
        host.Panel(host.AvailableWidth, state, static (panel, state) =>
        {
            PanelTitle(panel, "Activity", "Scrollable actions on one side, live state and custom drawing on the other.");

            if (panel.AvailableWidth >= 520f)
            {
                using (panel.Row())
                {
                    const float splitterWidth = 8f;
                    const float actionsHeight = 236f;
                    float totalWidth = panel.AvailableWidth;
                    float minLeftWidth = MathF.Min(180f, MathF.Max(0, totalWidth * 0.35f));
                    float minRightWidth = MathF.Min(220f, MathF.Max(0, totalWidth * 0.35f));
                    float maxLeftWidth = MathF.Max(minLeftWidth, totalWidth - minRightWidth - splitterWidth - panel.Theme.Gap * 2f);
                    if (state.ActivityActionsWidth <= 0f)
                        state.ActivityActionsWidth = MathF.Floor((totalWidth - splitterWidth - panel.Theme.Gap * 2f) * 0.4f);
                    state.ActivityActionsWidth = Math.Clamp(state.ActivityActionsWidth, minLeftWidth, maxLeftWidth);

                    float leftWidth = state.ActivityActionsWidth;
                    using (panel.FixedWidth(leftWidth))
                    {
                        panel.Label("Actions", color: panel.Theme.TextSecondary);
                        panel.ScrollArea("actions", panel.AvailableWidth, actionsHeight, state, static (actions, state) => DrawActionButtons(actions, state));
                    }

                    panel.Splitter("activity-splitter", ref state.ActivityActionsWidth, minLeftWidth, maxLeftWidth, splitterWidth, actionsHeight + panel.DefaultFontSize + panel.Theme.Gap);

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
            BodyLabel(panel, "The timeline preview scrolls on both axes. Drag the content or either thumb, use the wheel vertically, or hold Shift while wheeling to route motion horizontally.", panel.Theme.TextSecondary);
        });
    }

    private static void DrawActionButtons(Ui actions, DemoState state)
    {
        for (int i = 1; i <= 18; i++)
        {
            if (actions.Button($"Action {i}", width: actions.AvailableWidth).Clicked)
                state.SelectedAction = i;
        }
    }

    private static void DrawStatusCanvas(Ui panel, DemoState state)
    {
        BodyLabel(panel, $"Hello '{state.Name}' - you have clicked {state.ClickCount} times.");
        BodyLabel(panel, $"Notifications: {(state.NotificationsEnabled ? "on" : "off")} - Analytics: {(state.AnalyticsEnabled ? "on" : "off")}");
        BodyLabel(panel, $"Density: {DensityLabel(state.Density)} - Volume: {state.Volume:0}% - Theme: {ThemeLabel(state.SelectedTheme)}");
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

    private static void DrawTimelinePreview(Ui timeline, DemoState state)
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
            Color axisLabelBg = theme.PanelBg.WithAlpha(235);
            Color axisLabelText = theme.TextPrimary;

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
                    Color fill = actionIndex == state.SelectedAction
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

    private static void DrawInspector(Ui window, DemoState state)
    {
        window.Label("Resize from the bottom-right corner.", color: window.Theme.TextSecondary, maxWidth: window.AvailableWidth, wrap: TextWrapMode.WordWrap);
        window.Spacing(4);
        window.Label("Floating window", color: window.Theme.Accent);
        BodyLabel(window, "This behaves like an immediate-mode floating window: absolute position, caption, body drag, and title-bar collapse/close controls.");
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
        window.Label($"Frame time: {state.FrameTimeMs:0.00} ms", color: window.Theme.TextSecondary);
        window.Label($"FPS: {state.Fps}", color: window.Theme.TextSecondary);
        window.Spacing(4);
        window.CollapsingHeader("Garbage collection", ref state.GarbageCollectionOpen, width: window.AvailableWidth);
        if (state.GarbageCollectionOpen)
        {
            window.Label($"Managed heap: {FormatBytes(state.HeapSizeBytes)}", color: window.Theme.TextSecondary);
            window.Label($"Total allocated: {FormatBytes(state.TotalAllocatedBytes)}", color: window.Theme.TextSecondary);
            window.Label($"Collections: Gen0 {state.Gen0Collections} - Gen1 {state.Gen1Collections} - Gen2 {state.Gen2Collections}", color: window.Theme.TextSecondary);
            window.Label($"GC pause this frame: {state.GcPauseDeltaMs:0.###} ms", color: window.Theme.TextSecondary);
            window.Label($"GC pause total: {state.GcPauseTotalMs:0.###} ms", color: window.Theme.TextSecondary);
            window.Label($"Pause time percentage: {state.GcPausePercentage:0.##}%", color: window.Theme.TextSecondary);
        }
        window.Spacing(4);

        if (window.Button("Increment clicks", width: window.AvailableWidth).Clicked)
            state.ClickCount++;

        if (window.Button("Open settings dialog", width: window.AvailableWidth).Clicked)
            window.OpenPopup("settingsDialog");

        if (window.Button("Toggle details", width: window.AvailableWidth).Clicked)
            state.DetailsOpen = !state.DetailsOpen;

        if (window.Button("Reset action", width: window.AvailableWidth).Clicked)
            state.SelectedAction = -1;

        window.ModalPopup("settingsDialog", 380, 320, state, static (modal, state) => DrawSettingsDialog(modal, state));
    }

    private static void DrawQuickMenu(Ui popup, DemoState state)
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
            state.SelectedTheme = 0;

        if (popup.MenuItem("Select theme: Light", state.SelectedTheme == 1, closeOnActivate: true).Clicked)
            state.SelectedTheme = 1;

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

    private static void DrawMoreQuickMenu(Ui nested, DemoState state)
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

    private static void DrawSettingsDialog(Ui modal, DemoState state)
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

    private static void BodyLabel(Ui panel, string text, Color? color = null)
        => panel.Label(text, color: color, maxWidth: panel.AvailableWidth, wrap: TextWrapMode.WordWrap);

    private static void PanelTitle(Ui panel, string title, string subtitle)
    {
        panel.Label(title, color: panel.Theme.Accent);
        panel.Label(subtitle, color: panel.Theme.TextSecondary, maxWidth: panel.AvailableWidth, wrap: TextWrapMode.WordWrap);
    }

    private static string DensityLabel(int density) => density switch
    {
        0 => "Compact",
        1 => "Comfortable",
        2 => "Relaxed",
        _ => "Unknown"
    };

    private static string ThemeLabel(int theme) => theme switch
    {
        0 => "Dark",
        1 => "Light",
        _ => "Unknown"
    };

    private static string FormatBytes(long bytes)
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
}

internal readonly record struct DemoFrameContext(DemoState State, int CheckerTexture, int ScreenHeight);

internal sealed class DemoState
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
    public bool MenuOpenedThisFrame;
    public bool DetailsOpen;
    public string? SelectedTreeItem;
    public string Email = "pat@example.com";
    public string Role = "Engineer";
    public bool ProfileLocked;
    public bool GarbageCollectionOpen = true;
    public float ActivityActionsWidth;
    public WindowState InspectorWindow = new(new Vector2(720, 76));
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
}

internal static class ThemeCache
{
    public static readonly Theme Dark = ThemePresets.Dark();
    public static readonly Theme Light = ThemePresets.Light();
}
