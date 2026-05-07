using System.Numerics;
using Vellum.Rendering;

namespace Vellum;

/// <summary>
/// Retained state for dockable Vellum windows.
/// </summary>
public sealed class DockingState
{
    internal readonly Dictionary<int, DockSpaceRuntime> Spaces = new();
    internal readonly Dictionary<int, int> WindowSpaces = new();
    internal readonly Dictionary<int, DockNode> WindowNodes = new();
    internal readonly Dictionary<int, DockRect> WindowRects = new();
    internal readonly List<int> SpaceOrder = new();

    internal int DraggingWindowId;
    internal int Version { get; private set; }
    private int _nextNodeId = 1;

    /// <summary>Removes all docked window assignments and dock-space runtime data.</summary>
    public void Reset()
    {
        Spaces.Clear();
        WindowSpaces.Clear();
        WindowNodes.Clear();
        WindowRects.Clear();
        SpaceOrder.Clear();
        DraggingWindowId = 0;
        _nextNodeId = 1;
        Version++;
    }

    internal DockSpaceRuntime GetSpace(int spaceId, int frameIndex)
    {
        if (!Spaces.TryGetValue(spaceId, out var space))
        {
            space = new DockSpaceRuntime(spaceId, CreateLeaf());
            Spaces[spaceId] = space;
            Version++;
        }

        space.LastSeenFrame = frameIndex;
        if (!SpaceOrder.Contains(spaceId))
        {
            SpaceOrder.Add(spaceId);
            Version++;
        }

        return space;
    }

    internal bool IsWindowDocked(int windowId)
        => WindowSpaces.TryGetValue(windowId, out int spaceId) &&
           Spaces.TryGetValue(spaceId, out var space) &&
           WindowNodes.TryGetValue(windowId, out var node) &&
           node.IsLeaf &&
           NodeBelongsToSpace(space.Root, node);

    internal bool TryGetWindowRect(int windowId, out DockRect rect)
        => WindowRects.TryGetValue(windowId, out rect);

    internal void DockWindow(int windowId, DockDropTarget target)
    {
        if (!Spaces.TryGetValue(target.SpaceId, out var space))
            return;

        RemoveWindow(windowId);

        var leaf = target.Leaf ?? FindFirstLeaf(space.Root) ?? space.Root;
        if (!leaf.IsLeaf)
            leaf = FindFirstLeaf(leaf) ?? space.Root;

        if (target.Side == DockDropSide.Center)
        {
            AddWindowToLeaf(windowId, target.SpaceId, leaf);
            Version++;
            return;
        }

        SplitLeaf(windowId, target.SpaceId, leaf, target.Side);
        Version++;
    }

    internal void RemoveWindow(int windowId)
    {
        bool changed = false;
        if (WindowSpaces.TryGetValue(windowId, out int oldSpaceId) &&
            Spaces.TryGetValue(oldSpaceId, out var oldSpace) &&
            WindowNodes.TryGetValue(windowId, out var oldLeaf))
        {
            int removedIndex = oldLeaf.WindowIds.IndexOf(windowId);
            if (removedIndex >= 0)
            {
                oldLeaf.WindowIds.RemoveAt(removedIndex);
                changed = true;
            }

            if (oldLeaf.ActiveWindowId == windowId)
                oldLeaf.ActiveWindowId = oldLeaf.WindowIds.Count == 0
                    ? 0
                    : oldLeaf.WindowIds[Math.Clamp(removedIndex, 0, oldLeaf.WindowIds.Count - 1)];

            if (oldLeaf.WindowIds.Count == 0 && !oldLeaf.PreserveEmpty)
                CollapseEmptyLeaf(oldSpace, oldLeaf);
        }

        changed |= WindowSpaces.Remove(windowId);
        changed |= WindowNodes.Remove(windowId);
        changed |= WindowRects.Remove(windowId);
        if (changed)
            Version++;
    }

    internal bool TryFindDropTarget(Vector2 point, int frameIndex, out DockDropTarget target)
    {
        for (int i = SpaceOrder.Count - 1; i >= 0; i--)
        {
            int candidate = SpaceOrder[i];
            if (Spaces.TryGetValue(candidate, out var space) &&
                space.LastSeenFrame == frameIndex &&
                space.Rect.Contains(point))
            {
                var leaf = FindLeafAt(space.Root, point);
                DockDropSide side = leaf == null
                    ? DockDropSide.Center
                    : GetDropSide(leaf.Rect, point);
                DockRect previewRect = leaf == null
                    ? space.Rect
                    : GetPreviewRect(leaf.Rect, side);

                target = new DockDropTarget(candidate, leaf ?? space.Root, side, previewRect);
                return true;
            }
        }

        target = default;
        return false;
    }

    internal void PruneMissingWindows(IReadOnlySet<int> visibleWindowIds, int frameIndex)
    {
        List<int>? missingWindows = null;
        foreach (var pair in WindowSpaces)
        {
            if (!visibleWindowIds.Contains(pair.Key))
            {
                missingWindows ??= new List<int>();
                missingWindows.Add(pair.Key);
            }
        }

        if (missingWindows != null)
        {
            foreach (int windowId in missingWindows)
                RemoveWindow(windowId);
        }

        for (int i = SpaceOrder.Count - 1; i >= 0; i--)
        {
            int spaceId = SpaceOrder[i];
            if (!Spaces.TryGetValue(spaceId, out var space) || space.LastSeenFrame != frameIndex)
            {
                if (space != null)
                {
                    RemoveSpaceMappings(space.Root);

                    Spaces.Remove(spaceId);
                    Version++;
                }

                SpaceOrder.RemoveAt(i);
                Version++;
                continue;
            }

            PruneMissingFromNode(space, space.Root, visibleWindowIds);
        }

        WindowRects.Clear();
    }

