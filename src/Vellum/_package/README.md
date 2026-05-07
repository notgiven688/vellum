# Vellum

Immediate-mode GUI library for C# with a small renderer API and built-in text rendering.

Install:

```bash
dotnet add package VellumUI
```

Vellum is backend-neutral. Application code builds UI with `Ui`; a host backend owns the window, collects input, implements `Vellum.Rendering.IRenderer`, and turns `RenderList` data into graphics API calls. The package ships the core UI library, not a ready-made desktop host.

Basic usage:

```csharp
using System.Numerics;
using Vellum;
using Vellum.Rendering;

IRenderer renderer = /* your backend */;
var ui = new Ui(renderer)
{
    Theme = ThemePresets.Dark()
};

var state = new AppState();

while (app.Running)
{
    Vector2 mouse = app.MousePosition;
    UiInputState input = app.BuildUiInput();

    ui.Frame(app.Width, app.Height, mouse, input, state, static (root, state) =>
    {
        root.FillViewport(root.Theme.SurfaceBg);

        root.Panel(320f, state, static (panel, state) =>
        {
            panel.Heading("Hello");

            if (panel.Button("Increment").Clicked)
                state.Clicks++;

            panel.Label($"Clicks: {state.Clicks}");
        });
    });

    bool wantsMouse = ui.WantsCaptureMouse;
    bool wantsKeyboard = ui.WantsCaptureKeyboard;
}

sealed class AppState
{
    public int Clicks;
}
```

The current widget set includes labels, panels, buttons, checkboxes, switches, radio buttons, selectables, combo boxes, sliders, drag controls, color pickers, text fields, text areas, progress bars, histograms, spinners, scroll areas, tables, tabs, tree views, menu bars, cascading menus, popups, tooltips, movable windows, dock spaces, and custom canvas drawing.

Windows keep their persistent state in `WindowState`. Docking is opt-in through `DockingState`:

```csharp
var docking = new DockingState();
var inspector = new WindowState(new Vector2(40f, 40f), new Vector2(320f, 220f));

ui.Docking = docking;

ui.Frame(width, height, mouse, input, root =>
{
    root.DockSpace("main-dock", root.AvailableWidth, 360f);

    root.Window("Inspector", inspector, 320f, body =>
    {
        body.Label("Selected entity");
    }, resizable: true);
});
```

Vellum includes a lightweight TrueType loader, glyph rasterizer, atlas manager, and default embedded font. Custom fonts can be assigned with `TrueTypeFont.FromFile(...)`.

Implement `IRenderer` to connect Vellum to your graphics backend.
