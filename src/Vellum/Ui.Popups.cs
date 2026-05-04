using System.Numerics;
using Vellum.Rendering;

namespace Vellum;

public sealed partial class Ui
{
    private const float PopupScrollbarGap = 4f;

    private sealed class PopupState
    {
        public float ScrollY;
        public float ContentHeight;
        public float ThumbDragOffsetY;
        public bool DraggingThumb;
    }

    private sealed class PopupRequest
    {
        public int PopupId;
        public float AnchorX;
        public float AnchorY;
        public float Width;
        public float MaxHeight;
        public bool IsModal;
        public bool Enabled;
        public bool ZeroItemSpacing;
        public DeferredUiContent Content;
    }

    /// <summary>Marks a popup as open. The popup is rendered when declared with the same id.</summary>
    public void OpenPopup(UiId id) => OpenPopupById(MakePopupId(id));

    /// <summary>Closes the popup with the given id.</summary>
    public void ClosePopup(UiId id) => ClosePopupById(MakePopupId(id));

    /// <summary>Closes the popup currently being rendered and its descendants.</summary>
    public void CloseCurrentPopup()
    {
        if (_popupContext.Count == 0) return;
        int index = _popupContext.Count - 1;
        _openPopupIds.RemoveRange(index, _openPopupIds.Count - index);
    }

    /// <summary>Closes all open popups.</summary>
    public void CloseAllPopups() => _openPopupIds.Clear();

    /// <summary>Returns whether the popup with the given id is open.</summary>
    public bool IsPopupOpen(UiId id) => IsPopupOpen(MakePopupId(id));

    /// <summary>Gets the last known bounds of an open popup.</summary>
    public bool TryGetPopupBounds(UiId id, out float x, out float y, out float width, out float height)
    {
        if (TryGetKnownPopupRect(MakePopupId(id), out var rect))
        {
            x = rect.X;
            y = rect.Y;
            width = rect.W;
            height = rect.H;
            return true;
        }

        x = 0;
        y = 0;
        width = 0;
        height = 0;
        return false;
    }

    internal bool IsChildPopupOpen(UiWidgetKind parentKind, string parentId, string childId)
        => IsPopupOpen(MakeChildId(MakeWidgetId(parentKind, parentId.AsSpan()), childId));

    internal bool TryGetChildPopupBounds(UiWidgetKind parentKind, string parentId, string childId, out float x, out float y, out float width, out float height)
    {
        if (TryGetKnownPopupRect(MakeChildId(MakeWidgetId(parentKind, parentId.AsSpan()), childId), out var rect))
        {
            x = rect.X;
            y = rect.Y;
            width = rect.W;
            height = rect.H;
            return true;
        }

        x = 0;
        y = 0;
        width = 0;
        height = 0;
        return false;
    }

    /// <summary>Declares a popup anchored at an explicit position.</summary>
    public bool Popup(
        UiId id,
        float anchorX,
        float anchorY,
        float width,
        float maxHeight,
        Action<Ui> content,
        bool enabled = true)
        => QueuePopupRequest(MakePopupId(id), anchorX, anchorY, width, maxHeight, content, enabled, isModal: false, "Popup");

    /// <summary>Declares a popup anchored at an explicit position with explicit state passed to the content callback.</summary>
    /// <remarks>
    /// Use this overload with a <c>static</c> lambda to avoid capturing
    /// application state in delayed popup content.
    /// </remarks>
    public bool Popup<TState>(
        UiId id,
        float anchorX,
        float anchorY,
        float width,
        float maxHeight,
        TState state,
        Action<Ui, TState> content,
        bool enabled = true)
        => QueuePopupRequest(MakePopupId(id), anchorX, anchorY, width, maxHeight, state, content, enabled, isModal: false, "Popup");

    /// <summary>Declares a modal popup centered in the viewport.</summary>
    public bool ModalPopup(UiId id, float width, float maxHeight, Action<Ui> content, bool enabled = true)
        => QueuePopupRequest(MakePopupId(id), 0f, 0f, width, maxHeight, content, enabled, isModal: true, "ModalPopup");

