using Vellum.Rendering;

namespace Vellum;

public sealed partial class Ui
{
    private sealed class ScrollAreaState
    {
        public float ScrollX;
        public float ScrollY;
        public float ContentWidth;
        public float ContentHeight;
        public float ContentDragStartMouseX;
        public float ContentDragStartMouseY;
        public float ContentDragStartScrollX;
        public float ContentDragStartScrollY;
        public float ThumbDragOffsetX;
        public float ThumbDragOffsetY;
        public bool DraggingContent;
        public bool DraggingHorizontalThumb;
        public bool DraggingThumb;
    }

    private readonly struct ScrollAreaViewMetrics
    {
        public readonly bool ShowHorizontal;
        public readonly bool ShowVertical;
        public readonly float ViewWidth;
        public readonly float ViewHeight;

        public ScrollAreaViewMetrics(bool showHorizontal, bool showVertical, float viewWidth, float viewHeight)
        {
            ShowHorizontal = showHorizontal;
            ShowVertical = showVertical;
            ViewWidth = viewWidth;
            ViewHeight = viewHeight;
        }
    }

    /// <summary>Draws a vertical scroll area.</summary>
    public Response ScrollArea(UiId id, float width, float height, Action<Ui> content, bool enabled = true)
    {
        enabled = ResolveEnabled(enabled);
        const float ScrollbarGap = 4f;

        id = RequireSpecifiedId(id, nameof(id));
        int widgetId = MakeWidgetId(UiWidgetKind.ScrollArea, id);
        RegisterWidgetId(widgetId, "ScrollArea");
        var state = GetState<ScrollAreaState>(widgetId);
        var (x, y) = Place(width, height);
        float border = FrameBorderWidth;
        float scrollbarRadius = MathF.Min(FrameRadius, Theme.ScrollbarWidth * 0.5f);

        float scrollbarReserve = Theme.ScrollbarWidth + ScrollbarGap;
        float viewX = x + border;
        float viewY = y + border;
        float viewW = MathF.Max(0, width - border * 2 - scrollbarReserve);
        float viewH = MathF.Max(0, height - border * 2);
        float trackX = x + width - border - Theme.ScrollbarWidth;
        float trackY = viewY;
        float trackH = viewH;

        bool hover = enabled && PointIn(x, y, width, height);
        if (hover) _hotId = widgetId;

        bool changed = false;
        if (!enabled)
        {
            state.DraggingThumb = false;
            state.DraggingContent = false;
        }

        if (!IsMouseDown(UiMouseButton.Left))
        {
            state.DraggingThumb = false;
            state.DraggingContent = false;
        }

        float previousContentHeight = MathF.Max(state.ContentHeight, viewH);
        float previousMaxScroll = MathF.Max(0, previousContentHeight - viewH);
        bool hadScrollbar = previousMaxScroll > 0.5f;
        float thumbH = hadScrollbar
            ? MathF.Max(Theme.ScrollbarMinThumbSize, (viewH * viewH) / previousContentHeight)
            : 0;
        float thumbTravel = MathF.Max(0, viewH - thumbH);
        float thumbY = thumbTravel > 0 && previousMaxScroll > 0
            ? viewY + (state.ScrollY / previousMaxScroll) * thumbTravel
            : viewY;
        bool thumbHover = enabled && hadScrollbar && PointIn(trackX, thumbY, Theme.ScrollbarWidth, thumbH);

        bool mousePressed = enabled && IsMousePressed(UiMouseButton.Left);
        if (mousePressed && thumbHover)
        {
            _activeId = widgetId;
            state.DraggingThumb = true;
            state.DraggingContent = false;
            state.ThumbDragOffsetY = _mouse.Y - thumbY;
        }

        if (enabled && hover && _input.WheelDelta.Y != 0 && !state.DraggingThumb && !state.DraggingContent)
        {
            float clampedBefore = Math.Clamp(state.ScrollY, 0, previousMaxScroll);
            float previous = state.ScrollY;
            state.ScrollY = Math.Clamp(clampedBefore - _input.WheelDelta.Y * Theme.ScrollWheelStep, 0, previousMaxScroll);
            changed |= MathF.Abs(state.ScrollY - previous) > 0.01f;
        }

        if (enabled && state.DraggingThumb && _activeId == widgetId && IsMouseDown(UiMouseButton.Left) && thumbTravel > 0 && previousMaxScroll > 0)
        {
            float thumbTop = Math.Clamp(_mouse.Y - state.ThumbDragOffsetY, viewY, viewY + thumbTravel);
            float scrollRatio = (thumbTop - viewY) / thumbTravel;
            float previous = state.ScrollY;
            state.ScrollY = scrollRatio * previousMaxScroll;
            changed |= MathF.Abs(state.ScrollY - previous) > 0.01f;
        }

        if (enabled && state.DraggingContent && _activeId == widgetId && IsMouseDown(UiMouseButton.Left) && previousMaxScroll > 0)
        {
            float deltaY = _mouse.Y - state.ContentDragStartMouseY;
            float previous = state.ScrollY;
            state.ScrollY = Math.Clamp(state.ContentDragStartScrollY - deltaY, 0, previousMaxScroll);
            changed |= MathF.Abs(state.ScrollY - previous) > 0.01f;
        }

        state.ScrollY = Math.Clamp(state.ScrollY, 0, previousMaxScroll);

        DrawFrameRect(x, y, width, height, Theme.ScrollAreaBg, Theme.ScrollAreaBorder);

        _painter.PushClip(viewX, viewY, viewW, viewH);
        PushHitClip(viewX, viewY, viewW, viewH);

        _layouts.Add(new LayoutScope
        {
            OriginX = viewX,
            OriginY = viewY - state.ScrollY,
            CursorX = viewX,
            CursorY = viewY - state.ScrollY,
            Dir = LayoutDir.Vertical,
            WidthConstraint = viewW,
            HasWidthConstraint = true,
            Empty = true
        });

        LayoutScope inner;
        try
        {
            content(this);
            inner = _layouts[^1];
        }
        finally
        {
            _layouts.RemoveAt(_layouts.Count - 1);
            PopHitClip();
            _painter.PopClip();
        }

        float contentWidth = inner.Dir == LayoutDir.Horizontal
            ? inner.CursorX - inner.OriginX
            : inner.MaxExtent;
        float contentHeight = inner.Dir == LayoutDir.Horizontal
            ? inner.MaxExtent
            : inner.CursorY - inner.OriginY;

        state.ContentHeight = contentHeight;
        float maxScroll = MathF.Max(0, contentHeight - viewH);
        float clampedScroll = Math.Clamp(state.ScrollY, 0, maxScroll);
        changed |= MathF.Abs(clampedScroll - state.ScrollY) > 0.01f;
        state.ScrollY = clampedScroll;

        bool showScrollbar = maxScroll > 0.5f;
        bool currentThumbHover = false;
        if (!showScrollbar)
        {
            state.DraggingThumb = false;
            state.DraggingContent = false;
        }
        else
        {
            float currentThumbH = MathF.Max(Theme.ScrollbarMinThumbSize, (viewH * viewH) / MathF.Max(contentHeight, viewH));
            float currentThumbTravel = MathF.Max(0, viewH - currentThumbH);
            float currentThumbY = currentThumbTravel > 0
                ? viewY + (state.ScrollY / maxScroll) * currentThumbTravel
                : viewY;

            currentThumbHover = enabled && PointIn(trackX, currentThumbY, Theme.ScrollbarWidth, currentThumbH);
            if (currentThumbHover || state.DraggingThumb)
                RequestCursor(UiCursor.PointingHand);

            _painter.DrawRect(trackX, trackY, Theme.ScrollbarWidth, trackH, Theme.ScrollbarTrack, radius: scrollbarRadius);

            Color thumbColor = state.DraggingThumb && _activeId == widgetId
                ? Theme.ScrollbarThumbActive
                : currentThumbHover
                    ? Theme.ScrollbarThumbHover
                    : Theme.ScrollbarThumb;
            _painter.DrawRect(trackX, currentThumbY, Theme.ScrollbarWidth, currentThumbH, thumbColor, radius: scrollbarRadius);
        }

        bool viewHover = enabled && showScrollbar && PointIn(viewX, viewY, viewW, viewH);
        bool childClaimedHover = _hotId != 0 && _hotId != widgetId;
        if (mousePressed && viewHover && !currentThumbHover && !state.DraggingThumb && !childClaimedHover)
        {
            _activeId = widgetId;
            state.DraggingContent = true;
            state.ContentDragStartMouseY = _mouse.Y;
            state.ContentDragStartScrollY = state.ScrollY;
        }

        Advance(width, height);
        return new Response(
            x,
            y,
            width,
            height,
            hover,
            enabled && (state.DraggingThumb || state.DraggingContent) && _activeId == widgetId && IsMouseDown(UiMouseButton.Left),
            false,
            changed: changed,
            disabled: !enabled);
    }

