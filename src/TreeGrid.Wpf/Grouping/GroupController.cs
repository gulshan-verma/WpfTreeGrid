using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using TreeGrid.Wpf.Columns;
using TreeGrid.Wpf.Data;

namespace TreeGrid.Wpf.Grouping
{
    /// <summary>One level of grouping.</summary>
    public class GroupColumnDescription : DependencyObject
    {
        public static readonly DependencyProperty ColumnNameProperty = DependencyProperty.Register(
            nameof(ColumnName), typeof(string), typeof(GroupColumnDescription), new PropertyMetadata(null));

        public static readonly DependencyProperty SortDirectionProperty = DependencyProperty.Register(
            nameof(SortDirection), typeof(ListSortDirection), typeof(GroupColumnDescription),
            new PropertyMetadata(ListSortDirection.Ascending));

        public string ColumnName
        {
            get => (string)GetValue(ColumnNameProperty);
            set => SetValue(ColumnNameProperty, value);
        }

        /// <summary>Order of the group headers themselves, not of the records inside them.</summary>
        public ListSortDirection SortDirection
        {
            get => (ListSortDirection)GetValue(SortDirectionProperty);
            set => SetValue(SortDirectionProperty, value);
        }

        /// <summary>Header text for the chip and the group captions. Set by the grid.</summary>
        public string HeaderText { get; set; }

        public override string ToString() => $"{ColumnName} {SortDirection}";
    }

    public sealed class GroupColumnDescriptions : ObservableCollection<GroupColumnDescription>
    {
        public GroupColumnDescription Find(string columnName)
        {
            foreach (var description in this)
            {
                if (string.Equals(description.ColumnName, columnName, StringComparison.Ordinal))
                    return description;
            }

            return null;
        }

