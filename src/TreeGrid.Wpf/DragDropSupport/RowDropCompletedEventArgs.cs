using System;
using System.Collections.Generic;
using TreeGrid.Wpf.Data;

namespace TreeGrid.Wpf.DragDropSupport
{
    /// <summary>
    /// Raised once a drop is completely finished: the source collections have been
    /// updated and the view rebuilt.
    /// <para>
    /// Distinct from <c>RowDropped</c>, which fires before any relocation so a handler
    /// can take over. By the time this runs the hierarchy is settled, so the nodes here
    /// are the freshly built ones, not the ones that were dragged.
    /// </para>
    /// </summary>
    public sealed class RowDropCompletedEventArgs : EventArgs
    {
        public RowDropCompletedEventArgs(IReadOnlyList<TreeNode> nodes, IReadOnlyList<object> items,
            TreeNode parentNode, RowDropPosition position, bool handledByHost, bool sourceUpdated)
        {
            Nodes = nodes;
            Items = items;
            ParentNode = parentNode;
            Position = position;
            HandledByHost = handledByHost;
            SourceUpdated = sourceUpdated;
        }

        /// <summary>The moved rows, resolved against the rebuilt tree.</summary>
        public IReadOnlyList<TreeNode> Nodes { get; }

        /// <summary>The moved records, in the order they were dragged.</summary>
        public IReadOnlyList<object> Items { get; }

        /// <summary>The parent the rows now sit under. Null when they became roots.</summary>
        public TreeNode ParentNode { get; }

        /// <summary>The parent record, or null at root level.</summary>
        public object ParentItem => ParentNode?.Item;

        public RowDropPosition Position { get; }

        /// <summary>True when a handler relocated the records rather than the grid.</summary>
        public bool HandledByHost { get; }

        /// <summary>True when the grid rewrote the child collections or parent keys.</summary>
        public bool SourceUpdated { get; }
    }
}
