using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TreeGrid.Wpf.ClipboardSupport;
using TreeGrid.Wpf.Columns;
using TreeGrid.Wpf.ContextMenus;
using TreeGrid.Wpf.Data;
using TreeGrid.Wpf.DragDropSupport;
using TreeGrid.Wpf.Editing;
using TreeGrid.Wpf.Export;
using TreeGrid.Wpf.Filtering;
using TreeGrid.Wpf.Grouping;
using TreeGrid.Wpf.Headers;
using TreeGrid.Wpf.Merging;
using TreeGrid.Wpf.Selection;
using TreeGrid.Wpf.Sorting;
using TreeGrid.Wpf.Styling;
using TreeGrid.Wpf.Validation;
using TreeGrid.Wpf.View;

namespace TreeGrid.Wpf
{
    [TemplatePart(Name = PartFooter, Type = typeof(TreeGridFooterControl))]
    [TemplatePart(Name = PartGroupDropArea, Type = typeof(GroupDropAreaControl))]
    [TemplatePart(Name = PartHeaderHost, Type = typeof(Border))]
    [TemplatePart(Name = PartScrollViewer, Type = typeof(ScrollViewer))]
    [TemplatePart(Name = PartVisualContainer, Type = typeof(VisualContainer))]
    public class TreeGridControl : Control, ITreeGridColumnHost
    {
        private const string PartFooter = "PART_Footer";
        private const string PartGroupDropArea = "PART_GroupDropArea";
        private const string PartHeaderHost = "PART_HeaderHost";
        private const string PartScrollViewer = "PART_ScrollViewer";
        private const string PartVisualContainer = "PART_VisualContainer";

        private readonly TreeGridDataSource _dataSource = new TreeGridDataSource();
        private readonly ColumnLayout _layout = new ColumnLayout();
        private TreeGridRowControl _headerRow;
        private Border _headerHost;
        private ScrollViewer _scrollViewer;
        private VisualContainer _container;
        private bool _templateApplied;

        private SelectionController _selection;
        private readonly CheckStateController _checkState = new CheckStateController();
        private ColumnReorderAdorner _reorderAdorner;
        private TreeGridColumn _draggedColumn;
        private int _dropIndex = -1;
        private bool _autoFitPassPending;
        private bool _suppressColumnNotifications;
        private bool _syncingSelectedItem;

        private readonly SortController _sortController;
        private readonly FilterController _filterController = new FilterController();
        private Popup _filterPopup;
        private FilterPopupControl _filterPopupContent;

        private readonly EditController _editController = new EditController();
        private readonly CellMergeController _mergeController = new CellMergeController();
        private readonly RowDragDropController _dragController = new RowDragDropController();
        private RowDropAdorner _dropAdorner;
        private Point _dragOrigin;
        private bool _dragPending;
        private readonly ClipboardController _clipboard = new ClipboardController();
        private readonly GroupController _groupController = new GroupController();
        private GroupDropAreaControl _groupDropArea;
        private TreeGridFooterControl _footer;
        private List<TreeNode> _ungroupedRoots;
        private Dictionary<object, TreeNode> _groupedNodeMap;
        private bool _dropIntoGroupArea;
        private TreeGridColumn _headerDragColumn;
        private Point _headerDragOrigin;
        private bool _headerDragging;
        private int _groupDropIndex = -1;
        private GroupingChangedEventArgs _pendingGroupChange;
        private StackPanel _stackedHeaderPanel;
        private readonly List<StackedHeaderRowControl> _stackedRows = new List<StackedHeaderRowControl>();

