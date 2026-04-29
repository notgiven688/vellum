# Quickstart

Vellum is immediate-mode: your application owns state, and every frame redraws the interface from that state.

A host application supplies three things:

- an `IRenderer` implementation;
- mouse position in logical pixels;
- a `UiInputState` for keyboard, text, mouse buttons, wheel, and modifiers.

The OpenTK demo is the reference host and renderer implementation.

## Minimal Frame

```csharp
using System.Numerics;
using Vellum;
using Vellum.Rendering;

IRenderer renderer = /* your backend */;
var ui = new Ui(renderer)
{
    Theme = ThemePresets.Dark()
};

while (app.Running)
{
    Vector2 mouse = app.MousePosition;
    UiInputState input = app.BuildUiInput();

    ui.Frame(app.Width, app.Height, mouse, input, root =>
    {
        root.FillViewport(root.Theme.SurfaceBg);
        root.Label("Hello from Vellum");
    });
}
```

`ui.Frame(...)` begins the frame, runs your UI code, renders through the configured backend, and ends the frame.

## Buttons And State

Keep state in normal C# variables or your application model. Widget return values report what happened this frame.

```csharp
int clicks = 0;
bool enabled = true;
float volume = 0.35f;

ui.Frame(width, height, mouse, input, root =>
{
    root.Panel(320f, panel =>
    {
        panel.Heading("Controls");

        if (panel.Button("Increment").Clicked)
            clicks++;

        panel.Label($"Clicks: {clicks}");
        panel.Checkbox("Enabled", ref enabled);
        panel.Slider("Volume", ref volume, 0f, 1f, width: 240f);
    });
});
```

Most widgets return a `Response`. Common fields are `Hovered`, `Pressed`, `Clicked`, `Focused`, `Changed`, `Disabled`, and the widget rectangle `X`, `Y`, `W`, `H`.

## Layout

Vellum has simple immediate layout scopes. Use panels for framed regions and `Row`, `Column`, `FixedWidth`, and `MaxWidth` to control placement.

```csharp
ui.Frame(width, height, mouse, input, root =>
{
    root.FillViewport(root.Theme.SurfaceBg);

    root.MaxWidth(480f, content =>
    {
        content.Panel(content.AvailableWidth, panel =>
        {
            panel.Heading("Profile");

            panel.Row(row =>
            {
                row.Label("Name");
                row.TextField("name", ref name, width: 260f);
            });

            panel.Row(row =>
            {
                if (row.Button("Save").Clicked)
                    Save(name);

                if (row.Button("Reset").Clicked)
                    name = string.Empty;
            });
        });
    }, align: UiAlign.Center);
});
```

Widget IDs are derived from labels and the current ID scope. When repeated controls use the same label for different data objects, wrap each object in an `Id(...)` scope:

```csharp
foreach (var item in items)
{
    using (ui.Id(item.Id))
    {
        ui.TextField("Name", ref item.Name, width: 260f);
        ui.Button("Delete");
    }
}
```

Callback scopes are also available for the same pattern when that reads better.

When two widgets in the same scope need the same visible label, use a named `id:` override:

```csharp
ui.Button("Save", id: "save-primary");
ui.Button("Save", id: "save-secondary");
```

Debug builds throw when two widgets resolve to the same ID in one frame, so duplicate labels are caught early.

## Windows

Windows keep runtime position, size, collapse, and close state in a `WindowState` value owned by your application.

```csharp
WindowState inspector = new(new Vector2(40f, 40f), new Vector2(360f, 260f));

ui.Frame(width, height, mouse, input, root =>
{
    root.Window("inspector", "Inspector", inspector, width: 360f, body =>
    {
        body.Label("Selected entity");
        body.Separator();
        body.TextField("Name", ref entityName);
        body.Checkbox("Visible", ref entityVisible);
    });
});
```

Store `WindowState` alongside the rest of your application state if you want positions and sizes to persist.

## Text And Fonts

Vellum includes its own TrueType loader, rasterizer, glyph atlas, text fields, and text areas. Backends receive text as textured triangles through `IRenderer`.

```csharp
ui.Font = TrueTypeFont.FromFile("Inter-Regular.ttf");
ui.DefaultFontSize = 18f;

string notes = "";

ui.Frame(width, height, mouse, input, root =>
{
    root.TextArea("Notes", ref notes, width: 420f, height: 180f);
});
```

For HiDPI rendering, pass a `RenderFrameInfo` with logical and framebuffer sizes. Vellum will rasterize text at the framebuffer scale by default.

```csharp
var frame = new RenderFrameInfo(
    logicalWidth,
    logicalHeight,
    framebufferWidth,
    framebufferHeight);

ui.Frame(frame, mouseInLogicalPixels, input, root => DrawUi(root));
```

See [Text and Fonts](guides/text-and-fonts.md) for supported text behavior and current limitations.

## Backend Starting Point

If you are implementing a renderer, start from the OpenTK demo:

Its renderer source shows the full backend contract in one place.

The renderer contract is small:

```csharp
public interface IRenderer
{
    void BeginFrame(RenderFrameInfo frame);
    void Render(RenderList renderList);
    void EndFrame();

    int CreateTexture(byte[] rgba, int width, int height);
    void DestroyTexture(int textureId);
}
```

See [Backend Implementation](guides/backends.md) for the full renderer checklist.
