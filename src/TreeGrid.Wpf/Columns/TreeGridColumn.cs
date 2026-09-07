using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TreeGrid.Wpf.Columns
{
    public enum ColumnSizerMode
    {
        /// <summary>Use the explicit Width, or DefaultColumnWidth when unset.</summary>
        None,

        /// <summary>Fit the widest cell in the column.</summary>
        SizeToCells,

        /// <summary>Fit the header text.</summary>
        SizeToHeader,

        /// <summary>Fit the wider of header and cells.</summary>
        AllCells,

        /// <summary>Share leftover viewport width proportionally.</summary>
        Star,

        /// <summary>Auto-fit all, then stretch the last column to fill the viewport.</summary>
        LastColumnFill
    }

    public abstract class TreeGridColumn : DependencyObject
    {
        public static readonly DependencyProperty MappingNameProperty = DependencyProperty.Register(
            nameof(MappingName), typeof(string), typeof(TreeGridColumn), new PropertyMetadata(null));

        public static readonly DependencyProperty HeaderTextProperty = DependencyProperty.Register(
            nameof(HeaderText), typeof(string), typeof(TreeGridColumn),
            new PropertyMetadata(null, OnLayoutPropertyChanged));

        public static readonly DependencyProperty WidthProperty = DependencyProperty.Register(
            nameof(Width), typeof(double), typeof(TreeGridColumn),
            new PropertyMetadata(double.NaN, OnLayoutPropertyChanged));

        public static readonly DependencyProperty MinimumWidthProperty = DependencyProperty.Register(
            nameof(MinimumWidth), typeof(double), typeof(TreeGridColumn), new PropertyMetadata(20d));

        public static readonly DependencyProperty MaximumWidthProperty = DependencyProperty.Register(
            nameof(MaximumWidth), typeof(double), typeof(TreeGridColumn), new PropertyMetadata(double.PositiveInfinity));

        public static readonly DependencyProperty IsHiddenProperty = DependencyProperty.Register(
            nameof(IsHidden), typeof(bool), typeof(TreeGridColumn),
            new PropertyMetadata(false, OnLayoutPropertyChanged));

        public static readonly DependencyProperty AllowSortingProperty = DependencyProperty.Register(
            nameof(AllowSorting), typeof(bool), typeof(TreeGridColumn), new PropertyMetadata(true));

        public static readonly DependencyProperty AllowFilteringProperty = DependencyProperty.Register(
            nameof(AllowFiltering), typeof(bool), typeof(TreeGridColumn), new PropertyMetadata(true));

        public static readonly DependencyProperty AllowEditingProperty = DependencyProperty.Register(
            nameof(AllowEditing), typeof(bool), typeof(TreeGridColumn), new PropertyMetadata(true));

        public static readonly DependencyProperty AllowResizingProperty = DependencyProperty.Register(
            nameof(AllowResizing), typeof(bool), typeof(TreeGridColumn), new PropertyMetadata(true));

        public static readonly DependencyProperty AllowGroupingProperty = DependencyProperty.Register(
            nameof(AllowGrouping), typeof(bool), typeof(TreeGridColumn), new PropertyMetadata(true));

        public static readonly DependencyProperty TextAlignmentProperty = DependencyProperty.Register(
            nameof(TextAlignment), typeof(TextAlignment), typeof(TreeGridColumn),
            new PropertyMetadata(TextAlignment.Left));

        public static readonly DependencyProperty SortMemberPathProperty = DependencyProperty.Register(
            nameof(SortMemberPath), typeof(string), typeof(TreeGridColumn), new PropertyMetadata(null));

        public static readonly DependencyProperty HeaderTemplateProperty = DependencyProperty.Register(
            nameof(HeaderTemplate), typeof(DataTemplate), typeof(TreeGridColumn),
            new PropertyMetadata(null, OnLayoutPropertyChanged));

        public static readonly DependencyProperty DisplayFormatProperty = DependencyProperty.Register(
            nameof(DisplayFormat), typeof(string), typeof(TreeGridColumn), new PropertyMetadata(null));

        public static readonly DependencyProperty ColumnSizerProperty = DependencyProperty.Register(
            nameof(ColumnSizer), typeof(ColumnSizerMode), typeof(TreeGridColumn),
            new PropertyMetadata(ColumnSizerMode.None, OnLayoutPropertyChanged));

        public static readonly DependencyProperty CellForegroundProperty = DependencyProperty.Register(
            nameof(CellForeground), typeof(Brush), typeof(TreeGridColumn), new PropertyMetadata(null));

        public static readonly DependencyProperty PaddingProperty = DependencyProperty.Register(
            nameof(Padding), typeof(Thickness), typeof(TreeGridColumn),
            new PropertyMetadata(new Thickness(6, 0, 6, 0)));

        public string MappingName
        {
            get => (string)GetValue(MappingNameProperty);
            set => SetValue(MappingNameProperty, value);
        }

        public string HeaderText
        {
            get => (string)GetValue(HeaderTextProperty);
            set => SetValue(HeaderTextProperty, value);
        }

        public double Width
        {
            get => (double)GetValue(WidthProperty);
            set => SetValue(WidthProperty, value);
        }

        public double MinimumWidth
        {
            get => (double)GetValue(MinimumWidthProperty);
            set => SetValue(MinimumWidthProperty, value);
        }

        public double MaximumWidth
        {
            get => (double)GetValue(MaximumWidthProperty);
            set => SetValue(MaximumWidthProperty, value);
        }

        public bool IsHidden
        {
            get => (bool)GetValue(IsHiddenProperty);
            set => SetValue(IsHiddenProperty, value);
        }

        public bool AllowSorting
        {
            get => (bool)GetValue(AllowSortingProperty);
            set => SetValue(AllowSortingProperty, value);
        }

        public bool AllowFiltering
        {
            get => (bool)GetValue(AllowFilteringProperty);
            set => SetValue(AllowFilteringProperty, value);
        }

        public bool AllowEditing
        {
            get => (bool)GetValue(AllowEditingProperty);
            set => SetValue(AllowEditingProperty, value);
        }

        public bool AllowResizing
        {
            get => (bool)GetValue(AllowResizingProperty);
            set => SetValue(AllowResizingProperty, value);
        }

        /// <summary>Set false to stop this column being dropped on the group panel.</summary>
        public bool AllowGrouping
        {
            get => (bool)GetValue(AllowGroupingProperty);
            set => SetValue(AllowGroupingProperty, value);
        }

        public TextAlignment TextAlignment
        {
            get => (TextAlignment)GetValue(TextAlignmentProperty);
            set => SetValue(TextAlignmentProperty, value);
        }

        /// <summary>
        /// Property used when sorting this column, when it should differ from what is
        /// displayed. Falls back to <see cref="MappingName"/>.
        /// <para>
        /// Useful when the displayed value does not sort sensibly - a formatted status
        /// string, or a name column that should order by a sort key.
        /// </para>
        /// </summary>
        public string SortMemberPath
        {
            get => (string)GetValue(SortMemberPathProperty);
            set => SetValue(SortMemberPathProperty, value);
        }

        /// <summary>Custom header content. The template's DataContext is the column.</summary>
        public DataTemplate HeaderTemplate
        {
            get => (DataTemplate)GetValue(HeaderTemplateProperty);
            set => SetValue(HeaderTemplateProperty, value);
        }

        public string DisplayFormat
        {
            get => (string)GetValue(DisplayFormatProperty);
            set => SetValue(DisplayFormatProperty, value);
        }

        public ColumnSizerMode ColumnSizer
        {
            get => (ColumnSizerMode)GetValue(ColumnSizerProperty);
            set => SetValue(ColumnSizerProperty, value);
        }

        public Brush CellForeground
        {
            get => (Brush)GetValue(CellForegroundProperty);
            set => SetValue(CellForegroundProperty, value);
        }

        public Thickness Padding
        {
            get => (Thickness)GetValue(PaddingProperty);
            set => SetValue(PaddingProperty, value);
        }

        /// <summary>
        /// The grid this column belongs to. Set when the column enters a
        /// <see cref="TreeGridColumns"/> collection, and needed because a column is a
        /// plain DependencyObject with no place in any visual tree - without this
        /// back-reference, changing IsHidden could not trigger a relayout.
        /// </summary>
        internal ITreeGridColumnHost Host { get; set; }

        private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            ((TreeGridColumn)d).Host?.OnColumnLayoutChanged((TreeGridColumn)d);

        /// <summary>Resolved width in device pixels, written by the layout pass.</summary>
        public double ActualWidth { get; internal set; }

        /// <summary>Left edge relative to the full column strip, written by the layout pass.</summary>
        public double LeftOffset { get; internal set; }

        internal int DisplayIndex { get; set; }

        /// <summary>Header text falls back to the mapping name, as most grids do.</summary>
        public string ResolvedHeaderText => string.IsNullOrEmpty(HeaderText) ? MappingName : HeaderText;

        /// <summary>The property sorting actually reads.</summary>
        public string ResolvedSortMemberPath =>
            string.IsNullOrEmpty(SortMemberPath) ? MappingName : SortMemberPath;

        // ------------------------------------------------------- editor support

        /// <summary>
        /// True when the column renders through a custom element rather than the
        /// built-in text block. Template, progress and hyperlink columns override this.
        /// </summary>
        public virtual bool HasCustomDisplay => false;

        /// <summary>
        /// False for columns that edit through their own bindings (template columns),
        /// where there is no scalar value for the grid to write back.
        /// </summary>
        public virtual bool SupportsValueCommit => true;

        /// <summary>Creates the element used to render a cell when HasCustomDisplay is true.</summary>
        public virtual FrameworkElement CreateDisplayElement() => null;

        /// <summary>Re-targets a pooled display element onto a new value.</summary>
        public virtual void PrepareDisplayElement(FrameworkElement element, object value, object dataItem)
        {
        }

        /// <summary>Creates the editor. Defaults to a borderless text box.</summary>
        public virtual FrameworkElement CreateEditElement() => new TextBox
        {
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 0, 6, 0),
            VerticalContentAlignment = VerticalAlignment.Center
        };

        public virtual void PrepareEditElement(FrameworkElement element, object value)
        {
            if (!(element is TextBox box))
                return;

            box.Text = value?.ToString() ?? string.Empty;
            box.SelectAll();
        }

        /// <summary>
        /// Overload that also receives the record. Template columns edit through their
        /// own bindings and need the data item, not just the cell value; the two-argument
        /// form remains for column types that only care about the value.
        /// </summary>
        public virtual void PrepareEditElement(FrameworkElement element, object value, object dataItem) =>
            PrepareEditElement(element, value);

        public virtual object GetEditValue(FrameworkElement element) =>
            element is TextBox box ? box.Text : null;

        /// <summary>Formats a raw value for display. Overridden by typed columns.</summary>
        public virtual string FormatValue(object value)
        {
            if (value == null)
                return string.Empty;

            if (!string.IsNullOrEmpty(DisplayFormat))
                return string.Format(DisplayFormat, value);

            return value.ToString();
        }
    }

    /// <summary>Plain text column. Further column types arrive in Phase 4.</summary>
    public class TreeGridTextColumn : TreeGridColumn
    {
    }

    /// <summary>Implemented by the grid so columns can request a relayout.</summary>
    public interface ITreeGridColumnHost
    {
        void OnColumnLayoutChanged(TreeGridColumn column);
    }

    public sealed class TreeGridColumns : ObservableCollection<TreeGridColumn>
    {
    }
}
