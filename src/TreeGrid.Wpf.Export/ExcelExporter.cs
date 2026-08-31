using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using TreeGrid.Wpf.Columns;

namespace TreeGrid.Wpf.Export
{
    /// <summary>
    /// Writes an .xlsx workbook.
    /// <para>
    /// With <see cref="HierarchyExportStyle.Outline"/> the tree becomes Excel's own
    /// row grouping, so the collapse controls in the sheet margin mirror the grid's
    /// expanders. That is the only style that keeps the hierarchy genuinely
    /// interactive after export.
    /// </para>
    /// </summary>
    public sealed class ExcelExporter : IGridExporter
    {
        /// <summary>Freeze the header row in the produced sheet.</summary>
        public bool FreezeHeaderRow { get; set; } = true;

        public bool AutoFitColumns { get; set; } = true;

        /// <summary>Add Excel's filter dropdowns to the header row.</summary>
        public bool AddAutoFilter { get; set; } = true;

        public XLColor HeaderBackground { get; set; } = XLColor.FromHtml("#F5F6F8");

        public void Export(Stream stream, IReadOnlyList<TreeGridColumn> columns,
            IReadOnlyList<ExportRow> rows, GridExportOptions options)
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add(
                    string.IsNullOrWhiteSpace(options.SheetName) ? "TreeGrid" : options.SheetName);

                var currentRow = 1;

                if (!string.IsNullOrEmpty(options.Title))
                {
                    var titleCell = sheet.Cell(currentRow, 1);
                    titleCell.Value = options.Title;
                    titleCell.Style.Font.Bold = true;
                    titleCell.Style.Font.FontSize = 14;

                    sheet.Range(currentRow, 1, currentRow, Math.Max(1, columns.Count)).Merge();
                    currentRow += 2;
                }

                var headerRow = currentRow;
                var levelColumn = options.HierarchyStyle == HierarchyExportStyle.LevelColumn;
                var columnOffset = levelColumn ? 2 : 1;

                if (options.IncludeHeaders)
                {
                    if (levelColumn)
                        WriteHeaderCell(sheet.Cell(currentRow, 1), "Level");

                    for (var i = 0; i < columns.Count; i++)
                        WriteHeaderCell(sheet.Cell(currentRow, i + columnOffset), columns[i].ResolvedHeaderText);

                    currentRow++;
                }

                var firstDataRow = currentRow;

                foreach (var row in rows)
                {
                    if (levelColumn)
                        sheet.Cell(currentRow, 1).Value = row.Level;

                    for (var i = 0; i < row.Values.Count; i++)
                    {
                        var cell = sheet.Cell(currentRow, i + columnOffset);
                        WriteValue(cell, row.Values[i], row.Texts[i], columns[i], options);

                        if (i == 0 && options.HierarchyStyle == HierarchyExportStyle.Indent && row.Level > 0)
                            cell.Style.Alignment.Indent = row.Level;
                    }

                    // Excel's outline levels are 1-based and cap at 7; anything deeper
                    // is clamped rather than rejected by the writer.
                    if (options.HierarchyStyle == HierarchyExportStyle.Outline && row.Level > 0)
                        sheet.Row(currentRow).OutlineLevel = Math.Min(7, row.Level);

                    currentRow++;
                }

                var lastDataRow = currentRow - 1;

                if (options.IncludeHeaders && AddAutoFilter && lastDataRow >= firstDataRow)
                {
                    sheet.Range(headerRow, 1, lastDataRow, columns.Count + columnOffset - 1)
                         .SetAutoFilter();
                }

                if (options.IncludeHeaders && FreezeHeaderRow)
                    sheet.SheetView.FreezeRows(headerRow);

                if (AutoFitColumns)
                    sheet.Columns().AdjustToContents();

                workbook.SaveAs(stream);
            }
        }

        private void WriteHeaderCell(IXLCell cell, string text)
        {
            cell.Value = text;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = HeaderBackground;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        /// <summary>
        /// Writes the typed value where possible so Excel can sort and total the
        /// column, falling back to the formatted string only for values it cannot
        /// represent natively.
        /// </summary>
        private static void WriteValue(IXLCell cell, object value, string text,
            TreeGridColumn column, GridExportOptions options)
        {
            if (value == null)
            {
                cell.Value = string.Empty;
                return;
            }

            switch (value)
            {
                case bool boolean:
                    cell.Value = boolean;
                    return;

                case DateTime date:
                    cell.Value = date;
                    if (!string.IsNullOrEmpty(column.DisplayFormat))
                        cell.Style.DateFormat.Format = "yyyy-mm-dd";
                    return;

                case byte _:
                case short _:
                case int _:
                case long _:
                case float _:
                case double _:
                case decimal _:
                    cell.Value = Convert.ToDouble(value);

                    if (column is TreeGridNumericColumn numeric)
                        cell.Style.NumberFormat.Format = "#,##0." + new string('0', Math.Max(0, numeric.NumberDecimalDigits));

                    return;

                default:
                    cell.Value = options.ApplyDisplayFormat ? text : value.ToString();
                    return;
            }
        }
    }
}
