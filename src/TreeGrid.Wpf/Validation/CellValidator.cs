using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using TreeGrid.Wpf.Data;

namespace TreeGrid.Wpf.Validation
{
    public enum GridValidationMode
    {
        None,

        /// <summary>Validate the edited cell when the edit commits.</summary>
        Cell,

        /// <summary>Validate every column of the row when the row loses focus.</summary>
        Row,

        /// <summary>Both.</summary>
        CellAndRow
    }

    public sealed class CellValidatingEventArgs : CancelEventArgs
    {
        public CellValidatingEventArgs(TreeNode node, string mappingName, object oldValue, object newValue)
        {
            Node = node;
            MappingName = mappingName;
            OldValue = oldValue;
            NewValue = newValue;
        }

        public TreeNode Node { get; }

        public string MappingName { get; }

        public object OldValue { get; }

        public object NewValue { get; }

        /// <summary>Set alongside Cancel to explain the rejection.</summary>
        public string ErrorMessage { get; set; }
    }

    public sealed class RowValidatingEventArgs : CancelEventArgs
    {
        public RowValidatingEventArgs(TreeNode node)
        {
            Node = node;
            ErrorMessages = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public TreeNode Node { get; }

        public object Item => Node?.Item;

        /// <summary>Per-column messages, keyed by mapping name.</summary>
        public IDictionary<string, string> ErrorMessages { get; }

        public string ErrorMessage { get; set; }
    }

    public sealed class ValidationResultInfo
    {
        public ValidationResultInfo(bool isValid, string errorMessage)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }

        public bool IsValid { get; }

        public string ErrorMessage { get; }

        public static ValidationResultInfo Valid { get; } = new ValidationResultInfo(true, null);
    }

    /// <summary>
    /// Collects errors from every validation source the data item might implement.
    /// <para>
    /// The sources disagree about timing: DataAnnotations validate a candidate value
    /// before it is written, while IDataErrorInfo and INotifyDataErrorInfo can only
    /// report on a value already assigned. The grid therefore runs annotations first
    /// and only consults the interfaces after a successful write, rolling back if
    /// they object.
    /// </para>
    /// </summary>
    public static class CellValidator
    {
        /// <summary>Validates a candidate value without assigning it.</summary>
        public static ValidationResultInfo ValidateCandidate(object item, string propertyName, object value)
        {
            if (item == null || string.IsNullOrEmpty(propertyName))
                return ValidationResultInfo.Valid;

            var property = PropertyAccessor.GetPropertyInfo(item.GetType(), propertyName);
            if (property == null)
                return ValidationResultInfo.Valid;

            var attributes = property.GetCustomAttributes<ValidationAttribute>(true).ToList();
            if (attributes.Count == 0)
                return ValidationResultInfo.Valid;

            var context = new ValidationContext(item) { MemberName = propertyName };

            foreach (var attribute in attributes)
            {
                ValidationResult result;

                try
                {
                    result = attribute.GetValidationResult(value, context);
                }
                catch (Exception ex)
                {
                    // A validator that throws is itself a validation failure; surfacing
                    // the message beats tearing down the edit session.
                    return new ValidationResultInfo(false, ex.Message);
                }

                if (result != null && result != ValidationResult.Success)
                    return new ValidationResultInfo(false, result.ErrorMessage);
            }

            return ValidationResultInfo.Valid;
        }

        /// <summary>Reads errors reported by the item for a property already assigned.</summary>
        public static ValidationResultInfo ValidateAssigned(object item, string propertyName)
        {
            if (item == null || string.IsNullOrEmpty(propertyName))
                return ValidationResultInfo.Valid;

            if (item is IDataErrorInfo dataErrorInfo)
            {
                var error = dataErrorInfo[propertyName];
                if (!string.IsNullOrEmpty(error))
                    return new ValidationResultInfo(false, error);
            }

            if (item is INotifyDataErrorInfo notifyDataErrorInfo)
            {
                var errors = notifyDataErrorInfo.GetErrors(propertyName);
                var message = FirstMessage(errors);

                if (!string.IsNullOrEmpty(message))
                    return new ValidationResultInfo(false, message);
            }

            return ValidationResultInfo.Valid;
        }

        /// <summary>Validates every mapped column of a row.</summary>
        public static Dictionary<string, string> ValidateRow(object item, IEnumerable<string> propertyNames)
        {
            var errors = new Dictionary<string, string>(StringComparer.Ordinal);

            if (item == null)
                return errors;

            foreach (var propertyName in propertyNames)
            {
                if (string.IsNullOrEmpty(propertyName))
                    continue;

                var value = PropertyAccessor.GetValue(item, propertyName);

                var candidate = ValidateCandidate(item, propertyName, value);
                if (!candidate.IsValid)
                {
                    errors[propertyName] = candidate.ErrorMessage;
                    continue;
                }

                var assigned = ValidateAssigned(item, propertyName);
                if (!assigned.IsValid)
                    errors[propertyName] = assigned.ErrorMessage;
            }

            // IDataErrorInfo.Error carries a row-level message distinct from any column.
            if (item is IDataErrorInfo rowError && !string.IsNullOrEmpty(rowError.Error))
                errors[string.Empty] = rowError.Error;

            return errors;
        }

        private static string FirstMessage(IEnumerable errors)
        {
            if (errors == null)
                return null;

            foreach (var error in errors)
            {
                if (error == null)
                    continue;

                return error is ValidationResult result ? result.ErrorMessage : error.ToString();
            }

            return null;
        }

        /// <summary>
        /// Converts an edited value to the property's type. Editors hand back strings;
        /// the property may want a decimal, an enum or a nullable date.
        /// </summary>
        public static bool TryCoerce(object item, string propertyName, object value, out object coerced, out string error)
        {
            coerced = value;
            error = null;

            var property = PropertyAccessor.GetPropertyInfo(item?.GetType(), propertyName);
            if (property == null)
                return true;

            var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            if (value == null)
            {
                var isNullable = !property.PropertyType.IsValueType ||
                                 Nullable.GetUnderlyingType(property.PropertyType) != null;

                if (isNullable)
                    return true;

                error = $"{propertyName} cannot be empty.";
                return false;
            }

            if (targetType.IsInstanceOfType(value))
                return true;

            try
            {
                coerced = targetType.IsEnum
                    ? Enum.Parse(targetType, value.ToString(), true)
                    : Convert.ChangeType(value, targetType, System.Globalization.CultureInfo.CurrentCulture);

                return true;
            }
            catch (Exception)
            {
                error = $"'{value}' is not a valid {targetType.Name}.";
                return false;
            }
        }
    }
}