    /// <summary>Declares a modal popup with explicit state passed to the content callback.</summary>
    /// <remarks>
    /// Use this overload with a <c>static</c> lambda to avoid capturing
    /// application state in delayed modal content.
    /// </remarks>
    public bool ModalPopup<TState>(UiId id, float width, float maxHeight, TState state, Action<Ui, TState> content, bool enabled = true)
        => QueuePopupRequest(MakePopupId(id), 0f, 0f, width, maxHeight, state, content, enabled, isModal: true, "ModalPopup");

    /// <summary>Declares a popup anchored below a widget response.</summary>
    public bool Popup(
        UiId id,
        Response anchor,
        float width,
        float maxHeight,
        Action<Ui> content,
        bool enabled = true)
        => Popup(id, anchor.X, anchor.Y + anchor.H, width, maxHeight, content, enabled);

    /// <summary>Declares a popup anchored below a widget response with explicit state passed to the content callback.</summary>
    /// <remarks>
    /// Use this overload with a <c>static</c> lambda to avoid capturing
    /// application state in delayed popup content.
    /// </remarks>
    public bool Popup<TState>(
        UiId id,
        Response anchor,
        float width,
        float maxHeight,
        TState state,
        Action<Ui, TState> content,
        bool enabled = true)
        => Popup(id, anchor.X, anchor.Y + anchor.H, width, maxHeight, state, content, enabled);

    private bool Popup(
        int popupId,
        float anchorX,
        float anchorY,
        float width,
        float maxHeight,
        Action<Ui> content,
        bool enabled = true)
        => QueuePopupRequest(popupId, anchorX, anchorY, width, maxHeight, content, enabled, isModal: false, "Popup");

