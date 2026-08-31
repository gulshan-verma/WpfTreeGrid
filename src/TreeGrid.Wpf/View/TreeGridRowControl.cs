using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TreeGrid.Wpf.Columns;
using TreeGrid.Wpf.Data;
using TreeGrid.Wpf.Merging;

namespace TreeGrid.Wpf.View
{
    public enum TreeGridRowType
    {
        Record,
        Header
    }

    /// <summary>
    /// Lays out the cells of a single row.
    /// <para>
    /// Rows own their horizontal geometry rather than being translated wholesale by
    /// the scroll viewer. That is what makes frozen panes possible: frozen cells are
    /// arranged at fixed offsets while the middle band is shifted by
    /// <see cref="HorizontalOffset"/> and clipped to the scrollable region.
    /// </para>
    /// </summary>
    public class TreeGridRowControl : Panel
    {
        private readonly Dictionary<int, FrameworkElement> _realized = new Dictionary<int, FrameworkElement>();
        private readonly Stack<TreeGridCell> _cellPool = new Stack<TreeGridCell>();
        private readonly Stack<TreeGridHeaderCell> _headerPool = new Stack<TreeGridHeaderCell>();
        private readonly List<int> _scratchIndices = new List<int>();

        // Membership set alongside the ordered list. The list alone forced a linear
        // Contains scan per column, making cell realization O(columns^2) - painful
        // once column virtualization is off and a grid has 50+ columns.
        private readonly HashSet<int> _scratchSet = new HashSet<int>();
        private readonly List<int> _staleIndices = new List<int>();

        public TreeGridRowType RowType { get; set; } = TreeGridRowType.Record;

        public ColumnLayout Layout { get; set; }

        public TreeNode Node { get; private set; }

        public double HorizontalOffset { get; set; }

        public double ViewportWidth { get; set; }

        public double IndentPerLevel { get; set; } = 18d;

        public double IndentBase { get; set; } = 4d;

        public int TreeColumnIndex { get; set; }

        public bool EnableColumnVirtualization { get; set; } = true;

        public Brush GridLineBrush { get; set; }

        /// <summary>Shows the hierarchy checkbox in the expander column.</summary>
        public bool ShowNodeCheckBox { get; set; }

        /// <summary>Row-level selection state, applied to every realised cell.</summary>
        public bool IsRowSelected { get; set; }

        /// <summary>Column index of the current cell, or -1 when this row does not own it.</summary>
        public int CurrentColumnIndex { get; set; } = -1;

        /// <summary>In cell mode only the current cell paints as selected.</summary>
        public bool IsCellSelectionUnit { get; set; }

        /// <summary>
        /// Supplies sort/filter indicator state for a header cell. Set by the grid so
        /// the row control does not need to know about the sort or filter controllers.
        /// </summary>
        public Action<TreeGridHeaderCell> HeaderIndicatorResolver { get; set; }

        /// <summary>Row index in the flat view. Needed to resolve merge ranges.</summary>
        public int RowIndex { get; set; } = -1;

        /// <summary>Resolves how a cell participates in a vertical merge.</summary>
        public Func<int, int, TreeGridColumn, MergeRenderInfo> MergeResolver { get; set; }

        /// <summary>Supplies the validation error for a cell, or null.</summary>
        public Func<TreeNode, TreeGridColumn, string> ErrorResolver { get; set; }

        /// <summary>Draws vertical separators at the frozen-pane boundaries.</summary>
        public Brush FrozenLineBrush { get; set; }

        public double RowHeight { get; set; } = 26d;

        /// <summary>
        /// Opaque brush painted behind merged cells. Without one, the alternating
        /// stripes of the rows underneath show through the span.
        /// </summary>
        public Brush MergedCellBackground { get; set; }

        /// <summary>
        /// Mirrors cell positions for right-to-left cultures. Only the arrange pass is
        /// mirrored; FlowDirection on the cells themselves handles text direction, so
        /// content is not double-flipped.
        /// </summary>
        public bool IsRightToLeft { get; set; }

        private readonly Dictionary<int, int> _mergeSpans = new Dictionary<int, int>();
        private readonly HashSet<int> _coveredColumns = new HashSet<int>();

        /// <summary>Re-targets a pooled row onto a different node.</summary>
        public void BindNode(TreeNode node)
        {
            Node = node;
            RefreshCells();
        }

