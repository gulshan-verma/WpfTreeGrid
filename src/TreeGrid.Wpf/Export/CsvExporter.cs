using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TreeGrid.Wpf.Columns;
using TreeGrid.Wpf.Data;

namespace TreeGrid.Wpf.Export
{
    public enum ExportRowScope
    {
        /// <summary>Every row currently in the flat view, honouring filters.</summary>
        VisibleRows,

        /// <summary>Every node, including those hidden by collapse or filter.</summary>
        AllRows,

        SelectedRows
    }

    public enum HierarchyExportStyle
    {
        /// <summary>Prefix the first column with indent characters.</summary>
        Indent,

        /// <summary>Emit the level as its own leading column.</summary>
        LevelColumn,

        /// <summary>Group rows using the format's native outline, where supported.</summary>
        Outline,

        None
    }

    public sealed class GridExportOptions
    {
        public ExportRowScope RowScope { get; set; } = ExportRowScope.VisibleRows;

        public HierarchyExportStyle HierarchyStyle { get; set; } = HierarchyExportStyle.Indent;

        public bool IncludeHeaders { get; set; } = true;

        public bool IncludeHiddenColumns { get; set; }

        /// <summary>Apply each column's DisplayFormat rather than exporting raw values.</summary>
        public bool ApplyDisplayFormat { get; set; } = true;

        /// <summary>Characters used per level when HierarchyStyle is Indent.</summary>
        public string IndentString { get; set; } = "    ";

        public string SheetName { get; set; } = "TreeGrid";

        public string Title { get; set; }

        /// <summary>Return false to omit a row entirely.</summary>
        public Func<object, bool> RowFilter { get; set; }

        public Encoding Encoding { get; set; } = new UTF8Encoding(true);
    }

    /// <summary>One row flattened for export, carrying its depth.</summary>
    public sealed class ExportRow
    {
        public ExportRow(TreeNode node, int level, IReadOnlyList<object> values, IReadOnlyList<string> texts)
        {
            Node = node;
            Level = level;
            Values = values;
            Texts = texts;
        }

        public TreeNode Node { get; }

        public int Level { get; }

        /// <summary>Raw values, for formats that carry types.</summary>
        public IReadOnlyList<object> Values { get; }

        /// <summary>Formatted text, for formats that do not.</summary>
        public IReadOnlyList<string> Texts { get; }
    }

    public interface IGridExporter
    {
        void Export(Stream stream, IReadOnlyList<TreeGridColumn> columns,
            IReadOnlyList<ExportRow> rows, GridExportOptions options);
    }

    /// <summary>
    /// Turns a grid's data into flat export rows. Shared by every exporter so the row
    /// scope, filter and hierarchy rules behave identically across formats.
    /// </summary>
    public static class ExportDataBuilder
    {
        public static List<TreeGridColumn> ResolveColumns(IEnumerable<TreeGridColumn> columns, GridExportOptions options)
        {
            var result = new List<TreeGridColumn>();

            foreach (var column in columns)
            {
                if (column.IsHidden && !options.IncludeHiddenColumns)
                    continue;

                result.Add(column);
            }

            return result;
        }

        public static List<ExportRow> BuildRows(FlatTreeView view, IReadOnlyList<TreeGridColumn> columns,
            GridExportOptions options, IReadOnlyCollection<object> selectedItems)
        {
            var rows = new List<ExportRow>();

            if (view == null)
                return rows;

            switch (options.RowScope)
            {
                case ExportRowScope.AllRows:
                    foreach (var root in view.RootNodes)
                        AppendRecursive(root, columns, options, rows);
                    break;

                case ExportRowScope.SelectedRows:
                    if (selectedItems == null || selectedItems.Count == 0)
                        break;

                    // Hashing the selection once turns an O(rows x selected) scan into
                    // a single pass, which matters when exporting a large selection.
                    var selectedSet = new HashSet<object>(selectedItems);

                    for (var i = 0; i < view.Count; i++)
                    {
                        var node = view[i];

                        if (selectedSet.Contains(node.Item))
                            AppendRow(node, columns, options, rows);
                    }
                    break;

                default:
                    for (var i = 0; i < view.Count; i++)
                        AppendRow(view[i], columns, options, rows);
                    break;
            }

            return rows;
        }

