using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using TreeGrid.Wpf.Data;

namespace TreeGrid.Wpf.Selection
{
    /// <summary>
    /// Owns all selection state. Selection is tracked against <see cref="TreeNode"/>
    /// rather than flat-view row indices, so collapsing an ancestor or re-sorting does
    /// not silently drop the user's selection.
    /// </summary>
    public sealed class SelectionController
    {
        private readonly HashSet<TreeNode> _selected = new HashSet<TreeNode>();
        private readonly ObservableCollection<object> _selectedItems = new ObservableCollection<object>();
        private readonly Func<FlatTreeView> _viewAccessor;
        private readonly Func<int> _columnCountAccessor;

        private TreeNode _anchor;
        private TreeNode _currentNode;
        private int _currentColumn;
        private bool _suppressNotifications;

        public SelectionController(Func<FlatTreeView> viewAccessor, Func<int> columnCountAccessor)
        {
            _viewAccessor = viewAccessor;
            _columnCountAccessor = columnCountAccessor;
            SelectedItems = new ReadOnlyObservableCollection<object>(_selectedItems);
        }

        public GridSelectionMode Mode { get; set; } = GridSelectionMode.Extended;

        public GridSelectionUnit Unit { get; set; } = GridSelectionUnit.Row;

        public ReadOnlyObservableCollection<object> SelectedItems { get; }

        public TreeNode CurrentNode => _currentNode;

        /// <summary>Current cell in flat-view coordinates, recomputed on demand.</summary>
        public GridIndex CurrentCell =>
            _currentNode == null ? GridIndex.Invalid : new GridIndex(_currentNode.FlatIndex, _currentColumn);

        public int CurrentColumnIndex => _currentColumn;

        public object SelectedItem => _selectedItems.Count > 0 ? _selectedItems[0] : null;

        public event EventHandler<GridSelectionChangedEventArgs> SelectionChanged;

        public event EventHandler<CurrentCellChangedEventArgs> CurrentCellChanged;

        private FlatTreeView View => _viewAccessor();

        public bool IsSelected(TreeNode node) => node != null && _selected.Contains(node);

        /// <summary>
        /// Group headers are selectable so they can be highlighted and navigated, but
        /// they carry no data item. Adding their null Item to SelectedItems would put
        /// nulls in a collection the host enumerates, so they are tracked as nodes only.
        /// </summary>
        private static bool TracksItem(TreeNode node) => node?.Item != null;

        // ------------------------------------------------------------- pointer

