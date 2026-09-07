using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using TreeGrid.Wpf.Columns;
using TreeGrid.Wpf.Data;
using TreeGrid.Wpf.Validation;

namespace TreeGrid.Wpf.Editing
{
    public enum EditTrigger
    {
        /// <summary>Editing must be started in code.</summary>
        None,

        OnTap,

        OnDoubleTap,

        /// <summary>A printable keypress on the current cell opens the editor.</summary>
        OnKeyPress
    }

    public sealed class CellBeginEditEventArgs : CancelEventArgs
    {
        public CellBeginEditEventArgs(TreeNode node, TreeGridColumn column)
        {
            Node = node;
            Column = column;
        }

        public TreeNode Node { get; }

        public TreeGridColumn Column { get; }
    }

    public sealed class CellEndEditEventArgs : EventArgs
    {
        public CellEndEditEventArgs(TreeNode node, TreeGridColumn column, object oldValue, object newValue, bool committed)
        {
            Node = node;
            Column = column;
            OldValue = oldValue;
            NewValue = newValue;
            IsCommitted = committed;
        }

        public TreeNode Node { get; }

        public TreeGridColumn Column { get; }

        public object OldValue { get; }

        public object NewValue { get; }

        public bool IsCommitted { get; }
    }

    /// <summary>
    /// Owns the edit session: which cell is open, what the value was, and how a commit
    /// or rollback is performed.
    /// <para>
    /// Row-level transactions come from <see cref="IEditableObject"/>. The row's
    /// BeginEdit is called when editing first enters a row and EndEdit when focus
    /// leaves it, so a multi-cell edit rolls back as a unit on Escape.
    /// </para>
    /// </summary>
    public sealed class EditController
    {
        private TreeNode _editingNode;
        private TreeGridColumn _editingColumn;
        private FrameworkElement _editElement;
        private object _originalValue;

        private TreeNode _transactionNode;
        private readonly Dictionary<TreeNode, Dictionary<string, string>> _rowErrors
            = new Dictionary<TreeNode, Dictionary<string, string>>();

        public bool IsEditing => _editingNode != null;

        public TreeNode EditingNode => _editingNode;

        public TreeGridColumn EditingColumn => _editingColumn;

        public FrameworkElement EditElement => _editElement;

        public GridValidationMode ValidationMode { get; set; } = GridValidationMode.Cell;

        public event EventHandler<CellBeginEditEventArgs> CellBeginEdit;

        public event EventHandler<CellEndEditEventArgs> CellEndEdit;

        public event EventHandler<CellValidatingEventArgs> CellValidating;

        public event EventHandler<RowValidatingEventArgs> RowValidating;

        /// <summary>Raised when a cell's error state changes so the view can repaint.</summary>
        public event EventHandler<TreeNode> ErrorsChanged;

        // ------------------------------------------------------------- errors

        public string GetError(TreeNode node, string mappingName)
        {
            if (node == null || mappingName == null)
                return null;

            return _rowErrors.TryGetValue(node, out var errors) && errors.TryGetValue(mappingName, out var message)
                ? message
                : null;
        }

        public bool HasErrors(TreeNode node) =>
            node != null && _rowErrors.TryGetValue(node, out var errors) && errors.Count > 0;

        private void SetError(TreeNode node, string mappingName, string message)
        {
            if (node == null || mappingName == null)
                return;

            if (!_rowErrors.TryGetValue(node, out var errors))
            {
                if (string.IsNullOrEmpty(message))
                    return;

                errors = new Dictionary<string, string>(StringComparer.Ordinal);
                _rowErrors[node] = errors;
            }

            if (string.IsNullOrEmpty(message))
                errors.Remove(mappingName);
            else
                errors[mappingName] = message;

            if (errors.Count == 0)
                _rowErrors.Remove(node);

            ErrorsChanged?.Invoke(this, node);
        }

        public void ClearErrors(TreeNode node)
        {
            if (node != null && _rowErrors.Remove(node))
                ErrorsChanged?.Invoke(this, node);
        }

        public void ClearAllErrors()
        {
            _rowErrors.Clear();
            ErrorsChanged?.Invoke(this, null);
        }

        // -------------------------------------------------------------- editing

        /// <summary>Opens an editor. Returns the element to host, or null if refused.</summary>
        public FrameworkElement BeginEdit(TreeNode node, TreeGridColumn column)
        {
            if (node?.Item == null || column == null || !column.AllowEditing)
                return null;

            if (IsEditing && !EndEdit(commit: true))
                return null;

            var args = new CellBeginEditEventArgs(node, column);
            CellBeginEdit?.Invoke(this, args);

            if (args.Cancel)
                return null;

            BeginRowTransaction(node);

            _editingNode = node;
            _editingColumn = column;
            _originalValue = column.MappingName == null
                ? null
                : PropertyAccessor.GetValue(node.Item, column.MappingName);

            _editElement = column.CreateEditElement();
            column.PrepareEditElement(_editElement, _originalValue, node.Item);

            return _editElement;
        }

