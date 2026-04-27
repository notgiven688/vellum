using System.Numerics;

namespace Vellum;

public sealed partial class Ui
{
    private sealed class ContextMenuState
    {
        public Vector2 Anchor;
    }

    /// <summary>Opens and declares a context menu for a target response on right-click.</summary>
    public bool ContextMenu(string id, Response target, Action<Ui> content, float width = 220f, float maxHeight = 280f)
        => ContextMenu(id, target, new UiActionState(content), static (ui, state) => state.Content(ui), width, maxHeight);

    /// <inheritdoc cref="ContextMenu(string, Response, Action{Ui}, float, float)" />
    public bool ContextMenu<TState>(
        string id,
        Response target,
        TState state,
        Action<Ui, TState> content,
        float width = 220f,
        float maxHeight = 280f)
    {
        ArgumentNullException.ThrowIfNull(content);

        var ctxState = GetState<ContextMenuState>(MakeId(id + "/ctx-anchor"));
        int popupWidgetId = MakeId(id);

        if (target.Hovered && IsMousePressed(UiMouseButton.Right))
        {
            ctxState.Anchor = _mouse;
            OpenPopup(id);
        }

        SeedMenuPopupContentHeightIfNeeded(popupWidgetId, width, state, content);

        return Popup(id, ctxState.Anchor.X, ctxState.Anchor.Y, width, maxHeight, popup =>
        {
            popup.ItemSpacing(0);
            content(popup, state);
        });
    }
}