    private bool QueuePopupRequest(
        int popupId,
        float anchorX,
        float anchorY,
        float width,
        float maxHeight,
        Action<Ui> content,
        bool enabled,
        bool isModal,
        string diagnosticName)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!TryPreparePopupRequest(popupId, enabled, isModal, diagnosticName, out int depth))
            return false;

        StorePopupRequest(
            depth,
            popupId,
            anchorX,
            anchorY,
            width,
            maxHeight,
            isModal,
            zeroItemSpacing: false,
            DeferredUiContent.Create(content));
        return true;
    }

    private bool Popup<TState>(
        int popupId,
        float anchorX,
        float anchorY,
        float width,
        float maxHeight,
        TState state,
        Action<Ui, TState> content,
        bool enabled = true,
        bool zeroItemSpacing = false)
        => QueuePopupRequest(popupId, anchorX, anchorY, width, maxHeight, state, content, enabled, isModal: false, "Popup", zeroItemSpacing);

    private bool QueuePopupRequest<TState>(
        int popupId,
        float anchorX,
        float anchorY,
        float width,
        float maxHeight,
        TState state,
        Action<Ui, TState> content,
        bool enabled,
        bool isModal,
        string diagnosticName,
        bool zeroItemSpacing = false)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!TryPreparePopupRequest(popupId, enabled, isModal, diagnosticName, out int depth))
            return false;

        StorePopupRequest(
            depth,
            popupId,
            anchorX,
            anchorY,
            width,
            maxHeight,
            isModal,
            zeroItemSpacing,
            DeferredUiContent.Create(state, content));
        return true;
    }

    private bool TryPreparePopupRequest(
        int popupId,
        bool enabled,
        bool isModal,
        string diagnosticName,
        out int depth)
    {
        depth = 0;
        if (!IsPopupOpen(popupId)) return false;

        if (!enabled)
        {
            ClosePopupById(popupId);
            return false;
        }

        depth = _popupContext.Count;
        if (!CanQueuePopupRequest(popupId, depth))
            return false;

        RegisterWidgetId(popupId, diagnosticName);

        if (isModal)
            _modalPopupIdsCurrent.Add(popupId);

        return true;
    }

    private void StorePopupRequest(
        int depth,
        int popupId,
        float anchorX,
        float anchorY,
        float width,
        float maxHeight,
        bool isModal,
        bool zeroItemSpacing,
        DeferredUiContent content)
    {
        _popupRequestsByDepth[depth] = new PopupRequest
        {
            PopupId = popupId,
            AnchorX = anchorX,
            AnchorY = anchorY,
            Width = width,
            MaxHeight = maxHeight,
            IsModal = isModal,
            Enabled = true,
            ZeroItemSpacing = zeroItemSpacing,
            Content = content
        };
    }

    private void PreparePopupFrame()
    {
        _popupDismissedThisPress = false;
        _popupContext.Clear();
        _popupRequestsByDepth.Clear();
        _popupRectsCurrent.Clear();
        _modalPopupIdsCurrent.Clear();

        if (IsAnyMousePressed() && _openPopupIds.Count > 0)
            HandlePopupPress();
    }

    private void FinalizePopupFrame()
    {
        int keepCount = 0;
        while (keepCount < _openPopupIds.Count && _popupRectsCurrent.ContainsKey(_openPopupIds[keepCount]))
            keepCount++;

        if (keepCount < _openPopupIds.Count)
            _openPopupIds.RemoveRange(keepCount, _openPopupIds.Count - keepCount);

        _popupRectsPrev.Clear();
        foreach (var pair in _popupRectsCurrent)
            _popupRectsPrev[pair.Key] = pair.Value;

        _modalPopupIdsPrev.Clear();
        foreach (int popupId in _modalPopupIdsCurrent)
        {
            if (_openPopupIds.Contains(popupId))
                _modalPopupIdsPrev.Add(popupId);
        }

        _popupRectsCurrent.Clear();
        _popupRequestsByDepth.Clear();
        _popupContext.Clear();
        _modalPopupIdsCurrent.Clear();
    }

    private void HandlePopupPress()
    {
        int hitDepth = GetDeepestHitPopupDepth();
        if (hitDepth == 0)
        {
            if (IsRootPopupModal())
                return;

            _openPopupIds.Clear();
            _popupDismissedThisPress = true;
            return;
        }

        if (hitDepth < _openPopupIds.Count)
            _openPopupIds.RemoveRange(hitDepth, _openPopupIds.Count - hitDepth);
    }

    private int GetDeepestHitPopupDepth()
    {
        for (int i = _openPopupIds.Count - 1; i >= 0; i--)
        {
            if (TryGetKnownPopupRect(_openPopupIds[i], out var rect) && PointInRect(rect, _mouse))
                return i + 1;
        }

        return 0;
    }

    private bool TryGetKnownPopupRect(int popupId, out ClipRect rect)
        => _popupRectsCurrent.TryGetValue(popupId, out rect) || _popupRectsPrev.TryGetValue(popupId, out rect);

    private bool IsPopupOpen(int popupId) => _openPopupIds.Contains(popupId);

    private bool IsRootPopupModal()
        => _openPopupIds.Count > 0 &&
           (_modalPopupIdsCurrent.Contains(_openPopupIds[0]) || _modalPopupIdsPrev.Contains(_openPopupIds[0]));

    private void OpenPopupById(int popupId)
    {
        int prefixDepth = _popupContext.Count;
        if (prefixDepth == 0)
        {
            _openPopupIds.Clear();
            _openPopupIds.Add(popupId);
            return;
        }

        AlignOpenPopupPrefix(prefixDepth);
        if (_openPopupIds.Count == prefixDepth)
        {
            _openPopupIds.Add(popupId);
            return;
        }

        _openPopupIds[prefixDepth] = popupId;
        if (_openPopupIds.Count > prefixDepth + 1)
            _openPopupIds.RemoveRange(prefixDepth + 1, _openPopupIds.Count - prefixDepth - 1);
    }

    private void ClosePopupById(int popupId)
    {
        int index = _openPopupIds.IndexOf(popupId);
        if (index >= 0)
            _openPopupIds.RemoveRange(index, _openPopupIds.Count - index);
    }

    private void AlignOpenPopupPrefix(int prefixDepth)
    {
        int keepCount = 0;
        while (keepCount < prefixDepth &&
               keepCount < _openPopupIds.Count &&
               _openPopupIds[keepCount] == _popupContext[keepCount])
        {
            keepCount++;
        }

        if (keepCount < _openPopupIds.Count)
            _openPopupIds.RemoveRange(keepCount, _openPopupIds.Count - keepCount);

        for (int i = keepCount; i < prefixDepth; i++)
            _openPopupIds.Add(_popupContext[i]);
    }

    private bool CanQueuePopupRequest(int popupId, int depth)
    {
        if (depth >= _openPopupIds.Count || _openPopupIds[depth] != popupId)
            return false;

        for (int i = 0; i < depth; i++)
        {
            if (_openPopupIds[i] != _popupContext[i])
                return false;
        }

        return true;
    }

    private void RenderQueuedPopups()
    {
        if (_openPopupIds.Count == 0) return;
        if (!_popupRequestsByDepth.TryGetValue(0, out var request)) return;
        if (request.PopupId != _openPopupIds[0]) return;
        RenderPopupRequest(request, 0);
    }

    private bool PointInPopup(float x, float y, float w, float h)
        => _mouse.X >= x && _mouse.X < x + w &&
           _mouse.Y >= y && _mouse.Y < y + h &&
           CanHitCurrentContext();

    private void RenderPopupRequest(PopupRequest request, int depth)
    {
        if (!request.Content.HasContent || depth >= _openPopupIds.Count || _openPopupIds[depth] != request.PopupId)
            return;

        var state = GetState<PopupState>(request.PopupId);
        var pad = Theme.PopupPadding;
        float border = FrameBorderWidth;
        float scrollbarRadius = MathF.Min(FrameRadius, Theme.ScrollbarWidth * 0.5f);

        float outerW = MathF.Min(MathF.Max(0, request.Width), _vpW);
        float maxOuterH = MathF.Min(MathF.Max(0, request.MaxHeight), _vpH);
        float minOuterH = border * 2 + pad.Vertical;
        if (maxOuterH < minOuterH) maxOuterH = minOuterH;

        float estimatedContentH = state.ContentHeight > 0
            ? state.ContentHeight
            : MathF.Max(0, maxOuterH - border * 2 - pad.Vertical);
        float outerH = Math.Clamp(estimatedContentH + pad.Vertical + border * 2, minOuterH, maxOuterH);

        float x;
        float y;
        if (request.IsModal && depth == 0)
        {
            x = Math.Clamp((_vpW - outerW) * 0.5f, 0, MathF.Max(0, _vpW - outerW));
            y = Math.Clamp((_vpH - outerH) * 0.5f, 0, MathF.Max(0, _vpH - outerH));
        }
        else
        {
            x = Math.Clamp(request.AnchorX, 0, MathF.Max(0, _vpW - outerW));
            y = request.AnchorY;
            if (y + outerH > _vpH && y - outerH >= 0)
                y -= outerH;
            y = Math.Clamp(y, 0, MathF.Max(0, _vpH - outerH));
        }

        float previewViewH = MathF.Max(0, outerH - border * 2 - pad.Vertical);
        float previousContentHeight = MathF.Max(state.ContentHeight, previewViewH);
        float previousMaxScroll = MathF.Max(0, previousContentHeight - previewViewH);
        bool hadScrollbar = previousMaxScroll > 0.5f;
        float scrollbarReserve = hadScrollbar ? Theme.ScrollbarWidth + PopupScrollbarGap : 0;

        float viewX = x + border + pad.Left;
        float viewY = y + border + pad.Top;
        float viewW = MathF.Max(0, outerW - border * 2 - pad.Horizontal - scrollbarReserve);
        float viewH = previewViewH;
        float trackX = x + outerW - border - Theme.ScrollbarWidth;
        float trackY = viewY;
        float trackH = viewH;

        _popupRectsCurrent[request.PopupId] = new ClipRect(x, y, outerW, outerH);
        _popupContext.Add(request.PopupId);
        _idStack.Push(request.PopupId);

        try
        {
        bool changed = false;
        if (!IsMouseDown(UiMouseButton.Left)) state.DraggingThumb = false;

        bool hover = PointInPopup(x, y, outerW, outerH);
        if (hover) _hotId = request.PopupId;

        float thumbH = hadScrollbar
            ? MathF.Max(Theme.ScrollbarMinThumbSize, (viewH * viewH) / previousContentHeight)
            : 0;
        float thumbTravel = MathF.Max(0, viewH - thumbH);
        float thumbY = thumbTravel > 0 && previousMaxScroll > 0
            ? viewY + (state.ScrollY / previousMaxScroll) * thumbTravel
            : viewY;
        bool thumbHover = hadScrollbar && PointInPopup(trackX, thumbY, Theme.ScrollbarWidth, thumbH);

        bool mousePressed = IsMousePressed(UiMouseButton.Left);
        if (mousePressed && thumbHover)
        {
            _activeId = request.PopupId;
            state.DraggingThumb = true;
            state.ThumbDragOffsetY = _mouse.Y - thumbY;
        }

        if (hover && _input.WheelDelta.Y != 0 && !state.DraggingThumb)
        {
            float clampedBefore = Math.Clamp(state.ScrollY, 0, previousMaxScroll);
            float previous = state.ScrollY;
            state.ScrollY = Math.Clamp(clampedBefore - _input.WheelDelta.Y * Theme.ScrollWheelStep, 0, previousMaxScroll);
            changed |= MathF.Abs(state.ScrollY - previous) > 0.01f;
        }

        if (state.DraggingThumb && _activeId == request.PopupId && IsMouseDown(UiMouseButton.Left) && thumbTravel > 0 && previousMaxScroll > 0)
        {
            float thumbTop = Math.Clamp(_mouse.Y - state.ThumbDragOffsetY, viewY, viewY + thumbTravel);
            float scrollRatio = (thumbTop - viewY) / thumbTravel;
            float previous = state.ScrollY;
            state.ScrollY = scrollRatio * previousMaxScroll;
            changed |= MathF.Abs(state.ScrollY - previous) > 0.01f;
        }

        state.ScrollY = Math.Clamp(state.ScrollY, 0, previousMaxScroll);

        if (request.IsModal && depth == 0)
            _painter.DrawRect(0, 0, _vpW, _vpH, Theme.ModalBackdrop);

        DrawFrameRect(x, y, outerW, outerH, Theme.PopupBg, Theme.PopupBorder);

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
            if (request.ZeroItemSpacing)
                ItemSpacing(0);

            request.Content.Invoke(this);
            inner = _layouts[^1];
        }
        finally
        {
            _layouts.RemoveAt(_layouts.Count - 1);
            PopHitClip();
            _painter.PopClip();
        }

        float contentHeight = inner.Dir == LayoutDir.Horizontal
            ? inner.MaxExtent
            : inner.CursorY - inner.OriginY;

        state.ContentHeight = contentHeight;
        float maxScroll = MathF.Max(0, contentHeight - viewH);
        float clampedScroll = Math.Clamp(state.ScrollY, 0, maxScroll);
        changed |= MathF.Abs(clampedScroll - state.ScrollY) > 0.01f;
        state.ScrollY = clampedScroll;

        bool showScrollbar = maxScroll > 0.5f;
        if (!showScrollbar)
        {
            state.DraggingThumb = false;
        }
        else
        {
            float currentThumbH = MathF.Max(Theme.ScrollbarMinThumbSize, (viewH * viewH) / MathF.Max(contentHeight, viewH));
            float currentThumbTravel = MathF.Max(0, viewH - currentThumbH);
            float currentThumbY = currentThumbTravel > 0
                ? viewY + (state.ScrollY / maxScroll) * currentThumbTravel
                : viewY;

            bool currentThumbHover = PointInPopup(trackX, currentThumbY, Theme.ScrollbarWidth, currentThumbH);
            if (currentThumbHover || state.DraggingThumb)
                RequestCursor(UiCursor.PointingHand);

            _painter.DrawRect(trackX, trackY, Theme.ScrollbarWidth, trackH, Theme.ScrollbarTrack, radius: scrollbarRadius);
            Color thumbColor = state.DraggingThumb && _activeId == request.PopupId
                ? Theme.ScrollbarThumbActive
                : currentThumbHover
                    ? Theme.ScrollbarThumbHover
                    : Theme.ScrollbarThumb;
            _painter.DrawRect(trackX, currentThumbY, Theme.ScrollbarWidth, currentThumbH, thumbColor, radius: scrollbarRadius);
        }

        if (depth + 1 < _openPopupIds.Count &&
            _popupRequestsByDepth.TryGetValue(depth + 1, out var childRequest) &&
            childRequest.PopupId == _openPopupIds[depth + 1])
        {
            RenderPopupRequest(childRequest, depth + 1);
        }

        }
        finally
        {
            _idStack.Pop();
            _popupContext.RemoveAt(_popupContext.Count - 1);
        }
    }
}