        /// <summary>
        /// Closes the editor. Returns false when a commit was rejected by validation,
        /// in which case the session stays open on the offending cell.
        /// </summary>
        public bool EndEdit(bool commit)
        {
            if (!IsEditing)
                return true;

            var node = _editingNode;
            var column = _editingColumn;
            var element = _editElement;
            var oldValue = _originalValue;

            if (!commit)
            {
                CloseSession();
                SetError(node, column.MappingName, null);
                CellEndEdit?.Invoke(this, new CellEndEditEventArgs(node, column, oldValue, oldValue, false));
                return true;
            }

            if (!column.SupportsValueCommit || string.IsNullOrEmpty(column.MappingName))
            {
                CloseSession();
                CellEndEdit?.Invoke(this, new CellEndEditEventArgs(node, column, oldValue, oldValue, true));
                return true;
            }

            var newValue = column.GetEditValue(element);

            if (!TryCommitValue(node, column, oldValue, newValue, out var error))
            {
                SetError(node, column.MappingName, error);

                // Cell validation keeps the user in the editor; row validation defers.
                if (ValidationMode == GridValidationMode.Cell || ValidationMode == GridValidationMode.CellAndRow)
                    return false;
            }
            else
            {
                SetError(node, column.MappingName, null);
            }

            CloseSession();
            CellEndEdit?.Invoke(this, new CellEndEditEventArgs(node, column, oldValue, newValue, true));
            return true;
        }

        private bool TryCommitValue(TreeNode node, TreeGridColumn column, object oldValue, object newValue,
            out string error)
        {
            error = null;

            if (!CellValidator.TryCoerce(node.Item, column.MappingName, newValue, out var coerced, out error))
                return false;

            var validating = new CellValidatingEventArgs(node, column.MappingName, oldValue, coerced);
            CellValidating?.Invoke(this, validating);

            if (validating.Cancel)
            {
                error = validating.ErrorMessage ?? "Value rejected.";
                return false;
            }

            if (ValidationMode != GridValidationMode.None)
            {
                var candidate = CellValidator.ValidateCandidate(node.Item, column.MappingName, coerced);
                if (!candidate.IsValid)
                {
                    error = candidate.ErrorMessage;
                    return false;
                }
            }

            try
            {
                PropertyAccessor.SetValue(node.Item, column.MappingName, coerced);
            }
            catch (Exception ex)
            {
                error = ex.InnerException?.Message ?? ex.Message;
                return false;
            }

            if (ValidationMode == GridValidationMode.None)
                return true;

            // IDataErrorInfo can only speak once the value is assigned, so a rejection
            // here has to roll the property back.
            var assigned = CellValidator.ValidateAssigned(node.Item, column.MappingName);
            if (!assigned.IsValid)
            {
                error = assigned.ErrorMessage;
                PropertyAccessor.SetValue(node.Item, column.MappingName, oldValue);
                return false;
            }

            return true;
        }

        private void CloseSession()
        {
            _editingNode = null;
            _editingColumn = null;
            _editElement = null;
            _originalValue = null;
        }

        // --------------------------------------------------- row transactions

        private void BeginRowTransaction(TreeNode node)
        {
            if (ReferenceEquals(_transactionNode, node))
                return;

            CommitRow();

            _transactionNode = node;
            (node.Item as IEditableObject)?.BeginEdit();
        }

        /// <summary>
        /// Ends the row transaction. Returns false when row validation rejected it, in
        /// which case the transaction stays open.
        /// </summary>
        public bool CommitRow()
        {
            if (_transactionNode == null)
                return true;

            var node = _transactionNode;

            if (ValidationMode == GridValidationMode.Row || ValidationMode == GridValidationMode.CellAndRow)
            {
                var args = new RowValidatingEventArgs(node);
                RowValidating?.Invoke(this, args);

                if (args.Cancel)
                {
                    foreach (var pair in args.ErrorMessages)
                        SetError(node, pair.Key, pair.Value);

                    if (args.ErrorMessages.Count == 0 && !string.IsNullOrEmpty(args.ErrorMessage))
                        SetError(node, string.Empty, args.ErrorMessage);

                    return false;
                }
            }

            _transactionNode = null;
            (node.Item as IEditableObject)?.EndEdit();
            return true;
        }

        public void CancelRow()
        {
            if (_transactionNode == null)
                return;

            var node = _transactionNode;
            _transactionNode = null;

            (node.Item as IEditableObject)?.CancelEdit();
            ClearErrors(node);
        }

        /// <summary>Runs row validation for a node without ending its transaction.</summary>
        public bool ValidateRow(TreeNode node, IEnumerable<string> mappingNames)
        {
            if (node?.Item == null)
                return true;

            var errors = CellValidator.ValidateRow(node.Item, mappingNames);

            ClearErrors(node);

            foreach (var pair in errors)
                SetError(node, pair.Key, pair.Value);

            return errors.Count == 0;
        }
    }
}
