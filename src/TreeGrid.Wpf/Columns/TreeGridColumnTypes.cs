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

    /// <summary>
    /// Numeric column. The editor accepts digits only, so invalid text can never be
    /// typed rather than being rejected at commit.
    /// </summary>
    public class TreeGridNumericColumn : TreeGridColumn
    {
        public static readonly DependencyProperty NumberDecimalDigitsProperty = DependencyProperty.Register(
            nameof(NumberDecimalDigits), typeof(int), typeof(TreeGridNumericColumn), new PropertyMetadata(2));

        public static readonly DependencyProperty AllowDecimalsProperty = DependencyProperty.Register(
            nameof(AllowDecimals), typeof(bool?), typeof(TreeGridNumericColumn), new PropertyMetadata(null));

        public static readonly DependencyProperty AllowNegativeProperty = DependencyProperty.Register(
            nameof(AllowNegative), typeof(bool?), typeof(TreeGridNumericColumn), new PropertyMetadata(null));

        public static readonly DependencyProperty UseGroupSeparatorProperty = DependencyProperty.Register(
            nameof(UseGroupSeparator), typeof(bool), typeof(TreeGridNumericColumn), new PropertyMetadata(true));

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

        /// <summary>Digits shown after the separator, and the most that can be typed.</summary>
        public int NumberDecimalDigits
        {
            get => (int)GetValue(NumberDecimalDigitsProperty);
            set => SetValue(NumberDecimalDigitsProperty, value);
        }

        /// <summary>
        /// Whether a decimal separator can be typed. Leave unset to decide
        /// automatically: integral properties (int, long, short...) get integer-only
        /// entry, and everything else follows <see cref="NumberDecimalDigits"/>.
        /// </summary>
        public bool? AllowDecimals
        {
            get => (bool?)GetValue(AllowDecimalsProperty);
            set => SetValue(AllowDecimalsProperty, value);
        }

        /// <summary>Leave unset to allow a minus sign only when <see cref="MinValue"/> permits one.</summary>
        public bool? AllowNegative
        {
            get => (bool?)GetValue(AllowNegativeProperty);
            set => SetValue(AllowNegativeProperty, value);
        }

        /// <summary>Thousands separators in the displayed text. Never in the editor.</summary>
        public bool UseGroupSeparator
        {
            get => (bool)GetValue(UseGroupSeparatorProperty);
            set => SetValue(UseGroupSeparatorProperty, value);
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
            {
                var specifier = (UseGroupSeparator ? "N" : "F") + Math.Max(0, NumberDecimalDigits);
                return formattable.ToString(specifier, CultureInfo.CurrentCulture);
            }

            return value.ToString();
        }

        public override FrameworkElement CreateEditElement() => new NumericTextBox
        {
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 0, 6, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment
        };

        public override void PrepareEditElement(FrameworkElement element, object value, object dataItem)
        {
            if (!(element is NumericTextBox box))
            {
                // Do not delegate to base here: its default forwards to the two-argument
                // overload, which forwards back, and the pair would recurse.
                if (element is TextBox plain)
                {
                    plain.Text = value == null ? string.Empty : Convert.ToString(value, CultureInfo.CurrentCulture);
                    plain.SelectAll();
                }

                return;
            }

            var integral = IsIntegral(ResolveValueType(value, dataItem));

            box.AllowDecimals = AllowDecimals ?? (!integral && NumberDecimalDigits > 0);
            box.MaxDecimalDigits = box.AllowDecimals ? Math.Max(0, NumberDecimalDigits) : 0;
            box.AllowNegative = AllowNegative ?? MinValue < 0;
            box.TextAlignment = TextAlignment;

            // Editors show the raw value: users should not have to delete thousands
            // separators to change a number.
            box.Text = value == null
                ? string.Empty
                : Convert.ToString(value, CultureInfo.CurrentCulture);

            box.SelectAll();
        }

        public override void PrepareEditElement(FrameworkElement element, object value) =>
            PrepareEditElement(element, value, null);

        public override object GetEditValue(FrameworkElement element)
        {
            if (!(element is TextBox box))
                return null;

            var text = box.Text;

            if (string.IsNullOrWhiteSpace(text))
                return null;

            if (!double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out var parsed))
            {
                // Only reachable for a partial entry such as "-" on its own.
                return null;
            }

            if (element is NumericTextBox numeric)
            {
                var digits = numeric.AllowDecimals
                    ? Math.Min(15, Math.Max(0, NumberDecimalDigits))
                    : 0;

                parsed = Math.Round(parsed, digits, MidpointRounding.AwayFromZero);
            }

            if (parsed < MinValue) parsed = MinValue;
            if (parsed > MaxValue) parsed = MaxValue;

            return parsed;
        }

        /// <summary>Uses the live value's type, falling back to the declared property type.</summary>
        private Type ResolveValueType(object value, object dataItem)
        {
            if (value != null)
                return value.GetType();

            if (dataItem == null || string.IsNullOrEmpty(MappingName))
                return null;

            var info = Data.PropertyAccessor.GetPropertyInfo(dataItem.GetType(), MappingName);

            if (info == null)
                return null;

            return Nullable.GetUnderlyingType(info.PropertyType) ?? info.PropertyType;
        }

        private static bool IsIntegral(Type type)
        {
            if (type == null)
                return false;

            type = Nullable.GetUnderlyingType(type) ?? type;

            return type == typeof(byte) || type == typeof(sbyte) ||
                   type == typeof(short) || type == typeof(ushort) ||
                   type == typeof(int) || type == typeof(uint) ||
                   type == typeof(long) || type == typeof(ulong);
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

    /// <summary>
    /// Renders arbitrary content, in the spirit of <c>DataGridTemplateColumn</c>.
    /// <para>
    /// The template's DataContext is the data item, so bindings are written exactly as
    /// they would be in a DataGrid. Editing happens through the template's own
    /// bindings, so the grid does not write a value back on commit.
    /// </para>
    /// </summary>
    public class TreeGridTemplateColumn : TreeGridColumn
    {
        public static readonly DependencyProperty CellTemplateProperty = DependencyProperty.Register(
            nameof(CellTemplate), typeof(DataTemplate), typeof(TreeGridTemplateColumn), new PropertyMetadata(null));

        public static readonly DependencyProperty CellTemplateSelectorProperty = DependencyProperty.Register(
            nameof(CellTemplateSelector), typeof(DataTemplateSelector), typeof(TreeGridTemplateColumn),
            new PropertyMetadata(null));

        public static readonly DependencyProperty EditTemplateProperty = DependencyProperty.Register(
            nameof(EditTemplate), typeof(DataTemplate), typeof(TreeGridTemplateColumn), new PropertyMetadata(null));

        public static readonly DependencyProperty EditTemplateSelectorProperty = DependencyProperty.Register(
            nameof(EditTemplateSelector), typeof(DataTemplateSelector), typeof(TreeGridTemplateColumn),
            new PropertyMetadata(null));

        public DataTemplate CellTemplate
        {
            get => (DataTemplate)GetValue(CellTemplateProperty);
            set => SetValue(CellTemplateProperty, value);
        }

        /// <summary>Chooses a template per row, for heterogeneous content.</summary>
        public DataTemplateSelector CellTemplateSelector
        {
            get => (DataTemplateSelector)GetValue(CellTemplateSelectorProperty);
            set => SetValue(CellTemplateSelectorProperty, value);
        }

        public DataTemplate EditTemplate
        {
            get => (DataTemplate)GetValue(EditTemplateProperty);
            set => SetValue(EditTemplateProperty, value);
        }

        public DataTemplateSelector EditTemplateSelector
        {
            get => (DataTemplateSelector)GetValue(EditTemplateSelectorProperty);
            set => SetValue(EditTemplateSelectorProperty, value);
        }

        public override bool HasCustomDisplay => CellTemplate != null || CellTemplateSelector != null;

        public override bool SupportsValueCommit => false;

        /// <summary>Editing is only offered when there is something to edit with.</summary>
        public bool HasEditTemplate => EditTemplate != null || EditTemplateSelector != null;

        public override FrameworkElement CreateDisplayElement() =>
            new ContentPresenter { VerticalAlignment = VerticalAlignment.Center };

        public override void PrepareDisplayElement(FrameworkElement element, object value, object dataItem)
        {
            if (!(element is ContentPresenter presenter))
                return;

            presenter.ContentTemplate = CellTemplate;
            presenter.ContentTemplateSelector = CellTemplateSelector;

            // Content last: assigning it after the template avoids a redundant
            // container rebuild when a pooled cell is re-targeted.
            presenter.Content = dataItem;
        }

        public override FrameworkElement CreateEditElement() =>
            new ContentPresenter { VerticalAlignment = VerticalAlignment.Center };

        public override void PrepareEditElement(FrameworkElement element, object value, object dataItem)
        {
            if (!(element is ContentPresenter presenter))
                return;

            presenter.ContentTemplate = EditTemplate ?? CellTemplate;
            presenter.ContentTemplateSelector = EditTemplateSelector ?? CellTemplateSelector;

            // Without the data item the edit template has no DataContext and renders
            // empty - the reason template editing did not previously work.
            presenter.Content = dataItem;
        }

        /// <summary>Nothing to hand back; the template's bindings have already written.</summary>
        public override object GetEditValue(FrameworkElement element) => null;
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
