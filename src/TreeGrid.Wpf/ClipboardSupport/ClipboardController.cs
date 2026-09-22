using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Windows;
using TreeGrid.Wpf.Columns;
using TreeGrid.Wpf.Data;

namespace TreeGrid.Wpf.ClipboardSupport
{
    [Flags]
    public enum GridCopyOptions
    {
        None = 0,
        IncludeHeaders = 1,
        IncludeHiddenColumns = 2,
        IncludeFormat = 4,

        /// <summary>Prefix each row with tabs matching its hierarchy level.</summary>
        IncludeHierarchyIndent = 8,

        Default = IncludeFormat
    }

    public enum GridPasteMode
    {
        /// <summary>Overwrite the cells under the paste target.</summary>
        Overwrite,

        /// <summary>Only fill cells that are currently empty.</summary>
        FillEmptyOnly,

        /// <summary>Do nothing; the host handles PasteContent.</summary>
        Manual
    }

    public sealed class CopyContentEventArgs : CancelEventArgs
    {
        public CopyContentEventArgs(IReadOnlyList<TreeNode> nodes, bool isCut)
        {
            Nodes = nodes;
            IsCut = isCut;
        }

        public IReadOnlyList<TreeNode> Nodes { get; }

        public bool IsCut { get; }

        /// <summary>Set to override what reaches the clipboard.</summary>
        public string ClipboardText { get; set; }
    }

    /// <summary>Outcome of a paste, so hosts can report what actually landed.</summary>
    public sealed class GridPasteResult
    {
        public int UpdatedCells { get; internal set; }

        public int RejectedCells { get; internal set; }

        public int RowsAffected { get; internal set; }

        /// <summary>Rows in the clipboard that ran past the end of the grid.</summary>
        public int TruncatedRows { get; internal set; }

        public bool HasChanges => UpdatedCells > 0;
    }

    public sealed class PasteContentEventArgs : CancelEventArgs
    {
        public PasteContentEventArgs(TreeNode targetNode, int targetColumnIndex, string clipboardText)
        {
            TargetNode = targetNode;
            TargetColumnIndex = targetColumnIndex;
            ClipboardText = clipboardText;
        }

        public TreeNode TargetNode { get; }

        public int TargetColumnIndex { get; }

        public string ClipboardText { get; }
    }

    /// <summary>
    /// Clipboard interop.
    /// <para>
    /// Three formats go onto the clipboard at once: tab-separated text (what Excel
    /// reads on paste), CSV, and HTML. Excel prefers HTML when present, which is what
    /// preserves the table structure rather than dumping everything into one column.
    /// </para>
    /// </summary>
    public sealed class ClipboardController
    {
        public GridCopyOptions CopyOptions { get; set; } = GridCopyOptions.Default;

        public GridPasteMode PasteMode { get; set; } = GridPasteMode.Overwrite;

        public event EventHandler<CopyContentEventArgs> CopyContent;

        public event EventHandler<PasteContentEventArgs> PasteContent;

        // ------------------------------------------------------------------ copy

        public bool Copy(IReadOnlyList<TreeNode> nodes, IReadOnlyList<TreeGridColumn> columns, bool isCut = false)
        {
            if (nodes == null || nodes.Count == 0 || columns == null)
                return false;

            var args = new CopyContentEventArgs(nodes, isCut);
            CopyContent?.Invoke(this, args);

            if (args.Cancel)
                return false;

            var text = args.ClipboardText ?? BuildDelimited(nodes, columns, '\t');

            try
            {
                var data = new DataObject();

                data.SetData(DataFormats.UnicodeText, text);
                data.SetData(DataFormats.Text, text);
                data.SetData(DataFormats.CommaSeparatedValue, BuildDelimited(nodes, columns, ','));
                data.SetData(DataFormats.Html, BuildHtmlClipboardFragment(nodes, columns));

                Clipboard.SetDataObject(data, true);
                return true;
            }
            catch (Exception)
            {
                // The clipboard can be locked by another process; failing silently beats
                // throwing out of a Ctrl+C handler.
                return false;
            }
        }

