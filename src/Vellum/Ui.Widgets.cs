using System.Numerics;
using Vellum.Rendering;

namespace Vellum;

public sealed partial class Ui
{
    private sealed class ComboBoxState
    {
        public int HighlightedIndex = -1;
        public int PendingSelectedIndex = -1;
        public bool HasPendingSelection;
    }

    private float FrameBorderWidth => MathF.Max(0, Theme.BorderWidth);
    private float FrameRadius => MathF.Max(0, Theme.BorderRadius);

    private void DrawFrameRect(float x, float y, float width, float height, Color fill, Color border)
    {
        float strokeWidth = border.A > 0 ? FrameBorderWidth : 0;
        _painter.DrawRect(x, y, width, height, fill, border, strokeWidth, FrameRadius);
    }

    private ControlVisuals GetButtonVisuals(bool enabled, bool hover, bool pressed, bool focused)
    {
        Color fill = !enabled ? Theme.ButtonBg.WithAlpha(140)
            : pressed && hover ? Theme.ButtonBgPressed
            : hover ? Theme.ButtonBgHover
            : Theme.ButtonBg;
        Color border = !enabled ? Theme.ButtonBorder.WithAlpha(140)
            : focused ? Theme.FocusBorder
            : pressed && hover ? Theme.ButtonBorderPressed
            : hover ? Theme.ButtonBorderHover
            : Theme.ButtonBorder;
        Color foreground = enabled ? Theme.TextPrimary : Theme.TextPrimary.WithAlpha(140);
        return new ControlVisuals(fill, border, foreground);
    }

    private ControlVisuals GetCollapsingHeaderVisuals(bool enabled, bool hover, bool pressed, bool open, bool focused)
    {
        Color fill = !enabled ? Theme.CollapsingHeaderBgHover.WithAlpha(110)
            : pressed && hover ? Theme.CollapsingHeaderBgPressed
            : hover ? Theme.CollapsingHeaderBgHover
            : open ? Theme.CollapsingHeaderBgOpen
            : Theme.CollapsingHeaderBg;
        Color border = !enabled ? Theme.Separator.WithAlpha(110)
            : focused ? Theme.FocusBorder
            : pressed && hover ? Theme.Separator.WithAlpha(230)
            : hover ? Theme.Separator.WithAlpha(205)
            : open ? Theme.Separator.WithAlpha(185)
            : Theme.Separator.WithAlpha(150);
        Color foreground = enabled ? Theme.TextPrimary : Theme.TextPrimary.WithAlpha(140);
        return new ControlVisuals(fill, border, foreground);
    }

    private ControlVisuals GetToggleVisuals(bool enabled, bool hover, bool pressed, bool active, bool focused)
    {
        Color fill = !enabled ? Theme.ToggleBg.WithAlpha(140)
            : active ? Theme.ToggleBgActive
            : pressed && hover ? Theme.ToggleBgPressed
            : hover ? Theme.ToggleBgHover
            : Theme.ToggleBg;
        Color border = !enabled ? Theme.ToggleBorder.WithAlpha(140)
            : focused ? Theme.FocusBorder
            : active ? Theme.ToggleBorderActive
            : pressed && hover ? Theme.ToggleBorderPressed
            : hover ? Theme.ToggleBorderHover
            : Theme.ToggleBorder;
        Color foreground = enabled ? Theme.TextPrimary : Theme.TextPrimary.WithAlpha(140);
        return new ControlVisuals(fill, border, foreground);
    }

    private ControlVisuals GetSwitchVisuals(bool enabled, bool hover, bool pressed, bool active, bool focused)
    {
        Color fill = !enabled ? Theme.ToggleBg.WithAlpha(140)
            : active ? (pressed && hover ? Theme.Accent.WithAlpha(112)
                : hover ? Theme.Accent.WithAlpha(132)
                : Theme.Accent.WithAlpha(120))
            : pressed && hover ? Theme.ToggleBgPressed
            : hover ? Theme.ToggleBgHover
            : Theme.ToggleBg;
        Color border = !enabled ? Theme.ToggleBorder.WithAlpha(140)
            : focused ? Theme.FocusBorder
            : active ? Theme.ToggleBorderActive
            : pressed && hover ? Theme.ToggleBorderPressed
            : hover ? Theme.ToggleBorderHover
            : Theme.ToggleBorder;
        Color foreground = enabled ? Theme.TextPrimary : Theme.TextPrimary.WithAlpha(140);
        return new ControlVisuals(fill, border, foreground);
    }

    private Color GetSwitchThumbColor(bool enabled, bool hover, bool pressed, bool active)
    {
        if (!enabled)
            return Theme.TextSecondary.WithAlpha(140);

        if (active)
            return pressed && hover ? Color.White.WithAlpha(245) : Color.White;

        return hover || pressed
            ? Theme.TextPrimary
            : Theme.TextSecondary;
    }

    private ControlVisuals GetSelectableVisuals(bool enabled, bool hover, bool pressed, bool selected, bool focused)
    {
        Color fill = !enabled ? Theme.SelectableBg.WithAlpha(140)
            : selected ? Theme.SelectableBgSelected
            : pressed && hover ? Theme.SelectableBgPressed
            : hover ? Theme.SelectableBgHover
            : Theme.SelectableBg;
        Color border = !enabled ? Theme.SelectableBorder.WithAlpha(140)
            : focused ? Theme.FocusBorder
            : selected ? Theme.SelectableBorderSelected
            : pressed && hover ? Theme.SelectableBorderPressed
            : hover ? Theme.SelectableBorderHover
            : Theme.SelectableBorder;
        Color foreground = enabled ? Theme.TextPrimary : Theme.TextPrimary.WithAlpha(140);
        return new ControlVisuals(fill, border, foreground);
    }

    private ControlVisuals GetComboBoxVisuals(bool enabled, bool hover, bool pressed, bool open, bool focused)
    {
        Color fill = !enabled ? Theme.ButtonBg.WithAlpha(140)
            : open ? Theme.ButtonBgHover
            : pressed && hover ? Theme.ButtonBgPressed
            : hover ? Theme.ButtonBgHover
            : Theme.ButtonBg;
        Color border = !enabled ? Theme.ButtonBorder.WithAlpha(140)
            : focused ? Theme.FocusBorder
            : open ? Theme.ButtonBorderHover
            : pressed && hover ? Theme.ButtonBorderPressed
            : hover ? Theme.ButtonBorderHover
            : Theme.ButtonBorder;
        Color foreground = enabled ? Theme.TextPrimary : Theme.TextPrimary.WithAlpha(140);
        return new ControlVisuals(fill, border, foreground);
    }

    private static int ClampComboBoxIndex(int index, int optionCount)
    {
        if (optionCount <= 0)
            return -1;

        return Math.Clamp(index, 0, optionCount - 1);
    }

    private float EstimateComboBoxContentHeight(float itemHeight, int optionCount)
        => optionCount <= 0 ? 0 : itemHeight * optionCount;

    private float GetComboBoxPopupViewHeight(float maxPopupHeight, float itemHeight, int optionCount)
    {
        float border = FrameBorderWidth;
        var pad = Theme.PopupPadding;
        float maxOuterH = MathF.Min(MathF.Max(0, maxPopupHeight), _vpH);
        float minOuterH = border * 2 + pad.Vertical;
        if (maxOuterH < minOuterH) maxOuterH = minOuterH;

        float contentHeight = EstimateComboBoxContentHeight(itemHeight, optionCount);
        float outerH = Math.Clamp(contentHeight + pad.Vertical + border * 2, minOuterH, maxOuterH);
        return MathF.Max(0, outerH - border * 2 - pad.Vertical);
    }

    private void EnsureComboBoxItemVisible(int popupId, int highlightedIndex, int optionCount, float itemHeight, float maxPopupHeight)
    {
        if (highlightedIndex < 0 || optionCount <= 0 || itemHeight <= 0)
            return;

        var popupState = GetState<PopupState>(popupId);
        float viewHeight = GetComboBoxPopupViewHeight(maxPopupHeight, itemHeight, optionCount);
        float estimatedContent = EstimateComboBoxContentHeight(itemHeight, optionCount);
        float contentHeight = MathF.Max(popupState.ContentHeight, estimatedContent);
        float maxScroll = MathF.Max(0, contentHeight - viewHeight);
        float nextScroll = Math.Clamp(popupState.ScrollY, 0, maxScroll);

        float itemTop = highlightedIndex * itemHeight;
        float itemBottom = itemTop + itemHeight;
        if (itemTop < nextScroll)
            nextScroll = itemTop;
        else if (itemBottom > nextScroll + viewHeight)
            nextScroll = itemBottom - viewHeight;

        popupState.ScrollY = Math.Clamp(nextScroll, 0, maxScroll);
    }

    private static float NormalizeSliderValue(float value, float min, float max)
    {
        if (max <= min) return 0;
        return Math.Clamp((value - min) / (max - min), 0, 1);
    }

    private static float SnapSliderValue(float value, float min, float max, float? step)
    {
        float clamped = Math.Clamp(value, min, max);
        if (!step.HasValue || step.Value <= 0)
            return clamped;

        float steps = MathF.Round((clamped - min) / step.Value);
        return Math.Clamp(min + steps * step.Value, min, max);
    }

