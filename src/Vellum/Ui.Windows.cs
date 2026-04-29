using System.Numerics;
using Vellum.Rendering;

namespace Vellum;

public sealed partial class Ui
{
    private sealed class WindowRuntimeState
    {
        public Vector2 Position;
        public Vector2 DragOffset;
        public float Width;
        public float Height;
        public float TitleBarHeight;
        public float ScrollY;
        public float ContentHeight;
        public float ThumbDragOffsetY;
        public bool Initialized;
        public bool Dragging;
        public bool Resizing;
        public bool DraggingScrollThumb;
        public Vector2 ResizeAnchor;
        public Vector2 ResizeStartSize;
    }

    private sealed class WindowRequest
    {
        public int WindowId;
        public required string Id;
        public required string Title;
        public required WindowState State;
        public float Width;
        public bool Resizable;
        public bool Closable;
        public bool Header;
        public Action<Ui>? Content;
    }

    private enum WindowTitleIcon { Collapse, Expand, Close }

    /// <summary>Declares a floating window.</summary>
    public Response Window(string id, string title, WindowState state, float width, Action<Ui> content,
        bool resizable = false, bool closable = true, bool header = true)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(content);

        int windowId = MakeId(id);
        RegisterWidgetId(windowId, $"Window \"{id}\"");
        EnsureWindowOrder(windowId);

        if (!state.Open)
            return default;

        var runtime = GetWindowRuntimeState(windowId);
        if (!runtime.Initialized)
        {
            runtime.Position = state.Position;
            runtime.Initialized = true;
        }
        else if (!runtime.Dragging)
        {
            runtime.Position = state.Position;
        }

        if (resizable && runtime.Resizing && IsMouseDown(UiMouseButton.Left))
        {
            Vector2 delta = _mouse - runtime.ResizeAnchor;
            Vector2 newSize = runtime.ResizeStartSize + delta;
            Vector2 minSize = new(MathF.Max(60f, state.MinSize.X), MathF.Max(40f, state.MinSize.Y));
            newSize.X = MathF.Max(minSize.X, newSize.X);
            newSize.Y = MathF.Max(minSize.Y, newSize.Y);
            state.Size = newSize;
        }

        state.Position = runtime.Position;
        float effectiveWidth = resizable && state.Size.X > 0 ? state.Size.X : width;
        runtime.Width = MathF.Max(0, effectiveWidth);
        _windowRequests[windowId] = new WindowRequest
        {
            WindowId = windowId,
            Id = id,
            Title = title,
            State = state,
            Width = effectiveWidth,
            Resizable = resizable,
            Closable = closable,
            Header = header,
            Content = content
        };

        if (TryGetWindowRect(windowId, out var rect))
            return new Response(rect.X, rect.Y, rect.W, rect.H, PointInRect(rect, _mouse), false, false);

