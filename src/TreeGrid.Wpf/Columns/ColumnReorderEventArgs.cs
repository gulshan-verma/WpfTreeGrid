using System;
using System.ComponentModel;

namespace TreeGrid.Wpf.Columns
{
    /// <summary>Raised before a drag moves a column. Cancel to leave it where it is.</summary>
    public sealed class ColumnReorderingEventArgs : CancelEventArgs
    {
        public ColumnReorderingEventArgs(TreeGridColumn column, int oldIndex, int newIndex)
        {
            Column = column;
            OldIndex = oldIndex;
            NewIndex = newIndex;
        }

        public TreeGridColumn Column { get; }

        /// <summary>Index in the Columns collection, not the visible index.</summary>
        public int OldIndex { get; }

        public int NewIndex { get; }
    }

    /// <summary>Raised after a column has moved, by drag or in code.</summary>
    public sealed class ColumnReorderedEventArgs : EventArgs
    {
        public ColumnReorderedEventArgs(TreeGridColumn column, int oldIndex, int newIndex)
        {
            Column = column;
            OldIndex = oldIndex;
            NewIndex = newIndex;
        }

        public TreeGridColumn Column { get; }

        public int OldIndex { get; }

        public int NewIndex { get; }
    }
}