        private string BuildDelimited(IReadOnlyList<TreeNode> nodes, IReadOnlyList<TreeGridColumn> columns, char delimiter)
        {
            var builder = new StringBuilder();
            var exported = FilterColumns(columns);

            if (CopyOptions.HasFlag(GridCopyOptions.IncludeHeaders))
            {
                for (var i = 0; i < exported.Count; i++)
                {
                    if (i > 0) builder.Append(delimiter);
                    builder.Append(Escape(exported[i].ResolvedHeaderText, delimiter));
                }

                builder.AppendLine();
            }

            foreach (var node in nodes)
            {
                if (CopyOptions.HasFlag(GridCopyOptions.IncludeHierarchyIndent))
                    builder.Append(new string(' ', node.Level * 4));

                for (var i = 0; i < exported.Count; i++)
                {
                    if (i > 0) builder.Append(delimiter);
                    builder.Append(Escape(GetCellText(node, exported[i]), delimiter));
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }

        private string BuildHtmlClipboardFragment(IReadOnlyList<TreeNode> nodes, IReadOnlyList<TreeGridColumn> columns)
        {
            var exported = FilterColumns(columns);
            var table = new StringBuilder();

            table.Append("<table>");

            if (CopyOptions.HasFlag(GridCopyOptions.IncludeHeaders))
            {
                table.Append("<tr>");
                foreach (var column in exported)
                    table.Append("<th>").Append(HtmlEncode(column.ResolvedHeaderText)).Append("</th>");
                table.Append("</tr>");
            }

            foreach (var node in nodes)
            {
                table.Append("<tr>");

                for (var i = 0; i < exported.Count; i++)
                {
                    var text = HtmlEncode(GetCellText(node, exported[i]));

                    // Indent the first column so hierarchy survives the round trip.
                    if (i == 0 && node.Level > 0 && CopyOptions.HasFlag(GridCopyOptions.IncludeHierarchyIndent))
                        text = string.Concat(Repeat("&nbsp;&nbsp;&nbsp;&nbsp;", node.Level), text);

                    table.Append("<td>").Append(text).Append("</td>");
                }

                table.Append("</tr>");
            }

            table.Append("</table>");

            return WrapHtmlFragment(table.ToString());
        }

        /// <summary>
        /// CF_HTML requires a header with byte offsets into the payload. They must be
        /// computed after the header length is known, so the header is built with
        /// placeholders and then rewritten.
        /// </summary>
        private static string WrapHtmlFragment(string fragment)
        {
            const string header =
                "Version:0.9\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\n" +
                "StartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";

            const string htmlOpen = "<html><body><!--StartFragment-->";
            const string htmlClose = "<!--EndFragment--></body></html>";

            var sample = string.Format(CultureInfo.InvariantCulture, header, 0, 0, 0, 0);

            var startHtml = sample.Length;
            var startFragment = startHtml + htmlOpen.Length;
            var endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
            var endHtml = endFragment + htmlClose.Length;

            return string.Format(CultureInfo.InvariantCulture, header, startHtml, endHtml, startFragment, endFragment)
                   + htmlOpen + fragment + htmlClose;
        }

        // ----------------------------------------------------------------- paste

        /// <summary>
        /// Reads tab-separated clipboard text and writes it starting at the target
        /// cell. Returns the number of cells actually written.
        /// </summary>
        public GridPasteResult Paste(TreeNode targetNode, int targetColumnIndex, FlatTreeView view,
            IReadOnlyList<TreeGridColumn> columns, Func<TreeNode, TreeGridColumn, string, bool> writer,
            bool allowTabular = true)
        {
            if (targetNode == null || view == null || columns == null || writer == null)
                return new GridPasteResult();

            string text;

            try
            {
                if (!Clipboard.ContainsText())
                    return new GridPasteResult();

                text = Clipboard.GetText();
            }
            catch (Exception)
            {
                return new GridPasteResult();
            }

            if (string.IsNullOrEmpty(text))
                return new GridPasteResult();

            var args = new PasteContentEventArgs(targetNode, targetColumnIndex, text);
            PasteContent?.Invoke(this, args);

            if (args.Cancel || PasteMode == GridPasteMode.Manual)
                return new GridPasteResult();

            var table = ParseTabular(text);
            var result = new GridPasteResult();

            if (table.Count == 0)
                return result;

            // A single clipboard cell fills only the target when tabular paste is off.
            if (!allowTabular && (table.Count > 1 || table[0].Count > 1))
                table = new List<List<string>> { new List<string> { table[0][0] } };

            var rowIndex = targetNode.FlatIndex;

            foreach (var row in table)
            {
                // Skip group headers rather than consuming a clipboard row on them.
                while (rowIndex < view.Count && (view[rowIndex].IsGroupHeader || view[rowIndex].Item == null))
                    rowIndex++;

                if (rowIndex < 0 || rowIndex >= view.Count)
                {
                    result.TruncatedRows++;
                    continue;
                }

                var node = view[rowIndex];
                var touched = false;

                for (var i = 0; i < row.Count; i++)
                {
                    var columnIndex = targetColumnIndex + i;

                    if (columnIndex < 0 || columnIndex >= columns.Count)
                        continue;

                    var column = columns[columnIndex];

                    // A read-only column still consumes its clipboard cell, so the rest
                    // of the row stays aligned with the columns it came from.
                    if (!column.AllowEditing || !column.SupportsValueCommit ||
                        string.IsNullOrEmpty(column.MappingName))
                        continue;

                    if (PasteMode == GridPasteMode.FillEmptyOnly)
                    {
                        var existing = PropertyAccessor.GetValue(node.Item, column.MappingName);
                        if (!string.IsNullOrEmpty(existing?.ToString()))
                            continue;
                    }

                    if (writer(node, column, row[i]))
                    {
                        result.UpdatedCells++;
                        touched = true;
                    }
                    else
                    {
                        result.RejectedCells++;
                    }
                }

                if (touched)
                    result.RowsAffected++;

                rowIndex++;
            }

            return result;
        }

        /// <summary>
        /// Splits clipboard text into rows and cells.
        /// <para>
        /// Excel quotes any cell containing a tab or a line break, so splitting on the
        /// delimiters directly corrupts multi-line cells and shifts everything after
        /// them. This walks the text instead, honouring quotes and doubled quotes.
        /// </para>
        /// </summary>
        public static List<List<string>> ParseTabular(string text)
        {
            var rows = new List<List<string>>();

            if (string.IsNullOrEmpty(text))
                return rows;

            var row = new List<string>();
            var cell = new StringBuilder();
            var quoted = false;

            for (var i = 0; i < text.Length; i++)
            {
                var ch = text[i];

                if (quoted)
                {
                    if (ch != '"')
                    {
                        cell.Append(ch);
                        continue;
                    }

                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                        continue;
                    }

                    quoted = false;
                    continue;
                }

                switch (ch)
                {
                    case '"' when cell.Length == 0:
                        quoted = true;
                        break;

                    case '\t':
                        row.Add(cell.ToString());
                        cell.Clear();
                        break;

                    case '\r':
                        break;

                    case '\n':
                        row.Add(cell.ToString());
                        cell.Clear();
                        rows.Add(row);
                        row = new List<string>();
                        break;

                    default:
                        cell.Append(ch);
                        break;
                }
            }

            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row);
            }

