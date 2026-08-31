using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TreeGrid.Wpf.Data
{
    /// <summary>
    /// Wraps a single data item and carries its position in the hierarchy.
    /// Nodes are created lazily: a node's children are only materialised when it is
    /// first expanded (or when the source is fully in memory and eagerly walked).
    /// </summary>
    public sealed class TreeNode : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isLoading;
        private bool _hasChildNodes;
        private bool? _isChecked = false;
        private bool _isSelected;

        internal TreeNode(object item, TreeNode parent, int level)
        {
            Item = item;
            ParentNode = parent;
            Level = level;
            ChildNodes = new List<TreeNode>();
        }

        /// <summary>The underlying business object.</summary>
        public object Item { get; }

        public TreeNode ParentNode { get; internal set; }

        /// <summary>Zero-based depth. Root nodes are level 0.</summary>
        public int Level { get; internal set; }

        public List<TreeNode> ChildNodes { get; }

        /// <summary>
        /// Index into the flattened view, or -1 when the node is not currently visible
        /// (an ancestor is collapsed, or the node is filtered out). Maintained by
        /// <see cref="FlatTreeView"/> so index lookups stay O(1).
        /// </summary>
        public int FlatIndex { get; internal set; } = -1;

        /// <summary>
        /// Position among siblings as originally supplied by the source. Sorting
        /// reorders <see cref="ChildNodes"/>, so this is the only record of the
        /// source order and is what "no sort" restores.
        /// </summary>
        public int SourceIndex { get; internal set; }

        /// <summary>True once children have actually been fetched.</summary>
        public bool IsChildNodesPopulated { get; internal set; }

        /// <summary>Set to false by a filter pass; filtered nodes never enter the flat view.</summary>
        public bool IsFilteredOut { get; internal set; }

        /// <summary>
        /// Whether an expander should be drawn. For load-on-demand sources this can be
        /// true before any child has been fetched.
        /// </summary>
        public bool HasChildNodes
        {
            get => _hasChildNodes;
            internal set => Set(ref _hasChildNodes, value);
        }

        /// <summary>
        /// Do not set this directly to expand a node - call
        /// <see cref="FlatTreeView.Expand"/> so the flattened view stays in sync.
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            internal set => Set(ref _isExpanded, value);
        }

        /// <summary>True while an async load-on-demand fetch is in flight for this node.</summary>
        public bool IsLoading
        {
            get => _isLoading;
            internal set => Set(ref _isLoading, value);
        }

        /// <summary>Tri-state value used by the checkbox-selection feature (Phase 2).</summary>
        public bool? IsChecked
        {
            get => _isChecked;
            internal set => Set(ref _isChecked, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            internal set => Set(ref _isSelected, value);
        }

        public bool IsVisible => FlatIndex >= 0;

        /// <summary>Walks up the chain; used by filtering and by drag-drop validation.</summary>
        public bool IsDescendantOf(TreeNode candidate)
        {
            var current = ParentNode;
            while (current != null)
            {
                if (ReferenceEquals(current, candidate))
                    return true;
                current = current.ParentNode;
            }

            return false;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Set<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public override string ToString() => $"TreeNode(L{Level}, {Item})";
    }
}
