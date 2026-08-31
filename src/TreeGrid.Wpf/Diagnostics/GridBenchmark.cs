using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TreeGrid.Wpf.Data;

namespace TreeGrid.Wpf.Diagnostics
{
    public sealed class BenchmarkResult
    {
        public string Name { get; set; }

        public int Iterations { get; set; }

        public double TotalMilliseconds { get; set; }

        public double AverageMilliseconds => Iterations == 0 ? 0 : TotalMilliseconds / Iterations;

        public long AllocatedBytes { get; set; }

        public int ItemCount { get; set; }

        public override string ToString() =>
            $"{Name,-28} {AverageMilliseconds,9:F3} ms  {AllocatedBytes / 1024.0,9:F1} KB  n={ItemCount}";
    }

    /// <summary>
    /// Measures the operations that decide whether the grid feels fast.
    /// <para>
    /// Allocation is reported alongside time because the failure mode for a virtualized
    /// grid is usually not slow code, it is garbage: allocating per scroll frame
    /// produces gen-0 collections that show up as stutter rather than as a slow
    /// average.
    /// </para>
    /// </summary>
    public static class GridBenchmark
    {
        public static BenchmarkResult Measure(string name, int iterations, Action action, int itemCount = 0)
        {
            if (iterations <= 0)
                iterations = 1;

            // Warm up so JIT compilation does not land in the measurement.
            action();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var before = GC.GetTotalAllocatedBytes(precise: true);
            var stopwatch = Stopwatch.StartNew();

            for (var i = 0; i < iterations; i++)
                action();

            stopwatch.Stop();
            var after = GC.GetTotalAllocatedBytes(precise: true);

            return new BenchmarkResult
            {
                Name = name,
                Iterations = iterations,
                TotalMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
                AllocatedBytes = after - before,
                ItemCount = itemCount
            };
        }

        /// <summary>Runs the standard suite against a live grid.</summary>
        public static List<BenchmarkResult> RunStandardSuite(TreeGridControl grid)
        {
            var results = new List<BenchmarkResult>();

            if (grid == null)
                return results;

            var view = grid.View;

            results.Add(Measure("Expand all", 1, () => grid.ExpandAll(), view.Count));
            results.Add(Measure("Flat view rebuild", 20, () => view.Rebuild(), view.Count));

            if (view.Count > 0)
            {
                var target = view[Math.Min(view.Count - 1, view.Count / 2)];

                results.Add(Measure("Collapse + expand node", 200, () =>
                {
                    view.Collapse(target);
                    view.Expand(target);
                }, view.CountVisibleDescendants(target)));
            }

            results.Add(Measure("Sort hierarchy", 10, () => grid.ApplySorting(), view.Count));
            results.Add(Measure("Collapse all", 1, grid.CollapseAll, view.Count));

            return results;
        }

        /// <summary>
        /// Times a simulated scroll. Layout runs on the dispatcher, so this measures
        /// the work the grid does per frame, not the frames themselves.
        /// </summary>
        public static BenchmarkResult MeasureScroll(TreeGridControl grid, int frames = 120)
        {
            var container = grid?.Container;

            if (container == null)
                return new BenchmarkResult { Name = "Scroll (no container)" };

            var rowHeight = grid.RowHeight;
            var offset = 0d;

            return Measure("Scroll frame", frames, () =>
            {
                offset += rowHeight;

                if (offset > container.ExtentHeight - container.ViewportHeight)
                    offset = 0;

                container.SetVerticalOffset(offset);
                container.Measure(new System.Windows.Size(container.ViewportWidth, container.ViewportHeight));
                container.Arrange(new System.Windows.Rect(0, 0, container.ViewportWidth, container.ViewportHeight));
            }, grid.View.Count);
        }

        public static string Format(IEnumerable<BenchmarkResult> results)
        {
            var builder = new StringBuilder();

            // Alignment must be a plain integer; positive right-aligns. The widths
            // mirror BenchmarkResult.ToString so the columns line up.
            builder.AppendLine($"{"Operation",-28} {"Avg",9}     {"Alloc",9}");
            builder.AppendLine(new string('-', 68));

            foreach (var result in results)
                builder.AppendLine(result.ToString());

            return builder.ToString();
        }
    }
}
