using Vellum;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;

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
        _window.Cursor = cursor switch
        {
            UiCursor.IBeam => MouseCursor.IBeam,
            UiCursor.PointingHand => MouseCursor.PointingHand,
            UiCursor.ResizeEW => MouseCursor.ResizeEW,
            UiCursor.ResizeNWSE => MouseCursor.ResizeNWSE,
            _ => MouseCursor.Default
        };
    }
}
