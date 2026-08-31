using System;
using System.Collections;
using System.Collections.Generic;

namespace TreeGrid.Wpf.Data
{
    public sealed class FlatTreeViewChangedEventArgs : EventArgs
    {
        public FlatTreeViewChangedEventArgs(int startIndex, int removedCount, int addedCount)
        {
            StartIndex = startIndex;
            RemovedCount = removedCount;
            AddedCount = addedCount;
        }

        public int StartIndex { get; }
        public int RemovedCount { get; }
        public int AddedCount { get; }

        /// <summary>True for wholesale rebuilds (source swap, re-sort, re-filter).</summary>
        public bool IsReset { get; internal set; }
    }

    /// <summary>
    /// Maintains the flattened, currently-visible projection of the tree.
    /// <para>
    /// This is the piece everything else stands on: the virtualizer asks it for
    /// "node at row N", and expand/collapse is a splice into a single
    /// <see cref="List{T}"/> rather than a rebuild. Expanding a node costs
    /// O(visible descendants + tail reindex), not O(total nodes).
    /// </para>
    /// </summary>
    public sealed class FlatTreeView : IReadOnlyList<TreeNode>
    {
        private readonly List<TreeNode> _flat = new List<TreeNode>();
        private readonly List<TreeNode> _roots = new List<TreeNode>();
        private readonly List<TreeNode> _scratch = new List<TreeNode>();

        public event EventHandler<FlatTreeViewChangedEventArgs> Changed;

        public int Count => _flat.Count;

        public TreeNode this[int index] => _flat[index];

        public IReadOnlyList<TreeNode> RootNodes => _roots;

        /// <summary>
        /// Direct access for the data source's incremental updates. Callers must call
        /// <see cref="Rebuild"/> afterwards; nothing else should touch this.
        /// </summary>
        internal List<TreeNode> MutableRoots => _roots;

        public IEnumerator<TreeNode> GetEnumerator() => _flat.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public int IndexOf(TreeNode node) => node?.FlatIndex ?? -1;

        public TreeNode FindNode(object item)
        {
            // Linear over visible rows; callers that need this on hot paths should
            // keep their own item -> node map (see TreeGridDataSource).
            for (var i = 0; i < _flat.Count; i++)
            {
                if (Equals(_flat[i].Item, item))
                    return _flat[i];
            }

            return null;
        }

        // ---------------------------------------------------------------- build

        public void SetRoots(IEnumerable<TreeNode> roots)
        {
            _roots.Clear();
            _flat.Clear();

            if (roots != null)
                _roots.AddRange(roots);

            foreach (var root in _roots)
            {
                root.ParentNode = null;
                root.Level = 0;
            }

            Rebuild();
        }

        /// <summary>
        /// Rebuilds the whole flat list from the root collection, honouring current
        /// expansion and filter state. Used after sorting, filtering or a source swap.
        /// </summary>
        public void Rebuild()
        {
            var previous = _flat.Count;
            _flat.Clear();

            foreach (var root in _roots)
                AppendVisible(root, _flat);

            Reindex(0);
            Changed?.Invoke(this, new FlatTreeViewChangedEventArgs(0, previous, _flat.Count) { IsReset = true });
        }

        private void AppendVisible(TreeNode node, List<TreeNode> target)
        {
            if (node.IsFilteredOut)
                return;

            target.Add(node);

            if (!node.IsExpanded)
                return;

            for (var i = 0; i < node.ChildNodes.Count; i++)
                AppendVisible(node.ChildNodes[i], target);
        }

        // ------------------------------------------------------- expand/collapse

        /// <summary>
        /// Makes the node's visible descendants part of the flat view.
        /// Returns false when the node is already expanded or has nothing to show.
        /// </summary>
        public bool Expand(TreeNode node)
        {
            if (node == null || node.IsExpanded)
                return false;

            node.IsExpanded = true;

            if (node.FlatIndex < 0)
                return true; // Not visible; state recorded for when an ancestor opens.

            _scratch.Clear();
            for (var i = 0; i < node.ChildNodes.Count; i++)
                AppendVisible(node.ChildNodes[i], _scratch);

            if (_scratch.Count == 0)
                return true;

            var insertAt = node.FlatIndex + 1;
            _flat.InsertRange(insertAt, _scratch);
            Reindex(insertAt);

            Changed?.Invoke(this, new FlatTreeViewChangedEventArgs(insertAt, 0, _scratch.Count));
            _scratch.Clear();
            return true;
        }

        /// <summary>Removes the node's descendants from the flat view, keeping their own expansion state.</summary>
        public bool Collapse(TreeNode node)
        {
            if (node == null || !node.IsExpanded)
                return false;

            node.IsExpanded = false;

            if (node.FlatIndex < 0)
                return true;

            var start = node.FlatIndex + 1;
            var count = CountVisibleDescendants(node);

            if (count == 0)
                return true;

            for (var i = start; i < start + count; i++)
                _flat[i].FlatIndex = -1;

            _flat.RemoveRange(start, count);
            Reindex(start);

            Changed?.Invoke(this, new FlatTreeViewChangedEventArgs(start, count, 0));
            return true;
        }

