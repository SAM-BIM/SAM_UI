// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.Planar;

namespace SAM.Core.Mollier.UI
{
    public static partial class Convert
    {
        /// <summary>
        /// OxyPlot port of the WinForms Point2D -> DataPoint converter. OxyPlot's DataPoint is a
        /// value type, so a null input is mapped to a NaN point (OxyPlot skips NaN points when rendering).
        /// </summary>
        public static OxyPlot.DataPoint ToChart(this Point2D point2D)
        {
            if (point2D == null)
            {
                return new OxyPlot.DataPoint(double.NaN, double.NaN);
            }

            return new OxyPlot.DataPoint(point2D.X, point2D.Y);
        }
    }
}
