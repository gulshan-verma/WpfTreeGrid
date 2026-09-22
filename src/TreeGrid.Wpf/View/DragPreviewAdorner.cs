using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace TreeGrid.Wpf.View
{
    /// <summary>
    /// Draws a translucent copy of whatever is being dragged, following the pointer,
    /// plus an optional insertion line.
    /// <para>
    /// The ghost is a <see cref="VisualBrush"/> of the live element rather than a
    /// rendered bitmap, so it costs nothing to build and always matches the current
    /// theme. It is not hit-test visible, so it cannot swallow the drag it is
    /// illustrating.
    /// </para>
    /// </summary>
    public sealed class DragPreviewAdorner : Adorner
    {
        private readonly Brush _ghostBrush;
        private readonly Size _ghostSize;
        private readonly Pen _outlinePen;
        private readonly Pen _insertionPen;
        private readonly Brush _insertionBrush;

        private Point _position = new Point(double.NaN, double.NaN);
        private double _insertionX = double.NaN;
        private double _insertionHeight;

        public DragPreviewAdorner(UIElement adornedElement, FrameworkElement source,
            Brush accentBrush, double ghostOpacity = 0.7)
            : base(adornedElement)
        {
            IsHitTestVisible = false;
            GhostOpacity = ghostOpacity;

            if (source != null && source.ActualWidth > 0 && source.ActualHeight > 0)
            {
                _ghostSize = new Size(source.ActualWidth, source.ActualHeight);

                // Deliberately not frozen: the brush wraps a live element, so its
                // Visual is mutable and attached to a tree. Freeze() throws here.
                _ghostBrush = new VisualBrush(source)
                {
                    Stretch = Stretch.None,
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top,
                    Opacity = ghostOpacity
                };
            }

            var accent = accentBrush ?? Brushes.DodgerBlue;

            // The accent brush comes from the host and may carry a binding, which
            // would make the pen unfreezable too.
            _outlinePen = new Pen(accent, 1).FreezeIfPossible();
            _insertionPen = new Pen(accent, 2).FreezeIfPossible();

            _insertionBrush = accent;
        }

        public double GhostOpacity { get; }

        /// <summary>Pointer offset inside the source, so the ghost does not jump on grab.</summary>
        public Point GrabOffset { get; set; }

        public bool HasGhost => _ghostBrush != null;

        /// <summary>Moves the ghost. Point is in the adorned element's coordinates.</summary>
        public void UpdatePosition(Point position)
        {
            if (_position.Equals(position))
                return;

            _position = position;
            InvalidateVisual();
        }

        /// <summary>Shows a vertical drop line. Pass NaN to hide it.</summary>
        public void SetInsertion(double x, double height)
        {
            if (_insertionX.Equals(x) && Math.Abs(_insertionHeight - height) < 0.5)
                return;

            _insertionX = x;
            _insertionHeight = height;
            InvalidateVisual();
        }

        public void ClearInsertion() => SetInsertion(double.NaN, 0);

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            // Insertion line first, so the ghost floats above it.
            if (!double.IsNaN(_insertionX) && _insertionHeight > 0)
            {
                var x = Math.Round(_insertionX) + 0.5;

                drawingContext.DrawLine(_insertionPen, new Point(x, 0), new Point(x, _insertionHeight));

                var cap = new StreamGeometry();
                using (var ctx = cap.Open())
                {
                    ctx.BeginFigure(new Point(x - 4, 0), true, true);
                    ctx.LineTo(new Point(x + 4, 0), true, false);
                    ctx.LineTo(new Point(x, 5), true, false);
                }

                cap.FreezeIfPossible();
                drawingContext.DrawGeometry(_insertionBrush, null, cap);
            }

            if (_ghostBrush == null || double.IsNaN(_position.X))
                return;

            var origin = new Point(_position.X - GrabOffset.X, _position.Y - GrabOffset.Y);
            var bounds = new Rect(origin, _ghostSize);

            drawingContext.DrawRectangle(_ghostBrush, _outlinePen, bounds);
        }
    }
}