    internal DockNode CreateLeaf() => new(_nextNodeId++);

    private void AddWindowToLeaf(int windowId, int spaceId, DockNode leaf)
    {
        if (!leaf.WindowIds.Contains(windowId))
            leaf.WindowIds.Add(windowId);

        leaf.PreserveEmpty = false;
        leaf.ActiveWindowId = windowId;
        WindowSpaces[windowId] = spaceId;
        WindowNodes[windowId] = leaf;
    }

    private void SplitLeaf(int windowId, int spaceId, DockNode target, DockDropSide side)
    {
        var existingLeaf = CreateLeaf();
        existingLeaf.WindowIds.AddRange(target.WindowIds);
        existingLeaf.ActiveWindowId = target.ActiveWindowId;
        existingLeaf.PreserveEmpty = existingLeaf.WindowIds.Count == 0;

        var newLeaf = CreateLeaf();
        newLeaf.WindowIds.Add(windowId);
        newLeaf.ActiveWindowId = windowId;

        target.WindowIds.Clear();
        target.ActiveWindowId = 0;
        target.Orientation = side is DockDropSide.Left or DockDropSide.Right
            ? DockSplitOrientation.Horizontal
            : DockSplitOrientation.Vertical;
        target.Fraction = 0.5f;

        if (side is DockDropSide.Left or DockDropSide.Top)
        {
            target.First = newLeaf;
            target.Second = existingLeaf;
        }
        else
        {
            target.First = existingLeaf;
            target.Second = newLeaf;
        }

        target.First.Parent = target;
        target.Second.Parent = target;

        foreach (int existingWindowId in existingLeaf.WindowIds)
            WindowNodes[existingWindowId] = existingLeaf;

        WindowSpaces[windowId] = spaceId;
        WindowNodes[windowId] = newLeaf;
    }

    private void CollapseEmptyLeaf(DockSpaceRuntime space, DockNode emptyLeaf)
    {
        var parent = emptyLeaf.Parent;
        if (parent == null)
            return;

        var sibling = parent.First == emptyLeaf ? parent.Second : parent.First;
        if (sibling == null)
            return;

        if (parent.Parent == null)
        {
            CopyNode(parent, sibling);
            parent.Parent = null;
            RefreshMappings(parent);
            return;
        }

        var grandparent = parent.Parent;
        if (grandparent.First == parent)
            grandparent.First = sibling;
        else if (grandparent.Second == parent)
            grandparent.Second = sibling;

        sibling.Parent = grandparent;
        RefreshMappings(space.Root);
    }

    private void CopyNode(DockNode target, DockNode source)
    {
        target.WindowIds.Clear();
        target.WindowIds.AddRange(source.WindowIds);
        target.ActiveWindowId = source.ActiveWindowId;
        target.Orientation = source.Orientation;
        target.Fraction = source.Fraction;
        target.Rect = source.Rect;
        target.First = source.First;
        target.Second = source.Second;
        target.PreserveEmpty = source.PreserveEmpty;

        if (target.First != null)
            target.First.Parent = target;
        if (target.Second != null)
            target.Second.Parent = target;
    }

    private void RefreshMappings(DockNode node)
    {
        if (node.IsLeaf)
        {
            foreach (int windowId in node.WindowIds)
                WindowNodes[windowId] = node;
            return;
        }

        if (node.First != null) RefreshMappings(node.First);
        if (node.Second != null) RefreshMappings(node.Second);
    }

    private void RemoveSpaceMappings(DockNode node)
    {
        if (node.IsLeaf)
        {
            foreach (int windowId in node.WindowIds)
            {
                WindowSpaces.Remove(windowId);
                WindowNodes.Remove(windowId);
                WindowRects.Remove(windowId);
            }

            node.WindowIds.Clear();
            node.ActiveWindowId = 0;
            return;
        }

        if (node.First != null) RemoveSpaceMappings(node.First);
        if (node.Second != null) RemoveSpaceMappings(node.Second);
    }

    private void PruneMissingFromNode(DockSpaceRuntime space, DockNode node, IReadOnlySet<int> visibleWindowIds)
    {
        if (node.IsLeaf)
        {
            for (int i = node.WindowIds.Count - 1; i >= 0; i--)
            {
                int windowId = node.WindowIds[i];
                if (visibleWindowIds.Contains(windowId))
                    continue;

                node.WindowIds.RemoveAt(i);
                WindowSpaces.Remove(windowId);
                WindowNodes.Remove(windowId);
                WindowRects.Remove(windowId);
                Version++;
            }

            if (node.WindowIds.Count == 0)
            {
                node.ActiveWindowId = 0;
                if (node.PreserveEmpty)
                    return;

                CollapseEmptyLeaf(space, node);
                return;
            }

            if (!node.WindowIds.Contains(node.ActiveWindowId))
                node.ActiveWindowId = node.WindowIds[0];
            return;
        }

        if (node.First != null) PruneMissingFromNode(space, node.First, visibleWindowIds);
        if (node.Second != null) PruneMissingFromNode(space, node.Second, visibleWindowIds);
    }

    private static bool NodeBelongsToSpace(DockNode root, DockNode node)
    {
        for (DockNode? current = node; current != null; current = current.Parent)
        {
            if (current == root)
                return true;
        }

        return false;
    }

