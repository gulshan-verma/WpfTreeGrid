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
using TreeGrid.Wpf.Selection;
using TreeGrid.Wpf.Validation;

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
}
