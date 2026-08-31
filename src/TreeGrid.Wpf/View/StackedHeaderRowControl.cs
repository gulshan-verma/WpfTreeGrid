using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TreeGrid.Wpf.Columns;
using TreeGrid.Wpf.Headers;
using TreeGrid.Wpf.Styling;

namespace TreeGrid.Wpf.View
{
    /// <summary>
    /// Renders one stacked header row. Spans are resolved against the live column
    /// layout on every pass, so a stacked header follows its children when columns are
    /// resized or reordered.
    /// <para>
    /// A span whose children are no longer adjacent is drawn across the full range it
    /// covers, which is the least surprising outcome — the alternative is silently
    /// dropping the header.
    /// </para>
    /// </summary>
    public class StackedHeaderRowControl : Panel
    {
        private readonly List<TreeGridHeaderCell> _cells = new List<TreeGridHeaderCell>();
        private readonly List<StackedHeaderSpan> _spans = new List<StackedHeaderSpan>();

        public ColumnLayout Layout { get; set; }

        public StackedHeaderRow HeaderRow { get; set; }

        public double HorizontalOffset { get; set; }

        public double ViewportWidth { get; set; }

        public Brush GridLineBrush { get; set; }

        public TreeGridVisualStyle VisualStyle { get; set; }

        public void Refresh()
        {
            if (Layout == null || HeaderRow == null)
                return;

            ResolveSpans();

            while (_cells.Count < _spans.Count)
            {
                var cell = new TreeGridHeaderCell { CanResize = false };
                _cells.Add(cell);
                Children.Add(cell);
            }

            while (_cells.Count > _spans.Count)
            {
                var last = _cells.Count - 1;
                Children.Remove(_cells[last]);
                _cells.RemoveAt(last);
            }

            for (var i = 0; i < _spans.Count; i++)
            {
                _cells[i].HeaderText = _spans[i].HeaderText;
                _cells[i].CellTextAlignment = TextAlignment.Center;

                if (VisualStyle != null)
                {
                    _cells[i].Background = VisualStyle.HeaderBackground;
                    _cells[i].Foreground = VisualStyle.HeaderForeground;
                    _cells[i].BorderBrush = VisualStyle.HeaderBorderBrush;
                    _cells[i].FontSize = VisualStyle.HeaderFontSize;
                    _cells[i].FontWeight = VisualStyle.HeaderFontWeight;
                }
                _cells[i].ShowFilterButton = false;
                _cells[i].SortDirection = ListSortDirectionOrNone.None;
            }

            InvalidateMeasure();
        }

        private void ResolveSpans()
        {
            _spans.Clear();

            var columns = Layout.VisibleColumns;

            foreach (var stacked in HeaderRow.StackedColumns)
            {
                var names = stacked.GetChildMappingNames();
                if (names.Count == 0)
                    continue;

                // A set rather than a list scan: this runs per column, per stacked
                // header, on every layout pass.
                var nameSet = new HashSet<string>(names, StringComparer.Ordinal);

                var first = int.MaxValue;
                var last = -1;

                for (var i = 0; i < columns.Count; i++)
                {
                    if (columns[i].MappingName == null || !nameSet.Contains(columns[i].MappingName))
                        continue;

                    if (i < first) first = i;
                    if (i > last) last = i;
                }

                if (last < 0)
                    continue;

                var span = new StackedHeaderSpan(stacked.HeaderText, first, last);

                var firstColumn = columns[first];
                var lastColumn = columns[last];

                span.Left = firstColumn.LeftOffset;
                span.Width = lastColumn.LeftOffset + lastColumn.ActualWidth - firstColumn.LeftOffset;
                span.IsFrozen = last < Layout.FrozenColumnCount;

                _spans.Add(span);
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var height = double.IsInfinity(availableSize.Height) ? 28 : availableSize.Height;

            for (var i = 0; i < _cells.Count && i < _spans.Count; i++)
                _cells[i].Measure(new Size(Math.Max(0, _spans[i].Width), height));

            return new Size(ViewportWidth > 0 ? ViewportWidth : Layout?.TotalWidth ?? 0, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            for (var i = 0; i < _cells.Count && i < _spans.Count; i++)
            {
                var span = _spans[i];

                // Frozen spans ignore the scroll offset, matching their child columns.
                var x = span.IsFrozen ? span.Left : span.Left - HorizontalOffset;

                _cells[i].Arrange(new Rect(x, 0, Math.Max(0, span.Width), finalSize.Height));
            }

            return finalSize;
        }
    }
}
