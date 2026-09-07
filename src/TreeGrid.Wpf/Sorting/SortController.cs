using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using TreeGrid.Wpf.Columns;
using TreeGrid.Wpf.Data;

namespace TreeGrid.Wpf.Sorting
{
    /// <summary>
    /// Sorts the node hierarchy level by level.
    /// <para>
    /// A tree grid cannot sort the flattened list — doing so would tear children away
    /// from their parents. Instead each sibling group is sorted independently, which
    /// preserves the hierarchy while still giving a globally consistent ordering.
    /// </para>
    /// </summary>
    public sealed class SortController
    {
        private readonly SortComparers _customComparers;

        public SortController(SortComparers customComparers)
        {
            _customComparers = customComparers;
        }

        public SortColumnDescriptions SortDescriptions { get; } = new SortColumnDescriptions();

        /// <summary>
        /// Resolves a column by mapping name so the sort can honour SortMemberPath.
        /// Set by the grid; without it the mapping name is used directly.
        /// </summary>
        public Func<string, TreeGridColumn> ColumnResolver { get; set; }

        private string ResolvePath(string columnName)
        {
            var column = ColumnResolver?.Invoke(columnName);
            return column?.ResolvedSortMemberPath ?? columnName;
        }

        public bool HasSort => SortDescriptions.Count > 0;

        /// <summary>Builds the comparison used for every sibling group, or null when unsorted.</summary>
        public Comparison<TreeNode> BuildComparison()
        {
            if (SortDescriptions.Count == 0)
            {
                // Restoring the source order is what "no sort" means, so fall back to
                // the index captured when the nodes were built.
                return (a, b) => a.SourceIndex.CompareTo(b.SourceIndex);
            }

            var descriptions = new List<SortColumnDescription>(SortDescriptions);
            var comparers = new List<IComparer<object>>(descriptions.Count);
            var paths = new List<string>(descriptions.Count);

            foreach (var description in descriptions)
            {
                comparers.Add(_customComparers?.Find(description.ColumnName));

                // Resolved once per sort, not once per comparison.
                paths.Add(ResolvePath(description.ColumnName));
            }

            return (a, b) =>
            {
                for (var i = 0; i < descriptions.Count; i++)
                {
                    var description = descriptions[i];

                    var left = PropertyAccessor.GetValue(a.Item, paths[i]);
                    var right = PropertyAccessor.GetValue(b.Item, paths[i]);

                    var result = comparers[i] != null
                        ? comparers[i].Compare(left, right)
                        : CompareValues(left, right);

                    if (result == 0)
                        continue;

                    return description.SortDirection == ListSortDirection.Descending ? -result : result;
                }

                // Ties fall back to source order so the sort is deterministic.
                return a.SourceIndex.CompareTo(b.SourceIndex);
            };
        }

        /// <summary>
        /// Nulls sort first ascending, which matches Excel and most grids. Values that
        /// do not implement IComparable are compared by their string form rather than
        /// throwing, since a single odd cell should not break the whole sort.
        /// </summary>
        public static int CompareValues(object left, object right)
        {
            if (ReferenceEquals(left, right))
                return 0;

            if (left == null)
                return -1;

            if (right == null)
                return 1;

            if (left is IComparable comparable && left.GetType() == right.GetType())
                return comparable.CompareTo(right);

            if (left is IComparable crossType)
            {
                try
                {
                    var converted = Convert.ChangeType(right, left.GetType());
                    return crossType.CompareTo(converted);
                }
                catch (Exception)
                {
                    // Fall through to string comparison.
                }
            }

            return string.Compare(left.ToString(), right.ToString(), StringComparison.CurrentCulture);
        }

        /// <summary>
        /// Cycles a column through ascending, descending, unsorted. Returns the new
        /// direction, or null when the column dropped out of the sort.
        /// </summary>
        public ListSortDirection? ToggleColumn(string columnName, bool allowMultiSort, bool addToExisting)
        {
            var existing = SortDescriptions.Find(columnName);

            if (existing == null)
            {
                if (!allowMultiSort || !addToExisting)
                    SortDescriptions.Clear();

                SortDescriptions.Add(new SortColumnDescription
                {
                    ColumnName = columnName,
                    SortDirection = ListSortDirection.Ascending
                });

                return ListSortDirection.Ascending;
            }

            if (existing.SortDirection == ListSortDirection.Ascending)
            {
                if (!allowMultiSort || !addToExisting)
                {
                    SortDescriptions.Clear();
                    SortDescriptions.Add(new SortColumnDescription
                    {
                        ColumnName = columnName,
                        SortDirection = ListSortDirection.Descending
                    });
                }
                else
                {
                    existing.SortDirection = ListSortDirection.Descending;
                }

                return ListSortDirection.Descending;
            }

            // Descending -> unsorted.
            SortDescriptions.Remove(existing);

            if (!allowMultiSort || !addToExisting)
                SortDescriptions.Clear();

            return null;
        }

        public int GetSortNumber(string columnName)
        {
            for (var i = 0; i < SortDescriptions.Count; i++)
            {
                if (string.Equals(SortDescriptions[i].ColumnName, columnName, StringComparison.Ordinal))
                    return i + 1;
            }

            return 0;
        }

        public ListSortDirection? GetDirection(string columnName) =>
            SortDescriptions.Find(columnName)?.SortDirection;

        public void Clear() => SortDescriptions.Clear();

        /// <summary>
        /// Stable in-place sort. <see cref="List{T}.Sort"/> is unstable, which would
        /// make equal rows shuffle on every re-sort; the source-index tiebreak in
        /// <see cref="BuildComparison"/> removes that, and this keeps the guarantee
        /// even for caller-supplied comparisons.
        /// </summary>
        public static void StableSort(List<TreeNode> nodes, Comparison<TreeNode> comparison)
        {
            if (nodes == null || nodes.Count < 2 || comparison == null)
                return;

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
    }
}
