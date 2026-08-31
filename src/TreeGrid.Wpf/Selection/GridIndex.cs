using System;
using System.Collections.Generic;
using TreeGrid.Wpf.Data;

namespace TreeGrid.Wpf.Selection
{
    public enum GridSelectionMode
    {
        None,

        /// <summary>One item at a time; modifiers are ignored.</summary>
        Single,

        /// <summary>Click toggles; no anchor, no range.</summary>
        Multiple,

        /// <summary>Explorer semantics: plain click replaces, Ctrl toggles, Shift extends from anchor.</summary>
        Extended
    }

    public enum GridSelectionUnit
    {
        Row,
        Cell
    }

    /// <summary>
    /// A row/column coordinate in the flattened view. Row indices are flat-view
    /// indices, so they shift when nodes expand or collapse; anything that needs to
    /// survive a structural change should hold the <see cref="TreeNode"/> instead.
    /// </summary>
    public readonly struct GridIndex : IEquatable<GridIndex>
    {
        public GridIndex(int rowIndex, int columnIndex)
        {
            RowIndex = rowIndex;
            ColumnIndex = columnIndex;
        }

        public int RowIndex { get; }

        public int ColumnIndex { get; }

        public bool IsValid => RowIndex >= 0 && ColumnIndex >= 0;

        public static GridIndex Invalid => new GridIndex(-1, -1);

        public bool Equals(GridIndex other) =>
            RowIndex == other.RowIndex && ColumnIndex == other.ColumnIndex;

        public override bool Equals(object obj) => obj is GridIndex other && Equals(other);

        public override int GetHashCode() => (RowIndex * 397) ^ ColumnIndex;

        public static bool operator ==(GridIndex a, GridIndex b) => a.Equals(b);

        public static bool operator !=(GridIndex a, GridIndex b) => !a.Equals(b);

        public override string ToString() => $"[{RowIndex},{ColumnIndex}]";
    }

    public sealed class GridSelectionChangedEventArgs : EventArgs
    {
        public GridSelectionChangedEventArgs(IReadOnlyList<object> added, IReadOnlyList<object> removed)
        {
            AddedItems = added;
            RemovedItems = removed;
        }

        public IReadOnlyList<object> AddedItems { get; }

        public IReadOnlyList<object> RemovedItems { get; }
    }

    public sealed class CurrentCellChangedEventArgs : EventArgs
    {
        public CurrentCellChangedEventArgs(GridIndex oldIndex, GridIndex newIndex, TreeNode node)
        {
            OldIndex = oldIndex;
            NewIndex = newIndex;
            Node = node;
        }

        public GridIndex OldIndex { get; }

        public GridIndex NewIndex { get; }

        public TreeNode Node { get; }
    }

    public sealed class NodeCheckedEventArgs : EventArgs
    {
        public NodeCheckedEventArgs(TreeNode node, bool? oldState, bool? newState)
        {
            Node = node;
            OldState = oldState;
            NewState = newState;
        }

        public TreeNode Node { get; }

        public bool? OldState { get; }

        public bool? NewState { get; }
    }
}