            // Excel ends the block with a newline, leaving a trailing empty row.
            if (rows.Count > 0)
            {
                var last = rows[rows.Count - 1];
                if (last.Count == 1 && last[0].Length == 0)
                    rows.RemoveAt(rows.Count - 1);
            }

            return rows;
        }

        // ----------------------------------------------------------------- utils

        private List<TreeGridColumn> FilterColumns(IReadOnlyList<TreeGridColumn> columns)
        {
            var result = new List<TreeGridColumn>(columns.Count);

            foreach (var column in columns)
            {
                if (column.IsHidden && !CopyOptions.HasFlag(GridCopyOptions.IncludeHiddenColumns))
                    continue;

                result.Add(column);
            }

            return result;
        }

        private string GetCellText(TreeNode node, TreeGridColumn column)
        {
            if (string.IsNullOrEmpty(column.MappingName))
                return string.Empty;

            var value = PropertyAccessor.GetValue(node.Item, column.MappingName);

            return CopyOptions.HasFlag(GridCopyOptions.IncludeFormat)
                ? column.FormatValue(value)
                : value?.ToString() ?? string.Empty;
        }

        private static string Escape(string value, char delimiter)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (delimiter == '\t')
                return value.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ");

            var needsQuotes = value.IndexOf(delimiter) >= 0 ||
                              value.IndexOf('"') >= 0 ||
                              value.IndexOf('\n') >= 0;

            return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }

        private static string HtmlEncode(string value) =>
            string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        private static string Repeat(string value, int count)
        {
            var builder = new StringBuilder(value.Length * count);

            for (var i = 0; i < count; i++)
                builder.Append(value);

            return builder.ToString();
        }
    }
}
