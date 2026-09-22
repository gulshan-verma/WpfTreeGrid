using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;

namespace TreeGrid.Wpf.Data
{
    public enum TreeGridBindingMode
    {
        /// <summary>Each item exposes a child collection named by ChildPropertyName.</summary>
        Hierarchical,

        /// <summary>Flat collection linked by Id / ParentId properties.</summary>
        SelfRelational,

        /// <summary>No source walk at all; children arrive through RequestTreeItems.</summary>
        Unbound
    }

    /// <summary>
    /// Turns an ItemsSource into a node tree and keeps it in sync. Owns the
    /// <see cref="FlatTreeView"/> and is the only class allowed to build nodes.
    /// </summary>
    public sealed class TreeGridDataSource
    {
        private readonly Dictionary<object, TreeNode> _nodeMap = new Dictionary<object, TreeNode>();
        private IEnumerable _itemsSource;
        private int _updateDepth;
        private CancellationTokenSource _loadCts;

        public TreeGridDataSource()
        {
            View = new FlatTreeView();
        }

        public FlatTreeView View { get; }

        public TreeGridBindingMode BindingMode { get; set; } = TreeGridBindingMode.Hierarchical;

        /// <summary>Property holding the child collection (Hierarchical mode).</summary>
        public string ChildPropertyName { get; set; }

        /// <summary>Property holding the node's own key (SelfRelational mode).</summary>
        public string IdPropertyName { get; set; }

        /// <summary>Property holding the parent's key (SelfRelational mode).</summary>
        public string ParentIdPropertyName { get; set; }

        /// <summary>Parent-id value that marks a root. Defaults to null.</summary>
        public object SelfRelationRootValue { get; set; }

        /// <summary>
        /// Called when an item's leaf-ness cannot be determined from the source, i.e.
        /// in Unbound mode. Return true to draw an expander before children are known.
        /// </summary>
        public Func<object, bool> HasChildNodesResolver { get; set; }

        /// <summary>Synchronous load-on-demand hook.</summary>
        public event EventHandler<RequestTreeItemsEventArgs> RequestTreeItems;

        /// <summary>
        /// Asynchronous load-on-demand hook. When set it takes priority over
        /// <see cref="RequestTreeItems"/>. The node shows a busy indicator until the
        /// task completes.
        /// </summary>
        public Func<TreeNode, CancellationToken, Task<IEnumerable>> RequestTreeItemsAsync { get; set; }

        public event EventHandler SourceReset;

        // ----------------------------------------------------------- source swap

        public void SetItemsSource(IEnumerable source)
        {
            DetachCollectionChanged(_itemsSource);
            _itemsSource = source;
            AttachCollectionChanged(_itemsSource);
            Reload();
        }

        public void Reload()
        {
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            _nodeMap.Clear();

            switch (BindingMode)
            {
                case TreeGridBindingMode.SelfRelational:
                    View.SetRoots(BuildSelfRelational(_itemsSource));
                    break;

                case TreeGridBindingMode.Unbound:
                    View.SetRoots(BuildUnboundRoots(_itemsSource));
                    break;

                default:
                    View.SetRoots(BuildHierarchical(_itemsSource, null, 0));
                    break;
            }

            SourceReset?.Invoke(this, EventArgs.Empty);
        }

        public TreeNode GetNode(object item)
        {
            if (item == null)
                return null;

            return _nodeMap.TryGetValue(item, out var node) ? node : null;
        }

        // --------------------------------------------------------------- builders

        private List<TreeNode> BuildHierarchical(IEnumerable source, TreeNode parent, int level)
        {
            var nodes = new List<TreeNode>();
            if (source == null)
                return nodes;

            foreach (var item in source)
            {
                if (item == null)
                    continue;

                var node = CreateNode(item, parent, level);
                var children = GetChildCollection(item);

                if (children != null)
                {
                    var childNodes = BuildHierarchical(children, node, level + 1);
                    node.ChildNodes.AddRange(childNodes);
                    node.IsChildNodesPopulated = true;
                    node.HasChildNodes = childNodes.Count > 0;
                }
                else
                {
                    node.IsChildNodesPopulated = true;
                    node.HasChildNodes = false;
                }

                node.SourceIndex = nodes.Count;
                nodes.Add(node);
            }

            return nodes;
        }

