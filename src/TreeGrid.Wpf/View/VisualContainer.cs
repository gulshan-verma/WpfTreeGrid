using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using TreeGrid.Wpf.Columns;
using TreeGrid.Wpf.Data;
using TreeGrid.Wpf.Merging;
using TreeGrid.Wpf.Styling;

namespace TreeGrid.Wpf.View
{
    /// <summary>
    /// The row virtualizer. Realises only the rows intersecting the viewport and
    /// recycles row containers as the user scrolls, so memory and layout cost track
    /// viewport size rather than record count.
    /// <para>
    /// Rows are arranged full-viewport-width and translate their own cells; the
    /// container never applies a horizontal render transform. That separation is what
    /// lets frozen columns stay put during horizontal scroll.
    /// </para>
    /// </summary>
    public class VisualContainer : Panel, IScrollInfo
    {
        private readonly Dictionary<int, TreeGridRowControl> _realizedRows
            = new Dictionary<int, TreeGridRowControl>();

        private readonly Stack<TreeGridRowControl> _rowPool = new Stack<TreeGridRowControl>();
        private readonly List<int> _staleScratch = new List<int>();

        private Size _viewport;
        private Size _extent;
        private Vector _offset;

        public FlatTreeView Source { get; set; }

        public ColumnLayout Layout { get; set; }

        public double RowHeight { get; set; } = 26d;

        public double IndentPerLevel { get; set; } = 18d;

        public int TreeColumnIndex { get; set; }

        public Brush GridLineBrush { get; set; }

        public Brush AlternatingRowBrush { get; set; }

        public Brush SelectedRowBrush { get; set; }

        public bool ShowAlternatingRows { get; set; }

        public bool EnableColumnVirtualization { get; set; } = true;

        /// <summary>Shows the hierarchy checkbox in the expander column.</summary>
        public bool ShowNodeCheckBox { get; set; }

        /// <summary>True when selection is per-cell rather than per-row.</summary>
        public bool IsCellSelectionUnit { get; set; }

        /// <summary>Supplies the current-cell column for a given node, or -1.</summary>
        public Func<TreeNode, int> CurrentColumnResolver { get; set; }

        /// <summary>Resolves vertical merge participation for a cell.</summary>
        public Func<int, int, TreeGridColumn, MergeRenderInfo> MergeResolver { get; set; }

        /// <summary>Supplies the validation error message for a cell, or null.</summary>
        public Func<TreeNode, TreeGridColumn, string> ErrorResolver { get; set; }

        public Brush FrozenLineBrush { get; set; }

        /// <summary>Opaque backing brush for cells that span multiple rows.</summary>
        public Brush MergedCellBackground { get; set; }

        public TreeGridVisualStyle VisualStyle { get; set; }

        /// <summary>Supplies per-row conditional overrides, or null.</summary>
        public Func<TreeNode, int, QueryRowStyleEventArgs> RowStyleResolver { get; set; }

        /// <summary>Supplies per-cell conditional overrides, or null.</summary>
        public Func<TreeNode, TreeGridColumn, int, QueryCellStyleEventArgs> CellStyleResolver { get; set; }

        /// <summary>Flat index of the row under the pointer, or -1.</summary>
        public int HoveredRowIndex { get; private set; } = -1;

        /// <summary>Index of the topmost realised row. Merge clamping needs this.</summary>
        public int FirstVisibleRow { get; private set; }

        /// <summary>Number of extra rows realised above and below the viewport.</summary>
        public int OverscanRows { get; set; } = 2;

        public event EventHandler ScrollOffsetChanged;

        public event EventHandler<TreeGridRowControl> RowPrepared;

        /// <summary>Drops every realised row. Call after a source reset.</summary>
        public void ResetRows()
        {
            foreach (var kvp in _realizedRows)
                RecycleRow(kvp.Value);

            _realizedRows.Clear();
            InvalidateMeasure();
        }

