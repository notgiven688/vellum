using Vellum.Rendering;

namespace Vellum;

public sealed partial class Ui
{
    private sealed class TabBarState
    {
        public int SelectedIndex;
    }

    private sealed class TabBarContext
    {
        public required TabBarState State;
        public int CurrentIndex;
        public bool HasActiveTab;
        public float ActiveTabLeft;
        public float ActiveTabRight;
        public float HeaderBottom;
        public Action<Ui>? SelectedContent;
    }

    private readonly Stack<TabBarContext> _tabBarContexts = new();

    /// <summary>Starts a tab bar and renders the selected tab content.</summary>
    public Response TabBar(string id, Action<Ui> content)
        => TabBar(id, new UiActionState(content), static (ui, state) => state.Content(ui));

    /// <inheritdoc cref="TabBar(string, Action{Ui})" />
    public Response TabBar<TState>(string id, TState state, Action<Ui, TState> content)
    {
        ArgumentNullException.ThrowIfNull(content);

        int widgetId = MakeId(id);
        RegisterWidgetId(widgetId, $"TabBar \"{id}\"");
        var tabState = GetState<TabBarState>(widgetId);
        float availableWidth = AvailableWidth;

        var ctx = new TabBarContext { State = tabState };
        EnterIdScope(id);
        _tabBarContexts.Push(ctx);

        var (rowX, rowY) = Place(0, 0);

        try
        {
            using (Row())
                content(this, state);
        }
        finally
        {
            _tabBarContexts.Pop();
            ExitIdScope();
        }

        if (ctx.HeaderBottom > rowY)
        {
            float baselineY = ctx.HeaderBottom - 1f;
            _painter.DrawRect(rowX, baselineY, availableWidth, 1f, Theme.Separator);

            if (ctx.HasActiveTab)
            {
                float thickness = MathF.Max(1f, Theme.TabIndicatorThickness);
                _painter.DrawRect(
                    ctx.ActiveTabLeft,
                    ctx.HeaderBottom - thickness,
                    ctx.ActiveTabRight - ctx.ActiveTabLeft,
                    thickness,
                    Theme.Accent);
            }
        }

        if (ctx.SelectedContent != null)
            ctx.SelectedContent(this);

        return new Response(
            rowX,
            rowY,
            availableWidth,
            ctx.HeaderBottom > rowY ? ctx.HeaderBottom - rowY : 0,
            false,
            false,
            false);
    }

    /// <summary>Declares a tab inside the current tab bar.</summary>
    public Response Tab(string label, Action<Ui> content, string? id = null)
        => Tab(label, new UiActionState(content), static (ui, state) => state.Content(ui), id);

    /// <inheritdoc cref="Tab(string, Action{Ui}, string?)" />
    public Response Tab<TState>(string label, TState state, Action<Ui, TState> content, string? id = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (_tabBarContexts.Count == 0)
            throw new InvalidOperationException("Tab() can only be called inside TabBar().");

        var ctx = _tabBarContexts.Peek();
        int index = ctx.CurrentIndex++;

        float s = DefaultFontSize;
        var pad = Theme.TabPadding;
        var layout = LayoutText(label, s);
        float w = layout.Width + pad.Horizontal;
        float h = layout.Height + pad.Vertical;

        if (index > 0 && Theme.TabSpacing > 0)
            Spacing(Theme.TabSpacing);

        int tabId = MakeId(id ?? label);
        var (x, y) = Place(w, h);

        bool focused = RegisterFocusable(tabId, true);
        bool hover = PointIn(x, y, w, h);
        if (hover) _hotId = tabId;
        if (hover) RequestCursor(UiCursor.PointingHand);

        if (hover && IsMousePressed(UiMouseButton.Left))
        {
            _activeId = tabId;
            SetFocus(tabId);
            focused = true;
        }

        bool pressed = _activeId == tabId && IsMouseDown(UiMouseButton.Left);
        bool clicked = IsMouseReleased(UiMouseButton.Left) && _activeId == tabId && _hotId == tabId;
        if (focused && (_input.IsPressed(UiKey.Enter) || _input.IsPressed(UiKey.Space)))
            clicked = true;

        bool selected = ctx.State.SelectedIndex == index;
        bool changed = false;
        if (clicked && !selected)
        {
            ctx.State.SelectedIndex = index;
            selected = true;
            changed = true;
        }

        Color bgColor = selected ? default
            : pressed && hover ? Theme.ButtonBgPressed.WithAlpha(120)
            : hover ? Theme.ButtonBgHover.WithAlpha(120)
            : default;
        if (bgColor.A > 0)
        {
            float radius = MathF.Min(FrameRadius, MathF.Min(w, h) * 0.4f);
            _painter.DrawRect(x, y, w, h, bgColor, default, 0f, radius);
        }

        Color textColor = !selected
            ? (hover ? Theme.TextPrimary : Theme.TextSecondary)
            : Theme.TextPrimary;
        DrawTextLayout(layout, x + (w - layout.Width) * 0.5f, y + pad.Top, textColor);

        Advance(w, h);

        ctx.HeaderBottom = MathF.Max(ctx.HeaderBottom, y + h);
        if (selected)
        {
            ctx.HasActiveTab = true;
            ctx.ActiveTabLeft = x;
            ctx.ActiveTabRight = x + w;
            ctx.SelectedContent = ui => content(ui, state);
        }

        return new Response(x, y, w, h, hover, pressed, clicked, focused: focused, changed: changed);
    }
}
