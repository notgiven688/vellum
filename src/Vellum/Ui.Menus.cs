namespace Vellum;

public sealed partial class Ui
{
    private const float DefaultMenuPopupMinWidth = 180f;
    private const float SubmenuPopupGap = 4f;
    private const float MenuBridgePadding = 6f;

    private readonly struct UiActionState
    {
        public readonly Action<Ui> Content;

        public UiActionState(Action<Ui> content)
        {
            ArgumentNullException.ThrowIfNull(content);
            Content = content;
        }
    }

    /// <summary>Draws a menu bar using the current available width.</summary>
    public Response MenuBar(Action<Ui> content)
        => MenuBar(AvailableWidth, new UiActionState(content), static (ui, state) => state.Content(ui));

    /// <summary>Draws a menu bar with an explicit width.</summary>
    public Response MenuBar(float width, Action<Ui> content)
        => MenuBar(width, new UiActionState(content), static (ui, state) => state.Content(ui));

    /// <inheritdoc cref="MenuBar(Action{Ui})" />
    public Response MenuBar<TState>(TState state, Action<Ui, TState> content)
        => MenuBar(AvailableWidth, state, content);

    /// <inheritdoc cref="MenuBar(float, Action{Ui})" />
    public Response MenuBar<TState>(float width, TState state, Action<Ui, TState> content)
    {
        ArgumentNullException.ThrowIfNull(content);

        float resolvedWidth = MathF.Max(0, width);
        var (x, y) = Place(resolvedWidth, 0);
        float border = FrameBorderWidth;
        var pad = Theme.MenuBarPadding;
        float innerX = x + border + pad.Left;
        float innerY = y + border + pad.Top;
        float innerW = MathF.Max(0, resolvedWidth - border * 2 - pad.Horizontal);

        var parentPainter = _painter;
        var contentPainter = AcquireDeferredPainter();
        _painter = contentPainter;

        _layouts.Add(new LayoutScope
        {
            OriginX = innerX,
            OriginY = innerY,
            CursorX = innerX,
            CursorY = innerY,
            Dir = LayoutDir.Horizontal,
            WidthConstraint = innerW,
            HasWidthConstraint = true,
            Empty = true
        });

        float innerH;
        bool contentCompleted = false;
        try
        {
            content(this, state);

            var inner = _layouts[^1];
            innerH = inner.Dir == LayoutDir.Horizontal
                ? inner.MaxExtent
                : inner.CursorY - inner.OriginY;
            contentCompleted = true;
        }
        finally
        {
            _layouts.RemoveAt(_layouts.Count - 1);
            _painter = parentPainter;

            if (!contentCompleted)
                ReleaseDeferredPainter(contentPainter);
        }

        float resolvedHeight = MathF.Max(0, innerH + border * 2 + pad.Vertical);
        bool hover = PointIn(x, y, resolvedWidth, resolvedHeight);

        DrawFrameRect(x, y, resolvedWidth, resolvedHeight, Theme.PanelBg, Theme.PanelBorder);
        _painter.Append(contentPainter.RenderList);
        ReleaseDeferredPainter(contentPainter);

        Advance(resolvedWidth, resolvedHeight);
        return new Response(x, y, resolvedWidth, resolvedHeight, hover, false, false);
    }

    /// <summary>Draws a menu item that opens a popup containing nested menu content.</summary>
    public Response Menu(
        string label,
        Action<Ui> content,
        float? width = null,
        float? popupWidth = null,
        float? size = null,
        float maxPopupHeight = 280f,
        bool enabled = true,
        bool openOnHover = false,
        bool openToSide = false,
        UiId? id = null)
        => Menu(
            label,
            new UiActionState(content),
            static (ui, state) => state.Content(ui),
            width,
            popupWidth,
            size,
            maxPopupHeight,
            enabled,
            openOnHover,
            openToSide,
            id);