        public TreeGridRowControl GetRealizedRow(int index) =>
            _realizedRows.TryGetValue(index, out var row) ? row : null;

        /// <summary>Row index under a point in container coordinates, or -1.</summary>
        public int RowIndexFromPoint(Point point)
        {
            if (RowHeight <= 0)
                return -1;

            var index = (int)Math.Floor((point.Y + _offset.Y) / RowHeight);
            return index >= 0 && index < RowCount ? index : -1;
        }

        private int RowCount => Source?.Count ?? 0;

        /// <summary>
        /// Repaints selection state on realised rows without a measure pass. This is
        /// the hot path for arrow-key navigation and rubber-band selection.
        /// </summary>
        public void RefreshRowStates()
        {
            foreach (var kvp in _realizedRows)
            {
                var node = Source != null && kvp.Key < Source.Count ? Source[kvp.Key] : null;
                if (node == null)
                    continue;

                var row = kvp.Value;
                row.IsRowSelected = node.IsSelected;
                row.IsCellSelectionUnit = IsCellSelectionUnit;
                row.CurrentColumnIndex = CurrentColumnResolver?.Invoke(node) ?? -1;
                row.Background = ResolveRowBackground(kvp.Key, node);
                row.RefreshState();
            }
        }

        /// <summary>Rebinds realised rows in place, keeping scroll position and pooling.</summary>
        public void RefreshRowContent()
        {
            foreach (var kvp in _realizedRows)
                kvp.Value.RefreshCells();
        }

        /// <summary>Column index under a point in container coordinates, or -1.</summary>
        public int ColumnIndexFromPoint(Point point) =>
            Layout?.ColumnIndexFromX(point.X, _offset.X, _viewport.Width) ?? -1;

        // ------------------------------------------------------------- layout