        /// <summary>Recomputes which cells are realised and rebinds them.</summary>
        public void RefreshCells()
        {
            if (Layout == null)
                return;

            _scratchIndices.Clear();
            _scratchSet.Clear();

            var visibleCount = Layout.VisibleColumns.Count;

            for (var i = 0; i < Layout.FrozenColumnCount && i < visibleCount; i++)
                AddScratch(i);

            if (EnableColumnVirtualization)
            {
                var (first, last) = Layout.GetVisibleScrollableRange(HorizontalOffset, ViewportWidth);
                if (first >= 0)
                {
                    for (var i = first; i <= last; i++)
                        AddScratch(i);
                }
            }
            else
            {
                for (var i = Layout.FrozenColumnCount; i < visibleCount - Layout.FooterColumnCount; i++)
                    AddScratch(i);
            }

            for (var i = Math.Max(0, visibleCount - Layout.FooterColumnCount); i < visibleCount; i++)
                AddScratch(i);

            // Recycle everything that fell out of range.
            _staleIndices.Clear();

            foreach (var kvp in _realized)
            {
                if (!_scratchSet.Contains(kvp.Key))
                    _staleIndices.Add(kvp.Key);
            }

            for (var i = 0; i < _staleIndices.Count; i++)
            {
                var index = _staleIndices[i];
                Recycle(_realized[index]);
                _realized.Remove(index);
            }

            ResolveMergeState();

            foreach (var index in _scratchIndices)
            {
                var column = Layout.ColumnAt(index);
                if (column == null)
                    continue;

                // Covered cells are drawn by the row that owns the merge.
                if (_coveredColumns.Contains(index))
                {
                    if (_realized.TryGetValue(index, out var covered))
                    {
                        Recycle(covered);
                        _realized.Remove(index);
                    }

                    continue;
                }

                if (!_realized.TryGetValue(index, out var element))
                {
                    element = Rent(index);
                    _realized[index] = element;
                    Children.Add(element);
                }

                if (RowType == TreeGridRowType.Header)
                {
                    var header = (TreeGridHeaderCell)element;
                    header.Bind(column);
                    HeaderIndicatorResolver?.Invoke(header);
                }
                else if (Node != null)
                {
                    var cell = (TreeGridCell)element;
                    cell.IsTreeColumn = index == TreeColumnIndex;
                    cell.Bind(Node, column, index, IndentPerLevel, IndentBase, ShowNodeCheckBox);

                    var span = _mergeSpans.TryGetValue(index, out var resolved) ? resolved : 1;

                    cell.MergeRowSpan = span;
                    cell.IsMergedCell = span > 1;
                    cell.Background = span > 1
                        ? MergedCellBackground ?? Background
                        : Brushes.Transparent;

                    ApplyCellState(cell, index);
                }
            }

            // Rows are siblings in a Panel, so paint order follows the Children
            // collection - which recycling shuffles. A merged cell overflows into the
            // rows below it, so its row must be lifted above them explicitly or the
            // covered rows' opaque backgrounds paint straight over it.
            Panel.SetZIndex(this, _mergeSpans.Count > 0 ? 1 : 0);

            InvalidateMeasure();

            // The frozen-pane separator is drawn in OnRender against the current
            // FrozenWidth. Resizing a frozen column moves that boundary, and without
            // this the old line stayed painted down the full height of the grid.
            InvalidateVisual();
        }

        private void AddScratch(int index)
        {
            if (_scratchSet.Add(index))
                _scratchIndices.Add(index);
        }

        private FrameworkElement Rent(int columnIndex)
        {
            if (RowType == TreeGridRowType.Header)
                return _headerPool.Count > 0 ? _headerPool.Pop() : new TreeGridHeaderCell();

            return _cellPool.Count > 0 ? _cellPool.Pop() : new TreeGridCell();
        }

        private void Recycle(FrameworkElement element)
        {
            Children.Remove(element);

            switch (element)
            {
                case TreeGridHeaderCell header:
                    _headerPool.Push(header);
                    break;
                case TreeGridCell cell:
                    cell.Node = null;
                    _cellPool.Push(cell);
                    break;
            }
        }

        /// <summary>
        /// Repaints selection and current-cell state without re-reading bound values.
        /// Selection changes fire far more often than data changes, and this keeps a
        /// arrow-key repeat from turning into a full rebind of every visible cell.
        /// </summary>
        public void RefreshState()
        {
            foreach (var kvp in _realized)
            {
                if (kvp.Value is TreeGridCell cell)
                    ApplyCellState(cell, kvp.Key);
            }
        }

        /// <summary>Repaints header sort and filter glyphs without rebinding columns.</summary>
        public void RefreshHeaderIndicators()
        {
            if (RowType != TreeGridRowType.Header || HeaderIndicatorResolver == null)
                return;

            foreach (var kvp in _realized)
            {
                if (kvp.Value is TreeGridHeaderCell header)
                    HeaderIndicatorResolver(header);
            }
        }

        private void ApplyCellState(TreeGridCell cell, int columnIndex)
        {
            var isCurrent = columnIndex == CurrentColumnIndex;
            var isSelected = IsCellSelectionUnit
                ? IsRowSelected && isCurrent
                : IsRowSelected;

            var error = ErrorResolver != null && cell.Column != null
                ? ErrorResolver(Node, cell.Column)
                : null;

            cell.RefreshState(isSelected, isCurrent, error);
        }

