using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using TreeGrid.Wpf.Columns;
using TreeGrid.Wpf.Data;
using TreeGrid.Wpf.Diagnostics;
using TreeGrid.Wpf.Export;
using TreeGrid.Wpf.Filtering;
using TreeGrid.Wpf.Editing;
using TreeGrid.Wpf.Headers;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using TreeGrid.Wpf.Selection;
using TreeGrid.Wpf.Styling;
using TreeGrid.Wpf.Validation;
using TreeGrid.Wpf;

namespace TreeGrid.Demo
{
    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();

            DataContext = this;

            // Columns live outside the visual tree, so XAML bindings that walk it
            // (RelativeSource, ElementName) never resolve against them. Assigning in
            // code-behind is the reliable route.
            TitleColumn.ItemsSource = Titles;

            LoadHierarchical();
        }

        // ------------------------------------------------------ sidebar sources

        public string[] Titles { get; } =
        {
            "Director", "Manager", "Team Lead", "Senior Engineer", "Engineer", "Analyst"
        };

        public Array SelectionModes => Enum.GetValues(typeof(GridSelectionMode));

        public Array SelectionUnits => Enum.GetValues(typeof(GridSelectionUnit));

        public Array CascadeModes => Enum.GetValues(typeof(CheckBoxCascadeMode));

        public Array FilterNodeModes => Enum.GetValues(typeof(FilterNodeMode));

        public Array EditTriggers => Enum.GetValues(typeof(EditTrigger));

        public Array ValidationModes => Enum.GetValues(typeof(GridValidationMode));

        public int[] FreezeCounts { get; } = { 0, 1, 2, 3 };

        public Array GridLineOptions => Enum.GetValues(typeof(GridLinesVisibility));

        /// <summary>Named brushes offered by the appearance combos.</summary>
        public Brush[] Palette { get; } =
        {
            Brushes.Transparent,
            new SolidColorBrush(Color.FromRgb(0xF5, 0xF6, 0xF8)),
            new SolidColorBrush(Color.FromRgb(0xDC, 0xEB, 0xFB)),
            new SolidColorBrush(Color.FromRgb(0xEC, 0xEE, 0xF1)),
            new SolidColorBrush(Color.FromRgb(0x1F, 0x23, 0x28)),
            new SolidColorBrush(Color.FromRgb(0x1F, 0x6F, 0xEB)),
            new SolidColorBrush(Color.FromRgb(0x0D, 0x70, 0x4A)),
            new SolidColorBrush(Color.FromRgb(0xD1, 0x24, 0x2F)),
            new SolidColorBrush(Color.FromRgb(0xFF, 0xF4, 0xCE)),
            Brushes.White
        };

        public FontFamily[] FontFamilies { get; } =
        {
            new FontFamily("Segoe UI"),
            new FontFamily("Calibri"),
            new FontFamily("Consolas"),
            new FontFamily("Georgia"),
            new FontFamily("Verdana")
        };

        public FontWeight[] FontWeights { get; } =
        {
            System.Windows.FontWeights.Normal,
            System.Windows.FontWeights.SemiBold,
            System.Windows.FontWeights.Bold
        };

        // --------------------------------------------------------- data sources

        private void OnSourceChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Grid == null)
                return;

            switch (SourceBox.SelectedIndex)
            {
                case 1:
                    LoadSelfRelational();
                    break;
                case 2:
                    LoadOnDemand();
                    break;
                default:
                    LoadHierarchical();
                    break;
            }
        }

        private void LoadHierarchical()
        {
            ResetSource();

            Grid.ChildPropertyName = "Children";
            ConfigureColumns("FirstName", "LastName", "Title", "Salary", "Available", "Completion", "ProfileUrl");

            Grid.ItemsSource = DemoData.CreateHierarchy();
            StatusText.Text = "Hierarchical - child collection binding";
        }

        private void LoadSelfRelational()
        {
            ResetSource();

            Grid.IdPropertyName = "Id";
            Grid.ParentIdPropertyName = "ParentId";
            ConfigureColumns("Name", "Owner", "PercentComplete", "Start", "Id");

            Grid.ItemsSource = DemoData.CreateSelfRelational();
            StatusText.Text = "Self-relational - 5,000 rows joined by Id / ParentId";
        }

        private void LoadOnDemand()
        {
            ResetSource();

            Grid.DataSource.HasChildNodesResolver = item => item is FolderItem folder && folder.IsFolder;
            Grid.DataSource.RequestTreeItemsAsync = LoadFolderAsync;

            ConfigureColumns("Name", "Kind", "SizeBytes", "Depth");

            Grid.ItemsSource = new ObservableCollection<FolderItem>(DemoData.CreateFolderChildren(null, 12));
            StatusText.Text = "Load on demand - size unknown, filter offers conditions only";
        }

        private void ResetSource()
        {
            Grid.ItemsSource = null;
            Grid.ChildPropertyName = null;
            Grid.IdPropertyName = null;
            Grid.ParentIdPropertyName = null;
            Grid.DataSource.RequestTreeItemsAsync = null;
            Grid.DataSource.HasChildNodesResolver = null;
            Grid.ClearSorting();
            Grid.ClearFilters();
        }

        /// <summary>
        /// Shows only the columns that exist on the current source, so switching
        /// between models does not leave dead columns behind.
        /// </summary>
        private void ConfigureColumns(params string[] mappingNames)
        {
            foreach (var column in Grid.Columns)
                column.IsHidden = Array.IndexOf(mappingNames, column.MappingName) < 0;

            var known = new System.Collections.Generic.HashSet<string>(mappingNames);

            foreach (var column in Grid.Columns)
                known.Remove(column.MappingName);

            // Add any column this source needs that does not exist yet.
            foreach (var name in known)
            {
                Grid.Columns.Add(new TreeGridTextColumn
                {
                    MappingName = name,
                    HeaderText = name,
                    Width = 140
                });
            }
        }

        private static async Task<IEnumerable> LoadFolderAsync(TreeNode node, CancellationToken token)
        {
            await System.Threading.Tasks.Task.Delay(450, token);
            return DemoData.CreateFolderChildren(node.Item as FolderItem);
        }

        // -------------------------------------------------------- layout toggles

        private void OnStackedHeadersChanged(object sender, RoutedEventArgs e)
        {
            if (Grid == null)
                return;

            Grid.StackedHeaderRows.Clear();

            if (StackedHeaderBox.IsChecked != true)
                return;

            var row = new StackedHeaderRow();

            row.StackedColumns.Add(new StackedColumn
            {
                HeaderText = "Identity",
                ChildColumns = "FirstName,LastName"
            });

            row.StackedColumns.Add(new StackedColumn
            {
                HeaderText = "Employment",
                ChildColumns = "Title,Salary,Available"
            });

            row.StackedColumns.Add(new StackedColumn
            {
                HeaderText = "Status",
                ChildColumns = "Completion,ProfileUrl"
            });

            Grid.StackedHeaderRows.Add(row);
        }

        private void OnRtlChanged(object sender, RoutedEventArgs e)
        {
            if (Grid == null)
                return;

            Grid.FlowDirection = RtlBox.IsChecked == true
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;

            Grid.RefreshLayout();
        }

        /// <summary>
        /// Swaps the single theme dictionary at application scope. The control's
        /// templates look these brushes up with DynamicResource, so replacing the
        /// dictionary repaints the grid without rebuilding anything.
        /// </summary>
        private void OnThemeChanged(object sender, RoutedEventArgs e)
        {
            var themeName = DarkThemeBox.IsChecked == true ? "FluentDark" : "FluentLight";

            var replacement = new ResourceDictionary
            {
                Source = new Uri(
                    $"pack://application:,,,/TreeGrid.Wpf;component/Themes/{themeName}.xaml",
                    UriKind.Absolute)
            };

            var merged = Application.Current.Resources.MergedDictionaries;

            if (merged.Count > 0)
                merged[0] = replacement;
            else
                merged.Add(replacement);
        }

        // ----------------------------------------------------------- appearance

        private void OnConditionalChanged(object sender, RoutedEventArgs e)
        {
            if (Grid == null)
                return;

            // Detaching the handler is what turns the feature off: the grid only wires
            // its resolver when the event actually has subscribers.
            Grid.QueryRowStyle -= OnQueryRowStyle;
            Grid.QueryCellStyle -= OnQueryCellStyle;

            if (ConditionalBox.IsChecked == true)
            {
                Grid.QueryRowStyle += OnQueryRowStyle;
                Grid.QueryCellStyle += OnQueryCellStyle;
            }

            Grid.RefreshAppearance();
        }

        private void OnQueryRowStyle(object sender, QueryRowStyleEventArgs e)
        {
            if (e.Record is not Employee employee)
                return;

            if (employee.Title == "Director")
                e.FontWeight = System.Windows.FontWeights.Bold;
        }

        private void OnQueryCellStyle(object sender, QueryCellStyleEventArgs e)
        {
            if (e.Record is not Employee employee || e.Column?.MappingName != nameof(Employee.Salary))
                return;

            if (employee.Salary > 150000)
            {
                e.Background = new SolidColorBrush(Color.FromRgb(0xDC, 0xF5, 0xE6));
                e.Foreground = new SolidColorBrush(Color.FromRgb(0x0D, 0x70, 0x4A));
            }
            else if (employee.Salary < 60000)
            {
                e.Background = new SolidColorBrush(Color.FromRgb(0xFD, 0xE0, 0xE0));
                e.Foreground = new SolidColorBrush(Color.FromRgb(0xD1, 0x24, 0x2F));
            }
        }

        private void OnResetAppearance(object sender, RoutedEventArgs e)
        {
            // Clearing a property restores the theme fallback, which is not the same as
            // assigning the current theme's value - the grid will follow later theme
            // changes again.
            Grid.ClearValue(TreeGridControl.HeaderBackgroundProperty);
            Grid.ClearValue(TreeGridControl.HeaderForegroundProperty);
            Grid.ClearValue(TreeGridControl.SelectedRowBackgroundProperty);
            Grid.ClearValue(TreeGridControl.HoverRowBackgroundProperty);
            Grid.ClearValue(TreeGridControl.GridLineBrushProperty);
            Grid.ClearValue(TreeGridControl.CellFontFamilyProperty);
            Grid.ClearValue(TreeGridControl.CellFontSizeProperty);
            Grid.ClearValue(TreeGridControl.HeaderFontWeightProperty);
            Grid.ClearValue(TreeGridControl.GridLinesVisibilityProperty);
            Grid.ClearValue(TreeGridControl.RowHeightProperty);
            Grid.ClearValue(TreeGridControl.IndentPerLevelProperty);

            Grid.RefreshAppearance();
        }

        private void OnRestoreColumns(object sender, RoutedEventArgs e)
        {
            foreach (var column in Grid.Columns)
                column.IsHidden = false;
        }

        // -------------------------------------------------------------- actions

        private void OnExpandAll(object sender, RoutedEventArgs e) => Grid.ExpandAll();

        private void OnCollapseAll(object sender, RoutedEventArgs e) => Grid.CollapseAll();

        private void OnAutoFit(object sender, RoutedEventArgs e) => Grid.AutoFitColumns();

        private void OnClearSort(object sender, RoutedEventArgs e) => Grid.ClearSorting();

        private void OnClearFilters(object sender, RoutedEventArgs e) => Grid.ClearFilters();

        private void OnSelectAll(object sender, RoutedEventArgs e) => Grid.SelectAll();

        private void OnExport(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Excel workbook (*.xlsx)|*.xlsx|CSV file (*.csv)|*.csv|PDF document (*.pdf)|*.pdf",
                FileName = "treegrid-export"
            };

            if (dialog.ShowDialog() != true)
                return;

            var options = new GridExportOptions
            {
                Title = "TreeGrid export",
                RowScope = ExportRowScope.VisibleRows,
                IncludeHeaders = true
            };

            switch (Path.GetExtension(dialog.FileName)?.ToLowerInvariant())
            {
                case ".csv":
                    Grid.Export(new CsvExporter(), dialog.FileName, options);
                    break;

                case ".pdf":
                    options.HierarchyStyle = HierarchyExportStyle.Indent;
                    Grid.Export(new PdfExporter(), dialog.FileName, options);
                    break;

                default:
                    // Excel gets native row grouping, so the tree stays collapsible.
                    options.HierarchyStyle = HierarchyExportStyle.Outline;
                    Grid.Export(new ExcelExporter(), dialog.FileName, options);
                    break;
            }

            StatusText.Text = $"Exported to {Path.GetFileName(dialog.FileName)}";
        }

        private void OnBenchmark(object sender, RoutedEventArgs e)
        {
            var results = GridBenchmark.RunStandardSuite(Grid);
            results.Add(GridBenchmark.MeasureScroll(Grid));

            MessageBox.Show(GridBenchmark.Format(results), "Benchmark",
                MessageBoxButton.OK, MessageBoxImage.None);
        }
    }

    /// <summary>Shows a readable name beside each colour swatch in the palette combos.</summary>
    public sealed class BrushNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SolidColorBrush brush)
            {
                var colour = brush.Color;

                if (colour.A == 0)
                    return "None";

                return $"#{colour.R:X2}{colour.G:X2}{colour.B:X2}";
            }

            return value?.ToString() ?? "None";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }
}
