using System;
using System.Collections.Generic;
using System.ComponentModel;
using TreeGrid.Wpf.Data;

namespace TreeGrid.Wpf.Editing
{
    /// <summary>
    /// Raised before selected rows are removed. Cancel to refuse the delete outright.
    /// <para>
    /// Set <see cref="HandledByHost"/> when you have removed the records yourself; the
    /// grid then reloads from the source rather than attempting removal itself, which
    /// is what a view model owning its own collections normally wants.
    /// </para>
    /// </summary>
    public sealed class RowsDeletingEventArgs : CancelEventArgs
    {
        public RowsDeletingEventArgs(IReadOnlyList<TreeNode> nodes, IReadOnlyList<object> items)
        {
            Nodes = nodes;
            Items = items;
        }

        /// <summary>Top-level nodes being removed. Descendants are implied, not listed.</summary>
        public IReadOnlyList<TreeNode> Nodes { get; }

        public IReadOnlyList<object> Items { get; }

        /// <summary>Records removed once descendants are counted.</summary>
        public int TotalAffected { get; internal set; }

        /// <summary>True when the host removed the data itself.</summary>
        public bool HandledByHost { get; set; }

        /// <summary>Set true to skip the grid's confirmation prompt.</summary>
        public bool SuppressConfirmation { get; set; }
    }

    public sealed class RowsDeletedEventArgs : EventArgs
    {
        public RowsDeletedEventArgs(IReadOnlyList<object> items, int removedCount, bool handledByHost)
        {
            Items = items;
            RemovedCount = removedCount;
            HandledByHost = handledByHost;
        }

        public IReadOnlyList<object> Items { get; }

        /// <summary>Rows the grid actually removed from the source collections.</summary>
        public int RemovedCount { get; }

        public bool HandledByHost { get; }
    }
}