    /// <inheritdoc cref="Menu(string, Action{Ui}, float?, float?, float?, float, bool, bool, bool, UiId?)" />
    public Response Menu<TState>(
        string label,
        TState state,
        Action<Ui, TState> content,
        float? width = null,
        float? popupWidth = null,
        float? size = null,
        float maxPopupHeight = 280f,
        bool enabled = true,
        bool openOnHover = false,
        bool openToSide = false,
        UiId? id = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        enabled = ResolveEnabled(enabled);
        bool topLevel = _popupContext.Count == 0;
        bool sidePopup = !topLevel || openToSide;
        float resolvedSize = size ?? DefaultFontSize;
        var pad = sidePopup ? Theme.MenuItemPadding : Theme.MenuBarItemPadding;
        var labelLayout = LayoutText(label, resolvedSize);
        float arrowSize = MathF.Max(8f, resolvedSize * 0.55f);
        float arrowGap = sidePopup ? Theme.Gap : 0f;
        float intrinsicW = labelLayout.Width + pad.Horizontal + (sidePopup ? arrowSize + arrowGap : 0f);
        float resolvedHeight = labelLayout.Height + pad.Vertical;

        if (_menuMeasureOnly)
        {
            float measuredWidth = width.HasValue
                ? sidePopup
                    ? MathF.Max(0, width.Value)
                    : MathF.Max(width.Value, intrinsicW)
                : _menuMeasureIntrinsicWidth
                    ? intrinsicW
                    : topLevel
                        ? sidePopup
                            ? Math.Max(AvailableWidth, intrinsicW)
                            : intrinsicW
                        : MathF.Max(AvailableWidth, intrinsicW);
            var (measureX, measureY) = Place(measuredWidth, resolvedHeight);
            Advance(measuredWidth, resolvedHeight);
            return new Response(measureX, measureY, measuredWidth, resolvedHeight, false, false, false, disabled: !enabled);
        }

        float resolvedWidth = width.HasValue
            ? sidePopup
                ? MathF.Max(0, width.Value)
                : MathF.Max(width.Value, intrinsicW)
            : topLevel
                ? sidePopup
                    ? Math.Max(AvailableWidth, intrinsicW)
                    : intrinsicW
                : MathF.Max(AvailableWidth, intrinsicW);
        var (x, y) = Place(resolvedWidth, resolvedHeight);

        TextLayoutResult displayLabelLayout = labelLayout;
        if (sidePopup)
        {
            float labelMaxW = MathF.Max(0, resolvedWidth - pad.Horizontal - arrowSize - arrowGap);
            displayLabelLayout = LayoutText(label, resolvedSize, maxWidth: labelMaxW, overflow: TextOverflowMode.Ellipsis);
        }

        UiId resolvedId = ResolveWidgetId(id, label);
        int widgetId = MakeWidgetId(UiWidgetKind.Menu, resolvedId);
        int popupWidgetId = MakeChildId(widgetId, "menu");
        _menuPopupIds.Add(popupWidgetId);
        bool popupOpen = IsPopupOpen(popupWidgetId);
        bool menuRootActive = topLevel && IsRootMenuPopupActive();

        bool focused = RegisterFocusable(widgetId, enabled);
        bool hover = enabled && (menuRootActive
            ? PointInMenuAnchor(x, y, resolvedWidth, resolvedHeight)
            : PointIn(x, y, resolvedWidth, resolvedHeight));
        if (hover) _hotId = widgetId;
        if (hover || popupOpen) RequestCursor(UiCursor.PointingHand);

        if (enabled && _hotId == widgetId && IsMousePressed(UiMouseButton.Left))
        {
            _activeId = widgetId;
            SetFocus(widgetId);
            focused = true;
        }

        bool pressed = enabled && _activeId == widgetId && IsMouseDown(UiMouseButton.Left);
        bool clicked = enabled && IsMouseReleased(UiMouseButton.Left) && _activeId == widgetId && _hotId == widgetId;
        if (enabled && focused && (_input.IsPressed(UiKey.Enter) || _input.IsPressed(UiKey.Space)))
            clicked = true;

        bool openedThisFrame = false;
        bool closedThisFrame = false;
        bool popupTransitionHovered = popupOpen && IsMenuPopupBridgeHovered(x, y, resolvedWidth, resolvedHeight, popupWidgetId);
        bool popupPathHovered = popupOpen && IsPopupHierarchyHoveredOrBridged(popupWidgetId);

        bool allowHoverOpen = !topLevel || menuRootActive || openOnHover;
        if (enabled && !popupOpen && hover && allowHoverOpen)
        {
            OpenPopupById(popupWidgetId);
            popupOpen = true;
            openedThisFrame = true;
            popupTransitionHovered = false;
            popupPathHovered = false;
        }

        if (clicked)
        {
            if (popupOpen)
            {
                ClosePopupById(popupWidgetId);
                popupOpen = false;
                closedThisFrame = true;
            }
            else
            {
                OpenPopupById(popupWidgetId);
                popupOpen = true;
                openedThisFrame = true;
            }
        }

        if (enabled && focused && popupOpen && _input.IsPressed(UiKey.Escape))
        {
            ClosePopupById(popupWidgetId);
            popupOpen = false;
            closedThisFrame = true;
        }

        if (enabled &&
            topLevel &&
            openOnHover &&
            popupOpen &&
            !openedThisFrame &&
            !hover &&
            !popupTransitionHovered &&
            !popupPathHovered)
        {
            ClosePopupById(popupWidgetId);
            popupOpen = false;
            closedThisFrame = true;
        }

        if (!sidePopup)
        {
            var visuals = GetMenuBarItemVisuals(enabled, hover, pressed, popupOpen, focused);
            float strokeWidth = visuals.Border.A > 0 ? FrameBorderWidth : 0f;
            _painter.DrawRect(x, y, resolvedWidth, resolvedHeight, visuals.Fill, visuals.Border, strokeWidth, FrameRadius);

            float textX = x + MathF.Max(pad.Left, (resolvedWidth - displayLabelLayout.Width) * 0.5f);
            DrawTextLayout(displayLabelLayout, textX, y + pad.Top, visuals.Foreground);
        }
        else
        {
            var visuals = GetSelectableVisuals(enabled, hover, pressed, popupOpen, focused);
            float strokeWidth = focused && visuals.Border.A > 0 ? FrameBorderWidth : 0f;
            _painter.DrawRect(x, y, resolvedWidth, resolvedHeight, visuals.Fill, focused ? visuals.Border : default, strokeWidth, FrameRadius);

            float textX = x + pad.Left;
            DrawTextLayout(displayLabelLayout, textX, y + pad.Top, visuals.Foreground);
            DrawChevron(
                x + resolvedWidth - pad.Right - arrowSize,
                y + (resolvedHeight - arrowSize) * 0.5f,
                arrowSize,
                down: false,
                visuals.Foreground);
        }

        Advance(resolvedWidth, resolvedHeight);

        if (popupOpen)
        {
            bool shouldRenderPopup = topLevel || openedThisFrame || hover || popupTransitionHovered || popupPathHovered;
            if (shouldRenderPopup)
            {
                float resolvedPopupWidth;
                if (!popupWidth.HasValue && sidePopup)
                {
                    resolvedPopupWidth = MeasureAutoMenuPopupWidth(popupWidgetId, maxPopupHeight, state, content);
                }
                else
                {
                    resolvedPopupWidth = ResolveMenuPopupWidth(popupWidth, resolvedWidth, matchAnchor: !sidePopup);
                    SeedMenuPopupContentHeightIfNeeded(popupWidgetId, resolvedPopupWidth, state, content);
                }

                float popupAnchorX = ResolveMenuPopupAnchorX(!sidePopup, x, resolvedWidth, resolvedPopupWidth);
                float popupAnchorY = sidePopup ? y : y + resolvedHeight;

                Popup(popupWidgetId, popupAnchorX, popupAnchorY, resolvedPopupWidth, maxPopupHeight, state, content, enabled, zeroItemSpacing: true);
            }
        }

        return new Response(
            x,
            y,
            resolvedWidth,
            resolvedHeight,
            hover,
            pressed,
            clicked,
            focused: focused,
            disabled: !enabled,
            opened: openedThisFrame,
            closed: closedThisFrame);
    }

