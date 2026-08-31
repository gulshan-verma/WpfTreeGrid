using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using TreeGrid.Wpf.Columns;
using TreeGrid.Wpf.Data;

namespace TreeGrid.Wpf.View
{
    public sealed class TreeNodeRoutedEventArgs : RoutedEventArgs
    {
        public TreeNodeRoutedEventArgs(RoutedEvent routedEvent, TreeNode node) : base(routedEvent)
        {
            Node = node;
        }

        public TreeNode Node { get; }
    }

    public sealed class ColumnRoutedEventArgs : RoutedEventArgs
    {
        public ColumnRoutedEventArgs(RoutedEvent routedEvent, TreeGridColumn column) : base(routedEvent)
        {
            Column = column;
        }

        public TreeGridColumn Column { get; }
    }

    /// <summary>
    /// A single record cell. Instances are pooled and re-targeted rather than
    /// recreated, so everything mutable lives in dependency properties that can be
    /// reassigned cheaply.
    /// </summary>
    public class TreeGridCell : Control
    {
        public static readonly RoutedEvent ExpanderToggleEvent = EventManager.RegisterRoutedEvent(
            "ExpanderToggle", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TreeGridCell));

        public static readonly RoutedEvent NodeCheckToggleEvent = EventManager.RegisterRoutedEvent(
            "NodeCheckToggle", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TreeGridCell));

        public static readonly RoutedEvent CellValueToggleEvent = EventManager.RegisterRoutedEvent(
            "CellValueToggle", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TreeGridCell));

        public static readonly DependencyProperty DisplayTextProperty = DependencyProperty.Register(
            nameof(DisplayText), typeof(string), typeof(TreeGridCell), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty IsTreeColumnProperty = DependencyProperty.Register(
            nameof(IsTreeColumn), typeof(bool), typeof(TreeGridCell), new PropertyMetadata(false));

        public static readonly DependencyProperty IndentProperty = DependencyProperty.Register(
            nameof(Indent), typeof(double), typeof(TreeGridCell), new PropertyMetadata(0d));

        public static readonly DependencyProperty ShowExpanderProperty = DependencyProperty.Register(
            nameof(ShowExpander), typeof(bool), typeof(TreeGridCell), new PropertyMetadata(false));

        public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
            nameof(IsExpanded), typeof(bool), typeof(TreeGridCell), new PropertyMetadata(false));

        public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(
            nameof(IsLoading), typeof(bool), typeof(TreeGridCell), new PropertyMetadata(false));

        public static readonly DependencyProperty CellTextAlignmentProperty = DependencyProperty.Register(
            nameof(CellTextAlignment), typeof(TextAlignment), typeof(TreeGridCell),
            new PropertyMetadata(TextAlignment.Left));

        public static readonly DependencyProperty IsCellSelectedProperty = DependencyProperty.Register(
            nameof(IsCellSelected), typeof(bool), typeof(TreeGridCell), new PropertyMetadata(false));

        public static readonly DependencyProperty IsCurrentCellProperty = DependencyProperty.Register(
            nameof(IsCurrentCell), typeof(bool), typeof(TreeGridCell), new PropertyMetadata(false));

        public static readonly DependencyProperty ShowNodeCheckBoxProperty = DependencyProperty.Register(
            nameof(ShowNodeCheckBox), typeof(bool), typeof(TreeGridCell), new PropertyMetadata(false));

        public static readonly DependencyProperty NodeCheckStateProperty = DependencyProperty.Register(
            nameof(NodeCheckState), typeof(bool?), typeof(TreeGridCell), new PropertyMetadata(false));

        public static readonly DependencyProperty ShowValueCheckBoxProperty = DependencyProperty.Register(
            nameof(ShowValueCheckBox), typeof(bool), typeof(TreeGridCell), new PropertyMetadata(false));

        public static readonly DependencyProperty ValueCheckStateProperty = DependencyProperty.Register(
            nameof(ValueCheckState), typeof(bool?), typeof(TreeGridCell), new PropertyMetadata(false));

        public static readonly DependencyProperty IsEditingProperty = DependencyProperty.Register(
            nameof(IsEditing), typeof(bool), typeof(TreeGridCell), new PropertyMetadata(false));

        public static readonly DependencyProperty EditElementProperty = DependencyProperty.Register(
            nameof(EditElement), typeof(FrameworkElement), typeof(TreeGridCell), new PropertyMetadata(null));

        public static readonly DependencyProperty CustomDisplayElementProperty = DependencyProperty.Register(
            nameof(CustomDisplayElement), typeof(FrameworkElement), typeof(TreeGridCell), new PropertyMetadata(null));

        public static readonly DependencyProperty HasCustomDisplayProperty = DependencyProperty.Register(
            nameof(HasCustomDisplay), typeof(bool), typeof(TreeGridCell), new PropertyMetadata(false));

        public static readonly DependencyProperty HasErrorProperty = DependencyProperty.Register(
            nameof(HasError), typeof(bool), typeof(TreeGridCell), new PropertyMetadata(false));

        public static readonly DependencyProperty ErrorMessageProperty = DependencyProperty.Register(
            nameof(ErrorMessage), typeof(string), typeof(TreeGridCell), new PropertyMetadata(null));

        public static readonly DependencyProperty IsMergedCellProperty = DependencyProperty.Register(
            nameof(IsMergedCell), typeof(bool), typeof(TreeGridCell), new PropertyMetadata(false));

        // Brushes the template's triggers need. They cannot come from a theme
        // dictionary because they must be overridable per grid instance.
        public static readonly DependencyProperty GridLineBrushProperty = DependencyProperty.Register(
            nameof(GridLineBrush), typeof(Brush), typeof(TreeGridCell), new PropertyMetadata(null));

        public static readonly DependencyProperty ShowVerticalGridLineProperty = DependencyProperty.Register(
            nameof(ShowVerticalGridLine), typeof(bool), typeof(TreeGridCell), new PropertyMetadata(true));

        public static readonly DependencyProperty CurrentCellBorderBrushProperty = DependencyProperty.Register(
            nameof(CurrentCellBorderBrush), typeof(Brush), typeof(TreeGridCell), new PropertyMetadata(null));

        public static readonly DependencyProperty ErrorBrushProperty = DependencyProperty.Register(
            nameof(ErrorBrush), typeof(Brush), typeof(TreeGridCell), new PropertyMetadata(null));

        public static readonly DependencyProperty EditorBackgroundProperty = DependencyProperty.Register(
            nameof(EditorBackground), typeof(Brush), typeof(TreeGridCell), new PropertyMetadata(null));

        public static readonly DependencyProperty ExpanderGlyphBrushProperty = DependencyProperty.Register(
            nameof(ExpanderGlyphBrush), typeof(Brush), typeof(TreeGridCell), new PropertyMetadata(null));

        static TreeGridCell()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TreeGridCell),
                new FrameworkPropertyMetadata(typeof(TreeGridCell)));
        }

        public string DisplayText
        {
            get => (string)GetValue(DisplayTextProperty);
            set => SetValue(DisplayTextProperty, value);
        }

        public bool IsTreeColumn
        {
            get => (bool)GetValue(IsTreeColumnProperty);
            set => SetValue(IsTreeColumnProperty, value);
        }

        public double Indent
        {
            get => (double)GetValue(IndentProperty);
            set => SetValue(IndentProperty, value);
        }

        public bool ShowExpander
        {
            get => (bool)GetValue(ShowExpanderProperty);
            set => SetValue(ShowExpanderProperty, value);
        }

        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
            set => SetValue(IsLoadingProperty, value);
        }

        public TextAlignment CellTextAlignment
        {
            get => (TextAlignment)GetValue(CellTextAlignmentProperty);
            set => SetValue(CellTextAlignmentProperty, value);
        }

        /// <summary>True when the owning row is selected, or the cell itself in cell-selection mode.</summary>
        public bool IsCellSelected
        {
            get => (bool)GetValue(IsCellSelectedProperty);
            set => SetValue(IsCellSelectedProperty, value);
        }

        public bool IsCurrentCell
        {
            get => (bool)GetValue(IsCurrentCellProperty);
            set => SetValue(IsCurrentCellProperty, value);
        }

        /// <summary>Hierarchy checkbox shown in the expander column.</summary>
        public bool ShowNodeCheckBox
        {
            get => (bool)GetValue(ShowNodeCheckBoxProperty);
            set => SetValue(ShowNodeCheckBoxProperty, value);
        }

        public bool? NodeCheckState
        {
            get => (bool?)GetValue(NodeCheckStateProperty);
            set => SetValue(NodeCheckStateProperty, value);
        }

        /// <summary>Checkbox rendering a bound boolean (TreeGridCheckBoxColumn).</summary>
        public bool ShowValueCheckBox
        {
            get => (bool)GetValue(ShowValueCheckBoxProperty);
            set => SetValue(ShowValueCheckBoxProperty, value);
        }

        public bool? ValueCheckState
        {
            get => (bool?)GetValue(ValueCheckStateProperty);
            set => SetValue(ValueCheckStateProperty, value);
        }

        /// <summary>True while this cell hosts an open editor.</summary>
        public bool IsEditing
        {
            get => (bool)GetValue(IsEditingProperty);
            set => SetValue(IsEditingProperty, value);
        }

        public FrameworkElement EditElement
        {
            get => (FrameworkElement)GetValue(EditElementProperty);
            set => SetValue(EditElementProperty, value);
        }

        /// <summary>Element supplied by template, progress and hyperlink columns.</summary>
        public FrameworkElement CustomDisplayElement
        {
            get => (FrameworkElement)GetValue(CustomDisplayElementProperty);
            set => SetValue(CustomDisplayElementProperty, value);
        }

        public bool HasCustomDisplay
        {
            get => (bool)GetValue(HasCustomDisplayProperty);
            set => SetValue(HasCustomDisplayProperty, value);
        }

        public bool HasError
        {
            get => (bool)GetValue(HasErrorProperty);
            set => SetValue(HasErrorProperty, value);
        }

        public string ErrorMessage
        {
            get => (string)GetValue(ErrorMessageProperty);
            set => SetValue(ErrorMessageProperty, value);
        }

        public Brush GridLineBrush
        {
            get => (Brush)GetValue(GridLineBrushProperty);
            set => SetValue(GridLineBrushProperty, value);
        }

        public bool ShowVerticalGridLine
        {
            get => (bool)GetValue(ShowVerticalGridLineProperty);
            set => SetValue(ShowVerticalGridLineProperty, value);
        }

        public Brush CurrentCellBorderBrush
        {
            get => (Brush)GetValue(CurrentCellBorderBrushProperty);
            set => SetValue(CurrentCellBorderBrushProperty, value);
        }

        public Brush ErrorBrush
        {
            get => (Brush)GetValue(ErrorBrushProperty);
            set => SetValue(ErrorBrushProperty, value);
        }

        public Brush EditorBackground
        {
            get => (Brush)GetValue(EditorBackgroundProperty);
            set => SetValue(EditorBackgroundProperty, value);
        }

        public Brush ExpanderGlyphBrush
        {
            get => (Brush)GetValue(ExpanderGlyphBrushProperty);
            set => SetValue(ExpanderGlyphBrushProperty, value);
        }

        public TreeGridColumn Column { get; internal set; }

        public TreeNode Node { get; internal set; }

        public int ColumnIndex { get; internal set; } = -1;

        /// <summary>Rows spanned by a vertical merge. One when not merged.</summary>
        public int MergeRowSpan { get; internal set; } = 1;

        /// <summary>
        /// True when this cell spans several rows. A merged cell needs an opaque
        /// background and its own bottom border, because it is painted over the rows
        /// it covers rather than inside them.
        /// </summary>
        public bool IsMergedCell
        {
            get => (bool)GetValue(IsMergedCellProperty);
            set => SetValue(IsMergedCellProperty, value);
        }

        private ButtonBase _expander;
        private ToggleButton _nodeCheckBox;
        private ToggleButton _valueCheckBox;
        private TreeGridColumn _displayElementOwner;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_expander != null)
                _expander.Click -= OnExpanderClick;

            if (_nodeCheckBox != null)
                _nodeCheckBox.PreviewMouseLeftButtonDown -= OnNodeCheckBoxDown;

            if (_valueCheckBox != null)
                _valueCheckBox.PreviewMouseLeftButtonDown -= OnValueCheckBoxDown;

            _expander = GetTemplateChild("PART_Expander") as ButtonBase;
            _nodeCheckBox = GetTemplateChild("PART_NodeCheckBox") as ToggleButton;
            _valueCheckBox = GetTemplateChild("PART_ValueCheckBox") as ToggleButton;

            if (_expander != null)
                _expander.Click += OnExpanderClick;

            if (_nodeCheckBox != null)
                _nodeCheckBox.PreviewMouseLeftButtonDown += OnNodeCheckBoxDown;

            if (_valueCheckBox != null)
                _valueCheckBox.PreviewMouseLeftButtonDown += OnValueCheckBoxDown;
        }

        private void OnExpanderClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            RaiseEvent(new TreeNodeRoutedEventArgs(ExpanderToggleEvent, Node));
        }

        // Handled on preview-down so the click does not also change row selection.
        private void OnNodeCheckBoxDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            RaiseEvent(new TreeNodeRoutedEventArgs(NodeCheckToggleEvent, Node));
        }

        private void OnValueCheckBoxDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            RaiseEvent(new TreeNodeRoutedEventArgs(CellValueToggleEvent, Node));
        }

        /// <summary>
        /// Pushes the grid's resolved appearance onto this cell. Font properties are
        /// inherited by the template's children, so setting them here is enough.
        /// </summary>
        internal void ApplyVisualStyle(Styling.TreeGridVisualStyle style)
        {
            if (style == null)
                return;

            Foreground = style.CellForeground;
            FontSize = style.CellFontSize;
            FontWeight = style.CellFontWeight;

            if (style.CellFontFamily != null)
                FontFamily = style.CellFontFamily;

            Padding = style.CellPadding;

            GridLineBrush = style.GridLineBrush;
            ShowVerticalGridLine = style.ShowVerticalGridLines;
            CurrentCellBorderBrush = style.CurrentCellBorderBrush;
            ErrorBrush = style.ErrorBrush;
            EditorBackground = style.EditorBackground;
            ExpanderGlyphBrush = style.ExpanderGlyphBrush;
        }

        /// <summary>Opens an editor inside this cell.</summary>
        internal void BeginEdit(FrameworkElement editElement)
        {
            EditElement = editElement;
            IsEditing = editElement != null;

            if (editElement == null)
                return;

            // Deferred so the element is in the visual tree before focus is moved.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                editElement.Focus();
                Keyboard.Focus(editElement);
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        internal void EndEdit()
        {
            IsEditing = false;
            EditElement = null;
        }

        /// <summary>Re-targets a pooled cell onto a different node/column pair.</summary>
        internal void Bind(TreeNode node, TreeGridColumn column, int columnIndex,
            double indentPerLevel, double indentBase, bool showNodeCheckBox)
        {
            Node = node;
            Column = column;
            ColumnIndex = columnIndex;

            var raw = column.MappingName == null
                ? null
                : PropertyAccessor.GetValue(node.Item, column.MappingName);

            if (column is TreeGridCheckBoxColumn checkColumn)
            {
                ShowValueCheckBox = true;
                HasCustomDisplay = false;
                ValueCheckState = checkColumn.ToCheckState(raw);
                DisplayText = string.Empty;
            }
            else if (column.HasCustomDisplay)
            {
                ShowValueCheckBox = false;
                HasCustomDisplay = true;
                DisplayText = string.Empty;

                // Pooled cells may already hold an element from a different column type.
                if (CustomDisplayElement == null || !ReferenceEquals(_displayElementOwner, column))
                {
                    CustomDisplayElement = column.CreateDisplayElement();
                    _displayElementOwner = column;
                }

                column.PrepareDisplayElement(CustomDisplayElement, raw, node.Item);
            }
            else
            {
                ShowValueCheckBox = false;
                HasCustomDisplay = false;
                CustomDisplayElement = null;
                _displayElementOwner = null;
                DisplayText = column.FormatValue(raw);
            }

            CellTextAlignment = column.TextAlignment;

            if (IsTreeColumn)
            {
                Indent = indentBase + node.Level * indentPerLevel;
                ShowExpander = node.HasChildNodes;
                IsExpanded = node.IsExpanded;
                IsLoading = node.IsLoading;
                ShowNodeCheckBox = showNodeCheckBox;
                NodeCheckState = node.IsChecked;
            }
            else
            {
                Indent = 0;
                ShowExpander = false;
                ShowNodeCheckBox = false;
            }
        }

        /// <summary>
        /// Cheap refresh for state that changes far more often than content, so
        /// selection repaints do not have to re-read every bound property.
        /// </summary>
        internal void RefreshState(bool isSelected, bool isCurrent, string errorMessage = null)
        {
            IsCellSelected = isSelected;
            IsCurrentCell = isCurrent;
            ErrorMessage = errorMessage;
            HasError = !string.IsNullOrEmpty(errorMessage);

            if (IsTreeColumn && Node != null)
            {
                NodeCheckState = Node.IsChecked;
                IsExpanded = Node.IsExpanded;
                IsLoading = Node.IsLoading;
                ShowExpander = Node.HasChildNodes;
            }
        }
    }

    /// <summary>
    /// Header cell. Owns the resize gripper and the reorder drag gesture; the grid
    /// listens for the routed events rather than the cell mutating columns itself.
    /// </summary>
    public class TreeGridHeaderCell : Control
    {
        private const double DragThreshold = 4d;

        public static readonly RoutedEvent HeaderClickEvent = EventManager.RegisterRoutedEvent(
            "HeaderClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TreeGridHeaderCell));

        public static readonly RoutedEvent ColumnResizeEvent = EventManager.RegisterRoutedEvent(
            "ColumnResize", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TreeGridHeaderCell));

        public static readonly RoutedEvent ColumnAutoFitEvent = EventManager.RegisterRoutedEvent(
            "ColumnAutoFit", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TreeGridHeaderCell));

        public static readonly RoutedEvent ColumnDragEvent = EventManager.RegisterRoutedEvent(
            "ColumnDrag", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TreeGridHeaderCell));

        public static readonly RoutedEvent FilterButtonClickEvent = EventManager.RegisterRoutedEvent(
            "FilterButtonClick", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TreeGridHeaderCell));

        public static readonly DependencyProperty HeaderTextProperty = DependencyProperty.Register(
            nameof(HeaderText), typeof(string), typeof(TreeGridHeaderCell), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty SortDirectionProperty = DependencyProperty.Register(
            nameof(SortDirection), typeof(ListSortDirectionOrNone), typeof(TreeGridHeaderCell),
            new PropertyMetadata(ListSortDirectionOrNone.None));

        public static readonly DependencyProperty CellTextAlignmentProperty = DependencyProperty.Register(
            nameof(CellTextAlignment), typeof(TextAlignment), typeof(TreeGridHeaderCell),
            new PropertyMetadata(TextAlignment.Left));

        public static readonly DependencyProperty CanResizeProperty = DependencyProperty.Register(
            nameof(CanResize), typeof(bool), typeof(TreeGridHeaderCell), new PropertyMetadata(true));

        public static readonly DependencyProperty SortNumberProperty = DependencyProperty.Register(
            nameof(SortNumber), typeof(int), typeof(TreeGridHeaderCell), new PropertyMetadata(0));

        public static readonly DependencyProperty ShowSortNumberProperty = DependencyProperty.Register(
            nameof(ShowSortNumber), typeof(bool), typeof(TreeGridHeaderCell), new PropertyMetadata(false));

        public static readonly DependencyProperty ShowFilterButtonProperty = DependencyProperty.Register(
            nameof(ShowFilterButton), typeof(bool), typeof(TreeGridHeaderCell), new PropertyMetadata(false));

        public static readonly DependencyProperty IsFilterAppliedProperty = DependencyProperty.Register(
            nameof(IsFilterApplied), typeof(bool), typeof(TreeGridHeaderCell), new PropertyMetadata(false));

        static TreeGridHeaderCell()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TreeGridHeaderCell),
                new FrameworkPropertyMetadata(typeof(TreeGridHeaderCell)));
        }

        public string HeaderText
        {
            get => (string)GetValue(HeaderTextProperty);
            set => SetValue(HeaderTextProperty, value);
        }

        public ListSortDirectionOrNone SortDirection
        {
            get => (ListSortDirectionOrNone)GetValue(SortDirectionProperty);
            set => SetValue(SortDirectionProperty, value);
        }

        public TextAlignment CellTextAlignment
        {
            get => (TextAlignment)GetValue(CellTextAlignmentProperty);
            set => SetValue(CellTextAlignmentProperty, value);
        }

        public bool CanResize
        {
            get => (bool)GetValue(CanResizeProperty);
            set => SetValue(CanResizeProperty, value);
        }

        /// <summary>Position of this column in a multi-column sort, 1-based. Zero when unsorted.</summary>
        public int SortNumber
        {
            get => (int)GetValue(SortNumberProperty);
            set => SetValue(SortNumberProperty, value);
        }

        public bool ShowSortNumber
        {
            get => (bool)GetValue(ShowSortNumberProperty);
            set => SetValue(ShowSortNumberProperty, value);
        }

        public bool ShowFilterButton
        {
            get => (bool)GetValue(ShowFilterButtonProperty);
            set => SetValue(ShowFilterButtonProperty, value);
        }

        public bool IsFilterApplied
        {
            get => (bool)GetValue(IsFilterAppliedProperty);
            set => SetValue(IsFilterAppliedProperty, value);
        }

        public TreeGridColumn Column { get; internal set; }

        /// <summary>The filter button, used as the popup's placement anchor.</summary>
        internal FrameworkElement FilterButtonElement => _filterButton;

        private Thumb _gripper;
        private ButtonBase _filterButton;
        private Point _dragOrigin;
        private bool _isDragging;
        private bool _pointerDown;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_gripper != null)
            {
                _gripper.DragDelta -= OnGripperDragDelta;
                _gripper.DragCompleted -= OnGripperDragCompleted;
                _gripper.MouseDoubleClick -= OnGripperDoubleClick;
            }

            if (_filterButton != null)
                _filterButton.Click -= OnFilterButtonClick;

            _filterButton = GetTemplateChild("PART_FilterButton") as ButtonBase;

            if (_filterButton != null)
                _filterButton.Click += OnFilterButtonClick;

            _gripper = GetTemplateChild("PART_ResizeGripper") as Thumb;

            if (_gripper != null)
            {
                _gripper.DragDelta += OnGripperDragDelta;
                _gripper.DragCompleted += OnGripperDragCompleted;
                _gripper.MouseDoubleClick += OnGripperDoubleClick;
            }
        }

        internal void Bind(TreeGridColumn column)
        {
            Column = column;
            HeaderText = column.ResolvedHeaderText;
            CellTextAlignment = column.TextAlignment;
            CanResize = column.AllowResizing;
        }

        /// <summary>Applies sort and filter indicators without re-reading the column.</summary>
        internal void RefreshIndicators(ListSortDirectionOrNone direction, int sortNumber,
            bool showSortNumber, bool showFilterButton, bool isFilterApplied)
        {
            SortDirection = direction;
            SortNumber = sortNumber;
            ShowSortNumber = showSortNumber && sortNumber > 0;
            ShowFilterButton = showFilterButton;
            IsFilterApplied = isFilterApplied;
        }

        private void OnFilterButtonClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            RaiseEvent(new ColumnRoutedEventArgs(FilterButtonClickEvent, Column));
        }

        private void OnGripperDragDelta(object sender, DragDeltaEventArgs e)
        {
            if (Column == null || !Column.AllowResizing)
                return;

            e.Handled = true;
            RaiseEvent(new ColumnResizeEventArgs(ColumnResizeEvent, Column, e.HorizontalChange, false));
        }

        private void OnGripperDragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (Column == null)
                return;

            e.Handled = true;
            RaiseEvent(new ColumnResizeEventArgs(ColumnResizeEvent, Column, 0, true));
        }

        private void OnGripperDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (Column == null)
                return;

            e.Handled = true;
            RaiseEvent(new ColumnAutoFitEventArgs(ColumnAutoFitEvent, Column));
        }

        // ------------------------------------------------------ reorder gesture

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            // Clicks on the filter button open the popup; they are not a reorder gesture.
            if (_filterButton != null && _filterButton.IsMouseOver)
                return;

            _pointerDown = true;
            _dragOrigin = e.GetPosition(this);
            CaptureMouse();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (!_pointerDown || Column == null)
                return;

            var position = e.GetPosition(this);

            if (!_isDragging)
            {
                if (Math.Abs(position.X - _dragOrigin.X) < DragThreshold)
                    return;

                _isDragging = true;
                RaiseEvent(new ColumnDragEventArgs(ColumnDragEvent, Column, PointToScreen(position), ColumnDragPhase.Started));
                return;
            }

            RaiseEvent(new ColumnDragEventArgs(ColumnDragEvent, Column, PointToScreen(position), ColumnDragPhase.Moved));
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);

            if (!_pointerDown)
                return;

            var position = e.GetPosition(this);
            ReleaseMouseCapture();
            _pointerDown = false;

            if (_isDragging)
            {
                _isDragging = false;
                RaiseEvent(new ColumnDragEventArgs(ColumnDragEvent, Column, PointToScreen(position), ColumnDragPhase.Completed));
                return;
            }

            RaiseEvent(new ColumnRoutedEventArgs(HeaderClickEvent, Column));
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);

            if (!_isDragging)
                return;

            _isDragging = false;
            _pointerDown = false;
            RaiseEvent(new ColumnDragEventArgs(ColumnDragEvent, Column, default, ColumnDragPhase.Cancelled));
        }
    }

    public enum ListSortDirectionOrNone
    {
        None,
        Ascending,
        Descending
    }
}
