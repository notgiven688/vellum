using Vellum.Rendering;

namespace Vellum;

/// <summary>
/// Describes one table column. A positive width is fixed; zero or negative widths share remaining space.
/// </summary>
public readonly struct TableColumn
{
    /// <summary>Header text rendered when table headers are enabled.</summary>
    public readonly string Header;
    /// <summary>Fixed column width in logical pixels. Values less than or equal to zero stretch.</summary>
    public readonly float Width;
    /// <summary>Default horizontal alignment for text cells in this column.</summary>
    public readonly UiAlign Align;

    /// <summary>Creates a table column.</summary>
    public TableColumn(string header, float width = 0f, UiAlign align = UiAlign.Start)
    {
        Header = header ?? string.Empty;
        Width = width;
        Align = align;
    }
}

public sealed partial class Ui
{
    internal readonly struct TableColumnLayout
    {
        public readonly TableColumn Column;
        public readonly float X;
        public readonly float Width;

        public TableColumnLayout(TableColumn column, float x, float width)
        {
            Column = column;
            X = x;
            Width = width;
        }
    }

    private sealed class TableState
    {
        public readonly TableLayout Layout = new();
        public readonly TableBuilder Builder = new();
    }

    internal sealed class TableLayout
    {
        public int WidgetId;
        public float X;
        public float Y;
        public float Width;
        public float Border;
        public EdgeInsets CellPadding;
        public TableColumnLayout[] Columns = [];
        public float CursorY;
        public int RenderedRowCount;
        public int DataRowCount;

        public void Reset(int widgetId, float x, float y, float width, float border, EdgeInsets cellPadding)
        {
            WidgetId = widgetId;
            X = x;
            Y = y;
            Width = width;
            Border = border;
            CellPadding = cellPadding;
            CursorY = y + border;
            RenderedRowCount = 0;
            DataRowCount = 0;
        }

        public TableColumnLayout[] EnsureColumnCapacity(int columnCount)
        {
            if (Columns.Length != columnCount)
                Columns = new TableColumnLayout[columnCount];

            return Columns;
        }
    }

    /// <summary>Builder passed to a table callback.</summary>
    public sealed class TableBuilder
    {
        private readonly TableRowBuilder _row = new();
        private Ui _ui = null!;
        private TableLayout _layout = null!;
        private bool _stripedRows;

        internal TableBuilder()
        {
        }

        internal void Reset(Ui ui, TableLayout layout, bool stripedRows)
        {
            _ui = ui;
            _layout = layout;
            _stripedRows = stripedRows;
        }

        /// <summary>Number of columns in the table.</summary>
        public int ColumnCount => _layout.Columns.Length;

        /// <summary>Renders a header row from the configured column headers.</summary>
        public Response Header()
            => RenderRow(header: true, this, static (row, table) =>
            {
                for (int i = 0; i < table._layout.Columns.Length; i++)
                {
                    var column = table._layout.Columns[i].Column;
                    row.Cell(
                        column.Header,
                        color: row.Ui.Theme.TextPrimary,
                        align: column.Align);
                }
            });

        /// <summary>Renders a data row.</summary>
        public Response Row(Action<TableRowBuilder> content)
            => RenderRow(header: false, content, static (row, action) => action(row));

        /// <summary>Renders a data row with explicit state passed to the callback.</summary>
        public Response Row<TState>(TState state, Action<TableRowBuilder, TState> content)
            => RenderRow(header: false, (State: state, Content: content), static (row, tuple) => tuple.Content(row, tuple.State));