        protected override Size MeasureOverride(Size availableSize)
        {
            if (Layout == null || Source == null)
                return new Size(0, 0);

            var width = double.IsInfinity(availableSize.Width) ? Layout.TotalWidth : availableSize.Width;
            var height = double.IsInfinity(availableSize.Height) ? RowHeight * 20 : availableSize.Height;

            UpdateScrollInfo(new Size(width, height));
            RealizeRows(width, height);

            foreach (var kvp in _realizedRows)
                kvp.Value.Measure(new Size(width, RowHeight));

            return new Size(width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            foreach (var kvp in _realizedRows)
            {
                var y = kvp.Key * RowHeight - _offset.Y;
                kvp.Value.Arrange(new Rect(0, y, finalSize.Width, RowHeight));
            }

            return finalSize;
        }

        /// <summary>Row index and offset within that row, for drag-drop hit testing.</summary>
        public (int RowIndex, double OffsetInRow) RowHitTest(Point point)
        {
            if (RowHeight <= 0)
                return (-1, 0);

            var absolute = point.Y + _offset.Y;
            var index = (int)Math.Floor(absolute / RowHeight);

            if (index < 0 || index >= RowCount)
                return (-1, 0);

            return (index, absolute - index * RowHeight);
        }

        /// <summary>Y position of a row in container coordinates.</summary>
        public double GetRowOffset(int rowIndex) => rowIndex * RowHeight - _offset.Y;

        private void RealizeRows(double viewportWidth, double viewportHeight)
        {
            var count = RowCount;

            if (count == 0 || RowHeight <= 0)
            {
                ResetRowsInternal();
                return;
            }

            var first = Math.Max(0, (int)Math.Floor(_offset.Y / RowHeight) - OverscanRows);
            FirstVisibleRow = first;
            var last = Math.Min(count - 1,
                (int)Math.Ceiling((_offset.Y + viewportHeight) / RowHeight) + OverscanRows);

            _staleScratch.Clear();
            foreach (var kvp in _realizedRows)
            {
                if (kvp.Key < first || kvp.Key > last)
                    _staleScratch.Add(kvp.Key);
            }

            foreach (var index in _staleScratch)
            {
                RecycleRow(_realizedRows[index]);
                _realizedRows.Remove(index);
            }

            for (var i = first; i <= last; i++)
            {
                var node = Source[i];

                if (!_realizedRows.TryGetValue(i, out var row))
                {
                    row = RentRow();
                    _realizedRows[i] = row;
                    Children.Add(row);
                }

                row.Layout = Layout;
                row.ViewportWidth = viewportWidth;
                row.HorizontalOffset = _offset.X;
                row.IndentPerLevel = IndentPerLevel;
                row.TreeColumnIndex = TreeColumnIndex;
                row.GridLineBrush = GridLineBrush;
                row.EnableColumnVirtualization = EnableColumnVirtualization;
                row.ShowNodeCheckBox = ShowNodeCheckBox;
                row.IsCellSelectionUnit = IsCellSelectionUnit;
                row.IsRowSelected = node.IsSelected;
                row.CurrentColumnIndex = CurrentColumnResolver?.Invoke(node) ?? -1;
                row.RowIndex = i;
                row.RowHeight = RowHeight;
                row.MergeResolver = MergeResolver;
                row.ErrorResolver = ErrorResolver;
                row.FrozenLineBrush = FrozenLineBrush;
                row.MergedCellBackground = MergedCellBackground;
                row.VisualStyle = VisualStyle;
                row.CellStyleResolver = CellStyleResolver;
                row.RowOverrides = ResolveRowOverrides(node, i);
                row.IsRightToLeft = FlowDirection == FlowDirection.RightToLeft;
                row.Background = ResolveRowBackground(i, node);
                row.BindNode(node);

                RowPrepared?.Invoke(this, row);
            }
        }

        private QueryRowStyleEventArgs ResolveRowOverrides(TreeNode node, int rowIndex)
        {
            if (RowStyleResolver == null)
                return null;

            var overrides = RowStyleResolver(node, rowIndex);
            return overrides != null && overrides.HasOverrides ? overrides : null;
        }

        /// <summary>
        /// Row background precedence: conditional override, then selection, then hover,
        /// then the alternating stripe, then the plain row brush.
        /// </summary>
        private Brush ResolveRowBackground(int index, TreeNode node)
        {
            var overrides = ResolveRowOverrides(node, index);

            if (overrides?.Background != null)
                return overrides.Background;

            if (node.IsSelected && SelectedRowBrush != null)
                return SelectedRowBrush;

            if (index == HoveredRowIndex && VisualStyle?.HoverRowBackground != null)
                return VisualStyle.HoverRowBackground;

            if (ShowAlternatingRows && index % 2 == 1 && AlternatingRowBrush != null)
                return AlternatingRowBrush;

            return VisualStyle?.RowBackground ?? Brushes.Transparent;
        }

        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (VisualStyle?.HoverRowBackground == null)
                return;

            var index = RowIndexFromPoint(e.GetPosition(this));

            if (index == HoveredRowIndex)
                return;

            HoveredRowIndex = index;
            RefreshRowStates();
        }

        protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseLeave(e);

            if (HoveredRowIndex < 0)
                return;

            HoveredRowIndex = -1;
            RefreshRowStates();
        }

        private void ResetRowsInternal()
        {
            foreach (var kvp in _realizedRows)
                RecycleRow(kvp.Value);

            _realizedRows.Clear();
        }

        private TreeGridRowControl RentRow()
        {
            if (_rowPool.Count > 0)
                return _rowPool.Pop();

            return new TreeGridRowControl { RowType = TreeGridRowType.Record };
        }

        private void RecycleRow(TreeGridRowControl row)
        {
            Children.Remove(row);
            _rowPool.Push(row);
        }

        /// <summary>Pushes the current horizontal offset into already-realised rows.</summary>
        private void SyncRowHorizontalOffset()
        {
            foreach (var kvp in _realizedRows)
            {
                kvp.Value.HorizontalOffset = _offset.X;
                kvp.Value.RefreshCells();
            }
        }

