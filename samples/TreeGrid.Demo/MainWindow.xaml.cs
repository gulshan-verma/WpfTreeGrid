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
using TreeGrid.Wpf.Grouping;
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

            // Grouping drag-and-drop reporting.
            Tree.GroupingChanging += OnGroupingChanging;
            Tree.GroupingChanged += OnGroupingChanged;
            Tree.GroupDragOver += OnGroupDragOver;

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

        public FontWeight[] FontWeightOptions { get; } =
        {
            System.Windows.FontWeights.Normal,
            System.Windows.FontWeights.SemiBold,
            System.Windows.FontWeights.Bold
        };

        // --------------------------------------------------------- data sources

        private void OnSourceChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Tree == null)
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

            Tree.ChildPropertyName = "Children";
            ConfigureColumns("FirstName", "LastName", "Title", "Salary", "Available", "Completion", "ProfileUrl");

            Tree.ItemsSource = DemoData.CreateHierarchy();
            StatusText.Text = "Hierarchical - child collection binding";
        }

        private void LoadSelfRelational()
        {
            ResetSource();

            Tree.IdPropertyName = "Id";
            Tree.ParentIdPropertyName = "ParentId";
            ConfigureColumns("Name", "Owner", "PercentComplete", "Start", "Id");

            Tree.ItemsSource = DemoData.CreateSelfRelational();
            StatusText.Text = "Self-relational - 5,000 rows joined by Id / ParentId";
        }

        private void LoadOnDemand()
        {
            ResetSource();

            Tree.DataSource.HasChildNodesResolver = item => item is FolderItem folder && folder.IsFolder;
            Tree.DataSource.RequestTreeItemsAsync = LoadFolderAsync;

            ConfigureColumns("Name", "Kind", "SizeBytes", "Depth");

            Tree.ItemsSource = new ObservableCollection<FolderItem>(DemoData.CreateFolderChildren(null, 12));
            StatusText.Text = "Load on demand - size unknown, filter offers conditions only";
        }

        private void ResetSource()
        {
            Tree.ItemsSource = null;
            Tree.ChildPropertyName = null;
            Tree.IdPropertyName = null;
            Tree.ParentIdPropertyName = null;
            Tree.DataSource.RequestTreeItemsAsync = null;
            Tree.DataSource.HasChildNodesResolver = null;
            Tree.ClearGrouping();
            Tree.ClearSorting();
            Tree.ClearFilters();
        }

        /// <summary>
        /// Shows only the columns that exist on the current source, so switching
        /// between models does not leave dead columns behind.
        /// </summary>
        private void ConfigureColumns(params string[] mappingNames)
        {
            foreach (var column in Tree.Columns)
                column.IsHidden = Array.IndexOf(mappingNames, column.MappingName) < 0;

            var known = new System.Collections.Generic.HashSet<string>(mappingNames);

            foreach (var column in Tree.Columns)
                known.Remove(column.MappingName);

            // Add any column this source needs that does not exist yet.
            foreach (var name in known)
            {
                Tree.Columns.Add(new TreeGridTextColumn
                {
                    MappingName = name,
                    HeaderText = name,
                    Width = 140
                });
            }
        }

        private static async Task<IEnumerable> LoadFolderAsync(TreeNode node, CancellationToken token)
        {
            await Task.Delay(450, token);
            return DemoData.CreateFolderChildren(node.Item as FolderItem);
        }

        // -------------------------------------------------------- layout toggles

        private void OnStackedHeadersChanged(object sender, RoutedEventArgs e)
        {
            if (Tree == null)
                return;

            Tree.StackedHeaderRows.Clear();

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

            Tree.StackedHeaderRows.Add(row);
        }

        private void OnRtlChanged(object sender, RoutedEventArgs e)
        {
            if (Tree == null)
                return;

            Tree.FlowDirection = RtlBox.IsChecked == true
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;

            Tree.RefreshLayout();
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

        // ------------------------------------------------------------- grouping

        private void OnGroupingChanging(object sender, GroupingChangingEventArgs e)
        {
            // Cancel here to refuse a grouping change; nothing is applied.
            if (LogGroupEventsBox.IsChecked == true)
                StatusText.Text = $"Grouping {e.Action}: {e.ColumnName ?? "(all)"} {e.OldIndex} -> {e.NewIndex}";
        }

        private void OnGroupingChanged(object sender, GroupingChangedEventArgs e)
        {
            if (LogGroupEventsBox.IsChecked != true)
                return;

            StatusText.Text = e.Action switch
            {
                GroupingAction.Grouped => $"Grouped by {e.ColumnName} at level {e.NewIndex + 1} ({e.GroupLevelCount} total)",
                GroupingAction.Ungrouped => $"Ungrouped {e.ColumnName} ({e.GroupLevelCount} remaining)",
                GroupingAction.Reordered => $"Moved {e.ColumnName} from level {e.OldIndex + 1} to {e.NewIndex + 1}",
                GroupingAction.SortDirectionChanged => $"Flipped sort on {e.ColumnName}",
                GroupingAction.Cleared => "Grouping cleared",
                _ => StatusText.Text
            };
        }

        private void OnGroupDragOver(object sender, GroupDragOverEventArgs e)
        {
            if (LogGroupEventsBox.IsChecked == true)
                StatusText.Text = $"Drop {e.Column?.HeaderText} at level {e.TargetIndex + 1}";
        }

        private void OnGroupByTitle(object sender, RoutedEventArgs e)
        {
            Tree.ClearGrouping();
            Tree.GroupByColumn("Title");
        }

        private void OnGroupByAvailable(object sender, RoutedEventArgs e) =>
            Tree.GroupByColumn("Available");

        private void OnExpandGroups(object sender, RoutedEventArgs e) => Tree.ExpandAllGroups();

        private void OnClearGrouping(object sender, RoutedEventArgs e) => Tree.ClearGrouping();

        // ----------------------------------------------------------- appearance

        private void OnConditionalChanged(object sender, RoutedEventArgs e)
        {
            if (Tree == null)
                return;

            // Detaching the handler is what turns the feature off: the grid only wires
            // its resolver when the event actually has subscribers.
            Tree.QueryRowStyle -= OnQueryRowStyle;
            Tree.QueryCellStyle -= OnQueryCellStyle;

            if (ConditionalBox.IsChecked == true)
            {
                Tree.QueryRowStyle += OnQueryRowStyle;
                Tree.QueryCellStyle += OnQueryCellStyle;
            }

            Tree.RefreshAppearance();
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
            Tree.ClearValue(TreeGridControl.HeaderBackgroundProperty);
            Tree.ClearValue(TreeGridControl.HeaderForegroundProperty);
            Tree.ClearValue(TreeGridControl.SelectedRowBackgroundProperty);
            Tree.ClearValue(TreeGridControl.HoverRowBackgroundProperty);
            Tree.ClearValue(TreeGridControl.GridLineBrushProperty);
            Tree.ClearValue(TreeGridControl.CellFontFamilyProperty);
            Tree.ClearValue(TreeGridControl.CellFontSizeProperty);
            Tree.ClearValue(TreeGridControl.HeaderFontWeightProperty);
            Tree.ClearValue(TreeGridControl.GridLinesVisibilityProperty);
            Tree.ClearValue(TreeGridControl.RowHeightProperty);
            Tree.ClearValue(TreeGridControl.IndentPerLevelProperty);

            Tree.RefreshAppearance();
        }

        private void OnRestoreColumns(object sender, RoutedEventArgs e)
        {
            foreach (var column in Tree.Columns)
                column.IsHidden = false;
        }

        // -------------------------------------------------------------- actions

        private void OnExpandAll(object sender, RoutedEventArgs e) => Tree.ExpandAll();

        private void OnCollapseAll(object sender, RoutedEventArgs e) => Tree.CollapseAll();

        private void OnAutoFit(object sender, RoutedEventArgs e) => Tree.AutoFitColumns();

        private void OnClearSort(object sender, RoutedEventArgs e) => Tree.ClearSorting();

        private void OnClearFilters(object sender, RoutedEventArgs e) => Tree.ClearFilters();

        private void OnSelectAll(object sender, RoutedEventArgs e) => Tree.SelectAll();

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
                    Tree.Export(new CsvExporter(), dialog.FileName, options);
                    break;

                case ".pdf":
                    options.HierarchyStyle = HierarchyExportStyle.Indent;
                    Tree.Export(new PdfExporter(), dialog.FileName, options);
                    break;

                default:
                    // Excel gets native row grouping, so the tree stays collapsible.
                    options.HierarchyStyle = HierarchyExportStyle.Outline;
                    Tree.Export(new ExcelExporter(), dialog.FileName, options);
                    break;
            }

            StatusText.Text = $"Exported to {Path.GetFileName(dialog.FileName)}";
        }

        private void OnBenchmark(object sender, RoutedEventArgs e)
        {
            var results = GridBenchmark.RunStandardSuite(Tree);
            results.Add(GridBenchmark.MeasureScroll(Tree));

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