        private Response RenderRow<TState>(bool header, TState state, Action<TableRowBuilder, TState> content)
        {
            ArgumentNullException.ThrowIfNull(content);

            float rowY = _layout.CursorY;
            int rowSeed = HashMix(_layout.WidgetId, UiId.HashInt(_layout.RenderedRowCount));
            var tablePainter = _ui._painter;
            var rowPainter = _ui.AcquireDeferredPainter();
            _ui._painter = rowPainter;

            _row.Reset(_ui, _layout, rowSeed, rowY, header);
            bool completed = false;
            try
            {
                content(_row, state);
                completed = true;
            }
            finally
            {
                _ui._painter = tablePainter;
                if (!completed)
                    _ui.ReleaseDeferredPainter(rowPainter);
            }

            float rowHeight = _row.ResolveHeight();
            bool rowHovered = _ui.PointIn(_layout.X + _layout.Border, rowY, MathF.Max(0f, _layout.Width - _layout.Border * 2f), rowHeight);
            DrawRowChrome(rowY, rowHeight, header, rowHovered, _layout.DataRowCount);
            _ui._painter.Append(rowPainter.RenderList);
            _ui.ReleaseDeferredPainter(rowPainter);

            _layout.CursorY += rowHeight;
            _layout.RenderedRowCount++;
            if (!header)
                _layout.DataRowCount++;

            return new Response(_layout.X, rowY, _layout.Width, rowHeight, rowHovered, false, false);
        }

        private void DrawRowChrome(float y, float height, bool header, bool hovered, int dataRowIndex)
        {
            float x = _layout.X + _layout.Border;
            float width = MathF.Max(0f, _layout.Width - _layout.Border * 2f);
            Color fill = Color.Transparent;

            if (header)
                fill = _ui.Theme.ButtonBg;
            else if (hovered && _ui.Theme.SelectableBgHover.A > 0)
                fill = _ui.Theme.SelectableBgHover;
            else if (_stripedRows && (dataRowIndex & 1) == 1 && _ui.Theme.SelectableBg.A > 0)
                fill = _ui.Theme.SelectableBg;

            if (fill.A > 0)
                _ui._painter.DrawRect(x, y, width, height, fill);

            if (_ui.Theme.Separator.A > 0)
            {
                float separatorY = y + MathF.Max(0f, height - 1f);
                _ui._painter.DrawRect(x, separatorY, width, 1f, _ui.Theme.Separator);

                for (int i = 1; i < _layout.Columns.Length; i++)
                {
                    float columnX = _layout.Columns[i].X;
                    _ui._painter.DrawRect(columnX, y, 1f, height, _ui.Theme.Separator);
                }
            }
        }
    }

    /// <summary>Builder passed to a table row callback.</summary>
    public sealed class TableRowBuilder
    {
        private Ui _ui = null!;
        private TableLayout _layout = null!;
        private int _rowSeed;
        private float _rowY;
        private bool _header;
        private int _columnIndex;
        private float _maxContentHeight;

        internal TableRowBuilder()
        {
        }

        internal void Reset(Ui ui, TableLayout layout, int rowSeed, float rowY, bool header)
        {
            _ui = ui;
            _layout = layout;
            _rowSeed = rowSeed;
            _rowY = rowY;
            _header = header;
            _columnIndex = 0;
            _maxContentHeight = 0;
        }

        internal Ui Ui => _ui;

        /// <summary>Index of the next cell to be rendered.</summary>
        public int ColumnIndex => _columnIndex;

        /// <summary>Renders text into the next cell.</summary>
        public Response Cell(
            string text,
            Color? color = null,
            UiAlign? align = null,
            TextOverflowMode overflow = TextOverflowMode.Ellipsis,
            TextWrapMode wrap = TextWrapMode.NoWrap)
        {
            int columnIndex = _columnIndex;
            EnsureColumnAvailable(columnIndex);
            UiAlign resolvedAlign = align ?? _layout.Columns[columnIndex].Column.Align;
            Color resolvedColor = color ?? (_header ? _ui.Theme.TextPrimary : _ui.Theme.TextSecondary);

            return Cell(
                (Text: text, Color: resolvedColor, Align: resolvedAlign, Overflow: overflow, Wrap: wrap),
                static (cell, data) =>
                {
                    cell.Label(
                        data.Text,
                        color: data.Color,
                        maxWidth: cell.AvailableWidth,
                        wrap: data.Wrap,
                        overflow: data.Overflow,
                        width: cell.AvailableWidth,
                        align: data.Align);
                });
        }

