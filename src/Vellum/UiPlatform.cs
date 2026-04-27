namespace Vellum;

/// <summary>
/// Cursor shapes a platform backend can expose for hovered widgets.
/// </summary>
public enum UiCursor
{
    /// <summary>Default arrow cursor.</summary>
    Arrow,
    /// <summary>Text editing cursor.</summary>
    IBeam,
    /// <summary>Pointing hand cursor for clickable controls.</summary>
    PointingHand,
    /// <summary>Diagonal resize cursor.</summary>
    ResizeNWSE,
    /// <summary>Horizontal resize cursor.</summary>
    ResizeEW
}

/// <summary>
/// Optional host integration for clipboard and cursor changes.
/// </summary>
public interface IUiPlatform
{
    /// <summary>Returns the current clipboard text, or an empty string if unavailable.</summary>
    string GetClipboardText();
    /// <summary>Sets the clipboard text.</summary>
    void SetClipboardText(string text);
    /// <summary>Applies the cursor requested by the current frame.</summary>
    void SetCursor(UiCursor cursor);
}

/// <summary>
/// Platform implementation used when the host does not provide clipboard or cursor integration.
/// </summary>
public sealed class NullUiPlatform : IUiPlatform
{
    /// <summary>Shared no-op platform instance.</summary>
    public static readonly NullUiPlatform Instance = new();

    private NullUiPlatform() { }

    /// <inheritdoc />
    public string GetClipboardText() => string.Empty;

    /// <inheritdoc />
    public void SetClipboardText(string text) { }

    /// <inheritdoc />
    public void SetCursor(UiCursor cursor) { }
}
