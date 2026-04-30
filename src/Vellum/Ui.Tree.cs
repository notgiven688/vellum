using Vellum.Rendering;

namespace Vellum;

public sealed partial class Ui
{
    private sealed class TreeNodeState
    {
        public bool Open;
        public bool Initialized;
    }

    /// <summary>Draws an expandable tree node and renders child content when open.</summary>
    public Response TreeNode(
        string label,
        Action<Ui> children,
        float? size = null,
        bool enabled = true,
        bool defaultOpen = false,
        bool selected = false,
        string? id = null)
        => TreeNode(
            label,
            new UiActionState(children),
            static (ui, state) => state.Content(ui),
            size,
            enabled,
            defaultOpen,
            selected,
            id);

    /// <inheritdoc cref="TreeNode(string, Action{Ui}, float?, bool, bool, bool, string?)" />
    public Response TreeNode<TState>(
        string label,
        TState state,
        Action<Ui, TState> children,
        float? size = null,
        bool enabled = true,
        bool defaultOpen = false,
        bool selected = false,
        string? id = null)
    {
        ArgumentNullException.ThrowIfNull(children);

        enabled = ResolveEnabled(enabled);
        float s = size ?? DefaultFontSize;
        var pad = Theme.TreeNodePadding;
        string resolvedId = id ?? label;
        int focusId = MakeId(resolvedId);
        int widgetId = MakeWidgetId(UiWidgetKind.TreeNode, resolvedId);

        var nodeState = GetState<TreeNodeState>(widgetId);
        if (!nodeState.Initialized)
        {
            nodeState.Open = defaultOpen;
            nodeState.Initialized = true;
        }

        var labelLayout = LayoutText(label, s);
        float arrowSize = MathF.Max(8f, s * 0.55f);
        float arrowGap = MathF.Max(2f, Theme.Gap * 0.5f);
        float rowMinW = arrowSize + arrowGap + labelLayout.Width + pad.Horizontal;
        float w = MathF.Max(rowMinW, AvailableWidth);
        float h = MathF.Max(arrowSize, labelLayout.Height) + pad.Vertical;

        var (x, y) = Place(w, h);

        bool focused = RegisterFocusable(widgetId, enabled, focusId);
        bool hover = enabled && PointIn(x, y, w, h);
        if (hover) _hotId = widgetId;
        if (hover) RequestCursor(UiCursor.PointingHand);

        if (enabled && _hotId == widgetId && IsMousePressed(UiMouseButton.Left))
        {
            _activeId = widgetId;
            SetFocus(widgetId, focusId);
            focused = true;
        }

        bool pressed = enabled && _activeId == widgetId && IsMouseDown(UiMouseButton.Left);
        bool clicked = enabled && IsMouseReleased(UiMouseButton.Left) && _activeId == widgetId && _hotId == widgetId;
        if (enabled && focused && (_input.IsPressed(UiKey.Enter) || _input.IsPressed(UiKey.Space)))
            clicked = true;

        bool toggled = false;
        bool opened = false;
        bool closed = false;
        bool currentOpen = nodeState.Open;
        if (clicked)
        {
            currentOpen = !nodeState.Open;
            nodeState.Open = currentOpen;
            toggled = true;
            opened = currentOpen;
            closed = !currentOpen;
        }

        var visuals = GetSelectableVisuals(enabled, hover, pressed, selected, focused);
        _painter.DrawRect(x, y, w, h, visuals.Fill, default, 0f, FrameRadius);

        float chevronX = x + pad.Left;
        float chevronY = y + (h - arrowSize) * 0.5f;
        DrawChevron(chevronX, chevronY, arrowSize, currentOpen, visuals.Foreground);

        float labelX = chevronX + arrowSize + arrowGap;
        float labelY = y + pad.Top;
        DrawTextLayout(labelLayout, labelX, labelY, visuals.Foreground);

        Advance(w, h);

        if (currentOpen)
            RenderTreeChildren(state, children);

        return new Response(
            x,
            y,
            w,
            h,
            hover,
            pressed,
            clicked,
            focused: focused,
            changed: toggled,
            disabled: !enabled,
            toggled: toggled,
            opened: opened,
            closed: closed);
    }

