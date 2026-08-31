using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace TreeGrid.Wpf.Filtering
{
    public enum FilterType
    {
        Equals,
        NotEquals,
        Contains,
        NotContains,
        StartsWith,
        EndsWith,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        Empty,
        NotEmpty,
        Null,
        NotNull
    }

    public enum PredicateType
    {
        And,
        Or
    }

    public enum FilterBehavior
    {
        /// <summary>Compare using the displayed text. Right for checkbox lists.</summary>
        StringTyped,

        /// <summary>Coerce the filter value to the column's type before comparing.</summary>
        StronglyTyped
    }

    /// <summary>
    /// Determines how much of the hierarchy survives a filter. A tree grid cannot
    /// simply drop non-matching rows: a match five levels deep is meaningless without
    /// the ancestors that give it context.
    /// </summary>
    public enum FilterNodeMode
    {
        /// <summary>Only matching nodes remain, flattened against their surviving ancestors.</summary>
        MatchingNodesOnly,

        /// <summary>Matching nodes plus every ancestor. The usual choice.</summary>
        MatchingAndParentNodes,

        /// <summary>Matching nodes plus their whole subtree.</summary>
        MatchingAndChildNodes,

        /// <summary>Matching nodes plus ancestors and descendants.</summary>
        MatchingParentAndChildNodes
    }

    public sealed class FilterPredicate
    {
        public FilterType FilterType { get; set; } = FilterType.Equals;

        public object FilterValue { get; set; }

        /// <summary>How this predicate joins the one before it.</summary>
        public PredicateType PredicateType { get; set; } = PredicateType.Or;

        public bool IsCaseSensitive { get; set; }

        public FilterBehavior FilterBehavior { get; set; } = FilterBehavior.StringTyped;

        /// <summary>Evaluates one cell value against this predicate.</summary>
        public bool Evaluate(object cellValue)
        {
            switch (FilterType)
            {
                case FilterType.Null:
                    return cellValue == null;
                case FilterType.NotNull:
                    return cellValue != null;
                case FilterType.Empty:
                    return string.IsNullOrEmpty(cellValue?.ToString());
                case FilterType.NotEmpty:
                    return !string.IsNullOrEmpty(cellValue?.ToString());
            }

            if (FilterBehavior == FilterBehavior.StronglyTyped)
                return EvaluateTyped(cellValue);

            return EvaluateString(cellValue);
        }

        private bool EvaluateString(object cellValue)
        {
            var left = cellValue?.ToString() ?? string.Empty;
            var right = FilterValue?.ToString() ?? string.Empty;

            var comparison = IsCaseSensitive
                ? StringComparison.CurrentCulture
                : StringComparison.CurrentCultureIgnoreCase;

            switch (FilterType)
            {
                case FilterType.Equals:
                    return string.Equals(left, right, comparison);
                case FilterType.NotEquals:
                    return !string.Equals(left, right, comparison);
                case FilterType.Contains:
                    return left.IndexOf(right, comparison) >= 0;
                case FilterType.NotContains:
                    return left.IndexOf(right, comparison) < 0;
                case FilterType.StartsWith:
                    return left.StartsWith(right, comparison);
                case FilterType.EndsWith:
                    return left.EndsWith(right, comparison);
                case FilterType.GreaterThan:
                    return string.Compare(left, right, comparison) > 0;
                case FilterType.GreaterThanOrEqual:
                    return string.Compare(left, right, comparison) >= 0;
                case FilterType.LessThan:
                    return string.Compare(left, right, comparison) < 0;
                case FilterType.LessThanOrEqual:
                    return string.Compare(left, right, comparison) <= 0;
                default:
                    return true;
            }
        }

        private bool EvaluateTyped(object cellValue)
        {
            var converted = CoerceToCellType(cellValue, FilterValue);

            if (converted == null && FilterValue != null)
                return false;

            var result = Sorting.SortController.CompareValues(cellValue, converted);

            switch (FilterType)
            {
                case FilterType.Equals:
                    return result == 0;
                case FilterType.NotEquals:
                    return result != 0;
                case FilterType.GreaterThan:
                    return result > 0;
                case FilterType.GreaterThanOrEqual:
                    return result >= 0;
                case FilterType.LessThan:
                    return result < 0;
                case FilterType.LessThanOrEqual:
                    return result <= 0;
                default:
                    return EvaluateString(cellValue);
            }
        }

        /// <summary>
        /// Converts the typed filter value into the cell's runtime type. Users type
        /// strings; the column holds decimals or dates.
        /// </summary>
        private static object CoerceToCellType(object cellValue, object filterValue)
        {
            if (filterValue == null || cellValue == null)
                return filterValue;

            var targetType = cellValue.GetType();
            var nullable = Nullable.GetUnderlyingType(targetType);
            if (nullable != null)
                targetType = nullable;

            if (targetType.IsInstanceOfType(filterValue))
                return filterValue;

            try
            {
                return Convert.ChangeType(filterValue, targetType, CultureInfo.CurrentCulture);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>All predicates applied to a single column.</summary>
    public sealed class ColumnFilter
    {
        public ColumnFilter(string mappingName)
        {
            MappingName = mappingName;
        }

        public string MappingName { get; }

        public List<FilterPredicate> Predicates { get; } = new List<FilterPredicate>();

        public bool HasFilter => Predicates.Count > 0;

        /// <summary>
        /// Combines predicates left to right. The first predicate's
        /// <see cref="FilterPredicate.PredicateType"/> is ignored, since there is
        /// nothing to its left.
        /// </summary>
        public bool Evaluate(object cellValue)
        {
            if (Predicates.Count == 0)
                return true;

            var result = Predicates[0].Evaluate(cellValue);

            for (var i = 1; i < Predicates.Count; i++)
            {
                var predicate = Predicates[i];
                var current = predicate.Evaluate(cellValue);

                result = predicate.PredicateType == PredicateType.And
                    ? result && current
                    : result || current;
            }

            return result;
        }
    }

    /// <summary>One entry in the checkbox list of the filter popup.</summary>
    public sealed class FilterElement : INotifyPropertyChanged
    {
        private bool _isSelected = true;

        public FilterElement(object value, string displayText, int occurrences)
        {
            Value = value;
            DisplayText = displayText;
            Occurrences = occurrences;
        }

        public object Value { get; }

        public string DisplayText { get; }

        public int Occurrences { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class FilterChangedEventArgs : EventArgs
    {
        public FilterChangedEventArgs(string columnName, bool isCleared)
        {
            ColumnName = columnName;
            IsCleared = isCleared;
        }

        public string ColumnName { get; }

        public bool IsCleared { get; }
    }
}