        private void ResolveMergeState()
        {
            _mergeSpans.Clear();
            _coveredColumns.Clear();

            if (MergeResolver == null || RowType != TreeGridRowType.Record || RowIndex < 0)
                return;

            foreach (var index in _scratchIndices)
            {
                var column = Layout.ColumnAt(index);
                if (column == null)
                    continue;

                var info = MergeResolver(RowIndex, index, column);

                if (info.IsCovered)
                    _coveredColumns.Add(index);
                else if (info.IsMergeOrigin && info.RowSpan > 1)
                    _mergeSpans[index] = info.RowSpan;
            }
        }

        /// <summary>Returns the cell hosting a column, or null when it is not realised.</summary>
        public TreeGridCell GetCell(int columnIndex) =>
            _realized.TryGetValue(columnIndex, out var element) ? element as TreeGridCell : null;

        protected override Size MeasureOverride(Size availableSize)
        {
            if (Layout == null)
                return new Size(0, 0);

            var height = double.IsInfinity(availableSize.Height) ? 24 : availableSize.Height;

            foreach (var kvp in _realized)
            {
                var column = Layout.ColumnAt(kvp.Key);
                if (column == null)
                    continue;

                var needsAutoFit = column.ColumnSizer != ColumnSizerMode.None &&
                                   column.ColumnSizer != ColumnSizerMode.Star;

                if (needsAutoFit)
                {
                    kvp.Value.Measure(new Size(double.PositiveInfinity, height));
                    var desired = kvp.Value.DesiredSize.Width;

                    if (!Layout.AutoFitWidths.TryGetValue(column, out var current) || desired > current)
                        Layout.AutoFitWidths[column] = desired;
                }

                var cellHeight = height;

                if (_mergeSpans.TryGetValue(kvp.Key, out var span) && span > 1)
                    cellHeight = span * (RowHeight > 0 ? RowHeight : height);

                kvp.Value.Measure(new Size(column.ActualWidth, cellHeight));
            }

            return new Size(ViewportWidth > 0 ? ViewportWidth : Layout.TotalWidth, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (Layout == null)
                return finalSize;

            var visibleCount = Layout.VisibleColumns.Count;
            var footerStart = visibleCount - Layout.FooterColumnCount;
            var bandLeft = Layout.FrozenWidth;
            var bandRight = Math.Max(bandLeft, finalSize.Width - Layout.FooterWidth);

            foreach (var kvp in _realized)
            {
                var index = kvp.Key;
                var column = Layout.ColumnAt(index);
                if (column == null)
                    continue;

                var element = kvp.Value;
                double x;

                if (index < Layout.FrozenColumnCount)
                {
                    x = column.LeftOffset;
                    element.Clip = null;
                }
                else if (index >= footerStart)
                {
                    var offsetInFooter = column.LeftOffset - (Layout.TotalWidth - Layout.FooterWidth);
                    x = finalSize.Width - Layout.FooterWidth + offsetInFooter;
                    element.Clip = null;
                }
                else
                {
                    x = column.LeftOffset - HorizontalOffset;

                    // Clip the scrollable band so cells never bleed under frozen columns.
                    var visibleLeft = Math.Max(0, bandLeft - x);
                    var visibleRight = Math.Min(column.ActualWidth, bandRight - x);

                    if (visibleRight <= visibleLeft)
                    {
                        element.Arrange(new Rect(0, 0, 0, 0));
                        continue;
                    }

                    element.Clip = new RectangleGeometry(
                        new Rect(visibleLeft, 0, visibleRight - visibleLeft, finalSize.Height));
                }

                var height = finalSize.Height;

                // A merged cell is drawn by its top row and overflows downward. Rows do
                // not clip, so the taller cell paints over the rows it covers.
                if (_mergeSpans.TryGetValue(index, out var span) && span > 1)
                    height = span * (RowHeight > 0 ? RowHeight : finalSize.Height);

                if (IsRightToLeft)
                    x = finalSize.Width - x - column.ActualWidth;

                element.Arrange(new Rect(x, 0, column.ActualWidth, height));
            }

            return finalSize;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (GridLineBrush == null)
                return;

            var pen = new Pen(GridLineBrush, 1);
            pen.Freeze();

            var y = Math.Round(ActualHeight) - 0.5;
            dc.DrawLine(pen, new Point(0, y), new Point(ActualWidth, y));

            if (FrozenLineBrush == null || Layout == null)
                return;

            var frozenPen = new Pen(FrozenLineBrush, 1);
            frozenPen.Freeze();

            if (Layout.FrozenColumnCount > 0 && Layout.FrozenWidth > 0 &&
                Layout.FrozenWidth < ActualWidth)
            {
                var x = Math.Round(Layout.FrozenWidth) - 0.5;
                dc.DrawLine(frozenPen, new Point(x, 0), new Point(x, ActualHeight));
            }

            if (Layout.FooterColumnCount > 0 && Layout.FooterWidth > 0 &&
                Layout.FooterWidth < ActualWidth)
            {
                var x = Math.Round(ActualWidth - Layout.FooterWidth) + 0.5;
                dc.DrawLine(frozenPen, new Point(x, 0), new Point(x, ActualHeight));
            }
        }
    }
}
