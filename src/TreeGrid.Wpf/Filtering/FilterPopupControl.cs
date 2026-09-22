using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace TreeGrid.Wpf.Filtering
{
    public sealed class FilterAppliedEventArgs : EventArgs
    {
        public FilterAppliedEventArgs(ColumnFilter filter, bool isCleared)
        {
            Filter = filter;
            IsCleared = isCleared;
        }

        public ColumnFilter Filter { get; }

        public bool IsCleared { get; }
    }

    public sealed class FilterSortRequestedEventArgs : EventArgs
    {
        public FilterSortRequestedEventArgs(ListSortDirection direction)
        {
            Direction = direction;
        }

        public ListSortDirection Direction { get; }
    }

    /// <summary>
    /// The filter UI. Offers two routes to the same result: a checkbox list of
    /// distinct values, and a pair of chained conditions. The list is disabled when
    /// distinct values cannot be enumerated, which is the case for unbounded
    /// load-on-demand sources.
    /// </summary>
    public class FilterPopupControl : Control
    {
        public static readonly DependencyProperty SearchTextProperty = DependencyProperty.Register(
            nameof(SearchText), typeof(string), typeof(FilterPopupControl),
            new PropertyMetadata(string.Empty, OnSearchTextChanged));

        public static readonly DependencyProperty SelectAllStateProperty = DependencyProperty.Register(
            nameof(SelectAllState), typeof(bool?), typeof(FilterPopupControl), new PropertyMetadata(true));

        public static readonly DependencyProperty CanUseValueListProperty = DependencyProperty.Register(
            nameof(CanUseValueList), typeof(bool), typeof(FilterPopupControl), new PropertyMetadata(true));

        public static readonly DependencyProperty ColumnHeaderProperty = DependencyProperty.Register(
            nameof(ColumnHeader), typeof(string), typeof(FilterPopupControl), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty IsAdvancedExpandedProperty = DependencyProperty.Register(
            nameof(IsAdvancedExpanded), typeof(bool), typeof(FilterPopupControl), new PropertyMetadata(false));

        public static readonly DependencyProperty FirstConditionProperty = DependencyProperty.Register(
            nameof(FirstCondition), typeof(FilterType), typeof(FilterPopupControl),
            new PropertyMetadata(FilterType.Contains));

        public static readonly DependencyProperty SecondConditionProperty = DependencyProperty.Register(
            nameof(SecondCondition), typeof(FilterType), typeof(FilterPopupControl),
            new PropertyMetadata(FilterType.Contains));

        public static readonly DependencyProperty FirstValueProperty = DependencyProperty.Register(
            nameof(FirstValue), typeof(string), typeof(FilterPopupControl), new PropertyMetadata(null));

        public static readonly DependencyProperty SecondValueProperty = DependencyProperty.Register(
            nameof(SecondValue), typeof(string), typeof(FilterPopupControl), new PropertyMetadata(null));

        public static readonly DependencyProperty IsAndJoinProperty = DependencyProperty.Register(
            nameof(IsAndJoin), typeof(bool), typeof(FilterPopupControl), new PropertyMetadata(true));

        public static readonly DependencyProperty IsCaseSensitiveProperty = DependencyProperty.Register(
            nameof(IsCaseSensitive), typeof(bool), typeof(FilterPopupControl), new PropertyMetadata(false));

        // Appearance. All null by default; the grid fills them from its resolved
        // visual style, which falls back to the current theme.
        public static readonly DependencyProperty PopupBorderBrushProperty = DependencyProperty.Register(
            nameof(PopupBorderBrush), typeof(Brush), typeof(FilterPopupControl), new PropertyMetadata(null));

        public static readonly DependencyProperty ListBackgroundProperty = DependencyProperty.Register(
            nameof(ListBackground), typeof(Brush), typeof(FilterPopupControl), new PropertyMetadata(null));

        public static readonly DependencyProperty ListForegroundProperty = DependencyProperty.Register(
            nameof(ListForeground), typeof(Brush), typeof(FilterPopupControl), new PropertyMetadata(null));

        public static readonly DependencyProperty ListBorderBrushProperty = DependencyProperty.Register(
            nameof(ListBorderBrush), typeof(Brush), typeof(FilterPopupControl), new PropertyMetadata(null));

        public static readonly DependencyProperty ItemHoverBackgroundProperty = DependencyProperty.Register(
            nameof(ItemHoverBackground), typeof(Brush), typeof(FilterPopupControl), new PropertyMetadata(null));

        public static readonly DependencyProperty ItemSelectedBackgroundProperty = DependencyProperty.Register(
            nameof(ItemSelectedBackground), typeof(Brush), typeof(FilterPopupControl), new PropertyMetadata(null));

        public static readonly DependencyProperty AccentBrushProperty = DependencyProperty.Register(
            nameof(AccentBrush), typeof(Brush), typeof(FilterPopupControl), new PropertyMetadata(null));

        // Editors inside the popup need their own brushes: a TextBox, ComboBox,
        // CheckBox, RadioButton and Expander each carry a Foreground from their own
        // default style, and a style setter beats an inherited value - which is why
        // the Conditions section stayed black on a dark popup.
        public static readonly DependencyProperty InputBackgroundProperty = DependencyProperty.Register(
            nameof(InputBackground), typeof(Brush), typeof(FilterPopupControl), new PropertyMetadata(null));

        public static readonly DependencyProperty InputForegroundProperty = DependencyProperty.Register(
            nameof(InputForeground), typeof(Brush), typeof(FilterPopupControl), new PropertyMetadata(null));

        public static readonly DependencyProperty InputBorderBrushProperty = DependencyProperty.Register(
            nameof(InputBorderBrush), typeof(Brush), typeof(FilterPopupControl), new PropertyMetadata(null));

        public static readonly DependencyProperty ListMaxHeightProperty = DependencyProperty.Register(
            nameof(ListMaxHeight), typeof(double), typeof(FilterPopupControl), new PropertyMetadata(180d));

        public static readonly DependencyProperty AllowResizeProperty = DependencyProperty.Register(
            nameof(AllowResize), typeof(bool), typeof(FilterPopupControl), new PropertyMetadata(true));

        public static readonly DependencyProperty MinPopupWidthProperty = DependencyProperty.Register(
            nameof(MinPopupWidth), typeof(double), typeof(FilterPopupControl), new PropertyMetadata(220d));

        public static readonly DependencyProperty MinListHeightProperty = DependencyProperty.Register(
            nameof(MinListHeight), typeof(double), typeof(FilterPopupControl), new PropertyMetadata(80d));

        static FilterPopupControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FilterPopupControl),
                new FrameworkPropertyMetadata(typeof(FilterPopupControl)));
        }

        public FilterPopupControl()
        {
            Elements = new ObservableCollection<FilterElement>();
            VisibleElements = new ObservableCollection<FilterElement>();
            ConditionTypes = Enum.GetValues(typeof(FilterType)).Cast<FilterType>().ToList();
        }

        public string SearchText
        {
            get => (string)GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        public bool? SelectAllState
        {
            get => (bool?)GetValue(SelectAllStateProperty);
            set => SetValue(SelectAllStateProperty, value);
        }

        /// <summary>False when distinct values could not be enumerated.</summary>
        public bool CanUseValueList
        {
            get => (bool)GetValue(CanUseValueListProperty);
            set => SetValue(CanUseValueListProperty, value);
        }

        public string ColumnHeader
        {
            get => (string)GetValue(ColumnHeaderProperty);
            set => SetValue(ColumnHeaderProperty, value);
        }

        public bool IsAdvancedExpanded
        {
            get => (bool)GetValue(IsAdvancedExpandedProperty);
            set => SetValue(IsAdvancedExpandedProperty, value);
        }

        public FilterType FirstCondition
        {
            get => (FilterType)GetValue(FirstConditionProperty);
            set => SetValue(FirstConditionProperty, value);
        }

        public FilterType SecondCondition
        {
            get => (FilterType)GetValue(SecondConditionProperty);
            set => SetValue(SecondConditionProperty, value);
        }

        public string FirstValue
        {
            get => (string)GetValue(FirstValueProperty);
            set => SetValue(FirstValueProperty, value);
        }

        public string SecondValue
        {
            get => (string)GetValue(SecondValueProperty);
            set => SetValue(SecondValueProperty, value);
        }

        public bool IsAndJoin
        {
            get => (bool)GetValue(IsAndJoinProperty);
            set => SetValue(IsAndJoinProperty, value);
        }

        public bool IsCaseSensitive
        {
            get => (bool)GetValue(IsCaseSensitiveProperty);
            set => SetValue(IsCaseSensitiveProperty, value);
        }

        public Brush PopupBorderBrush
        {
            get => (Brush)GetValue(PopupBorderBrushProperty);
            set => SetValue(PopupBorderBrushProperty, value);
        }

        /// <summary>Background of the value checklist.</summary>
        public Brush ListBackground
        {
            get => (Brush)GetValue(ListBackgroundProperty);
            set => SetValue(ListBackgroundProperty, value);
        }

        public Brush ListForeground
        {
            get => (Brush)GetValue(ListForegroundProperty);
            set => SetValue(ListForegroundProperty, value);
        }

        public Brush ListBorderBrush
        {
            get => (Brush)GetValue(ListBorderBrushProperty);
            set => SetValue(ListBorderBrushProperty, value);
        }

        public Brush ItemHoverBackground
        {
            get => (Brush)GetValue(ItemHoverBackgroundProperty);
            set => SetValue(ItemHoverBackgroundProperty, value);
        }

        public Brush ItemSelectedBackground
        {
            get => (Brush)GetValue(ItemSelectedBackgroundProperty);
            set => SetValue(ItemSelectedBackgroundProperty, value);
        }

        /// <summary>Used for the column-name heading and other emphasis.</summary>
        public Brush AccentBrush
        {
            get => (Brush)GetValue(AccentBrushProperty);
            set => SetValue(AccentBrushProperty, value);
        }

        /// <summary>Background of the search box, condition boxes and drop-downs.</summary>
        public Brush InputBackground
        {
            get => (Brush)GetValue(InputBackgroundProperty);
            set => SetValue(InputBackgroundProperty, value);
        }

        /// <summary>Text colour for every editor in the popup, Conditions included.</summary>
        public Brush InputForeground
        {
            get => (Brush)GetValue(InputForegroundProperty);
            set => SetValue(InputForegroundProperty, value);
        }

        public Brush InputBorderBrush
        {
            get => (Brush)GetValue(InputBorderBrushProperty);
            set => SetValue(InputBorderBrushProperty, value);
        }

        public double ListMaxHeight
        {
            get => (double)GetValue(ListMaxHeightProperty);
            set => SetValue(ListMaxHeightProperty, value);
        }

        /// <summary>Shows a grip in the bottom-right corner for dragging the popup larger.</summary>
        public bool AllowResize
        {
            get => (bool)GetValue(AllowResizeProperty);
            set => SetValue(AllowResizeProperty, value);
        }

        public double MinPopupWidth
        {
            get => (double)GetValue(MinPopupWidthProperty);
            set => SetValue(MinPopupWidthProperty, value);
        }

        public double MinListHeight
        {
            get => (double)GetValue(MinListHeightProperty);
            set => SetValue(MinListHeightProperty, value);
        }

        /// <summary>Raised after a resize drag, so the host can remember the new size.</summary>
        public event EventHandler Resized;

        /// <summary>Every distinct value in the column.</summary>
        public ObservableCollection<FilterElement> Elements { get; }

        /// <summary>The subset matching the search box, which is what the list binds to.</summary>
        public ObservableCollection<FilterElement> VisibleElements { get; }

        public IReadOnlyList<FilterType> ConditionTypes { get; }

        public string MappingName { get; private set; }

        /// <summary>True when the column uses strongly-typed comparison for conditions.</summary>
        public bool UseStronglyTypedConditions { get; set; }

        public event EventHandler<FilterAppliedEventArgs> FilterApplied;

        public event EventHandler<FilterSortRequestedEventArgs> SortRequested;

        public event EventHandler CloseRequested;

        private ButtonBase _okButton;
        private ButtonBase _cancelButton;
        private ButtonBase _clearButton;
        private ButtonBase _sortAscButton;
        private ButtonBase _sortDescButton;
        private ToggleButton _selectAllBox;
        private Thumb _resizeGrip;
        private bool _suppressSelectAll;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            Detach();

            _okButton = GetTemplateChild("PART_OkButton") as ButtonBase;
            _cancelButton = GetTemplateChild("PART_CancelButton") as ButtonBase;
            _clearButton = GetTemplateChild("PART_ClearButton") as ButtonBase;
            _sortAscButton = GetTemplateChild("PART_SortAscending") as ButtonBase;
            _sortDescButton = GetTemplateChild("PART_SortDescending") as ButtonBase;
            _selectAllBox = GetTemplateChild("PART_SelectAll") as ToggleButton;
            _resizeGrip = GetTemplateChild("PART_ResizeGrip") as Thumb;

            if (_okButton != null) _okButton.Click += OnOk;
            if (_cancelButton != null) _cancelButton.Click += OnCancel;
            if (_clearButton != null) _clearButton.Click += OnClear;
            if (_sortAscButton != null) _sortAscButton.Click += OnSortAscending;
            if (_sortDescButton != null) _sortDescButton.Click += OnSortDescending;
            if (_selectAllBox != null) _selectAllBox.Click += OnSelectAllClicked;

            if (_resizeGrip != null)
            {
                _resizeGrip.DragDelta += OnResizeDelta;
                _resizeGrip.DragCompleted += OnResizeCompleted;
            }
        }

        private void Detach()
        {
            if (_okButton != null) _okButton.Click -= OnOk;
            if (_cancelButton != null) _cancelButton.Click -= OnCancel;
            if (_clearButton != null) _clearButton.Click -= OnClear;
            if (_sortAscButton != null) _sortAscButton.Click -= OnSortAscending;
            if (_sortDescButton != null) _sortDescButton.Click -= OnSortDescending;
            if (_selectAllBox != null) _selectAllBox.Click -= OnSelectAllClicked;

            if (_resizeGrip != null)
            {
                _resizeGrip.DragDelta -= OnResizeDelta;
                _resizeGrip.DragCompleted -= OnResizeCompleted;
            }
        }

        private void OnResizeDelta(object sender, DragDeltaEventArgs e)
        {
            e.Handled = true;

            // Width may still be NaN if the style never set one; fall back to what is
            // actually on screen so the first drag does not jump.
            var width = double.IsNaN(Width) || Width <= 0 ? ActualWidth : Width;

            Width = Math.Max(MinPopupWidth, width + e.HorizontalChange);

            // Only the list grows vertically; the header, conditions and buttons keep
            // their natural height, so the popup stays usable at any size.
            ListMaxHeight = Math.Max(MinListHeight, ListMaxHeight + e.VerticalChange);
        }

        private void OnResizeCompleted(object sender, DragCompletedEventArgs e)
        {
            e.Handled = true;
            Resized?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Loads the popup for a column. Existing predicates pre-select the list.</summary>
        public void Initialize(string mappingName, string header, IEnumerable<FilterElement> elements,
            ColumnFilter existingFilter, bool canUseValueList)
        {
            MappingName = mappingName;
            ColumnHeader = header;
            CanUseValueList = canUseValueList;
            SearchText = string.Empty;

            foreach (var element in Elements)
                element.PropertyChanged -= OnElementChanged;

            Elements.Clear();

            if (elements != null)
            {
                var selectedTexts = ExtractSelectedTexts(existingFilter);

                foreach (var element in elements)
                {
                    element.IsSelected = selectedTexts == null || selectedTexts.Contains(element.DisplayText);
                    element.PropertyChanged += OnElementChanged;
                    Elements.Add(element);
                }
            }

            LoadConditions(existingFilter);
            RefreshVisibleElements();
            UpdateSelectAllState();
        }

        /// <summary>
        /// A filter built from the checkbox list is an OR chain of equality predicates,
        /// so it can be read back to restore the tick marks. Condition filters cannot,
        /// which is why they populate the advanced section instead.
        /// </summary>
        private static HashSet<string> ExtractSelectedTexts(ColumnFilter filter)
        {
            if (filter == null || !filter.HasFilter)
                return null;

            if (filter.Predicates.Any(p => p.FilterType != FilterType.Equals || p.PredicateType != PredicateType.Or))
                return null;

            return new HashSet<string>(filter.Predicates.Select(p => p.FilterValue?.ToString() ?? string.Empty));
        }

        private void LoadConditions(ColumnFilter filter)
        {
            FirstValue = null;
            SecondValue = null;
            FirstCondition = FilterType.Contains;
            SecondCondition = FilterType.Contains;
            IsAndJoin = true;

            if (filter == null || !filter.HasFilter)
                return;

            var conditions = filter.Predicates
                .Where(p => p.FilterType != FilterType.Equals || p.PredicateType != PredicateType.Or)
                .ToList();

            if (conditions.Count == 0)
                return;

            IsAdvancedExpanded = true;

            FirstCondition = conditions[0].FilterType;
            FirstValue = conditions[0].FilterValue?.ToString();
            IsCaseSensitive = conditions[0].IsCaseSensitive;

            if (conditions.Count > 1)
            {
                SecondCondition = conditions[1].FilterType;
                SecondValue = conditions[1].FilterValue?.ToString();
                IsAndJoin = conditions[1].PredicateType == PredicateType.And;
            }
        }

        private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
            ((FilterPopupControl)d).RefreshVisibleElements();

        private void RefreshVisibleElements()
        {
            VisibleElements.Clear();

            var search = SearchText;
            var hasSearch = !string.IsNullOrWhiteSpace(search);

            foreach (var element in Elements)
            {
                if (hasSearch &&
                    element.DisplayText.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) < 0)
                    continue;

                VisibleElements.Add(element);
            }
        }

        private void OnElementChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FilterElement.IsSelected))
                UpdateSelectAllState();
        }

        private void UpdateSelectAllState()
        {
            if (_suppressSelectAll)
                return;

            var total = VisibleElements.Count;
            if (total == 0)
            {
                SelectAllState = false;
                return;
            }

            var selected = VisibleElements.Count(e => e.IsSelected);

            SelectAllState = selected == 0
                ? false
                : selected == total ? true : (bool?)null;
        }

        private void OnSelectAllClicked(object sender, RoutedEventArgs e)
        {
            // Tri-state on the way in, binary on the way out: an indeterminate
            // Select All resolves to "select everything".
            var target = SelectAllState != true;

            _suppressSelectAll = true;
            try
            {
                foreach (var element in VisibleElements)
                    element.IsSelected = target;
            }
            finally
            {
                _suppressSelectAll = false;
            }

            SelectAllState = target;
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            var filter = BuildFilter();
            FilterApplied?.Invoke(this, new FilterAppliedEventArgs(filter, !filter.HasFilter));
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnCancel(object sender, RoutedEventArgs e) =>
            CloseRequested?.Invoke(this, EventArgs.Empty);

        private void OnClear(object sender, RoutedEventArgs e)
        {
            FilterApplied?.Invoke(this, new FilterAppliedEventArgs(new ColumnFilter(MappingName), true));
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnSortAscending(object sender, RoutedEventArgs e)
        {
            SortRequested?.Invoke(this, new FilterSortRequestedEventArgs(ListSortDirection.Ascending));
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnSortDescending(object sender, RoutedEventArgs e)
        {
            SortRequested?.Invoke(this, new FilterSortRequestedEventArgs(ListSortDirection.Descending));
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Conditions win when present, since typing one is a deliberate act; otherwise
        /// the checkbox list is used. An all-selected list means no filter at all.
        /// </summary>
        public ColumnFilter BuildFilter()
        {
            var filter = new ColumnFilter(MappingName);
            var behavior = UseStronglyTypedConditions
                ? FilterBehavior.StronglyTyped
                : FilterBehavior.StringTyped;

            var hasFirst = !string.IsNullOrEmpty(FirstValue) || IsValuelessCondition(FirstCondition);
            var hasSecond = !string.IsNullOrEmpty(SecondValue) || IsValuelessCondition(SecondCondition);

            if (IsAdvancedExpanded && (hasFirst || hasSecond))
            {
                if (hasFirst)
                {
                    filter.Predicates.Add(new FilterPredicate
                    {
                        FilterType = FirstCondition,
                        FilterValue = FirstValue,
                        PredicateType = PredicateType.And,
                        IsCaseSensitive = IsCaseSensitive,
                        FilterBehavior = behavior
                    });
                }

                if (hasSecond)
                {
                    filter.Predicates.Add(new FilterPredicate
                    {
                        FilterType = SecondCondition,
                        FilterValue = SecondValue,
                        PredicateType = IsAndJoin ? PredicateType.And : PredicateType.Or,
                        IsCaseSensitive = IsCaseSensitive,
                        FilterBehavior = behavior
                    });
                }

                return filter;
            }

            if (!CanUseValueList)
                return filter;

            var selected = Elements.Where(el => el.IsSelected).ToList();

            // Everything ticked is the same as no filter; keeping it would only cost
            // an evaluation pass on every node.
            if (selected.Count == Elements.Count)
                return filter;

            return FilterController.BuildFromSelection(MappingName, selected);
        }

        private static bool IsValuelessCondition(FilterType type) =>
            type == FilterType.Empty || type == FilterType.NotEmpty ||
            type == FilterType.Null || type == FilterType.NotNull;
    }
}