        /// <summary>Renders arbitrary Vellum content into the next cell.</summary>
        public Response Cell(Action<Ui> content)
            => Cell(content, static (cell, action) => action(cell));

        /// <summary>Renders arbitrary Vellum content into the next cell with explicit state.</summary>
        public Response Cell<TState>(TState state, Action<Ui, TState> content)
        {
            ArgumentNullException.ThrowIfNull(content);
            int columnIndex = _columnIndex;
            EnsureColumnAvailable(columnIndex);
            _columnIndex++;

            var column = _layout.Columns[columnIndex];
            var pad = _layout.CellPadding;
            float cellX = column.X;
            float cellY = _rowY;
            float cellW = column.Width;
            float innerX = cellX + pad.Left;
            float innerY = cellY + pad.Top;
            float innerW = MathF.Max(0f, cellW - pad.Horizontal);
            float clipH = MathF.Max(0f, _ui._vpH - cellY);

            int cellSeed = HashMix(_rowSeed, UiId.HashInt(columnIndex));
            _ui._idStack.Push(cellSeed);
            _ui._painter.PushClip(innerX, cellY, innerW, clipH);
            _ui.PushHitClip(innerX, cellY, innerW, clipH);
            _ui._layouts.Add(new LayoutScope
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

            float contentHeight;
            bool completed = false;
            try
            {
                content(_ui, state);
                var inner = _ui._layouts[^1];
                contentHeight = inner.Dir == LayoutDir.Horizontal
                    ? inner.MaxExtent
                    : inner.CursorY - inner.OriginY;
                completed = true;
            }
            finally
            {
                _ui._layouts.RemoveAt(_ui._layouts.Count - 1);
                _ui.PopHitClip();
                _ui._painter.PopClip();
                _ui._idStack.Pop();
            }

            if (!completed)
                return default;

            _maxContentHeight = MathF.Max(_maxContentHeight, contentHeight);
            float cellH = MathF.Max(GetMinimumContentHeight(), contentHeight) + pad.Vertical;
            bool hovered = _ui.PointIn(cellX, cellY, cellW, cellH);
            return new Response(cellX, cellY, cellW, cellH, hovered, false, false);
        }

        internal float ResolveHeight()
            => MathF.Max(GetMinimumContentHeight(), _maxContentHeight) + _layout.CellPadding.Vertical;

        private float GetMinimumContentHeight()
            => MathF.Max(1f, _ui.LayoutText("Ag", _ui.DefaultFontSize).Height);

        private void EnsureColumnAvailable(int columnIndex)
        {
            if (columnIndex >= _layout.Columns.Length)
                throw new InvalidOperationException("Table row has more cells than columns.");
        }
    }

    /// <summary>Renders a compact immediate-mode table.</summary>
    public Response Table(
        UiId id,
        IReadOnlyList<TableColumn> columns,
        Action<TableBuilder> content,
        float? width = null,
        bool header = true,
        bool stripedRows = true,
        EdgeInsets? cellPadding = null)
        => TableCore(id, columns, content, static (table, action) => action(table), width, header, stripedRows, cellPadding);

    /// <summary>Renders a compact immediate-mode table with explicit state passed to the callback.</summary>
    public Response Table<TState>(
        UiId id,
        IReadOnlyList<TableColumn> columns,
        TState state,
        Action<TableBuilder, TState> content,
        float? width = null,
        bool header = true,
        bool stripedRows = true,
        EdgeInsets? cellPadding = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        return TableCore(id, columns, state, content, width, header, stripedRows, cellPadding);
    }

