# TreeGrid.Wpf

A from-scratch WPF tree grid for .NET 8, built to match the feature set of a
commercial multi-column tree view. Clean-room implementation — no third-party
control assemblies are referenced or decompiled.

**Status: all 8 phases complete.**

## Build

```
dotnet build TreeGrid.sln
dotnet run --project samples/TreeGrid.Demo
```

Requires Windows and the .NET 8 SDK with the desktop workload.

## Projects

| Project | Dependencies |
| --- | --- |
| `TreeGrid.Wpf` | None. The control, plus CSV export. |
| `TreeGrid.Wpf.Export` | ClosedXML, QuestPDF. Optional; reference only if you need xlsx or pdf. |
| `TreeGrid.Demo` | Both. |

The split is deliberate: an application that only needs CSV should not pull two
document libraries into its output.

## What Phase 1 delivers

| Capability | State |
| --- | --- |
| Hierarchical binding (`ChildPropertyName`) | Working |
| Self-relational binding (`IdPropertyName` / `ParentIdPropertyName`) | Working |
| Unbound / load-on-demand, sync and async | Working |
| Flattened view engine with O(subtree) expand/collapse | Working |
| Row virtualization with container recycling | Working |
| Column virtualization with cell recycling | Working |
| Frozen columns, left and right | Layout complete, no UI to set them yet |
| Column sizing modes (Star, LastColumnFill, auto-fit) | Star and LastColumnFill working; auto-fit measured but not yet applied on first pass |
| Single-row selection, arrow-key navigation | Working |
| Alternating rows, grid lines, templated cells | Working |
| Auto-generated columns | Working |
| Expand/collapse events with cancellation | Working |

## What Phase 2 adds

| Capability | State |
| --- | --- |
| Selection modes: None, Single, Multiple, Extended | Working |
| Selection units: Row and Cell, with current-cell tracking | Working |
| Ctrl-click toggle, Shift-click range, Ctrl+A | Working |
| Keyboard: arrows, Home/End, PageUp/PageDown, Shift/Ctrl variants | Working |
| Selection survives collapse, re-sort and source reload | Working |
| Tri-state checkbox column with parent/child cascade | Working |
| Cascade inheritance for load-on-demand children | Working |
| `TreeGridCheckBoxColumn` for bound booleans | Working |
| Column resizing with hover gripper, double-click to auto-fit | Working |
| Column reordering with drop-indicator adorner | Working |
| Auto-fit widths applied on a guarded second pass | Working |

## What Phase 3 adds

| Capability | State |
| --- | --- |
| Level-wise multi-column sorting (siblings sorted within each parent) | Working |
| Header click cycles ascending / descending / unsorted | Working |
| Ctrl-click adds a column to the sort; numbered sort badges | Working |
| Stable sort with source-order restore when unsorted | Working |
| Custom comparers per column (`SortComparers`) | Working |
| Excel-style filter popup: search, value checklist, tri-state Select All | Working |
| Condition filters (14 operators) joined by And/Or, case-sensitivity toggle | Working |
| Strongly-typed comparison so "> 500000" works on numeric columns | Working |
| Four `FilterNodeMode` retention modes | Working |
| Auto-expand surviving nodes after filtering | Working |
| Programmatic `SortColumn`, `ClearSorting`, `FilterPredicate`, `ClearFilters` | Working |
| Sort and filter reapplied to lazily loaded children | Working |

## What Phase 4 adds — editing and validation

| Capability | State |
| --- | --- |
| Column types: Numeric, DateTime, ComboBox, Template, ProgressBar, Hyperlink | Working |
| Edit triggers: None, OnTap, OnDoubleTap, OnKeyPress, plus F2 | Working |
| Enter commits and moves down; Tab traverses editable cells across rows | Working |
| Escape cancels; clicking away commits (blocked if validation refuses) | Working |
| `IEditableObject` row transactions with rollback | Working |
| DataAnnotations validated before the value is written | Working |
| `IDataErrorInfo` / `INotifyDataErrorInfo` validated after write, with rollback | Working |
| `CellValidating` / `RowValidating` events | Working |
| Type coercion from editor strings to the property's type | Working |
| Error visuals: red border, corner triangle, message tooltip | Working |