    /// <summary>Draws a separator inside a menu or menu popup.</summary>
    public Response MenuSeparator(float? length = null, float thickness = 1f, Rendering.Color? color = null)
        => Separator(length ?? AvailableWidth, thickness, color);

    private ControlVisuals GetMenuBarItemVisuals(bool enabled, bool hover, bool pressed, bool open, bool focused)
    {
        Rendering.Color fill = !enabled ? Theme.PanelBg.WithAlpha(110)
            : pressed && hover ? Theme.PanelBg.WithAlpha(230)
            : hover ? Theme.PanelBg.WithAlpha(185)
            : open ? Theme.PanelBg.WithAlpha(145)
            : default;
        Rendering.Color border = !enabled ? Theme.Separator.WithAlpha(110)
            : focused ? Theme.FocusBorder
            : pressed && hover ? Theme.Separator.WithAlpha(225)
            : hover ? Theme.Separator.WithAlpha(180)
            : open ? Theme.Separator.WithAlpha(160)
            : default;
        Rendering.Color foreground = enabled ? Theme.TextPrimary : Theme.TextPrimary.WithAlpha(140);
        return new ControlVisuals(fill, border, foreground);
    }

    private bool IsRootMenuPopupActive()
        => _openPopupIds.Count > 0 && _menuPopupIds.Contains(_openPopupIds[0]);

