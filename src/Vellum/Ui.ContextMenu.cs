using System.Numerics;

namespace Vellum;

public sealed partial class Ui
{
    private sealed class ContextMenuState
    {
        public Vector2 Anchor;
    }

    /// <summary>Opens and declares a context menu for a target response on right-click.</summary>
    public bool ContextMenu(UiId id, Response target, Action<Ui> content, float width = 220f, float maxHeight = 280f)
        => ContextMenu(id, target, new UiActionState(content), static (ui, state) => state.Content(ui), width, maxHeight);

    /// <summary>Opens and declares a context menu with explicit state passed to the content callback.</summary>
    /// <remarks>
    /// Use this overload with a <c>static</c> lambda to avoid capturing
    /// application state in delayed context menu content.
    /// </remarks>
    public bool ContextMenu<TState>(
        UiId id,
        Response target,
        TState state,
        Action<Ui, TState> content,
        float width = 220f,
        float maxHeight = 280f)
    {
        ArgumentNullException.ThrowIfNull(content);

        int popupWidgetId = MakePopupId(id);
        var ctxState = GetState<ContextMenuState>(MakeChildId(popupWidgetId, "ctx-anchor"));

        if (target.Hovered && IsMousePressed(UiMouseButton.Right))
        {
            ctxState.Anchor = _mouse;
            OpenPopup(id);
        }

        SeedMenuPopupContentHeightIfNeeded(popupWidgetId, width, state, content);

        return Popup(popupWidgetId, ctxState.Anchor.X, ctxState.Anchor.Y, width, maxHeight, state, content, zeroItemSpacing: true);
    }
}
