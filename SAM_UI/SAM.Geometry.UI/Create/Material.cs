using SAM.Core.UI;
using System.Windows.Media;

namespace SAM.Geometry.UI
{
    public static partial class Create
    {
        public static System.Windows.Media.Media3D.Material Material(this System.Drawing.Color color)
        {
            return Material(color.ToMedia());
        }

        public static System.Windows.Media.Media3D.Material Material(this Color color)
        {
            SolidColorBrush brush = new SolidColorBrush(color);

            System.Windows.Media.Media3D.DiffuseMaterial result = new System.Windows.Media.Media3D.DiffuseMaterial(brush);

            // Freeze the immutable material so WPF stops change-tracking it and can share/optimise it
            // across the thousands of 3D visuals (issue #16). Selection/highlight build new materials
            // rather than mutate this one, so freezing is safe; guard in case a caller needs it mutable.
            if (result.CanFreeze)
            {
                result.Freeze();
            }

            return result;
        }

        public static System.Windows.Media.Media3D.Material Material(this Color color, double opacity)
        {
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Opacity = opacity;

            System.Windows.Media.Media3D.DiffuseMaterial result = new System.Windows.Media.Media3D.DiffuseMaterial(brush);

            // See note above (issue #16): freeze the immutable material.
            if (result.CanFreeze)
            {
                result.Freeze();
            }

            return result;
        }

        public static System.Windows.Media.Media3D.Material Material(this System.Drawing.Color color, double opacity)
        {
            return Material(color.ToMedia(), opacity);
        }
    }
}
