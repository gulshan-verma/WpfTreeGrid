using System;
using System.ComponentModel;
using TreeGrid.Wpf.Columns;

namespace TreeGrid.Wpf.Grouping
{
    public enum GroupingAction
    {
        /// <summary>A column was added to the grouping.</summary>
        Grouped,

        /// <summary>A column was removed from the grouping.</summary>
        Ungrouped,

        /// <summary>An existing level moved, changing precedence.</summary>
        Reordered,

        /// <summary>A level's ascending/descending order was flipped.</summary>
        SortDirectionChanged,

        /// <summary>All grouping was removed at once.</summary>
        Cleared
    }

    /// <summary>
    /// Raised before grouping changes. Cancel to refuse the change; nothing is applied
    /// and the panel is left untouched.
    /// </summary>
    public sealed class GroupingChangingEventArgs : CancelEventArgs
    {
        public GroupingChangingEventArgs(GroupingAction action, string columnName,
            TreeGridColumn column, int oldIndex, int newIndex)
        {
            Action = action;
            ColumnName = columnName;
            Column = column;
            OldIndex = oldIndex;
            NewIndex = newIndex;
        }

        public GroupingAction Action { get; }

        /// <summary>Mapping name of the affected column. Null for <see cref="GroupingAction.Cleared"/>.</summary>
        public string ColumnName { get; }

        /// <summary>The affected column, when it is still present on the grid.</summary>
        public TreeGridColumn Column { get; }

        /// <summary>Previous grouping position, or -1 when the column was not grouped.</summary>
        public int OldIndex { get; }

        /// <summary>Requested grouping position, or -1 when it is being removed.</summary>
        public int NewIndex { get; }
    }

    /// <summary>Raised after grouping has been applied and the view rebuilt.</summary>
    public sealed class GroupingChangedEventArgs : EventArgs
    {
        public GroupingChangedEventArgs(GroupingAction action, string columnName,
            TreeGridColumn column, int oldIndex, int newIndex, int groupCount)
        {
            Action = action;
            ColumnName = columnName;
            Column = column;
            OldIndex = oldIndex;
            NewIndex = newIndex;
            GroupLevelCount = groupCount;
        }

        public GroupingAction Action { get; }

        public string ColumnName { get; }

        public TreeGridColumn Column { get; }

        public int OldIndex { get; }

        public int NewIndex { get; }

        /// <summary>Number of grouping levels now active.</summary>
        public int GroupLevelCount { get; }
    }

    /// <summary>
    /// Raised while a column header is dragged over the group panel, once per move.
    /// Set <see cref="IsAllowed"/> false to refuse the drop at this position.
    /// </summary>
    public sealed class GroupDragOverEventArgs : EventArgs
    {
        public GroupDragOverEventArgs(TreeGridColumn column, int targetIndex)
        {
            Column = column;
            TargetIndex = targetIndex;
            IsAllowed = true;
        }

        public TreeGridColumn Column { get; }

        /// <summary>Grouping position the column would take if dropped now.</summary>
        public int TargetIndex { get; }

        public bool IsAllowed { get; set; }
    }

    /// <summary>Raised when a chip drag begins in the group panel.</summary>
    public sealed class GroupChipDragEventArgs : CancelEventArgs
    {
        public GroupChipDragEventArgs(string columnName, int index)
        {
            ColumnName = columnName;
            Index = index;
        }

        public string ColumnName { get; }

        public int Index { get; }
    }
}
