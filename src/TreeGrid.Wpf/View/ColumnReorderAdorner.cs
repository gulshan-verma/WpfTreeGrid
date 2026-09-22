using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace TreeGrid.Wpf.View
{
    /// <summary>
    /// Draws the insertion line while a header is being dragged. Lives on the header
    /// host so it is clipped to the header band rather than floating over the rows.
    /// </summary>
    public sealed class ColumnReorderAdorner : Adorner
    {
        private readonly Pen _pen;
        private double _x = -1;

        public ColumnReorderAdorner(UIElement adornedElement, Brush indicatorBrush)
            : base(adornedElement)
        {
            IsHitTestVisible = false;

            _pen = new Pen(indicatorBrush ?? Brushes.DodgerBlue, 2).FreezeIfPossible();
        }

        /// <summary>Horizontal position of the insertion line, in adorned-element coordinates.</summary>
        public double IndicatorX
        {
            get => _x;
            set
            {
                if (System.Math.Abs(_x - value) < 0.5)
                    return;

                _x = value;
                InvalidateVisual();
            }
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            if (_x < 0)
                return;

            var height = AdornedElement.RenderSize.Height;
            var x = System.Math.Round(_x) + 0.5;

            drawingContext.DrawLine(_pen, new Point(x, 0), new Point(x, height));

            // Small triangular caps make the drop point readable at a glance.
            var top = new StreamGeometry();
            using (var ctx = top.Open())
            {
                ctx.BeginFigure(new Point(x - 4, 0), true, true);
                ctx.LineTo(new Point(x + 4, 0), true, false);
                ctx.LineTo(new Point(x, 5), true, false);
            }

            top.FreezeIfPossible();
            drawingContext.DrawGeometry(_pen.Brush, null, top);
        }
    }
}