    private static DockNode? FindFirstLeaf(DockNode node)
    {
        if (node.IsLeaf)
            return node;

        return node.First != null ? FindFirstLeaf(node.First) : node.Second != null ? FindFirstLeaf(node.Second) : null;
    }

    private static DockNode? FindLeafAt(DockNode node, Vector2 point)
    {
        if (!node.Rect.Contains(point))
            return null;

        if (node.IsLeaf)
            return node;

        if (node.First != null)
        {
            var first = FindLeafAt(node.First, point);
            if (first != null)
                return first;
        }

        return node.Second != null ? FindLeafAt(node.Second, point) : null;
    }

    private static DockDropSide GetDropSide(DockRect rect, Vector2 point)
    {
        float edge = MathF.Min(72f, MathF.Min(rect.W, rect.H) * 0.28f);
        if (edge <= 0)
            return DockDropSide.Center;

        float left = point.X - rect.X;
        float right = rect.X + rect.W - point.X;
        float top = point.Y - rect.Y;
        float bottom = rect.Y + rect.H - point.Y;
        float best = edge;
        DockDropSide side = DockDropSide.Center;

        if (left < best) { best = left; side = DockDropSide.Left; }
        if (right < best) { best = right; side = DockDropSide.Right; }
        if (top < best) { best = top; side = DockDropSide.Top; }
        if (bottom < best) side = DockDropSide.Bottom;

        return side;
    }

    private static DockRect GetPreviewRect(DockRect rect, DockDropSide side)
    {
        const float SplitPreviewFraction = 0.5f;
        return side switch
        {
            DockDropSide.Left => new DockRect(rect.X, rect.Y, rect.W * SplitPreviewFraction, rect.H),
            DockDropSide.Right => new DockRect(rect.X + rect.W * (1f - SplitPreviewFraction), rect.Y, rect.W * SplitPreviewFraction, rect.H),
            DockDropSide.Top => new DockRect(rect.X, rect.Y, rect.W, rect.H * SplitPreviewFraction),
            DockDropSide.Bottom => new DockRect(rect.X, rect.Y + rect.H * (1f - SplitPreviewFraction), rect.W, rect.H * SplitPreviewFraction),
            _ => rect
        };
    }
}

internal sealed class DockSpaceRuntime
{
    public readonly int Id;
    public readonly DockNode Root;
    public DockRect Rect;
    public int LastSeenFrame;

    public DockSpaceRuntime(int id, DockNode root)
    {
        Id = id;
        Root = root;
    }
}

internal sealed class DockNode
{
    public readonly int Id;
    public readonly List<int> WindowIds = new();
    public DockNode? Parent;
    public DockNode? First;
    public DockNode? Second;
    public DockSplitOrientation Orientation;
    public float Fraction = 0.5f;
    public DockRect Rect;
    public DockRect SplitterRect;
    public int ActiveWindowId;
    public bool PreserveEmpty;

    public DockNode(int id) => Id = id;

    public bool IsLeaf => First == null && Second == null;
}

internal readonly struct DockDropTarget
{
    public readonly int SpaceId;
    public readonly DockNode? Leaf;
    public readonly DockDropSide Side;
    public readonly DockRect PreviewRect;

    public DockDropTarget(int spaceId, DockNode? leaf, DockDropSide side, DockRect previewRect)
    {
        SpaceId = spaceId;
        Leaf = leaf;
        Side = side;
        PreviewRect = previewRect;
    }
}

internal enum DockDropSide { Center, Left, Right, Top, Bottom }

internal enum DockSplitOrientation { Horizontal, Vertical }

internal readonly struct DockRect
{
    public readonly float X;
    public readonly float Y;
    public readonly float W;
    public readonly float H;

    public DockRect(float x, float y, float w, float h)
    {
        X = x;
        Y = y;
        W = w;
        H = h;
    }

    public bool Contains(Vector2 point)
        => W > 0 &&
           H > 0 &&
           point.X >= X &&
           point.X < X + W &&
           point.Y >= Y &&
           point.Y < Y + H;
}

public sealed partial class Ui
{
    /// <summary>
    /// Optional docking state. When assigned, windows can be docked into <see cref="DockSpace(UiId, float?, float?, bool)"/>.
    /// </summary>
    public DockingState? Docking { get; set; }

    /// <summary>Reserves a dock-space region for dockable windows.</summary>
    public Response DockSpace(UiId id, float? width = null, float? height = null, bool enabled = true)
    {
        UiId resolvedId = RequireSpecifiedId(id, nameof(id));
        int widgetId = MakeWidgetId(UiWidgetKind.DockSpace, resolvedId);
        RegisterWidgetId(widgetId, "DockSpace");
        MarkWidgetSeen(widgetId);

        float resolvedWidth = MathF.Max(0, width ?? AvailableWidth);
        float previewHeight = MathF.Max(0, height ?? 0);
        var (x, y) = Place(resolvedWidth, previewHeight);
        float resolvedHeight = MathF.Max(0, height ?? MathF.Max(120f, _vpH - y - RootPadding));
        var rect = new DockRect(x, y, resolvedWidth, resolvedHeight);

        bool directHover = enabled && MouseInHitClip() && rect.Contains(_mouse);
        bool hover = directHover && (Docking?.DraggingWindowId != 0 || CanHitCurrentContext());
        if (hover)
            _hotId = widgetId;

        DrawFrameRect(x, y, resolvedWidth, resolvedHeight, Theme.ScrollAreaBg, Theme.ScrollAreaBorder);

        if (Docking != null)
        {
            var space = Docking.GetSpace(widgetId, _frameIndex);
            space.Rect = rect;
        }

        Advance(resolvedWidth, resolvedHeight);
        return new Response(x, y, resolvedWidth, resolvedHeight, hover, false, false, disabled: !enabled);
    }

