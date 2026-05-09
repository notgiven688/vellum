namespace Vellum;

public sealed partial class Ui
{
    /// <summary>Shows a tooltip while the anchor response is hovered.</summary>
    public bool Tooltip(Response anchor, string text, float maxWidth = 320f, float? size = null)
    {
        if (!anchor.Hovered)
            return false;

        return Tooltip(_mouse.X, _mouse.Y, text, maxWidth, size);
    }

    /// <summary>Shows a tooltip at the current mouse position.</summary>
    public bool Tooltip(string text, float maxWidth = 320f, float? size = null)
        => Tooltip(_mouse.X, _mouse.Y, text, maxWidth, size);

    /// <summary>Shows a tooltip at an explicit anchor position.</summary>
    public bool Tooltip(float anchorX, float anchorY, string text, float maxWidth = 320f, float? size = null)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        _tooltipText = text;
        _tooltipAnchorX = anchorX;
        _tooltipAnchorY = anchorY;
        _tooltipMaxWidth = maxWidth;
        _tooltipFontSize = size ?? DefaultFontSize;
        return true;
    }

    private void PrepareTooltipFrame()
    {
        _tooltipText = null;
        _tooltipAnchorX = 0;
        _tooltipAnchorY = 0;
        _tooltipMaxWidth = 0;
        _tooltipFontSize = 0;
    }

    private void RenderQueuedTooltip()
    {
        if (string.IsNullOrWhiteSpace(_tooltipText))
            return;

        float border = FrameBorderWidth;
        var pad = Theme.TooltipPadding;
        float resolvedSize = _tooltipFontSize > 0 ? _tooltipFontSize : DefaultFontSize;

        float maxOuterWidth = _tooltipMaxWidth > 0
            ? MathF.Min(_tooltipMaxWidth, _vpW)
            : _vpW;
        float minOuterWidth = border * 2 + pad.Horizontal;
        if (maxOuterWidth < minOuterWidth)
            maxOuterWidth = minOuterWidth;

        float innerMaxWidth = MathF.Max(0, maxOuterWidth - border * 2 - pad.Horizontal);
        var layout = LayoutText(_tooltipText, resolvedSize, innerMaxWidth, wrap: TextWrapMode.WordWrap);

        float outerW = Math.Clamp(layout.Width + pad.Horizontal + border * 2, minOuterWidth, maxOuterWidth);
        float outerH = MathF.Min(_vpH, layout.Height + pad.Vertical + border * 2);

        float x = _tooltipAnchorX + Theme.TooltipOffsetX;
        float y = _tooltipAnchorY + Theme.TooltipOffsetY;
        if (x + outerW > _vpW)
            x = _tooltipAnchorX - outerW - Theme.TooltipOffsetX;
        if (y + outerH > _vpH)
            y = _tooltipAnchorY - outerH - Theme.TooltipOffsetY;

        x = Math.Clamp(x, 0, MathF.Max(0, _vpW - outerW));
        y = Math.Clamp(y, 0, MathF.Max(0, _vpH - outerH));

        DrawFrameRect(x, y, outerW, outerH, Theme.TooltipBg, Theme.TooltipBorder);

        float contentX = x + border + pad.Left;
        float contentY = y + border + pad.Top;
        float contentW = MathF.Max(0, outerW - border * 2 - pad.Horizontal);
        float contentH = MathF.Max(0, outerH - border * 2 - pad.Vertical);
        _painter.PushClip(contentX, contentY, contentW, contentH);
        DrawTextLayout(layout, contentX, contentY, Theme.TooltipText);
        _painter.PopClip();
    }
}
