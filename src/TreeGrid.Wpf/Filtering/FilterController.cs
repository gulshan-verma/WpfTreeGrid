using System;
using System.Collections.Generic;
using TreeGrid.Wpf.Data;

namespace TreeGrid.Wpf.Filtering
{
    /// <summary>
    /// Evaluates column filters across the node tree and marks nodes as filtered out.
    /// The flat view already skips <see cref="TreeNode.IsFilteredOut"/> nodes, so a
    /// filter pass is a mark phase followed by one rebuild.
    /// </summary>
    public sealed class FilterController
    {
        private readonly Dictionary<string, ColumnFilter> _filters =
            new Dictionary<string, ColumnFilter>(StringComparer.Ordinal);

        public FilterNodeMode NodeMode { get; set; } = FilterNodeMode.MatchingAndParentNodes;

        /// <summary>Custom row-level filter, applied on top of the column filters.</summary>
        public Predicate<object> FilterPredicate { get; set; }

        public bool HasFilters => _filters.Count > 0 || FilterPredicate != null;

        public event EventHandler<FilterChangedEventArgs> FilterChanged;

        public IReadOnlyDictionary<string, ColumnFilter> Filters => _filters;

        public ColumnFilter GetFilter(string mappingName)
        {
            if (string.IsNullOrEmpty(mappingName))
                return null;

            return _filters.TryGetValue(mappingName, out var filter) ? filter : null;
        }

        public bool IsFiltered(string mappingName) => GetFilter(mappingName)?.HasFilter == true;

        public void SetFilter(ColumnFilter filter)
        {
            if (filter == null || string.IsNullOrEmpty(filter.MappingName))
                return;

            if (!filter.HasFilter)
            {
                ClearFilter(filter.MappingName);
                return;
            }

            _filters[filter.MappingName] = filter;
            FilterChanged?.Invoke(this, new FilterChangedEventArgs(filter.MappingName, false));
        }

        public void ClearFilter(string mappingName)
        {
            if (string.IsNullOrEmpty(mappingName) || !_filters.Remove(mappingName))
                return;

            FilterChanged?.Invoke(this, new FilterChangedEventArgs(mappingName, true));
        }

        public void ClearAll()
        {
            if (_filters.Count == 0)
                return;

            _filters.Clear();
            FilterChanged?.Invoke(this, new FilterChangedEventArgs(null, true));
        }

        // ----------------------------------------------------------- evaluation

        /// <summary>
        /// Marks the whole hierarchy. Returns the number of nodes left visible, which
        /// the grid uses to decide whether to show an empty-results message.
        /// </summary>
        public int Apply(IReadOnlyList<TreeNode> roots)
        {
            if (roots == null)
                return 0;

            if (!HasFilters)
            {
                var total = 0;
                for (var i = 0; i < roots.Count; i++)
                    total += ClearMarks(roots[i]);

                return total;
            }

            var visible = 0;
            for (var i = 0; i < roots.Count; i++)
            {
                Walk(roots[i], false, ref visible);
            }

            return visible;
        }

        /// <summary>Re-marks a single subtree, used after load-on-demand children arrive.</summary>
        public void ApplyToSubtree(TreeNode node)
        {
            if (node == null)
                return;

            if (!HasFilters)
            {
                ClearMarks(node);
                return;
            }

            var ancestorMatched = false;
            var ancestor = node.ParentNode;

            while (ancestor != null)
            {
                if (Matches(ancestor))
                {
                    ancestorMatched = true;
                    break;
                }

                ancestor = ancestor.ParentNode;
            }

            var visible = 0;
            Walk(node, ancestorMatched, ref visible);
        }

        private static int ClearMarks(TreeNode node)
        {
            node.IsFilteredOut = false;
            var count = 1;

            for (var i = 0; i < node.ChildNodes.Count; i++)
                count += ClearMarks(node.ChildNodes[i]);

            return count;
        }

