# TreeGrid.Wpf.Export

Excel (.xlsx) and PDF exporters for the
[`TreeGrid.Wpf`](https://www.nuget.org/packages/TreeGrid.Wpf) control. Excel export uses
ClosedXML and PDF export uses QuestPDF. The core package can already export CSV, so you
only need this package for xlsx or pdf.

## Install

```
dotnet add package TreeGrid.Wpf.Export
```

## Usage

```csharp
using TreeGrid.Wpf.Export;

var options = new GridExportOptions
{
    Title = "Staff hierarchy",
    RowScope = ExportRowScope.VisibleRows,
    HierarchyStyle = HierarchyExportStyle.Outline
};

grid.Export(new ExcelExporter(), "staff.xlsx", options);
grid.Export(new PdfExporter(), "staff.pdf", options);
```

| Option | Values |
|---|---|
| `RowScope` | `VisibleRows`, `AllRows`, `SelectedRows` |
| `HierarchyStyle` | `Indent`, `LevelColumn`, `Outline`, `None` |
| Also | `IncludeHeaders`, `IncludeHiddenColumns`, `ApplyDisplayFormat`, `RowFilter` |

`Outline` maps the tree onto Excel's own row grouping, so the collapse controls in the
sheet margin match the grid's expanders. Excel cells keep their data types instead of
being written as text, so sorting and totals still work in Excel.

## QuestPDF licensing

QuestPDF is free under its Community license for individuals, open-source projects and
companies below its revenue threshold. Larger companies need a commercial QuestPDF
license. If your application has not set `QuestPDF.Settings.License` before the first
PDF export, `PdfExporter` sets it to Community. If you set a different license first,
`PdfExporter` keeps it. See [questpdf.com/license](https://www.questpdf.com/license/)
for the terms.

## License

MIT (this package). ClosedXML is MIT licensed. QuestPDF has its own license, described above.
