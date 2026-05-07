using System.Numerics;

namespace Vellum.WidgetGallery;

internal sealed record WidgetExample(
    string Id,
    string Title,
    string Category,
    int Width,
    int Height,
    Action<Ui, WidgetExampleContext> Draw,
    Vector2 Mouse = default,
    UiInputState Input = default,
    IReadOnlyList<WidgetExampleFrame>? WarmupFrames = null);

internal readonly record struct WidgetExampleFrame(Vector2 Mouse, UiInputState Input);