    private Response TableCore<TState>(
        UiId id,
        IReadOnlyList<TableColumn> columns,
        TState state,
        Action<TableBuilder, TState> content,
        float? width,
        bool header,
        bool stripedRows,
        EdgeInsets? cellPadding)
    {
        ArgumentNullException.ThrowIfNull(columns);
        ArgumentNullException.ThrowIfNull(content);
        UiId resolvedId = RequireSpecifiedId(id, nameof(id));
        if (columns.Count == 0)
            throw new ArgumentException("A table requires at least one column.", nameof(columns));

        float resolvedWidth = MathF.Max(0f, width ?? AvailableWidth);
        int widgetId = MakeWidgetId(UiWidgetKind.Table, resolvedId);
        RegisterWidgetId(widgetId, "Table");
        var (x, y) = Place(resolvedWidth, 0f);
        float border = FrameBorderWidth;
        var tableState = GetState<TableState>(widgetId);
        var layout = tableState.Layout;
        layout.Reset(widgetId, x, y, resolvedWidth, border, cellPadding ?? Theme.MenuItemPadding);
        ResolveTableColumns(
            columns,
            x + border,
            MathF.Max(0f, resolvedWidth - border * 2f),
            layout.EnsureColumnCapacity(columns.Count));

        var parentPainter = _painter;
        var tablePainter = AcquireDeferredPainter();
        _painter = tablePainter;
        _idStack.Push(widgetId);

        bool completed = false;
        try
        {
            var table = tableState.Builder;
            table.Reset(this, layout, stripedRows);
            if (header && HasTableHeader(columns))
                table.Header();

            content(table, state);
            completed = true;
        }
        finally
        {
            _idStack.Pop();
            _painter = parentPainter;
            if (!completed)
                ReleaseDeferredPainter(tablePainter);
        }

        float resolvedHeight = MathF.Max(0f, layout.CursorY - y + border);
        bool hovered = PointIn(x, y, resolvedWidth, resolvedHeight);
        _painter.DrawRect(x, y, resolvedWidth, resolvedHeight, Theme.PanelBg, radius: FrameRadius);
        _painter.Append(tablePainter.RenderList);
        float strokeWidth = Theme.PanelBorder.A > 0 ? FrameBorderWidth : 0f;
        _painter.DrawRect(x, y, resolvedWidth, resolvedHeight, default, Theme.PanelBorder, strokeWidth, FrameRadius);
        ReleaseDeferredPainter(tablePainter);
        Advance(resolvedWidth, resolvedHeight);
        return new Response(x, y, resolvedWidth, resolvedHeight, hovered, false, false);
    }

    private static bool HasTableHeader(IReadOnlyList<TableColumn> columns)
    {
        for (int i = 0; i < columns.Count; i++)
        {
            if (!string.IsNullOrEmpty(columns[i].Header))
                return true;
        }

        return false;
    }

    private static void ResolveTableColumns(IReadOnlyList<TableColumn> columns, float x, float width, TableColumnLayout[] result)
    {
        float fixedWidth = 0f;
        int stretchCount = 0;
        for (int i = 0; i < columns.Count; i++)
        {
            if (columns[i].Width > 0f)
                fixedWidth += columns[i].Width;
            else
                stretchCount++;
        }

        float fixedScale = fixedWidth > width && fixedWidth > 0f ? width / fixedWidth : 1f;
        float remaining = MathF.Max(0f, width - fixedWidth * fixedScale);
        float stretchWidth = stretchCount > 0 ? remaining / stretchCount : 0f;
        float cursorX = x;

        for (int i = 0; i < columns.Count; i++)
        {
            float columnWidth = columns[i].Width > 0f
                ? MathF.Max(0f, columns[i].Width * fixedScale)
                : stretchWidth;
            result[i] = new TableColumnLayout(columns[i], cursorX, columnWidth);
            cursorX += columnWidth;
        }
    }
}