## What Phase 5 adds — advanced layout

| Capability | State |
| --- | --- |
| Stacked header rows spanning groups of columns | Working |
| Stacked headers follow resize, reorder and freeze | Working |
| Vertical cell merging | **Parked** — implemented but off by default, see below |
| Frozen-pane separator lines, left and right | Working |
| Row drag and drop with Above / Below / Into drop positions | Working |
| Drop indicator adorner, indented to show the intended parent | Working |
| Cycle prevention — a node cannot be dropped into its own subtree | Working |
| `RowDragStarting` / `RowDragOver` / `RowDropped`, all cancellable | Working |

## What Phase 6 adds — shell features

| Capability | State |
| --- | --- |
| Record, header and expander context menus with region hit-testing | Working |
| Stock menus (sort, filter, auto-fit, freeze, hide) when none supplied | Working |
| `GridContextMenuOpening` to substitute or cancel per opening | Working |
| Copy / Cut / Paste with Ctrl+C, Ctrl+X, Ctrl+V | Working |
| Clipboard writes TSV, CSV and CF_HTML so Excel keeps the table shape | Working |
| Paste routed through the same coercion and validation as typed edits | Working |
| Paste modes: Overwrite, FillEmptyOnly, Manual | Working |
| Localization with English defaults and `ResourceManager` override | Working |
| `{loc:Localize Key}` markup extension | Working |
| Right-to-left mirroring of the arrange pass | Working |
| `FluentDark` theme as a brush-only dictionary | Working |

## What Phase 7 adds — export

| Capability | State |
| --- | --- |
| CSV export, no third-party dependency, in the core library | Working |
| Excel export via ClosedXML, with typed cells and auto-filter | Working |
| Excel native row grouping so the tree stays collapsible in the sheet | Working |
| PDF export via QuestPDF, repeating headers and page numbers | Working |
| PDF column widths scaled from the grid's on-screen proportions | Working |
| Row scopes: VisibleRows, AllRows, SelectedRows | Working |
| Hierarchy styles: Indent, LevelColumn, Outline, None | Working |
| `IGridExporter` so hosts can add their own formats | Working |

## What Phase 8 adds — performance

| Fix | Impact |
| --- | --- |
| Quadratic cell realization in `TreeGridRowControl` | `Contains` on a list per column made realization O(columns²). Now a `HashSet`. |
| Quadratic range selection | `ObservableCollection.Remove` per item made shift-click O(n·k). Now rebuilt in one pass. |
| Full reload on every source change | Add/remove/move/replace are now spliced in place, preserving selection, check state, expansion and scroll. |
| No measurement tooling | `GridBenchmark` reports time *and* allocation per operation. |

Allocation is reported alongside time because the failure mode for a virtualized
grid is rarely slow code — it is garbage. Allocating per scroll frame produces
gen-0 collections that read as stutter while the average stays flat.

## Architecture

```
TreeGridControl                 Public surface: DPs, events, template parts
├── TreeGridDataSource          Builds nodes from ItemsSource; owns load-on-demand
│   └── FlatTreeView            Flattened visible projection; expand = list splice
├── SelectionController         Node-keyed selection, current cell, keyboard
├── CheckStateController        Tri-state cascade up and down the hierarchy
├── SortController              Level-wise stable sort of sibling groups
├── FilterController            Predicate evaluation + hierarchy retention modes
├── EditController              Edit sessions, row transactions, error tracking
├── CellMergeController         Vertical merge ranges over the flat view
├── RowDragDropController       Drop resolution and node relocation
├── ClipboardController         TSV / CSV / CF_HTML round-tripping
├── Export/                     IGridExporter + CSV (Excel and PDF are a separate assembly)
├── ColumnLayout                Widths, offsets, frozen bands, visible column range
├── VisualContainer : IScrollInfo
│   └── TreeGridRowControl      Per-row cell layout, frozen bands, clipping
│       ├── TreeGridCell
│       └── TreeGridHeaderCell
└── Themes/Generic.xaml         Templates and theme brushes
```

