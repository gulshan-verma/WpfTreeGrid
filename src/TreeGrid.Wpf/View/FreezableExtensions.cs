using System.Windows;

namespace TreeGrid.Wpf.View
{
    /// <summary>
    /// Freezing is an optimisation, never a requirement, and it throws when the object
    /// cannot be frozen.
    /// <para>
    /// Two cases bite here. A <see cref="System.Windows.Media.VisualBrush"/> built from
    /// a live element can never be frozen, because its Visual is mutable and attached
    /// to a tree. And a Pen wrapping a caller-supplied brush is only freezable if that
    /// brush is - a brush carrying a binding or an animation is not. Guarding on
    /// <see cref="Freezable.CanFreeze"/> keeps the optimisation where it is available
    /// and silently skips it where it is not.
    /// </para>
    /// </summary>
    internal static class FreezableExtensions
    {
        /// <summary>Freezes when allowed and returns the same instance either way.</summary>
        public static T FreezeIfPossible<T>(this T freezable) where T : Freezable
        {
            if (freezable != null && !freezable.IsFrozen && freezable.CanFreeze)
                freezable.Freeze();

            return freezable;
        }
    }
}
