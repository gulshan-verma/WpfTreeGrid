using System;
using System.Collections.Generic;
using System.IO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TreeGrid.Wpf.Columns;

namespace TreeGrid.Wpf.Export
{
    /// <summary>
    /// Renders the grid to PDF.
    /// <para>
    /// Column widths come from the grid's own resolved widths, scaled to the page.
    /// Using the on-screen proportions keeps the exported document recognisable as
    /// the thing the user was looking at.
    /// </para>
    /// </summary>
    public sealed class PdfExporter : IGridExporter
    {
        public PageSize PageSize { get; set; } = PageSizes.A4.Landscape();

        public float FontSize { get; set; } = 8f;

        public float Margin { get; set; } = 20f;

        /// <summary>Points of left padding applied per hierarchy level.</summary>
        public float IndentPerLevel { get; set; } = 10f;

        public bool ShowPageNumbers { get; set; } = true;

        public string HeaderBackgroundHex { get; set; } = "#F5F6F8";

        public string BorderHex { get; set; } = "#D6D9DE";

        public void Export(Stream stream, IReadOnlyList<TreeGridColumn> columns,
            IReadOnlyList<ExportRow> rows, GridExportOptions options)
        {
            // QuestPDF requires a licence declaration; the Community terms cover
            // open-source and small-business use. Respect a licence the host
            // application has already declared rather than overwriting it.
            QuestPDF.Settings.License ??= LicenseType.Community;

            var levelColumn = options.HierarchyStyle == HierarchyExportStyle.LevelColumn;
            var widths = ResolveWidths(columns);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSize);
                    page.Margin(Margin);
                    page.DefaultTextStyle(style => style.FontSize(FontSize));

                    if (!string.IsNullOrEmpty(options.Title))
                    {
                        page.Header()
                            .PaddingBottom(6)
                            .Text(options.Title)
                            .FontSize(FontSize + 5)
                            .SemiBold();
                    }

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(definition =>
                        {
                            if (levelColumn)
                                definition.ConstantColumn(28);

                            foreach (var width in widths)
                                definition.RelativeColumn(width);
                        });

                        if (options.IncludeHeaders)
                        {
                            table.Header(header =>
                            {
                                if (levelColumn)
                                    HeaderCell(header.Cell(), "Level");

                                foreach (var column in columns)
                                    HeaderCell(header.Cell(), column.ResolvedHeaderText);
                            });
                        }

                        foreach (var row in rows)
                        {
                            if (levelColumn)
                                BodyCell(table.Cell(), row.Level.ToString(), 0);

                            for (var i = 0; i < row.Texts.Count; i++)
                            {
                                var indent = i == 0 && options.HierarchyStyle == HierarchyExportStyle.Indent
                                    ? row.Level * IndentPerLevel
                                    : 0f;

                                BodyCell(table.Cell(), row.Texts[i], indent);
                            }
                        }
                    });

                    if (ShowPageNumbers)
                    {
                        page.Footer()
                            .AlignRight()
                            .Text(text =>
                            {
                                text.CurrentPageNumber();
                                text.Span(" / ");
                                text.TotalPages();
                            });
                    }
                });
            }).GeneratePdf(stream);
        }

        private void HeaderCell(IContainer cell, string text) =>
            cell.Background(HeaderBackgroundHex)
                .BorderBottom(1)
                .BorderColor(BorderHex)
                .Padding(4)
                .Text(text)
                .SemiBold();

        private void BodyCell(IContainer cell, string text, float indent) =>
            cell.BorderBottom(0.5f)
                .BorderColor(BorderHex)
                .PaddingVertical(3)
                .PaddingRight(4)
                .PaddingLeft(4 + indent)
                .Text(text ?? string.Empty);

        /// <summary>
        /// Converts on-screen pixel widths into relative units. Columns that were never
        /// measured fall back to an equal share rather than collapsing to nothing.
        /// </summary>
        private static List<float> ResolveWidths(IReadOnlyList<TreeGridColumn> columns)
        {
            var widths = new List<float>(columns.Count);
            var total = 0d;

            foreach (var column in columns)
                total += column.ActualWidth > 0 ? column.ActualWidth : 0;

            foreach (var column in columns)
            {
                if (total <= 0)
                {
                    widths.Add(1f);
                    continue;
                }

                var width = column.ActualWidth > 0 ? column.ActualWidth : total / columns.Count;
                widths.Add((float)Math.Max(0.3, width / total * columns.Count));
            }

            return widths;
        }
    }
}