        return new Response(runtime.Position.X, runtime.Position.Y, runtime.Width, 0, false, false, false);
    }

    private void PrepareWindowFrame()
    {
        _windowContextId = 0;
        _windowRequests.Clear();

        foreach (var runtime in _windowRuntimeStates.Values)
        {
            if (!IsMouseDown(UiMouseButton.Left))
            {
                runtime.Dragging = false;
                runtime.Resizing = false;
                runtime.DraggingScrollThumb = false;
            }
        }

        if (_popupDismissedThisPress || _openPopupIds.Count > 0)
            return;

        if (IsMousePressed(UiMouseButton.Left))
        {
            int hitWindowId = GetTopHitWindowId();
            if (hitWindowId != 0)
                BringWindowToFront(hitWindowId);
        }

        if (!IsMouseDown(UiMouseButton.Left))
            return;

        foreach (var runtime in _windowRuntimeStates.Values)
        {
            if (!runtime.Dragging)
                continue;

            runtime.Position = _mouse - runtime.DragOffset;
        }
    }

    private void FinalizeWindowFrame()
    {
        for (int i = _windowOrder.Count - 1; i >= 0; i--)
        {
            int windowId = _windowOrder[i];
            if (_windowRequests.ContainsKey(windowId))
                continue;

            _windowOrder.RemoveAt(i);
            _windowRuntimeStates.Remove(windowId);
        }
    }

    private WindowRuntimeState GetWindowRuntimeState(int windowId)
    {
        if (_windowRuntimeStates.TryGetValue(windowId, out var runtime))
            return runtime;

        runtime = new WindowRuntimeState();
        _windowRuntimeStates[windowId] = runtime;
        return runtime;
    }

    private void EnsureWindowOrder(int windowId)
    {
        if (!_windowOrder.Contains(windowId))
            _windowOrder.Add(windowId);
    }

    private void BringWindowToFront(int windowId)
    {
        int index = _windowOrder.IndexOf(windowId);
        if (index < 0 || index == _windowOrder.Count - 1)
            return;

        _windowOrder.RemoveAt(index);
        _windowOrder.Add(windowId);
    }

    private int GetTopHitWindowId()
    {
        for (int i = _windowOrder.Count - 1; i >= 0; i--)
        {
            int windowId = _windowOrder[i];
            if (TryGetWindowRect(windowId, out var rect) && PointInRect(rect, _mouse))
                return windowId;
        }

        return 0;
    }

    private bool TryGetWindowRect(int windowId, out ClipRect rect)
    {
        if (_windowRuntimeStates.TryGetValue(windowId, out var runtime) &&
            runtime.Width > 0 &&
            runtime.Height > 0)
        {
            rect = new ClipRect(runtime.Position.X, runtime.Position.Y, runtime.Width, runtime.Height);
            return true;
        }

        rect = default;
        return false;
    }

    private static bool PointInWindowTitleBar(WindowRuntimeState runtime, Vector2 point)
        => runtime.Width > 0 &&
           runtime.TitleBarHeight > 0 &&
           point.X >= runtime.Position.X &&
           point.X < runtime.Position.X + runtime.Width &&
           point.Y >= runtime.Position.Y &&
           point.Y < runtime.Position.Y + runtime.TitleBarHeight;

    private void RenderQueuedWindows()
    {
        if (_windowOrder.Count == 0)
            return;

        foreach (int windowId in _windowOrder)
        {
            if (_windowRequests.TryGetValue(windowId, out var request))
                RenderWindowRequest(request);
        }
    }

    private void RenderWindowRequest(WindowRequest request)
    {
        if (request.Content == null || !request.State.Open)
            return;

        var runtime = GetWindowRuntimeState(request.WindowId);
        float resolvedWidth = MathF.Max(0, request.Width);
        float border = FrameBorderWidth;
        var bodyPad = Theme.PanelPadding;
        var titlePad = Theme.ButtonPadding;
        string titleText = string.IsNullOrEmpty(request.Title) ? request.Id : request.Title;
        bool collapsedAtFrameStart = request.State.Collapsed;
        bool resizable = request.Resizable;
        bool hasHeader = request.Header;
        bool closable = hasHeader && request.Closable;
        bool fixedHeight = resizable && request.State.Size.Y > 0;
        bool maxHeightLimited = !fixedHeight && request.State.MaxSize.Y > 0;
        bool scrollableBody = fixedHeight || maxHeightLimited;
        const float ScrollbarGap = 4f;
        const float TitleButtonInset = 2f;
        const float TitleButtonGap = 4f;
        const float TitleButtonMaxSize = 16f;

        if (request.State.MaxSize.X > 0)
        {
            float maxWidth = MathF.Max(request.State.MinSize.X, request.State.MaxSize.X);
            resolvedWidth = MathF.Min(resolvedWidth, maxWidth);
        }

        TextLayoutResult titleLayout = default;
        float titleBarHeight = 0f;
        float titleButtonSize = 0f;
        if (hasHeader)
        {
            float titleMaxWidth = MathF.Max(0, resolvedWidth - border * 2 - titlePad.Horizontal);
            titleLayout = LayoutText(titleText, DefaultFontSize, maxWidth: titleMaxWidth, overflow: TextOverflowMode.Ellipsis);
            titleBarHeight = MathF.Max(22f, titleLayout.Height + titlePad.Vertical);
            titleButtonSize = MathF.Min(TitleButtonMaxSize, MathF.Max(14f, titleBarHeight - TitleButtonInset * 2));
            int titleButtonCount = closable ? 2 : 1;
            float titleButtonsWidth = titleButtonSize * titleButtonCount +
                                      TitleButtonGap * Math.Max(0, titleButtonCount - 1) +
                                      TitleButtonInset;
            titleMaxWidth = MathF.Max(0, resolvedWidth - border * 2 - titlePad.Left - titleButtonsWidth - 4f);
            titleLayout = LayoutText(titleText, DefaultFontSize, maxWidth: titleMaxWidth, overflow: TextOverflowMode.Ellipsis);
            titleBarHeight = MathF.Max(22f, titleLayout.Height + titlePad.Vertical);
            titleButtonSize = MathF.Min(TitleButtonMaxSize, MathF.Max(14f, titleBarHeight - TitleButtonInset * 2));
        }

        float x = runtime.Position.X;
        float y = runtime.Position.Y;
        float contentX = x + border + bodyPad.Left;
        float contentY = y + border + titleBarHeight + bodyPad.Top;
        int hotIdBeforeContent = _hotId;

        float fixedOuterHeight = fixedHeight
            ? MathF.Max(request.State.MinSize.Y, request.State.Size.Y)
            : 0f;
        float fixedBodyInnerH = fixedHeight
            ? MathF.Max(0, fixedOuterHeight - border * 2 - titleBarHeight - bodyPad.Vertical)
            : 0f;
        float maxOuterHeight = maxHeightLimited
            ? MathF.Max(request.State.MinSize.Y, request.State.MaxSize.Y)
            : 0f;
        float maxBodyInnerH = maxHeightLimited
            ? MathF.Max(0, maxOuterHeight - border * 2 - titleBarHeight - bodyPad.Vertical)
            : 0f;
        float bodyClipH = fixedHeight ? fixedBodyInnerH : maxBodyInnerH;
        float previousContentHeight = scrollableBody ? MathF.Max(runtime.ContentHeight, bodyClipH) : 0f;
        float previousMaxScroll = scrollableBody ? MathF.Max(0, previousContentHeight - bodyClipH) : 0f;
        bool bodyScrollable = scrollableBody && previousMaxScroll > 0.5f;
        float trackX = x + resolvedWidth - border - Theme.ScrollbarWidth;
        float trackY = contentY;
        float trackH = bodyClipH;
        float bodyRegionX = x + border;
        float bodyRegionY = y + border + titleBarHeight;
        float bodyRegionW = MathF.Max(0, resolvedWidth - border * 2);
        float bodyRegionH = scrollableBody
            ? MathF.Max(0, (fixedHeight ? fixedOuterHeight : maxOuterHeight) - border * 2 - titleBarHeight)
            : 0f;
        int scrollId = HashMix(request.WindowId, HashString("scroll"));
        float scrollbarReserve = bodyScrollable ? Theme.ScrollbarWidth + ScrollbarGap : 0f;
        float contentW = MathF.Max(0, resolvedWidth - border * 2 - bodyPad.Horizontal - scrollbarReserve);
        bool scrollTrackHovered = false;
        bool scrollThumbHovered = false;
        bool scrollThumbPressed = false;
        if (scrollableBody)
        {
            MarkWidgetSeen(scrollId);
            bool windowInteractive =
                GetTopHitWindowId() == request.WindowId &&
                _openPopupIds.Count == 0 &&
                !_popupDismissedThisPress;
            bool bodyHover = windowInteractive &&
                             PointInRect(new ClipRect(bodyRegionX, bodyRegionY, bodyRegionW, bodyRegionH), _mouse);

            float thumbH = bodyScrollable
                ? MathF.Max(Theme.ScrollbarMinThumbSize, (bodyClipH * bodyClipH) / previousContentHeight)
                : 0f;
            float thumbTravel = MathF.Max(0, bodyClipH - thumbH);
            float thumbY = thumbTravel > 0 && previousMaxScroll > 0
                ? trackY + (runtime.ScrollY / previousMaxScroll) * thumbTravel
                : trackY;
            scrollTrackHovered = bodyScrollable &&
                                 windowInteractive &&
                                 PointInRect(new ClipRect(trackX, trackY, Theme.ScrollbarWidth, trackH), _mouse);
            scrollThumbHovered = bodyScrollable &&
                                 windowInteractive &&
                                 PointInRect(new ClipRect(trackX, thumbY, Theme.ScrollbarWidth, thumbH), _mouse);

            if (scrollThumbHovered || runtime.DraggingScrollThumb)
            {
                _hotId = scrollId;
                RequestCursor(UiCursor.PointingHand);
            }

            if (scrollThumbHovered && IsMousePressed(UiMouseButton.Left))
            {
                _activeId = scrollId;
                runtime.DraggingScrollThumb = true;
                runtime.ThumbDragOffsetY = _mouse.Y - thumbY;
            }

            if (bodyHover && _input.WheelDelta.Y != 0 && !runtime.DraggingScrollThumb && previousMaxScroll > 0)
            {
                runtime.ScrollY = Math.Clamp(runtime.ScrollY - _input.WheelDelta.Y * Theme.ScrollWheelStep, 0, previousMaxScroll);
            }

            if (runtime.DraggingScrollThumb &&
                _activeId == scrollId &&
                IsMouseDown(UiMouseButton.Left) &&
                thumbTravel > 0 &&
                previousMaxScroll > 0)
            {
                float thumbTop = Math.Clamp(_mouse.Y - runtime.ThumbDragOffsetY, trackY, trackY + thumbTravel);
                float scrollRatio = (thumbTop - trackY) / thumbTravel;
                runtime.ScrollY = scrollRatio * previousMaxScroll;
            }

            runtime.ScrollY = Math.Clamp(runtime.ScrollY, 0, previousMaxScroll);
            scrollThumbPressed = runtime.DraggingScrollThumb && _activeId == scrollId && IsMouseDown(UiMouseButton.Left);
        }

        Painter? contentPainter = null;
        float contentHeight = 0f;
        int hotIdAfterContent = hotIdBeforeContent;
        bool hasInputEdgeThisFrame = _input.PressedKeys?.Count > 0 ||
                                     _input.TextInput.Length > 0 ||
                                     _input.WheelDelta.X != 0 ||
                                     _input.WheelDelta.Y != 0;
        for (int i = 0; i < _mouseButtonsPressed.Length && !hasInputEdgeThisFrame; i++)
            hasInputEdgeThisFrame = _mouseButtonsPressed[i] || _mouseButtonsReleased[i];

        Painter RenderContentPass(float availableWidth, out float passContentHeight, out int passHotId)
        {
            var parentPainter = _painter;
            var passPainter = AcquireDeferredPainter();
            _painter = passPainter;

            int previousWindowContextId = _windowContextId;
            _windowContextId = request.WindowId;
            _idStack.Push(request.WindowId);

            bool pushedBodyClip = scrollableBody;
            if (pushedBodyClip)
            {
                _painter.PushClip(contentX, contentY, availableWidth, bodyClipH);
                PushHitClip(contentX, contentY, availableWidth, bodyClipH);
            }

            _layouts.Add(new LayoutScope
            {
                OriginX = contentX,
                OriginY = contentY - (scrollableBody ? runtime.ScrollY : 0f),
                CursorX = contentX,
                CursorY = contentY - (scrollableBody ? runtime.ScrollY : 0f),
                Dir = LayoutDir.Vertical,
                WidthConstraint = availableWidth,
                HasWidthConstraint = true,
                Empty = true
            });

            try
            {
                request.Content(this);

                var inner = _layouts[^1];
                passContentHeight = inner.Dir == LayoutDir.Horizontal
                    ? inner.MaxExtent
                    : inner.CursorY - inner.OriginY;
            }
            finally
            {
                _layouts.RemoveAt(_layouts.Count - 1);
                if (pushedBodyClip)
                {
                    PopHitClip();
                    _painter.PopClip();
                }
                _idStack.Pop();
                _windowContextId = previousWindowContextId;
                _painter = parentPainter;
            }

            passHotId = _hotId;
            return passPainter;
        }

        if (!collapsedAtFrameStart)
        {
            contentPainter = RenderContentPass(contentW, out contentHeight, out hotIdAfterContent);

            bool measuredBodyScrollable = scrollableBody && MathF.Max(0, contentHeight - bodyClipH) > 0.5f;
            if (measuredBodyScrollable != bodyScrollable && !hasInputEdgeThisFrame)
            {
                ReleaseDeferredPainter(contentPainter);

                bodyScrollable = measuredBodyScrollable;
                scrollbarReserve = bodyScrollable ? Theme.ScrollbarWidth + ScrollbarGap : 0f;
                contentW = MathF.Max(0, resolvedWidth - border * 2 - bodyPad.Horizontal - scrollbarReserve);

                _hotId = hotIdBeforeContent;
#if DEBUG
                _debugDuplicateIdCheckSuppressionDepth++;
                try
                {
                    contentPainter = RenderContentPass(contentW, out contentHeight, out hotIdAfterContent);
                }
                finally
                {
                    _debugDuplicateIdCheckSuppressionDepth--;
                }
#else
                contentPainter = RenderContentPass(contentW, out contentHeight, out hotIdAfterContent);
#endif
            }

            if (!bodyScrollable)
            {
                scrollTrackHovered = false;
                scrollThumbHovered = false;
                scrollThumbPressed = false;
                runtime.DraggingScrollThumb = false;
            }
        }

        float collapsedHeight = MathF.Max(0, border * 2 + titleBarHeight);
        float expandedHeight = MathF.Max(0, border * 2 + titleBarHeight + bodyPad.Vertical + contentHeight);
        float resolvedHeight;
        if (collapsedAtFrameStart)
            resolvedHeight = collapsedHeight;
        else if (fixedHeight)
            resolvedHeight = fixedOuterHeight;
        else if (maxHeightLimited)
            resolvedHeight = MathF.Min(expandedHeight, maxOuterHeight);
        else
            resolvedHeight = expandedHeight;

        if (scrollableBody)
        {
            runtime.ContentHeight = contentHeight;
            float maxScroll = MathF.Max(0, contentHeight - bodyClipH);
            runtime.ScrollY = Math.Clamp(runtime.ScrollY, 0, maxScroll);
        }

        if (resizable && !collapsedAtFrameStart)
        {
            if (request.State.Size.X <= 0)
                request.State.Size.X = resolvedWidth;
            if (request.State.Size.Y <= 0)
                request.State.Size.Y = resolvedHeight;
        }

        runtime.Width = resolvedWidth;
        runtime.Height = resolvedHeight;
        runtime.TitleBarHeight = titleBarHeight;
        request.State.Position = runtime.Position;

        float rightButtonX = x + resolvedWidth - border - TitleButtonInset - titleButtonSize;
        float buttonY = y + border + TitleButtonInset;
        var closeButtonRect = new ClipRect(rightButtonX, buttonY, titleButtonSize, titleButtonSize);
        var collapseButtonRect = new ClipRect(
            closable ? rightButtonX - TitleButtonGap - titleButtonSize : rightButtonX,
            buttonY,
            titleButtonSize,
            titleButtonSize);
        int collapseButtonId = HashMix(request.WindowId, HashString("collapse"));
        int closeButtonId = HashMix(request.WindowId, HashString("close"));
        bool titleButtonsInteractive = hasHeader &&
            _openPopupIds.Count == 0 &&
            !_popupDismissedThisPress &&
            GetTopHitWindowId() == request.WindowId;

        (bool Hover, bool Pressed, bool Clicked) EvaluateTitleButton(int id, in ClipRect rect)
        {
            MarkWidgetSeen(id);

            bool hover = titleButtonsInteractive && PointInRect(rect, _mouse);
            if (hover)
            {
                _hotId = id;
                RequestCursor(UiCursor.PointingHand);
            }

            if (hover && IsMousePressed(UiMouseButton.Left))
                _activeId = id;

            bool pressed = _activeId == id && IsMouseDown(UiMouseButton.Left);
            bool clicked = IsMouseReleased(UiMouseButton.Left) && _activeId == id && hover;
            return (hover, pressed, clicked);
        }

        var collapseButton = hasHeader
            ? EvaluateTitleButton(collapseButtonId, collapseButtonRect)
            : default;
        var closeButton = closable
            ? EvaluateTitleButton(closeButtonId, closeButtonRect)
            : default;
        bool childClaimedHover = hotIdAfterContent != hotIdBeforeContent;
        bool titleButtonsClaimedHover = collapseButton.Hover || closeButton.Hover;
        bool renderCollapsed = collapsedAtFrameStart;

        if (collapseButton.Clicked)
        {
            request.State.Collapsed = !request.State.Collapsed;
            runtime.Dragging = false;
            runtime.Resizing = false;
            renderCollapsed = true;
            resolvedHeight = collapsedHeight;
        }

        if (closeButton.Clicked)
        {
            request.State.Open = false;
            runtime.Dragging = false;
            runtime.Resizing = false;
            request.State.Position = runtime.Position;
            return;
        }

        runtime.Height = resolvedHeight;
        var windowRect = new ClipRect(x, y, resolvedWidth, resolvedHeight);
        bool hovered = PointInRect(windowRect, _mouse);
        bool titleHover = PointInWindowTitleBar(runtime, _mouse);

        const float ResizeGripSize = 14f;
        bool showGrip = resizable && !renderCollapsed;
        ClipRect gripRect = default;
        int gripId = 0;
        bool gripHovered = false;
        bool gripPressed = false;
        if (showGrip)
        {
            float gripX = x + resolvedWidth - border - ResizeGripSize;
            float gripY = y + resolvedHeight - border - ResizeGripSize;
            gripRect = new ClipRect(gripX, gripY, ResizeGripSize, ResizeGripSize);
            gripId = HashMix(request.WindowId, HashString("resize"));
            MarkWidgetSeen(gripId);

            bool gripInteractive =
                _openPopupIds.Count == 0 &&
                !_popupDismissedThisPress &&
                GetTopHitWindowId() == request.WindowId;

            gripHovered = gripInteractive && PointInRect(gripRect, _mouse);
            if (gripHovered || runtime.Resizing)
            {
                _hotId = gripId;
                RequestCursor(UiCursor.ResizeNWSE);
            }

            if (gripHovered && IsMousePressed(UiMouseButton.Left))
            {
                runtime.Resizing = true;
                runtime.ResizeAnchor = _mouse;
                runtime.ResizeStartSize = new Vector2(resolvedWidth, resolvedHeight);
                if (request.State.Size.X <= 0) request.State.Size.X = resolvedWidth;
                if (request.State.Size.Y <= 0) request.State.Size.Y = resolvedHeight;
                _activeId = gripId;
            }

            gripPressed = runtime.Resizing;
        }

        if (hovered &&
            !childClaimedHover &&
            !titleButtonsClaimedHover &&
            !gripHovered &&
            !scrollTrackHovered &&
            !runtime.DraggingScrollThumb &&
            !runtime.Resizing &&
            IsMousePressed(UiMouseButton.Left) &&
            _openPopupIds.Count == 0 &&
            !_popupDismissedThisPress &&
            GetTopHitWindowId() == request.WindowId)
        {
            runtime.Dragging = true;
            runtime.DragOffset = _mouse - runtime.Position;
            _activeId = request.WindowId;
        }

        Color borderColor = hovered ? Theme.ButtonBorderHover : Theme.PanelBorder;
        if (titleHover && _openPopupIds.Count == 0)
            RequestCursor(UiCursor.PointingHand);

        DrawFrameRect(x, y, resolvedWidth, resolvedHeight, Theme.PanelBg, borderColor);

        float titleFillWidth = MathF.Max(0, resolvedWidth - border * 2);
        Color titleFill = titleHover && Theme.WindowTitleBgHover.A > 0
            ? Theme.WindowTitleBgHover
            : Theme.WindowTitleBg;
        if (titleFillWidth > 0 && titleBarHeight > 0 && titleFill.A > 0)
        {
            _painter.DrawRect(
                x + border,
                y + border,
                titleFillWidth,
                titleBarHeight,
                titleFill,
                default,
                0f,
                MathF.Max(0, FrameRadius - border));
        }

        float separatorY = y + border + titleBarHeight;
        if (!renderCollapsed && resolvedWidth > border * 2 && Theme.Separator.A > 0)
        {
            _painter.DrawRect(
                x + border,
                separatorY,
                MathF.Max(0, resolvedWidth - border * 2),
                1f,
                Theme.Separator);
        }

        if (hasHeader)
        {
            DrawTextLayout(titleLayout, x + border + titlePad.Left, y + border + titlePad.Top, Theme.WindowTitleText);
            DrawWindowTitleButton(collapseButtonRect, renderCollapsed ? WindowTitleIcon.Expand : WindowTitleIcon.Collapse, collapseButton.Hover, collapseButton.Pressed);
            if (closable)
                DrawWindowTitleButton(closeButtonRect, WindowTitleIcon.Close, closeButton.Hover, closeButton.Pressed);
        }

        if (!renderCollapsed && contentPainter != null)
            _painter.Append(contentPainter.RenderList);

        if (contentPainter != null)
            ReleaseDeferredPainter(contentPainter);

        if (scrollableBody && !renderCollapsed)
        {
            float viewH = bodyClipH;
            float maxScroll = MathF.Max(0, contentHeight - viewH);
            bool showScrollbar = maxScroll > 0.5f;
            if (!showScrollbar)
            {
                runtime.DraggingScrollThumb = false;
            }
            else
            {
                float currentTrackX = x + resolvedWidth - border - Theme.ScrollbarWidth;
                float currentTrackY = contentY;
                float currentThumbH = MathF.Max(Theme.ScrollbarMinThumbSize, (viewH * viewH) / MathF.Max(contentHeight, viewH));
                float currentThumbTravel = MathF.Max(0, viewH - currentThumbH);
                float currentThumbY = currentThumbTravel > 0
                    ? currentTrackY + (runtime.ScrollY / maxScroll) * currentThumbTravel
                    : currentTrackY;
                bool currentThumbHover =
                    PointInRect(new ClipRect(currentTrackX, currentThumbY, Theme.ScrollbarWidth, currentThumbH), _mouse) &&
                    GetTopHitWindowId() == request.WindowId &&
                    _openPopupIds.Count == 0 &&
                    !_popupDismissedThisPress;

                if (currentThumbHover || scrollThumbPressed)
                    RequestCursor(UiCursor.PointingHand);

                float scrollbarRadius = MathF.Min(FrameRadius, Theme.ScrollbarWidth * 0.5f);
                _painter.DrawRect(currentTrackX, currentTrackY, Theme.ScrollbarWidth, viewH, Theme.ScrollbarTrack, radius: scrollbarRadius);

                Color thumbColor = scrollThumbPressed
                    ? Theme.ScrollbarThumbActive
                    : currentThumbHover
                        ? Theme.ScrollbarThumbHover
                        : Theme.ScrollbarThumb;
                _painter.DrawRect(currentTrackX, currentThumbY, Theme.ScrollbarWidth, currentThumbH, thumbColor, radius: scrollbarRadius);
            }
        }

        if (showGrip)
            DrawWindowResizeGrip(gripRect, gripHovered, gripPressed);
    }

    private void DrawWindowTitleButton(in ClipRect rect, WindowTitleIcon icon, bool hover, bool pressed)
    {
        var visuals = GetButtonVisuals(enabled: true, hover, pressed, focused: false);
        float cornerRadius = MathF.Min(FrameRadius, rect.W * 0.25f);
        float strokeWidth = visuals.Border.A > 0 ? FrameBorderWidth : 0;
        _painter.DrawRect(rect.X, rect.Y, rect.W, rect.H, visuals.Fill, visuals.Border, strokeWidth, cornerRadius);

        float inset = MathF.Max(3f, rect.W * 0.28f);
        float thickness = MathF.Max(1f, MathF.Round(rect.W * 0.11f));
        float left = rect.X + inset;
        float top = rect.Y + inset;
        float right = rect.X + rect.W - inset;
        float bottom = rect.Y + rect.H - inset;
        float midX = rect.X + rect.W * 0.5f;
        float midY = rect.Y + rect.H * 0.5f;
        Color color = visuals.Foreground;

        switch (icon)
        {
            case WindowTitleIcon.Collapse:
                _painter.FillRect(left, midY - thickness * 0.5f, right - left, thickness, color);
                break;
            case WindowTitleIcon.Expand:
                _painter.FillRect(left, midY - thickness * 0.5f, right - left, thickness, color);
                _painter.FillRect(midX - thickness * 0.5f, top, thickness, bottom - top, color);
                break;
            case WindowTitleIcon.Close:
                DrawIconLine(left, top, right, bottom, thickness, color);
                DrawIconLine(left, bottom, right, top, thickness, color);
                break;
        }
    }

    private void DrawIconLine(float x1, float y1, float x2, float y2, float thickness, Color color)
    {
        Vector2 a = new(x1, y1);
        Vector2 b = new(x2, y2);
        Vector2 delta = b - a;
        float length = delta.Length();
        if (length < 0.0001f) return;
        Vector2 normal = new Vector2(-delta.Y, delta.X) / length * (thickness * 0.5f);
        Vector2 p1 = a + normal;
        Vector2 p2 = b + normal;
        Vector2 p3 = b - normal;
        Vector2 p4 = a - normal;
        _painter.FillTriangle(p1, p2, p3, color);
        _painter.FillTriangle(p1, p3, p4, color);
    }

    private void DrawWindowResizeGrip(in ClipRect rect, bool hover, bool pressed)
    {
        Color baseColor = pressed ? Theme.Accent
            : hover ? Theme.ButtonBorderHover
            : Theme.PanelBorder;

        float pad = MathF.Max(2f, rect.W * 0.18f);
        float thickness = MathF.Max(1f, MathF.Round(rect.W * 0.08f));
        float stripeGap = MathF.Max(2f, rect.W * 0.22f);
        float innerRight = rect.X + rect.W - pad;
        float innerBottom = rect.Y + rect.H - pad;
        float innerLeft = rect.X + pad;
        float innerTop = rect.Y + pad;
        float span = MathF.Min(innerRight - innerLeft, innerBottom - innerTop);

        for (int i = 1; i <= 3; i++)
        {
            float offset = i * stripeGap;
            if (offset >= span) break;

            float x1 = innerRight - offset;
            float y1 = innerBottom;
            float x2 = innerRight;
            float y2 = innerBottom - offset;
            DrawIconLine(x1, y1, x2, y2, thickness, baseColor);
        }
    }
}
