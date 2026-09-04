using TreeGrid.Wpf.Grouping;

namespace TreeGrid.Wpf.Data
{
    /// <summary>
    /// Creates nodes for code outside the data source. <see cref="TreeNode"/> keeps an
    /// internal constructor so that only the data layer can mint nodes; grouping needs
    /// the same privilege without widening it to the whole assembly's callers.
    /// </summary>
    internal static class TreeNodeFactory
    {
        public static TreeNode CreateGroupNode(GroupInfo info, string caption, TreeNode parent, int level)
        {
            var node = new TreeNode(null, parent, level)
            {
                GroupInfo = info,
                GroupCaption = caption,
                IsChildNodesPopulated = true
            };

            return node;
        }

        /// <summary>
        /// A record leaf under a group. This is a fresh node rather than the original,
        /// because the original still belongs to the ungrouped tree that will be
        /// restored when grouping is cleared.
        /// </summary>
        public static TreeNode CreateRecordLeaf(object item, TreeNode parent, int level)
        {
            return new TreeNode(item, parent, level)
            {
                IsChildNodesPopulated = true,
                HasChildNodes = false
            };
        }
    }
}