    private float ResolveSliderKeyboardStep(float min, float max, float? step)
    {
        if (step.HasValue && step.Value > 0)
            return step.Value;
        if (Theme.SliderKeyboardStep > 0)
            return Theme.SliderKeyboardStep;

        float range = MathF.Abs(max - min);
        return range <= 0 ? 0 : range / 100f;
    }

    private static float SliderValueFromMouse(float mouseX, float x, float width, float blockWidth, float min, float max, float? step)
    {
        if (max <= min)
            return min;

        float travel = MathF.Max(0, width - blockWidth);
        float t = travel > 0
            ? Math.Clamp((mouseX - x - blockWidth * 0.5f) / travel, 0, 1)
            : 0;
        float value = min + (max - min) * t;
        return SnapSliderValue(value, min, max, step);
    }

    private static string FormatSliderValue(float value, string? format)
    {
        string fmt = format ?? "{0:0.###}";
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, fmt, value);
    }

    private static string FormatSliderValue(int value, string? format)
    {
        string fmt = format ?? "{0:0}";
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, fmt, value);
    }

    private static string BuildSliderDisplay(string? label, string valueText)
        => string.IsNullOrWhiteSpace(label) ? valueText : $"{label} ({valueText})";

    private float ResolveSliderBlockWidth(float innerWidth)
    {
        if (innerWidth <= 0)
            return 0;

        if (Theme.SliderBlockWidthFactor > 0)
            return Math.Clamp(innerWidth * Theme.SliderBlockWidthFactor, 0, innerWidth);

        return MathF.Min(MathF.Max(0, Theme.SliderBlockWidth), innerWidth);
    }

    private static void ResolveHistogramScale(ReadOnlySpan<float> values, float? scaleMin, float? scaleMax,
        out float minValue, out float maxValue)
    {
        if (values.Length > 0)
        {
            float computedMin = values[0];
            float computedMax = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                computedMin = MathF.Min(computedMin, values[i]);
                computedMax = MathF.Max(computedMax, values[i]);
            }

            minValue = scaleMin ?? computedMin;
            maxValue = scaleMax ?? computedMax;
        }
        else
        {
            minValue = scaleMin ?? 0f;
            maxValue = scaleMax ?? 1f;
        }

        if (maxValue > minValue)
            return;

        if (!scaleMin.HasValue && maxValue > 0f)
        {
            minValue = 0f;
            if (maxValue > minValue)
                return;
        }

        if (!scaleMax.HasValue && minValue < 0f)
        {
            maxValue = 0f;
            if (maxValue > minValue)
                return;
        }

        maxValue = minValue + 1f;
    }

    /// <summary>Draws non-interactive text.</summary>
    public Response Label(
        string text,
        float? size = null,
        Color? color = null,
        float? maxWidth = null,
        TextWrapMode wrap = TextWrapMode.NoWrap,
        TextOverflowMode overflow = TextOverflowMode.Visible,
        int maxLines = int.MaxValue,
        float? width = null,
        UiAlign align = UiAlign.Start)
    {
        float s = size ?? DefaultFontSize;
        Color c = color ?? Theme.TextPrimary;
        var layout = LayoutText(text, s, maxWidth, wrap, overflow, maxLines);
        float resolvedWidth = width.HasValue ? MathF.Max(width.Value, layout.Width) : layout.Width;
        var (x, y) = Place(resolvedWidth, layout.Height);
        float textX = align switch
        {
            UiAlign.Center => x + MathF.Max(0, (resolvedWidth - layout.Width) * 0.5f),
            UiAlign.End => x + MathF.Max(0, resolvedWidth - layout.Width),
            _ => x
        };
        DrawTextLayout(layout, textX, y, c);
        Advance(resolvedWidth, layout.Height);
        bool hover = PointIn(x, y, resolvedWidth, layout.Height);
        return new Response(x, y, resolvedWidth, layout.Height, hover, false, false);
    }

    /// <summary>Draws prominent heading text.</summary>
    public Response Heading(string text) =>
        Label(text, size: DefaultFontSize * 1.75f, color: Theme.TextPrimary);

    /// <summary>Draws a horizontal or vertical separator depending on the current layout direction.</summary>
    public Response Separator(float? length = null, float thickness = 1f, Color? color = null)
    {
        bool vertical = _layouts.Count > 0 && Top.Dir == LayoutDir.Horizontal;
        float resolvedThickness = MathF.Max(0, thickness);
        float resolvedLength = length ?? (vertical ? DefaultFontSize + 6f : AvailableWidth);
        float w = vertical ? resolvedThickness : resolvedLength;
        float h = vertical ? resolvedLength : resolvedThickness;

        var (x, y) = Place(w, h);
        Color separatorColor = color ?? Theme.Separator;
        if (w > 0 && h > 0 && separatorColor.A > 0)
            _painter.DrawRect(x, y, w, h, separatorColor, radius: MathF.Min(FrameRadius, resolvedThickness * 0.5f));

        Advance(w, h);
        return new Response(x, y, w, h, false, false, false);
    }

    /// <summary>Draws a draggable vertical splitter for horizontal pane layouts.</summary>
    public Response Splitter(
        string id,
        ref float beforeSize,
        float min = 0f,
        float max = float.PositiveInfinity,
        float thickness = 6f,
        float? height = null,
        bool enabled = true,
        float keyboardStep = 8f)
    {
        enabled = ResolveEnabled(enabled);
        if (max < min) (min, max) = (max, min);
        beforeSize = Math.Clamp(beforeSize, min, max);

        int widgetId = MakeId(id);
        float w = MathF.Max(1f, thickness);
        float h = MathF.Max(1f, height ?? (DefaultFontSize + 6f));
        var (x, y) = Place(w, h);

        bool focused = RegisterFocusable(widgetId, enabled);
        bool hover = enabled && PointIn(x, y, w, h);
        if (hover) _hotId = widgetId;

        if (enabled && hover && IsMousePressed(UiMouseButton.Left))
        {
            _activeId = widgetId;
            SetFocus(widgetId);
            focused = true;
        }

        bool pressed = enabled && _activeId == widgetId && IsMouseDown(UiMouseButton.Left);
        if (enabled && (hover || pressed))
            RequestCursor(UiCursor.ResizeEW);

        bool changed = false;
        if (enabled && pressed && _mouseDelta.X != 0)
        {
            float next = Math.Clamp(beforeSize + _mouseDelta.X, min, max);
            if (MathF.Abs(next - beforeSize) > 0.01f)
            {
                beforeSize = next;
                changed = true;
            }
        }

        if (enabled && focused && !pressed)
        {
            float next = beforeSize;
            float step = MathF.Max(0.1f, keyboardStep);
            if (_input.IsPressed(UiKey.Left)) next -= step;
            if (_input.IsPressed(UiKey.Right)) next += step;
            if (_input.IsPressed(UiKey.Home)) next = min;
            if (!float.IsPositiveInfinity(max) && _input.IsPressed(UiKey.End)) next = max;
            next = Math.Clamp(next, min, max);
            if (MathF.Abs(next - beforeSize) > 0.01f)
            {
                beforeSize = next;
                changed = true;
            }
        }

        Color color = !enabled
            ? Theme.Separator.WithAlpha(90)
            : pressed
                ? Theme.ScrollbarThumbActive
                : hover || focused
                    ? Theme.ScrollbarThumbHover
                    : Theme.Separator;
        float lineW = MathF.Min(w, MathF.Max(2f, MathF.Floor(w * 0.4f)));
        float lineX = x + (w - lineW) * 0.5f;
        _painter.DrawRect(lineX, y, lineW, h, color, radius: MathF.Min(FrameRadius, lineW * 0.5f));

        Advance(w, h);
        return new Response(
            x,
            y,
            w,
            h,
            hover,
            pressed,
            false,
            focused: focused,
            changed: changed,
            disabled: !enabled);
    }

    /// <summary>Draws an auto-height panel using the current available width.</summary>
    public Response Panel(Action<Ui> content)
        => Panel(null, AvailableWidth, content);

    /// <inheritdoc cref="Panel(Action{Ui})" />
    public Response Panel<TState>(TState state, Action<Ui, TState> content)
        => Panel(null, AvailableWidth, state, content);

    /// <summary>Draws an auto-height panel with an optional id.</summary>
    public Response Panel(string? id, Action<Ui> content)
        => Panel(id, AvailableWidth, content);

    /// <inheritdoc cref="Panel(string?, Action{Ui})" />
    public Response Panel<TState>(string? id, TState state, Action<Ui, TState> content)
        => Panel(id, AvailableWidth, state, content);

    /// <summary>Draws an auto-height panel with explicit width.</summary>
    public Response Panel(float width, Action<Ui> content)
        => Panel(null, width, content);

    /// <inheritdoc cref="Panel(float, Action{Ui})" />
    public Response Panel<TState>(float width, TState state, Action<Ui, TState> content)
        => Panel(null, width, state, content);

    /// <summary>Draws an auto-height panel with optional id and explicit width.</summary>
    public Response Panel(string? id, float width, Action<Ui> content)
    {
        float resolvedWidth = MathF.Max(0, width);
        var (x, y) = Place(resolvedWidth, 0);
        float border = FrameBorderWidth;
        var pad = Theme.PanelPadding;
        float innerX = x + border + pad.Left;
        float innerY = y + border + pad.Top;
        float innerW = MathF.Max(0, resolvedWidth - border * 2 - pad.Horizontal);

        var parentPainter = _painter;
        var contentPainter = AcquireDeferredPainter();
        _painter = contentPainter;

        if (!string.IsNullOrEmpty(id))
            PushId(id);

        _layouts.Add(new LayoutScope
        {
            OriginX = innerX,
            OriginY = innerY,
            CursorX = innerX,
            CursorY = innerY,
            Dir = LayoutDir.Vertical,
            WidthConstraint = innerW,
            HasWidthConstraint = true,
            Empty = true
        });

        float innerH;
        bool contentCompleted = false;
        try
        {
            content(this);

            var inner = _layouts[^1];
            innerH = inner.Dir == LayoutDir.Horizontal
                ? inner.MaxExtent
                : inner.CursorY - inner.OriginY;
            contentCompleted = true;
        }
        finally
        {
            _layouts.RemoveAt(_layouts.Count - 1);

            if (!string.IsNullOrEmpty(id))
                PopId();

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

    /// <inheritdoc cref="Panel(string?, float, Action{Ui})" />
    public Response Panel<TState>(string? id, float width, TState state, Action<Ui, TState> content)
    {
        float resolvedWidth = MathF.Max(0, width);
        var (x, y) = Place(resolvedWidth, 0);
        float border = FrameBorderWidth;
        var pad = Theme.PanelPadding;
        float innerX = x + border + pad.Left;
        float innerY = y + border + pad.Top;
        float innerW = MathF.Max(0, resolvedWidth - border * 2 - pad.Horizontal);

        var parentPainter = _painter;
        var contentPainter = AcquireDeferredPainter();
        _painter = contentPainter;

        if (!string.IsNullOrEmpty(id))
            PushId(id);

        _layouts.Add(new LayoutScope
        {
            OriginX = innerX,
            OriginY = innerY,
            CursorX = innerX,
            CursorY = innerY,
            Dir = LayoutDir.Vertical,
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

            if (!string.IsNullOrEmpty(id))
                PopId();

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

    /// <summary>Draws a fixed-size panel.</summary>
    public Response Panel(float width, float height, Action<Ui> content, bool clip = true)
        => Panel(null, width, height, content, clip);

    /// <inheritdoc cref="Panel(float, float, Action{Ui}, bool)" />
    public Response Panel<TState>(float width, float height, TState state, Action<Ui, TState> content, bool clip = true)
        => Panel(null, width, height, state, content, clip);

    /// <summary>Draws a fixed-size panel with an optional id.</summary>
    public Response Panel(string? id, float width, float height, Action<Ui> content, bool clip = true)
    {
        float resolvedWidth = MathF.Max(0, width);
        float resolvedHeight = MathF.Max(0, height);
        var (x, y) = Place(resolvedWidth, resolvedHeight);
        bool hover = PointIn(x, y, resolvedWidth, resolvedHeight);

        DrawFrameRect(x, y, resolvedWidth, resolvedHeight, Theme.PanelBg, Theme.PanelBorder);

        float border = FrameBorderWidth;
        var pad = Theme.PanelPadding;
        float innerX = x + border + pad.Left;
        float innerY = y + border + pad.Top;
        float innerW = MathF.Max(0, resolvedWidth - border * 2 - pad.Horizontal);
        float innerH = MathF.Max(0, resolvedHeight - border * 2 - pad.Vertical);

        if (clip)
        {
            _painter.PushClip(innerX, innerY, innerW, innerH);
            PushHitClip(innerX, innerY, innerW, innerH);
        }

        if (!string.IsNullOrEmpty(id))
            PushId(id);

        _layouts.Add(new LayoutScope
        {
            OriginX = innerX,
            OriginY = innerY,
            CursorX = innerX,
            CursorY = innerY,
            Dir = LayoutDir.Vertical,
            WidthConstraint = innerW,
            HasWidthConstraint = true,
            Empty = true
        });

        try
        {
            content(this);
        }
        finally
        {
            _layouts.RemoveAt(_layouts.Count - 1);

            if (!string.IsNullOrEmpty(id))
                PopId();

            if (clip)
            {
                PopHitClip();
                _painter.PopClip();
            }
        }

        Advance(resolvedWidth, resolvedHeight);
        return new Response(x, y, resolvedWidth, resolvedHeight, hover, false, false);
    }

    /// <inheritdoc cref="Panel(string?, float, float, Action{Ui}, bool)" />
    public Response Panel<TState>(string? id, float width, float height, TState state, Action<Ui, TState> content, bool clip = true)
    {
        float resolvedWidth = MathF.Max(0, width);
        float resolvedHeight = MathF.Max(0, height);
        var (x, y) = Place(resolvedWidth, resolvedHeight);
        bool hover = PointIn(x, y, resolvedWidth, resolvedHeight);

        DrawFrameRect(x, y, resolvedWidth, resolvedHeight, Theme.PanelBg, Theme.PanelBorder);

        float border = FrameBorderWidth;
        var pad = Theme.PanelPadding;
        float innerX = x + border + pad.Left;
        float innerY = y + border + pad.Top;
        float innerW = MathF.Max(0, resolvedWidth - border * 2 - pad.Horizontal);
        float innerH = MathF.Max(0, resolvedHeight - border * 2 - pad.Vertical);

        if (clip)
        {
            _painter.PushClip(innerX, innerY, innerW, innerH);
            PushHitClip(innerX, innerY, innerW, innerH);
        }

        if (!string.IsNullOrEmpty(id))
            PushId(id);

        _layouts.Add(new LayoutScope
        {
            OriginX = innerX,
            OriginY = innerY,
            CursorX = innerX,
            CursorY = innerY,
            Dir = LayoutDir.Vertical,
            WidthConstraint = innerW,
            HasWidthConstraint = true,
            Empty = true
        });

        try
        {
            content(this, state);
        }
        finally
        {
            _layouts.RemoveAt(_layouts.Count - 1);

            if (!string.IsNullOrEmpty(id))
                PopId();

            if (clip)
            {
                PopHitClip();
                _painter.PopClip();
            }
        }

        Advance(resolvedWidth, resolvedHeight);
        return new Response(x, y, resolvedWidth, resolvedHeight, hover, false, false);
    }

    /// <summary>Reserves a custom drawing surface in the layout.</summary>
    public Response Canvas(float width, float height, Action<UiCanvas> draw, bool clip = true)
    {
        var (x, y) = Place(width, height);
        bool hover = PointIn(x, y, width, height);

        if (clip)
        {
            _painter.PushClip(x, y, width, height);
            PushHitClip(x, y, width, height);
        }

        try
        {
            draw(new UiCanvas(this, x, y, width, height));
        }
        finally
        {
            if (clip)
            {
                PopHitClip();
                _painter.PopClip();
            }
        }

        Advance(width, height);
        return new Response(x, y, width, height, hover, false, false);
    }

    /// <inheritdoc cref="Canvas(float, float, Action{UiCanvas}, bool)" />
    public Response Canvas<TState>(float width, float height, TState state, Action<UiCanvas, TState> draw, bool clip = true)
    {
        var (x, y) = Place(width, height);
        bool hover = PointIn(x, y, width, height);

        if (clip)
        {
            _painter.PushClip(x, y, width, height);
            PushHitClip(x, y, width, height);
        }

        try
        {
            draw(new UiCanvas(this, x, y, width, height), state);
        }
        finally
        {
            if (clip)
            {
                PopHitClip();
                _painter.PopClip();
            }
        }

        Advance(width, height);
        return new Response(x, y, width, height, hover, false, false);
    }

    /// <summary>Draws a clickable button.</summary>
    public Response Button(string label, float? width = null, float? size = null, bool enabled = true, string? id = null)
    {
        enabled = ResolveEnabled(enabled);
        float s = size ?? DefaultFontSize;
        var pad = Theme.ButtonPadding;
        var layout = LayoutText(label, s);
        float intrinsicW = layout.Width + pad.Horizontal;
        float w = width.HasValue ? MathF.Max(width.Value, intrinsicW) : intrinsicW;
        float h = layout.Height + pad.Vertical;

        int widgetId = MakeId(id ?? label);
        var (x, y) = Place(w, h);

        bool focused = RegisterFocusable(widgetId, enabled);
        bool hover = enabled && PointIn(x, y, w, h);
        if (hover) _hotId = widgetId;
        if (hover) RequestCursor(UiCursor.PointingHand);

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

        var visuals = GetButtonVisuals(enabled, hover, pressed, focused);
        DrawFrameRect(x, y, w, h, visuals.Fill, visuals.Border);
        float textX = x + MathF.Max(pad.Left, (w - layout.Width) * 0.5f);
        DrawTextLayout(layout, textX, y + pad.Top, visuals.Foreground);

        Advance(w, h);
        return new Response(x, y, w, h, hover, pressed, clicked, focused: focused, disabled: !enabled);
    }

    /// <summary>Draws a checkbox bound to a boolean value.</summary>
    public Response Checkbox(string label, ref bool value, float? width = null, float? size = null, bool enabled = true, string? id = null)
    {
        enabled = ResolveEnabled(enabled);
        float s = size ?? DefaultFontSize;
        var layout = LayoutText(label, s);
        float indicatorSize = MathF.Max(16f, MathF.Ceiling(s));
        float spacing = Theme.Gap;
        float intrinsicW = indicatorSize + spacing + layout.Width;
        float w = width.HasValue ? MathF.Max(width.Value, intrinsicW) : intrinsicW;
        float h = MathF.Max(indicatorSize, layout.Height);

        int widgetId = MakeId(id ?? label);
        var (x, y) = Place(w, h);

        bool focused = RegisterFocusable(widgetId, enabled);
        bool hover = enabled && PointIn(x, y, w, h);
        if (hover) _hotId = widgetId;
        if (hover) RequestCursor(UiCursor.PointingHand);

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

        bool changed = false;
        if (clicked)
        {
            value = !value;
            changed = true;
        }

        var visuals = GetToggleVisuals(enabled, hover, pressed, value, focused);
        float indicatorY = y + (h - indicatorSize) * 0.5f;
        float indicatorRadius = MathF.Min(FrameRadius, indicatorSize * 0.28f);
        _painter.DrawRect(x, indicatorY, indicatorSize, indicatorSize, visuals.Fill, visuals.Border, FrameBorderWidth, indicatorRadius);

        if (value)
        {
            float inset = MathF.Max(3f, indicatorSize * 0.22f);
            float indicatorInnerSize = MathF.Max(0, indicatorSize - inset * 2);
            if (indicatorInnerSize > 0)
            {
                float innerRadius = MathF.Min(indicatorRadius * 0.75f, indicatorInnerSize * 0.22f);
                _painter.DrawRect(
                    x + inset,
                    indicatorY + inset,
                    indicatorInnerSize,
                    indicatorInnerSize,
                    Theme.ToggleIndicator,
                    radius: innerRadius);
            }
        }

        float textX = x + indicatorSize + spacing;
        float textY = y + (h - layout.Height) * 0.5f;
        DrawTextLayout(layout, textX, textY, visuals.Foreground);

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
            changed: changed,
            disabled: !enabled,
            toggled: changed);
    }

    /// <summary>Draws an on/off switch bound to a boolean value.</summary>
    public Response Switch(string label, ref bool value, float? width = null, float? size = null, bool enabled = true, string? id = null)
    {
        enabled = ResolveEnabled(enabled);
        float s = size ?? DefaultFontSize;
        var layout = LayoutText(label, s);
        float trackHeight = MathF.Max(18f, MathF.Ceiling(s * 1.05f));
        float trackWidth = MathF.Ceiling(trackHeight * 1.9f);
        float labelGap = string.IsNullOrEmpty(label) ? 0f : Theme.Gap;
        float intrinsicW = trackWidth + labelGap + layout.Width;
        float w = width.HasValue ? MathF.Max(width.Value, intrinsicW) : intrinsicW;
        float h = MathF.Max(trackHeight, layout.Height);

        int widgetId = MakeId(id ?? label);
        var (x, y) = Place(w, h);

        bool focused = RegisterFocusable(widgetId, enabled);
        bool hover = enabled && PointIn(x, y, w, h);
        if (hover) _hotId = widgetId;
        if (hover) RequestCursor(UiCursor.PointingHand);

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

        bool changed = false;
        if (clicked)
        {
            value = !value;
            changed = true;
        }

        var visuals = GetSwitchVisuals(enabled, hover, pressed, value, focused);
        float trackY = y + (h - trackHeight) * 0.5f;
        float trackRadius = trackHeight * 0.5f;
        _painter.DrawRect(x, trackY, trackWidth, trackHeight, visuals.Fill, visuals.Border, FrameBorderWidth, trackRadius);

        float thumbInset = MathF.Max(2f, trackHeight * 0.12f);
        float thumbSize = MathF.Max(0, trackHeight - thumbInset * 2);
        float thumbX = value
            ? x + trackWidth - thumbInset - thumbSize
            : x + thumbInset;
        float thumbY = trackY + thumbInset;
        Color thumbColor = GetSwitchThumbColor(enabled, hover, pressed, value);
        Color thumbBorder = enabled ? Theme.PanelBorder.WithAlpha(110) : Theme.PanelBorder.WithAlpha(60);
        float thumbStrokeWidth = thumbBorder.A > 0 ? MathF.Max(1f, FrameBorderWidth) : 0f;
        _painter.DrawRect(thumbX, thumbY, thumbSize, thumbSize, thumbColor, thumbBorder, thumbStrokeWidth, thumbSize * 0.5f);

        if (!string.IsNullOrEmpty(label))
        {
            float textX = x + trackWidth + labelGap;
            float textY = y + (h - layout.Height) * 0.5f;
            DrawTextLayout(layout, textX, textY, visuals.Foreground);
        }

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
            changed: changed,
            disabled: !enabled,
            toggled: changed);
    }

    /// <summary>Draws a radio button.</summary>
    public Response RadioButton(string label, bool selected, float? width = null, float? size = null, bool enabled = true, string? id = null)
    {
        enabled = ResolveEnabled(enabled);
        float s = size ?? DefaultFontSize;
        var layout = LayoutText(label, s);
        float indicatorSize = MathF.Max(16f, MathF.Ceiling(s));
        float spacing = Theme.Gap;
        float intrinsicW = indicatorSize + spacing + layout.Width;
        float w = width.HasValue ? MathF.Max(width.Value, intrinsicW) : intrinsicW;
        float h = MathF.Max(indicatorSize, layout.Height);

        int widgetId = MakeId(id ?? label);
        var (x, y) = Place(w, h);

        bool focused = RegisterFocusable(widgetId, enabled);
        bool hover = enabled && PointIn(x, y, w, h);
        if (hover) _hotId = widgetId;
        if (hover) RequestCursor(UiCursor.PointingHand);

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

        bool changed = clicked && !selected;
        bool currentSelected = selected || changed;

        var visuals = GetToggleVisuals(enabled, hover, pressed, currentSelected, focused);
        float indicatorY = y + (h - indicatorSize) * 0.5f;
        float outerRadius = indicatorSize * 0.5f;
        _painter.DrawRect(x, indicatorY, indicatorSize, indicatorSize, visuals.Fill, visuals.Border, FrameBorderWidth, outerRadius);

        if (currentSelected)
        {
            float innerSize = MathF.Max(0, indicatorSize * 0.44f);
            float innerOffset = (indicatorSize - innerSize) * 0.5f;
            _painter.DrawRect(
                x + innerOffset,
                indicatorY + innerOffset,
                innerSize,
                innerSize,
                Theme.ToggleIndicator,
                radius: innerSize * 0.5f);
        }

        float textX = x + indicatorSize + spacing;
        float textY = y + (h - layout.Height) * 0.5f;
        DrawTextLayout(layout, textX, textY, visuals.Foreground);

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
            changed: changed,
            disabled: !enabled,
            toggled: changed);
    }

    /// <summary>Draws a radio button that assigns a value when selected.</summary>
    public Response RadioValue<T>(string label, ref T current, T value, float? width = null, float? size = null, bool enabled = true, string? id = null)
    {
        bool selected = EqualityComparer<T>.Default.Equals(current, value);
        Response response = RadioButton(label, selected, width, size, enabled, id);
        if (response.Changed && !selected)
            current = value;

        return response;
    }

    /// <summary>Draws a progress bar for a normalized value from 0 to 1.</summary>
    public Response ProgressBar(float value, float width, float height = 22f, string? overlay = null, float? size = null)
    {
        float clampedValue = Math.Clamp(value, 0, 1);
        float overlaySize = size ?? MathF.Max(12f, DefaultFontSize * 0.78f);
        TextLayoutResult? overlayLayout = string.IsNullOrEmpty(overlay) ? null : LayoutText(overlay, overlaySize);

        float minHeight = overlayLayout is null
            ? height
            : MathF.Max(height, overlayLayout.Value.Height + FrameBorderWidth * 2 + 6f);
        var (x, y) = Place(width, minHeight);

        DrawFrameRect(x, y, width, minHeight, Theme.ProgressBarBg, Theme.ProgressBarBorder);

        float innerX = x + FrameBorderWidth;
        float innerY = y + FrameBorderWidth;
        float innerH = MathF.Max(0, minHeight - FrameBorderWidth * 2);
        float innerW = MathF.Max(0, width - FrameBorderWidth * 2);
        float fillW = innerW * clampedValue;
        if (fillW > 0)
        {
            float fillRadius = MathF.Min(MathF.Max(0, FrameRadius - FrameBorderWidth), innerH * 0.5f);
            _painter.DrawRect(innerX, innerY, fillW, innerH, Theme.ProgressBarFill, radius: fillRadius);
        }

        if (overlayLayout is not null)
        {
            float textX = x + MathF.Max(0, (width - overlayLayout.Value.Width) * 0.5f);
            float textY = y + MathF.Max(0, (minHeight - overlayLayout.Value.Height) * 0.5f);
            DrawTextLayout(overlayLayout.Value, textX, textY, Theme.TextPrimary);
        }

        Advance(width, minHeight);
        bool hover = PointIn(x, y, width, minHeight);
        return new Response(x, y, width, minHeight, hover, false, false);
    }

    /// <summary>Draws a compact histogram.</summary>
    public Response Histogram(ReadOnlySpan<float> values, float width, float height = 80f, string? overlay = null,
        float? scaleMin = null, float? scaleMax = null, float? size = null)
    {
        float resolvedWidth = MathF.Max(0, width);
        float resolvedHeight = MathF.Max(0, height);
        float overlaySize = size ?? MathF.Max(12f, DefaultFontSize * 0.78f);
        TextLayoutResult? overlayLayout = string.IsNullOrEmpty(overlay) ? null : LayoutText(overlay, overlaySize);

        float minHeight = overlayLayout is null
            ? resolvedHeight
            : MathF.Max(resolvedHeight, overlayLayout.Value.Height + FrameBorderWidth * 2 + 6f);
        var (x, y) = Place(resolvedWidth, minHeight);

        DrawFrameRect(x, y, resolvedWidth, minHeight, Theme.PlotBg, Theme.PlotBorder);

        float innerX = x + FrameBorderWidth;
        float innerY = y + FrameBorderWidth;
        float innerH = MathF.Max(0, minHeight - FrameBorderWidth * 2);
        float innerW = MathF.Max(0, resolvedWidth - FrameBorderWidth * 2);

        if (values.Length > 0 && innerW > 0 && innerH > 0)
        {
            ResolveHistogramScale(values, scaleMin, scaleMax, out float minValue, out float maxValue);

            float baselineY = maxValue <= 0f
                ? innerY
                : minValue >= 0f
                    ? innerY + innerH
                    : innerY + innerH * (1f - ((0f - minValue) / (maxValue - minValue)));

            float barPitch = innerW / values.Length;
            float barGap = barPitch >= 6f ? 1f : 0f;
            float zeroLineThickness = MathF.Max(1f, FrameBorderWidth);

            _painter.PushClip(innerX, innerY, innerW, innerH);

            if (minValue < 0f && maxValue > 0f && Theme.Separator.A > 0)
            {
                _painter.DrawRect(
                    innerX,
                    baselineY - zeroLineThickness * 0.5f,
                    innerW,
                    zeroLineThickness,
                    Theme.Separator.WithAlpha(Math.Max(Theme.Separator.A, (byte)140)));
            }

            for (int i = 0; i < values.Length; i++)
            {
                float normalized = Math.Clamp((values[i] - minValue) / (maxValue - minValue), 0f, 1f);
                float valueY = innerY + innerH * (1f - normalized);
                float left = innerX + i * barPitch;
                float right = i == values.Length - 1
                    ? innerX + innerW
                    : MathF.Min(innerX + innerW, left + MathF.Max(1f, barPitch - barGap));
                float top = MathF.Min(baselineY, valueY);
                float bottom = MathF.Max(baselineY, valueY);
                float barW = MathF.Max(0, right - left);
                float barH = MathF.Max(0, bottom - top);

                if (barW <= 0 || barH <= 0)
                    continue;

                _painter.DrawRect(left, top, barW, barH, Theme.PlotFill);
            }

            _painter.PopClip();
        }

        if (overlayLayout is not null)
        {
            float textX = x + MathF.Max(0, (resolvedWidth - overlayLayout.Value.Width) * 0.5f);
            float textY = y + FrameBorderWidth + 3f;
            DrawTextLayout(overlayLayout.Value, textX, textY, Theme.TextPrimary);
        }

        Advance(resolvedWidth, minHeight);
        bool hover = PointIn(x, y, resolvedWidth, minHeight);
        return new Response(x, y, resolvedWidth, minHeight, hover, false, false);
    }

    /// <summary>Draws a simple activity spinner.</summary>
    public Response Spinner(
        float size = 18f,
        float thickness = 3f,
        Color? color = null,
        Color? trackColor = null,
        float speed = 1.8f)
    {
        float resolvedSize = MathF.Max(0, size);
        float resolvedThickness = Math.Clamp(thickness, 1f, MathF.Max(1f, resolvedSize * 0.5f));
        var (x, y) = Place(resolvedSize, resolvedSize);

        Color activeColor = color ?? Theme.Accent;
        Color resolvedTrackColor = trackColor ?? Theme.TextMuted.WithAlpha(96);
        bool hover = PointIn(x, y, resolvedSize, resolvedSize);

        if (resolvedSize > 0 && resolvedThickness > 0)
        {
            double timeSeconds = _input.TimeSeconds ?? _frameIndex / 60.0;
            float phase = (float)(timeSeconds * speed);
            float rotation = phase * MathF.Tau;
            float sweep = MathF.PI * (0.7f + 0.25f * (1f + MathF.Sin(phase * 1.35f)));
            float radius = MathF.Max(0, resolvedSize * 0.5f - resolvedThickness * 0.5f);
            Vector2 center = new(x + resolvedSize * 0.5f, y + resolvedSize * 0.5f);

            DrawSpinnerArc(center, radius, resolvedThickness, 0, MathF.Tau, resolvedTrackColor);
            DrawSpinnerArc(center, radius, resolvedThickness, rotation, rotation + sweep, activeColor);
        }

        Advance(resolvedSize, resolvedSize);
        return new Response(x, y, resolvedSize, resolvedSize, hover, false, false);
    }

    /// <summary>Draws a backend texture by id.</summary>
    public Response Image(
        int textureId,
        float width,
        float height,
        Color? tint = null,
        bool enabled = true)
    {
        enabled = ResolveEnabled(enabled);
        var (x, y) = Place(width, height);
        bool hover = enabled && PointIn(x, y, width, height);
        Color resolvedTint = tint ?? Color.White;
        if (!enabled)
            resolvedTint = resolvedTint.WithAlpha(140);

        if (width > 0 && height > 0 && resolvedTint.A > 0)
            _painter.AddTexturedQuad(x, y, width, height, textureId, 0, 0, 1, 1, resolvedTint);

        Advance(width, height);
        return new Response(x, y, width, height, hover, false, false, disabled: !enabled);
    }

    private void DrawChevron(float x, float y, float size, bool down, Color color)
    {
        if (size <= 0 || color.A == 0)
            return;

        float cross = size * 0.8f;
        float along = size * 0.6f;
        float crossInset = (size - cross) * 0.5f;
        float alongInset = (size - along) * 0.5f;
        if (down)
        {
            _painter.FillTriangle(
                new Vector2(x + crossInset, y + alongInset),
                new Vector2(x + crossInset + cross, y + alongInset),
                new Vector2(x + size * 0.5f, y + alongInset + along),
                color);
        }
        else
        {
            _painter.FillTriangle(
                new Vector2(x + alongInset, y + crossInset),
                new Vector2(x + alongInset, y + crossInset + cross),
                new Vector2(x + alongInset + along, y + size * 0.5f),
                color);
        }
    }

    private void DrawSpinnerArc(Vector2 center, float radius, float thickness, float startAngle, float endAngle, Color color)
    {
        if (radius <= 0 || thickness <= 0 || color.A == 0)
            return;

        float sweep = endAngle - startAngle;
        if (MathF.Abs(sweep) <= 0.001f)
            return;

        float outerRadius = radius + thickness * 0.5f;
        float innerRadius = MathF.Max(0, radius - thickness * 0.5f);
        int segments = Math.Clamp((int)MathF.Ceiling(MathF.Abs(sweep) * MathF.Max(outerRadius, 1f) / 6f), 10, 48);

        Vector2 previousOuter = center + new Vector2(MathF.Cos(startAngle), MathF.Sin(startAngle)) * outerRadius;
        Vector2 previousInner = center + new Vector2(MathF.Cos(startAngle), MathF.Sin(startAngle)) * innerRadius;

        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            float angle = startAngle + sweep * t;
            Vector2 nextOuter = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * outerRadius;
            Vector2 nextInner = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * innerRadius;

            _painter.FillTriangle(previousOuter, nextInner, nextOuter, color);
            _painter.FillTriangle(previousOuter, previousInner, nextInner, color);

            previousOuter = nextOuter;
            previousInner = nextInner;
        }
    }

    /// <summary>Draws a selectable row.</summary>
    public Response Selectable(
        string label,
        bool selected,
        float? width = null,
        float? size = null,
        bool enabled = true,
        float? frameBorderWidth = null,
        EdgeInsets? padding = null,
        string? id = null)
    {
        enabled = ResolveEnabled(enabled);
        float s = size ?? DefaultFontSize;
        var pad = padding ?? Theme.ButtonPadding;
        var layout = LayoutText(label, s);
        float markerSize = selected ? MathF.Max(10f, MathF.Ceiling(s * 0.35f)) : 0f;
        float markerGap = selected ? Theme.Gap : 0f;
        float intrinsicW = layout.Width + pad.Horizontal + markerSize + markerGap;
        float w = width.HasValue ? MathF.Max(width.Value, intrinsicW) : intrinsicW;
        float h = layout.Height + pad.Vertical;

        int widgetId = MakeId(id ?? label);
        var (x, y) = Place(w, h);

        bool focused = RegisterFocusable(widgetId, enabled);
        bool hover = enabled && PointIn(x, y, w, h);
        if (hover) _hotId = widgetId;
        if (hover) RequestCursor(UiCursor.PointingHand);

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

        var visuals = GetSelectableVisuals(enabled, hover, pressed, selected, focused);
        if (frameBorderWidth.HasValue && frameBorderWidth.Value <= 0f)
            _painter.DrawRect(x, y, w, h, visuals.Fill, default, 0f, FrameRadius);
        else
            DrawFrameRect(x, y, w, h, visuals.Fill, visuals.Border);

        float contentX = x + pad.Left;
        float textY = y + pad.Top;
        if (selected && markerSize > 0)
        {
            float markerY = y + (h - markerSize) * 0.5f;
            _painter.DrawRect(contentX, markerY, markerSize, markerSize, Theme.SelectableIndicator, radius: markerSize * 0.2f);
            contentX += markerSize + Theme.Gap;
        }

        DrawTextLayout(layout, contentX, textY, visuals.Foreground);

        Advance(w, h);
        return new Response(x, y, w, h, hover, pressed, clicked, focused: focused, changed: clicked, disabled: !enabled);
    }

    /// <summary>Draws an activatable menu item row.</summary>
    public Response MenuItem(
        string label,
        bool selected = false,
        float? width = null,
        float? size = null,
        bool enabled = true,
        bool closeOnActivate = false,
        string? shortcut = null,
        string? id = null)
    {
        enabled = ResolveEnabled(enabled);
        bool hasShortcut = !string.IsNullOrEmpty(shortcut);
        if (_menuMeasureOnly)
        {
            float s = size ?? DefaultFontSize;
            var pad = Theme.MenuItemPadding;
            var layout = LayoutText(label, s);
            float shortcutGap = hasShortcut ? MathF.Max(12f, Theme.Gap * 1.5f) : 0f;
            float shortcutWidth = hasShortcut ? LayoutText(shortcut!, s).Width : 0f;
            float markerSize = selected ? MathF.Max(10f, MathF.Ceiling(s * 0.35f)) : 0f;
            float markerGap = selected ? Theme.Gap : 0f;
            float intrinsicW = layout.Width + pad.Horizontal + markerSize + markerGap + shortcutGap + shortcutWidth;
            float resolvedWidth = width.HasValue ? MathF.Max(width.Value, intrinsicW) : intrinsicW;
            float resolvedHeight = layout.Height + pad.Vertical;
            var (measureX, measureY) = Place(resolvedWidth, resolvedHeight);
            Advance(resolvedWidth, resolvedHeight);
            return new Response(measureX, measureY, resolvedWidth, resolvedHeight, false, false, false, disabled: !enabled);
        }

        float s2 = size ?? DefaultFontSize;
        var pad2 = Theme.MenuItemPadding;
        var labelLayout = LayoutText(label, s2);
        TextLayoutResult? shortcutLayout = hasShortcut ? LayoutText(shortcut!, s2) : null;
        float shortcutGap2 = hasShortcut ? MathF.Max(12f, Theme.Gap * 1.5f) : 0f;
        float markerSize2 = selected ? MathF.Max(10f, MathF.Ceiling(s2 * 0.35f)) : 0f;
        float markerGap2 = selected ? Theme.Gap : 0f;
        float intrinsicW2 = labelLayout.Width + pad2.Horizontal + markerSize2 + markerGap2 + shortcutGap2 + (shortcutLayout?.Width ?? 0f);
        float resolvedWidth2 = width.HasValue ? MathF.Max(width.Value, intrinsicW2) : MathF.Max(AvailableWidth, intrinsicW2);
        float resolvedHeight2 = labelLayout.Height + pad2.Vertical;
        int widgetId2 = MakeId(id ?? label);
        var (x2, y2) = Place(resolvedWidth2, resolvedHeight2);

        bool focused2 = RegisterFocusable(widgetId2, enabled);
        bool hover2 = enabled && PointIn(x2, y2, resolvedWidth2, resolvedHeight2);
        if (hover2) _hotId = widgetId2;
        if (hover2) RequestCursor(UiCursor.PointingHand);

        if (enabled && _hotId == widgetId2 && IsMousePressed(UiMouseButton.Left))
        {
            _activeId = widgetId2;
            SetFocus(widgetId2);
            focused2 = true;
        }

        bool pressed2 = enabled && _activeId == widgetId2 && IsMouseDown(UiMouseButton.Left);
        bool clicked2 = enabled && IsMouseReleased(UiMouseButton.Left) && _activeId == widgetId2 && _hotId == widgetId2;
        if (enabled && focused2 && (_input.IsPressed(UiKey.Enter) || _input.IsPressed(UiKey.Space)))
            clicked2 = true;

        var visuals2 = GetSelectableVisuals(enabled, hover2, pressed2, selected, focused2);
        _painter.DrawRect(x2, y2, resolvedWidth2, resolvedHeight2, visuals2.Fill, default, 0f, FrameRadius);

        float contentX2 = x2 + pad2.Left;
        float textY2 = y2 + pad2.Top;
        if (selected && markerSize2 > 0)
        {
            float markerY2 = y2 + (resolvedHeight2 - markerSize2) * 0.5f;
            _painter.DrawRect(contentX2, markerY2, markerSize2, markerSize2, Theme.SelectableIndicator, radius: markerSize2 * 0.2f);
            contentX2 += markerSize2 + Theme.Gap;
        }

        if (hasShortcut)
        {
            float shortcutX = x2 + resolvedWidth2 - pad2.Right - (shortcutLayout?.Width ?? 0f);
            DrawTextLayout(shortcutLayout!.Value, shortcutX, textY2, Theme.TextSecondary);
        }

        DrawTextLayout(labelLayout, contentX2, textY2, visuals2.Foreground);

        Advance(resolvedWidth2, resolvedHeight2);
        var response = new Response(x2, y2, resolvedWidth2, resolvedHeight2, hover2, pressed2, clicked2, focused: focused2, changed: clicked2, disabled: !enabled);
        if (closeOnActivate && response.Activated)
            CloseAllPopups();

        return response;
    }

    /// <summary>Draws a menu item row that renders custom content when activated.</summary>
    public Response MenuItem(
        string id,
        Action<Ui> content,
        bool selected = false,
        float? width = null,
        float? size = null,
        bool enabled = true,
        bool closeOnActivate = false,
        string? shortcut = null)
        => MenuItem(
            id,
            new UiActionState(content),
            static (ui, state) => state.Content(ui),
            selected,
            width,
            size,
            enabled,
            closeOnActivate,
            shortcut);

    /// <inheritdoc cref="MenuItem(string, Action{Ui}, bool, float?, float?, bool, bool, string?)" />
    public Response MenuItem<TState>(
        string id,
        TState state,
        Action<Ui, TState> content,
        bool selected = false,
        float? width = null,
        float? size = null,
        bool enabled = true,
        bool closeOnActivate = false,
        string? shortcut = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        enabled = ResolveEnabled(enabled);
        float s = size ?? DefaultFontSize;
        var pad = Theme.MenuItemPadding;
        int widgetId = MakeId(id);
        bool hasShortcut = !string.IsNullOrEmpty(shortcut);
        TextLayoutResult? shortcutLayout = hasShortcut ? LayoutText(shortcut!, s) : null;
        float shortcutGap = hasShortcut ? MathF.Max(12f, Theme.Gap * 1.5f) : 0f;

        float rowContentHeight = LayoutText("Ag", s).Height;
        float resolvedWidth = width ?? AvailableWidth;
        float resolvedHeight = rowContentHeight + pad.Vertical;

        if (_menuMeasureOnly)
        {
            var (mx, my) = Place(resolvedWidth, resolvedHeight);
            Advance(resolvedWidth, resolvedHeight);
            return new Response(mx, my, resolvedWidth, resolvedHeight, false, false, false, disabled: !enabled);
        }

        var (x, y) = Place(resolvedWidth, resolvedHeight);

        bool focused = RegisterFocusable(widgetId, enabled);
        bool hover = enabled && PointIn(x, y, resolvedWidth, resolvedHeight);
        if (hover) _hotId = widgetId;
        if (hover) RequestCursor(UiCursor.PointingHand);

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

        var visuals = GetSelectableVisuals(enabled, hover, pressed, selected, focused);
        _painter.DrawRect(x, y, resolvedWidth, resolvedHeight, visuals.Fill, default, 0f, FrameRadius);

        float contentX = x + pad.Left;
        float contentY = y + pad.Top;
        float contentW = MathF.Max(0, resolvedWidth - pad.Horizontal - (shortcutLayout?.Width ?? 0f) - (hasShortcut ? shortcutGap : 0f));

        if (hasShortcut)
        {
            float shortcutX = x + resolvedWidth - pad.Right - (shortcutLayout?.Width ?? 0f);
            DrawTextLayout(shortcutLayout!.Value, shortcutX, contentY, Theme.TextSecondary);
        }

        PushId(id);
        _layouts.Add(new LayoutScope
        {
            OriginX = contentX,
            OriginY = contentY,
            CursorX = contentX,
            CursorY = contentY,
            Dir = LayoutDir.Horizontal,
            WidthConstraint = contentW,
            HasWidthConstraint = true,
            Empty = true
        });

        try { content(this, state); }
        finally
        {
            _layouts.RemoveAt(_layouts.Count - 1);
            PopId();
        }

        Advance(resolvedWidth, resolvedHeight);

        var response = new Response(
            x, y, resolvedWidth, resolvedHeight,
            hover, pressed, clicked,
            focused: focused, changed: clicked, disabled: !enabled);

        if (closeOnActivate && response.Activated)
            CloseAllPopups();

        return response;
    }

    /// <summary>Draws a combo box over a list of string options.</summary>
    public Response ComboBox(
        string label,
        IReadOnlyList<string> options,
        ref int selectedIndex,
        float width,
        float? size = null,
        float maxPopupHeight = 220f,
        bool enabled = true,
        string? id = null)
    {
        enabled = ResolveEnabled(enabled);
        float s = size ?? DefaultFontSize;
        var pad = Theme.ComboBoxPadding;
        string resolvedId = id ?? label;
        string popupId = resolvedId + "/popup";
        int popupWidgetId = MakeId(popupId);
        bool selectionChanged = false;
        bool appliedPendingSelection = false;
        int widgetId = MakeId(resolvedId);
        var comboState = GetState<ComboBoxState>(widgetId);

        if (comboState.HasPendingSelection)
        {
            selectedIndex = comboState.PendingSelectedIndex;
            comboState.PendingSelectedIndex = -1;
            comboState.HasPendingSelection = false;
            appliedPendingSelection = true;
            ClosePopup(popupId);
            SetFocus(widgetId);
        }

        int selected = selectedIndex;

        if (options.Count == 0)
            selected = -1;
        else
            selected = Math.Clamp(selected, 0, options.Count - 1);

        comboState.HighlightedIndex = ClampComboBoxIndex(comboState.HighlightedIndex, options.Count);

        string selectedText = options.Count > 0 && selected >= 0 && selected < options.Count
            ? options[selected]
            : string.Empty;

        TextLayoutResult labelLayout = LayoutText(selectedText, s, maxWidth: MathF.Max(0, width - pad.Horizontal - s - Theme.Gap - 2f), overflow: TextOverflowMode.Ellipsis);
        float h = labelLayout.Height + pad.Vertical;
        float itemHeight = LayoutText("Ag", s).Height + Theme.MenuItemPadding.Vertical;
        var popupState = GetState<PopupState>(popupWidgetId);
        var (x, y) = Place(width, h);

        bool popupOpen = IsPopupOpen(popupId);
        bool registeredFocus = RegisterFocusable(widgetId, enabled);
        bool focused = registeredFocus || (enabled && _focusedId == widgetId);
        bool ensureHighlightedVisible = false;
        if (!popupOpen)
            comboState.HighlightedIndex = selected;
        else if (comboState.HighlightedIndex < 0)
        {
            comboState.HighlightedIndex = selected;
            ensureHighlightedVisible = true;
        }

        bool hover = enabled && PointIn(x, y, width, h);
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
        bool openedThisFrame = false;
        if (enabled && focused && !popupOpen && (_input.IsPressed(UiKey.Enter) || _input.IsPressed(UiKey.Space)))
            clicked = true;

        if (clicked)
        {
            if (popupOpen)
            {
                ClosePopup(popupId);
                popupOpen = false;
                comboState.HighlightedIndex = selected;
            }
            else
            {
                OpenPopup(popupId);
                popupOpen = true;
                openedThisFrame = true;
                comboState.HighlightedIndex = selected;
                ensureHighlightedVisible = true;
            }
        }

        if (enabled && focused && popupOpen && !openedThisFrame)
        {
            int nextHighlighted = comboState.HighlightedIndex;

            if (_input.IsPressed(UiKey.Up))
                nextHighlighted = comboState.HighlightedIndex < 0 ? selected : comboState.HighlightedIndex - 1;
            if (_input.IsPressed(UiKey.Down))
                nextHighlighted = comboState.HighlightedIndex < 0 ? selected : comboState.HighlightedIndex + 1;
            if (_input.IsPressed(UiKey.Home))
                nextHighlighted = 0;
            if (_input.IsPressed(UiKey.End))
                nextHighlighted = options.Count - 1;

            int clampedHighlighted = ClampComboBoxIndex(nextHighlighted, options.Count);
            ensureHighlightedVisible |= clampedHighlighted != comboState.HighlightedIndex;
            comboState.HighlightedIndex = clampedHighlighted;

            if (_input.IsPressed(UiKey.Escape))
            {
                ClosePopup(popupId);
                popupOpen = false;
                comboState.HighlightedIndex = selected;
            }
            else if (_input.IsPressed(UiKey.Enter))
            {
                if (comboState.HighlightedIndex >= 0 && comboState.HighlightedIndex != selected)
                {
                    selected = comboState.HighlightedIndex;
                    selectionChanged = true;
                }

                ClosePopup(popupId);
                popupOpen = false;
                comboState.HighlightedIndex = selected;
            }
        }

        if (popupOpen && ensureHighlightedVisible)
        {
            EnsureComboBoxItemVisible(popupWidgetId, comboState.HighlightedIndex, options.Count, itemHeight, maxPopupHeight);
        }

        if (selectionChanged)
        {
            selectedText = options.Count > 0 && selected >= 0 && selected < options.Count
                ? options[selected]
                : string.Empty;
            labelLayout = LayoutText(selectedText, s, maxWidth: MathF.Max(0, width - pad.Horizontal - s - Theme.Gap - 2f), overflow: TextOverflowMode.Ellipsis);
        }

        var visuals = GetComboBoxVisuals(enabled, hover, pressed, popupOpen, focused);
        DrawFrameRect(x, y, width, h, visuals.Fill, visuals.Border);

        float arrowSize = MathF.Max(8f, s * 0.55f);
        float textX = x + pad.Left;
        float textY = y + pad.Top;
        if (!string.IsNullOrEmpty(selectedText))
            DrawTextLayout(labelLayout, textX, textY, visuals.Foreground);

        DrawChevron(
            x + width - pad.Right - arrowSize,
            y + (h - arrowSize) * 0.5f,
            arrowSize,
            true,
            visuals.Foreground);

        Advance(width, h);

        if (popupOpen)
        {
            Popup(popupId, x, y + h, width, maxPopupHeight, popup =>
            {
                popup.ItemSpacing(0);
                for (int i = 0; i < options.Count; i++)
                {
                    if (popup.Selectable(
                            options[i],
                            i == comboState.HighlightedIndex,
                            width: popup.AvailableWidth,
                            frameBorderWidth: 0f,
                            padding: popup.Theme.MenuItemPadding).Clicked)
                    {
                        comboState.HighlightedIndex = i;
                        comboState.PendingSelectedIndex = i;
                        comboState.HasPendingSelection = true;
                        ClosePopup(popupId);
                    }
                }
            });
        }

        selectedIndex = selected;

        return new Response(
            x,
            y,
            width,
            h,
            hover,
            pressed,
            clicked,
            focused: focused,
            changed: clicked || selectionChanged || appliedPendingSelection,
            disabled: !enabled);
    }

    /// <summary>Draws a floating-point slider.</summary>
    public Response Slider(
        string labelOrId,
        ref float value,
        float min,
        float max,
        float width,
        float? step = null,
        bool enabled = true,
        string? format = null,
        string? label = null,
        string? id = null)
    {
        enabled = ResolveEnabled(enabled);
        if (max < min)
            (min, max) = (max, min);

        value = SnapSliderValue(value, min, max, step);

        int widgetId = MakeId(id ?? labelOrId);
        string display = BuildSliderDisplay(label, FormatSliderValue(value, format));
        float textMaxWidth = MathF.Max(0, width - FrameBorderWidth * 2 - 8f);
        var layout = LayoutText(display, DefaultFontSize, maxWidth: textMaxWidth, overflow: TextOverflowMode.Ellipsis);
        float h = MathF.Max(Theme.SliderHeight, layout.Height + FrameBorderWidth * 2 + 6f);
        var (x, y) = Place(width, h);

        bool focused = RegisterFocusable(widgetId, enabled);
        bool hover = enabled && PointIn(x, y, width, h);
        if (hover) _hotId = widgetId;

        if (enabled && hover && IsMousePressed(UiMouseButton.Left))
        {
            _activeId = widgetId;
            SetFocus(widgetId);
            focused = true;
        }

        bool pressed = enabled && _activeId == widgetId && IsMouseDown(UiMouseButton.Left);
        if (enabled && (hover || pressed))
            RequestCursor(UiCursor.PointingHand);

        bool changed = false;
        if (enabled && pressed)
        {
            float next = SliderValueFromMouse(_mouse.X, x, width,
                ResolveSliderBlockWidth(MathF.Max(0, width - FrameBorderWidth * 2)), min, max, step);
            if (MathF.Abs(next - value) > 0.0001f)
            {
                value = next;
                changed = true;
            }
        }

        if (enabled && focused)
        {
            float keyboardStep = ResolveSliderKeyboardStep(min, max, step);
            float next = value;

            if (_input.IsPressed(UiKey.Home)) next = min;
            if (_input.IsPressed(UiKey.End)) next = max;
            if (_input.IsPressed(UiKey.Left) || _input.IsPressed(UiKey.Down)) next -= keyboardStep;
            if (_input.IsPressed(UiKey.Right) || _input.IsPressed(UiKey.Up)) next += keyboardStep;

            next = SnapSliderValue(next, min, max, step);
            if (MathF.Abs(next - value) > 0.0001f)
            {
                value = next;
                changed = true;
            }
        }

        bool clicked = enabled && IsMouseReleased(UiMouseButton.Left) && _activeId == widgetId && hover;

        float normalized = NormalizeSliderValue(value, min, max);
        Color trackBg = !enabled ? Theme.SliderBg.WithAlpha(140)
            : pressed ? Theme.SliderBgActive
            : hover ? Theme.SliderBgHover
            : Theme.SliderBg;
        Color fillColor = !enabled ? Theme.SliderFill.WithAlpha(140)
            : pressed ? Theme.SliderFillActive
            : Theme.SliderFill;
        Color borderColor = !enabled ? Theme.SliderBorder.WithAlpha(140)
            : focused ? Theme.FocusBorder
            : Theme.SliderBorder;

        DrawFrameRect(x, y, width, h, trackBg, borderColor);

        float innerX = x + FrameBorderWidth;
        float innerY = y + FrameBorderWidth;
        float innerW = MathF.Max(0, width - FrameBorderWidth * 2);
        float innerH = MathF.Max(0, h - FrameBorderWidth * 2);
        float blockW = ResolveSliderBlockWidth(innerW);
        float blockTravel = MathF.Max(0, innerW - blockW);
        float blockX = innerX + normalized * blockTravel;
        if (blockW > 0 && innerH > 0)
        {
            float fillRadius = MathF.Min(MathF.Max(0, FrameRadius - FrameBorderWidth), innerH * 0.5f);
            _painter.DrawRect(blockX, innerY, blockW, innerH, fillColor, radius: fillRadius);
        }

        if (changed)
        {
            display = BuildSliderDisplay(label, FormatSliderValue(value, format));
            layout = LayoutText(display, DefaultFontSize, maxWidth: textMaxWidth, overflow: TextOverflowMode.Ellipsis);
        }

        Color textColor = enabled ? Theme.TextPrimary : Theme.TextPrimary.WithAlpha(140);
        float textX = x + MathF.Max(0, (width - layout.Width) * 0.5f);
        float textY = y + MathF.Max(0, (h - layout.Height) * 0.5f);
        DrawTextLayout(layout, textX, textY, textColor);

        Advance(width, h);
        return new Response(
            x,
            y,
            width,
            h,
            hover,
            pressed,
            clicked,
            focused: focused,
            changed: changed,
            disabled: !enabled);
    }

    /// <summary>Draws an integer slider.</summary>
    public Response SliderInt(
        string labelOrId,
        ref int value,
        int min,
        int max,
        float width,
        int step = 1,
        bool enabled = true,
        string? format = null,
        string? label = null,
        string? id = null)
    {
        float current = value;
        Response response = Slider(labelOrId, ref current, min, max, width, step, enabled, format ?? "{0:0}", label, id);
        value = (int)MathF.Round(current);
        return response;
    }

    /// <summary>Draws a draggable floating-point value editor.</summary>
    public Response DragFloat(
        string labelOrId,
        ref float value,
        float speed = 1f,
        float? min = null,
        float? max = null,
        string? format = null,
        float? width = null,
        float? size = null,
        bool enabled = true,
        string? id = null)
    {
        enabled = ResolveEnabled(enabled);
        float minV = min ?? float.NegativeInfinity;
        float maxV = max ?? float.PositiveInfinity;
        if (maxV < minV) (minV, maxV) = (maxV, minV);
        value = MathF.Max(minV, MathF.Min(maxV, value));

        float s = size ?? DefaultFontSize;
        var pad = Theme.ButtonPadding;
        int widgetId = MakeId(id ?? labelOrId);
        string fmt = format ?? "{0:0.00}";

        string display = string.Format(System.Globalization.CultureInfo.InvariantCulture, fmt, value);
        var layout = LayoutText(display, s);
        float intrinsicW = layout.Width + pad.Horizontal;
        float w = width ?? MathF.Max(96f, intrinsicW);
        float h = layout.Height + pad.Vertical;

        var (x, y) = Place(w, h);

        bool focused = RegisterFocusable(widgetId, enabled);
        bool hover = enabled && PointIn(x, y, w, h);
        if (hover) _hotId = widgetId;

        if (enabled && hover && IsMousePressed(UiMouseButton.Left))
        {
            _activeId = widgetId;
            SetFocus(widgetId);
            focused = true;
        }

        bool pressed = enabled && _activeId == widgetId && IsMouseDown(UiMouseButton.Left);
        if (enabled && (hover || pressed))
            RequestCursor(UiCursor.ResizeEW);

        bool changed = false;
        if (enabled && pressed && _mouseDelta.X != 0)
        {
            float next = value + _mouseDelta.X * speed;
            next = MathF.Max(minV, MathF.Min(maxV, next));
            if (MathF.Abs(next - value) > 0.0001f)
            {
                value = next;
                changed = true;
            }
        }

        if (enabled && focused && !pressed)
        {
            float next = value;
            if (_input.IsPressed(UiKey.Left)) next -= speed;
            if (_input.IsPressed(UiKey.Right)) next += speed;
            if (min.HasValue && _input.IsPressed(UiKey.Home)) next = minV;
            if (max.HasValue && _input.IsPressed(UiKey.End)) next = maxV;
            next = MathF.Max(minV, MathF.Min(maxV, next));
            if (MathF.Abs(next - value) > 0.0001f)
            {
                value = next;
                changed = true;
            }
        }

        bool clicked = enabled && IsMouseReleased(UiMouseButton.Left) && _activeId == widgetId && hover;

        var visuals = GetButtonVisuals(enabled, hover, pressed, focused);
        DrawFrameRect(x, y, w, h, visuals.Fill, visuals.Border);

        if (changed)
        {
            display = string.Format(System.Globalization.CultureInfo.InvariantCulture, fmt, value);
            layout = LayoutText(display, s);
        }

        float textX = x + MathF.Max(pad.Left, (w - layout.Width) * 0.5f);
        DrawTextLayout(layout, textX, y + pad.Top, visuals.Foreground);

        Advance(w, h);
        return new Response(
            x, y, w, h,
            hover, pressed, clicked,
            focused: focused,
            changed: changed,
            disabled: !enabled);
    }

    private sealed class DragIntState
    {
        public float FloatValue;
    }

    /// <summary>Draws a draggable integer value editor.</summary>
    public Response DragInt(
        string labelOrId,
        ref int value,
        float speed = 1f,
        int? min = null,
        int? max = null,
        string? format = null,
        float? width = null,
        float? size = null,
        bool enabled = true,
        string? id = null)
    {
        int widgetId = MakeId(id ?? labelOrId);
        var dragState = GetState<DragIntState>(widgetId);

        bool draggingActive = _activeId == widgetId && IsMouseDown(UiMouseButton.Left);
        if (!draggingActive)
            dragState.FloatValue = value;

        int startValue = value;
        Response response = DragFloat(
            labelOrId,
            ref dragState.FloatValue,
            speed,
            min.HasValue ? min.Value : null,
            max.HasValue ? max.Value : null,
            format ?? "{0:0}",
            width,
            size,
            enabled,
            id);

        int rounded = (int)MathF.Round(dragState.FloatValue);
        if (min.HasValue) rounded = Math.Max(min.Value, rounded);
        if (max.HasValue) rounded = Math.Min(max.Value, rounded);
        value = rounded;

        bool changed = rounded != startValue;
        return new Response(
            response.X, response.Y, response.W, response.H,
            response.Hovered, response.Pressed, response.Clicked,
            focused: response.Focused,
            changed: changed,
            disabled: response.Disabled);
    }

    /// <summary>Draws a header that toggles an open/collapsed state.</summary>
    public Response CollapsingHeader(string label, ref bool open, float? width = null, float? size = null, bool enabled = true, string? id = null)
    {
        enabled = ResolveEnabled(enabled);
        float s = size ?? DefaultFontSize;
        var pad = Theme.CollapsingHeaderPadding;
        var labelLayout = LayoutText(label, s);
        float arrowSize = MathF.Max(8f, s * 0.6f);
        float arrowGap = MathF.Max(0, Theme.CollapsingHeaderArrowGap);
        float intrinsicW = labelLayout.Width + pad.Horizontal + arrowSize + arrowGap;
        float w = width.HasValue ? MathF.Max(width.Value, intrinsicW) : intrinsicW;
        float h = MathF.Max(labelLayout.Height, arrowSize) + pad.Vertical;

        int widgetId = MakeId(id ?? label);
        var (x, y) = Place(w, h);

        bool focused = RegisterFocusable(widgetId, enabled);
        bool hover = enabled && PointIn(x, y, w, h);
        if (hover) _hotId = widgetId;
        if (hover) RequestCursor(UiCursor.PointingHand);

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

        bool toggled = false;
        bool opened = false;
        bool closed = false;
        bool currentOpen = open;
        if (clicked)
        {
            currentOpen = !open;
            open = currentOpen;
            toggled = true;
            opened = currentOpen;
            closed = !currentOpen;
        }

        var visuals = GetCollapsingHeaderVisuals(enabled, hover, pressed, currentOpen, focused);
        float backgroundRadius = MathF.Min(FrameRadius, h * 0.35f);
        if (visuals.Fill.A > 0)
            _painter.DrawRect(x, y, w, h, visuals.Fill, radius: backgroundRadius);

        float chevronX = x + pad.Left;
        DrawChevron(
            chevronX,
            y + (h - arrowSize) * 0.5f,
            arrowSize,
            currentOpen,
            visuals.Foreground);
        DrawTextLayout(labelLayout, chevronX + arrowSize + arrowGap, y + pad.Top, visuals.Foreground);

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
            changed: toggled,
            disabled: !enabled,
            toggled: toggled,
            opened: opened,
            closed: closed);
    }
}
