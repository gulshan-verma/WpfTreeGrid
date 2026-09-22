using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using TreeGrid.Wpf.Columns;
using TreeGrid.Wpf.Data;
using TreeGrid.Wpf.Localization;

namespace TreeGrid.Wpf.ContextMenus
{
    public enum GridRegion
    {
        None,
        Record,
        Header,
        Expander
    }

    /// <summary>Base for the object set as a context menu's DataContext.</summary>
    public abstract class GridContextMenuInfo
    {
        protected GridContextMenuInfo(TreeGridControl grid)
        {
            TreeGrid = grid;
        }

        public TreeGridControl TreeGrid { get; }
    }

    public sealed class RecordContextMenuInfo : GridContextMenuInfo
    {
        public RecordContextMenuInfo(TreeGridControl grid, TreeNode node, TreeGridColumn column)
            : base(grid)
        {
            Node = node;
            Column = column;
        }

        public TreeNode Node { get; }

        public object Record => Node?.Item;

        public TreeGridColumn Column { get; }
    }

    public sealed class HeaderContextMenuInfo : GridContextMenuInfo
    {
        public HeaderContextMenuInfo(TreeGridControl grid, TreeGridColumn column) : base(grid)
        {
            Column = column;
        }

        public TreeGridColumn Column { get; }
    }

    public sealed class ExpanderContextMenuInfo : GridContextMenuInfo
    {
        public ExpanderContextMenuInfo(TreeGridControl grid, TreeNode node) : base(grid)
        {
            Node = node;
        }

        public TreeNode Node { get; }
    }

    public sealed class GridContextMenuOpeningEventArgs : CancelEventArgs
    {
        public GridContextMenuOpeningEventArgs(GridRegion region, GridContextMenuInfo info, ContextMenu menu)
        {
            Region = region;
            Info = info;
            ContextMenu = menu;
        }

        public GridRegion Region { get; }

        public GridContextMenuInfo Info { get; }

        /// <summary>Replace this to substitute a different menu for this one opening.</summary>
        public ContextMenu ContextMenu { get; set; }
    }

    /// <summary>
    /// Builds the stock menus used when the host has not supplied its own.
    /// <para>
    /// Items are rebuilt each time rather than cached, because nearly every entry is
    /// state-dependent: sort direction, whether a filter is applied, whether the
    /// column is already frozen.
    /// </para>
    /// </summary>
    public static class DefaultContextMenus
    {
        /// <summary>
        /// Appends host-supplied items to a stock menu.
        /// <para>
        /// An item can only have one logical parent, and the stock menus are rebuilt on
        /// every right-click, so each item is detached from the previous menu before
        /// being added again. Without that, the second right-click throws.
        /// </para>
        /// </summary>
        private static void AppendCustomItems(ContextMenu menu, IEnumerable<FrameworkElement> items)
        {
            if (items == null)
                return;

            var added = false;

            foreach (var item in items)
            {
                if (item == null)
                    continue;

                if (LogicalTreeHelper.GetParent(item) is ItemsControl previous)
                    previous.Items.Remove(item);

                if (!added)
                {
                    if (menu.Items.Count > 0)
                        menu.Items.Add(new Separator());

                    added = true;
                }

                menu.Items.Add(item);
            }
        }

