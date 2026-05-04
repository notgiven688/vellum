# Vellum

Immediate-mode GUI library for C# with a small renderer API and built-in text rendering.

- Documentation: https://notgiven688.github.io/vellum/docs/
- Interactive demo: https://notgiven688.github.io/vellum/
- Repository: https://github.com/notgiven688/vellum

Vellum is backend-neutral. Application code builds UI with `Ui`; a host backend implements `Vellum.Rendering.IRenderer` and turns `RenderList` data into graphics API calls.

Basic usage:

```csharp
using System.Numerics;
using Vellum;
using Vellum.Rendering;

IRenderer renderer = /* your backend */;
using var ui = new Ui(renderer)
{
    Theme = ThemePresets.Dark()
};

var state = new AppState();

ui.Frame(width, height, mouse, input, state, static (root, state) =>
{
    root.Panel(320f, state, static (panel, state) =>
    {
        panel.Heading("Hello");

        if (panel.Button("Increment").Clicked)
            state.Clicks++;

        panel.Label($"Clicks: {state.Clicks}");
    });
});

sealed class AppState
{
    public int Clicks;
}
```

The docs cover widget identity, `UiId`, scope forms, text/font behavior, and backend implementation.
