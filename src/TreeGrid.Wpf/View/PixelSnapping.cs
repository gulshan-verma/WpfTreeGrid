using System;
using System.Windows;
using System.Windows.Media;

namespace TreeGrid.Wpf.View
{
    /// <summary>
    /// Places row and cell edges on whole device pixels.
    /// <para>
    /// Layout rounding rounds an element's position and its size separately, so at display
    /// scaling above 100% a row placed at 37.5px with a height of 37.5px becomes 38 + 38 and
    /// overlaps its neighbour by a pixel. The later sibling then paints over the last pixel of
    /// the earlier one - its bottom / right grid line and the current-cell border. Snapping
    /// both edges and deriving the size from them makes neighbours meet exactly.
    /// </para>
    /// </summary>
    internal static class PixelSnapping
    {
        /// <summary>The span between two snapped edges: (snapped start, snapped length).</summary>
        public static (double start, double length) SnapX(Visual visual, double start, double length)
        {
            var scale = VisualTreeHelper.GetDpi(visual).DpiScaleX;
            return Snap(start, length, scale);
        }

        /// <inheritdoc cref="SnapX"/>
        public static (double start, double length) SnapY(Visual visual, double start, double length)
        {
            var scale = VisualTreeHelper.GetDpi(visual).DpiScaleY;
            return Snap(start, length, scale);
        }

        /// <summary>
        /// A measure constraint rounded down to whole device pixels.
        /// <para>
        /// Layout rounding rounds a desired size up, and an element whose size is larger than
        /// the slot it is arranged into is clipped to that slot - losing its last pixel, where
        /// the bottom / right border is drawn. A snapped slot is never smaller than the length
        /// rounded down, so measuring against it keeps the desired size within the slot.
        /// </para>
        /// </summary>
        public static double FloorX(Visual visual, double length) =>
            Floor(length, VisualTreeHelper.GetDpi(visual).DpiScaleX);

        /// <inheritdoc cref="FloorX"/>
        public static double FloorY(Visual visual, double length) =>
            Floor(length, VisualTreeHelper.GetDpi(visual).DpiScaleY);

        private static double Floor(double length, double scale)
        {
            if (scale <= 0 || double.IsInfinity(length) || double.IsNaN(length))
                return length;

            // The small tolerance keeps an exact pixel multiple (37.9999...) from losing a pixel.
            return Math.Floor(length * scale + 1e-6) / scale;
        }

        private static (double start, double length) Snap(double start, double length, double scale)
        {
            if (scale <= 0)
                return (start, length);

            var snappedStart = Math.Round(start * scale) / scale;
            var snappedEnd = Math.Round((start + length) * scale) / scale;

            return (snappedStart, Math.Max(0, snappedEnd - snappedStart));
        }
    }
}
