using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Media;
using TreeGrid.Wpf.Grouping;

namespace TreeGrid.Wpf.View
{
    /// <summary>
    /// The caption row for a group. Spans the full viewport width rather than being
    /// laid out per column, since a group header belongs to no single column.
    /// </summary>
    public class TreeGridGroupCell : Control
    {
        public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
            nameof(Caption), typeof(string), typeof(TreeGridGroupCell), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ItemCountProperty = DependencyProperty.Register(
            nameof(ItemCount), typeof(int), typeof(TreeGridGroupCell), new PropertyMetadata(0));

        public static readonly DependencyProperty ShowItemCountProperty = DependencyProperty.Register(
            nameof(ShowItemCount), typeof(bool), typeof(TreeGridGroupCell), new PropertyMetadata(true));

        public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
            nameof(IsExpanded), typeof(bool), typeof(TreeGridGroupCell), new PropertyMetadata(true));

        public static readonly DependencyProperty IndentProperty = DependencyProperty.Register(
            nameof(Indent), typeof(double), typeof(TreeGridGroupCell), new PropertyMetadata(0d));

        public static readonly DependencyProperty ExpanderGlyphBrushProperty = DependencyProperty.Register(
            nameof(ExpanderGlyphBrush), typeof(Brush), typeof(TreeGridGroupCell), new PropertyMetadata(null));