        public int IndexOfColumn(string columnName)
        {
            for (var i = 0; i < Count; i++)
            {
                if (string.Equals(this[i].ColumnName, columnName, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }
    }

    /// <summary>Payload carried by a group header node.</summary>
    public sealed class GroupInfo
    {
        public GroupInfo(TreeGridColumn column, object key, string displayText, int level)
        {
            Column = column;
            Key = key;
            DisplayText = displayText;
            GroupLevel = level;
        }

        public TreeGridColumn Column { get; }

        public object Key { get; }

        /// <summary>Formatted key, e.g. "Manager".</summary>
        public string DisplayText { get; }

        /// <summary>Zero-based grouping depth.</summary>
        public int GroupLevel { get; }

        /// <summary>Records beneath this group, counted recursively.</summary>
        public int ItemCount { get; internal set; }

        public string ColumnHeader => Column?.ResolvedHeaderText ?? Column?.MappingName;

        public string Caption => $"{ColumnHeader}: {DisplayText}";
    }

    public sealed class GroupCaptionEventArgs : EventArgs
    {
        public GroupCaptionEventArgs(GroupInfo group)
        {
            Group = group;
            Caption = group.Caption;
        }

        public GroupInfo Group { get; }

        /// <summary>Override to customise the group header text.</summary>
        public string Caption { get; set; }
    }

    /// <summary>
    /// Builds a tree of group header nodes from a flat set of records.
    /// <para>
    /// Grouping replaces the source hierarchy rather than nesting inside it: a row can
    /// sit under its parent or under a group, not both. Records are therefore
    /// represented by fresh leaf nodes, and the original tree is restored untouched
    /// when grouping is cleared.
    /// </para>
    /// </summary>
    public sealed class GroupController
    {
        public GroupColumnDescriptions Descriptions { get; } = new GroupColumnDescriptions();

        public bool IsGrouped => Descriptions.Count > 0;

        /// <summary>Shown when a grouped value is null or empty.</summary>
        public string NullGroupCaption { get; set; } = "(Blank)";

        /// <summary>Raised per group header so the caption can be customised.</summary>
        public event EventHandler<GroupCaptionEventArgs> QueryGroupCaption;

        /// <summary>
        /// Produces the grouped root nodes. <paramref name="records"/> are the original
        /// nodes; the leaves returned are new nodes wrapping the same data items.
        /// </summary>
        public List<TreeNode> Build(IReadOnlyList<TreeNode> records,
            Func<string, TreeGridColumn> columnResolver,
            Comparison<TreeNode> recordComparison)
        {
            var roots = new List<TreeNode>();

            if (records == null || records.Count == 0 || !IsGrouped)
                return roots;

            BuildLevel(records, 0, null, roots, columnResolver, recordComparison);

            for (var i = 0; i < roots.Count; i++)
                roots[i].SourceIndex = i;

            return roots;
        }

        private void BuildLevel(IReadOnlyList<TreeNode> records, int levelIndex, TreeNode parent,
            List<TreeNode> output, Func<string, TreeGridColumn> columnResolver,
            Comparison<TreeNode> recordComparison)
        {
            var description = Descriptions[levelIndex];
            var column = columnResolver(description.ColumnName);

            // Preserve first-seen order inside each bucket so a stable record sort is
            // not undone by the grouping pass.
            var buckets = new Dictionary<string, List<TreeNode>>(StringComparer.CurrentCulture);
            var order = new List<string>();
            var keys = new Dictionary<string, object>(StringComparer.CurrentCulture);

            foreach (var record in records)
            {
                var value = string.IsNullOrEmpty(description.ColumnName)
                    ? null
                    : PropertyAccessor.GetValue(record.Item, description.ColumnName);

                var text = FormatKey(column, value);

                if (!buckets.TryGetValue(text, out var bucket))
                {
                    bucket = new List<TreeNode>();
                    buckets[text] = bucket;
                    keys[text] = value;
                    order.Add(text);
                }

                bucket.Add(record);
            }

            order.Sort((a, b) =>
            {
                var result = Sorting.SortController.CompareValues(keys[a], keys[b]);

                if (result == 0)
                    result = string.Compare(a, b, StringComparison.CurrentCulture);

                return description.SortDirection == ListSortDirection.Descending ? -result : result;
            });

            var groupLevel = parent == null ? 0 : parent.Level + 1;

            foreach (var text in order)
            {
                var info = new GroupInfo(column, keys[text], text, levelIndex);

                var captionArgs = new GroupCaptionEventArgs(info);
                QueryGroupCaption?.Invoke(this, captionArgs);

                var groupNode = TreeNodeFactory.CreateGroupNode(info, captionArgs.Caption, parent, groupLevel);
                var bucket = buckets[text];

                if (levelIndex + 1 < Descriptions.Count)
                {
                    BuildLevel(bucket, levelIndex + 1, groupNode, groupNode.ChildNodes,
                        columnResolver, recordComparison);
                }
                else
                {
                    var leaves = new List<TreeNode>(bucket.Count);

                    foreach (var record in bucket)
                        leaves.Add(TreeNodeFactory.CreateRecordLeaf(record.Item, groupNode, groupLevel + 1));

                    if (recordComparison != null)
                        Sorting.SortController.StableSort(leaves, recordComparison);

                    for (var i = 0; i < leaves.Count; i++)
                    {
                        leaves[i].SourceIndex = i;
                        groupNode.ChildNodes.Add(leaves[i]);
                    }
                }

                for (var i = 0; i < groupNode.ChildNodes.Count; i++)
                    groupNode.ChildNodes[i].SourceIndex = i;

                info.ItemCount = CountRecords(groupNode);

                groupNode.HasChildNodes = groupNode.ChildNodes.Count > 0;
                groupNode.IsChildNodesPopulated = true;
                groupNode.IsExpanded = true;

                output.Add(groupNode);
            }
        }

        private string FormatKey(TreeGridColumn column, object value)
        {
            if (value == null)
                return NullGroupCaption;

            var text = column != null ? column.FormatValue(value) : Convert.ToString(value, CultureInfo.CurrentCulture);

            return string.IsNullOrWhiteSpace(text) ? NullGroupCaption : text;
        }

        private static int CountRecords(TreeNode node)
        {
            var count = 0;

            for (var i = 0; i < node.ChildNodes.Count; i++)
            {
                var child = node.ChildNodes[i];
                count += child.IsGroupHeader ? CountRecords(child) : 1;
            }

            return count;
        }

        /// <summary>Flattens a node tree into its records, ignoring group headers.</summary>
        public static List<TreeNode> CollectRecords(IReadOnlyList<TreeNode> roots)
        {
            var records = new List<TreeNode>();

            void Visit(TreeNode node)
            {
                if (!node.IsGroupHeader)
                    records.Add(node);

                for (var i = 0; i < node.ChildNodes.Count; i++)
                    Visit(node.ChildNodes[i]);
            }

            if (roots != null)
            {
                for (var i = 0; i < roots.Count; i++)
                    Visit(roots[i]);
            }

            return records;
        }
    }
}
