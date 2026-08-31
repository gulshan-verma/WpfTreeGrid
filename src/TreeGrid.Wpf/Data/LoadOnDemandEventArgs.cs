using System;
using System.Collections;
using System.ComponentModel;

namespace TreeGrid.Wpf.Data
{
    /// <summary>
    /// Raised the first time a node is expanded when the grid is in unbound
    /// (load-on-demand) mode. Handlers set <see cref="ChildItems"/>.
    /// </summary>
    public sealed class RequestTreeItemsEventArgs : EventArgs
    {
        internal RequestTreeItemsEventArgs(TreeNode node)
        {
            Node = node;
        }

        public TreeNode Node { get; }

        /// <summary>Null for root-level requests.</summary>
        public object ParentItem => Node?.Item;

        /// <summary>Set by the handler. Null or empty means the node turns into a leaf.</summary>
        public IEnumerable ChildItems { get; set; }
    }

    public sealed class NodeExpandingEventArgs : CancelEventArgs
    {
        internal NodeExpandingEventArgs(TreeNode node) => Node = node;
        public TreeNode Node { get; }
    }

    public sealed class NodeExpandedEventArgs : EventArgs
    {
        internal NodeExpandedEventArgs(TreeNode node) => Node = node;
        public TreeNode Node { get; }
    }

    public sealed class NodeCollapsingEventArgs : CancelEventArgs
    {
        internal NodeCollapsingEventArgs(TreeNode node) => Node = node;
        public TreeNode Node { get; }
    }

    public sealed class NodeCollapsedEventArgs : EventArgs
    {
        internal NodeCollapsedEventArgs(TreeNode node) => Node = node;
        public TreeNode Node { get; }
    }
}
