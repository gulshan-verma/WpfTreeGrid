using System;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;
using System.Windows.Markup;

namespace TreeGrid.Wpf.Localization
{
    /// <summary>
    /// String provider for everything the control shows to a user.
    /// <para>
    /// Built-in English defaults mean the grid works with no setup. Assign
    /// <see cref="ResourceManager"/> to point at satellite assemblies; any key the
    /// resource manager does not carry falls back to the default rather than
    /// rendering an empty label.
    /// </para>
    /// </summary>
    public static class TreeGridLocalization
    {
        private static readonly Dictionary<string, string> Defaults = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SortAscending"] = "Sort A-Z",
            ["SortDescending"] = "Sort Z-A",
            ["ClearSorting"] = "Clear sorting",
            ["ClearFilter"] = "Clear filter",
            ["ClearAllFilters"] = "Clear all filters",
            ["SelectAll"] = "(Select All)",
            ["Search"] = "Search",
            ["Conditions"] = "Conditions",
            ["CaseSensitive"] = "Case sensitive",
            ["And"] = "And",
            ["Or"] = "Or",
            ["Ok"] = "OK",
            ["Cancel"] = "Cancel",
            ["Clear"] = "Clear",
            ["Copy"] = "Copy",
            ["Cut"] = "Cut",
            ["Paste"] = "Paste",
            ["ExpandAll"] = "Expand all",
            ["CollapseAll"] = "Collapse all",
            ["Expand"] = "Expand",
            ["Collapse"] = "Collapse",
            ["NoRecords"] = "No records to display",
            ["NoMatches"] = "No rows match the current filter",
            ["Loading"] = "Loading...",
            ["GroupByColumn"] = "Group by this column",
            ["UngroupColumn"] = "Ungroup this column",
            ["MoveGroupUp"] = "Move group level up",
            ["MoveGroupDown"] = "Move group level down",
            ["ExpandAllGroups"] = "Expand all groups",
            ["CollapseAllGroups"] = "Collapse all groups",
            ["ClearGrouping"] = "Clear all grouping",
            ["AutoFit"] = "Auto-fit this column",
            ["AutoFitAll"] = "Auto-fit all columns",
            ["FreezeHere"] = "Freeze up to this column",
            ["Unfreeze"] = "Unfreeze columns",
            ["HideColumn"] = "Hide this column",
            ["ExportExcel"] = "Export to Excel",
            ["ExportCsv"] = "Export to CSV",
            ["ExportPdf"] = "Export to PDF",
            ["ValidationFailed"] = "The value is not valid.",
            ["RequiredField"] = "This field is required."
        };

        /// <summary>Optional satellite resource source. Keys match those in Defaults.</summary>
        public static ResourceManager ResourceManager { get; set; }

        /// <summary>Culture used for lookups. Defaults to the current UI culture.</summary>
        public static CultureInfo Culture { get; set; }

        /// <summary>Raised when the string source changes so open UI can refresh.</summary>
        public static event EventHandler LocalizationChanged;

        public static string GetString(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            if (ResourceManager != null)
            {
                try
                {
                    var localized = ResourceManager.GetString(key, Culture ?? CultureInfo.CurrentUICulture);

                    if (!string.IsNullOrEmpty(localized))
                        return localized;
                }
                catch (MissingManifestResourceException)
                {
                    // A missing satellite assembly should degrade to English, not crash.
                }
            }

            return Defaults.TryGetValue(key, out var fallback) ? fallback : key;
        }

        /// <summary>Overrides a single string without a resource assembly.</summary>
        public static void SetString(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                return;

            Defaults[key] = value;
            LocalizationChanged?.Invoke(null, EventArgs.Empty);
        }

        public static void NotifyChanged() => LocalizationChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// XAML helper: <c>Content="{loc:Localize SelectAll}"</c>.
    /// </summary>
    public sealed class LocalizeExtension : MarkupExtension
    {
        public LocalizeExtension()
        {
        }

        public LocalizeExtension(string key)
        {
            Key = key;
        }

        [ConstructorArgument("key")]
        public string Key { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider) =>
            TreeGridLocalization.GetString(Key);
    }
}