    /// <summary>Draws a vertical scroll area with explicit state passed to the content callback.</summary>
    /// <remarks>
    /// Use this overload with a <c>static</c> lambda to avoid capturing
    /// application state while rendering scroll area content.
    /// </remarks>
    public Response ScrollArea<TState>(UiId id, float width, float height, TState contentState, Action<Ui, TState> content, bool enabled = true)
    {
        enabled = ResolveEnabled(enabled);
        const float ScrollbarGap = 4f;

        id = RequireSpecifiedId(id, nameof(id));
        int widgetId = MakeWidgetId(UiWidgetKind.ScrollArea, id);
        RegisterWidgetId(widgetId, "ScrollArea");
        var state = GetState<ScrollAreaState>(widgetId);
        var (x, y) = Place(width, height);
        float border = FrameBorderWidth;
        float scrollbarRadius = MathF.Min(FrameRadius, Theme.ScrollbarWidth * 0.5f);

        float scrollbarReserve = Theme.ScrollbarWidth + ScrollbarGap;
        float viewX = x + border;
        float viewY = y + border;
        float viewW = MathF.Max(0, width - border * 2 - scrollbarReserve);
        float viewH = MathF.Max(0, height - border * 2);
        float trackX = x + width - border - Theme.ScrollbarWidth;
        float trackY = viewY;
        float trackH = viewH;

        bool hover = enabled && PointIn(x, y, width, height);
        if (hover) _hotId = widgetId;

        bool changed = false;
        if (!enabled)
        {
            state.DraggingThumb = false;
            state.DraggingContent = false;
        }

        if (!IsMouseDown(UiMouseButton.Left))
        {
            state.DraggingThumb = false;
            state.DraggingContent = false;
        }

        float previousContentHeight = MathF.Max(state.ContentHeight, viewH);
        float previousMaxScroll = MathF.Max(0, previousContentHeight - viewH);
        bool hadScrollbar = previousMaxScroll > 0.5f;
        float thumbH = hadScrollbar
            ? MathF.Max(Theme.ScrollbarMinThumbSize, (viewH * viewH) / previousContentHeight)
            : 0;
        float thumbTravel = MathF.Max(0, viewH - thumbH);
        float thumbY = thumbTravel > 0 && previousMaxScroll > 0
            ? viewY + (state.ScrollY / previousMaxScroll) * thumbTravel
            : viewY;
        bool thumbHover = enabled && hadScrollbar && PointIn(trackX, thumbY, Theme.ScrollbarWidth, thumbH);

        bool mousePressed = enabled && IsMousePressed(UiMouseButton.Left);
        if (mousePressed && thumbHover)
        {
            _activeId = widgetId;
            state.DraggingThumb = true;
            state.DraggingContent = false;
            state.ThumbDragOffsetY = _mouse.Y - thumbY;
        }

        if (enabled && hover && _input.WheelDelta.Y != 0 && !state.DraggingThumb && !state.DraggingContent)
        {
            float clampedBefore = Math.Clamp(state.ScrollY, 0, previousMaxScroll);
            float previous = state.ScrollY;
            state.ScrollY = Math.Clamp(clampedBefore - _input.WheelDelta.Y * Theme.ScrollWheelStep, 0, previousMaxScroll);
            changed |= MathF.Abs(state.ScrollY - previous) > 0.01f;
        }

        if (enabled && state.DraggingThumb && _activeId == widgetId && IsMouseDown(UiMouseButton.Left) && thumbTravel > 0 && previousMaxScroll > 0)
        {
            float thumbTop = Math.Clamp(_mouse.Y - state.ThumbDragOffsetY, viewY, viewY + thumbTravel);
            float scrollRatio = (thumbTop - viewY) / thumbTravel;
            float previous = state.ScrollY;
            state.ScrollY = scrollRatio * previousMaxScroll;
            changed |= MathF.Abs(state.ScrollY - previous) > 0.01f;
        }

        if (enabled && state.DraggingContent && _activeId == widgetId && IsMouseDown(UiMouseButton.Left) && previousMaxScroll > 0)
        {
            float deltaY = _mouse.Y - state.ContentDragStartMouseY;
            float previous = state.ScrollY;
            state.ScrollY = Math.Clamp(state.ContentDragStartScrollY - deltaY, 0, previousMaxScroll);
            changed |= MathF.Abs(state.ScrollY - previous) > 0.01f;
        }

        state.ScrollY = Math.Clamp(state.ScrollY, 0, previousMaxScroll);

        DrawFrameRect(x, y, width, height, Theme.ScrollAreaBg, Theme.ScrollAreaBorder);

        _painter.PushClip(viewX, viewY, viewW, viewH);
        PushHitClip(viewX, viewY, viewW, viewH);

        _layouts.Add(new LayoutScope
        {
            OriginX = viewX,
            OriginY = viewY - state.ScrollY,
            CursorX = viewX,
            CursorY = viewY - state.ScrollY,
            Dir = LayoutDir.Vertical,
            WidthConstraint = viewW,
            HasWidthConstraint = true,
            Empty = true
        });

        LayoutScope inner;
        try
        {
            content(this, contentState);
            inner = _layouts[^1];
        }
        finally
        {
            _layouts.RemoveAt(_layouts.Count - 1);
            PopHitClip();
            _painter.PopClip();
        }

        float contentWidth = inner.Dir == LayoutDir.Horizontal
            ? inner.CursorX - inner.OriginX
            : inner.MaxExtent;
        float contentHeight = inner.Dir == LayoutDir.Horizontal
            ? inner.MaxExtent
            : inner.CursorY - inner.OriginY;

        state.ContentHeight = contentHeight;
        float maxScroll = MathF.Max(0, contentHeight - viewH);
        float clampedScroll = Math.Clamp(state.ScrollY, 0, maxScroll);
        changed |= MathF.Abs(clampedScroll - state.ScrollY) > 0.01f;
        state.ScrollY = clampedScroll;

        bool showScrollbar = maxScroll > 0.5f;
        bool currentThumbHover = false;
        if (!showScrollbar)
        {
            state.DraggingThumb = false;
            state.DraggingContent = false;
        }
        else
        {
            float currentThumbH = MathF.Max(Theme.ScrollbarMinThumbSize, (viewH * viewH) / MathF.Max(contentHeight, viewH));
            float currentThumbTravel = MathF.Max(0, viewH - currentThumbH);
            float currentThumbY = currentThumbTravel > 0
                ? viewY + (state.ScrollY / maxScroll) * currentThumbTravel
                : viewY;

            currentThumbHover = enabled && PointIn(trackX, currentThumbY, Theme.ScrollbarWidth, currentThumbH);
            if (currentThumbHover || state.DraggingThumb)
                RequestCursor(UiCursor.PointingHand);

            _painter.DrawRect(trackX, trackY, Theme.ScrollbarWidth, trackH, Theme.ScrollbarTrack, radius: scrollbarRadius);

            Color thumbColor = state.DraggingThumb && _activeId == widgetId
                ? Theme.ScrollbarThumbActive
                : currentThumbHover
                    ? Theme.ScrollbarThumbHover
                    : Theme.ScrollbarThumb;
            _painter.DrawRect(trackX, currentThumbY, Theme.ScrollbarWidth, currentThumbH, thumbColor, radius: scrollbarRadius);
        }

        bool viewHover = enabled && showScrollbar && PointIn(viewX, viewY, viewW, viewH);
        bool childClaimedHover = _hotId != 0 && _hotId != widgetId;
        if (mousePressed && viewHover && !currentThumbHover && !state.DraggingThumb && !childClaimedHover)
        {
            _activeId = widgetId;
            state.DraggingContent = true;
            state.ContentDragStartMouseY = _mouse.Y;
            state.ContentDragStartScrollY = state.ScrollY;
        }

        Advance(width, height);
        return new Response(
            x,
            y,
            width,
            height,
            hover,
            enabled && (state.DraggingThumb || state.DraggingContent) && _activeId == widgetId && IsMouseDown(UiMouseButton.Left),
            false,
            changed: changed,
            disabled: !enabled);
    }

