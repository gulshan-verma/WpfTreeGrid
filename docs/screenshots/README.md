# Screenshot checklist

The root README references six images from this folder. Filenames must match
exactly. Nothing else in the README needs editing once they are here.

Run the demo first:

```bash
dotnet run --project samples/TreeGrid.Demo
```

Capture with <kbd>Alt</kbd>+<kbd>PrtScn</kbd> (active window) or Snipping Tool.
Crop to the window; PNG; roughly 1400px wide is plenty.

| File | What to show | How to set it up |
|---|---|---|
| `feature-explorer.png` | The whole window — sidebar plus grid | Default state. Expand two or three nodes so the hierarchy is visible. |
| `filtering.png` | Filter popup open over the grid | Click a header's filter button. Type in the search box so the checklist is filtered, and expand **Conditions** so both halves show. |
| `editing.png` | An open editor and a validation error | Double-click a **Salary** cell, type `999999`, then Tab away. The `[Range]` attribute rejects it — capture the red border and tooltip. |
| `frozen-stacked.png` | Frozen column and stacked headers | Tick **Stacked headers** in the sidebar, set **Frozen columns (left)** to 2, then scroll right so the frozen band clearly holds position. |
| `drag-drop.png` | Drop indicator mid-drag | Tick **Row drag and drop**. Drag a child row over another row and hold — capture while the indented indicator is showing. |
| `dark-theme.png` | The same grid in dark | Tick **Dark theme**. Sidebar and chrome follow the theme too, so capture the whole window. |

## Optional extras

Worth adding if you want the README richer — reference them yourself where they fit.

| File | What to show |
|---|---|
| `sorting.png` | Ctrl-click two headers to get numbered multi-sort badges |
| `load-on-demand.png` | Switch the data source to **Load on demand**, expand a folder, capture the busy indicator |
| `excel-export.png` | Open an exported `.xlsx` in Excel showing the outline grouping controls in the sheet margin |
| `benchmark.png` | The dialog from the **Benchmark** button |