        private List<TreeNode> BuildSelfRelational(IEnumerable source)
        {
            var roots = new List<TreeNode>();
            if (source == null || string.IsNullOrEmpty(IdPropertyName) || string.IsNullOrEmpty(ParentIdPropertyName))
                return roots;

            var byId = new Dictionary<object, TreeNode>();
            var pendingParents = new List<(TreeNode node, object parentId)>();

            foreach (var item in source)
            {
                if (item == null)
                    continue;

                var node = CreateNode(item, null, 0);
                var id = PropertyAccessor.GetValue(item, IdPropertyName);

                if (id != null)
                    byId[id] = node;

                var parentId = PropertyAccessor.GetValue(item, ParentIdPropertyName);
                pendingParents.Add((node, parentId));
            }

            foreach (var (node, parentId) in pendingParents)
            {
                if (IsRootValue(parentId) || !byId.TryGetValue(parentId, out var parent))
                {
                    roots.Add(node);
                    continue;
                }

                // Guard against cycles in badly formed data.
                if (ReferenceEquals(parent, node) || parent.IsDescendantOf(node))
                {
                    roots.Add(node);
                    continue;
                }

                node.ParentNode = parent;
                node.SourceIndex = parent.ChildNodes.Count;
                parent.ChildNodes.Add(node);
                parent.HasChildNodes = true;
                parent.IsChildNodesPopulated = true;
            }

            for (var i = 0; i < roots.Count; i++)
                roots[i].SourceIndex = i;

            foreach (var root in roots)
                AssignLevels(root, 0);

            return roots;
        }

        private List<TreeNode> BuildUnboundRoots(IEnumerable source)
        {
            var nodes = new List<TreeNode>();
            if (source == null)
                return nodes;

            foreach (var item in source)
            {
                if (item == null)
                    continue;

                var node = CreateNode(item, null, 0);
                node.IsChildNodesPopulated = false;
                node.HasChildNodes = HasChildNodesResolver?.Invoke(item) ?? true;
                node.SourceIndex = nodes.Count;
                nodes.Add(node);
            }

            return nodes;
        }

        private static void AssignLevels(TreeNode node, int level)
        {
            node.Level = level;
            for (var i = 0; i < node.ChildNodes.Count; i++)
                AssignLevels(node.ChildNodes[i], level + 1);
        }

        private TreeNode CreateNode(object item, TreeNode parent, int level)
        {
            var node = new TreeNode(item, parent, level);
            _nodeMap[item] = node;
            return node;
        }

        private bool IsRootValue(object parentId)
        {
            if (parentId == null)
                return SelfRelationRootValue == null;

            return Equals(parentId, SelfRelationRootValue);
        }

        private IEnumerable GetChildCollection(object item)
        {
            if (string.IsNullOrEmpty(ChildPropertyName))
                return null;

            return PropertyAccessor.GetValue(item, ChildPropertyName) as IEnumerable;
        }

        // -------------------------------------------------------- load on demand

        public bool NeedsLoad(TreeNode node) =>
            node != null && node.HasChildNodes && !node.IsChildNodesPopulated;

        /// <summary>
        /// Fetches children for a node. Awaits the async hook when one is supplied,
        /// otherwise raises the synchronous event. Safe to call more than once - the
        /// second call is a no-op while a fetch is in flight.
        /// </summary>
        public async Task LoadChildrenAsync(TreeNode node)
        {
            if (node == null || node.IsLoading || node.IsChildNodesPopulated)
                return;

            node.IsLoading = true;

            try
            {
                IEnumerable children;

                if (RequestTreeItemsAsync != null)
                {
                    var token = _loadCts?.Token ?? CancellationToken.None;
                    children = await RequestTreeItemsAsync(node, token).ConfigureAwait(true);
                    if (token.IsCancellationRequested)
                        return;
                }
                else
                {
                    var args = new RequestTreeItemsEventArgs(node);
                    RequestTreeItems?.Invoke(this, args);
                    children = args.ChildItems;
                }

                PopulateChildren(node, children);
            }
            finally
            {
                node.IsLoading = false;
            }
        }

        public void PopulateChildren(TreeNode node, IEnumerable children)
        {
            node.ChildNodes.Clear();

            if (children != null)
            {
                foreach (var item in children)
                {
                    if (item == null)
                        continue;

                    var child = CreateNode(item, node, node.Level + 1);

                    if (BindingMode == TreeGridBindingMode.Unbound)
                    {
                        child.IsChildNodesPopulated = false;
                        child.HasChildNodes = HasChildNodesResolver?.Invoke(item) ?? true;
                    }
                    else
                    {
                        var grandChildren = GetChildCollection(item);
                        if (grandChildren != null)
                        {
                            var built = BuildHierarchical(grandChildren, child, child.Level + 1);
                            child.ChildNodes.AddRange(built);
                            child.HasChildNodes = built.Count > 0;
                        }

                        child.IsChildNodesPopulated = true;
                    }

                    child.SourceIndex = node.ChildNodes.Count;
                    node.ChildNodes.Add(child);
                }
            }

            node.IsChildNodesPopulated = true;
            node.HasChildNodes = node.ChildNodes.Count > 0;
            View.RefreshChildren(node);
        }

