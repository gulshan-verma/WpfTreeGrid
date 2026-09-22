# TreeGrid.Wpf

A multi-column tree grid for WPF on .NET 8. Hierarchical data binding, row and column
virtualization, sorting, Excel-style filtering, editing with validation, frozen panes,
stacked headers, grouping, footers, drag-and-drop, clipboard interop and CSV export.

The library has no external dependencies. For Excel and PDF export, add the optional
[`TreeGrid.Wpf.Export`](https://www.nuget.org/packages/TreeGrid.Wpf.Export) package.

## Install

```
dotnet add package TreeGrid.Wpf
```

## Quick start

Add these XML namespaces to your window or user control:

```xml
xmlns:tg="clr-namespace:TreeGrid.Wpf;assembly=TreeGrid.Wpf"
xmlns:cols="clr-namespace:TreeGrid.Wpf.Columns;assembly=TreeGrid.Wpf"
```

Declare the grid and point `ChildPropertyName` at the collection that holds each item's children:

```xml
<tg:TreeGridControl ItemsSource="{Binding Staff}"
                    ChildPropertyName="Children"
                    AllowSorting="True"
                    AllowFiltering="True"
                    FrozenColumnCount="1">
    <tg:TreeGridControl.Columns>
        <cols:TreeGridTextColumn MappingName="FirstName" HeaderText="First Name" Width="220" />
        <cols:TreeGridNumericColumn MappingName="Salary" HeaderText="Salary" Width="110" />
        <cols:TreeGridProgressBarColumn MappingName="Completion" HeaderText="Progress" />
    </tg:TreeGridControl.Columns>
</tg:TreeGridControl>
```

```csharp
public class Employee
{
    public string FirstName { get; set; }
    public double Salary { get; set; }
    public ObservableCollection<Employee> Children { get; set; } = new();
}
```

CSV export is built in:

```csharp
using TreeGrid.Wpf.Export; // options types ship in the core package

grid.ExportToCsv("staff.csv", new GridExportOptions { RowScope = ExportRowScope.VisibleRows });
```

## Documentation

The full guide covers columns, selection, editing and validation, theming, the keyboard
reference and the events reference. It is in the
[GitHub repository](https://github.com/gulshan-verma/WpfTreeGrid#readme), which also
has a runnable demo app.

## License

MIT