    private bool IsWindowDocked(int windowId)
        => Docking?.IsWindowDocked(windowId) == true;

    private bool TryGetDockedWindowRect(int windowId, out ClipRect rect)
    {
        if (Docking != null && Docking.TryGetWindowRect(windowId, out var dockRect))
        {
            rect = new ClipRect(dockRect.X, dockRect.Y, dockRect.W, dockRect.H);
            return true;
        }

        rect = default;
        return false;
    }

    private void ResolveDockingDrops()
    {
        if (Docking == null)
            return;

        UpdateDockLayouts();
        Docking.DraggingWindowId = 0;

        foreach (var pair in _windowRuntimeStates)
        {
            int windowId = pair.Key;
            var runtime = pair.Value;
            if (runtime.Dragging)
                Docking.DraggingWindowId = windowId;

            if (runtime.ReleasedDrag &&
                _windowRequests.TryGetValue(windowId, out var request) &&
                Docking.TryFindDropTarget(_mouse, _frameIndex, out var target))
            {
                request.State.Collapsed = false;
                Docking.DockWindow(windowId, target);
                runtime.Dragging = false;
                runtime.ReleasedDrag = false;
            }
        }
    }

    private void RenderDockSpaces()
    {
        if (Docking == null)
            return;

        _visibleWindowIdsScratch.Clear();
        foreach (int windowId in _windowRequests.Keys)
            _visibleWindowIdsScratch.Add(windowId);

        Docking.PruneMissingWindows(_visibleWindowIdsScratch, _frameIndex);
        int dockingVersion = Docking.Version;
        _dockSpaceOrderScratch.Clear();
        _dockSpaceOrderScratch.AddRange(Docking.SpaceOrder);

        foreach (int spaceId in _dockSpaceOrderScratch)
        {
            if (Docking.Version != dockingVersion)
                return;

            if (!Docking.Spaces.TryGetValue(spaceId, out var space) ||
                space.LastSeenFrame != _frameIndex ||
                !DockNodeHasWindows(space.Root))
            {
                continue;
            }

            LayoutDockNode(space.Root, space.Rect);
            RenderDockSpace(space);
            if (Docking.Version != dockingVersion)
                return;
        }
    }

    private void RenderDockSpace(DockSpaceRuntime space)
        => RenderDockNode(space, space.Root);

    private void RenderDockNode(DockSpaceRuntime space, DockNode node)
    {
        if (!node.IsLeaf)
        {
            var splitter = UpdateDockSplitterInput(space, node);
            if (splitter.Changed)
                LayoutDockNode(node, node.Rect);

            if (node.First != null)
                RenderDockNode(space, node.First);
            if (node.Second != null)
                RenderDockNode(space, node.Second);

            DrawDockSplitter(node, splitter.Hovered, splitter.Pressed);
            return;
        }

        RenderDockLeaf(space, node);
    }

