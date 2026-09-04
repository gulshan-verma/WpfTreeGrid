using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using TreeGrid.Wpf.Data;

namespace TreeGrid.Wpf.DragDropSupport
{
    public enum RowDropPosition
    {
        /// <summary>Insert as a sibling above the target.</summary>
        Above,

        /// <summary>Insert as a sibling below the target.</summary>
        Below,

        /// <summary>Insert as a child of the target.</summary>
        Into
    }

    public sealed class RowDragStartingEventArgs : CancelEventArgs
    {
        public RowDragStartingEventArgs(IReadOnlyList<TreeNode> nodes) => Nodes = nodes;

        public IReadOnlyList<TreeNode> Nodes { get; }
    }

    public sealed class RowDragOverEventArgs : EventArgs
    {
        public RowDragOverEventArgs(IReadOnlyList<TreeNode> nodes, TreeNode target, RowDropPosition position)
        {
            Nodes = nodes;
            TargetNode = target;
            Position = position;
            IsAllowed = true;
        }

        public IReadOnlyList<TreeNode> Nodes { get; }

        public TreeNode TargetNode { get; }

        public RowDropPosition Position { get; }

        /// <summary>Set false to refuse this drop point.</summary>
        public bool IsAllowed { get; set; }
    }

    public sealed class RowDroppedEventArgs : EventArgs
    {
        public RowDroppedEventArgs(IReadOnlyList<TreeNode> nodes, TreeNode target, RowDropPosition position)
        {
            Nodes = nodes;
            TargetNode = target;
            Position = position;
        }

        public IReadOnlyList<TreeNode> Nodes { get; }

        public TreeNode TargetNode { get; }

        public RowDropPosition Position { get; }

        /// <summary>
        /// Set true if the handler relocated the underlying data itself. The grid then
        /// reloads rather than moving nodes, which keeps the source authoritative.
        /// </summary>
        public bool HandledByHost { get; set; }
    }

    /// <summary>
    /// Row reordering.
    /// <para>
    /// Moving nodes and moving the underlying data are deliberately separate. The node
    /// tree can always be rearranged; the source collections cannot, since a
    /// self-relational list needs a parent-id rewrite while a hierarchical one needs
    /// items moved between child collections. Hosts that care handle
    /// <c>RowDropped</c> and set <see cref="RowDroppedEventArgs.HandledByHost"/>.
    /// </para>
    /// </summary>
    public sealed class RowDragDropController
    {
        private readonly List<TreeNode> _dragging = new List<TreeNode>();

        public bool IsDragging { get; private set; }

        public IReadOnlyList<TreeNode> DraggingNodes => _dragging;

        public TreeNode TargetNode { get; private set; }

        public RowDropPosition Position { get; private set; }

        public bool IsCurrentDropAllowed { get; private set; }

        /// <summary>Fraction of row height at each edge that means "sibling" rather than "child".</summary>
        public double EdgeThreshold { get; set; } = 0.3;

        public event EventHandler<RowDragStartingEventArgs> DragStarting;

        public event EventHandler<RowDragOverEventArgs> DragOver;

        public event EventHandler<RowDroppedEventArgs> Dropped;

        public bool Begin(IEnumerable<TreeNode> nodes)
        {
            _dragging.Clear();

            foreach (var node in nodes)
            {
                // Group headers are synthetic: they carry no record and their position
                // is derived from the grouping, so moving one is meaningless.
                if (node != null && !node.IsGroupHeader)
                    _dragging.Add(node);
            }

            if (_dragging.Count == 0)
                return false;

            var args = new RowDragStartingEventArgs(_dragging);
            DragStarting?.Invoke(this, args);

            if (args.Cancel)
            {
                _dragging.Clear();
                return false;
            }

            IsDragging = true;
            return true;
        }

        /// <summary>Resolves the drop point from a pointer position within a row.</summary>
        public void Update(TreeNode target, double offsetWithinRow, double rowHeight)
        {
            if (!IsDragging)
                return;

            TargetNode = target;
            Position = ResolvePosition(offsetWithinRow, rowHeight);
            IsCurrentDropAllowed = EvaluateAllowed();
        }

        private RowDropPosition ResolvePosition(double offsetWithinRow, double rowHeight)
        {
            if (rowHeight <= 0)
                return RowDropPosition.Below;

            var ratio = offsetWithinRow / rowHeight;

            if (ratio <= EdgeThreshold)
                return RowDropPosition.Above;

            if (ratio >= 1 - EdgeThreshold)
                return RowDropPosition.Below;

            return RowDropPosition.Into;
        }

        private bool EvaluateAllowed()
        {
            if (TargetNode == null)
                return false;

            // Dropping beside a group header would make a record a sibling of the
            // group, and dropping into one would put it outside its own grouping key.
            if (TargetNode.IsGroupHeader)
                return false;

            // Dropping a node into its own subtree would detach that subtree from the
            // tree entirely, so it is refused before any handler sees it.
            foreach (var node in _dragging)
            {
                if (ReferenceEquals(node, TargetNode))
                    return false;

                if (TargetNode.IsDescendantOf(node))
                    return false;
            }

            var args = new RowDragOverEventArgs(_dragging, TargetNode, Position);
            DragOver?.Invoke(this, args);

            return args.IsAllowed;
        }

        /// <summary>
        /// Completes the drop. The root list must be supplied because a node can both
        /// leave and enter it, and only the flat view owns that collection.
        /// </summary>
        public bool Complete(IList<TreeNode> rootNodes)
        {
            if (!IsDragging || TargetNode == null || !IsCurrentDropAllowed)
            {
                Cancel();
                return false;
            }

            var args = new RowDroppedEventArgs(new List<TreeNode>(_dragging), TargetNode, Position);
            Dropped?.Invoke(this, args);

            var moved = false;

            if (!args.HandledByHost)
                moved = MoveNodes(_dragging, TargetNode, Position, rootNodes);

            Reset();
            return moved || args.HandledByHost;
        }