    /// <summary>Draws a non-expandable tree row.</summary>
    public Response TreeLeaf(
        string label,
        bool selected = false,
        float? size = null,
        bool enabled = true,
        string? id = null)
    {
        enabled = ResolveEnabled(enabled);
        float s = size ?? DefaultFontSize;
        var pad = Theme.TreeNodePadding;
        string resolvedId = id ?? label;
        int focusId = MakeId(resolvedId);
        int widgetId = MakeWidgetId(UiWidgetKind.TreeLeaf, resolvedId);

        var labelLayout = LayoutText(label, s);
        float arrowSize = MathF.Max(8f, s * 0.55f);
        float arrowGap = MathF.Max(2f, Theme.Gap * 0.5f);
        float rowMinW = arrowSize + arrowGap + labelLayout.Width + pad.Horizontal;
        float w = MathF.Max(rowMinW, AvailableWidth);
        float h = MathF.Max(arrowSize, labelLayout.Height) + pad.Vertical;

        var (x, y) = Place(w, h);

        bool focused = RegisterFocusable(widgetId, enabled, focusId);
        bool hover = enabled && PointIn(x, y, w, h);
        if (hover) _hotId = widgetId;
        if (hover) RequestCursor(UiCursor.PointingHand);

        if (enabled && _hotId == widgetId && IsMousePressed(UiMouseButton.Left))
        {
            _activeId = widgetId;
            SetFocus(widgetId, focusId);
            focused = true;
        }

        bool pressed = enabled && _activeId == widgetId && IsMouseDown(UiMouseButton.Left);
        bool clicked = enabled && IsMouseReleased(UiMouseButton.Left) && _activeId == widgetId && _hotId == widgetId;
        if (enabled && focused && (_input.IsPressed(UiKey.Enter) || _input.IsPressed(UiKey.Space)))
            clicked = true;

        var visuals = GetSelectableVisuals(enabled, hover, pressed, selected, focused);
        _painter.DrawRect(x, y, w, h, visuals.Fill, default, 0f, FrameRadius);

        float labelX = x + pad.Left + arrowSize + arrowGap;
        float labelY = y + pad.Top;
        DrawTextLayout(labelLayout, labelX, labelY, visuals.Foreground);

        Advance(w, h);

        return new Response(
            x,
            y,
            w,
            h,
            hover,
            pressed,
            clicked,
            focused: focused,
            changed: clicked,
            disabled: !enabled);
    }

    private void RenderTreeChildren<TState>(TState state, Action<Ui, TState> children)
    {
        float indent = MathF.Max(0, Theme.TreeIndent);
        Spacing(0);

        float parentAvailableWidth = GetAvailableWidth(Top);
        float childWidth = MathF.Max(0, parentAvailableWidth - indent);

        var (childX, childY) = Place(0, 0);
        _layouts.Add(new LayoutScope
        {
            OriginX = childX + indent,
            OriginY = childY,
            CursorX = childX + indent,
            CursorY = childY,
            Dir = LayoutDir.Vertical,
            WidthConstraint = childWidth,
            HasWidthConstraint = true,
            Empty = true,
            DefaultGap = 0f,
            HasDefaultGap = true
        });

        float innerW;
        float innerH;
        try
        {
            children(this, state);

            var inner = _layouts[^1];
            innerW = inner.Dir == LayoutDir.Horizontal
                ? inner.CursorX - inner.OriginX
                : inner.MaxExtent;
            innerH = inner.Dir == LayoutDir.Horizontal
                ? inner.MaxExtent
                : inner.CursorY - inner.OriginY;
        }
        finally
        {
            _layouts.RemoveAt(_layouts.Count - 1);
        }

        Advance(indent + innerW, innerH);
    }
}