    private void RenderDockLeaf(DockSpaceRuntime space, DockNode node)
    {
        float border = FrameBorderWidth;
        float x = node.Rect.X;
        float y = node.Rect.Y;
        float w = node.Rect.W;
        float h = node.Rect.H;
        if (w <= 0 || h <= 0)
            return;
        var docking = Docking!;
        int dockingVersion = docking.Version;

        node.WindowIds.RemoveAll(windowId => !_windowRequests.ContainsKey(windowId));
        if (node.WindowIds.Count == 0)
        {
            node.ActiveWindowId = 0;
            return;
        }

        if (!node.WindowIds.Contains(node.ActiveWindowId))
            node.ActiveWindowId = node.WindowIds[0];

        foreach (int windowId in node.WindowIds)
        {
            _dockedWindowIdsRenderedThisFrame.Add(windowId);
            if (_windowRequests.TryGetValue(windowId, out var dockedRequest))
                dockedRequest.State.Collapsed = false;
        }

        float tabHeight = MathF.Max(24f, LayoutText("Ag", DefaultFontSize).Height + Theme.TabPadding.Vertical);
        float tabX = x + border;
        float tabY = y + border;
        float tabMaxRight = x + w - border;
        int activeWindowId = node.ActiveWindowId;
        if (!_windowRequests.TryGetValue(activeWindowId, out var activeRequest))
            return;

        var activeRuntime = GetWindowRuntimeState(activeRequest.WindowId);
        activeRuntime.Position = new Vector2(x, y);
        activeRuntime.Width = w;
        activeRuntime.Height = h;
        activeRuntime.TitleBarHeight = tabHeight;
        activeRequest.State.Position = activeRuntime.Position;
        docking.WindowRects[activeRequest.WindowId] = new DockRect(x, y, w, h);

        int topHitWindowId = GetTopHitWindowId();
        bool dockInteractive = topHitWindowId == 0 || node.WindowIds.Contains(topHitWindowId);

        for (int i = 0; i < node.WindowIds.Count; i++)
        {
            int windowId = node.WindowIds[i];
            if (!_windowRequests.TryGetValue(windowId, out var request))
                continue;

            TextLayoutResult label = LayoutText(request.Title, DefaultFontSize, overflow: TextOverflowMode.Ellipsis);
            bool active = windowId == activeWindowId;
            const float TabButtonInset = 2f;
            const float TabButtonGap = 4f;
            float tabButtonSize = MathF.Min(16f, MathF.Max(14f, tabHeight - TabButtonInset * 2f));
            int tabButtonCount = 1 + (request.Closable ? 1 : 0);
            float activeButtonReserve = active
                ? tabButtonSize * tabButtonCount +
                  TabButtonGap * Math.Max(0, tabButtonCount - 1) +
                  TabButtonInset
                : 0f;
            float desiredW = label.Width + Theme.TabPadding.Horizontal + activeButtonReserve;
            float tabW = Math.Clamp(desiredW, 58f, MathF.Max(58f, tabMaxRight - tabX));
            if (tabX + tabW > tabMaxRight + 0.1f)
                break;

            var tabRect = new ClipRect(tabX, tabY, tabW, tabHeight);
            int tabId = HashMix(windowId, UiId.HashString("dock-tab"));
            MarkWidgetSeen(tabId);
            bool rawHover = PointInRect(tabRect, _mouse) &&
                            dockInteractive &&
                            MouseInHitClip() &&
                            _openPopupIds.Count == 0 &&
                            !_popupDismissedThisPress;
            bool showButtons = active || rawHover;
            float buttonY = tabY + MathF.Max(0, (tabHeight - tabButtonSize) * 0.5f);
            float buttonX = tabX + tabW - Theme.TabPadding.Right - tabButtonSize;
            ClipRect closeButtonRect = default;
            ClipRect undockButtonRect;
            if (request.Closable)
            {
                closeButtonRect = new ClipRect(buttonX, buttonY, tabButtonSize, tabButtonSize);
                buttonX -= tabButtonSize + TabButtonGap;
            }

            undockButtonRect = new ClipRect(buttonX, buttonY, tabButtonSize, tabButtonSize);
            int undockButtonId = HashMix(windowId, UiId.HashString("dock-undock"));
            int closeButtonId = HashMix(windowId, UiId.HashString("dock-close"));
            (bool Hover, bool Pressed, bool Clicked) undockButton = default;
            (bool Hover, bool Pressed, bool Clicked) closeButton = default;
            if (showButtons)
            {
                undockButton = EvaluateDockTabButton(undockButtonId, undockButtonRect, dockInteractive);
                if (request.Closable)
                    closeButton = EvaluateDockTabButton(closeButtonId, closeButtonRect, dockInteractive);
            }

            bool buttonHovered = undockButton.Hover || closeButton.Hover;
            bool hover = rawHover && !buttonHovered;
            if (hover)
            {
                _hotId = tabId;
                RequestCursor(UiCursor.PointingHand);
            }

            if (hover && IsMousePressed(UiMouseButton.Left))
            {
                _activeId = tabId;
                node.ActiveWindowId = windowId;
                activeWindowId = windowId;
            }

            if ((undockButton.Hover || closeButton.Hover) && IsMousePressed(UiMouseButton.Left))
            {
                node.ActiveWindowId = windowId;
                activeWindowId = windowId;
            }

            bool pressed = _activeId == tabId && IsMouseDown(UiMouseButton.Left);
            if (pressed && Vector2.DistanceSquared(_mousePressOrigins[MouseButtonIndex(UiMouseButton.Left)], _mouse) >= DragStartThreshold * DragStartThreshold)
            {
                DetachDockedWindow(space, windowId, tabHeight, beginDrag: true);
                return;
            }

            if (undockButton.Clicked)
            {
                DetachDockedWindow(space, windowId, tabHeight, beginDrag: false);
                return;
            }

            if (closeButton.Clicked)
            {
                request.State.Open = false;
                Docking!.RemoveWindow(windowId);
                var runtime = GetWindowRuntimeState(windowId);
                runtime.Dragging = false;
                runtime.ReleasedDrag = false;
                runtime.Resizing = false;
                runtime.DraggingScrollThumb = false;
                return;
            }

            Color fill = active ? Theme.PanelBg : rawHover ? Theme.ButtonBgHover : Theme.ButtonBg;
            Color stroke = active ? Theme.Accent.WithAlpha(220) : Theme.ButtonBorder;
            _painter.DrawRect(tabX, tabY, tabW, tabHeight, fill, stroke, FrameBorderWidth, MathF.Min(FrameRadius, tabHeight * 0.35f));

            float labelX = tabX + Theme.TabPadding.Left;
            float labelY = tabY + MathF.Max(0, (tabHeight - label.Height) * 0.5f);
            float labelMaxW = MathF.Max(0, (showButtons ? undockButtonRect.X - TabButtonGap : tabX + tabW - Theme.TabPadding.Right) - labelX);
            TextLayoutResult clippedLabel = LayoutText(request.Title, DefaultFontSize, maxWidth: labelMaxW, overflow: TextOverflowMode.Ellipsis);
            DrawTextLayout(clippedLabel, labelX, labelY, active ? Theme.TextPrimary : Theme.TextSecondary);

            if (showButtons)
            {
                DrawWindowTitleButton(undockButtonRect, WindowTitleIcon.Undock, undockButton.Hover, undockButton.Pressed);
                if (request.Closable)
                    DrawWindowTitleButton(closeButtonRect, WindowTitleIcon.Close, closeButton.Hover, closeButton.Pressed);
            }

            tabX += tabW + Theme.TabSpacing;
        }

        if (node.ActiveWindowId != activeRequest.WindowId &&
            _windowRequests.TryGetValue(node.ActiveWindowId, out var switchedRequest))
        {
            activeRequest = switchedRequest;
            activeRuntime = GetWindowRuntimeState(activeRequest.WindowId);
            activeRuntime.Position = new Vector2(x, y);
            activeRuntime.Width = w;
            activeRuntime.Height = h;
            activeRuntime.TitleBarHeight = tabHeight;
            activeRequest.State.Position = activeRuntime.Position;
            docking.WindowRects[activeRequest.WindowId] = new DockRect(x, y, w, h);
        }

        float bodyX = x + border;
        float bodyY = y + border + tabHeight;
        float bodyW = MathF.Max(0, w - border * 2);
        float bodyH = MathF.Max(0, h - border * 2 - tabHeight);

        foreach (int windowId in node.WindowIds)
        {
            if (windowId == activeRequest.WindowId)
                continue;

            var runtime = GetWindowRuntimeState(windowId);
            runtime.Width = 0;
            runtime.Height = 0;
            runtime.TitleBarHeight = 0;
        }

        if (bodyW <= 0 || bodyH <= 0 || activeRequest.State.Collapsed)
            return;

        var bodyPad = Theme.PanelPadding;
        const float ScrollbarGap = 4f;
        float contentX = bodyX + bodyPad.Left;
        float contentY = bodyY + bodyPad.Top;
        float contentH = MathF.Max(0, bodyH - bodyPad.Vertical);
        if (contentH <= 0 || !activeRequest.Content.HasContent)
            return;

        float previousContentHeight = MathF.Max(activeRuntime.ContentHeight, contentH);
        float previousMaxScroll = MathF.Max(0, previousContentHeight - contentH);
        bool bodyScrollable = previousMaxScroll > 0.5f;
        float scrollbarReserve = bodyScrollable ? Theme.ScrollbarWidth + ScrollbarGap : 0f;
        float contentW = MathF.Max(0, bodyW - bodyPad.Horizontal - scrollbarReserve);
        if (contentW <= 0)
            return;

        float trackX = bodyX + bodyW - Theme.ScrollbarWidth;
        float trackY = contentY;
        float trackH = contentH;
        int scrollId = HashMix(activeRequest.WindowId, UiId.HashString("scroll"));
        bool scrollThumbPressed = false;
        if (bodyScrollable)
        {
            MarkWidgetSeen(scrollId);
            int currentTopHitWindowId = GetTopHitWindowId();
            bool windowInteractive =
                (currentTopHitWindowId == 0 || currentTopHitWindowId == activeRequest.WindowId) &&
                _openPopupIds.Count == 0 &&
                !_popupDismissedThisPress;
            bool bodyHover = windowInteractive &&
                             PointInRect(new ClipRect(bodyX, bodyY, bodyW, bodyH), _mouse);

            float thumbH = MathF.Max(Theme.ScrollbarMinThumbSize, (contentH * contentH) / previousContentHeight);
            float thumbTravel = MathF.Max(0, contentH - thumbH);
            float thumbY = thumbTravel > 0 && previousMaxScroll > 0
                ? trackY + (activeRuntime.ScrollY / previousMaxScroll) * thumbTravel
                : trackY;
            bool scrollThumbHovered =
                windowInteractive &&
                PointInRect(new ClipRect(trackX, thumbY, Theme.ScrollbarWidth, thumbH), _mouse);

            if (scrollThumbHovered || activeRuntime.DraggingScrollThumb)
            {
                _hotId = scrollId;
                RequestCursor(UiCursor.PointingHand);
            }

            if (scrollThumbHovered && IsMousePressed(UiMouseButton.Left))
            {
                _activeId = scrollId;
                activeRuntime.DraggingScrollThumb = true;
                activeRuntime.ThumbDragOffsetY = _mouse.Y - thumbY;
            }

            if (bodyHover && _input.WheelDelta.Y != 0 && !activeRuntime.DraggingScrollThumb)
                activeRuntime.ScrollY = Math.Clamp(activeRuntime.ScrollY - _input.WheelDelta.Y * Theme.ScrollWheelStep, 0, previousMaxScroll);

            if (activeRuntime.DraggingScrollThumb &&
                _activeId == scrollId &&
                IsMouseDown(UiMouseButton.Left) &&
                thumbTravel > 0 &&
                previousMaxScroll > 0)
            {
                float thumbTop = Math.Clamp(_mouse.Y - activeRuntime.ThumbDragOffsetY, trackY, trackY + thumbTravel);
                float scrollRatio = (thumbTop - trackY) / thumbTravel;
                activeRuntime.ScrollY = scrollRatio * previousMaxScroll;
            }

            activeRuntime.ScrollY = Math.Clamp(activeRuntime.ScrollY, 0, previousMaxScroll);
            scrollThumbPressed = activeRuntime.DraggingScrollThumb && _activeId == scrollId && IsMouseDown(UiMouseButton.Left);
        }

        Painter RenderDockContentPass(float availableWidth, out float passContentHeight)
        {
            var parentPainter = _painter;
            var passPainter = AcquireDeferredPainter();
            _painter = passPainter;

            int previousWindowContextId = _windowContextId;
            _windowContextId = activeRequest.WindowId;
            _idStack.Push(activeRequest.WindowId);
            _painter.PushClip(contentX, contentY, availableWidth, contentH);
            PushHitClip(contentX, contentY, availableWidth, contentH);

            _layouts.Add(new LayoutScope
            {
                OriginX = contentX,
                OriginY = contentY - activeRuntime.ScrollY,
                CursorX = contentX,
                CursorY = contentY - activeRuntime.ScrollY,
                Dir = LayoutDir.Vertical,
                WidthConstraint = availableWidth,
                HasWidthConstraint = true,
                Empty = true
            });

            try
            {
                activeRequest.Content.Invoke(this);

                var inner = _layouts[^1];
                passContentHeight = inner.Dir == LayoutDir.Horizontal
                    ? inner.MaxExtent
                    : inner.CursorY - inner.OriginY;
            }
            finally
            {
                _layouts.RemoveAt(_layouts.Count - 1);
                PopHitClip();
                _painter.PopClip();
                _idStack.Pop();
                _windowContextId = previousWindowContextId;
                _painter = parentPainter;
            }

            return passPainter;
        }

        Painter contentPainter = RenderDockContentPass(contentW, out float contentHeight);
        if (docking.Version != dockingVersion)
        {
            ReleaseDeferredPainter(contentPainter);
            return;
        }

        bool measuredBodyScrollable = MathF.Max(0, contentHeight - contentH) > 0.5f;
        bool hasInputEdgeThisFrame = _input.PressedKeys?.Count > 0 ||
                                     _input.TextInput.Length > 0 ||
                                     _input.WheelDelta.X != 0 ||
                                     _input.WheelDelta.Y != 0;
        for (int i = 0; i < _mouseButtonsPressed.Length && !hasInputEdgeThisFrame; i++)
            hasInputEdgeThisFrame = _mouseButtonsPressed[i] || _mouseButtonsReleased[i];

        if (measuredBodyScrollable != bodyScrollable && !hasInputEdgeThisFrame)
        {
            ReleaseDeferredPainter(contentPainter);
            bodyScrollable = measuredBodyScrollable;
            scrollbarReserve = bodyScrollable ? Theme.ScrollbarWidth + ScrollbarGap : 0f;
            contentW = MathF.Max(0, bodyW - bodyPad.Horizontal - scrollbarReserve);
#if DEBUG
            _idTrackingDisabledDepth++;
            try
            {
                contentPainter = RenderDockContentPass(contentW, out contentHeight);
            }
            finally
            {
                _idTrackingDisabledDepth--;
            }
#else
            contentPainter = RenderDockContentPass(contentW, out contentHeight);
#endif
            if (docking.Version != dockingVersion)
            {
                ReleaseDeferredPainter(contentPainter);
                return;
            }
        }

        activeRuntime.ContentHeight = contentHeight;
        float maxScroll = MathF.Max(0, contentHeight - contentH);
        activeRuntime.ScrollY = Math.Clamp(activeRuntime.ScrollY, 0, maxScroll);
        if (!bodyScrollable || maxScroll <= 0.5f)
            activeRuntime.DraggingScrollThumb = false;

        _painter.Append(contentPainter.RenderList);
        ReleaseDeferredPainter(contentPainter);

        if (bodyScrollable && maxScroll > 0.5f)
        {
            float currentThumbH = MathF.Max(Theme.ScrollbarMinThumbSize, (contentH * contentH) / MathF.Max(contentHeight, contentH));
            float currentThumbTravel = MathF.Max(0, contentH - currentThumbH);
            float currentThumbY = currentThumbTravel > 0
                ? trackY + (activeRuntime.ScrollY / maxScroll) * currentThumbTravel
                : trackY;
            bool currentThumbHover =
                PointInRect(new ClipRect(trackX, currentThumbY, Theme.ScrollbarWidth, currentThumbH), _mouse) &&
                _openPopupIds.Count == 0 &&
                !_popupDismissedThisPress;

            if (currentThumbHover || scrollThumbPressed)
                RequestCursor(UiCursor.PointingHand);

            float scrollbarRadius = MathF.Min(FrameRadius, Theme.ScrollbarWidth * 0.5f);
            _painter.DrawRect(trackX, trackY, Theme.ScrollbarWidth, trackH, Theme.ScrollbarTrack, radius: scrollbarRadius);

            Color thumbColor = scrollThumbPressed
                ? Theme.ScrollbarThumbActive
                : currentThumbHover
                    ? Theme.ScrollbarThumbHover
                    : Theme.ScrollbarThumb;
            _painter.DrawRect(trackX, currentThumbY, Theme.ScrollbarWidth, currentThumbH, thumbColor, radius: scrollbarRadius);
        }
    }