        public void Toggle(TreeNode node)
        {
            if (node == null)
                return;

            if (node.IsExpanded)
                Collapse(node);
            else
                Expand(node);
        }

        public void ExpandAll(int maxLevel = int.MaxValue)
        {
            foreach (var root in _roots)
                SetExpandedRecursive(root, true, maxLevel);

            Rebuild();
        }

        public void CollapseAll()
        {
            foreach (var root in _roots)
                SetExpandedRecursive(root, false, int.MaxValue);

            Rebuild();
        }

        private static void SetExpandedRecursive(TreeNode node, bool expanded, int maxLevel)
        {
            if (node.Level >= maxLevel)
                return;

            if (node.HasChildNodes && node.IsChildNodesPopulated)
                node.IsExpanded = expanded;

            for (var i = 0; i < node.ChildNodes.Count; i++)
                SetExpandedRecursive(node.ChildNodes[i], expanded, maxLevel);
        }

        /// <summary>Opens every collapsed ancestor so the node becomes visible.</summary>
        public void EnsureVisible(TreeNode node)
        {
            if (node == null)
                return;

            var chain = new Stack<TreeNode>();
            var current = node.ParentNode;

            while (current != null)
            {
                if (!current.IsExpanded)
                    chain.Push(current);
                current = current.ParentNode;
            }

            while (chain.Count > 0)
                Expand(chain.Pop());
        }

        // ---------------------------------------------------------------- sorting

        /// <summary>
        /// Sorts every sibling group with the same comparison, then rebuilds.
        /// Sorting the flat list directly would separate children from their parents,
        /// so the hierarchy is sorted level by level instead.
        /// </summary>
        public void SortHierarchy(Comparison<TreeNode> comparison)
        {
            if (comparison == null)
                return;

            SortSiblings(_roots, comparison);

            for (var i = 0; i < _roots.Count; i++)
                SortRecursive(_roots[i], comparison);

            Rebuild();
        }

        /// <summary>Sorts one node's children in place, without a full rebuild.</summary>
        public void SortSubtree(TreeNode node, Comparison<TreeNode> comparison)
        {
            if (node == null || comparison == null)
                return;

            SortRecursive(node, comparison);
        }

        private static void SortRecursive(TreeNode node, Comparison<TreeNode> comparison)
        {
            if (node.ChildNodes.Count > 1)
                SortSiblings(node.ChildNodes, comparison);

            for (var i = 0; i < node.ChildNodes.Count; i++)
                SortRecursive(node.ChildNodes[i], comparison);
        }

        private static void SortSiblings(List<TreeNode> nodes, Comparison<TreeNode> comparison)
        {
            if (nodes.Count < 2)
                return;

            // Stable: equal rows must not shuffle between successive sorts.
            var indexed = new KeyValuePair<int, TreeNode>[nodes.Count];
            for (var i = 0; i < nodes.Count; i++)
                indexed[i] = new KeyValuePair<int, TreeNode>(i, nodes[i]);

            Array.Sort(indexed, (a, b) =>
            {
                var result = comparison(a.Value, b.Value);
                return result != 0 ? result : a.Key.CompareTo(b.Key);
            });

            for (var i = 0; i < indexed.Length; i++)
                nodes[i] = indexed[i].Value;
        }

        // ----------------------------------------------------- structural edits

        /// <summary>
        /// Called after load-on-demand supplies children for an already-expanded node,
        /// or after an insert/remove on a child collection.
        /// </summary>
        public void RefreshChildren(TreeNode node)
        {
            if (node == null || node.FlatIndex < 0)
                return;

            if (!node.IsExpanded)
                return;

            var start = node.FlatIndex + 1;
            var oldCount = CountVisibleDescendants(node);

            for (var i = start; i < start + oldCount; i++)
                _flat[i].FlatIndex = -1;

            if (oldCount > 0)
                _flat.RemoveRange(start, oldCount);

            _scratch.Clear();
            for (var i = 0; i < node.ChildNodes.Count; i++)
                AppendVisible(node.ChildNodes[i], _scratch);

            if (_scratch.Count > 0)
                _flat.InsertRange(start, _scratch);

            Reindex(start);
            Changed?.Invoke(this, new FlatTreeViewChangedEventArgs(start, oldCount, _scratch.Count));
            _scratch.Clear();
        }

        /// <summary>
        /// Number of rows the node currently occupies in the flat view, excluding itself.
        /// Walks the flat list by level rather than the tree, which is cheaper and
        /// naturally respects filtering.
        /// </summary>
        public int CountVisibleDescendants(TreeNode node)
        {
            if (node.FlatIndex < 0)
                return 0;

            var count = 0;
            for (var i = node.FlatIndex + 1; i < _flat.Count; i++)
            {
                if (_flat[i].Level <= node.Level)
                    break;
                count++;
            }

            return count;
        }

        private void Reindex(int from)
        {
            for (var i = from; i < _flat.Count; i++)
                _flat[i].FlatIndex = i;
        }
    }
}