    private float MeasureAutoMenuPopupWidth<TState>(int popupId, float maxPopupHeight, TState state, Action<Ui, TState> content)
    {
        float border = FrameBorderWidth;
        var pad = Theme.PopupPadding;
        float maxOuterH = MathF.Min(MathF.Max(0, maxPopupHeight), _vpH);
        float minOuterH = border * 2 + pad.Vertical;
        if (maxOuterH < minOuterH) maxOuterH = minOuterH;

        MeasureMenuPopupContent(popupId, state, content, null, useIntrinsicWidths: true,
            out float contentWidth, out float contentHeight);

        float resolvedPopupWidth = MathF.Max(DefaultMenuPopupMinWidth, contentWidth + border * 2 + pad.Horizontal);
        bool showScrollbar = contentHeight + border * 2 + pad.Vertical > maxOuterH + 0.5f;
        if (showScrollbar)
            resolvedPopupWidth += Theme.ScrollbarWidth + PopupScrollbarGap;

        resolvedPopupWidth = MathF.Min(MathF.Max(0, resolvedPopupWidth), _vpW);

        float innerWidth = MathF.Max(0, resolvedPopupWidth - border * 2 - pad.Horizontal);
        if (showScrollbar)
            innerWidth = MathF.Max(0, innerWidth - Theme.ScrollbarWidth - PopupScrollbarGap);

        MeasureMenuPopupContent(popupId, state, content, innerWidth, useIntrinsicWidths: false,
            out _, out contentHeight);
        GetState<PopupState>(popupId).ContentHeight = contentHeight;

        return resolvedPopupWidth;
    }

    private void MeasureMenuPopupContent<TState>(
        int popupId,
        TState state,
        Action<Ui, TState> content,
        float? innerWidth,
        bool useIntrinsicWidths,
        out float contentWidth,
        out float contentHeight)
    {
        var parentPainter = _painter;
        var measurePainter = new Rendering.Painter();
        parentPainter.CopyClipStackTo(measurePainter);
        _painter = measurePainter;

        bool previousMenuMeasureOnly = _menuMeasureOnly;
        bool previousMenuMeasureIntrinsicWidth = _menuMeasureIntrinsicWidth;
        _menuMeasureOnly = true;
        _menuMeasureIntrinsicWidth = useIntrinsicWidths;
#if DEBUG
        _idTrackingDisabledDepth++;
#endif
        _popupContext.Add(popupId);
        _idStack.Push(popupId);
        _layouts.Add(new LayoutScope
        {
            OriginX = 0,
            OriginY = 0,
            CursorX = 0,
            CursorY = 0,
            Dir = LayoutDir.Vertical,
            WidthConstraint = innerWidth ?? 0f,
            HasWidthConstraint = innerWidth.HasValue,
            Empty = true
        });

        try
        {
            ItemSpacing(0);
            content(this, state);

            var inner = _layouts[^1];
            contentWidth = inner.Dir == LayoutDir.Horizontal
                ? inner.CursorX - inner.OriginX
                : inner.MaxExtent;
            contentHeight = inner.Dir == LayoutDir.Horizontal
                ? inner.MaxExtent
                : inner.CursorY - inner.OriginY;
        }
        finally
        {
            _layouts.RemoveAt(_layouts.Count - 1);
            _idStack.Pop();
            _popupContext.RemoveAt(_popupContext.Count - 1);
#if DEBUG
            _idTrackingDisabledDepth--;
#endif
            _menuMeasureIntrinsicWidth = previousMenuMeasureIntrinsicWidth;
            _menuMeasureOnly = previousMenuMeasureOnly;
            _painter = parentPainter;
        }
    }

    private void SeedMenuPopupContentHeightIfNeeded<TState>(int popupId, float popupWidth, TState state, Action<Ui, TState> content)
    {
        var popupState = GetState<PopupState>(popupId);
        if (popupState.ContentHeight > 0.01f)
            return;

        float border = FrameBorderWidth;
        var pad = Theme.PopupPadding;
        float resolvedPopupWidth = MathF.Min(MathF.Max(0, popupWidth), _vpW);
        float innerW = MathF.Max(0, resolvedPopupWidth - border * 2 - pad.Horizontal);
        MeasureMenuPopupContent(popupId, state, content, innerW, useIntrinsicWidths: false,
            out _, out popupState.ContentHeight);
    }