    /// <summary>Draws a scroll area with horizontal and vertical scrolling.</summary>
    public Response ScrollAreaBoth(UiId id, float width, float height, Action<Ui> content, bool enabled = true)
        => ScrollAreaBothCore(id, width, height, content, static (ui, callback) => callback(ui), enabled);

    /// <summary>Draws a both-axis scroll area with explicit state passed to the content callback.</summary>
    /// <remarks>
    /// Use this overload with a <c>static</c> lambda to avoid capturing
    /// application state while rendering scroll area content.
    /// </remarks>
    public Response ScrollAreaBoth<TState>(UiId id, float width, float height, TState contentState, Action<Ui, TState> content, bool enabled = true)
        => ScrollAreaBothCore(id, width, height, contentState, content, enabled);

    private Response ScrollAreaBothCore<TState>(UiId id, float width, float height, TState contentState, Action<Ui, TState> content, bool enabled)
    {
        enabled = ResolveEnabled(enabled);

        id = RequireSpecifiedId(id, nameof(id));
        int widgetId = MakeWidgetId(UiWidgetKind.ScrollAreaBoth, id);
        RegisterWidgetId(widgetId, "ScrollAreaBoth");
        var state = GetState<ScrollAreaState>(widgetId);
        var (x, y) = Place(width, height);
        float border = FrameBorderWidth;
        float scrollbarRadius = MathF.Min(FrameRadius, Theme.ScrollbarWidth * 0.5f);
        float innerX = x + border;
        float innerY = y + border;
        float innerW = MathF.Max(0, width - border * 2);
        float innerH = MathF.Max(0, height - border * 2);

        bool hover = enabled && PointIn(x, y, width, height);
        if (hover) _hotId = widgetId;

        bool changed = false;
        if (!enabled)
        {
            state.DraggingThumb = false;
            state.DraggingHorizontalThumb = false;
            state.DraggingContent = false;
        }

        if (!IsMouseDown(UiMouseButton.Left))
        {
            state.DraggingThumb = false;
            state.DraggingHorizontalThumb = false;
            state.DraggingContent = false;
        }

        float previousContentWidth = MathF.Max(state.ContentWidth, innerW);
        float previousContentHeight = MathF.Max(state.ContentHeight, innerH);
        ScrollAreaViewMetrics preview = ResolveScrollAreaViewMetrics(previousContentWidth, previousContentHeight, innerW, innerH, allowHorizontal: true);
        float previousMaxScrollX = MathF.Max(0, previousContentWidth - preview.ViewWidth);
        float previousMaxScrollY = MathF.Max(0, previousContentHeight - preview.ViewHeight);

        float verticalTrackX = x + width - border - Theme.ScrollbarWidth;
        float verticalTrackY = innerY;
        float horizontalTrackX = innerX;
        float horizontalTrackY = y + height - border - Theme.ScrollbarWidth;

        float verticalThumbH = preview.ShowVertical
            ? MathF.Max(Theme.ScrollbarMinThumbSize, (preview.ViewHeight * preview.ViewHeight) / previousContentHeight)
            : 0f;
        float verticalThumbTravel = MathF.Max(0, preview.ViewHeight - verticalThumbH);
        float verticalThumbY = verticalThumbTravel > 0 && previousMaxScrollY > 0
            ? innerY + (state.ScrollY / previousMaxScrollY) * verticalThumbTravel
            : innerY;
        bool verticalThumbHover = enabled && preview.ShowVertical && PointIn(verticalTrackX, verticalThumbY, Theme.ScrollbarWidth, verticalThumbH);

        float horizontalThumbW = preview.ShowHorizontal
            ? MathF.Max(Theme.ScrollbarMinThumbSize, (preview.ViewWidth * preview.ViewWidth) / previousContentWidth)
            : 0f;
        float horizontalThumbTravel = MathF.Max(0, preview.ViewWidth - horizontalThumbW);
        float horizontalThumbX = horizontalThumbTravel > 0 && previousMaxScrollX > 0
            ? innerX + (state.ScrollX / previousMaxScrollX) * horizontalThumbTravel
            : innerX;
        bool horizontalThumbHover = enabled && preview.ShowHorizontal && PointIn(horizontalThumbX, horizontalTrackY, horizontalThumbW, Theme.ScrollbarWidth);

        bool mousePressed = enabled && IsMousePressed(UiMouseButton.Left);
        if (mousePressed)
        {
            if (verticalThumbHover)
            {
                _activeId = widgetId;
                state.DraggingThumb = true;
                state.DraggingHorizontalThumb = false;
                state.DraggingContent = false;
                state.ThumbDragOffsetY = _mouse.Y - verticalThumbY;
            }
            else if (horizontalThumbHover)
            {
                _activeId = widgetId;
                state.DraggingHorizontalThumb = true;
                state.DraggingThumb = false;
                state.DraggingContent = false;
                state.ThumbDragOffsetX = _mouse.X - horizontalThumbX;
            }
        }

        if (enabled && hover && !state.DraggingThumb && !state.DraggingHorizontalThumb && !state.DraggingContent)
        {
            float horizontalWheel = _input.WheelDelta.X;
            float verticalWheel = _input.WheelDelta.Y;

            if (verticalWheel != 0 && previousMaxScrollX > 0 && (_input.Shift || previousMaxScrollY <= 0.5f))
            {
                horizontalWheel += verticalWheel;
                verticalWheel = 0f;
            }

            if (horizontalWheel != 0 && previousMaxScrollX > 0)
            {
                float previous = state.ScrollX;
                float clampedBefore = Math.Clamp(state.ScrollX, 0, previousMaxScrollX);
                state.ScrollX = Math.Clamp(clampedBefore - horizontalWheel * Theme.ScrollWheelStep, 0, previousMaxScrollX);
                changed |= MathF.Abs(state.ScrollX - previous) > 0.01f;
            }

            if (verticalWheel != 0 && previousMaxScrollY > 0)
            {
                float previous = state.ScrollY;
                float clampedBefore = Math.Clamp(state.ScrollY, 0, previousMaxScrollY);
                state.ScrollY = Math.Clamp(clampedBefore - verticalWheel * Theme.ScrollWheelStep, 0, previousMaxScrollY);
                changed |= MathF.Abs(state.ScrollY - previous) > 0.01f;
            }
        }

        if (enabled && state.DraggingThumb && _activeId == widgetId && IsMouseDown(UiMouseButton.Left) && verticalThumbTravel > 0 && previousMaxScrollY > 0)
        {
            float thumbTop = Math.Clamp(_mouse.Y - state.ThumbDragOffsetY, innerY, innerY + verticalThumbTravel);
            float scrollRatio = (thumbTop - innerY) / verticalThumbTravel;
            float previous = state.ScrollY;
            state.ScrollY = scrollRatio * previousMaxScrollY;
            changed |= MathF.Abs(state.ScrollY - previous) > 0.01f;
        }

        if (enabled && state.DraggingHorizontalThumb && _activeId == widgetId && IsMouseDown(UiMouseButton.Left) && horizontalThumbTravel > 0 && previousMaxScrollX > 0)
        {
            float thumbLeft = Math.Clamp(_mouse.X - state.ThumbDragOffsetX, innerX, innerX + horizontalThumbTravel);
            float scrollRatio = (thumbLeft - innerX) / horizontalThumbTravel;
            float previous = state.ScrollX;
            state.ScrollX = scrollRatio * previousMaxScrollX;
            changed |= MathF.Abs(state.ScrollX - previous) > 0.01f;
        }

        if (enabled && state.DraggingContent && _activeId == widgetId && IsMouseDown(UiMouseButton.Left) && (previousMaxScrollX > 0 || previousMaxScrollY > 0))
        {
            float deltaX = _mouse.X - state.ContentDragStartMouseX;
            float deltaY = _mouse.Y - state.ContentDragStartMouseY;

            if (previousMaxScrollX > 0)
            {
                float previous = state.ScrollX;
                state.ScrollX = Math.Clamp(state.ContentDragStartScrollX - deltaX, 0, previousMaxScrollX);
                changed |= MathF.Abs(state.ScrollX - previous) > 0.01f;
            }

            if (previousMaxScrollY > 0)
            {
                float previous = state.ScrollY;
                state.ScrollY = Math.Clamp(state.ContentDragStartScrollY - deltaY, 0, previousMaxScrollY);
                changed |= MathF.Abs(state.ScrollY - previous) > 0.01f;
            }
        }

        state.ScrollX = Math.Clamp(state.ScrollX, 0, previousMaxScrollX);
        state.ScrollY = Math.Clamp(state.ScrollY, 0, previousMaxScrollY);

        DrawFrameRect(x, y, width, height, Theme.ScrollAreaBg, Theme.ScrollAreaBorder);

        _painter.PushClip(innerX, innerY, preview.ViewWidth, preview.ViewHeight);
        PushHitClip(innerX, innerY, preview.ViewWidth, preview.ViewHeight);

        _layouts.Add(new LayoutScope
        {
            OriginX = innerX - state.ScrollX,
            OriginY = innerY - state.ScrollY,
            CursorX = innerX - state.ScrollX,
            CursorY = innerY - state.ScrollY,
            Dir = LayoutDir.Vertical,
            WidthConstraint = preview.ViewWidth,
            HasWidthConstraint = true,
            Empty = true
        });

        LayoutScope inner;
        try
        {
            content(this, contentState);
            inner = _layouts[^1];
        }
        finally
        {
            _layouts.RemoveAt(_layouts.Count - 1);
            PopHitClip();
            _painter.PopClip();
        }

        float contentWidth = inner.Dir == LayoutDir.Horizontal
            ? inner.CursorX - inner.OriginX
            : inner.MaxExtent;
        float contentHeight = inner.Dir == LayoutDir.Horizontal
            ? inner.MaxExtent
            : inner.CursorY - inner.OriginY;

        state.ContentWidth = contentWidth;
        state.ContentHeight = contentHeight;

        ScrollAreaViewMetrics current = ResolveScrollAreaViewMetrics(contentWidth, contentHeight, innerW, innerH, allowHorizontal: true);
        float maxScrollX = MathF.Max(0, contentWidth - current.ViewWidth);
        float maxScrollY = MathF.Max(0, contentHeight - current.ViewHeight);

        float clampedScrollX = Math.Clamp(state.ScrollX, 0, maxScrollX);
        float clampedScrollY = Math.Clamp(state.ScrollY, 0, maxScrollY);
        changed |= MathF.Abs(clampedScrollX - state.ScrollX) > 0.01f;
        changed |= MathF.Abs(clampedScrollY - state.ScrollY) > 0.01f;
        state.ScrollX = clampedScrollX;
        state.ScrollY = clampedScrollY;

        if (!current.ShowVertical)
            state.DraggingThumb = false;
        if (!current.ShowHorizontal)
            state.DraggingHorizontalThumb = false;
        if (!current.ShowVertical && !current.ShowHorizontal)
            state.DraggingContent = false;

        bool currentVerticalThumbHover = false;
        if (current.ShowVertical)
        {
            float currentThumbH = MathF.Max(Theme.ScrollbarMinThumbSize, (current.ViewHeight * current.ViewHeight) / MathF.Max(contentHeight, current.ViewHeight));
            float currentThumbTravel = MathF.Max(0, current.ViewHeight - currentThumbH);
            float currentThumbY = currentThumbTravel > 0 && maxScrollY > 0
                ? innerY + (state.ScrollY / maxScrollY) * currentThumbTravel
                : innerY;

            currentVerticalThumbHover = enabled && PointIn(verticalTrackX, currentThumbY, Theme.ScrollbarWidth, currentThumbH);
            if (currentVerticalThumbHover || state.DraggingThumb)
                RequestCursor(UiCursor.PointingHand);

            _painter.DrawRect(verticalTrackX, verticalTrackY, Theme.ScrollbarWidth, current.ViewHeight, Theme.ScrollbarTrack, radius: scrollbarRadius);

            Color thumbColor = state.DraggingThumb && _activeId == widgetId
                ? Theme.ScrollbarThumbActive
                : currentVerticalThumbHover
                    ? Theme.ScrollbarThumbHover
                    : Theme.ScrollbarThumb;
            _painter.DrawRect(verticalTrackX, currentThumbY, Theme.ScrollbarWidth, currentThumbH, thumbColor, radius: scrollbarRadius);
        }

        bool currentHorizontalThumbHover = false;
        if (current.ShowHorizontal)
        {
            float currentThumbW = MathF.Max(Theme.ScrollbarMinThumbSize, (current.ViewWidth * current.ViewWidth) / MathF.Max(contentWidth, current.ViewWidth));
            float currentThumbTravel = MathF.Max(0, current.ViewWidth - currentThumbW);
            float currentThumbX = currentThumbTravel > 0 && maxScrollX > 0
                ? innerX + (state.ScrollX / maxScrollX) * currentThumbTravel
                : innerX;

            currentHorizontalThumbHover = enabled && PointIn(currentThumbX, horizontalTrackY, currentThumbW, Theme.ScrollbarWidth);
            if (currentHorizontalThumbHover || state.DraggingHorizontalThumb)
                RequestCursor(UiCursor.PointingHand);

            _painter.DrawRect(horizontalTrackX, horizontalTrackY, current.ViewWidth, Theme.ScrollbarWidth, Theme.ScrollbarTrack, radius: scrollbarRadius);

            Color thumbColor = state.DraggingHorizontalThumb && _activeId == widgetId
                ? Theme.ScrollbarThumbActive
                : currentHorizontalThumbHover
                    ? Theme.ScrollbarThumbHover
                    : Theme.ScrollbarThumb;
            _painter.DrawRect(currentThumbX, horizontalTrackY, currentThumbW, Theme.ScrollbarWidth, thumbColor, radius: scrollbarRadius);
        }

        if (current.ShowHorizontal && current.ShowVertical)
        {
            _painter.DrawRect(verticalTrackX, horizontalTrackY, Theme.ScrollbarWidth, Theme.ScrollbarWidth, Theme.ScrollbarTrack);
        }

        bool viewHover = enabled && (current.ShowHorizontal || current.ShowVertical) && PointIn(innerX, innerY, current.ViewWidth, current.ViewHeight);
        bool childClaimedHover = _hotId != 0 && _hotId != widgetId;
        if (mousePressed &&
            viewHover &&
            !currentVerticalThumbHover &&
            !currentHorizontalThumbHover &&
            !state.DraggingThumb &&
            !state.DraggingHorizontalThumb &&
            !childClaimedHover)
        {
            _activeId = widgetId;
            state.DraggingContent = true;
            state.ContentDragStartMouseX = _mouse.X;
            state.ContentDragStartMouseY = _mouse.Y;
            state.ContentDragStartScrollX = state.ScrollX;
            state.ContentDragStartScrollY = state.ScrollY;
        }

        Advance(width, height);
        bool dragging = enabled && _activeId == widgetId && IsMouseDown(UiMouseButton.Left) && (state.DraggingThumb || state.DraggingHorizontalThumb || state.DraggingContent);
        return new Response(
            x,
            y,
            width,
            height,
            hover,
            dragging,
            false,
            changed: changed,
            disabled: !enabled);
    }