        public void HandlePointerDown(int rowIndex, int columnIndex, ModifierKeys modifiers)
        {
            var view = View;
            if (view == null || rowIndex < 0 || rowIndex >= view.Count)
                return;

            var node = view[rowIndex];

            if (Mode == GridSelectionMode.None)
            {
                SetCurrent(node, columnIndex);
                return;
            }

            var ctrl = (modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            var shift = (modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            switch (Mode)
            {
                case GridSelectionMode.Single:
                    ReplaceSelection(node);
                    _anchor = node;
                    break;

                case GridSelectionMode.Multiple:
                    Toggle(node);
                    _anchor = node;
                    break;

                case GridSelectionMode.Extended:
                    if (shift && _anchor != null)
                        SelectRange(_anchor, node, keepExisting: ctrl);
                    else if (ctrl)
                    {
                        Toggle(node);
                        _anchor = node;
                    }
                    else
                    {
                        ReplaceSelection(node);
                        _anchor = node;
                    }
                    break;
            }

            SetCurrent(node, columnIndex);
        }

        // ------------------------------------------------------------ keyboard

        /// <summary>Returns true when the key was consumed.</summary>
        public bool HandleKey(Key key, ModifierKeys modifiers)
        {
            var view = View;
            if (view == null || view.Count == 0)
                return false;

            var ctrl = (modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            var shift = (modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            var columnCount = Math.Max(1, _columnCountAccessor());
            var current = _currentNode?.FlatIndex ?? -1;

            switch (key)
            {
                case Key.A when ctrl:
                    SelectAll();
                    return true;

                case Key.Down:
                    MoveTo(Math.Min(view.Count - 1, current + 1), shift, ctrl);
                    return true;

                case Key.Up:
                    MoveTo(Math.Max(0, current <= 0 ? 0 : current - 1), shift, ctrl);
                    return true;

                case Key.Home:
                    MoveTo(0, shift, ctrl);
                    return true;

                case Key.End:
                    MoveTo(view.Count - 1, shift, ctrl);
                    return true;

                case Key.Left when Unit == GridSelectionUnit.Cell:
                    SetCurrent(_currentNode, Math.Max(0, _currentColumn - 1));
                    return true;

                case Key.Right when Unit == GridSelectionUnit.Cell:
                    SetCurrent(_currentNode, Math.Min(columnCount - 1, _currentColumn + 1));
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>Page navigation needs the viewport size, so the owner supplies the row delta.</summary>
        public void PageMove(int rowDelta, ModifierKeys modifiers)
        {
            var view = View;
            if (view == null || view.Count == 0)
                return;

            var current = _currentNode?.FlatIndex ?? 0;
            var target = Math.Min(view.Count - 1, Math.Max(0, current + rowDelta));

            MoveTo(target,
                (modifiers & ModifierKeys.Shift) == ModifierKeys.Shift,
                (modifiers & ModifierKeys.Control) == ModifierKeys.Control);
        }

        private void MoveTo(int rowIndex, bool shift, bool ctrl)
        {
            var view = View;
            if (rowIndex < 0 || rowIndex >= view.Count)
                return;

            var node = view[rowIndex];

            if (Mode == GridSelectionMode.Extended && shift && _anchor != null)
                SelectRange(_anchor, node, keepExisting: false);
            else if (Mode != GridSelectionMode.None && !ctrl)
            {
                ReplaceSelection(node);
                _anchor = node;
            }
            else if (ctrl)
            {
                // Ctrl+arrow moves the cursor without disturbing the selection.
                _anchor = node;
            }

            SetCurrent(node, _currentColumn);
        }

        // ------------------------------------------------------- selection ops

        public void SelectAll()
        {
            if (Mode == GridSelectionMode.None || Mode == GridSelectionMode.Single)
                return;

            var view = View;
            var added = new List<object>();

            BeginBatch();

            for (var i = 0; i < view.Count; i++)
            {
                var node = view[i];
                if (!_selected.Add(node))
                    continue;

                node.IsSelected = true;

                if (!TracksItem(node))
                    continue;

                _selectedItems.Add(node.Item);
                added.Add(node.Item);
            }

            EndBatch(added, Array.Empty<object>());
        }

        public void Clear()
        {
            if (_selected.Count == 0)
                return;

            var removed = _selectedItems.ToList();

            BeginBatch();

            foreach (var node in _selected)
                node.IsSelected = false;

            _selected.Clear();
            _selectedItems.Clear();

            EndBatch(Array.Empty<object>(), removed);
        }

        public void Select(TreeNode node, bool clearExisting = true)
        {
            if (node == null || Mode == GridSelectionMode.None)
                return;

            if (clearExisting)
                ReplaceSelection(node);
            else
            {
                BeginBatch();
                var added = new List<object>();

                if (_selected.Add(node))
                {
                    node.IsSelected = true;

                    // Group headers highlight but are not tracked as items.
                    if (TracksItem(node))
                    {
                        _selectedItems.Add(node.Item);
                        added.Add(node.Item);
                    }
                }

                EndBatch(added, Array.Empty<object>());
            }

            _anchor = node;
            SetCurrent(node, _currentColumn);
        }

        public void Deselect(TreeNode node)
        {
            if (node == null || !_selected.Remove(node))
                return;

            BeginBatch();
            node.IsSelected = false;

            if (!TracksItem(node))
            {
                EndBatch(Array.Empty<object>(), Array.Empty<object>());
                return;
            }

            _selectedItems.Remove(node.Item);
            EndBatch(Array.Empty<object>(), new[] { node.Item });
        }

        private void Toggle(TreeNode node)
        {
            if (_selected.Contains(node))
                Deselect(node);
            else
                Select(node, clearExisting: false);
        }

        private void ReplaceSelection(TreeNode node)
        {
            var removed = new List<object>();
            var added = new List<object>();

            BeginBatch();

            foreach (var existing in _selected)
            {
                if (ReferenceEquals(existing, node))
                    continue;

                existing.IsSelected = false;

                if (TracksItem(existing))
                    removed.Add(existing.Item);
            }

            var wasSelected = _selected.Contains(node);

            _selected.Clear();
            _selectedItems.Clear();

            _selected.Add(node);
            node.IsSelected = true;

            if (TracksItem(node))
            {
                _selectedItems.Add(node.Item);

                if (!wasSelected)
                    added.Add(node.Item);
            }

            EndBatch(added, removed);
        }

        private void SelectRange(TreeNode from, TreeNode to, bool keepExisting)
        {
            var view = View;
            var start = from.FlatIndex;
            var end = to.FlatIndex;

            // The anchor may have been collapsed away since it was set.
            if (start < 0)
            {
                ReplaceSelection(to);
                _anchor = to;
                return;
            }

            if (start > end)
                (start, end) = (end, start);

            var added = new List<object>();
            var removed = new List<object>();

            BeginBatch();

            if (!keepExisting)
            {
                foreach (var existing in _selected)
                {
                    var index = existing.FlatIndex;
                    if (index < start || index > end)
                    {
                        existing.IsSelected = false;

                        if (TracksItem(existing))
                            removed.Add(existing.Item);
                    }
                }

                _selected.RemoveWhere(n => n.FlatIndex < start || n.FlatIndex > end);

                // Rebuilding beats calling Remove per item: ObservableCollection.Remove
                // is a linear scan, so dropping k items from n was O(n*k) - which bites
                // hard on a shift-click across tens of thousands of rows.
                if (removed.Count > 0)
                {
                    _selectedItems.Clear();

                    foreach (var node in _selected)
                    {
                        if (TracksItem(node))
                            _selectedItems.Add(node.Item);
                    }
                }
            }

            for (var i = start; i <= end; i++)
            {
                var node = view[i];
                if (!_selected.Add(node))
                    continue;

                node.IsSelected = true;

                if (!TracksItem(node))
                    continue;

                _selectedItems.Add(node.Item);
                added.Add(node.Item);
            }

            EndBatch(added, removed);
        }

        // ---------------------------------------------------------- current cell

        public void SetCurrent(TreeNode node, int columnIndex)
        {
            var columnCount = Math.Max(1, _columnCountAccessor());
            var column = Math.Min(Math.Max(0, columnIndex), columnCount - 1);

            if (ReferenceEquals(node, _currentNode) && column == _currentColumn)
                return;

            var oldIndex = CurrentCell;
            _currentNode = node;
            _currentColumn = column;

            CurrentCellChanged?.Invoke(this, new CurrentCellChangedEventArgs(oldIndex, CurrentCell, node));
        }

        public bool IsCurrentCell(TreeNode node, int columnIndex) =>
            Unit == GridSelectionUnit.Cell &&
            ReferenceEquals(node, _currentNode) &&
            columnIndex == _currentColumn;

        // ------------------------------------------------------------- reload

        /// <summary>
        /// Re-attaches selection after the node tree is rebuilt. Nodes are new objects
        /// after a reload, so we remap by data item.
        /// </summary>
        public void Restore(Func<object, TreeNode> nodeLookup)
        {
            if (_selectedItems.Count == 0 && _currentNode == null)
                return;

            var items = _selectedItems.ToList();
            var currentItem = _currentNode?.Item;
            var anchorItem = _anchor?.Item;

            _selected.Clear();

            BeginBatch();
            _selectedItems.Clear();

            foreach (var item in items)
            {
                var node = nodeLookup(item);
                if (node == null)
                    continue;

                node.IsSelected = true;
                _selected.Add(node);
                _selectedItems.Add(item);
            }

            _currentNode = currentItem == null ? null : nodeLookup(currentItem);
            _anchor = anchorItem == null ? null : nodeLookup(anchorItem);

            var removed = items.Where(i => !_selectedItems.Contains(i)).ToList();
            EndBatch(Array.Empty<object>(), removed);
        }

        // ------------------------------------------------------------ batching

        private void BeginBatch() => _suppressNotifications = true;

        private void EndBatch(IReadOnlyList<object> added, IReadOnlyList<object> removed)
        {
            _suppressNotifications = false;

            if (added.Count == 0 && removed.Count == 0)
                return;

            SelectionChanged?.Invoke(this, new GridSelectionChangedEventArgs(added, removed));
        }
    }
}
