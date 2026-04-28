using System.Numerics;
using Vellum.Rendering;

namespace Vellum.WidgetGallery;

internal static class WidgetExamples
{
    public static IReadOnlyList<WidgetExample> All { get; } =
    [
        new("label", "Label", "Text", 360, 88, (ui, _) =>
        {
            ui.Label("Status: renderer connected", color: ui.Theme.TextPrimary);
            ui.Label("Secondary information", color: ui.Theme.TextSecondary);
        }),

        new("heading", "Heading", "Text", 360, 96, (ui, _) =>
        {
            ui.Heading("Project Settings");
            ui.Label("Large text uses the same layout path as labels.", color: ui.Theme.TextSecondary);
        }),

        new("separator", "Separator", "Text", 360, 96, (ui, _) =>
        {
            ui.Label("Before");
            ui.Separator();
            ui.Label("After");
        }),

        new("button", "Button", "Controls", 300, 90, (ui, _) =>
        {
            ui.Button("Save Changes", width: 180f);
        }, Mouse: new Vector2(42, 26)),

        new("checkbox", "Checkbox", "Controls", 300, 90, (ui, _) =>
        {
            bool value = true;
            ui.Checkbox("Enable notifications", ref value);
        }),

        new("switch", "Switch", "Controls", 320, 90, (ui, _) =>
        {
            bool value = true;
            ui.Switch("Sync automatically", ref value, width: 230f);
        }),

        new("radio-button", "Radio Button", "Controls", 300, 112, (ui, _) =>
        {
            ui.RadioButton("Daily", selected: true);
            ui.RadioButton("Weekly", selected: false);
        }),

        new("selectable", "Selectable", "Controls", 320, 132, (ui, _) =>
        {
            ui.Selectable("Overview", selected: true, width: 220f);
            ui.Selectable("Metrics", selected: false, width: 220f);
            ui.Selectable("Logs", selected: false, width: 220f);
        }),

        new("combo-box", "Combo Box", "Controls", 340, 96, (ui, _) =>
        {
            int selected = 1;
            ui.ComboBox("theme", ["Dark", "Light", "System"], ref selected, 220f);
        }),

        new("slider", "Slider", "Controls", 360, 96, (ui, _) =>
        {
            float volume = 65f;
            ui.Slider("volume", ref volume, 0f, 100f, 260f, label: "Volume");
        }),

        new("slider-int", "SliderInt", "Controls", 360, 96, (ui, _) =>
        {
            int retries = 3;
            ui.SliderInt("retries", ref retries, 0, 8, 260f, label: "Retries");
        }),

        new("drag-float", "DragFloat", "Controls", 320, 92, (ui, _) =>
        {
            float value = 1.25f;
            ui.DragFloat("gain", ref value, speed: 0.05f, min: 0f, max: 4f, width: 160f);
        }),

        new("drag-int", "DragInt", "Controls", 320, 92, (ui, _) =>
        {
            int value = 12;
            ui.DragInt("count", ref value, speed: 1f, min: 0, max: 99, width: 160f);
        }),

        new("progress-bar", "ProgressBar", "Status", 360, 92, (ui, _) =>
        {
            ui.ProgressBar(0.68f, 260f, overlay: "68%");
        }),

        new("histogram", "Histogram", "Status", 360, 144, (ui, _) =>
        {
            float[] values = [0.18f, 0.42f, 0.36f, 0.72f, 0.55f, 0.86f, 0.62f, 0.7f, 0.48f, 0.58f];
            ui.Histogram(values, 280f, 92f, overlay: "Requests");
        }),

        new("spinner", "Spinner", "Status", 220, 92, (ui, _) =>
        {
            using (ui.Horizontal())
            {
                ui.Spinner(28f);
                ui.Label("Loading");
            }
        }, Input: new UiInputState(timeSeconds: 0.35)),

        new("text-field", "TextField", "Input", 380, 92, (ui, _) =>
        {
            string name = "Ada Lovelace";
            ui.TextField("name", ref name, 280f);
        }),

        new("text-area", "TextArea", "Input", 420, 178, (ui, _) =>
        {
            string notes = "Line one\nLine two\nLine three";
            ui.TextArea("notes", ref notes, 320f, 112f);
        }),

        new("panel", "Panel", "Layout", 380, 158, (ui, _) =>
        {
            ui.Panel(300f, panel =>
            {
                panel.Label("Panel title", color: panel.Theme.Accent);
                panel.Label("Panels reserve a framed region for related controls.", color: panel.Theme.TextSecondary, maxWidth: panel.AvailableWidth, wrap: TextWrapMode.WordWrap);
            });
        }),

        new("canvas", "Canvas", "Layout", 360, 152, (ui, _) =>
        {
            ui.Canvas(280f, 90f, canvas =>
            {
                canvas.DrawRect(0, 0, canvas.Width, canvas.Height, ui.Theme.PlotBg, ui.Theme.PlotBorder, ui.Theme.BorderWidth, ui.Theme.BorderRadius);
                canvas.FillRect(18, 54, 34, 22, ui.Theme.Accent, radius: 4f);
                canvas.FillRect(64, 36, 34, 40, ui.Theme.Accent.WithAlpha(210), radius: 4f);
                canvas.FillRect(110, 20, 34, 56, ui.Theme.Accent.WithAlpha(170), radius: 4f);
                canvas.DrawText("Custom drawing", 18, 16, color: ui.Theme.TextSecondary);
            });
        }),

        new("splitter", "Splitter", "Layout", 440, 146, (ui, _) =>
        {
            float left = 150f;
            using (ui.Horizontal())
            {
                using (ui.Width(left))
                    ui.Panel(ui.AvailableWidth, 88f, panel => panel.Label("Left pane"));

                ui.Splitter("main", ref left, 100f, 240f, thickness: 8f, height: 88f);

                using (ui.Width(ui.AvailableWidth))
                    ui.Panel(ui.AvailableWidth, 88f, panel => panel.Label("Right pane"));
            }
        }, Mouse: new Vector2(176, 42)),

        new("scroll-area", "ScrollArea", "Layout", 360, 164, (ui, _) =>
        {
            ui.ScrollArea("items", 260f, 104f, area =>
            {
                for (int i = 1; i <= 8; i++)
                    area.Button($"Action {i}", width: area.AvailableWidth);
            });
        }),

        new("scroll-area-both", "ScrollAreaBoth", "Layout", 380, 168, (ui, _) =>
        {
            ui.ScrollAreaBoth("canvas", 280f, 108f, area =>
            {
                area.Canvas(440f, 180f, canvas =>
                {
                    canvas.DrawRect(0, 0, canvas.Width, canvas.Height, ui.Theme.PlotBg, ui.Theme.PlotBorder, ui.Theme.BorderWidth, ui.Theme.BorderRadius);
                    canvas.DrawText("Oversized content", 18, 16, color: ui.Theme.TextSecondary);
                    for (int i = 0; i < 5; i++)
                        canvas.FillRect(24 + i * 72, 58 + i * 10, 46, 36, ui.Theme.Accent.WithAlpha((byte)(230 - i * 24)), radius: 5f);
                });
            });
        }),

        new("image", "Image", "Media", 260, 132, (ui, context) =>
        {
            ui.Image(context.CheckerTexture, 96f, 72f);
        }),

        new("tab-bar", "TabBar", "Navigation", 420, 154, (ui, _) =>
        {
            ui.TabBar("sections", tabs =>
            {
                tabs.Tab("Overview", page => page.Label("Summary metrics"));
                tabs.Tab("Details", page => page.Label("Detailed view"));
                tabs.Tab("Logs", page => page.Label("Recent events"));
            });
        }),

        new("tree", "Tree", "Navigation", 360, 154, (ui, _) =>
        {
            ui.TreeNode("Project", tree =>
            {
                tree.TreeLeaf("src", selected: true);
                tree.TreeLeaf("docs");
                tree.TreeLeaf("tests");
            }, defaultOpen: true);
        }),

        new("collapsing-header", "CollapsingHeader", "Navigation", 380, 126, (ui, _) =>
        {
            bool open = true;
            if (ui.CollapsingHeader("Advanced", ref open, width: 280f).Opened || open)
                ui.Label("Hidden settings appear here.", color: ui.Theme.TextSecondary);
        }),

        new("menu-bar", "MenuBar", "Menus", 420, 92, (ui, _) =>
        {
            ui.MenuBar(320f, bar =>
            {
                bar.Menu("File", menu =>
                {
                    menu.MenuItem("Open");
                    menu.MenuItem("Save");
                });
                bar.Menu("Edit", menu =>
                {
                    menu.MenuItem("Undo");
                    menu.MenuItem("Redo");
                });
            });
        }),

        new("window", "Window", "Windows", 460, 250, (ui, _) =>
        {
            var state = new WindowState(new Vector2(32f, 30f), new Vector2(300f, 160f));
            ui.Window("inspector", "Inspector", state, 300f, window =>
            {
                string entityName = "Camera";
                window.Label("Selected entity", color: window.Theme.Accent);
                window.TextField("entity", ref entityName, window.AvailableWidth);
                bool visible = true;
                window.Checkbox("Visible", ref visible);
            }, resizable: true);
        }),
    ];
}