        /// <summary>
        /// Depth-first mark. Returns whether this node or any descendant matched, which
        /// is what lets an ancestor decide to keep itself as context.
        /// </summary>
        private bool Walk(TreeNode node, bool ancestorMatched, ref int visibleCount)
        {
            var selfMatch = Matches(node);
            var keepAsChild = ancestorMatched && KeepsChildren;

            var descendantMatched = false;
            var childAncestorMatched = ancestorMatched || selfMatch;

            for (var i = 0; i < node.ChildNodes.Count; i++)
            {
                if (Walk(node.ChildNodes[i], childAncestorMatched, ref visibleCount))
                    descendantMatched = true;
            }

            var visible = selfMatch
                          || keepAsChild
                          || (descendantMatched && KeepsParents);

            node.IsFilteredOut = !visible;

            if (visible)
                visibleCount++;

            return selfMatch || descendantMatched;
        }

        private bool KeepsParents =>
            NodeMode == FilterNodeMode.MatchingAndParentNodes ||
            NodeMode == FilterNodeMode.MatchingParentAndChildNodes;

        private bool KeepsChildren =>
            NodeMode == FilterNodeMode.MatchingAndChildNodes ||
            NodeMode == FilterNodeMode.MatchingParentAndChildNodes;

        /// <summary>Column filters combine with AND; the row predicate must also pass.</summary>
        public bool Matches(TreeNode node)
        {
            if (node?.Item == null)
                return false;

            foreach (var pair in _filters)
            {
                var value = PropertyAccessor.GetValue(node.Item, pair.Key);

                if (!pair.Value.Evaluate(value))
                    return false;
            }

            if (FilterPredicate != null && !FilterPredicate(node.Item))
                return false;

            return true;
        }

        // ------------------------------------------------------ distinct values

        /// <summary>
        /// Gathers the distinct values of a column for the checkbox list, walking the
        /// entire node tree rather than the flat view so collapsed rows still appear.
        /// <para>
        /// Note this is unavailable for genuinely unbounded load-on-demand sources: the
        /// grid can only offer what has been materialised, which is why the popup
        /// falls back to condition-only filtering in unbound mode.
        /// </para>
        /// </summary>
        public List<FilterElement> GetDistinctValues(IReadOnlyList<TreeNode> roots, string mappingName,
            Func<object, string> formatter, int limit = 5000)
        {
            var result = new List<FilterElement>();
            if (roots == null || string.IsNullOrEmpty(mappingName))
                return result;

            var counts = new Dictionary<string, (object Value, int Count)>(StringComparer.CurrentCulture);
            var order = new List<string>();

            void Visit(TreeNode node)
            {
                if (order.Count >= limit)
                    return;

                var value = PropertyAccessor.GetValue(node.Item, mappingName);
                var text = formatter != null ? formatter(value) : value?.ToString() ?? string.Empty;

                if (counts.TryGetValue(text, out var existing))
                    counts[text] = (existing.Value, existing.Count + 1);
                else
                {
                    counts[text] = (value, 1);
                    order.Add(text);
                }

                for (var i = 0; i < node.ChildNodes.Count; i++)
                    Visit(node.ChildNodes[i]);
            }

            for (var i = 0; i < roots.Count; i++)
                Visit(roots[i]);

            order.Sort((a, b) => string.Compare(a, b, StringComparison.CurrentCulture));

            foreach (var text in order)
            {
                var entry = counts[text];
                result.Add(new FilterElement(entry.Value, text, entry.Count));
            }

            return result;
        }

        /// <summary>Turns a checkbox-list selection into an OR chain of equality predicates.</summary>
        public static ColumnFilter BuildFromSelection(string mappingName, IEnumerable<FilterElement> selected)
        {
            var filter = new ColumnFilter(mappingName);

            foreach (var element in selected)
            {
                filter.Predicates.Add(new FilterPredicate
                {
                    FilterType = FilterType.Equals,
                    FilterValue = element.DisplayText,
                    PredicateType = PredicateType.Or,
                    FilterBehavior = FilterBehavior.StringTyped
                });
            }

            return filter;
        }
    }
}