        public void Cancel() => Reset();

        private void Reset()
        {
            IsDragging = false;
            _dragging.Clear();
            TargetNode = null;
            IsCurrentDropAllowed = false;
        }

        /// <summary>
        /// Relocates nodes within the node tree.
        /// <para>
        /// Root nodes are ordinary nodes whose sibling list happens to be the root
        /// collection. Treating that list as just another sibling list is what lets a
        /// child be promoted to a root, and a root be demoted into a child.
        /// </para>
        /// </summary>
        public static bool MoveNodes(IReadOnlyList<TreeNode> nodes, TreeNode target,
            RowDropPosition position, IList<TreeNode> rootNodes)
        {
            var moved = false;

            if (target == null || target.IsGroupHeader)
                return false;

            foreach (var node in nodes)
            {
                if (node.IsGroupHeader || ReferenceEquals(node, target) || target.IsDescendantOf(node))
                    continue;

                var previousParent = node.ParentNode;

                Detach(node, rootNodes);

                if (position == RowDropPosition.Into)
                {
                    node.ParentNode = target;
                    target.ChildNodes.Add(node);
                    target.HasChildNodes = true;
                    target.IsChildNodesPopulated = true;
                    target.IsExpanded = true;

                    Reindex(target.ChildNodes);
                }
                else
                {
                    // Siblings of a root are the roots themselves.
                    var siblings = target.ParentNode == null
                        ? rootNodes
                        : target.ParentNode.ChildNodes;

                    if (siblings == null)
                        continue;

                    var index = siblings.IndexOf(target);
                    if (index < 0)
                        index = siblings.Count - 1;

                    if (position == RowDropPosition.Below)
                        index++;

                    node.ParentNode = target.ParentNode;
                    siblings.Insert(Math.Min(Math.Max(0, index), siblings.Count), node);

                    Reindex(siblings);
                }

                // The old parent may have just become a leaf.
                if (previousParent != null && previousParent.ChildNodes.Count == 0)
                {
                    previousParent.HasChildNodes = false;
                    previousParent.IsExpanded = false;
                }

                AssignLevels(node, (node.ParentNode?.Level ?? -1) + 1);
                moved = true;
            }

            return moved;
        }

        /// <summary>
        /// Removes a node from whichever list currently holds it. A root lives in the
        /// root collection rather than in a parent's ChildNodes, and missing that case
        /// left dragged roots visible in their old position.
        /// </summary>
        private static void Detach(TreeNode node, IList<TreeNode> rootNodes)
        {
            if (node.ParentNode == null)
            {
                rootNodes?.Remove(node);
                return;
            }

            node.ParentNode.ChildNodes.Remove(node);
        }

        /// <summary>
        /// Rewrites sibling order after a move. Without this the nodes keep their
        /// original SourceIndex, so clearing the sort would snap them back to where
        /// they were dragged from.
        /// </summary>
        private static void Reindex(IList<TreeNode> siblings)
        {
            for (var i = 0; i < siblings.Count; i++)
                siblings[i].SourceIndex = i;
        }

        private static void AssignLevels(TreeNode node, int level)
        {
            node.Level = level;

            for (var i = 0; i < node.ChildNodes.Count; i++)
                AssignLevels(node.ChildNodes[i], level + 1);
        }
    }

    /// <summary>Drop indicator drawn over the row area during a drag.</summary>
    public sealed class RowDropAdorner : Adorner
    {
        private readonly Pen _allowedPen;
        private readonly Pen _deniedPen;
        private readonly Brush _intoBrush;

        public RowDropAdorner(UIElement adornedElement, Brush indicatorBrush) : base(adornedElement)
        {
            IsHitTestVisible = false;

            var brush = indicatorBrush ?? Brushes.DodgerBlue;

            _allowedPen = new Pen(brush, 2);
            _allowedPen.Freeze();

            _deniedPen = new Pen(Brushes.IndianRed, 2) { DashStyle = DashStyles.Dash };
            _deniedPen.Freeze();

            _intoBrush = new SolidColorBrush(Color.FromArgb(48, 31, 111, 235));
            _intoBrush.Freeze();
        }

        public double IndicatorY { get; private set; } = -1;

        public double RowHeight { get; private set; }

        public double IndentLeft { get; private set; }

        public RowDropPosition Position { get; private set; }

        public bool IsAllowed { get; private set; }

        public void Update(double y, double rowHeight, double indentLeft, RowDropPosition position, bool isAllowed)
        {
            IndicatorY = y;
            RowHeight = rowHeight;
            IndentLeft = indentLeft;
            Position = position;
            IsAllowed = isAllowed;

            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (IndicatorY < 0)
                return;

            var width = AdornedElement.RenderSize.Width;
            var pen = IsAllowed ? _allowedPen : _deniedPen;

            if (Position == RowDropPosition.Into)
            {
                // A filled band reads as "inside this row", where a line would read as
                // "between these rows".
                drawingContext.DrawRectangle(IsAllowed ? _intoBrush : null, pen,
                    new Rect(IndentLeft, IndicatorY, Math.Max(0, width - IndentLeft), RowHeight));
                return;
            }

            var y = Math.Round(IndicatorY) + 0.5;
            drawingContext.DrawLine(pen, new Point(IndentLeft, y), new Point(width, y));
            drawingContext.DrawEllipse(pen.Brush, null, new Point(IndentLeft + 3, y), 3, 3);
        }
    }
}
