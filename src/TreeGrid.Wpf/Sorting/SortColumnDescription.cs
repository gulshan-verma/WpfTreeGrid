using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace TreeGrid.Wpf.Sorting
{
    /// <summary>Describes one level of a multi-column sort.</summary>
    public class SortColumnDescription : DependencyObject
    {
        public static readonly DependencyProperty ColumnNameProperty = DependencyProperty.Register(
            nameof(ColumnName), typeof(string), typeof(SortColumnDescription), new PropertyMetadata(null));

        public static readonly DependencyProperty SortDirectionProperty = DependencyProperty.Register(
            nameof(SortDirection), typeof(ListSortDirection), typeof(SortColumnDescription),
            new PropertyMetadata(ListSortDirection.Ascending));

        /// <summary>Mapping name of the column being sorted.</summary>
        public string ColumnName
        {
            get => (string)GetValue(ColumnNameProperty);
            set => SetValue(ColumnNameProperty, value);
        }

        public ListSortDirection SortDirection
        {
            get => (ListSortDirection)GetValue(SortDirectionProperty);
            set => SetValue(SortDirectionProperty, value);
        }

        public override string ToString() => $"{ColumnName} {SortDirection}";
    }

    public sealed class SortColumnDescriptions : ObservableCollection<SortColumnDescription>
    {
        public SortColumnDescription Find(string columnName)
        {
            foreach (var description in this)
            {
                if (string.Equals(description.ColumnName, columnName, System.StringComparison.Ordinal))
                    return description;
            }

            return null;
        }
    }

    /// <summary>
    /// Supplies custom ordering for a column. Registered per mapping name on the grid;
    /// values handed to <see cref="System.Collections.IComparer.Compare"/> are the raw
    /// cell values, not the data items.
    /// </summary>
    public sealed class SortComparer
    {
        public string ColumnName { get; set; }

        public IComparer<object> Comparer { get; set; }
    }

    public sealed class SortComparers : ObservableCollection<SortComparer>
    {
        public IComparer<object> Find(string columnName)
        {
            foreach (var entry in this)
            {
                if (string.Equals(entry.ColumnName, columnName, System.StringComparison.Ordinal))
                    return entry.Comparer;
            }

            return null;
        }
    }
}