    private bool PointInMenuAnchor(float x, float y, float width, float height)
    {
        if (width <= 0 || height <= 0)
            return false;

        if (_mouse.X < x || _mouse.X >= x + width || _mouse.Y < y || _mouse.Y >= y + height)
            return false;

        if (!MouseInHitClip() || _popupDismissedThisPress)
            return false;

        int topHitWindowId = GetTopHitWindowId();
        if (_windowContextId == 0)
            return topHitWindowId == 0;

        return topHitWindowId == 0 || topHitWindowId == _windowContextId;
    }

    private bool IsMenuPopupBridgeHovered(float x, float y, float width, float height, int popupId)
    {
        if (!TryGetKnownPopupRect(popupId, out var popupRect))
            return false;

        var anchorRect = new ClipRect(x, y, width, height);
        return PointInMenuBridge(anchorRect, popupRect);
    }

    private bool IsPopupHierarchyHoveredOrBridged(int popupId)
    {
        int popupIndex = _openPopupIds.IndexOf(popupId);
        if (popupIndex < 0)
            return false;

        bool hasPreviousRect = false;
        ClipRect previousRect = default;
        for (int i = popupIndex; i < _openPopupIds.Count; i++)
        {
            if (!TryGetKnownPopupRect(_openPopupIds[i], out var rect))
                continue;

            if (PointInRect(rect, _mouse))
                return true;

            if (hasPreviousRect && PointInMenuBridge(previousRect, rect))
                return true;

            previousRect = rect;
            hasPreviousRect = true;
        }

        return false;
    }

    private bool PointInMenuBridge(ClipRect from, ClipRect to)
    {
        if (from.W <= 0 || from.H <= 0 || to.W <= 0 || to.H <= 0 || _popupDismissedThisPress)
            return false;

        float fromRight = from.X + from.W;
        float fromBottom = from.Y + from.H;
        float toRight = to.X + to.W;
        float toBottom = to.Y + to.H;

        float x;
        float y;
        float width;
        float height;

        if (to.X >= fromRight)
        {
            x = fromRight - MenuBridgePadding;
            width = (to.X - fromRight) + MenuBridgePadding * 2f;
            y = MathF.Min(from.Y, to.Y) - MenuBridgePadding;
            height = MathF.Max(fromBottom, toBottom) - MathF.Min(from.Y, to.Y) + MenuBridgePadding * 2f;
        }
        else if (toRight <= from.X)
        {
            x = toRight - MenuBridgePadding;
            width = (from.X - toRight) + MenuBridgePadding * 2f;
            y = MathF.Min(from.Y, to.Y) - MenuBridgePadding;
            height = MathF.Max(fromBottom, toBottom) - MathF.Min(from.Y, to.Y) + MenuBridgePadding * 2f;
        }
        else if (to.Y >= fromBottom)
        {
            x = MathF.Min(from.X, to.X) - MenuBridgePadding;
            width = MathF.Max(fromRight, toRight) - MathF.Min(from.X, to.X) + MenuBridgePadding * 2f;
            y = fromBottom - MenuBridgePadding;
            height = (to.Y - fromBottom) + MenuBridgePadding * 2f;
        }
        else if (toBottom <= from.Y)
        {
            x = MathF.Min(from.X, to.X) - MenuBridgePadding;
            width = MathF.Max(fromRight, toRight) - MathF.Min(from.X, to.X) + MenuBridgePadding * 2f;
            y = toBottom - MenuBridgePadding;
            height = (from.Y - toBottom) + MenuBridgePadding * 2f;
        }
        else
        {
            return false;
        }

        return x <= _mouse.X &&
               _mouse.X < x + width &&
               y <= _mouse.Y &&
               _mouse.Y < y + height;
    }

    private float ResolveMenuPopupWidth(float? popupWidth, float anchorWidth, bool matchAnchor)
        => popupWidth.HasValue
            ? MathF.Max(0, popupWidth.Value)
            : matchAnchor
                ? MathF.Max(DefaultMenuPopupMinWidth, anchorWidth)
                : DefaultMenuPopupMinWidth;

    private float ResolveMenuPopupAnchorX(bool topLevel, float x, float anchorWidth, float popupWidth)
    {
        if (topLevel)
        {
            if (x + popupWidth > _vpW && x + anchorWidth - popupWidth >= 0)
                return x + anchorWidth - popupWidth;

            return x;
        }

        float anchorX = x + anchorWidth + SubmenuPopupGap;
        if (anchorX + popupWidth > _vpW && x - popupWidth - SubmenuPopupGap >= 0)
            return x - popupWidth - SubmenuPopupGap;

        return anchorX;
    }
}