Two design decisions carry most of the weight:

**The flat view is a single `List<TreeNode>`.** Expanding a node splices its visible
descendants in at one index and reindexes the tail. Nodes cache their own
`FlatIndex`, so "which row is this node?" is O(1) — which the virtualizer needs on
every scroll frame.

**Rows translate their own cells.** The container never applies a horizontal
transform to a row. Each row arranges frozen cells at fixed offsets and shifts only
the middle band by the scroll offset, clipping it so it cannot bleed under the
frozen columns. Freeze panes therefore needs no separate panel tree.

## Roadmap

All eight phases are implemented. Remaining work is listed under known gaps below.


## Cell merging is parked

`AllowMergeCells` defaults to false and the demo no longer exposes it. The
implementation is complete — `CellMergeController`, sibling-restricted ranges,
`QueryCoveredRange` for custom spans, and clamping for ranges scrolled partway off
the top — and the rendering faults found in testing are fixed: merge-owning rows
are lifted above the rows they cover, merged cells paint on an opaque background,
and they measure at full span height.

It is parked because it has had no real use, not because it is known broken. When
merging is off the resolver is detached entirely, so the row control skips its
merge pass and the feature costs nothing per cell.

To re-enable: set `AllowMergeCells="True"`. Nothing else is required.

## Known gaps after Phase 2

- Row height is uniform. Variable-height rows land with cell merging in Phase 5.
- Incremental collection updates cover root-level Add, Remove, Move and Replace in
  hierarchical and unbound modes. Self-relational sources still reload in full,
  because one new row can re-parent an arbitrary part of the tree and a splice
  cannot be proven correct. Nested child-collection changes also reload.
- Auto-fit measures only *realised* rows, so a column fits what has been scrolled
  through rather than the whole column. Measuring every row would defeat
  virtualization; a sampled-fit option is the planned compromise.
- Header click is wired but does nothing until sorting lands in Phase 3.
- Cell-unit selection paints and navigates, but multi-cell rectangular ranges are
  not implemented — Extended mode still selects whole rows.
- The filter value checklist is unavailable for unbound load-on-demand sources.
  Distinct values cannot be enumerated from a tree whose size is unknown, so the
  popup falls back to condition-only filtering there. This is a real constraint of
  lazy loading, not a shortcut.
- Filtering walks the entire materialised node tree on each pass. Fine to ~100k
  nodes; incremental re-filtering is a Phase 8 concern.
- The Or radio button in the condition section is presentation-only — it reads back
  through `IsAndJoin` on the And button, so the pair behaves correctly but the
  binding is one-sided.
- Cell merging is parked (see above). If re-enabled: it is vertical only, and
  horizontal column-span merging is not implemented.
- Row drag and drop moves nodes in the node tree, not in your source collections.
  Handle `RowDropped` and set `HandledByHost` to relocate the underlying data —
  a self-relational source needs a parent-id rewrite, a hierarchical one needs items
  moved between child collections, and the grid cannot guess which.
- Template columns do not commit through the grid (`SupportsValueCommit` is false);
  they edit through their own bindings.
- Stacked headers whose child columns have been reordered apart will span the full
  range between them, including any column that has drifted into the middle.
- RTL mirrors the arrange pass but the filter popup and drag adorners are not
  mirrored; they will open on the LTR side.
- Copy always exports whole rows. Rectangular cell-range copy needs the cell-range
  selection that is itself still outstanding from Phase 2.
- `GridBenchmark.MeasureScroll` drives measure and arrange directly rather than
  through the compositor, so it measures the grid's own per-frame work, not real
  frame times. Use it for regressions, not for absolute figures.