        static TreeGridControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TreeGridControl),
                new FrameworkPropertyMetadata(typeof(TreeGridControl)));
        }

        public TreeGridControl()
        {
            Columns = new TreeGridColumns();
            Columns.CollectionChanged += OnColumnsChanged;

            _dataSource.View.Changed += OnFlatViewChanged;
            _dataSource.SourceReset += OnSourceReset;

            SortComparers = new SortComparers();
            _sortController = new SortController(SortComparers) { ColumnResolver = FindColumn };
            _sortController.SortDescriptions.CollectionChanged += (s2, e2) => ApplySorting();
            _filterController.FilterChanged += OnFilterControllerChanged;

            StackedHeaderRows = new StackedHeaderRows();
            StackedHeaderRows.CollectionChanged += (s2, e2) => RebuildStackedHeaders();

            _editController.CellBeginEdit += (s2, e2) => CellBeginEdit?.Invoke(this, e2);
            _editController.CellEndEdit += OnCellEndEditInternal;
            _editController.CellValidating += (s2, e2) => CellValidating?.Invoke(this, e2);
            _editController.RowValidating += (s2, e2) => RowValidating?.Invoke(this, e2);
            _editController.ErrorsChanged += (s2, e2) => _container?.RefreshRowStates();

            _dragController.DragStarting += (s2, e2) => RowDragStarting?.Invoke(this, e2);
            _dragController.DragOver += (s2, e2) => RowDragOver?.Invoke(this, e2);
            _dragController.Dropped += (s2, e2) => RowDropped?.Invoke(this, e2);

            _clipboard.CopyContent += (s2, e2) => CopyContent?.Invoke(this, e2);
            _clipboard.PasteContent += (s2, e2) => PasteContent?.Invoke(this, e2);

            _dataSource.IncrementalChange += OnIncrementalChange;

            _groupController.Descriptions.CollectionChanged += (s2, e2) =>
            {
                _groupDropArea?.Refresh();
                ApplyGrouping();
            };

            _selection = new SelectionController(() => _dataSource.View, () => _layout.VisibleColumns.Count);
            _selection.SelectionChanged += OnSelectionControllerChanged;
            _selection.CurrentCellChanged += OnCurrentCellChanged;
            _checkState.NodeChecked += OnNodeCheckedInternal;

            AddHandler(TreeGridCell.ExpanderToggleEvent, new RoutedEventHandler(OnExpanderToggle));
            AddHandler(TreeGridCell.NodeCheckToggleEvent, new RoutedEventHandler(OnNodeCheckToggle));
            AddHandler(TreeGridCell.CellValueToggleEvent, new RoutedEventHandler(OnCellValueToggle));
            AddHandler(TreeGridHeaderCell.ColumnResizeEvent, new RoutedEventHandler(OnColumnResize));
            AddHandler(TreeGridHeaderCell.ColumnAutoFitEvent, new RoutedEventHandler(OnColumnAutoFit));
            AddHandler(TreeGridHeaderCell.HeaderPointerDownEvent, new RoutedEventHandler(OnHeaderPointerDown));
            AddHandler(TreeGridHeaderCell.FilterButtonClickEvent, new RoutedEventHandler(OnFilterButtonClick));
        }

        // ------------------------------------------------------------ properties

        public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
            nameof(ItemsSource), typeof(IEnumerable), typeof(TreeGridControl),
            new PropertyMetadata(null, OnItemsSourceChanged));

        public static readonly DependencyProperty ChildPropertyNameProperty = DependencyProperty.Register(
            nameof(ChildPropertyName), typeof(string), typeof(TreeGridControl),
            new PropertyMetadata(null, OnBindingConfigChanged));

        public static readonly DependencyProperty IdPropertyNameProperty = DependencyProperty.Register(
            nameof(IdPropertyName), typeof(string), typeof(TreeGridControl),
            new PropertyMetadata(null, OnBindingConfigChanged));

        public static readonly DependencyProperty ParentIdPropertyNameProperty = DependencyProperty.Register(
            nameof(ParentIdPropertyName), typeof(string), typeof(TreeGridControl),
            new PropertyMetadata(null, OnBindingConfigChanged));

        public static readonly DependencyProperty SelfRelationRootValueProperty = DependencyProperty.Register(
            nameof(SelfRelationRootValue), typeof(object), typeof(TreeGridControl),
            new PropertyMetadata(null, OnBindingConfigChanged));

        public static readonly DependencyProperty AutoGenerateColumnsProperty = DependencyProperty.Register(
            nameof(AutoGenerateColumns), typeof(bool), typeof(TreeGridControl), new PropertyMetadata(true));

        public static readonly DependencyProperty RowHeightProperty = DependencyProperty.Register(
            nameof(RowHeight), typeof(double), typeof(TreeGridControl),
            new PropertyMetadata(26d, OnVisualConfigChanged));

        public static readonly DependencyProperty HeaderRowHeightProperty = DependencyProperty.Register(
            nameof(HeaderRowHeight), typeof(double), typeof(TreeGridControl),
            new PropertyMetadata(30d, OnVisualConfigChanged));

        public static readonly DependencyProperty IndentPerLevelProperty = DependencyProperty.Register(
            nameof(IndentPerLevel), typeof(double), typeof(TreeGridControl),
            new PropertyMetadata(18d, OnVisualConfigChanged));

        public static readonly DependencyProperty ExpanderColumnIndexProperty = DependencyProperty.Register(
            nameof(ExpanderColumnIndex), typeof(int), typeof(TreeGridControl),
            new PropertyMetadata(0, OnVisualConfigChanged));

        public static readonly DependencyProperty FrozenColumnCountProperty = DependencyProperty.Register(
            nameof(FrozenColumnCount), typeof(int), typeof(TreeGridControl),
            new PropertyMetadata(0, OnVisualConfigChanged));

        public static readonly DependencyProperty FooterColumnCountProperty = DependencyProperty.Register(
            nameof(FooterColumnCount), typeof(int), typeof(TreeGridControl),
            new PropertyMetadata(0, OnVisualConfigChanged));

        public static readonly DependencyProperty ShowAlternatingRowsProperty = DependencyProperty.Register(
            nameof(ShowAlternatingRows), typeof(bool), typeof(TreeGridControl),
            new PropertyMetadata(false, OnVisualConfigChanged));

        public static readonly DependencyProperty AlternatingRowBackgroundProperty = DependencyProperty.Register(
            nameof(AlternatingRowBackground), typeof(Brush), typeof(TreeGridControl),
            new PropertyMetadata(null, OnVisualConfigChanged));

        public static readonly DependencyProperty SelectedRowBackgroundProperty = DependencyProperty.Register(
            nameof(SelectedRowBackground), typeof(Brush), typeof(TreeGridControl),
            new PropertyMetadata(null, OnVisualConfigChanged));

        public static readonly DependencyProperty GridLineBrushProperty = DependencyProperty.Register(
            nameof(GridLineBrush), typeof(Brush), typeof(TreeGridControl),
            new PropertyMetadata(null, OnVisualConfigChanged));

        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
            nameof(SelectedItem), typeof(object), typeof(TreeGridControl),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

        public static readonly DependencyProperty SelectionModeProperty = DependencyProperty.Register(
            nameof(SelectionMode), typeof(GridSelectionMode), typeof(TreeGridControl),
            new PropertyMetadata(GridSelectionMode.Extended, OnSelectionConfigChanged));

        public static readonly DependencyProperty SelectionUnitProperty = DependencyProperty.Register(
            nameof(SelectionUnit), typeof(GridSelectionUnit), typeof(TreeGridControl),
            new PropertyMetadata(GridSelectionUnit.Row, OnSelectionConfigChanged));

        public static readonly DependencyProperty AllowCheckBoxSelectionProperty = DependencyProperty.Register(
            nameof(AllowCheckBoxSelection), typeof(bool), typeof(TreeGridControl),
            new PropertyMetadata(false, OnVisualConfigChanged));

        public static readonly DependencyProperty CheckBoxCascadeModeProperty = DependencyProperty.Register(
            nameof(CheckBoxCascadeMode), typeof(CheckBoxCascadeMode), typeof(TreeGridControl),
            new PropertyMetadata(CheckBoxCascadeMode.SynchronizeWithParentAndChildren, OnSelectionConfigChanged));

        public static readonly DependencyProperty AllowColumnResizingProperty = DependencyProperty.Register(
            nameof(AllowColumnResizing), typeof(bool), typeof(TreeGridControl), new PropertyMetadata(true));

        public static readonly DependencyProperty AllowColumnReorderingProperty = DependencyProperty.Register(
            nameof(AllowColumnReordering), typeof(bool), typeof(TreeGridControl), new PropertyMetadata(true));

        public static readonly DependencyProperty DropIndicatorBrushProperty = DependencyProperty.Register(
            nameof(DropIndicatorBrush), typeof(Brush), typeof(TreeGridControl), new PropertyMetadata(null));

        public static readonly DependencyProperty AllowSortingProperty = DependencyProperty.Register(
            nameof(AllowSorting), typeof(bool), typeof(TreeGridControl), new PropertyMetadata(true));

        public static readonly DependencyProperty AllowMultiSortProperty = DependencyProperty.Register(
            nameof(AllowMultiSort), typeof(bool), typeof(TreeGridControl), new PropertyMetadata(true));

        public static readonly DependencyProperty ShowSortNumbersProperty = DependencyProperty.Register(
            nameof(ShowSortNumbers), typeof(bool), typeof(TreeGridControl),
            new PropertyMetadata(true, OnIndicatorConfigChanged));

        public static readonly DependencyProperty AllowFilteringProperty = DependencyProperty.Register(
            nameof(AllowFiltering), typeof(bool), typeof(TreeGridControl),
            new PropertyMetadata(false, OnIndicatorConfigChanged));

        public static readonly DependencyProperty FilterNodeModeProperty = DependencyProperty.Register(
            nameof(FilterNodeMode), typeof(FilterNodeMode), typeof(TreeGridControl),
            new PropertyMetadata(FilterNodeMode.MatchingAndParentNodes, OnFilterModeChanged));

        public static readonly DependencyProperty ExpandNodesOnFilteringProperty = DependencyProperty.Register(
            nameof(ExpandNodesOnFiltering), typeof(bool), typeof(TreeGridControl), new PropertyMetadata(true));

        public static readonly DependencyProperty AllowEditingProperty = DependencyProperty.Register(
            nameof(AllowEditing), typeof(bool), typeof(TreeGridControl), new PropertyMetadata(false));

        public static readonly DependencyProperty EditTriggerProperty = DependencyProperty.Register(
            nameof(EditTrigger), typeof(EditTrigger), typeof(TreeGridControl),
            new PropertyMetadata(EditTrigger.OnDoubleTap));

        public static readonly DependencyProperty ValidationModeProperty = DependencyProperty.Register(
            nameof(ValidationMode), typeof(GridValidationMode), typeof(TreeGridControl),
            new PropertyMetadata(GridValidationMode.Cell, OnValidationModeChanged));

        public static readonly DependencyProperty AllowMergeCellsProperty = DependencyProperty.Register(
            nameof(AllowMergeCells), typeof(bool), typeof(TreeGridControl),
            new PropertyMetadata(false, OnMergeConfigChanged));

        public static readonly DependencyProperty MergeOnlyWithinSiblingsProperty = DependencyProperty.Register(
            nameof(MergeOnlyWithinSiblings), typeof(bool), typeof(TreeGridControl),
            new PropertyMetadata(true, OnMergeConfigChanged));

        public static readonly DependencyProperty AllowRowDragDropProperty = DependencyProperty.Register(
            nameof(AllowRowDragDrop), typeof(bool), typeof(TreeGridControl), new PropertyMetadata(false));

        public static readonly DependencyProperty StackedHeaderRowHeightProperty = DependencyProperty.Register(
            nameof(StackedHeaderRowHeight), typeof(double), typeof(TreeGridControl),
            new PropertyMetadata(28d, OnVisualConfigChanged));

        public static readonly DependencyProperty FrozenLineBrushProperty = DependencyProperty.Register(
            nameof(FrozenLineBrush), typeof(Brush), typeof(TreeGridControl),
            new PropertyMetadata(null, OnVisualConfigChanged));

        public static readonly DependencyProperty MergedCellBackgroundProperty = DependencyProperty.Register(
            nameof(MergedCellBackground), typeof(Brush), typeof(TreeGridControl),
            new PropertyMetadata(null, OnVisualConfigChanged));

        public static readonly DependencyProperty AllowCopyProperty = DependencyProperty.Register(
            nameof(AllowCopy), typeof(bool), typeof(TreeGridControl), new PropertyMetadata(true));

        public static readonly DependencyProperty AllowPasteProperty = DependencyProperty.Register(
            nameof(AllowPaste), typeof(bool), typeof(TreeGridControl), new PropertyMetadata(false));

        public static readonly DependencyProperty ShowDefaultContextMenusProperty = DependencyProperty.Register(
            nameof(ShowDefaultContextMenus), typeof(bool), typeof(TreeGridControl), new PropertyMetadata(false));

        public static readonly DependencyProperty RecordContextMenuProperty = DependencyProperty.Register(
            nameof(RecordContextMenu), typeof(ContextMenu), typeof(TreeGridControl), new PropertyMetadata(null));

        public static readonly DependencyProperty HeaderContextMenuProperty = DependencyProperty.Register(
            nameof(HeaderContextMenu), typeof(ContextMenu), typeof(TreeGridControl), new PropertyMetadata(null));

        public static readonly DependencyProperty ExpanderContextMenuProperty = DependencyProperty.Register(
            nameof(ExpanderContextMenu), typeof(ContextMenu), typeof(TreeGridControl), new PropertyMetadata(null));

        // ------------------------------------------------------ appearance

        public static readonly DependencyProperty HeaderBackgroundProperty = DependencyProperty.Register(
            nameof(HeaderBackground), typeof(Brush), typeof(TreeGridControl),
            new PropertyMetadata(null, OnVisualConfigChanged));

        public static readonly DependencyProperty HeaderForegroundProperty = DependencyProperty.Register(
            nameof(HeaderForeground), typeof(Brush), typeof(TreeGridControl),
            new PropertyMetadata(null, OnVisualConfigChanged));

        public static readonly DependencyProperty HeaderBorderBrushProperty = DependencyProperty.Register(
            nameof(HeaderBorderBrush), typeof(Brush), typeof(TreeGridControl),
            new PropertyMetadata(null, OnVisualConfigChanged));

        public static readonly DependencyProperty HeaderFontSizeProperty = DependencyProperty.Register(
            nameof(HeaderFontSize), typeof(double), typeof(TreeGridControl),
            new PropertyMetadata(12d, OnVisualConfigChanged));

        public static readonly DependencyProperty HeaderFontWeightProperty = DependencyProperty.Register(
            nameof(HeaderFontWeight), typeof(FontWeight), typeof(TreeGridControl),
            new PropertyMetadata(FontWeights.SemiBold, OnVisualConfigChanged));

        public static readonly DependencyProperty HeaderFontFamilyProperty = DependencyProperty.Register(
            nameof(HeaderFontFamily), typeof(FontFamily), typeof(TreeGridControl),
            new PropertyMetadata(null, OnVisualConfigChanged));

        public static readonly DependencyProperty RowBackgroundProperty = DependencyProperty.Register(
            nameof(RowBackground), typeof(Brush), typeof(TreeGridControl),
            new PropertyMetadata(null, OnVisualConfigChanged));

        public static readonly DependencyProperty HoverRowBackgroundProperty = DependencyProperty.Register(
            nameof(HoverRowBackground), typeof(Brush), typeof(TreeGridControl),
            new PropertyMetadata(null, OnVisualConfigChanged));

        public static readonly DependencyProperty SelectedRowForegroundProperty = DependencyProperty.Register(
            nameof(SelectedRowForeground), typeof(Brush), typeof(TreeGridControl),
            new PropertyMetadata(null, OnVisualConfigChanged));

        public static readonly DependencyProperty CellForegroundProperty = DependencyProperty.Register(
            nameof(CellForeground), typeof(Brush), typeof(TreeGridControl),
            new PropertyMetadata(null, OnVisualConfigChanged));

        public static readonly DependencyProperty CellFontSizeProperty = DependencyProperty.Register(
            nameof(CellFontSize), typeof(double), typeof(TreeGridControl),
            new PropertyMetadata(12d, OnVisualConfigChanged));

        public static readonly DependencyProperty CellFontWeightProperty = DependencyProperty.Register(
            nameof(CellFontWeight), typeof(FontWeight), typeof(TreeGridControl),
            new PropertyMetadata(FontWeights.Normal, OnVisualConfigChanged));

        public static readonly DependencyProperty CellFontFamilyProperty = DependencyProperty.Register(
            nameof(CellFontFamily), typeof(FontFamily), typeof(TreeGridControl),
            new PropertyMetadata(null, OnVisualConfigChanged));

        public static readonly DependencyProperty CellPaddingProperty = DependencyProperty.Register(
            nameof(CellPadding), typeof(Thickness), typeof(TreeGridControl),
            new PropertyMetadata(new Thickness(6, 0, 6, 0), OnVisualConfigChanged));

        public static readonly DependencyProperty GridLinesVisibilityProperty = DependencyProperty.Register(
            nameof(GridLinesVisibility), typeof(GridLinesVisibility), typeof(TreeGridControl),
            new PropertyMetadata(GridLinesVisibility.Both, OnVisualConfigChanged));

        public static readonly DependencyProperty CurrentCellBorderBrushProperty = DependencyProperty.Register(
            nameof(CurrentCellBorderBrush), typeof(Brush), typeof(TreeGridControl),
            new PropertyMetadata(null, OnVisualConfigChanged));

        public static readonly DependencyProperty ErrorBrushProperty = DependencyProperty.Register(
            nameof(ErrorBrush), typeof(Brush), typeof(TreeGridControl),
            new PropertyMetadata(null, OnVisualConfigChanged));

        public static readonly DependencyProperty EditorBackgroundProperty = DependencyProperty.Register(
            nameof(EditorBackground), typeof(Brush), typeof(TreeGridControl),
            new PropertyMetadata(null, OnVisualConfigChanged));

        public static readonly DependencyProperty ExpanderGlyphBrushProperty = DependencyProperty.Register(
            nameof(ExpanderGlyphBrush), typeof(Brush), typeof(TreeGridControl),
            new PropertyMetadata(null, OnVisualConfigChanged));

        public static readonly DependencyProperty ShowFooterProperty = DependencyProperty.Register(
            nameof(ShowFooter), typeof(bool), typeof(TreeGridControl),
            new PropertyMetadata(true, OnVisualConfigChanged));

        public static readonly DependencyProperty FooterHeightProperty = DependencyProperty.Register(
            nameof(FooterHeight), typeof(double), typeof(TreeGridControl),
            new PropertyMetadata(26d, OnVisualConfigChanged));

        public static readonly DependencyProperty FooterContentProperty = DependencyProperty.Register(
            nameof(FooterContent), typeof(object), typeof(TreeGridControl),
            new PropertyMetadata(null, OnVisualConfigChanged));

        public static readonly DependencyProperty FooterStatusTextProperty = DependencyProperty.Register(
            nameof(FooterStatusText), typeof(string), typeof(TreeGridControl),
            new PropertyMetadata(null, OnVisualConfigChanged));

        public static readonly DependencyProperty AllowGroupingProperty = DependencyProperty.Register(
            nameof(AllowGrouping), typeof(bool), typeof(TreeGridControl), new PropertyMetadata(false));

        public static readonly DependencyProperty ShowGroupDropAreaProperty = DependencyProperty.Register(
            nameof(ShowGroupDropArea), typeof(bool), typeof(TreeGridControl),
            new PropertyMetadata(false, OnVisualConfigChanged));

        public static readonly DependencyProperty GroupDropAreaHeightProperty = DependencyProperty.Register(
            nameof(GroupDropAreaHeight), typeof(double), typeof(TreeGridControl),
            new PropertyMetadata(38d, OnVisualConfigChanged));

        public static readonly DependencyProperty ShowGroupItemCountProperty = DependencyProperty.Register(
            nameof(ShowGroupItemCount), typeof(bool), typeof(TreeGridControl),
            new PropertyMetadata(true, OnVisualConfigChanged));

        public static readonly DependencyProperty AutoExpandGroupsProperty = DependencyProperty.Register(
            nameof(AutoExpandGroups), typeof(bool), typeof(TreeGridControl), new PropertyMetadata(true));

        public static readonly DependencyProperty EnableColumnVirtualizationProperty = DependencyProperty.Register(
            nameof(EnableColumnVirtualization), typeof(bool), typeof(TreeGridControl),
            new PropertyMetadata(true, OnVisualConfigChanged));

        public IEnumerable ItemsSource
        {
            get => (IEnumerable)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public string ChildPropertyName
        {
            get => (string)GetValue(ChildPropertyNameProperty);
            set => SetValue(ChildPropertyNameProperty, value);
        }

        public string IdPropertyName
        {
            get => (string)GetValue(IdPropertyNameProperty);
            set => SetValue(IdPropertyNameProperty, value);
        }

        public string ParentIdPropertyName
        {
            get => (string)GetValue(ParentIdPropertyNameProperty);
            set => SetValue(ParentIdPropertyNameProperty, value);
        }

        public object SelfRelationRootValue
        {
            get => GetValue(SelfRelationRootValueProperty);
            set => SetValue(SelfRelationRootValueProperty, value);
        }

        public bool AutoGenerateColumns
        {
            get => (bool)GetValue(AutoGenerateColumnsProperty);
            set => SetValue(AutoGenerateColumnsProperty, value);
        }

        public double RowHeight
        {
            get => (double)GetValue(RowHeightProperty);
            set => SetValue(RowHeightProperty, value);
        }

        public double HeaderRowHeight
        {
            get => (double)GetValue(HeaderRowHeightProperty);
            set => SetValue(HeaderRowHeightProperty, value);
        }

        public double IndentPerLevel
        {
            get => (double)GetValue(IndentPerLevelProperty);
            set => SetValue(IndentPerLevelProperty, value);
        }

        public int ExpanderColumnIndex
        {
            get => (int)GetValue(ExpanderColumnIndexProperty);
            set => SetValue(ExpanderColumnIndexProperty, value);
        }

        public int FrozenColumnCount
        {
            get => (int)GetValue(FrozenColumnCountProperty);
            set => SetValue(FrozenColumnCountProperty, value);
        }

        public int FooterColumnCount
        {
            get => (int)GetValue(FooterColumnCountProperty);
            set => SetValue(FooterColumnCountProperty, value);
        }

        public bool ShowAlternatingRows
        {
            get => (bool)GetValue(ShowAlternatingRowsProperty);
            set => SetValue(ShowAlternatingRowsProperty, value);
        }

        public Brush AlternatingRowBackground
        {
            get => (Brush)GetValue(AlternatingRowBackgroundProperty);
            set => SetValue(AlternatingRowBackgroundProperty, value);
        }

        public Brush SelectedRowBackground
        {
            get => (Brush)GetValue(SelectedRowBackgroundProperty);
            set => SetValue(SelectedRowBackgroundProperty, value);
        }

        public Brush GridLineBrush
        {
            get => (Brush)GetValue(GridLineBrushProperty);
            set => SetValue(GridLineBrushProperty, value);
        }

        public object SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public bool EnableColumnVirtualization
        {
            get => (bool)GetValue(EnableColumnVirtualizationProperty);
            set => SetValue(EnableColumnVirtualizationProperty, value);
        }

        public GridSelectionMode SelectionMode
        {
            get => (GridSelectionMode)GetValue(SelectionModeProperty);
            set => SetValue(SelectionModeProperty, value);
        }

        public GridSelectionUnit SelectionUnit
        {
            get => (GridSelectionUnit)GetValue(SelectionUnitProperty);
            set => SetValue(SelectionUnitProperty, value);
        }

        /// <summary>Shows a tri-state checkbox in the expander column.</summary>
        public bool AllowCheckBoxSelection
        {
            get => (bool)GetValue(AllowCheckBoxSelectionProperty);
            set => SetValue(AllowCheckBoxSelectionProperty, value);
        }

        public CheckBoxCascadeMode CheckBoxCascadeMode
        {
            get => (CheckBoxCascadeMode)GetValue(CheckBoxCascadeModeProperty);
            set => SetValue(CheckBoxCascadeModeProperty, value);
        }

        public bool AllowColumnResizing
        {
            get => (bool)GetValue(AllowColumnResizingProperty);
            set => SetValue(AllowColumnResizingProperty, value);
        }

        public bool AllowColumnReordering
        {
            get => (bool)GetValue(AllowColumnReorderingProperty);
            set => SetValue(AllowColumnReorderingProperty, value);
        }

        public Brush DropIndicatorBrush
        {
            get => (Brush)GetValue(DropIndicatorBrushProperty);
            set => SetValue(DropIndicatorBrushProperty, value);
        }

        /// <summary>Live view of the selected data items.</summary>
        public ReadOnlyObservableCollection<object> SelectedItems => _selection.SelectedItems;

        /// <summary>Live view of the checked data items.</summary>
        public ReadOnlyObservableCollection<object> CheckedItems => _checkState.CheckedItems;

        public GridIndex CurrentCell => _selection.CurrentCell;

        public bool AllowSorting
        {
            get => (bool)GetValue(AllowSortingProperty);
            set => SetValue(AllowSortingProperty, value);
        }

        /// <summary>Ctrl-click a header to add it to the sort rather than replace it.</summary>
        public bool AllowMultiSort
        {
            get => (bool)GetValue(AllowMultiSortProperty);
            set => SetValue(AllowMultiSortProperty, value);
        }

        public bool ShowSortNumbers
        {
            get => (bool)GetValue(ShowSortNumbersProperty);
            set => SetValue(ShowSortNumbersProperty, value);
        }

        public bool AllowFiltering
        {
            get => (bool)GetValue(AllowFilteringProperty);
            set => SetValue(AllowFilteringProperty, value);
        }

        /// <summary>How much of the hierarchy survives around a match.</summary>
        public FilterNodeMode FilterNodeMode
        {
            get => (FilterNodeMode)GetValue(FilterNodeModeProperty);
            set => SetValue(FilterNodeModeProperty, value);
        }

        /// <summary>Auto-expands surviving nodes so matches are visible immediately.</summary>
        public bool ExpandNodesOnFiltering
        {
            get => (bool)GetValue(ExpandNodesOnFilteringProperty);
            set => SetValue(ExpandNodesOnFilteringProperty, value);
        }

        /// <summary>The active multi-column sort. Mutate this to sort programmatically.</summary>
        public SortColumnDescriptions SortColumnDescriptions => _sortController.SortDescriptions;

        /// <summary>Custom comparers registered per mapping name.</summary>
        public SortComparers SortComparers { get; }

        public FilterController FilterController => _filterController;

        /// <summary>Row-level filter applied on top of the column filters.</summary>
        public Predicate<object> FilterPredicate
        {
            get => _filterController.FilterPredicate;
            set
            {
                _filterController.FilterPredicate = value;
                ApplyFilters();
            }
        }

        /// <summary>Master switch for editing. Individual columns can still opt out.</summary>
        public bool AllowEditing
        {
            get => (bool)GetValue(AllowEditingProperty);
            set => SetValue(AllowEditingProperty, value);
        }

        public EditTrigger EditTrigger
        {
            get => (EditTrigger)GetValue(EditTriggerProperty);
            set => SetValue(EditTriggerProperty, value);
        }

        public GridValidationMode ValidationMode
        {
            get => (GridValidationMode)GetValue(ValidationModeProperty);
            set => SetValue(ValidationModeProperty, value);
        }

        /// <summary>
        /// Merges vertically adjacent cells holding the same value.
        /// <para>
        /// EXPERIMENTAL and off by default. The implementation is complete and the
        /// rendering issues are fixed, but it is not exercised by the demo and has had
        /// no real use, so treat it as unverified. Setting this true is the only thing
        /// needed to switch it back on.
        /// </para>
        /// </summary>
        public bool AllowMergeCells
        {
            get => (bool)GetValue(AllowMergeCellsProperty);
            set => SetValue(AllowMergeCellsProperty, value);
        }

        public bool MergeOnlyWithinSiblings
        {
            get => (bool)GetValue(MergeOnlyWithinSiblingsProperty);
            set => SetValue(MergeOnlyWithinSiblingsProperty, value);
        }

        public bool AllowRowDragDrop
        {
            get => (bool)GetValue(AllowRowDragDropProperty);
            set => SetValue(AllowRowDragDropProperty, value);
        }

        public double StackedHeaderRowHeight
        {
            get => (double)GetValue(StackedHeaderRowHeightProperty);
            set => SetValue(StackedHeaderRowHeightProperty, value);
        }

        public Brush FrozenLineBrush
        {
            get => (Brush)GetValue(FrozenLineBrushProperty);
            set => SetValue(FrozenLineBrushProperty, value);
        }

        /// <summary>
        /// Painted behind merged cells. Defaults to the grid's own Background, which is
        /// what makes a span read as one solid cell rather than a translucent overlay
        /// on the striped rows beneath it.
        /// </summary>
        public Brush MergedCellBackground
        {
            get => (Brush)GetValue(MergedCellBackgroundProperty);
            set => SetValue(MergedCellBackgroundProperty, value);
        }

        /// <summary>Spanning header rows drawn above the column headers.</summary>
        public StackedHeaderRows StackedHeaderRows { get; }

        public CellMergeController MergeController => _mergeController;

        public EditController EditController => _editController;

        public bool IsEditing => _editController.IsEditing;

        public bool AllowCopy
        {
            get => (bool)GetValue(AllowCopyProperty);
            set => SetValue(AllowCopyProperty, value);
        }

        public bool AllowPaste
        {
            get => (bool)GetValue(AllowPasteProperty);
            set => SetValue(AllowPasteProperty, value);
        }

        /// <summary>Builds stock menus for regions with no menu supplied.</summary>
        public bool ShowDefaultContextMenus
        {
            get => (bool)GetValue(ShowDefaultContextMenusProperty);
            set => SetValue(ShowDefaultContextMenusProperty, value);
        }

        public ContextMenu RecordContextMenu
        {
            get => (ContextMenu)GetValue(RecordContextMenuProperty);
            set => SetValue(RecordContextMenuProperty, value);
        }

        public ContextMenu HeaderContextMenu
        {
            get => (ContextMenu)GetValue(HeaderContextMenuProperty);
            set => SetValue(HeaderContextMenuProperty, value);
        }

        public ContextMenu ExpanderContextMenu
        {
            get => (ContextMenu)GetValue(ExpanderContextMenuProperty);
            set => SetValue(ExpanderContextMenuProperty, value);
        }

        public ClipboardController ClipboardController => _clipboard;

        /// <summary>The virtualizing panel. Exposed for diagnostics and benchmarking.</summary>
        public VisualContainer Container => _container;

        /// <summary>Header strip background. Falls back to the current theme when unset.</summary>
        public Brush HeaderBackground
        {
            get => (Brush)GetValue(HeaderBackgroundProperty);
            set => SetValue(HeaderBackgroundProperty, value);
        }

        public Brush HeaderForeground
        {
            get => (Brush)GetValue(HeaderForegroundProperty);
            set => SetValue(HeaderForegroundProperty, value);
        }

        public Brush HeaderBorderBrush
        {
            get => (Brush)GetValue(HeaderBorderBrushProperty);
            set => SetValue(HeaderBorderBrushProperty, value);
        }

        public double HeaderFontSize
        {
            get => (double)GetValue(HeaderFontSizeProperty);
            set => SetValue(HeaderFontSizeProperty, value);
        }

        public FontWeight HeaderFontWeight
        {
            get => (FontWeight)GetValue(HeaderFontWeightProperty);
            set => SetValue(HeaderFontWeightProperty, value);
        }

        public FontFamily HeaderFontFamily
        {
            get => (FontFamily)GetValue(HeaderFontFamilyProperty);
            set => SetValue(HeaderFontFamilyProperty, value);
        }

        /// <summary>Background for ordinary rows. Alternating rows use their own brush.</summary>
        public Brush RowBackground
        {
            get => (Brush)GetValue(RowBackgroundProperty);
            set => SetValue(RowBackgroundProperty, value);
        }

        /// <summary>Set this to enable row hover highlighting. Null disables it entirely.</summary>
        public Brush HoverRowBackground
        {
            get => (Brush)GetValue(HoverRowBackgroundProperty);
            set => SetValue(HoverRowBackgroundProperty, value);
        }

        public Brush SelectedRowForeground
        {
            get => (Brush)GetValue(SelectedRowForegroundProperty);
            set => SetValue(SelectedRowForegroundProperty, value);
        }

        public Brush CellForeground
        {
            get => (Brush)GetValue(CellForegroundProperty);
            set => SetValue(CellForegroundProperty, value);
        }

        public double CellFontSize
        {
            get => (double)GetValue(CellFontSizeProperty);
            set => SetValue(CellFontSizeProperty, value);
        }

        public FontWeight CellFontWeight
        {
            get => (FontWeight)GetValue(CellFontWeightProperty);
            set => SetValue(CellFontWeightProperty, value);
        }

        public FontFamily CellFontFamily
        {
            get => (FontFamily)GetValue(CellFontFamilyProperty);
            set => SetValue(CellFontFamilyProperty, value);
        }

        public Thickness CellPadding
        {
            get => (Thickness)GetValue(CellPaddingProperty);
            set => SetValue(CellPaddingProperty, value);
        }

        /// <summary>None, Horizontal, Vertical or Both.</summary>
        public GridLinesVisibility GridLinesVisibility
        {
            get => (GridLinesVisibility)GetValue(GridLinesVisibilityProperty);
            set => SetValue(GridLinesVisibilityProperty, value);
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

        /// <summary>Shows the status strip below the rows. On by default.</summary>
        public bool ShowFooter
        {
            get => (bool)GetValue(ShowFooterProperty);
            set => SetValue(ShowFooterProperty, value);
        }

        public double FooterHeight
        {
            get => (double)GetValue(FooterHeightProperty);
            set => SetValue(FooterHeightProperty, value);
        }

        /// <summary>Content shown on the right of the footer, e.g. aggregates.</summary>
        public object FooterContent
        {
            get => GetValue(FooterContentProperty);
            set => SetValue(FooterContentProperty, value);
        }

        /// <summary>Overrides the generated status line when set.</summary>
        public string FooterStatusText
        {
            get => (string)GetValue(FooterStatusTextProperty);
            set => SetValue(FooterStatusTextProperty, value);
        }

        /// <summary>Enables grouping. Required before headers can be dragged to the panel.</summary>
        public bool AllowGrouping
        {
            get => (bool)GetValue(AllowGroupingProperty);
            set => SetValue(AllowGroupingProperty, value);
        }

        /// <summary>Shows the panel above the headers listing the active grouping.</summary>
        public bool ShowGroupDropArea
        {
            get => (bool)GetValue(ShowGroupDropAreaProperty);
            set => SetValue(ShowGroupDropAreaProperty, value);
        }

        public double GroupDropAreaHeight
        {
            get => (double)GetValue(GroupDropAreaHeightProperty);
            set => SetValue(GroupDropAreaHeightProperty, value);
        }

        public bool ShowGroupItemCount
        {
            get => (bool)GetValue(ShowGroupItemCountProperty);
            set => SetValue(ShowGroupItemCountProperty, value);
        }

        /// <summary>Expand new groups on creation. Off leaves them collapsed.</summary>
        public bool AutoExpandGroups
        {
            get => (bool)GetValue(AutoExpandGroupsProperty);
            set => SetValue(AutoExpandGroupsProperty, value);
        }

        /// <summary>The active grouping, outermost first. Mutate to group in code.</summary>
        public GroupColumnDescriptions GroupColumnDescriptions => _groupController.Descriptions;

        public bool IsGrouped => _groupController.IsGrouped;

        public GroupController GroupController => _groupController;

        public TreeGridColumns Columns { get; }

        /// <summary>The flattened, currently-visible node projection.</summary>
        public FlatTreeView View => _dataSource.View;

        public TreeGridDataSource DataSource => _dataSource;

        // ---------------------------------------------------------------- events

        /// <summary>Raised when an unpopulated node is expanded. Set ChildItems to supply children.</summary>
        public event EventHandler<RequestTreeItemsEventArgs> RequestTreeItems
        {
            add => _dataSource.RequestTreeItems += value;
            remove => _dataSource.RequestTreeItems -= value;
        }

        public event EventHandler<NodeExpandingEventArgs> NodeExpanding;
        public event EventHandler<NodeExpandedEventArgs> NodeExpanded;
        public event EventHandler<NodeCollapsingEventArgs> NodeCollapsing;
        public event EventHandler<NodeCollapsedEventArgs> NodeCollapsed;

        public event EventHandler<GridSelectionChangedEventArgs> SelectionChanged;

        public event EventHandler<CurrentCellChangedEventArgs> CurrentCellChanged;

        public event EventHandler<NodeCheckedEventArgs> NodeChecked;

        public event EventHandler SortColumnsChanged;

        public event EventHandler<FilterChangedEventArgs> FilterChanged;

        public event EventHandler<CellBeginEditEventArgs> CellBeginEdit;

        public event EventHandler<CellEndEditEventArgs> CellEndEdit;

        public event EventHandler<CellValidatingEventArgs> CellValidating;

        public event EventHandler<RowValidatingEventArgs> RowValidating;

        public event EventHandler<RowDragStartingEventArgs> RowDragStarting;

        public event EventHandler<RowDragOverEventArgs> RowDragOver;

        public event EventHandler<RowDroppedEventArgs> RowDropped;

        public event EventHandler<CopyContentEventArgs> CopyContent;

        public event EventHandler<PasteContentEventArgs> PasteContent;

        public event EventHandler<GridContextMenuOpeningEventArgs> GridContextMenuOpening;

        /// <summary>
        /// Raised for each realised row so appearance can depend on the data.
        /// Leave a property null to keep the grid's own value.
        /// </summary>
        public event EventHandler<QueryRowStyleEventArgs> QueryRowStyle;

        /// <summary>Per-cell equivalent of <see cref="QueryRowStyle"/>.</summary>
        public event EventHandler<QueryCellStyleEventArgs> QueryCellStyle;

        /// <summary>Raised per group header so its caption can be customised.</summary>
        public event EventHandler<GroupCaptionEventArgs> QueryGroupCaption
        {
            add => _groupController.QueryGroupCaption += value;
            remove => _groupController.QueryGroupCaption -= value;
        }

        /// <summary>Raised before grouping changes. Cancel to refuse the change.</summary>
        public event EventHandler<GroupingChangingEventArgs> GroupingChanging;

        /// <summary>Raised after grouping has been applied and the view rebuilt.</summary>
        public event EventHandler<GroupingChangedEventArgs> GroupingChanged;

        /// <summary>Raised per pointer move while a header is dragged over the group panel.</summary>
        public event EventHandler<GroupDragOverEventArgs> GroupDragOver;

        /// <summary>Raised when a chip drag starts in the group panel. Cancel to pin it.</summary>
        public event EventHandler<GroupChipDragEventArgs> GroupChipDragStarting;

        // --------------------------------------------------------------- template

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _groupDropArea = GetTemplateChild(PartGroupDropArea) as GroupDropAreaControl;

            if (_groupDropArea != null)
            {
                _groupDropArea.Descriptions = _groupController.Descriptions;
                _groupDropArea.Refresh();

                _groupDropArea.AddHandler(GroupDropAreaControl.GroupRemovedEvent,
                    new RoutedEventHandler(OnGroupChipRemoved));
                _groupDropArea.AddHandler(GroupDropAreaControl.GroupSortToggledEvent,
                    new RoutedEventHandler(OnGroupChipSortToggled));
                _groupDropArea.AddHandler(GroupDropAreaControl.GroupReorderedEvent,
                    new RoutedEventHandler(OnGroupChipReordered));
                _groupDropArea.AddHandler(GroupDropAreaControl.GroupChipDragStartedEvent,
                    new RoutedEventHandler(OnGroupChipDragStarted));
            }

            _footer = GetTemplateChild(PartFooter) as TreeGridFooterControl;
            _headerHost = GetTemplateChild(PartHeaderHost) as Border;
            _scrollViewer = GetTemplateChild(PartScrollViewer) as ScrollViewer;
            _container = GetTemplateChild(PartVisualContainer) as VisualContainer;

            if (_container != null)
            {
                _container.Source = _dataSource.View;
                _container.Layout = _layout;
                _container.ScrollOffsetChanged += OnContainerScrollChanged;
                _container.SizeChanged += (s, e) => RefreshLayout();
            }

            if (_headerHost != null)
            {
                _headerRow = new TreeGridRowControl
                {
                    RowType = TreeGridRowType.Header,
                    Layout = _layout
                };

                _headerRow.HeaderIndicatorResolver = ApplyHeaderIndicators;

                // Stacked header rows sit above the column headers in a shared panel.
                _stackedHeaderPanel = new StackPanel { Orientation = Orientation.Vertical };
                var headerStack = new DockPanel { LastChildFill = true };
                DockPanel.SetDock(_stackedHeaderPanel, Dock.Top);
                headerStack.Children.Add(_stackedHeaderPanel);
                headerStack.Children.Add(_headerRow);

                _headerHost.Child = headerStack;
                RebuildStackedHeaders();
                _headerHost.SizeChanged += (s, e) => RefreshLayout();
            }

            _templateApplied = true;
            ApplyVisualConfig();
            RefreshLayout();
        }

        // -------------------------------------------------------- change handlers

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = (TreeGridControl)d;
            grid.ConfigureBindingMode();
            grid.GenerateColumnsIfNeeded(e.NewValue as IEnumerable);
            grid._dataSource.SetItemsSource(e.NewValue as IEnumerable);
        }

        private static void OnBindingConfigChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = (TreeGridControl)d;
            grid.ConfigureBindingMode();
            grid._dataSource.Reload();
        }

        private static void OnVisualConfigChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = (TreeGridControl)d;
            grid.ApplyVisualConfig();
            grid.RefreshLayout();
        }

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = (TreeGridControl)d;

            if (grid._syncingSelectedItem)
                return;

            var node = grid.ResolveNode(e.NewValue);

            if (node == null)
                grid._selection.Clear();
            else
            {
                grid._selection.Select(node);
                grid.BringNodeIntoView(node);
            }

            grid._container?.RefreshRowStates();
        }

        private static void OnValidationModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = (TreeGridControl)d;
            grid._editController.ValidationMode = grid.ValidationMode;
        }

        private static void OnMergeConfigChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = (TreeGridControl)d;

            grid._mergeController.IsEnabled = grid.AllowMergeCells;
            grid._mergeController.MergeOnlyWithinSiblings = grid.MergeOnlyWithinSiblings;
            grid._mergeController.Invalidate();

            // Attach or detach the resolver so toggling this at runtime works both ways.
            grid.ApplyVisualConfig();

            grid._container?.ResetRows();
            grid._container?.InvalidateMeasure();
        }

        private MergeRenderInfo ResolveMerge(int rowIndex, int columnIndex, TreeGridColumn column) =>
            _mergeController.Resolve(_dataSource.View, rowIndex, columnIndex, column,
                _container?.FirstVisibleRow ?? 0);

        private string ResolveCellError(TreeNode node, TreeGridColumn column) =>
            _editController.GetError(node, column?.MappingName);

        private static void OnIndicatorConfigChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            ((TreeGridControl)d)._headerRow?.RefreshHeaderIndicators();

        private static void OnFilterModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = (TreeGridControl)d;
            grid._filterController.NodeMode = grid.FilterNodeMode;
            grid.ApplyFilters();
        }

        private void ApplyHeaderIndicators(TreeGridHeaderCell header)
        {
            var column = header.Column;
            if (column == null)
                return;

            var direction = _sortController.GetDirection(column.MappingName);

            var glyph = direction == null
                ? ListSortDirectionOrNone.None
                : direction == ListSortDirection.Ascending
                    ? ListSortDirectionOrNone.Ascending
                    : ListSortDirectionOrNone.Descending;

            header.RefreshIndicators(
                glyph,
                _sortController.GetSortNumber(column.MappingName),
                ShowSortNumbers && _sortController.SortDescriptions.Count > 1,
                AllowFiltering && column.AllowFiltering,
                _filterController.IsFiltered(column.MappingName));
        }

        private static void OnSelectionConfigChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = (TreeGridControl)d;
            grid._selection.Mode = grid.SelectionMode;
            grid._selection.Unit = grid.SelectionUnit;
            grid._checkState.CascadeMode = grid.CheckBoxCascadeMode;
            grid.ApplyVisualConfig();
            grid._container?.RefreshRowStates();
        }

        /// <summary>
        /// Builds the appearance actually used for a pass: the explicit property when
        /// set, otherwise the matching theme resource. Resolving once here means rows
        /// and cells never have to handle a null and guess a fallback.
        /// </summary>
        public TreeGridVisualStyle ResolveVisualStyle() => new TreeGridVisualStyle
        {
            HeaderBackground = HeaderBackground ?? ThemeBrush("TreeGrid.HeaderBackground"),
            HeaderForeground = HeaderForeground ?? ThemeBrush("TreeGrid.HeaderForeground"),
            HeaderBorderBrush = HeaderBorderBrush ?? ThemeBrush("TreeGrid.BorderBrush"),
            HeaderFontFamily = HeaderFontFamily,
            HeaderFontSize = HeaderFontSize,
            HeaderFontWeight = HeaderFontWeight,

            RowBackground = RowBackground,
            AlternatingRowBackground = AlternatingRowBackground ?? ThemeBrush("TreeGrid.AlternatingRowBackground"),
            SelectedRowBackground = SelectedRowBackground ?? ThemeBrush("TreeGrid.SelectedRowBackground"),
            SelectedRowForeground = SelectedRowForeground,
            HoverRowBackground = HoverRowBackground,
            ShowAlternatingRows = ShowAlternatingRows,

            CellForeground = CellForeground ?? ThemeBrush("TreeGrid.CellForeground"),
            CellFontFamily = CellFontFamily,
            CellFontSize = CellFontSize,
            CellFontWeight = CellFontWeight,
            CellPadding = CellPadding,

            GridLineBrush = GridLineBrush ?? ThemeBrush("TreeGrid.GridLineBrush"),
            GridLinesVisibility = GridLinesVisibility,
            CurrentCellBorderBrush = CurrentCellBorderBrush ?? ThemeBrush("TreeGrid.CurrentCellBorder"),
            ErrorBrush = ErrorBrush ?? ThemeBrush("TreeGrid.ErrorBrush"),
            EditorBackground = EditorBackground ?? ThemeBrush("TreeGrid.EditorBackground"),
            ExpanderGlyphBrush = ExpanderGlyphBrush ?? ThemeBrush("TreeGrid.ExpanderGlyph"),
            FrozenLineBrush = FrozenLineBrush ?? ThemeBrush("TreeGrid.FrozenLine"),
            MergedCellBackground = MergedCellBackground ?? Background
        };

        private Brush ThemeBrush(string key) => TryFindResource(key) as Brush;

        private QueryRowStyleEventArgs ResolveRowStyle(TreeNode node, int rowIndex)
        {
            var args = new QueryRowStyleEventArgs(node, rowIndex);
            QueryRowStyle?.Invoke(this, args);
            return args;
        }

        private QueryCellStyleEventArgs ResolveCellStyle(TreeNode node, TreeGridColumn column, int columnIndex)
        {
            var args = new QueryCellStyleEventArgs(node, column, node?.FlatIndex ?? -1, columnIndex);
            QueryCellStyle?.Invoke(this, args);
            return args;
        }

        /// <summary>
        /// Re-reads appearance and repaints. Call after changing styling properties in
        /// code, or after your conditional-formatting inputs change.
        /// </summary>
        public void RefreshAppearance()
        {
            ApplyVisualConfig();
            _headerRow?.RefreshCells();
            _container?.ResetRows();
            _container?.InvalidateMeasure();
        }

        /// <summary>
        /// Maps a data item to the node currently on screen.
        /// <para>
        /// Grouping represents records with fresh leaf nodes, so the data source's own
        /// map still points at the ungrouped tree. Anything that turns an item back
        /// into a node - SelectedItem, ScrollIntoView, check state - has to go through
        /// here or it will address invisible nodes.
        /// </para>
        /// </summary>
        private TreeNode ResolveNode(object item)
        {
            if (item == null)
                return null;

            if (_groupedNodeMap != null && _groupedNodeMap.TryGetValue(item, out var grouped))
                return grouped;

            return _dataSource.GetNode(item);
        }

        private void RebuildGroupedNodeMap(IReadOnlyList<TreeNode> roots)
        {
            if (roots == null)
            {
                _groupedNodeMap = null;
                return;
            }

            _groupedNodeMap = new Dictionary<object, TreeNode>();

            void Visit(TreeNode node)
            {
                if (!node.IsGroupHeader && node.Item != null)
                    _groupedNodeMap[node.Item] = node;

                for (var i = 0; i < node.ChildNodes.Count; i++)
                    Visit(node.ChildNodes[i]);
            }

            for (var i = 0; i < roots.Count; i++)
                Visit(roots[i]);
        }

        /// <summary>
        /// Recomputes the footer counts. Group headers are excluded from the record
        /// count, so "1,204 records" means records whether or not grouping is on.
        /// </summary>
        public void UpdateFooter()
        {
            if (_footer == null || !ShowFooter)
                return;

            var view = _dataSource.View;
            var records = 0;
            var groups = 0;

            for (var i = 0; i < view.Count; i++)
            {
                if (view[i].IsGroupHeader)
                    groups++;
                else
                    records++;
            }

            _footer.VisibleRowCount = view.Count;
            _footer.RecordCount = records;
            _footer.GroupCount = groups;
            _footer.SelectedCount = _selection.SelectedItems.Count;
            _footer.CheckedCount = _checkState.CheckedItems.Count;

            _footer.StatusText = !string.IsNullOrEmpty(FooterStatusText)
                ? FooterStatusText
                : ComposeFooterText(records, groups);
        }

        private string ComposeFooterText(int records, int groups)
        {
            var builder = new System.Text.StringBuilder();

            builder.Append(records.ToString("N0")).Append(records == 1 ? " record" : " records");

            if (groups > 0)
                builder.Append("  |  ").Append(groups.ToString("N0")).Append(groups == 1 ? " group" : " groups");

            var selected = _selection.SelectedItems.Count;
            if (selected > 0)
                builder.Append("  |  ").Append(selected.ToString("N0")).Append(" selected");

            var checkedCount = _checkState.CheckedItems.Count;
            if (checkedCount > 0)
                builder.Append("  |  ").Append(checkedCount.ToString("N0")).Append(" checked");

            if (_filterController.HasFilters)
                builder.Append("  |  filtered");

            return builder.ToString();
        }

        private int ResolveCurrentColumn(TreeNode node) =>
            ReferenceEquals(node, _selection.CurrentNode) ? _selection.CurrentColumnIndex : -1;

        private void ConfigureBindingMode()
        {
            _dataSource.ChildPropertyName = ChildPropertyName;
            _dataSource.IdPropertyName = IdPropertyName;
            _dataSource.ParentIdPropertyName = ParentIdPropertyName;
            _dataSource.SelfRelationRootValue = SelfRelationRootValue;

            if (!string.IsNullOrEmpty(IdPropertyName) && !string.IsNullOrEmpty(ParentIdPropertyName))
                _dataSource.BindingMode = TreeGridBindingMode.SelfRelational;
            else if (!string.IsNullOrEmpty(ChildPropertyName))
                _dataSource.BindingMode = TreeGridBindingMode.Hierarchical;
            else
                _dataSource.BindingMode = TreeGridBindingMode.Unbound;
        }

        private void ApplyVisualConfig()
        {
            if (!_templateApplied)
                return;

            _layout.FrozenColumnCount = FrozenColumnCount;
            _layout.FooterColumnCount = FooterColumnCount;

            if (_container != null)
            {
                _container.RowHeight = RowHeight;
                _container.IndentPerLevel = IndentPerLevel;
                _container.TreeColumnIndex = ExpanderColumnIndex;
                _container.GridLineBrush = GridLineBrush;
                _container.AlternatingRowBrush = AlternatingRowBackground;
                _container.SelectedRowBrush = SelectedRowBackground;
                _container.ShowAlternatingRows = ShowAlternatingRows;
                _container.EnableColumnVirtualization = EnableColumnVirtualization;
                _container.ShowNodeCheckBox = AllowCheckBoxSelection;
                _container.IsCellSelectionUnit = SelectionUnit == GridSelectionUnit.Cell;
                _container.CurrentColumnResolver = ResolveCurrentColumn;
                // Null when disabled: the row control skips its whole merge pass, so a
                // parked feature costs nothing per cell.
                _container.MergeResolver = AllowMergeCells ? ResolveMerge : (Func<int, int, TreeGridColumn, MergeRenderInfo>)null;
                _container.ErrorResolver = ResolveCellError;
                _container.FrozenLineBrush = FrozenLineBrush;
                _container.MergedCellBackground = MergedCellBackground ?? Background;
                _container.VisualStyle = ResolveVisualStyle();
                _container.RowStyleResolver = QueryRowStyle != null ? ResolveRowStyle : (Func<TreeNode, int, QueryRowStyleEventArgs>)null;
                _container.CellStyleResolver = QueryCellStyle != null ? ResolveCellStyle : (Func<TreeNode, TreeGridColumn, int, QueryCellStyleEventArgs>)null;
            }

            _mergeController.IsEnabled = AllowMergeCells;
            _mergeController.MergeOnlyWithinSiblings = MergeOnlyWithinSiblings;
            _editController.ValidationMode = ValidationMode;

            if (_headerHost != null)
                _headerHost.Height = HeaderRowHeight + StackedHeaderRows.Count * StackedHeaderRowHeight;

            if (_groupDropArea != null)
            {
                _groupDropArea.Height = GroupDropAreaHeight;
                _groupDropArea.Visibility = ShowGroupDropArea ? Visibility.Visible : Visibility.Collapsed;
            }

            if (_footer != null)
            {
                _footer.Height = FooterHeight;
                _footer.Visibility = ShowFooter ? Visibility.Visible : Visibility.Collapsed;
                _footer.CustomContent = FooterContent;
                _footer.SeparatorBrush = GridLineBrush ?? ThemeBrush("TreeGrid.BorderBrush");
            }

            UpdateFooter();

            if (_headerRow != null)
            {
                _headerRow.EnableColumnVirtualization = EnableColumnVirtualization;
                _headerRow.VisualStyle = _container?.VisualStyle ?? ResolveVisualStyle();
            }
        }

        private void OnColumnsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (TreeGridColumn column in e.OldItems)
                    column.Host = null;
            }

            if (e.NewItems != null)
            {
                foreach (TreeGridColumn column in e.NewItems)
                    column.Host = this;
            }

            RefreshLayout();
        }

        /// <summary>
        /// A column changed something that affects layout - width, visibility, sizer.
        /// Hiding a column previously did nothing until an unrelated action forced a
        /// refresh, because a column has no place in the visual tree to notify from.
        /// </summary>
        void ITreeGridColumnHost.OnColumnLayoutChanged(TreeGridColumn column)
        {
            // Auto-fit and resize assign Width themselves; without this guard each
            // assignment would re-enter the layout pass that produced it.
            if (!_templateApplied || _suppressColumnNotifications)
                return;

            // A hidden column cannot keep the current cell or an open editor.
            if (column.IsHidden)
            {
                if (_editController.IsEditing && ReferenceEquals(_editController.EditingColumn, column))
                    CancelEdit();

                if (_layout.IndexOf(column) == _selection.CurrentColumnIndex)
                    _selection.SetCurrent(_selection.CurrentNode, 0);
            }

            _mergeController.Invalidate();
            RefreshLayout();
            _container?.ResetRows();
        }

        private void OnFlatViewChanged(object sender, FlatTreeViewChangedEventArgs e)
        {
            // Merge ranges are computed over flat indices, so any splice invalidates them.
            _mergeController.Invalidate();

            if (e.IsReset)
                _container?.ResetRows();

            _container?.InvalidateMeasure();
            UpdateFooter();
        }

        private void OnSourceReset(object sender, EventArgs e)
        {
            // Nodes are new objects after a rebuild, so both controllers remap by item.
            // The cached ungrouped tree belongs to the previous source.
            _ungroupedRoots = null;
            _groupedNodeMap = null;

            _selection.Restore(ResolveNode);
            _checkState.Restore(ResolveNode);

            // A new node tree arrives unsorted and unfiltered; reapply both.
            if (_filterController.HasFilters)
                _filterController.Apply(_dataSource.View.RootNodes);

            if (_sortController.HasSort)
                _dataSource.View.SortHierarchy(_sortController.BuildComparison());
            else if (_filterController.HasFilters)
                _dataSource.View.Rebuild();

            if (_groupController.IsGrouped)
                ApplyGrouping();

            _container?.ResetRows();
            RefreshLayout();
        }

        private void OnContainerScrollChanged(object sender, EventArgs e)
        {
            if (_headerRow == null || _container == null)
                return;

            _headerRow.HorizontalOffset = _container.HorizontalOffset;
            _headerRow.RefreshCells();

            foreach (var stacked in _stackedRows)
            {
                stacked.HorizontalOffset = _container.HorizontalOffset;
                stacked.InvalidateArrange();
            }
        }

        // ---------------------------------------------------------------- layout

        /// <summary>Recomputes column geometry and repaints the header and rows.</summary>
        public void RefreshLayout() => RefreshLayoutCore(scheduleAutoFit: true);

        private void RefreshLayoutCore(bool scheduleAutoFit)
        {
            if (!_templateApplied)
                return;

            var viewportWidth = _container?.ViewportWidth ?? 0;
            if (viewportWidth <= 0)
                viewportWidth = _headerHost?.ActualWidth ?? ActualWidth;

            _layout.FrozenColumnCount = FrozenColumnCount;
            _layout.FooterColumnCount = FooterColumnCount;
            _layout.Recalculate(Columns, viewportWidth);

            if (_headerRow != null)
            {
                _headerRow.ViewportWidth = viewportWidth;
                _headerRow.HorizontalOffset = _container?.HorizontalOffset ?? 0;
                _headerRow.GridLineBrush = GridLineBrush;
                _headerRow.RefreshCells();
            }

            foreach (var stacked in _stackedRows)
            {
                stacked.ViewportWidth = viewportWidth;
                stacked.HorizontalOffset = _container?.HorizontalOffset ?? 0;
                stacked.VisualStyle = _container?.VisualStyle;
                stacked.Refresh();
            }

            _container?.InvalidateMeasure();
            _container?.RefreshRowContent();

            if (scheduleAutoFit && HasAutoFitColumns())
                ScheduleAutoFitPass();
        }

        /// <summary>
        /// Position of a column among the currently visible columns, or -1 when it is
        /// hidden or does not belong to this grid.
        /// <para>
        /// This is the visible index, not the index in <see cref="Columns"/>. That is
        /// deliberate: frozen counts, cell coordinates and hit-testing all work in
        /// visible space, so a hidden column must not consume a position.
        /// </para>
        /// </summary>
        public int GetColumnIndex(TreeGridColumn column) => _layout.IndexOf(column);

        /// <summary>Visible column at an index, or null when out of range.</summary>
        public TreeGridColumn GetColumnAt(int visibleIndex) => _layout.ColumnAt(visibleIndex);

        /// <summary>Columns currently taking part in layout, in display order.</summary>
        public IReadOnlyList<TreeGridColumn> VisibleColumns => _layout.VisibleColumns;

        private bool HasAutoFitColumns()
        {
            foreach (var column in Columns)
            {
                if (column.ColumnSizer != ColumnSizerMode.None && column.ColumnSizer != ColumnSizerMode.Star)
                    return true;
            }

            return false;
        }

        private void GenerateColumnsIfNeeded(IEnumerable source)
        {
            if (!AutoGenerateColumns || Columns.Count > 0 || source == null)
                return;

            var first = source.Cast<object>().FirstOrDefault();
            if (first == null)
                return;

            foreach (var property in first.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    continue;

                // Skip the child collection and the parent link - they are plumbing.
                if (property.Name == ChildPropertyName || property.Name == ParentIdPropertyName)
                    continue;

                if (typeof(IEnumerable).IsAssignableFrom(property.PropertyType) && property.PropertyType != typeof(string))
                    continue;

                Columns.Add(new TreeGridTextColumn
                {
                    MappingName = property.Name,
                    HeaderText = SplitPascalCase(property.Name)
                });
            }
        }

        private static string SplitPascalCase(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            var builder = new System.Text.StringBuilder(value.Length + 4);

            for (var i = 0; i < value.Length; i++)
            {
                if (i > 0 && char.IsUpper(value[i]) && !char.IsUpper(value[i - 1]))
                    builder.Append(' ');
                builder.Append(value[i]);
            }

            return builder.ToString();
        }

        // ------------------------------------------------------ expand / collapse

        private async void OnExpanderToggle(object sender, RoutedEventArgs e)
        {
            if (e is TreeNodeRoutedEventArgs args && args.Node != null)
                await ToggleNodeAsync(args.Node);
        }

        public async System.Threading.Tasks.Task ToggleNodeAsync(TreeNode node)
        {
            if (node == null)
                return;

            if (node.IsExpanded)
            {
                var collapsing = new NodeCollapsingEventArgs(node);
                NodeCollapsing?.Invoke(this, collapsing);
                if (collapsing.Cancel)
                    return;

                _dataSource.View.Collapse(node);
                NodeCollapsed?.Invoke(this, new NodeCollapsedEventArgs(node));
                return;
            }

            var expanding = new NodeExpandingEventArgs(node);
            NodeExpanding?.Invoke(this, expanding);
            if (expanding.Cancel)
                return;

            _dataSource.View.Expand(node);
            RefreshRow(node);

            if (_dataSource.NeedsLoad(node))
            {
                RefreshRow(node);
                await _dataSource.LoadChildrenAsync(node);

                if (AllowCheckBoxSelection)
                    _checkState.ApplyInheritedState(node);

                // Children arriving from a lazy source have not been through either pass.
                if (_sortController.HasSort)
                    _dataSource.View.SortSubtree(node, _sortController.BuildComparison());

                if (_filterController.HasFilters)
                    _filterController.ApplyToSubtree(node);

                if (_sortController.HasSort || _filterController.HasFilters)
                    _dataSource.View.RefreshChildren(node);

                RefreshRow(node);
            }

            NodeExpanded?.Invoke(this, new NodeExpandedEventArgs(node));
        }

        public void ExpandAll(int maxLevel = int.MaxValue) => _dataSource.View.ExpandAll(maxLevel);

        public void CollapseAll() => _dataSource.View.CollapseAll();

        private void RefreshRow(TreeNode node)
        {
            if (node == null || _container == null || node.FlatIndex < 0)
                return;

            var row = _container.GetRealizedRow(node.FlatIndex);
            row?.RefreshCells();
        }

        private void BringNodeIntoView(TreeNode node)
        {
            if (node == null || _container == null)
                return;

            _dataSource.View.EnsureVisible(node);
            _container.ScrollIntoView(node.FlatIndex);
        }

        public void ScrollIntoView(object item)
        {
            var node = ResolveNode(item);
            BringNodeIntoView(node);
        }

        // ------------------------------------------------------------- selection

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            if (_container == null)
                return;

            Focus();

            var point = e.GetPosition(_container);
            var rowIndex = _container.RowIndexFromPoint(point);

            if (rowIndex < 0 || rowIndex >= _dataSource.View.Count)
                return;

            var columnIndex = _container.ColumnIndexFromPoint(point);
            if (columnIndex < 0)
                columnIndex = _selection.CurrentColumnIndex;

            var node = _dataSource.View[rowIndex];
            var column = _layout.ColumnAt(columnIndex);

            // Clicking away from an open editor commits it. A rejected commit keeps
            // focus in the editor rather than letting the user navigate away from a
            // value the model refused.
            if (_editController.IsEditing &&
                (!ReferenceEquals(node, _editController.EditingNode) ||
                 !ReferenceEquals(column, _editController.EditingColumn)))
            {
                if (!EndEdit(commit: true))
                    return;
            }

            _selection.HandlePointerDown(rowIndex, columnIndex, Keyboard.Modifiers);
            _container.RefreshRowStates();

            // Group headers cannot be dragged; their position is derived from grouping.
            if (AllowRowDragDrop && !node.IsGroupHeader)
            {
                _dragPending = true;
                _dragOrigin = point;
            }

            if (AllowEditing && EditTrigger == EditTrigger.OnTap)
                BeginEdit(node, column);
        }

        protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
        {
            base.OnMouseDoubleClick(e);

            if (!AllowEditing || EditTrigger != EditTrigger.OnDoubleTap || _container == null)
                return;

            var point = e.GetPosition(_container);
            var rowIndex = _container.RowIndexFromPoint(point);

            if (rowIndex < 0 || rowIndex >= _dataSource.View.Count)
                return;

            var columnIndex = _container.ColumnIndexFromPoint(point);
            if (columnIndex < 0)
                return;

            _dragPending = false;
            BeginEdit(_dataSource.View[rowIndex], _layout.ColumnAt(columnIndex));
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            // Header gestures take precedence and run even without a container hit.
            if (UpdateHeaderGesture(e))
                return;

            if (_container == null || e.LeftButton != MouseButtonState.Pressed)
                return;

            var point = e.GetPosition(_container);

            if (_dragController.IsDragging)
            {
                UpdateRowDrag(point);
                return;
            }

            if (!_dragPending || _editController.IsEditing)
                return;

            var delta = point - _dragOrigin;

            if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            _dragPending = false;
            BeginRowDrag(_dragOrigin);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);

            _dragPending = false;

            if (FinishHeaderGesture())
            {
                e.Handled = true;
                return;
            }

            if (_dragController.IsDragging)
            {
                CompleteRowDrag();
                e.Handled = true;
            }
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);

            if (_headerDragColumn != null)
                CancelHeaderGesture();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Handled)
                return;

            // An open editor owns the keyboard except for the keys that close it.
            if (_editController.IsEditing)
            {
                switch (e.Key)
                {
                    case Key.Escape:
                        CancelEdit();
                        e.Handled = true;
                        return;

                    case Key.Enter:
                        if (EndEdit(commit: true))
                            _selection.HandleKey(Key.Down, ModifierKeys.None);

                        e.Handled = true;
                        return;

                    case Key.Tab:
                        if (EndEdit(commit: true))
                            MoveEditToNextCell((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift);

                        e.Handled = true;
                        return;

                    default:
                        return;
                }
            }

            if (_dragController.IsDragging && e.Key == Key.Escape)
            {
                CancelRowDrag();
                e.Handled = true;
                return;
            }

            if (AllowEditing && e.Key == Key.F2)
            {
                BeginEdit();
                e.Handled = true;
                return;
            }

            var ctrlDown = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;

            if (ctrlDown)
            {
                switch (e.Key)
                {
                    case Key.C:
                        if (Copy()) e.Handled = true;
                        return;

                    case Key.X:
                        if (Cut()) e.Handled = true;
                        return;

                    case Key.V:
                        if (Paste() > 0) e.Handled = true;
                        return;
                }
            }

            // Page navigation needs the viewport, which only the container knows.
            if (e.Key == Key.PageDown || e.Key == Key.PageUp)
            {
                var rows = Math.Max(1, (int)(_container?.ViewportHeight / Math.Max(1, RowHeight) ?? 1));
                _selection.PageMove(e.Key == Key.PageDown ? rows : -rows, Keyboard.Modifiers);
                _container?.RefreshRowStates();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Space)
            {
                if (AllowCheckBoxSelection && _selection.CurrentNode != null)
                {
                    ToggleNodeCheck(_selection.CurrentNode);
                    e.Handled = true;
                }

                return;
            }

            if (_selection.HandleKey(e.Key, Keyboard.Modifiers))
            {
                _container?.RefreshRowStates();
                e.Handled = true;
                return;
            }

            // Left/Right fall through to expand/collapse in row-selection mode.
            var node = _selection.CurrentNode;
            if (node == null)
                return;

            switch (e.Key)
            {
                case Key.Right:
                    if (node.HasChildNodes && !node.IsExpanded)
                    {
                        _ = ToggleNodeAsync(node);
                        e.Handled = true;
                    }
                    break;

                case Key.Left:
                    if (node.IsExpanded)
                        _dataSource.View.Collapse(node);
                    else if (node.ParentNode != null)
                        _selection.Select(node.ParentNode);

                    _container?.RefreshRowStates();
                    e.Handled = true;
                    break;
            }
        }

        protected override void OnTextInput(TextCompositionEventArgs e)
        {
            base.OnTextInput(e);

            if (!AllowEditing || EditTrigger != EditTrigger.OnKeyPress || _editController.IsEditing)
                return;

            if (string.IsNullOrEmpty(e.Text) || char.IsControl(e.Text[0]))
                return;

            var column = _layout.ColumnAt(_selection.CurrentColumnIndex);

            if (BeginEdit(_selection.CurrentNode, column) &&
                _editController.EditElement is System.Windows.Controls.TextBox box)
            {
                // Seed the editor with the keystroke that opened it.
                box.Text = e.Text;
                box.CaretIndex = box.Text.Length;
                e.Handled = true;
            }
        }

        /// <summary>Moves the editor to the next editable cell, wrapping across rows.</summary>
        private void MoveEditToNextCell(bool backwards)
        {
            var node = _selection.CurrentNode;
            if (node == null)
                return;

            var count = _layout.VisibleColumns.Count;
            var index = _selection.CurrentColumnIndex;

            for (var step = 0; step < count; step++)
            {
                index += backwards ? -1 : 1;

                if (index < 0 || index >= count)
                {
                    var rowDelta = backwards ? -1 : 1;
                    var nextRow = node.FlatIndex + rowDelta;

                    if (nextRow < 0 || nextRow >= _dataSource.View.Count)
                        return;

                    node = _dataSource.View[nextRow];
                    index = backwards ? count - 1 : 0;
                }

                var candidate = _layout.ColumnAt(index);

                if (candidate != null && candidate.AllowEditing)
                {
                    _selection.Select(node);
                    BeginEdit(node, candidate);
                    return;
                }
            }
        }

        public void SelectAll() => _selection.SelectAll();

        public void ClearSelection()
        {
            _selection.Clear();
            _container?.RefreshRowStates();
        }

        private void OnSelectionControllerChanged(object sender, GridSelectionChangedEventArgs e)
        {
            _syncingSelectedItem = true;
            try
            {
                SelectedItem = _selection.SelectedItem;
            }
            finally
            {
                _syncingSelectedItem = false;
            }

            _container?.RefreshRowStates();
            UpdateFooter();
            SelectionChanged?.Invoke(this, e);
        }

        private void OnCurrentCellChanged(object sender, CurrentCellChangedEventArgs e)
        {
            if (e.NewIndex.IsValid)
                _container?.ScrollIntoView(e.NewIndex.RowIndex);

            _container?.RefreshRowStates();
            CurrentCellChanged?.Invoke(this, e);
        }

        // -------------------------------------------------------------- checkbox

        private void OnNodeCheckToggle(object sender, RoutedEventArgs e)
        {
            if (e is TreeNodeRoutedEventArgs args && args.Node != null)
            {
                e.Handled = true;
                ToggleNodeCheck(args.Node);
            }
        }

        public void ToggleNodeCheck(TreeNode node)
        {
            _checkState.Toggle(node);

            // A cascade can touch nodes far outside the viewport, so repaint whatever
            // is currently realised rather than trying to track individual rows.
            _container?.RefreshRowStates();
        }

        public void SetNodeCheckState(object item, bool? state)
        {
            var node = ResolveNode(item);
            if (node == null)
                return;

            _checkState.SetState(node, state);
            _container?.RefreshRowStates();
        }

        private void OnNodeCheckedInternal(object sender, NodeCheckedEventArgs e)
        {
            UpdateFooter();
            NodeChecked?.Invoke(this, e);
        }

        /// <summary>Toggles a bound boolean rendered by a TreeGridCheckBoxColumn.</summary>
        private void OnCellValueToggle(object sender, RoutedEventArgs e)
        {
            if (!(e is TreeNodeRoutedEventArgs args) || args.Node == null)
                return;

            e.Handled = true;

            if (!(e.OriginalSource is TreeGridCell cell) || cell.Column == null)
                return;

            if (!cell.Column.AllowEditing)
                return;

            var current = PropertyAccessor.GetValue(args.Node.Item, cell.Column.MappingName) as bool?;
            PropertyAccessor.SetValue(args.Node.Item, cell.Column.MappingName, !(current ?? false));

            _container?.RefreshRowContent();
        }

        // ------------------------------------------------------- column resizing

        private void OnColumnResize(object sender, RoutedEventArgs e)
        {
            if (!(e is ColumnResizeEventArgs args) || args.Column == null)
                return;

            e.Handled = true;

            if (!AllowColumnResizing || !args.Column.AllowResizing)
                return;

            if (args.IsCompleted)
            {
                RefreshLayout();
                return;
            }

            _suppressColumnNotifications = true;
            try
            {
                // An explicit drag wins over any auto-fit mode the column was using.
                args.Column.ColumnSizer = ColumnSizerMode.None;

                var target = args.Column.ActualWidth + args.Delta;
                args.Column.Width = Math.Min(Math.Max(target, args.Column.MinimumWidth), args.Column.MaximumWidth);
            }
            finally
            {
                _suppressColumnNotifications = false;
            }

            RefreshLayout();
        }

        private void OnColumnAutoFit(object sender, RoutedEventArgs e)
        {
            if (!(e is ColumnAutoFitEventArgs args) || args.Column == null)
                return;

            e.Handled = true;
            AutoFitColumn(args.Column);
        }

        /// <summary>Sizes a column to the wider of its header and its realised cells.</summary>
        public void AutoFitColumn(TreeGridColumn column)
        {
            if (column == null)
                return;

            _layout.AutoFitWidths.Remove(column);
            column.ColumnSizer = ColumnSizerMode.AllCells;

            RefreshLayout();
            ScheduleAutoFitPass();
        }

        public void AutoFitColumns()
        {
            _layout.ResetAutoFitWidths();

            foreach (var column in Columns)
                column.ColumnSizer = ColumnSizerMode.AllCells;

            RefreshLayout();
            ScheduleAutoFitPass();
        }

        /// <summary>
        /// Auto-fit needs measured content, which only exists after a measure pass, so
        /// widths are applied on a follow-up tick. The guard stops a width change from
        /// scheduling another pass forever.
        /// </summary>
        private void ScheduleAutoFitPass()
        {
            if (_autoFitPassPending || !_templateApplied)
                return;

            _autoFitPassPending = true;

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                _autoFitPassPending = false;

                bool changed;

                _suppressColumnNotifications = true;
                try
                {
                    changed = _layout.ApplyAutoFitWidths();
                }
                finally
                {
                    _suppressColumnNotifications = false;
                }

                if (changed)
                    RefreshLayoutCore(scheduleAutoFit: false);
            }));
        }

        // ------------------------------------------------------ column reordering

        /// <summary>
        /// Takes ownership of the header gesture. Capture is held by the grid, which is
        /// never recycled, so a relayout mid-drag can no longer cancel it.
        /// </summary>
        private void OnHeaderPointerDown(object sender, RoutedEventArgs e)
        {
            if (!(e is HeaderPointerEventArgs args) || args.Column == null)
                return;

            e.Handled = true;

            if (!AllowColumnReordering && !AllowGrouping && !AllowSorting)
                return;

            _headerDragColumn = args.Column;
            _headerDragOrigin = args.ScreenPoint;
            _headerDragging = false;

            CaptureMouse();
        }

        /// <summary>Returns true when the move was consumed by a header gesture.</summary>
        private bool UpdateHeaderGesture(MouseEventArgs e)
        {
            if (_headerDragColumn == null)
                return false;

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                CancelHeaderGesture();
                return false;
            }

            var screenPoint = PointToScreen(e.GetPosition(this));

            if (!_headerDragging)
            {
                var dx = Math.Abs(screenPoint.X - _headerDragOrigin.X);
                var dy = Math.Abs(screenPoint.Y - _headerDragOrigin.Y);

                // Both axes: the group panel sits above the header, so dropping a
                // column there is a mostly vertical movement.
                if (dx < SystemParameters.MinimumHorizontalDragDistance &&
                    dy < SystemParameters.MinimumVerticalDragDistance)
                    return true;

                _headerDragging = true;
                BeginColumnDrag(_headerDragColumn);
            }

            UpdateColumnDrag(screenPoint);
            return true;
        }

        /// <summary>Returns true when the release was consumed by a header gesture.</summary>
        private bool FinishHeaderGesture()
        {
            if (_headerDragColumn == null)
                return false;

            var column = _headerDragColumn;
            var wasDragging = _headerDragging;

            _headerDragColumn = null;
            _headerDragging = false;

            if (IsMouseCaptured)
                ReleaseMouseCapture();

            if (wasDragging)
                CompleteColumnDrag();
            else
                HandleHeaderClick(column);

            return true;
        }

        private void CancelHeaderGesture()
        {
            _headerDragColumn = null;
            _headerDragging = false;

            if (IsMouseCaptured)
                ReleaseMouseCapture();

            EndColumnDrag();
        }

        private void BeginColumnDrag(TreeGridColumn column)
        {
            _draggedColumn = column;
            _dropIndex = -1;

            var adornerLayer = AdornerLayer.GetAdornerLayer(_headerHost);
            if (adornerLayer == null)
                return;

            _reorderAdorner = new ColumnReorderAdorner(_headerHost, DropIndicatorBrush);
            adornerLayer.Add(_reorderAdorner);
        }

        private void UpdateColumnDrag(Point screenPoint)
        {
            if (_headerHost == null)
                return;

            // Dropping on the group panel groups by the column instead of moving it.
            if (IsOverGroupArea(screenPoint, out var areaPoint))
            {
                _groupDropIndex = _groupDropArea.GetInsertIndex(areaPoint);

                var over = new GroupDragOverEventArgs(_draggedColumn, _groupDropIndex);
                GroupDragOver?.Invoke(this, over);

                var allowed = over.IsAllowed && (_draggedColumn?.AllowGrouping ?? false);

                _dropIntoGroupArea = allowed;
                _groupDropArea.IsDropTarget = allowed;

                if (_reorderAdorner != null)
                    _reorderAdorner.IndicatorX = -1;

                return;
            }

            if (_dropIntoGroupArea)
            {
                _dropIntoGroupArea = false;

                if (_groupDropArea != null)
                    _groupDropArea.IsDropTarget = false;
            }

            if (_reorderAdorner == null)
                return;

            var local = _headerHost.PointFromScreen(screenPoint);
            var index = _layout.ColumnIndexFromX(local.X, _container?.HorizontalOffset ?? 0, _headerHost.ActualWidth);

            if (index < 0)
                index = _layout.VisibleColumns.Count - 1;

            var target = _layout.ColumnAt(index);
            if (target == null)
                return;

            // Drop before or after the hovered column depending on which half we are on.
            var columnLeft = ResolveHeaderX(index);
            var dropAfter = local.X > columnLeft + target.ActualWidth / 2;

            _dropIndex = dropAfter ? index + 1 : index;
            _reorderAdorner.IndicatorX = dropAfter ? columnLeft + target.ActualWidth : columnLeft;
        }

        /// <summary>Header-space X of a column, accounting for the frozen bands.</summary>
        private double ResolveHeaderX(int index)
        {
            var column = _layout.ColumnAt(index);
            if (column == null)
                return 0;

            if (index < _layout.FrozenColumnCount)
                return column.LeftOffset;

            var footerStart = _layout.VisibleColumns.Count - _layout.FooterColumnCount;
            if (index >= footerStart)
            {
                var offsetInFooter = column.LeftOffset - (_layout.TotalWidth - _layout.FooterWidth);
                return (_headerHost?.ActualWidth ?? 0) - _layout.FooterWidth + offsetInFooter;
            }

            return column.LeftOffset - (_container?.HorizontalOffset ?? 0);
        }

        private void CompleteColumnDrag()
        {
            if (_dropIntoGroupArea && _draggedColumn != null)
            {
                var column = _draggedColumn;
                var index = _groupDropIndex >= 0 ? _groupDropIndex : _groupController.Descriptions.Count;

                EndColumnDrag();

                if (column.AllowGrouping)
                    GroupByColumn(column.MappingName, index);

                return;
            }

            if (_draggedColumn != null && _dropIndex >= 0 && AllowColumnReordering)
            {
                var from = Columns.IndexOf(_draggedColumn);

                if (from >= 0)
                {
                    var to = _dropIndex;

                    // Removing the source first shifts every later index down by one.
                    if (to > from)
                        to--;

                    to = Math.Min(Math.Max(0, to), Columns.Count - 1);

                    if (to != from)
                        Columns.Move(from, to);
                }
            }

            EndColumnDrag();
        }

        private void EndColumnDrag()
        {
            _dropIntoGroupArea = false;
            _groupDropIndex = -1;

            if (_groupDropArea != null)
                _groupDropArea.IsDropTarget = false;

            if (_reorderAdorner != null && _headerHost != null)
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(_headerHost);
                adornerLayer?.Remove(_reorderAdorner);
            }

            _reorderAdorner = null;
            _draggedColumn = null;
            _dropIndex = -1;
        }

        // ---------------------------------------------------------------- sorting

        /// <summary>A press with no drag sorts the column.</summary>
        private void HandleHeaderClick(TreeGridColumn column)
        {
            if (column == null)
                return;

            if (!AllowSorting || !column.AllowSorting || string.IsNullOrEmpty(column.MappingName))
                return;

            var addToExisting = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            _sortController.ToggleColumn(column.MappingName, AllowMultiSort, addToExisting);

            // The collection-changed hook runs ApplySorting; if the toggle produced no
            // structural change (single-sort replacing itself) force it here.
            ApplySorting();
        }

        /// <summary>Sorts a column programmatically, replacing any existing sort.</summary>
        public void SortColumn(string mappingName, ListSortDirection direction, bool addToExisting = false)
        {
            if (string.IsNullOrEmpty(mappingName))
                return;

            if (!addToExisting)
                _sortController.SortDescriptions.Clear();

            var existing = _sortController.SortDescriptions.Find(mappingName);

            if (existing != null)
                existing.SortDirection = direction;
            else
            {
                _sortController.SortDescriptions.Add(new SortColumnDescription
                {
                    ColumnName = mappingName,
                    SortDirection = direction
                });
            }

            ApplySorting();
        }

        public void ClearSorting()
        {
            _sortController.Clear();
            ApplySorting();
        }

        /// <summary>Re-sorts the hierarchy and repaints. Safe to call when unsorted.</summary>
        public void ApplySorting()
        {
            if (_dataSource.View.RootNodes.Count == 0)
                return;

            _dataSource.View.SortHierarchy(_sortController.BuildComparison());

            _headerRow?.RefreshHeaderIndicators();
            _container?.ResetRows();
            _container?.InvalidateMeasure();

            SortColumnsChanged?.Invoke(this, EventArgs.Empty);
        }

        // -------------------------------------------------------------- filtering

        private void OnFilterControllerChanged(object sender, FilterChangedEventArgs e)
        {
            ApplyFilters();
            FilterChanged?.Invoke(this, e);
        }

        /// <summary>Re-evaluates every filter and rebuilds the flat view.</summary>
        public void ApplyFilters()
        {
            if (_dataSource.View.RootNodes.Count == 0)
                return;

            _filterController.NodeMode = FilterNodeMode;
            _filterController.Apply(_dataSource.View.RootNodes);

            if (ExpandNodesOnFiltering && _filterController.HasFilters)
                ExpandSurvivingNodes();

            _dataSource.View.Rebuild();

            _headerRow?.RefreshHeaderIndicators();
            _container?.ResetRows();
            _container?.InvalidateMeasure();
        }

        /// <summary>
        /// After filtering, a match buried under collapsed ancestors would be invisible,
        /// which reads as "the filter found nothing". Open every node that survived.
        /// </summary>
        private void ExpandSurvivingNodes()
        {
            void Visit(TreeNode node)
            {
                if (node.IsFilteredOut)
                    return;

                var hasVisibleChild = false;

                for (var i = 0; i < node.ChildNodes.Count; i++)
                {
                    var child = node.ChildNodes[i];
                    if (child.IsFilteredOut)
                        continue;

                    hasVisibleChild = true;
                    Visit(child);
                }

                if (hasVisibleChild)
                    node.IsExpanded = true;
            }

            foreach (var root in _dataSource.View.RootNodes)
                Visit(root);
        }

        public void ClearFilters()
        {
            _filterController.ClearAll();
            ApplyFilters();
        }

        public void ClearFilter(string mappingName) => _filterController.ClearFilter(mappingName);

        // ----------------------------------------------------------- filter popup

        private void OnFilterButtonClick(object sender, RoutedEventArgs e)
        {
            if (!(e is ColumnRoutedEventArgs args) || args.Column == null)
                return;

            e.Handled = true;
            ShowFilterPopup(args.Column, e.OriginalSource as TreeGridHeaderCell);
        }

        private void ShowFilterPopup(TreeGridColumn column, TreeGridHeaderCell headerCell)
        {
            if (!AllowFiltering || !column.AllowFiltering || string.IsNullOrEmpty(column.MappingName))
                return;

            EnsureFilterPopup();

            // Distinct values cannot be enumerated for a genuinely unbounded lazy
            // source, so the popup drops to condition-only filtering there.
            var canUseValueList = _dataSource.BindingMode != TreeGridBindingMode.Unbound;

            var elements = canUseValueList
                ? _filterController.GetDistinctValues(_dataSource.View.RootNodes, column.MappingName, column.FormatValue)
                : new List<FilterElement>();

            _filterPopupContent.UseStronglyTypedConditions = true;
            _filterPopupContent.IsAdvancedExpanded = !canUseValueList;
            _filterPopupContent.Initialize(
                column.MappingName,
                column.ResolvedHeaderText,
                elements,
                _filterController.GetFilter(column.MappingName),
                canUseValueList);

            // Anchor to the header cell and right-align the popup under it. The event
            // is raised by the header cell, so using the raw OriginalSource anchored
            // the popup to the column's left edge instead.
            var anchor = (UIElement)headerCell ?? this;

            _filterPopup.PlacementTarget = anchor;
            _filterPopup.Placement = PlacementMode.Bottom;
            _filterPopup.HorizontalOffset = ResolveFilterPopupOffset(headerCell);
            _filterPopup.IsOpen = true;
        }

        /// <summary>
        /// Aligns the popup's right edge with the column's right edge, then pulls it
        /// back if that would push it off the left of the grid - a narrow first column
        /// would otherwise open partly outside the control.
        /// </summary>
        private double ResolveFilterPopupOffset(TreeGridHeaderCell headerCell)
        {
            if (headerCell == null)
                return 0;

            var popupWidth = _filterPopupContent.Width;

            if (double.IsNaN(popupWidth) || popupWidth <= 0)
                popupWidth = 270;

            var offset = headerCell.ActualWidth - popupWidth;

            if (offset >= 0)
                return offset;

            var cellLeft = headerCell.TranslatePoint(new Point(0, 0), this).X;

            // Never let the popup start left of the grid itself.
            return Math.Max(offset, -cellLeft);
        }

        private void EnsureFilterPopup()
        {
            if (_filterPopup != null)
                return;

            _filterPopupContent = new FilterPopupControl();
            _filterPopupContent.FilterApplied += OnFilterPopupApplied;
            _filterPopupContent.SortRequested += OnFilterPopupSortRequested;
            _filterPopupContent.CloseRequested += (s, e) => _filterPopup.IsOpen = false;

            _filterPopup = new Popup
            {
                Child = _filterPopupContent,
                StaysOpen = false,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade
            };
        }

        private void OnFilterPopupApplied(object sender, FilterAppliedEventArgs e)
        {
            if (e.IsCleared || !e.Filter.HasFilter)
                _filterController.ClearFilter(e.Filter.MappingName);
            else
                _filterController.SetFilter(e.Filter);
        }

        private void OnFilterPopupSortRequested(object sender, FilterSortRequestedEventArgs e)
        {
            if (_filterPopupContent?.MappingName == null)
                return;

            SortColumn(_filterPopupContent.MappingName, e.Direction);
        }

        // ================================================================ EDITING

        /// <summary>Opens the editor on a cell. Returns false when editing was refused.</summary>
        public bool BeginEdit(TreeNode node, TreeGridColumn column)
        {
            if (!AllowEditing || node == null || column == null || !column.AllowEditing)
                return false;

            // Group headers hold no record, and display-only columns have nothing to
            // commit, so neither should ever open an editor.
            if (node.IsGroupHeader || !column.SupportsValueCommit && !(column is TreeGridTemplateColumn))
                return false;

            if (column is TreeGridTemplateColumn template && !template.HasEditTemplate)
                return false;

            var element = _editController.BeginEdit(node, column);
            if (element == null)
                return false;

            var columnIndex = _layout.IndexOf(column);
            _selection.SetCurrent(node, columnIndex);

            var cell = FindCell(node, columnIndex);

            if (cell == null)
            {
                // The cell was not realised; scroll it into view and retry next tick.
                _container?.ScrollIntoView(node.FlatIndex);

                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    var retry = FindCell(node, columnIndex);
                    retry?.BeginEdit(element);
                }));

                return true;
            }

            cell.BeginEdit(element);
            return true;
        }

        public bool BeginEdit() =>
            BeginEdit(_selection.CurrentNode, _layout.ColumnAt(_selection.CurrentColumnIndex));

        /// <summary>Commits the open editor. Returns false when validation rejected it.</summary>
        public bool EndEdit(bool commit = true)
        {
            if (!_editController.IsEditing)
                return true;

            var node = _editController.EditingNode;
            var column = _editController.EditingColumn;
            var columnIndex = _layout.IndexOf(column);

            var succeeded = _editController.EndEdit(commit);

            if (!succeeded)
                return false;

            FindCell(node, columnIndex)?.EndEdit();

            // The committed value changes what the cell displays, and a merge range may
            // have opened or closed as a result.
            _mergeController.Invalidate();
            _container?.RefreshRowContent();
            _container?.RefreshRowStates();

            return true;
        }

        public void CancelEdit() => EndEdit(commit: false);

        private void OnCellEndEditInternal(object sender, CellEndEditEventArgs e) =>
            CellEndEdit?.Invoke(this, e);

        private TreeGridCell FindCell(TreeNode node, int columnIndex)
        {
            if (node == null || node.FlatIndex < 0 || columnIndex < 0)
                return null;

            return _container?.GetRealizedRow(node.FlatIndex)?.GetCell(columnIndex);
        }

        /// <summary>Validates every mapped column of a row and paints any errors.</summary>
        public bool ValidateRow(TreeNode node)
        {
            var names = new List<string>();

            foreach (var column in Columns)
            {
                if (!string.IsNullOrEmpty(column.MappingName))
                    names.Add(column.MappingName);
            }

            var valid = _editController.ValidateRow(node, names);
            _container?.RefreshRowStates();
            return valid;
        }

        // ====================================================== STACKED HEADERS

        private void RebuildStackedHeaders()
        {
            if (_stackedHeaderPanel == null)
                return;

            _stackedHeaderPanel.Children.Clear();
            _stackedRows.Clear();

            foreach (var headerRow in StackedHeaderRows)
            {
                var control = new StackedHeaderRowControl
                {
                    Layout = _layout,
                    HeaderRow = headerRow,
                    Height = StackedHeaderRowHeight,
                    GridLineBrush = GridLineBrush
                };

                _stackedRows.Add(control);
                _stackedHeaderPanel.Children.Add(control);
            }

            ApplyVisualConfig();
            RefreshLayout();
        }

        // ==================================================== ROW DRAG AND DROP

        private void BeginRowDrag(Point origin)
        {
            var nodes = new List<TreeNode>();

            foreach (var item in _selection.SelectedItems)
            {
                var node = ResolveNode(item);
                if (node != null)
                    nodes.Add(node);
            }

            if (nodes.Count == 0 && _selection.CurrentNode != null &&
                !_selection.CurrentNode.IsGroupHeader)
                nodes.Add(_selection.CurrentNode);

            // Begin filters group headers too and refuses an empty set.
            if (!_dragController.Begin(nodes))
                return;

            EnsureDropAdorner();
            CaptureMouse();
        }

        private void EnsureDropAdorner()
        {
            if (_dropAdorner != null || _container == null)
                return;

            var layer = AdornerLayer.GetAdornerLayer(_container);
            if (layer == null)
                return;

            _dropAdorner = new RowDropAdorner(_container, DropIndicatorBrush);
            layer.Add(_dropAdorner);
        }

        private void UpdateRowDrag(Point containerPoint)
        {
            if (!_dragController.IsDragging || _container == null)
                return;

            var (rowIndex, offsetInRow) = _container.RowHitTest(containerPoint);

            if (rowIndex < 0)
            {
                _dropAdorner?.Update(-1, RowHeight, 0, RowDropPosition.Below, false);
                return;
            }

            var target = _dataSource.View[rowIndex];
            _dragController.Update(target, offsetInRow, RowHeight);

            var rowTop = _container.GetRowOffset(rowIndex);

            var y = _dragController.Position switch
            {
                RowDropPosition.Above => rowTop,
                RowDropPosition.Below => rowTop + RowHeight,
                _ => rowTop
            };

            // Indent the indicator to the target's depth so the intended parent is clear.
            var indent = 4 + (target.Level + 1) * IndentPerLevel;

            _dropAdorner?.Update(y, RowHeight, indent, _dragController.Position,
                _dragController.IsCurrentDropAllowed);
        }

        private void CompleteRowDrag()
        {
            if (!_dragController.IsDragging)
                return;

            var changed = _dragController.Complete(_dataSource.View.MutableRoots);

            RemoveDropAdorner();
            ReleaseMouseCapture();

            if (!changed)
                return;

            _mergeController.Invalidate();
            _dataSource.View.Rebuild();
            _container?.ResetRows();
            _container?.InvalidateMeasure();
        }

        private void CancelRowDrag()
        {
            _dragController.Cancel();
            RemoveDropAdorner();
            ReleaseMouseCapture();
        }

        private void RemoveDropAdorner()
        {
            if (_dropAdorner == null || _container == null)
                return;

            AdornerLayer.GetAdornerLayer(_container)?.Remove(_dropAdorner);
            _dropAdorner = null;
        }

        // ========================================================= CONTEXT MENUS

        protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonDown(e);

            if (_container == null)
                return;

            Focus();

            // Right-clicking an unselected row selects it first, so menu commands act
            // on what the user actually pointed at.
            var containerPoint = e.GetPosition(_container);
            var rowIndex = _container.RowIndexFromPoint(containerPoint);

            if (rowIndex < 0 || rowIndex >= _dataSource.View.Count)
                return;

            var node = _dataSource.View[rowIndex];

            if (!node.IsSelected)
            {
                var columnIndex = _container.ColumnIndexFromPoint(containerPoint);
                _selection.HandlePointerDown(rowIndex, Math.Max(0, columnIndex), ModifierKeys.None);
                _container.RefreshRowStates();
            }
        }

        protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseRightButtonUp(e);

            if (ShowContextMenuAt(e.GetPosition(this)))
                e.Handled = true;
        }

        /// <summary>
        /// Opens the appropriate menu for a point in grid coordinates.
        /// <para>
        /// This deliberately does not use ContextMenuOpening. WPF only raises that
        /// event when the element already has a non-null ContextMenu, so building the
        /// menu inside the handler meant every right-click displayed the menu built
        /// for the previous one - which is why "hide column" always hit whichever
        /// column had been clicked first.
        /// </para>
        /// </summary>
        public bool ShowContextMenuAt(Point gridPoint)
        {
            if (_container == null)
                return false;

            var region = ResolveRegion(gridPoint, out var node, out var column);

            if (region == GridRegion.None)
                return false;

            GridContextMenuInfo info;
            ContextMenu menu;

            switch (region)
            {
                case GridRegion.Header:
                    if (column == null)
                        return false;

                    info = new HeaderContextMenuInfo(this, column);
                    menu = HeaderContextMenu ??
                           (ShowDefaultContextMenus ? DefaultContextMenus.BuildHeaderMenu(this, column) : null);
                    break;

                case GridRegion.Expander:
                    info = new ExpanderContextMenuInfo(this, node);
                    menu = ExpanderContextMenu ??
                           (ShowDefaultContextMenus ? DefaultContextMenus.BuildExpanderMenu(this, node) : null);
                    break;

                default:
                    info = new RecordContextMenuInfo(this, node, column);
                    menu = RecordContextMenu ??
                           (ShowDefaultContextMenus ? DefaultContextMenus.BuildRecordMenu(this, node) : null);
                    break;
            }

            var args = new GridContextMenuOpeningEventArgs(region, info, menu);
            GridContextMenuOpening?.Invoke(this, args);

            if (args.Cancel || args.ContextMenu == null)
                return false;

            // The info object becomes the menu's DataContext so command bindings can
            // reach the clicked row and column.
            args.ContextMenu.DataContext = info;
            args.ContextMenu.PlacementTarget = this;
            args.ContextMenu.Placement = PlacementMode.MousePoint;

            ContextMenu = args.ContextMenu;
            args.ContextMenu.IsOpen = true;

            return true;
        }

        /// <summary>Resolves which part of the grid a point falls in.</summary>
        private GridRegion ResolveRegion(Point gridPoint, out TreeNode node, out TreeGridColumn column)
        {
            node = null;
            column = null;

            if (_headerHost != null)
            {
                // TranslatePoint keeps everything in the visual tree; the previous
                // screen round-trip was an unnecessary source of drift.
                var headerPoint = TranslatePoint(gridPoint, _headerHost);

                if (headerPoint.Y >= 0 && headerPoint.Y <= _headerHost.ActualHeight &&
                    headerPoint.X >= 0 && headerPoint.X <= _headerHost.ActualWidth)
                {
                    var headerIndex = _layout.ColumnIndexFromX(headerPoint.X,
                        _container?.HorizontalOffset ?? 0, _headerHost.ActualWidth);

                    column = _layout.ColumnAt(headerIndex);
                    return GridRegion.Header;
                }
            }

            var containerPoint = TranslatePoint(gridPoint, _container);
            var rowIndex = _container.RowIndexFromPoint(containerPoint);

            if (rowIndex < 0 || rowIndex >= _dataSource.View.Count)
                return GridRegion.None;

            node = _dataSource.View[rowIndex];

            var columnIndex = _container.ColumnIndexFromPoint(containerPoint);
            column = _layout.ColumnAt(columnIndex);

            if (columnIndex == ExpanderColumnIndex && column != null)
            {
                var indentEnd = 4 + (node.Level + 1) * IndentPerLevel;
                var columnLeft = column.LeftOffset;

                if (columnIndex >= _layout.FrozenColumnCount)
                    columnLeft -= _container.HorizontalOffset;

                if (containerPoint.X - columnLeft <= indentEnd)
                    return GridRegion.Expander;
            }

            return GridRegion.Record;
        }

        // ============================================================= CLIPBOARD

        public bool Copy() => CopyInternal(false);

        public bool Cut() => CopyInternal(true);

        private bool CopyInternal(bool isCut)
        {
            if (!AllowCopy)
                return false;

            var nodes = GetSelectedNodesInViewOrder();

            if (nodes.Count == 0)
                return false;

            if (!_clipboard.Copy(nodes, Columns, isCut))
                return false;

            if (!isCut || !AllowEditing)
                return true;

            // Cut clears the source cells only after the clipboard write succeeded.
            foreach (var node in nodes)
            {
                foreach (var column in Columns)
                {
                    if (column.AllowEditing && !string.IsNullOrEmpty(column.MappingName))
                        PropertyAccessor.SetValue(node.Item, column.MappingName, null);
                }
            }

            _container?.RefreshRowContent();
            return true;
        }

        public int Paste()
        {
            if (!AllowPaste || !AllowEditing)
                return 0;

            var node = _selection.CurrentNode;

            if (node == null)
                return 0;

            var written = _clipboard.Paste(node, _selection.CurrentColumnIndex, _dataSource.View,
                _layout.VisibleColumns, WritePastedCell);

            if (written > 0)
            {
                _mergeController.Invalidate();
                _container?.RefreshRowContent();
                _container?.RefreshRowStates();
            }

            return written;
        }

        /// <summary>
        /// Pasted values go through the same coercion and validation as typed edits, so
        /// a bad paste reports errors instead of corrupting the model.
        /// </summary>
        private bool WritePastedCell(TreeNode node, TreeGridColumn column, string text)
        {
            if (!CellValidator.TryCoerce(node.Item, column.MappingName, text, out var coerced, out _))
                return false;

            var result = CellValidator.ValidateCandidate(node.Item, column.MappingName, coerced);

            if (!result.IsValid)
                return false;

            PropertyAccessor.SetValue(node.Item, column.MappingName, coerced);
            return true;
        }

        private List<TreeNode> GetSelectedNodesInViewOrder()
        {
            var nodes = new List<TreeNode>();

            for (var i = 0; i < _dataSource.View.Count; i++)
            {
                var node = _dataSource.View[i];

                // A group header has no record, so copying one would emit a blank row.
                if (node.IsSelected && !node.IsGroupHeader)
                    nodes.Add(node);
            }

            if (nodes.Count == 0 && _selection.CurrentNode != null && !_selection.CurrentNode.IsGroupHeader)
                nodes.Add(_selection.CurrentNode);

            return nodes;
        }

        // ================================================================ EXPORT

        /// <summary>Exports through any IGridExporter. Excel and PDF live in TreeGrid.Wpf.Export.</summary>
        public void Export(IGridExporter exporter, Stream stream, GridExportOptions options = null)
        {
            if (exporter == null || stream == null)
                return;

            options ??= new GridExportOptions();

            var columns = ExportDataBuilder.ResolveColumns(Columns, options);
            var rows = ExportDataBuilder.BuildRows(_dataSource.View, columns, options, _selection.SelectedItems);

            exporter.Export(stream, columns, rows, options);
        }

        public void Export(IGridExporter exporter, string path, GridExportOptions options = null)
        {
            if (exporter == null || string.IsNullOrEmpty(path))
                return;

            using (var stream = File.Create(path))
                Export(exporter, stream, options);
        }

        public void ExportToCsv(string path, GridExportOptions options = null) =>
            Export(new CsvExporter(), path, options);

        // =========================================================== INCREMENTAL

        /// <summary>
        /// Applies an incremental source change. Unlike a reload this keeps node
        /// identity, so selection, check state and expansion all survive untouched.
        /// </summary>
        // ============================================================== GROUPING

        /// <summary>Adds a grouping level, or moves an existing one to <paramref name="index"/>.</summary>
        public void GroupByColumn(string mappingName, int index = -1,
            ListSortDirection direction = ListSortDirection.Ascending)
        {
            if (string.IsNullOrEmpty(mappingName))
                return;

            var existing = _groupController.Descriptions.IndexOfColumn(mappingName);

            if (existing >= 0)
            {
                var target = index < 0 ? _groupController.Descriptions.Count - 1 : index;
                target = Math.Min(Math.Max(0, target), _groupController.Descriptions.Count - 1);

                if (target == existing)
                    return;

                if (!RequestGroupingChange(GroupingAction.Reordered, mappingName, existing, target))
                    return;

                _groupController.Descriptions.Move(existing, target);
                return;
            }

            var column = FindColumn(mappingName);

            if (column != null && !column.AllowGrouping)
                return;

            var insertAt = index < 0 || index > _groupController.Descriptions.Count
                ? _groupController.Descriptions.Count
                : index;

            if (!RequestGroupingChange(GroupingAction.Grouped, mappingName, -1, insertAt))
                return;

            var description = new GroupColumnDescription
            {
                ColumnName = mappingName,
                SortDirection = direction,
                HeaderText = column?.ResolvedHeaderText ?? mappingName
            };

            _groupController.Descriptions.Insert(insertAt, description);
        }

        public void UngroupColumn(string mappingName)
        {
            var index = _groupController.Descriptions.IndexOfColumn(mappingName);

            if (index < 0)
                return;

            if (!RequestGroupingChange(GroupingAction.Ungrouped, mappingName, index, -1))
                return;

            _groupController.Descriptions.RemoveAt(index);
        }

        public void ClearGrouping()
        {
            if (_groupController.Descriptions.Count == 0)
                return;

            if (!RequestGroupingChange(GroupingAction.Cleared, null, -1, -1))
                return;

            _groupController.Descriptions.Clear();
        }

        /// <summary>Moves a grouping level, changing precedence.</summary>
        public void MoveGroup(int fromIndex, int toIndex)
        {
            var count = _groupController.Descriptions.Count;

            if (fromIndex < 0 || fromIndex >= count)
                return;

            toIndex = Math.Min(Math.Max(0, toIndex), count - 1);

            if (fromIndex == toIndex)
                return;

            var columnName = _groupController.Descriptions[fromIndex].ColumnName;

            if (!RequestGroupingChange(GroupingAction.Reordered, columnName, fromIndex, toIndex))
                return;

            _groupController.Descriptions.Move(fromIndex, toIndex);
        }

        public void ExpandAllGroups() => _dataSource.View.ExpandAll();

        public void CollapseAllGroups()
        {
            foreach (var root in _dataSource.View.RootNodes)
                CollapseGroupsRecursive(root);

            _dataSource.View.Rebuild();
        }

        private static void CollapseGroupsRecursive(TreeNode node)
        {
            if (!node.IsGroupHeader)
                return;

            node.IsExpanded = false;

            for (var i = 0; i < node.ChildNodes.Count; i++)
                CollapseGroupsRecursive(node.ChildNodes[i]);
        }

        private TreeGridColumn FindColumn(string mappingName)
        {
            foreach (var column in Columns)
            {
                if (string.Equals(column.MappingName, mappingName, StringComparison.Ordinal))
                    return column;
            }

            return null;
        }

        /// <summary>
        /// Rebuilds the view for the current grouping.
        /// <para>
        /// The ungrouped roots are cached rather than regenerated, so clearing grouping
        /// restores the original tree - including expansion state - instead of forcing
        /// a reload from the source.
        /// </para>
        /// </summary>
        public void ApplyGrouping()
        {
            if (!_templateApplied)
                return;

            _groupDropArea?.Refresh();

            if (!_groupController.IsGrouped)
            {
                if (_ungroupedRoots != null)
                {
                    _dataSource.View.SetRoots(_ungroupedRoots);
                    _ungroupedRoots = null;
                }

                _groupedNodeMap = null;
                FinishGroupingPass();
                return;
            }

            // Capture the real tree the first time we group so it can be handed back.
            _ungroupedRoots ??= new List<TreeNode>(_dataSource.View.RootNodes);

            var records = GroupController.CollectRecords(_ungroupedRoots);

            var grouped = _groupController.Build(
                records,
                FindColumn,
                _sortController.HasSort ? _sortController.BuildComparison() : null);

            if (!AutoExpandGroups)
            {
                foreach (var root in grouped)
                    CollapseGroupsRecursive(root);
            }

            RebuildGroupedNodeMap(grouped);
            _dataSource.View.SetRoots(grouped);
            FinishGroupingPass();
        }

        private void FinishGroupingPass()
        {
            if (_filterController.HasFilters)
                _filterController.Apply(_dataSource.View.RootNodes);

            _mergeController.Invalidate();
            _dataSource.View.Rebuild();

            _container?.ResetRows();
            _container?.InvalidateMeasure();
            UpdateFooter();

            var change = _pendingGroupChange ?? new GroupingChangedEventArgs(
                GroupingAction.Reordered, null, null, -1, -1, _groupController.Descriptions.Count);

            _pendingGroupChange = null;
            GroupingChanged?.Invoke(this, change);
        }

        /// <summary>Runs the cancellable pre-change event and records detail for the post-change one.</summary>
        private bool RequestGroupingChange(GroupingAction action, string columnName, int oldIndex, int newIndex)
        {
            var column = columnName == null ? null : FindColumn(columnName);
            var changing = new GroupingChangingEventArgs(action, columnName, column, oldIndex, newIndex);

            GroupingChanging?.Invoke(this, changing);

            if (changing.Cancel)
                return false;

            _pendingGroupChange = new GroupingChangedEventArgs(action, columnName, column,
                oldIndex, newIndex, _groupController.Descriptions.Count);

            return true;
        }

        // ------------------------------------------------------- chip gestures

        private void OnGroupChipRemoved(object sender, RoutedEventArgs e)
        {
            if (e is GroupChipEventArgs args)
            {
                e.Handled = true;
                UngroupColumn(args.ColumnName);
            }
        }

        private void OnGroupChipSortToggled(object sender, RoutedEventArgs e)
        {
            if (!(e is GroupChipEventArgs args))
                return;

            e.Handled = true;

            var description = _groupController.Descriptions.Find(args.ColumnName);

            if (description == null)
                return;

            var index = _groupController.Descriptions.IndexOf(description);

            if (!RequestGroupingChange(GroupingAction.SortDirectionChanged, args.ColumnName, index, index))
                return;

            description.SortDirection = description.SortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            ApplyGrouping();
        }

        private void OnGroupChipDragStarted(object sender, RoutedEventArgs e)
        {
            if (!(e is GroupChipEventArgs args))
                return;

            var index = _groupController.Descriptions.IndexOfColumn(args.ColumnName);
            var starting = new GroupChipDragEventArgs(args.ColumnName, index);

            GroupChipDragStarting?.Invoke(this, starting);

            // Handled tells the panel to abandon the drag, pinning that level.
            e.Handled = starting.Cancel;
        }

        private void OnGroupChipReordered(object sender, RoutedEventArgs e)
        {
            if (e is GroupReorderEventArgs args)
            {
                e.Handled = true;
                MoveGroup(args.FromIndex, args.ToIndex);
            }
        }

        /// <summary>True when a point in grid coordinates is over the group panel.</summary>
        private bool IsOverGroupArea(Point screenPoint, out Point areaPoint)
        {
            areaPoint = default;

            if (_groupDropArea == null || !AllowGrouping || !ShowGroupDropArea ||
                _groupDropArea.Visibility != Visibility.Visible)
                return false;

            areaPoint = _groupDropArea.PointFromScreen(screenPoint);

            return areaPoint.X >= 0 && areaPoint.X <= _groupDropArea.ActualWidth &&
                   areaPoint.Y >= 0 && areaPoint.Y <= _groupDropArea.ActualHeight;
        }

        private void OnIncrementalChange(object sender, EventArgs e)
        {
            if (_sortController.HasSort)
                _dataSource.View.SortHierarchy(_sortController.BuildComparison());

            if (_filterController.HasFilters)
                _filterController.Apply(_dataSource.View.RootNodes);

            _mergeController.Invalidate();
            _dataSource.View.Rebuild();
            _container?.InvalidateMeasure();
        }
    }
}
