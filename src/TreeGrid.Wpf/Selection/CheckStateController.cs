using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TreeGrid.Wpf.Data;

namespace TreeGrid.Wpf.Selection
{
    public enum CheckBoxCascadeMode
    {
        /// <summary>Each node's checkbox is independent.</summary>
        None,

        /// <summary>Checking a node checks its subtree and rolls the state up to ancestors.</summary>
        SynchronizeWithParentAndChildren,

        /// <summary>Checking a node checks its subtree, but ancestors are left alone.</summary>
        SynchronizeChildrenOnly
    }

    /// <summary>
    /// Maintains tri-state checkbox state across the hierarchy.
    /// <para>
    /// The awkward case is load-on-demand: an ancestor can be checked before its
    /// children exist. Rather than pretend the subtree is known, a checked-but-
    /// unpopulated node records its state and children inherit it when they arrive
    /// (see <see cref="ApplyInheritedState"/>).
    /// </para>
    /// </summary>
    public sealed class CheckStateController
    {
        private readonly ObservableCollection<object> _checkedItems = new ObservableCollection<object>();
        private readonly HashSet<TreeNode> _checkedNodes = new HashSet<TreeNode>();

        public CheckStateController()
        {
            CheckedItems = new ReadOnlyObservableCollection<object>(_checkedItems);
        }

        public CheckBoxCascadeMode CascadeMode { get; set; } = CheckBoxCascadeMode.SynchronizeWithParentAndChildren;

        public ReadOnlyObservableCollection<object> CheckedItems { get; }

        public event EventHandler<NodeCheckedEventArgs> NodeChecked;

        /// <summary>Cycles a node between checked and unchecked. Indeterminate becomes checked.</summary>
        public void Toggle(TreeNode node)
        {
            if (node == null)
                return;

            var next = node.IsChecked != true;
            SetState(node, next);
        }

        public void SetState(TreeNode node, bool? state)
        {
            if (node == null)
                return;

            var old = node.IsChecked;
            ApplyState(node, state);

            if (CascadeMode != CheckBoxCascadeMode.None && state.HasValue)
                CascadeDown(node, state.Value);

            if (CascadeMode == CheckBoxCascadeMode.SynchronizeWithParentAndChildren)
                CascadeUp(node.ParentNode);

            NodeChecked?.Invoke(this, new NodeCheckedEventArgs(node, old, node.IsChecked));
        }

        private void CascadeDown(TreeNode node, bool state)
        {
            for (var i = 0; i < node.ChildNodes.Count; i++)
            {
                var child = node.ChildNodes[i];
                ApplyState(child, state);
                CascadeDown(child, state);
            }
        }

        private void CascadeUp(TreeNode node)
        {
            while (node != null)
            {
                ApplyState(node, ResolveParentState(node));
                node = node.ParentNode;
            }
        }

        /// <summary>
        /// A parent is checked when every child is checked, unchecked when none are,
        /// indeterminate otherwise. Unpopulated parents keep their own state, since
        /// nothing is known about the subtree yet.
        /// </summary>
        private static bool? ResolveParentState(TreeNode node)
        {
            if (node.ChildNodes.Count == 0)
                return node.IsChecked;

            var checkedCount = 0;
            var indeterminate = false;

            for (var i = 0; i < node.ChildNodes.Count; i++)
            {
                var state = node.ChildNodes[i].IsChecked;

                if (state == null)
                {
                    indeterminate = true;
                    break;
                }

                if (state.Value)
                    checkedCount++;
            }

            if (indeterminate)
                return null;

            if (checkedCount == 0)
                return false;

            return checkedCount == node.ChildNodes.Count ? true : (bool?)null;
        }

        /// <summary>
        /// Called after load-on-demand supplies children. A fully-checked parent hands
        /// its state down to the newly arrived nodes so the tree stays consistent.
        /// </summary>
        public void ApplyInheritedState(TreeNode parent)
        {
            if (parent == null || CascadeMode == CheckBoxCascadeMode.None)
                return;

            if (parent.IsChecked != true)
            {
                if (CascadeMode == CheckBoxCascadeMode.SynchronizeWithParentAndChildren)
                    CascadeUp(parent);
                return;
            }

            for (var i = 0; i < parent.ChildNodes.Count; i++)
            {
                var child = parent.ChildNodes[i];
                ApplyState(child, true);
                CascadeDown(child, true);
            }
        }

        public void Clear()
        {
            foreach (var node in _checkedNodes)
                node.IsChecked = false;

            _checkedNodes.Clear();
            _checkedItems.Clear();
        }

        /// <summary>Re-attaches check state after the node tree is rebuilt.</summary>
        public void Restore(Func<object, TreeNode> nodeLookup)
        {
            var items = new List<object>(_checkedItems);

            _checkedNodes.Clear();
            _checkedItems.Clear();

            foreach (var item in items)
            {
                var node = nodeLookup(item);
                if (node == null)
                    continue;

                node.IsChecked = true;
                _checkedNodes.Add(node);
                _checkedItems.Add(item);
            }
        }

        private void ApplyState(TreeNode node, bool? state)
        {
            if (node.IsChecked == state)
                return;

            node.IsChecked = state;

            if (state == true)
            {
                if (_checkedNodes.Add(node))
                    _checkedItems.Add(node.Item);
            }
            else
            {
                if (_checkedNodes.Remove(node))
                    _checkedItems.Remove(node.Item);
            }
        }
    }
}
