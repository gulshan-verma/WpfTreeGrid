using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using TreeGrid.Wpf.Columns;

namespace TreeGrid.Wpf.Headers
{
    /// <summary>
    /// One spanning header cell. <see cref="ChildColumns"/> is a comma-separated list
    /// of mapping names, matching the convention most grids use in XAML.
    /// </summary>
    public class StackedColumn : DependencyObject
    {
        public static readonly DependencyProperty HeaderTextProperty = DependencyProperty.Register(
            nameof(HeaderText), typeof(string), typeof(StackedColumn), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ChildColumnsProperty = DependencyProperty.Register(
            nameof(ChildColumns), typeof(string), typeof(StackedColumn), new PropertyMetadata(null));

        public string HeaderText
        {
            get => (string)GetValue(HeaderTextProperty);
            set => SetValue(HeaderTextProperty, value);
        }

        /// <summary>Comma-separated mapping names, e.g. "FirstName,LastName".</summary>
        public string ChildColumns
        {
            get => (string)GetValue(ChildColumnsProperty);
            set => SetValue(ChildColumnsProperty, value);
        }

        public IReadOnlyList<string> GetChildMappingNames()
        {
            if (string.IsNullOrWhiteSpace(ChildColumns))
                return Array.Empty<string>();

            var parts = ChildColumns.Split(',');
            var result = new List<string>(parts.Length);

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Length > 0)
                    result.Add(trimmed);
            }

            return result;
        }
    }

    public sealed class StackedColumns : ObservableCollection<StackedColumn>
    {
    }

    public class StackedHeaderRow : DependencyObject
    {
        public StackedHeaderRow()
        {
            StackedColumns = new StackedColumns();
        }

        public StackedColumns StackedColumns { get; }
    }

    public sealed class StackedHeaderRows : ObservableCollection<StackedHeaderRow>
    {
    }

    /// <summary>Resolved geometry for one stacked header cell in a given layout pass.</summary>
    public sealed class StackedHeaderSpan
    {
        public StackedHeaderSpan(string headerText, int firstColumnIndex, int lastColumnIndex)
        {
            HeaderText = headerText;
            FirstColumnIndex = firstColumnIndex;
            LastColumnIndex = lastColumnIndex;
        }

        public string HeaderText { get; }

        public int FirstColumnIndex { get; }

        public int LastColumnIndex { get; }

        public double Left { get; internal set; }

        public double Width { get; internal set; }

        /// <summary>True when every column in the span is inside the left frozen band.</summary>
        public bool IsFrozen { get; internal set; }
    }
}
