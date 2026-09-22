using System;
using System.Collections.Generic;
using System.ComponentModel;
using TreeGrid.Wpf.Columns;
using TreeGrid.Wpf.Data;

namespace TreeGrid.Wpf.Editing
{
    /// <summary>
    /// Raised before a committed value is pushed to the rest of the selection.
    /// <para>
    /// Cancel to apply the edit to the edited cell only. Set
    /// <see cref="SuppressConfirmation"/> when the handler has already asked the user,
    /// or wants no prompt at all.
    /// </para>
    /// </summary>
    public sealed class BulkValueChangingEventArgs : CancelEventArgs
    {
        public BulkValueChangingEventArgs(TreeGridColumn column, object newValue,
            IReadOnlyList<TreeNode> nodes, int distinctValueCount)
        {
            Column = column;
            NewValue = newValue;
            Nodes = nodes;
            DistinctValueCount = distinctValueCount;
        }

        public TreeGridColumn Column { get; }

        /// <summary>The value being applied, already coerced by the column's editor.</summary>
        public object NewValue { get; }

        /// <summary>Rows that will receive the value. Excludes the edited row.</summary>
        public IReadOnlyList<TreeNode> Nodes { get; }

        /// <summary>How many distinct values the affected cells currently hold.</summary>
        public int DistinctValueCount { get; }

        /// <summary>True when the affected cells do not all share one value.</summary>
        public bool HasMixedValues => DistinctValueCount > 1;

        /// <summary>Set true to skip the grid's built-in confirmation prompt.</summary>
        public bool SuppressConfirmation { get; set; }
    }

    /// <summary>Raised after a bulk update, whether or not every row accepted the value.</summary>
    public sealed class BulkValueChangedEventArgs : EventArgs
    {
        public BulkValueChangedEventArgs(TreeGridColumn column, object newValue,
            int updatedCount, IReadOnlyList<TreeNode> rejected)
        {
            Column = column;
            NewValue = newValue;
            UpdatedCount = updatedCount;
            RejectedNodes = rejected;
        }

        public TreeGridColumn Column { get; }

        public object NewValue { get; }

        /// <summary>Rows that accepted the value, excluding the edited row.</summary>
        public int UpdatedCount { get; }

        /// <summary>Rows whose validation refused the value; their errors are already recorded.</summary>
        public IReadOnlyList<TreeNode> RejectedNodes { get; }
    }
}