        // ---------------------------------------------------------- IScrollInfo

        public bool CanVerticallyScroll { get; set; } = true;

        public bool CanHorizontallyScroll { get; set; } = true;

        public double ExtentWidth => _extent.Width;

        public double ExtentHeight => _extent.Height;

        public double ViewportWidth => _viewport.Width;

        public double ViewportHeight => _viewport.Height;

        public double HorizontalOffset => _offset.X;

        public double VerticalOffset => _offset.Y;

        public ScrollViewer ScrollOwner { get; set; }

        private void UpdateScrollInfo(Size viewport)
        {
            var extent = new Size(Layout?.TotalWidth ?? 0, RowCount * RowHeight);

            var changed = extent != _extent || viewport != _viewport;

            _extent = extent;
            _viewport = viewport;

            CoerceOffset();

            if (changed)
                ScrollOwner?.InvalidateScrollInfo();
        }

        private void CoerceOffset()
        {
            var maxX = Math.Max(0, _extent.Width - _viewport.Width);
            var maxY = Math.Max(0, _extent.Height - _viewport.Height);

            var x = Math.Min(Math.Max(0, _offset.X), maxX);
            var y = Math.Min(Math.Max(0, _offset.Y), maxY);

            _offset = new Vector(x, y);
        }

        public void SetHorizontalOffset(double offset)
        {
            if (double.IsNaN(offset))
                return;

            var maxX = Math.Max(0, _extent.Width - _viewport.Width);
            var value = Math.Min(Math.Max(0, offset), maxX);

            if (Math.Abs(value - _offset.X) < 0.01)
                return;

            _offset.X = value;
            SyncRowHorizontalOffset();
            ScrollOwner?.InvalidateScrollInfo();
            ScrollOffsetChanged?.Invoke(this, EventArgs.Empty);
            InvalidateArrange();
        }

        public void SetVerticalOffset(double offset)
        {
            if (double.IsNaN(offset))
                return;

            var maxY = Math.Max(0, _extent.Height - _viewport.Height);
            var value = Math.Min(Math.Max(0, offset), maxY);

            if (Math.Abs(value - _offset.Y) < 0.01)
                return;

            _offset.Y = value;
            ScrollOwner?.InvalidateScrollInfo();
            ScrollOffsetChanged?.Invoke(this, EventArgs.Empty);
            InvalidateMeasure();
        }

        public void LineUp() => SetVerticalOffset(VerticalOffset - RowHeight);

        public void LineDown() => SetVerticalOffset(VerticalOffset + RowHeight);

        public void LineLeft() => SetHorizontalOffset(HorizontalOffset - 16);

        public void LineRight() => SetHorizontalOffset(HorizontalOffset + 16);

        public void PageUp() => SetVerticalOffset(VerticalOffset - ViewportHeight);

        public void PageDown() => SetVerticalOffset(VerticalOffset + ViewportHeight);

        public void PageLeft() => SetHorizontalOffset(HorizontalOffset - ViewportWidth);

        public void PageRight() => SetHorizontalOffset(HorizontalOffset + ViewportWidth);

        public void MouseWheelUp() => SetVerticalOffset(VerticalOffset - RowHeight * SystemParameters.WheelScrollLines);

        public void MouseWheelDown() => SetVerticalOffset(VerticalOffset + RowHeight * SystemParameters.WheelScrollLines);

        public void MouseWheelLeft() => LineLeft();

        public void MouseWheelRight() => LineRight();

        public Rect MakeVisible(Visual visual, Rect rectangle) => rectangle;

        /// <summary>Scrolls the given flat row index into view.</summary>
        public void ScrollIntoView(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= RowCount)
                return;

            var top = rowIndex * RowHeight;
            var bottom = top + RowHeight;

            if (top < _offset.Y)
                SetVerticalOffset(top);
            else if (bottom > _offset.Y + _viewport.Height)
                SetVerticalOffset(bottom - _viewport.Height);
        }
    }
}
