using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace TreeGrid.Wpf.Columns
{
    /// <summary>
    /// Column types supply their own editors through these hooks. The grid never
    /// switches on column type; it asks the column for an element, hands it a value,
    /// and asks for the value back when the edit commits.
    /// </summary>
    public static class ColumnEditorSupport
    {
        public const string EditElementName = "PART_EditElement";
    }

    /// <summary>Numeric column with culture-aware parsing and format support.</summary>
    public class TreeGridNumericColumn : TreeGridColumn
    {
        public static readonly DependencyProperty NumberDecimalDigitsProperty = DependencyProperty.Register(
            nameof(NumberDecimalDigits), typeof(int), typeof(TreeGridNumericColumn), new PropertyMetadata(2));

        public static readonly DependencyProperty MinValueProperty = DependencyProperty.Register(
            nameof(MinValue), typeof(double), typeof(TreeGridNumericColumn),
            new PropertyMetadata(double.NegativeInfinity));

        public static readonly DependencyProperty MaxValueProperty = DependencyProperty.Register(
            nameof(MaxValue), typeof(double), typeof(TreeGridNumericColumn),
            new PropertyMetadata(double.PositiveInfinity));

        public TreeGridNumericColumn()
        {
            TextAlignment = TextAlignment.Right;
        }

        public int NumberDecimalDigits
        {
            get => (int)GetValue(NumberDecimalDigitsProperty);
            set => SetValue(NumberDecimalDigitsProperty, value);
        }

        public double MinValue
        {
            get => (double)GetValue(MinValueProperty);
            set => SetValue(MinValueProperty, value);
        }

        public double MaxValue
        {
            get => (double)GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }

        public override string FormatValue(object value)
        {
            if (value == null)
                return string.Empty;

            if (!string.IsNullOrEmpty(DisplayFormat))
                return string.Format(CultureInfo.CurrentCulture, DisplayFormat, value);

            if (value is IFormattable formattable)
                return formattable.ToString("N" + NumberDecimalDigits, CultureInfo.CurrentCulture);

            return value.ToString();
        }

        public override FrameworkElement CreateEditElement() => new TextBox
        {
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 0, 6, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment
        };

        public override void PrepareEditElement(FrameworkElement element, object value)
        {
            if (!(element is TextBox box))
                return;

            // Editors show the raw value, not the display format: users should not have
            // to delete thousands separators to type a number.
            box.Text = value == null ? string.Empty : Convert.ToString(value, CultureInfo.CurrentCulture);
            box.SelectAll();
        }

        public override object GetEditValue(FrameworkElement element)
        {
            if (!(element is TextBox box))
                return null;

            if (string.IsNullOrWhiteSpace(box.Text))
                return null;

            if (!double.TryParse(box.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var parsed))
                return box.Text; // Let validation report the bad input.

            if (parsed < MinValue) parsed = MinValue;
            if (parsed > MaxValue) parsed = MaxValue;

            return parsed;
        }
    }

    public class TreeGridDateTimeColumn : TreeGridColumn
    {
        public static readonly DependencyProperty DateFormatProperty = DependencyProperty.Register(
            nameof(DateFormat), typeof(string), typeof(TreeGridDateTimeColumn), new PropertyMetadata("d"));

        public string DateFormat
        {
            get => (string)GetValue(DateFormatProperty);
            set => SetValue(DateFormatProperty, value);
        }

        public override string FormatValue(object value)
        {
            if (value == null)
                return string.Empty;

            if (!string.IsNullOrEmpty(DisplayFormat))
                return string.Format(CultureInfo.CurrentCulture, DisplayFormat, value);

            if (value is DateTime date)
                return date.ToString(DateFormat, CultureInfo.CurrentCulture);

            return value.ToString();
        }

        public override FrameworkElement CreateEditElement() => new DatePicker
        {
            BorderThickness = new Thickness(0),
            VerticalContentAlignment = VerticalAlignment.Center
        };

        public override void PrepareEditElement(FrameworkElement element, object value)
        {
            if (element is DatePicker picker)
                picker.SelectedDate = value as DateTime?;
        }

        public override object GetEditValue(FrameworkElement element) =>
            element is DatePicker picker ? picker.SelectedDate : null;
    }

    public class TreeGridComboBoxColumn : TreeGridColumn
    {
        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource), typeof(System.Collections.IEnumerable), typeof(TreeGridComboBoxColumn),
            new PropertyMetadata(null));

        public static readonly DependencyProperty DisplayMemberPathProperty = DependencyProperty.Register(
            nameof(DisplayMemberPath), typeof(string), typeof(TreeGridComboBoxColumn), new PropertyMetadata(null));

        public static readonly DependencyProperty SelectedValuePathProperty = DependencyProperty.Register(
            nameof(SelectedValuePath), typeof(string), typeof(TreeGridComboBoxColumn), new PropertyMetadata(null));

        public static readonly DependencyProperty IsEditableProperty = DependencyProperty.Register(
            nameof(IsEditable), typeof(bool), typeof(TreeGridComboBoxColumn), new PropertyMetadata(false));

        public System.Collections.IEnumerable ItemsSource
        {
            get => (System.Collections.IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public string DisplayMemberPath
        {
            get => (string)GetValue(DisplayMemberPathProperty);
            set => SetValue(DisplayMemberPathProperty, value);
        }

        public string SelectedValuePath
        {
            get => (string)GetValue(SelectedValuePathProperty);
            set => SetValue(SelectedValuePathProperty, value);
        }

        public bool IsEditable
        {
            get => (bool)GetValue(IsEditableProperty);
            set => SetValue(IsEditableProperty, value);
        }

        public override FrameworkElement CreateEditElement()
        {
            var combo = new ComboBox
            {
                BorderThickness = new Thickness(0),
                VerticalContentAlignment = VerticalAlignment.Center,
                ItemsSource = ItemsSource,
                IsEditable = IsEditable
            };

            if (!string.IsNullOrEmpty(DisplayMemberPath))
                combo.DisplayMemberPath = DisplayMemberPath;

            if (!string.IsNullOrEmpty(SelectedValuePath))
                combo.SelectedValuePath = SelectedValuePath;

            return combo;
        }

        public override void PrepareEditElement(FrameworkElement element, object value)
        {
            if (!(element is ComboBox combo))
                return;

            combo.ItemsSource = ItemsSource;

            if (!string.IsNullOrEmpty(SelectedValuePath))
                combo.SelectedValue = value;
            else
                combo.SelectedItem = value;

            // Opening an empty list shows a blank floating box with nothing in it.
            // If there is nothing to choose from, leave it closed.
            combo.IsDropDownOpen = combo.HasItems;
        }

        /// <summary>
        /// Columns are plain DependencyObjects with no place in the visual or logical
        /// tree, so bindings using RelativeSource or ElementName cannot resolve against
        /// them. Set this from code-behind, or use a static resource.
        /// </summary>
        public bool HasItems
        {
            get
            {
                if (ItemsSource == null)
                    return false;

                var enumerator = ItemsSource.GetEnumerator();

                try
                {
                    return enumerator.MoveNext();
                }
                finally
                {
                    (enumerator as IDisposable)?.Dispose();
                }
            }
        }

        public override object GetEditValue(FrameworkElement element)
        {
            if (!(element is ComboBox combo))
                return null;

            if (!string.IsNullOrEmpty(SelectedValuePath))
                return combo.SelectedValue;

            return combo.IsEditable && combo.SelectedItem == null ? combo.Text : combo.SelectedItem;
        }
    }

    /// <summary>Renders an arbitrary DataTemplate, with an optional separate edit template.</summary>
    public class TreeGridTemplateColumn : TreeGridColumn
    {
        public static readonly DependencyProperty CellTemplateProperty = DependencyProperty.Register(
            nameof(CellTemplate), typeof(DataTemplate), typeof(TreeGridTemplateColumn), new PropertyMetadata(null));

        public static readonly DependencyProperty EditTemplateProperty = DependencyProperty.Register(
            nameof(EditTemplate), typeof(DataTemplate), typeof(TreeGridTemplateColumn), new PropertyMetadata(null));

        public DataTemplate CellTemplate
        {
            get => (DataTemplate)GetValue(CellTemplateProperty);
            set => SetValue(CellTemplateProperty, value);
        }

        public DataTemplate EditTemplate
        {
            get => (DataTemplate)GetValue(EditTemplateProperty);
            set => SetValue(EditTemplateProperty, value);
        }

        public override bool HasCustomDisplay => CellTemplate != null;

        public override FrameworkElement CreateDisplayElement() =>
            new ContentPresenter { ContentTemplate = CellTemplate, VerticalAlignment = VerticalAlignment.Center };

        /// <summary>Template columns bind to the whole data item, not a single cell value.</summary>
        public override void PrepareDisplayElement(FrameworkElement element, object value, object dataItem)
        {
            if (element is ContentPresenter presenter)
            {
                presenter.ContentTemplate = CellTemplate;
                presenter.Content = dataItem;
            }
        }

        public override FrameworkElement CreateEditElement() =>
            new ContentPresenter { ContentTemplate = EditTemplate ?? CellTemplate, VerticalAlignment = VerticalAlignment.Center };

        public override void PrepareEditElement(FrameworkElement element, object value)
        {
            if (element is ContentPresenter presenter)
                presenter.ContentTemplate = EditTemplate ?? CellTemplate;
        }

        /// <summary>
        /// A template column edits through its own bindings, so there is no scalar to
        /// hand back. Returning the unchanged value keeps the commit path a no-op.
        /// </summary>
        public override object GetEditValue(FrameworkElement element) => null;

        public override bool SupportsValueCommit => false;
    }

    public class TreeGridProgressBarColumn : TreeGridColumn
    {
        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
            nameof(Minimum), typeof(double), typeof(TreeGridProgressBarColumn), new PropertyMetadata(0d));

        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
            nameof(Maximum), typeof(double), typeof(TreeGridProgressBarColumn), new PropertyMetadata(100d));

        public static readonly DependencyProperty ShowPercentTextProperty = DependencyProperty.Register(
            nameof(ShowPercentText), typeof(bool), typeof(TreeGridProgressBarColumn), new PropertyMetadata(true));

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public bool ShowPercentText
        {
            get => (bool)GetValue(ShowPercentTextProperty);
            set => SetValue(ShowPercentTextProperty, value);
        }

        public override bool HasCustomDisplay => true;

        public override FrameworkElement CreateDisplayElement()
        {
            var grid = new Grid { Margin = new Thickness(6, 4, 6, 4) };

            grid.Children.Add(new ProgressBar
            {
                Name = "Bar",
                Minimum = Minimum,
                Maximum = Maximum
            });

            grid.Children.Add(new TextBlock
            {
                Name = "Label",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 10
            });

            return grid;
        }

        public override void PrepareDisplayElement(FrameworkElement element, object value, object dataItem)
        {
            if (!(element is Grid grid) || grid.Children.Count < 2)
                return;

            var numeric = 0d;
            if (value != null)
                double.TryParse(Convert.ToString(value, CultureInfo.CurrentCulture),
                    NumberStyles.Any, CultureInfo.CurrentCulture, out numeric);

            if (grid.Children[0] is ProgressBar bar)
            {
                bar.Minimum = Minimum;
                bar.Maximum = Maximum;
                bar.Value = numeric;
            }

            if (grid.Children[1] is TextBlock label)
            {
                label.Visibility = ShowPercentText ? Visibility.Visible : Visibility.Collapsed;
                label.Text = ShowPercentText ? $"{numeric:0}%" : string.Empty;
            }
        }
    }

    public class TreeGridHyperlinkColumn : TreeGridColumn
    {
        public override bool HasCustomDisplay => true;

        public override FrameworkElement CreateDisplayElement()
        {
            var link = new Hyperlink();
            var block = new TextBlock
            {
                Margin = new Thickness(6, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            block.Inlines.Add(link);
            return block;
        }

        public override void PrepareDisplayElement(FrameworkElement element, object value, object dataItem)
        {
            if (!(element is TextBlock block) || block.Inlines.FirstInline is not Hyperlink link)
                return;

            link.Inlines.Clear();
            link.Inlines.Add(new Run(FormatValue(value)));

            // Only set NavigateUri for well-formed absolute URIs; a malformed value
            // would otherwise throw when the link is clicked.
            var text = value?.ToString();
            link.NavigateUri = Uri.TryCreate(text, UriKind.Absolute, out var uri) ? uri : null;
        }
    }
}