    private ScrollAreaViewMetrics ResolveScrollAreaViewMetrics(float contentWidth, float contentHeight, float innerWidth, float innerHeight, bool allowHorizontal)
    {
        const float ScrollbarGap = 4f;

        bool showHorizontal = false;
        bool showVertical = false;
        float viewWidth = MathF.Max(0, innerWidth);
        float viewHeight = MathF.Max(0, innerHeight);

        for (int i = 0; i < 3; i++)
        {
            viewWidth = MathF.Max(0, innerWidth - (showVertical ? Theme.ScrollbarWidth + ScrollbarGap : 0f));
            viewHeight = MathF.Max(0, innerHeight - (showHorizontal ? Theme.ScrollbarWidth + ScrollbarGap : 0f));

            bool nextHorizontal = allowHorizontal && contentWidth > viewWidth + 0.5f;
            bool nextVertical = contentHeight > viewHeight + 0.5f;
            if (nextHorizontal == showHorizontal && nextVertical == showVertical)
                break;

            showHorizontal = nextHorizontal;
            showVertical = nextVertical;
        }

        viewWidth = MathF.Max(0, innerWidth - (showVertical ? Theme.ScrollbarWidth + ScrollbarGap : 0f));
        viewHeight = MathF.Max(0, innerHeight - (showHorizontal ? Theme.ScrollbarWidth + ScrollbarGap : 0f));
        return new ScrollAreaViewMetrics(showHorizontal, showVertical, viewWidth, viewHeight);
    }
}