    private (bool Hover, bool Pressed, bool Clicked) EvaluateDockTabButton(int id, in ClipRect rect, bool dockInteractive)
    {
        MarkWidgetSeen(id);

        bool hover = dockInteractive &&
                     PointInRect(rect, _mouse) &&
                     MouseInHitClip() &&
                     _openPopupIds.Count == 0 &&
                     !_popupDismissedThisPress;
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

    private void DetachDockedWindow(DockSpaceRuntime space, int windowId, float tabHeight, bool beginDrag)
    {
        if (Docking == null || !_windowRequests.TryGetValue(windowId, out var request))
            return;

        Docking.RemoveWindow(windowId);
        var runtime = GetWindowRuntimeState(windowId);
        float floatingW = MathF.Max(160f, request.Width);
        float floatingH = MathF.Max(96f, MathF.Min(280f, space.Rect.H));
        runtime.Position = _mouse - new Vector2(MathF.Min(64f, floatingW * 0.5f), MathF.Max(10f, tabHeight * 0.5f));
        runtime.Width = floatingW;
        runtime.Height = floatingH;
        runtime.TitleBarHeight = tabHeight;
        runtime.DragOffset = _mouse - runtime.Position;
        runtime.Initialized = true;
        runtime.Dragging = beginDrag;
        runtime.ReleasedDrag = false;
        runtime.Resizing = false;
        runtime.DraggingScrollThumb = false;
        request.State.Position = runtime.Position;
        if (beginDrag)
        {
            _activeId = request.WindowId;
            Docking.DraggingWindowId = windowId;
        }
        else if (_activeId != 0)
        {
            _activeId = 0;
        }

        BringWindowToFront(windowId);
    }

    private void UpdateDockLayouts()
    {
        if (Docking == null)
            return;

        foreach (int spaceId in Docking.SpaceOrder)
        {
            if (Docking.Spaces.TryGetValue(spaceId, out var space) &&
                space.LastSeenFrame == _frameIndex)
            {
                LayoutDockNode(space.Root, space.Rect);
            }
        }
    }

    private void LayoutDockNode(DockNode node, DockRect rect)
    {
        node.Rect = rect;
        if (node.IsLeaf)
        {
            node.SplitterRect = default;
            return;
        }

        if (node.First == null || node.Second == null)
            return;

        float splitterSize = GetDockSplitterSize();
        float fraction = ClampDockFraction(node.Fraction, node.Orientation == DockSplitOrientation.Horizontal ? rect.W : rect.H);
        node.Fraction = fraction;

        if (node.Orientation == DockSplitOrientation.Horizontal)
        {
            float available = MathF.Max(0, rect.W - splitterSize);
            float firstW = MathF.Floor(available * fraction);
            float secondW = MathF.Max(0, available - firstW);
            node.SplitterRect = new DockRect(rect.X + firstW, rect.Y, splitterSize, rect.H);
            LayoutDockNode(node.First, new DockRect(rect.X, rect.Y, firstW, rect.H));
            LayoutDockNode(node.Second, new DockRect(rect.X + firstW + splitterSize, rect.Y, secondW, rect.H));
        }
        else
        {
            float available = MathF.Max(0, rect.H - splitterSize);
            float firstH = MathF.Floor(available * fraction);
            float secondH = MathF.Max(0, available - firstH);
            node.SplitterRect = new DockRect(rect.X, rect.Y + firstH, rect.W, splitterSize);
            LayoutDockNode(node.First, new DockRect(rect.X, rect.Y, rect.W, firstH));
            LayoutDockNode(node.Second, new DockRect(rect.X, rect.Y + firstH + splitterSize, rect.W, secondH));
        }
    }

    private (bool Hovered, bool Pressed, bool Changed) UpdateDockSplitterInput(DockSpaceRuntime space, DockNode node)
    {
        var rect = node.SplitterRect;
        int splitterId = HashMix(space.Id, HashMix(node.Id, UiId.HashString("splitter")));
        MarkWidgetSeen(splitterId);

        int topHitWindowId = GetTopHitWindowId();
        bool hover = PointInRect(new ClipRect(rect.X, rect.Y, rect.W, rect.H), _mouse) &&
                     MouseInHitClip() &&
                     _openPopupIds.Count == 0 &&
                     !_popupDismissedThisPress &&
                     (topHitWindowId == 0 || (Docking?.WindowNodes.ContainsKey(topHitWindowId) == true));

        if (hover || _activeId == splitterId)
        {
            _hotId = splitterId;
            RequestCursor(node.Orientation == DockSplitOrientation.Horizontal ? UiCursor.ResizeEW : UiCursor.ResizeNS);
        }

        if (hover && IsMousePressed(UiMouseButton.Left))
            _activeId = splitterId;

        bool pressed = _activeId == splitterId && IsMouseDown(UiMouseButton.Left);
        bool changed = false;
        if (pressed)
        {
            float axisSize = node.Orientation == DockSplitOrientation.Horizontal ? node.Rect.W : node.Rect.H;
            float local = node.Orientation == DockSplitOrientation.Horizontal
                ? _mouse.X - node.Rect.X
                : _mouse.Y - node.Rect.Y;
            float next = ClampDockFraction(axisSize <= 0 ? 0.5f : local / axisSize, axisSize);
            changed = MathF.Abs(next - node.Fraction) > 0.0001f;
            node.Fraction = next;
        }

        return (hover, pressed, changed);
    }

    private void DrawDockSplitter(DockNode node, bool hover, bool pressed)
    {
        var rect = node.SplitterRect;
        Color color = pressed ? Theme.Accent.WithAlpha(220)
            : hover ? Theme.Accent.WithAlpha(160)
            : Theme.Separator;
        _painter.DrawRect(rect.X, rect.Y, rect.W, rect.H, color);
    }

    private void RenderDockingPreview()
    {
        if (Docking == null ||
            Docking.DraggingWindowId == 0 ||
            !Docking.TryFindDropTarget(_mouse, _frameIndex, out var target))
        {
            return;
        }

        var rect = target.PreviewRect;
        float border = MathF.Max(1f, FrameBorderWidth * 2f);
        _painter.DrawRect(
            rect.X + border,
            rect.Y + border,
            MathF.Max(0, rect.W - border * 2),
            MathF.Max(0, rect.H - border * 2),
            Theme.Accent.WithAlpha(42),
            Theme.Accent.WithAlpha(190),
            border,
            MathF.Max(0, FrameRadius - border));
    }

    private static bool DockNodeHasWindows(DockNode node)
    {
        if (node.IsLeaf)
            return node.WindowIds.Count > 0;

        return (node.First != null && DockNodeHasWindows(node.First)) ||
               (node.Second != null && DockNodeHasWindows(node.Second));
    }

    private float GetDockSplitterSize()
        => MathF.Max(4f, FrameBorderWidth * 4f);

    private static float ClampDockFraction(float fraction, float axisSize)
    {
        if (axisSize <= 0)
            return 0.5f;

        float min = Math.Clamp(48f / axisSize, 0.08f, 0.45f);
        return Math.Clamp(fraction, min, 1f - min);
    }
}
