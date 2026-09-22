using System;
using System.Windows;
using System.Windows.Media;
using TreeGrid.Wpf.Columns;
using TreeGrid.Wpf.Data;

namespace TreeGrid.Wpf.Styling
{
    [Flags]
    public enum GridLinesVisibility
    {
        None = 0,
        Horizontal = 1,
        Vertical = 2,
        Both = Horizontal | Vertical
    }

    /// <summary>
    /// The resolved appearance of one grid, pushed down to rows and cells on each
    /// layout pass.
    /// <para>
    /// This exists so per-instance styling does not have to be threaded through the
    /// view layer as twenty separate fields. The control resolves every value once -
    /// taking the explicit property if set, otherwise the current theme resource - so
    /// downstream code never has to ask "is this null, and what do I fall back to?".
    /// </para>
    /// </summary>
    public sealed class TreeGridVisualStyle
    {
        // ------------------------------------------------------------- header
        public Brush HeaderBackground { get; set; }
        public Brush HeaderForeground { get; set; }
        public Brush HeaderBorderBrush { get; set; }
        public FontFamily HeaderFontFamily { get; set; }
        public double HeaderFontSize { get; set; } = 12;
        public FontWeight HeaderFontWeight { get; set; } = FontWeights.SemiBold;

        // --------------------------------------------------------------- rows
        public Brush RowBackground { get; set; }
        public Brush AlternatingRowBackground { get; set; }
        public Brush SelectedRowBackground { get; set; }
        public Brush SelectedRowForeground { get; set; }
        public Brush HoverRowBackground { get; set; }
        public bool ShowAlternatingRows { get; set; }

        // -------------------------------------------------------------- cells
        public Brush CellForeground { get; set; }
        public FontFamily CellFontFamily { get; set; }
        public double CellFontSize { get; set; } = 12;
        public FontWeight CellFontWeight { get; set; } = FontWeights.Normal;
        public Thickness CellPadding { get; set; } = new Thickness(6, 0, 6, 0);

        // -------------------------------------------------------- header icons
        public Brush SortIconBrush { get; set; }
        public Brush FilterIconBrush { get; set; }
        public Brush FilterIconActiveBrush { get; set; }
        public Brush SortBadgeBackground { get; set; }
        public Brush SortBadgeForeground { get; set; }

        // -------------------------------------------------------- filter popup
        public Brush FilterPopupBackground { get; set; }
        public Brush FilterPopupForeground { get; set; }
        public Brush FilterPopupBorderBrush { get; set; }
        public Brush FilterPopupAccentBrush { get; set; }
        public Brush FilterListBackground { get; set; }
        public Brush FilterListForeground { get; set; }
        public Brush FilterListBorderBrush { get; set; }
        public Brush FilterItemHoverBackground { get; set; }
        public Brush FilterItemSelectedBackground { get; set; }
        public Brush FilterInputBackground { get; set; }
        public Brush FilterInputForeground { get; set; }
        public Brush FilterInputBorderBrush { get; set; }
        public double FilterPopupWidth { get; set; } = 270;
        public double FilterListMaxHeight { get; set; } = 180;

        // ------------------------------------------------------------- chrome
        public Brush GridLineBrush { get; set; }
        public GridLinesVisibility GridLinesVisibility { get; set; } = GridLinesVisibility.Both;
        public Brush CurrentCellBorderBrush { get; set; }
        public Thickness CurrentCellBorderThickness { get; set; } = new Thickness(1);
        public Brush ErrorBrush { get; set; }
        public Brush EditorBackground { get; set; }
        public Brush ExpanderGlyphBrush { get; set; }
        public Brush FrozenLineBrush { get; set; }
        public Brush MergedCellBackground { get; set; }

        public bool ShowHorizontalGridLines =>
            (GridLinesVisibility & GridLinesVisibility.Horizontal) == GridLinesVisibility.Horizontal;

        public bool ShowVerticalGridLines =>
            (GridLinesVisibility & GridLinesVisibility.Vertical) == GridLinesVisibility.Vertical;
    }

    /// <summary>
    /// Raised per realised row so the host can colour rows conditionally. Leaving a
    /// property null keeps the grid's own value, so a handler only sets what it wants
    /// to override.
    /// </summary>
    public sealed class QueryRowStyleEventArgs : EventArgs
    {
        public QueryRowStyleEventArgs(TreeNode node, int rowIndex)
        {
            Node = node;
            RowIndex = rowIndex;
        }

        public TreeNode Node { get; }

        public object Record => Node?.Item;

        public int RowIndex { get; }

        public Brush Background { get; set; }

        public Brush Foreground { get; set; }

        public FontWeight? FontWeight { get; set; }

        public bool HasOverrides =>
            Background != null || Foreground != null || FontWeight.HasValue;
    }

    /// <summary>Per-cell equivalent of <see cref="QueryRowStyleEventArgs"/>.</summary>
    public sealed class QueryCellStyleEventArgs : EventArgs
    {
        public QueryCellStyleEventArgs(TreeNode node, TreeGridColumn column, int rowIndex, int columnIndex)
        {
            Node = node;
            Column = column;
            RowIndex = rowIndex;
            ColumnIndex = columnIndex;
        }

        public TreeNode Node { get; }

        public object Record => Node?.Item;

        public TreeGridColumn Column { get; }

        public int RowIndex { get; }

        public int ColumnIndex { get; }

        public Brush Background { get; set; }

        public Brush Foreground { get; set; }

        public FontWeight? FontWeight { get; set; }

        public bool HasOverrides =>
            Background != null || Foreground != null || FontWeight.HasValue;
    }
}