        static TreeGridGroupCell()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TreeGridGroupCell),
                new FrameworkPropertyMetadata(typeof(TreeGridGroupCell)));
        }

        public string Caption
        {
            get => (string)GetValue(CaptionProperty);
            set => SetValue(CaptionProperty, value);
        }

        public int ItemCount
        {
            get => (int)GetValue(ItemCountProperty);
            set => SetValue(ItemCountProperty, value);
        }

        public bool ShowItemCount
        {
            get => (bool)GetValue(ShowItemCountProperty);
            set => SetValue(ShowItemCountProperty, value);
        }

        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        public double Indent
        {
            get => (double)GetValue(IndentProperty);
            set => SetValue(IndentProperty, value);
        }

        public Brush ExpanderGlyphBrush
        {
            get => (Brush)GetValue(ExpanderGlyphBrushProperty);
            set => SetValue(ExpanderGlyphBrushProperty, value);
        }

        public Data.TreeNode Node { get; internal set; }

        private ButtonBase _expander;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_expander != null)
                _expander.Click -= OnExpanderClick;

            _expander = GetTemplateChild("PART_Expander") as ButtonBase;

            if (_expander != null)
                _expander.Click += OnExpanderClick;
        }

        private void OnExpanderClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            // Reuses the record expander event so the grid has one collapse path.
            RaiseEvent(new TreeNodeRoutedEventArgs(TreeGridCell.ExpanderToggleEvent, Node));
        }

        internal void Bind(Data.TreeNode node, double indentPerLevel, double indentBase)
        {
            Node = node;
            Caption = node.GroupCaption ?? node.GroupInfo?.Caption;
            ItemCount = node.GroupInfo?.ItemCount ?? 0;
            IsExpanded = node.IsExpanded;
            Indent = indentBase + node.Level * indentPerLevel;
        }
    }

    // =====================================================================

    public sealed class GroupChipEventArgs : RoutedEventArgs
    {
        public GroupChipEventArgs(RoutedEvent routedEvent, string columnName) : base(routedEvent)
        {
            ColumnName = columnName;
        }

        public string ColumnName { get; }
    }

    public sealed class GroupReorderEventArgs : RoutedEventArgs
    {
        public GroupReorderEventArgs(RoutedEvent routedEvent, int fromIndex, int toIndex) : base(routedEvent)
        {
            FromIndex = fromIndex;
            ToIndex = toIndex;
        }

        public int FromIndex { get; }

        public int ToIndex { get; }
    }

    /// <summary>
    /// The panel above the headers showing the active grouping, in order.
    /// <para>
    /// Chips can be dragged to reorder, which changes grouping precedence, clicked to
    /// flip the group sort direction, and dismissed to ungroup. Column headers dragged
    /// from the grid land here too - the grid routes its existing header-drag gesture
    /// into this panel when the pointer is over it.
    /// </para>
    /// </summary>
    public class GroupDropAreaControl : Control
    {
        public static readonly RoutedEvent GroupRemovedEvent = EventManager.RegisterRoutedEvent(
            "GroupRemoved", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(GroupDropAreaControl));

        public static readonly RoutedEvent GroupSortToggledEvent = EventManager.RegisterRoutedEvent(
            "GroupSortToggled", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(GroupDropAreaControl));

        public static readonly RoutedEvent GroupReorderedEvent = EventManager.RegisterRoutedEvent(
            "GroupReordered", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(GroupDropAreaControl));

        public static readonly RoutedEvent GroupChipDragStartedEvent = EventManager.RegisterRoutedEvent(
            "GroupChipDragStarted", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(GroupDropAreaControl));

        public static readonly DependencyProperty DescriptionsProperty = DependencyProperty.Register(
            nameof(Descriptions), typeof(GroupColumnDescriptions), typeof(GroupDropAreaControl),
            new PropertyMetadata(null));

        public static readonly DependencyProperty PromptTextProperty = DependencyProperty.Register(
            nameof(PromptText), typeof(string), typeof(GroupDropAreaControl),
            new PropertyMetadata("Drag a column header here to group by that column"));

        public static readonly DependencyProperty HasGroupsProperty = DependencyProperty.Register(
            nameof(HasGroups), typeof(bool), typeof(GroupDropAreaControl), new PropertyMetadata(false));

        public static readonly DependencyProperty IsDropTargetProperty = DependencyProperty.Register(
            nameof(IsDropTarget), typeof(bool), typeof(GroupDropAreaControl), new PropertyMetadata(false));

        static GroupDropAreaControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GroupDropAreaControl),
                new FrameworkPropertyMetadata(typeof(GroupDropAreaControl)));
        }

        public GroupColumnDescriptions Descriptions
        {
            get => (GroupColumnDescriptions)GetValue(DescriptionsProperty);
            set => SetValue(DescriptionsProperty, value);
        }

        public string PromptText
        {
            get => (string)GetValue(PromptTextProperty);
            set => SetValue(PromptTextProperty, value);
        }

        public bool HasGroups
        {
            get => (bool)GetValue(HasGroupsProperty);
            set => SetValue(HasGroupsProperty, value);
        }

        /// <summary>True while a column header is being dragged over this panel.</summary>
        public bool IsDropTarget
        {
            get => (bool)GetValue(IsDropTargetProperty);
            set => SetValue(IsDropTargetProperty, value);
        }

        private ItemsControl _chipHost;
        private Point _dragOrigin;
        private int _dragChipIndex = -1;
        private bool _pointerDown;
        private bool _closePressed;
        private DragPreviewAdorner _preview;

        /// <summary>Accent used for the ghost outline and the drop line.</summary>
        public Brush DropIndicatorBrush { get; set; }

        /// <summary>Set false to drag chips without the floating preview.</summary>
        public bool ShowDragPreview { get; set; } = true;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _chipHost = GetTemplateChild("PART_Chips") as ItemsControl;
            Refresh();
        }

        public void Refresh()
        {
            HasGroups = Descriptions != null && Descriptions.Count > 0;

            if (_chipHost != null)
                _chipHost.ItemsSource = Descriptions;
        }

        /// <summary>X position of the drop line for an insertion index.</summary>
        public double GetInsertX(int index)
        {
            if (_chipHost == null || Descriptions == null || Descriptions.Count == 0)
                return 8;

            var clamped = Math.Min(Math.Max(0, index), Descriptions.Count);

            // Past the last chip, the line sits at its trailing edge.
            if (clamped == Descriptions.Count)
            {
                var last = _chipHost.ItemContainerGenerator.ContainerFromIndex(Descriptions.Count - 1)
                    as FrameworkElement;

                if (last == null)
                    return 8;

                return last.TranslatePoint(new Point(0, 0), this).X + last.ActualWidth;
            }

            var container = _chipHost.ItemContainerGenerator.ContainerFromIndex(clamped) as FrameworkElement;

            return container?.TranslatePoint(new Point(0, 0), this).X ?? 8;
        }

        /// <summary>Shows the drop line at a grouping index. Used by the panel and by the grid.</summary>
        public void ShowInsertion(int index)
        {
            EnsurePreview(null, default);
            _preview?.SetInsertion(GetInsertX(index), ActualHeight);
        }

        public void HideInsertion()
        {
            if (_preview == null)
                return;

            // Keep the adorner alive while a chip ghost is still being dragged.
            if (_dragChipIndex >= 0 && IsMouseCaptured)
            {
                _preview.ClearInsertion();
                return;
            }

            RemovePreview();
        }

        private void EnsurePreview(FrameworkElement ghostSource, Point grabOffset)
        {
            if (_preview != null)
                return;

            var layer = AdornerLayer.GetAdornerLayer(this);

            if (layer == null)
                return;

            _preview = new DragPreviewAdorner(this, ShowDragPreview ? ghostSource : null,
                DropIndicatorBrush) { GrabOffset = grabOffset };

            layer.Add(_preview);
        }

        private void RemovePreview()
        {
            if (_preview == null)
                return;

            AdornerLayer.GetAdornerLayer(this)?.Remove(_preview);
            _preview = null;
        }

        /// <summary>
        /// Insertion index for a point in this panel's coordinates. Used both by chip
        /// reordering and by column headers dropped in from the grid.
        /// </summary>
        public int GetInsertIndex(Point point)
        {
            if (_chipHost == null || Descriptions == null || Descriptions.Count == 0)
                return 0;

            for (var i = 0; i < Descriptions.Count; i++)
            {
                if (!(_chipHost.ItemContainerGenerator.ContainerFromIndex(i) is FrameworkElement container))
                    continue;

                var origin = container.TranslatePoint(new Point(0, 0), this);

                if (point.X < origin.X + container.ActualWidth / 2)
                    return i;
            }

            return Descriptions.Count;
        }

        // ------------------------------------------------------ chip gestures

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            _dragChipIndex = FindChipIndex(e.OriginalSource as DependencyObject);

            if (_dragChipIndex < 0)
                return;

            // The dismiss glyph must not also start a reorder drag.
            _closePressed = (e.OriginalSource as FrameworkElement)?.Tag as string == "Close";

            _pointerDown = true;
            _dragOrigin = e.GetPosition(this);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!_pointerDown || _closePressed || e.LeftButton != MouseButtonState.Pressed)
                return;

            var point = e.GetPosition(this);

            if (Math.Abs(point.X - _dragOrigin.X) < SystemParameters.MinimumHorizontalDragDistance)
                return;

            if (!IsMouseCaptured)
            {
                // Announce the drag so a host can pin a level in place.
                var starting = new GroupChipEventArgs(GroupChipDragStartedEvent,
                    Descriptions[_dragChipIndex].ColumnName);

                RaiseEvent(starting);

                if (starting.Handled)
                {
                    _pointerDown = false;
                    return;
                }

                CaptureMouse();

                var container = _chipHost?.ItemContainerGenerator
                    .ContainerFromIndex(_dragChipIndex) as FrameworkElement;

                var grab = container == null
                    ? default
                    : (Point)(_dragOrigin - container.TranslatePoint(new Point(0, 0), this));

                RemovePreview();
                EnsurePreview(container, grab);
            }

            IsDropTarget = true;

            _preview?.UpdatePosition(point);
            _preview?.SetInsertion(GetInsertX(GetInsertIndex(point)), ActualHeight);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);

            if (!_pointerDown)
                return;

            var point = e.GetPosition(this);
            var wasDragging = IsMouseCaptured;

            _pointerDown = false;
            IsDropTarget = false;

            if (IsMouseCaptured)
                ReleaseMouseCapture();

            RemovePreview();

            var from = _dragChipIndex;
            var wasClose = _closePressed;

            _dragChipIndex = -1;
            _closePressed = false;

            if (from < 0 || Descriptions == null || from >= Descriptions.Count)
                return;

            if (wasClose)
            {
                RequestRemove(Descriptions[from].ColumnName);
                return;
            }

            if (!wasDragging)
            {
                // A click without a drag flips the group's sort direction.
                RaiseEvent(new GroupChipEventArgs(GroupSortToggledEvent, Descriptions[from].ColumnName));
                return;
            }

            var to = GetInsertIndex(point);

            if (to > from)
                to--;

            if (to != from)
                RaiseEvent(new GroupReorderEventArgs(GroupReorderedEvent, from, to));
        }

        /// <summary>Called by the chip's close button.</summary>
        internal void RequestRemove(string columnName) =>
            RaiseEvent(new GroupChipEventArgs(GroupRemovedEvent, columnName));

        private int FindChipIndex(DependencyObject source)
        {
            if (_chipHost == null || Descriptions == null)
                return -1;

            var current = source;

            while (current != null && !ReferenceEquals(current, this))
            {
                if (current is FrameworkElement element &&
                    element.DataContext is GroupColumnDescription description)
                {
                    return Descriptions.IndexOf(description);
                }

                current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
            }

            return -1;
        }
    }

}
