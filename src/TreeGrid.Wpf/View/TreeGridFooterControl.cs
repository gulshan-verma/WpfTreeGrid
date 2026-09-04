using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TreeGrid.Wpf.View
{
    /// <summary>
    /// The status strip below the rows. Shows the record count and, when relevant, the
    /// selected, checked and group counts.
    /// <para>
    /// Counts are pushed in by the grid rather than computed here: the record count has
    /// to exclude group header rows, which only the grid can distinguish.
    /// </para>
    /// </summary>
    public class TreeGridFooterControl : Control
    {
        public static readonly DependencyProperty RecordCountProperty = DependencyProperty.Register(
            nameof(RecordCount), typeof(int), typeof(TreeGridFooterControl), new PropertyMetadata(0));

        public static readonly DependencyProperty VisibleRowCountProperty = DependencyProperty.Register(
            nameof(VisibleRowCount), typeof(int), typeof(TreeGridFooterControl), new PropertyMetadata(0));

        public static readonly DependencyProperty SelectedCountProperty = DependencyProperty.Register(
            nameof(SelectedCount), typeof(int), typeof(TreeGridFooterControl), new PropertyMetadata(0));

        public static readonly DependencyProperty CheckedCountProperty = DependencyProperty.Register(
            nameof(CheckedCount), typeof(int), typeof(TreeGridFooterControl), new PropertyMetadata(0));

        public static readonly DependencyProperty GroupCountProperty = DependencyProperty.Register(
            nameof(GroupCount), typeof(int), typeof(TreeGridFooterControl), new PropertyMetadata(0));

        public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register(
            nameof(StatusText), typeof(string), typeof(TreeGridFooterControl), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty CustomContentProperty = DependencyProperty.Register(
            nameof(CustomContent), typeof(object), typeof(TreeGridFooterControl), new PropertyMetadata(null));

        public static readonly DependencyProperty SeparatorBrushProperty = DependencyProperty.Register(
            nameof(SeparatorBrush), typeof(Brush), typeof(TreeGridFooterControl), new PropertyMetadata(null));

        static TreeGridFooterControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TreeGridFooterControl),
                new FrameworkPropertyMetadata(typeof(TreeGridFooterControl)));
        }

        /// <summary>Data records, excluding group header rows.</summary>
        public int RecordCount
        {
            get => (int)GetValue(RecordCountProperty);
            set => SetValue(RecordCountProperty, value);
        }

        /// <summary>Rows currently in the flat view, including group headers.</summary>
        public int VisibleRowCount
        {
            get => (int)GetValue(VisibleRowCountProperty);
            set => SetValue(VisibleRowCountProperty, value);
        }

        public int SelectedCount
        {
            get => (int)GetValue(SelectedCountProperty);
            set => SetValue(SelectedCountProperty, value);
        }

        public int CheckedCount
        {
            get => (int)GetValue(CheckedCountProperty);
            set => SetValue(CheckedCountProperty, value);
        }

        public int GroupCount
        {
            get => (int)GetValue(GroupCountProperty);
            set => SetValue(GroupCountProperty, value);
        }

        /// <summary>The composed status line. Set by the grid unless overridden.</summary>
        public string StatusText
        {
            get => (string)GetValue(StatusTextProperty);
            set => SetValue(StatusTextProperty, value);
        }

        /// <summary>Host content shown on the right of the footer, e.g. aggregates.</summary>
        public object CustomContent
        {
            get => GetValue(CustomContentProperty);
            set => SetValue(CustomContentProperty, value);
        }

        public Brush SeparatorBrush
        {
            get => (Brush)GetValue(SeparatorBrushProperty);
            set => SetValue(SeparatorBrushProperty, value);
        }
    }
}
