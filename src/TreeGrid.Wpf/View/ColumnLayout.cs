using System;
using System.Collections.Generic;
using TreeGrid.Wpf.Columns;

namespace TreeGrid.Wpf.View
{
    /// <summary>
    /// Single source of truth for horizontal geometry. The header row, every record
    /// row and the scroll extent all read from the same instance, which is what keeps
    /// frozen columns and resizing from drifting out of alignment.
    /// </summary>
    public sealed class ColumnLayout
    {
        private readonly List<TreeGridColumn> _visible = new List<TreeGridColumn>();

        public IReadOnlyList<TreeGridColumn> VisibleColumns => _visible;

        public double TotalWidth { get; private set; }

        /// <summary>Number of leading columns pinned to the left edge.</summary>
        public int FrozenColumnCount { get; set; }

        /// <summary>Number of trailing columns pinned to the right edge.</summary>
        public int FooterColumnCount { get; set; }

        public double FrozenWidth { get; private set; }

        public double FooterWidth { get; private set; }

        /// <summary>Width of the horizontally scrollable middle band.</summary>
        public double ScrollableWidth => Math.Max(0, TotalWidth - FrozenWidth - FooterWidth);

        public double DefaultColumnWidth { get; set; } = 120d;

        public void Recalculate(IList<TreeGridColumn> columns, double viewportWidth)
        {
            _visible.Clear();

            if (columns != null)
            {
                foreach (var column in columns)
                {
                    if (!column.IsHidden)
                        _visible.Add(column);
                }
            }

            ResolveWidths(viewportWidth);

            var offset = 0d;
            for (var i = 0; i < _visible.Count; i++)
            {
                var column = _visible[i];
                column.DisplayIndex = i;
                column.LeftOffset = offset;
                offset += column.ActualWidth;
            }

            TotalWidth = offset;

            FrozenColumnCount = Clamp(FrozenColumnCount, 0, _visible.Count);
            FooterColumnCount = Clamp(FooterColumnCount, 0, Math.Max(0, _visible.Count - FrozenColumnCount));

            FrozenWidth = 0;
            for (var i = 0; i < FrozenColumnCount; i++)
                FrozenWidth += _visible[i].ActualWidth;

            FooterWidth = 0;
            for (var i = _visible.Count - FooterColumnCount; i < _visible.Count; i++)
            {
                if (i >= 0)
                    FooterWidth += _visible[i].ActualWidth;
            }
        }

        private void ResolveWidths(double viewportWidth)
        {
            var starColumns = new List<TreeGridColumn>();
            var fixedTotal = 0d;

            foreach (var column in _visible)
            {
                if (column.ColumnSizer == ColumnSizerMode.Star)
                {
                    starColumns.Add(column);
                    continue;
                }

                var width = double.IsNaN(column.Width) ? DefaultColumnWidth : column.Width;

                // Auto-fit modes are measured by the row generator and written back to
                // AutoFitWidth; until a measure pass has happened we use the fallback.
                if (column.ColumnSizer != ColumnSizerMode.None && AutoFitWidths.TryGetValue(column, out var fit) && fit > 0)
                    width = fit;

                column.ActualWidth = Clamp(width, column.MinimumWidth, column.MaximumWidth);
                fixedTotal += column.ActualWidth;
            }

            if (starColumns.Count > 0)
            {
                var remaining = Math.Max(0, viewportWidth - fixedTotal);
                var share = remaining / starColumns.Count;

                foreach (var column in starColumns)
                    column.ActualWidth = Clamp(share, column.MinimumWidth, column.MaximumWidth);
            }
            else if (_visible.Count > 0)
            {
                var last = _visible[_visible.Count - 1];
                if (last.ColumnSizer == ColumnSizerMode.LastColumnFill && fixedTotal < viewportWidth)
                    last.ActualWidth += viewportWidth - fixedTotal;
            }
        }

        /// <summary>
        /// Measured content widths, keyed by column. Populated during the row measure
        /// pass so auto-fit does not need a second full walk of the data.
        /// </summary>
        public Dictionary<TreeGridColumn, double> AutoFitWidths { get; }
            = new Dictionary<TreeGridColumn, double>();

        /// <summary>
        /// Indices of the scrollable columns intersecting the viewport. Frozen and
        /// footer columns are always realised and are excluded from this range.
        /// </summary>
        public (int First, int Last) GetVisibleScrollableRange(double horizontalOffset, double viewportWidth)
        {
            var start = FrozenColumnCount;
            var end = _visible.Count - FooterColumnCount - 1;

            if (start > end)
                return (-1, -1);

            var bandLeft = FrozenWidth + horizontalOffset;
            var bandRight = bandLeft + Math.Max(0, viewportWidth - FrozenWidth - FooterWidth);

            var first = -1;
            var last = -1;

            for (var i = start; i <= end; i++)
            {
                var column = _visible[i];
                var left = column.LeftOffset;
                var right = left + column.ActualWidth;

                if (right <= bandLeft || left >= bandRight)
                    continue;

                if (first < 0)
                    first = i;
                last = i;
            }

            return (first, last);
        }

        public TreeGridColumn ColumnAt(int index) =>
            index >= 0 && index < _visible.Count ? _visible[index] : null;

        public int IndexOf(TreeGridColumn column) =>
            column == null ? -1 : _visible.IndexOf(column);

        /// <summary>
        /// Maps a horizontal position in row coordinates to a column index, accounting
        /// for the frozen bands. Returns -1 past the last column.
        /// </summary>
        public int ColumnIndexFromX(double x, double horizontalOffset, double viewportWidth)
        {
            if (_visible.Count == 0)
                return -1;

            // Left frozen band.
            if (x < FrozenWidth)
            {
                for (var i = 0; i < FrozenColumnCount; i++)
                {
                    var column = _visible[i];
                    if (x >= column.LeftOffset && x < column.LeftOffset + column.ActualWidth)
                        return i;
                }
            }

            // Right frozen band.
            var footerLeft = viewportWidth - FooterWidth;
            if (FooterColumnCount > 0 && x >= footerLeft)
            {
                var cursor = footerLeft;
                for (var i = _visible.Count - FooterColumnCount; i < _visible.Count; i++)
                {
                    var width = _visible[i].ActualWidth;
                    if (x >= cursor && x < cursor + width)
                        return i;
                    cursor += width;
                }
            }

            // Scrollable band.
            var target = x + horizontalOffset;
            for (var i = FrozenColumnCount; i < _visible.Count - FooterColumnCount; i++)
            {
                var column = _visible[i];
                if (target >= column.LeftOffset && target < column.LeftOffset + column.ActualWidth)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Pushes measured content widths onto the columns that asked for auto-fit.
        /// Returns true when something actually moved, which is the signal for the
        /// owner to run one more layout pass.
        /// </summary>
        public bool ApplyAutoFitWidths()
        {
            var changed = false;

            foreach (var column in _visible)
            {
                if (column.ColumnSizer == ColumnSizerMode.None || column.ColumnSizer == ColumnSizerMode.Star)
                    continue;

                if (!AutoFitWidths.TryGetValue(column, out var measured) || measured <= 0)
                    continue;

                var target = Clamp(measured, column.MinimumWidth, column.MaximumWidth);

                if (Math.Abs(target - column.ActualWidth) > 0.5)
                {
                    column.Width = target;
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>Discards measured widths so the next pass re-measures from scratch.</summary>
        public void ResetAutoFitWidths() => AutoFitWidths.Clear();

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
