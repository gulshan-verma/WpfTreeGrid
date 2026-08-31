using System;
using System.Windows;

namespace TreeGrid.Wpf.Columns
{
    /// <summary>
    /// Renders a bound boolean as a checkbox. Distinct from the grid-level
    /// <c>AllowCheckBoxSelection</c> feature, which puts a node checkbox in the
    /// expander column and drives the hierarchy cascade.
    /// </summary>
    public class TreeGridCheckBoxColumn : TreeGridColumn
    {
        public static readonly DependencyProperty IsThreeStateProperty = DependencyProperty.Register(
            nameof(IsThreeState), typeof(bool), typeof(TreeGridCheckBoxColumn), new PropertyMetadata(false));

        public bool IsThreeState
        {
            get => (bool)GetValue(IsThreeStateProperty);
            set => SetValue(IsThreeStateProperty, value);
        }

        public override string FormatValue(object value) => string.Empty;

        /// <summary>Coerces the bound value into a nullable bool for the checkbox.</summary>
        public bool? ToCheckState(object value)
        {
            switch (value)
            {
                case bool b:
                    return b;
                case null:
                    return IsThreeState ? (bool?)null : false;
                default:
                    return bool.TryParse(value.ToString(), out var parsed) ? parsed : (bool?)null;
            }
        }
    }

    public sealed class ColumnResizeEventArgs : RoutedEventArgs
    {
        public ColumnResizeEventArgs(RoutedEvent routedEvent, TreeGridColumn column, double delta, bool isCompleted)
            : base(routedEvent)
        {
            Column = column;
            Delta = delta;
            IsCompleted = isCompleted;
        }

        public TreeGridColumn Column { get; }

        public double Delta { get; }

        public bool IsCompleted { get; }
    }

    public sealed class ColumnAutoFitEventArgs : RoutedEventArgs
    {
        public ColumnAutoFitEventArgs(RoutedEvent routedEvent, TreeGridColumn column) : base(routedEvent)
        {
            Column = column;
        }

        public TreeGridColumn Column { get; }
    }

    public sealed class ColumnDragEventArgs : RoutedEventArgs
    {
        public ColumnDragEventArgs(RoutedEvent routedEvent, TreeGridColumn column, Point screenPoint, ColumnDragPhase phase)
            : base(routedEvent)
        {
            Column = column;
            ScreenPoint = screenPoint;
            Phase = phase;
        }

        public TreeGridColumn Column { get; }

        public Point ScreenPoint { get; }

        public ColumnDragPhase Phase { get; }
    }

    public enum ColumnDragPhase
    {
        Started,
        Moved,
        Completed,
        Cancelled
    }
}
