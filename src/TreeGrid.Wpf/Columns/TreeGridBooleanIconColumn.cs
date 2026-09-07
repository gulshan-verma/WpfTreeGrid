using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TreeGrid.Wpf.Columns
{
    /// <summary>
    /// Displays a boolean as an icon rather than a checkbox, and never enters edit mode.
    /// <para>
    /// A checkbox invites clicking; this column is for status you can read but not
    /// change. Defaults are a green tick and a red cross, both overridable - supply a
    /// <see cref="TrueIcon"/>/<see cref="FalseIcon"/> geometry for a different glyph,
    /// or a <see cref="TrueTemplate"/>/<see cref="FalseTemplate"/> for full control.
    /// </para>
    /// </summary>
    public class TreeGridBooleanIconColumn : TreeGridColumn
    {
        // Drawn in a 24x24 box and scaled by the Viewbox, so any custom geometry
        // authored against the same box lines up without extra maths.
        private static readonly Geometry DefaultTrueIcon =
            Geometry.Parse("M 5,12.5 L 10,17.5 L 19,7").GetAsFrozen() as Geometry;

        private static readonly Geometry DefaultFalseIcon =
            Geometry.Parse("M 7,7 L 17,17 M 17,7 L 7,17").GetAsFrozen() as Geometry;

        public static readonly DependencyProperty TrueIconProperty = DependencyProperty.Register(
            nameof(TrueIcon), typeof(Geometry), typeof(TreeGridBooleanIconColumn), new PropertyMetadata(null));

        public static readonly DependencyProperty FalseIconProperty = DependencyProperty.Register(
            nameof(FalseIcon), typeof(Geometry), typeof(TreeGridBooleanIconColumn), new PropertyMetadata(null));

        public static readonly DependencyProperty TrueBrushProperty = DependencyProperty.Register(
            nameof(TrueBrush), typeof(Brush), typeof(TreeGridBooleanIconColumn), new PropertyMetadata(null));

        public static readonly DependencyProperty FalseBrushProperty = DependencyProperty.Register(
            nameof(FalseBrush), typeof(Brush), typeof(TreeGridBooleanIconColumn), new PropertyMetadata(null));

        public static readonly DependencyProperty ShowBackgroundCircleProperty = DependencyProperty.Register(
            nameof(ShowBackgroundCircle), typeof(bool), typeof(TreeGridBooleanIconColumn),
            new PropertyMetadata(true));

        public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(
            nameof(IconSize), typeof(double), typeof(TreeGridBooleanIconColumn), new PropertyMetadata(16d));

        public static readonly DependencyProperty TrueTemplateProperty = DependencyProperty.Register(
            nameof(TrueTemplate), typeof(DataTemplate), typeof(TreeGridBooleanIconColumn),
            new PropertyMetadata(null));

        public static readonly DependencyProperty FalseTemplateProperty = DependencyProperty.Register(
            nameof(FalseTemplate), typeof(DataTemplate), typeof(TreeGridBooleanIconColumn),
            new PropertyMetadata(null));

        public static readonly DependencyProperty NullTemplateProperty = DependencyProperty.Register(
            nameof(NullTemplate), typeof(DataTemplate), typeof(TreeGridBooleanIconColumn),
            new PropertyMetadata(null));

        public static readonly DependencyProperty TrueTextProperty = DependencyProperty.Register(
            nameof(TrueText), typeof(string), typeof(TreeGridBooleanIconColumn), new PropertyMetadata("Yes"));

        public static readonly DependencyProperty FalseTextProperty = DependencyProperty.Register(
            nameof(FalseText), typeof(string), typeof(TreeGridBooleanIconColumn), new PropertyMetadata("No"));

        public static readonly DependencyProperty ShowToolTipProperty = DependencyProperty.Register(
            nameof(ShowToolTip), typeof(bool), typeof(TreeGridBooleanIconColumn), new PropertyMetadata(true));

        public TreeGridBooleanIconColumn()
        {
            TextAlignment = TextAlignment.Center;
        }

        /// <summary>Glyph for true. Author against a 24x24 box. Defaults to a tick.</summary>
        public Geometry TrueIcon
        {
            get => (Geometry)GetValue(TrueIconProperty);
            set => SetValue(TrueIconProperty, value);
        }

        /// <summary>Glyph for false. Author against a 24x24 box. Defaults to a cross.</summary>
        public Geometry FalseIcon
        {
            get => (Geometry)GetValue(FalseIconProperty);
            set => SetValue(FalseIconProperty, value);
        }

        public Brush TrueBrush
        {
            get => (Brush)GetValue(TrueBrushProperty);
            set => SetValue(TrueBrushProperty, value);
        }

        public Brush FalseBrush
        {
            get => (Brush)GetValue(FalseBrushProperty);
            set => SetValue(FalseBrushProperty, value);
        }

        /// <summary>Draws a tinted disc behind the glyph.</summary>
        public bool ShowBackgroundCircle
        {
            get => (bool)GetValue(ShowBackgroundCircleProperty);
            set => SetValue(ShowBackgroundCircleProperty, value);
        }

        public double IconSize
        {
            get => (double)GetValue(IconSizeProperty);
            set => SetValue(IconSizeProperty, value);
        }

        /// <summary>Replaces the whole true visual. DataContext is the data item.</summary>
        public DataTemplate TrueTemplate
        {
            get => (DataTemplate)GetValue(TrueTemplateProperty);
            set => SetValue(TrueTemplateProperty, value);
        }

        public DataTemplate FalseTemplate
        {
            get => (DataTemplate)GetValue(FalseTemplateProperty);
            set => SetValue(FalseTemplateProperty, value);
        }

        /// <summary>Shown for a null value. Nothing is drawn when unset.</summary>
        public DataTemplate NullTemplate
        {
            get => (DataTemplate)GetValue(NullTemplateProperty);
            set => SetValue(NullTemplateProperty, value);
        }

        /// <summary>Screen-reader text and tooltip for true.</summary>
        public string TrueText
        {
            get => (string)GetValue(TrueTextProperty);
            set => SetValue(TrueTextProperty, value);
        }

        public string FalseText
        {
            get => (string)GetValue(FalseTextProperty);
            set => SetValue(FalseTextProperty, value);
        }

        public bool ShowToolTip
        {
            get => (bool)GetValue(ShowToolTipProperty);
            set => SetValue(ShowToolTipProperty, value);
        }

        public override bool HasCustomDisplay => true;

        /// <summary>Read-only by definition; the grid never opens an editor for it.</summary>
        public override bool SupportsValueCommit => false;

        public override string FormatValue(object value)
        {
            var state = ToBool(value);

            // Used for grouping captions, filter lists, clipboard and export, where a
            // glyph is useless and the text is what a person actually wants.
            return state == null ? string.Empty : state.Value ? TrueText : FalseText;
        }

        public override FrameworkElement CreateDisplayElement()
        {
            var host = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            host.Children.Add(new Ellipse
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            });

            host.Children.Add(new Viewbox
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new Path
                {
                    // Without this the Path measures from the origin, so the empty
                    // gutter left of and above the geometry is included in its size
                    // and the Viewbox centres the padded box - which pushes the glyph
                    // down and right inside the disc. Uniform normalises to the
                    // geometry's own bounds, so any custom icon lands centred too.
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeLineJoin = PenLineJoin.Round
                }
            });

            host.Children.Add(new ContentPresenter
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            });

            return host;
        }

        public override void PrepareDisplayElement(FrameworkElement element, object value, object dataItem)
        {
            if (!(element is Grid host) || host.Children.Count < 3)
                return;

            var disc = (Ellipse)host.Children[0];
            var box = (Viewbox)host.Children[1];
            var custom = (ContentPresenter)host.Children[2];
            var glyph = (Path)box.Child;

            var state = ToBool(value);
            var template = state == null ? NullTemplate : state.Value ? TrueTemplate : FalseTemplate;

            host.Width = IconSize + 6;
            host.Height = IconSize + 6;

            // Respect the column's alignment; the default for this column is Center.
            host.HorizontalAlignment = TextAlignment == TextAlignment.Left
                ? HorizontalAlignment.Left
                : TextAlignment == TextAlignment.Right
                    ? HorizontalAlignment.Right
                    : HorizontalAlignment.Center;

            if (template != null)
            {
                custom.ContentTemplate = template;
                custom.Content = dataItem;
                custom.Visibility = Visibility.Visible;
                disc.Visibility = Visibility.Collapsed;
                box.Visibility = Visibility.Collapsed;
            }
            else
            {
                custom.Content = null;
                custom.Visibility = Visibility.Collapsed;

                if (state == null)
                {
                    // No value, no icon - an empty cell reads as "unknown".
                    disc.Visibility = Visibility.Collapsed;
                    box.Visibility = Visibility.Collapsed;
                }
                else
                {
                    var accent = state.Value
                        ? TrueBrush ?? DefaultTrueBrush
                        : FalseBrush ?? DefaultFalseBrush;

                    glyph.Data = state.Value
                        ? TrueIcon ?? DefaultTrueIcon
                        : FalseIcon ?? DefaultFalseIcon;

                    glyph.Stroke = ShowBackgroundCircle ? Brushes.White : accent;
                    glyph.Fill = null;

                    // Uniform stretch scales the stroke with the geometry, so express
                    // it against the 24-unit design box rather than in device pixels.
                    glyph.StrokeThickness = 2.5;

                    box.Width = IconSize * (ShowBackgroundCircle ? 0.62 : 1);
                    box.Height = box.Width;
                    box.Visibility = Visibility.Visible;

                    disc.Fill = accent;
                    disc.Width = IconSize;
                    disc.Height = IconSize;
                    disc.Visibility = ShowBackgroundCircle ? Visibility.Visible : Visibility.Collapsed;
                }
            }

            var text = FormatValue(value);

            // A glyph carries no text, so state it explicitly for screen readers.
            AutomationProperties.SetName(host, text);
            host.ToolTip = ShowToolTip && !string.IsNullOrEmpty(text) ? text : null;
        }

        private static Brush DefaultTrueBrush { get; } = Freeze(Color.FromRgb(0x1A, 0x7F, 0x37));

        private static Brush DefaultFalseBrush { get; } = Freeze(Color.FromRgb(0xD1, 0x24, 0x2F));

        private static Brush Freeze(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        /// <summary>Accepts bools, nullable bools and anything parseable, like the checkbox column.</summary>
        public static bool? ToBool(object value)
        {
            switch (value)
            {
                case null:
                    return null;
                case bool b:
                    return b;
                default:
                    return bool.TryParse(value.ToString(), out var parsed) ? parsed : (bool?)null;
            }
        }
    }
}
