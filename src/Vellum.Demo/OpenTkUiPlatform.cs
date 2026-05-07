using Vellum;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Vellum.Demo;

internal sealed class OpenTkUiPlatform : IUiPlatform
{
    private readonly NativeWindow _window;

    public OpenTkUiPlatform(NativeWindow window)
    {
        _window = window;
    }

    public string GetClipboardText() => _window.ClipboardString;

    public void SetClipboardText(string text)
    {
        _window.ClipboardString = text;
    }

    public void SetCursor(UiCursor cursor)
    {
        MouseCursor mouseCursor = cursor switch
        {
            UiCursor.IBeam => MouseCursor.IBeam,
            UiCursor.PointingHand => MouseCursor.PointingHand,
            UiCursor.ResizeEW => MouseCursor.ResizeEW,
            UiCursor.ResizeNS => MouseCursor.ResizeNS,
            // GLFW diagonal resize cursors require newer GLFW builds. Fall back
            // to a common resize cursor so older runtime libraries do not throw.
            UiCursor.ResizeNWSE => MouseCursor.ResizeEW,
            _ => MouseCursor.Default
        };

        try
        {
            _window.Cursor = mouseCursor;
        }
        catch (GLFWException) when (mouseCursor != MouseCursor.Default)
        {
            _window.Cursor = MouseCursor.Default;
        }
    }
}