        // ---------------------------------------------------- collection changed

        private void AttachCollectionChanged(IEnumerable source)
        {
            if (source is INotifyCollectionChanged incc)
                incc.CollectionChanged += OnSourceCollectionChanged;
        }

        private void DetachCollectionChanged(IEnumerable source)
        {
            if (source is INotifyCollectionChanged incc)
                incc.CollectionChanged -= OnSourceCollectionChanged;
        }

        /// <summary>
        /// Raised when the source changed in a way that was handled incrementally, so
        /// the grid can rebuild the flat view without discarding selection or scroll.
        /// </summary>
        public event EventHandler IncrementalChange;

        /// <summary>
        /// Suspends reaction to source collection changes.
        /// <para>
        /// Used while the grid rewrites the source for a drop: each Remove and Insert
        /// would otherwise be processed incrementally, fighting the single rebuild that
        /// follows.
        /// </para>
        /// </summary>
        public void BeginSourceUpdate() => _updateDepth++;

        public void EndSourceUpdate()
        {
            if (_updateDepth > 0)
                _updateDepth--;
        }

        public bool IsSourceUpdateSuspended => _updateDepth > 0;

        private void OnSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_updateDepth > 0)
                return;

            // Self-relational sources cannot be patched incrementally: a single new row
            // can re-parent an arbitrary part of the tree, so a rebuild is the only
            // correct answer there.
            if (BindingMode == TreeGridBindingMode.SelfRelational)
            {
                Reload();
                return;
            }

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add when e.NewItems != null:
                    InsertRoots(e.NewItems, e.NewStartingIndex);
                    break;

                case NotifyCollectionChangedAction.Remove when e.OldItems != null:
                    RemoveRoots(e.OldItems);
                    break;

                case NotifyCollectionChangedAction.Move:
                    MoveRoot(e.OldStartingIndex, e.NewStartingIndex);
                    break;

                case NotifyCollectionChangedAction.Replace when e.OldItems != null && e.NewItems != null:
                    RemoveRoots(e.OldItems);
                    InsertRoots(e.NewItems, e.NewStartingIndex);
                    break;

                default:
                    Reload();
                    return;
            }

            IncrementalChange?.Invoke(this, EventArgs.Empty);
        }

        private void InsertRoots(IList items, int startIndex)
        {
            var roots = View.MutableRoots;
            var insertAt = startIndex < 0 || startIndex > roots.Count ? roots.Count : startIndex;

            foreach (var item in items)
            {
                if (item == null)
                    continue;

                var node = CreateNode(item, null, 0);

                if (BindingMode == TreeGridBindingMode.Unbound)
                {
                    node.IsChildNodesPopulated = false;
                    node.HasChildNodes = HasChildNodesResolver?.Invoke(item) ?? true;
                }
                else
                {
                    var children = GetChildCollection(item);

                    if (children != null)
                    {
                        var built = BuildHierarchical(children, node, 1);
                        node.ChildNodes.AddRange(built);
                        node.HasChildNodes = built.Count > 0;
                    }

                    node.IsChildNodesPopulated = true;
                }

                roots.Insert(Math.Min(insertAt, roots.Count), node);
                insertAt++;
            }

            ReindexRoots();
        }

        private void RemoveRoots(IList items)
        {
            var roots = View.MutableRoots;

            foreach (var item in items)
            {
                if (item == null)
                    continue;

                if (_nodeMap.TryGetValue(item, out var node))
                {
                    roots.Remove(node);
                    ForgetSubtree(node);
                }
            }

            ReindexRoots();
        }

        private void MoveRoot(int oldIndex, int newIndex)
        {
            var roots = View.MutableRoots;

            if (oldIndex < 0 || oldIndex >= roots.Count)
                return;

            var node = roots[oldIndex];
            roots.RemoveAt(oldIndex);
            roots.Insert(Math.Min(Math.Max(0, newIndex), roots.Count), node);

            ReindexRoots();
        }

        private void ReindexRoots()
        {
            var roots = View.MutableRoots;

            for (var i = 0; i < roots.Count; i++)
                roots[i].SourceIndex = i;
        }

        /// <summary>Drops a subtree from the item-to-node map so it can be collected.</summary>
        private void ForgetSubtree(TreeNode node)
        {
            if (node.Item != null)
                _nodeMap.Remove(node.Item);

            for (var i = 0; i < node.ChildNodes.Count; i++)
                ForgetSubtree(node.ChildNodes[i]);
        }
    }
}
