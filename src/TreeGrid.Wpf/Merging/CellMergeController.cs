using System;
using System.Collections.Generic;
using TreeGrid.Wpf.Columns;
using TreeGrid.Wpf.Data;

namespace TreeGrid.Wpf.Merging
{
    /// <summary>A contiguous run of rows sharing one merged cell in a single column.</summary>
    public readonly struct CoveredCellInfo
    {
        public CoveredCellInfo(int firstRow, int lastRow, int columnIndex)
        {
            FirstRow = firstRow;
            LastRow = lastRow;
            ColumnIndex = columnIndex;
        }

        public int FirstRow { get; }

        public int LastRow { get; }

        public int ColumnIndex { get; }

        public int RowSpan => LastRow - FirstRow + 1;

        public bool IsValid => LastRow >= FirstRow && ColumnIndex >= 0;

        public static CoveredCellInfo Empty => new CoveredCellInfo(-1, -1, -1);
    }

    public sealed class QueryCoveredRangeEventArgs : EventArgs
    {
        public QueryCoveredRangeEventArgs(TreeNode node, int rowIndex, int columnIndex, TreeGridColumn column)
        {
            Node = node;
            RowIndex = rowIndex;
            ColumnIndex = columnIndex;
            Column = column;
            Range = CoveredCellInfo.Empty;
        }

        public TreeNode Node { get; }

        public int RowIndex { get; }

        public int ColumnIndex { get; }

        public TreeGridColumn Column { get; }

        /// <summary>Set this and Handled to define a custom merge range.</summary>
        public CoveredCellInfo Range { get; set; }

        public bool Handled { get; set; }
    }

    /// <summary>
    /// Computes vertical merge ranges over the flattened view.
    /// <para>
    /// The default rule merges adjacent rows that share both the same parent and the
    /// same cell value. Restricting to siblings matters: two unrelated rows that
    /// happen to hold "Manager" should not be welded together just because they landed
    /// next to each other after a sort.
    /// </para>
    /// </summary>
    public sealed class CellMergeController
    {
        private readonly Dictionary<(int, int), CoveredCellInfo> _cache
            = new Dictionary<(int, int), CoveredCellInfo>();

        public bool IsEnabled { get; set; }

        /// <summary>When false, merging ignores the parent check and merges by value alone.</summary>
        public bool MergeOnlyWithinSiblings { get; set; } = true;

        public event EventHandler<QueryCoveredRangeEventArgs> QueryCoveredRange;

        /// <summary>Drops cached ranges. Call after any sort, filter or structural change.</summary>
        public void Invalidate() => _cache.Clear();

        /// <summary>
        /// Returns the merge range covering a cell, or an invalid range when the cell
        /// stands alone.
        /// </summary>
        public CoveredCellInfo GetRange(FlatTreeView view, int rowIndex, int columnIndex, TreeGridColumn column)
        {
            if (!IsEnabled || view == null || column == null || rowIndex < 0 || rowIndex >= view.Count)
                return CoveredCellInfo.Empty;

            var key = (rowIndex, columnIndex);
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var node = view[rowIndex];

            if (QueryCoveredRange != null)
            {
                var args = new QueryCoveredRangeEventArgs(node, rowIndex, columnIndex, column);
                QueryCoveredRange(this, args);

                if (args.Handled)
                {
                    CacheRange(args.Range);
                    return args.Range;
                }
            }

            if (string.IsNullOrEmpty(column.MappingName))
                return CoveredCellInfo.Empty;

            var value = PropertyAccessor.GetValue(node.Item, column.MappingName);

            var first = rowIndex;
            while (first - 1 >= 0 && IsMergeable(view[first - 1], node, value, column))
                first--;

            var last = rowIndex;
            while (last + 1 < view.Count && IsMergeable(view[last + 1], node, value, column))
                last++;

            var range = first == last
                ? CoveredCellInfo.Empty
                : new CoveredCellInfo(first, last, columnIndex);

            if (range.IsValid)
                CacheRange(range);
            else
                _cache[key] = range;

            return range;
        }

        private void CacheRange(CoveredCellInfo range)
        {
            if (!range.IsValid)
                return;

            for (var i = range.FirstRow; i <= range.LastRow; i++)
                _cache[(i, range.ColumnIndex)] = range;
        }

        private bool IsMergeable(TreeNode candidate, TreeNode anchor, object value, TreeGridColumn column)
        {
            if (candidate == null)
                return false;

            if (MergeOnlyWithinSiblings && !ReferenceEquals(candidate.ParentNode, anchor.ParentNode))
                return false;

            var other = PropertyAccessor.GetValue(candidate.Item, column.MappingName);
            return Equals(value, other);
        }

        /// <summary>
        /// Resolves what a given row should render for a column.
        /// <para>
        /// A span can start above the viewport, in which case its owning row was never
        /// realised. Rather than leave a gap, the range is clamped to the first visible
        /// row, so a merged cell scrolled halfway off the top still shows its label —
        /// the same behaviour a spreadsheet gives you.
        /// </para>
        /// </summary>
        public MergeRenderInfo Resolve(FlatTreeView view, int rowIndex, int columnIndex,
            TreeGridColumn column, int firstVisibleRow)
        {
            var range = GetRange(view, rowIndex, columnIndex, column);

            if (!range.IsValid)
                return MergeRenderInfo.Normal;

            var effectiveFirst = Math.Max(range.FirstRow, firstVisibleRow);

            if (rowIndex != effectiveFirst)
                return MergeRenderInfo.Covered;

            return new MergeRenderInfo(true, false, range.LastRow - effectiveFirst + 1);
        }
    }

    /// <summary>How one cell participates in a merge during a layout pass.</summary>
    public readonly struct MergeRenderInfo
    {
        public MergeRenderInfo(bool isMergeOrigin, bool isCovered, int rowSpan)
        {
            IsMergeOrigin = isMergeOrigin;
            IsCovered = isCovered;
            RowSpan = rowSpan;
        }

        /// <summary>This row draws the merged cell, spanning <see cref="RowSpan"/> rows.</summary>
        public bool IsMergeOrigin { get; }

        /// <summary>Another row draws this cell; render nothing here.</summary>
        public bool IsCovered { get; }

        public int RowSpan { get; }

        public static MergeRenderInfo Normal => new MergeRenderInfo(false, false, 1);

        public static MergeRenderInfo Covered => new MergeRenderInfo(false, true, 0);
    }
}