        public static ContextMenu BuildHeaderMenu(TreeGridControl grid, TreeGridColumn column)
        {
            var menu = new ContextMenu();

            if (grid.AllowSorting && column.AllowSorting)
            {
                menu.Items.Add(CreateItem(TreeGridLocalization.GetString("SortAscending"),
                    () => grid.SortColumn(column.MappingName, ListSortDirection.Ascending)));

                menu.Items.Add(CreateItem(TreeGridLocalization.GetString("SortDescending"),
                    () => grid.SortColumn(column.MappingName, ListSortDirection.Descending)));

                menu.Items.Add(CreateItem(TreeGridLocalization.GetString("ClearSorting"),
                    grid.ClearSorting));

                menu.Items.Add(new Separator());
            }

            if (grid.AllowFiltering && column.AllowFiltering &&
                grid.FilterController.IsFiltered(column.MappingName))
            {
                menu.Items.Add(CreateItem(TreeGridLocalization.GetString("ClearFilter"),
                    () => grid.ClearFilter(column.MappingName)));

                menu.Items.Add(CreateItem(TreeGridLocalization.GetString("ClearAllFilters"),
                    grid.ClearFilters));

                menu.Items.Add(new Separator());
            }

            if (grid.AllowGrouping)
            {
                // Toggles the panel without touching the grouping itself, so any
                // active grouping survives hiding and showing the box.
                var groupBox = new MenuItem
                {
                    Header = TreeGridLocalization.GetString("GroupByBox"),
                    IsCheckable = true,
                    IsChecked = grid.ShowGroupDropArea,
                    StaysOpenOnClick = false
                };

                groupBox.Click += (s, e) => grid.ShowGroupDropArea = groupBox.IsChecked;
                menu.Items.Add(groupBox);
                menu.Items.Add(new Separator());
            }

            if (grid.AllowGrouping && !string.IsNullOrEmpty(column.MappingName))
            {
                var groupIndex = grid.GroupColumnDescriptions.IndexOfColumn(column.MappingName);

                if (groupIndex < 0)
                {
                    if (column.AllowGrouping)
                    {
                        menu.Items.Add(CreateItem(TreeGridLocalization.GetString("GroupByColumn"),
                            () => grid.GroupByColumn(column.MappingName)));
                    }
                }
                else
                {
                    menu.Items.Add(CreateItem(TreeGridLocalization.GetString("UngroupColumn"),
                        () => grid.UngroupColumn(column.MappingName)));

                    // Only worth offering when there is another level to move past.
                    if (groupIndex > 0)
                    {
                        menu.Items.Add(CreateItem(TreeGridLocalization.GetString("MoveGroupUp"),
                            () => grid.MoveGroup(groupIndex, groupIndex - 1)));
                    }

                    if (groupIndex < grid.GroupColumnDescriptions.Count - 1)
                    {
                        menu.Items.Add(CreateItem(TreeGridLocalization.GetString("MoveGroupDown"),
                            () => grid.MoveGroup(groupIndex, groupIndex + 1)));
                    }
                }

                if (grid.IsGrouped)
                {
                    menu.Items.Add(CreateItem(TreeGridLocalization.GetString("ExpandAllGroups"),
                        grid.ExpandAllGroups));
                    menu.Items.Add(CreateItem(TreeGridLocalization.GetString("CollapseAllGroups"),
                        grid.CollapseAllGroups));
                    menu.Items.Add(CreateItem(TreeGridLocalization.GetString("ClearGrouping"),
                        grid.ClearGrouping));
                }

                menu.Items.Add(new Separator());
            }

            // Hierarchy commands: columns do not expand, nodes do.
            menu.Items.Add(CreateItem(TreeGridLocalization.GetString("ExpandAll"),
                () => grid.ExpandAll()));

            menu.Items.Add(CreateItem(TreeGridLocalization.GetString("CollapseAll"),
                grid.CollapseAll));

            menu.Items.Add(new Separator());

            menu.Items.Add(CreateItem(TreeGridLocalization.GetString("AutoFit"),
                () => grid.AutoFitColumn(column)));

            menu.Items.Add(CreateItem(TreeGridLocalization.GetString("AutoFitAll"),
                grid.AutoFitColumns));

            menu.Items.Add(new Separator());

            var columnIndex = grid.GetColumnIndex(column);

            menu.Items.Add(CreateItem(TreeGridLocalization.GetString("FreezeHere"),
                () => grid.FrozenColumnCount = columnIndex + 1));

            if (grid.FrozenColumnCount > 0)
            {
                menu.Items.Add(CreateItem(TreeGridLocalization.GetString("Unfreeze"),
                    () => grid.FrozenColumnCount = 0));
            }

            if (grid.AllowHidingColumns)
            {
                menu.Items.Add(CreateItem(TreeGridLocalization.GetString("HideColumn"),
                    () => column.IsHidden = true));
            }

            AppendCustomItems(menu, grid.HeaderContextMenuItems);

            return menu;
        }

        public static ContextMenu BuildRecordMenu(TreeGridControl grid, TreeNode node)
        {
            var menu = new ContextMenu();

            menu.Items.Add(CreateItem(TreeGridLocalization.GetString("Copy"), () => grid.Copy()));

            if (grid.AllowEditing)
            {
                menu.Items.Add(CreateItem(TreeGridLocalization.GetString("Cut"), () => grid.Cut()));
                menu.Items.Add(CreateItem(TreeGridLocalization.GetString("Paste"), () => grid.Paste()));
            }

            if (node != null && node.HasChildNodes)
            {
                menu.Items.Add(new Separator());

                menu.Items.Add(CreateItem(
                    node.IsExpanded
                        ? TreeGridLocalization.GetString("Collapse")
                        : TreeGridLocalization.GetString("Expand"),
                    () => _ = grid.ToggleNodeAsync(node)));
            }

            AppendCustomItems(menu, grid.RecordContextMenuItems);

            return menu;
        }

        public static ContextMenu BuildExpanderMenu(TreeGridControl grid, TreeNode node)
        {
            var menu = new ContextMenu();

            menu.Items.Add(CreateItem(TreeGridLocalization.GetString("ExpandAll"),
                () => grid.ExpandAll()));

            menu.Items.Add(CreateItem(TreeGridLocalization.GetString("CollapseAll"),
                grid.CollapseAll));

            AppendCustomItems(menu, grid.ExpanderContextMenuItems);

            return menu;
        }

        private static MenuItem CreateItem(string header, Action action)
        {
            var item = new MenuItem { Header = header };
            item.Click += (s, e) => action();
            return item;
        }
    }
}