        private static void AppendRecursive(TreeNode node, IReadOnlyList<TreeGridColumn> columns,
            GridExportOptions options, List<ExportRow> rows)
        {
            AppendRow(node, columns, options, rows);

            for (var i = 0; i < node.ChildNodes.Count; i++)
                AppendRecursive(node.ChildNodes[i], columns, options, rows);
        }

        private static void AppendRow(TreeNode node, IReadOnlyList<TreeGridColumn> columns,
            GridExportOptions options, List<ExportRow> rows)
        {
            if (options.RowFilter != null && !options.RowFilter(node.Item))
                return;

            var values = new object[columns.Count];
            var texts = new string[columns.Count];

            for (var i = 0; i < columns.Count; i++)
            {
                var column = columns[i];

                var value = string.IsNullOrEmpty(column.MappingName)
                    ? null
                    : PropertyAccessor.GetValue(node.Item, column.MappingName);

                values[i] = value;
                texts[i] = options.ApplyDisplayFormat
                    ? column.FormatValue(value)
                    : value?.ToString() ?? string.Empty;
            }

            rows.Add(new ExportRow(node, node.Level, values, texts));
        }
    }

    /// <summary>CSV export. No third-party dependency, so it lives in the core library.</summary>
    public sealed class CsvExporter : IGridExporter
    {
        public char Delimiter { get; set; } = ',';

        public void Export(Stream stream, IReadOnlyList<TreeGridColumn> columns,
            IReadOnlyList<ExportRow> rows, GridExportOptions options)
        {
            // leaveOpen: the caller owns the stream and may still need it.
            using (var writer = new StreamWriter(stream, options.Encoding, 1024, leaveOpen: true))
            {
                var levelColumn = options.HierarchyStyle == HierarchyExportStyle.LevelColumn;

                if (options.IncludeHeaders)
                {
                    var headers = new List<string>(columns.Count + 1);

                    if (levelColumn)
                        headers.Add("Level");

                    foreach (var column in columns)
                        headers.Add(column.ResolvedHeaderText);

                    writer.WriteLine(string.Join(Delimiter.ToString(), Escape(headers)));
                }

                foreach (var row in rows)
                {
                    var cells = new List<string>(columns.Count + 1);

                    if (levelColumn)
                        cells.Add(row.Level.ToString());

                    for (var i = 0; i < row.Texts.Count; i++)
                    {
                        var text = row.Texts[i];

                        if (i == 0 && options.HierarchyStyle == HierarchyExportStyle.Indent && row.Level > 0)
                            text = Repeat(options.IndentString, row.Level) + text;

                        cells.Add(text);
                    }

                    writer.WriteLine(string.Join(Delimiter.ToString(), Escape(cells)));
                }

                writer.Flush();
            }
        }

        private IEnumerable<string> Escape(IEnumerable<string> values)
        {
            foreach (var value in values)
            {
                if (string.IsNullOrEmpty(value))
                {
                    yield return string.Empty;
                    continue;
                }

                var needsQuotes = value.IndexOf(Delimiter) >= 0 ||
                                  value.IndexOf('"') >= 0 ||
                                  value.IndexOf('\n') >= 0 ||
                                  value.IndexOf('\r') >= 0;

                yield return needsQuotes
                    ? "\"" + value.Replace("\"", "\"\"") + "\""
                    : value;
            }
        }

        private static string Repeat(string value, int count)
        {
            var builder = new StringBuilder(value.Length * count);

            for (var i = 0; i < count; i++)
                builder.Append(value);

            return builder.ToString();
        }
    }
}
