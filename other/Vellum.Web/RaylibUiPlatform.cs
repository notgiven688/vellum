using Raylib_cs;

namespace Vellum.Web;

internal sealed class RaylibUiPlatform : IUiPlatform
{
    private MouseCursor _currentCursor = MouseCursor.Arrow;

    public string GetClipboardText() => Raylib.GetClipboardText_();

    public void SetClipboardText(string text) => Raylib.SetClipboardText(text);

    public void SetCursor(UiCursor cursor)
    {
        MouseCursor raylibCursor = cursor switch
        {
            UiCursor.IBeam => MouseCursor.IBeam,
            UiCursor.PointingHand => MouseCursor.PointingHand,
            UiCursor.ResizeNWSE => MouseCursor.ResizeNwse,
            UiCursor.ResizeEW => MouseCursor.ResizeEw,
            UiCursor.ResizeNS => MouseCursor.ResizeNs,
            _ => MouseCursor.Arrow
        };

        if (raylibCursor == _currentCursor) return;

        Raylib.SetMouseCursor(raylibCursor);
        _currentCursor = raylibCursor;
    }
}
