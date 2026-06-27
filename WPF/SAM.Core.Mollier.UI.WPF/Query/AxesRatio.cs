// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using OxyPlot;
using OxyPlot.Axes;
using System.Linq;

namespace SAM.Core.Mollier.UI
{
    public static partial class Query
    {
        /// <summary>
        /// OX-to-OY length ratio used to make the label solver's coordinate space roughly isotropic in
        /// pixels (OxyPlot port). When the plot has been rendered (PlotArea valid) this uses the real
        /// pixel-per-data ratio of the two axes; otherwise it falls back to the WinForms data-range ratio.
        /// </summary>
        public static double AxesRatio(this PlotModel plotModel, MollierControlSettings mollierControlSettings)
        {
            ChartType chartType = mollierControlSettings.ChartType;
            double realOXSize = chartType == ChartType.Mollier ? 30.45 : 28;
            double realOYSize = 13.3;

            Axis axisX = plotModel.Axes.FirstOrDefault(a => a.IsHorizontal());
            Axis axisY = plotModel.Axes.FirstOrDefault(a => a.IsVertical());
            if (axisX == null || axisY == null)
            {
                return 1;
            }

            double OXRatio = axisX.Maximum - axisX.Minimum;
            double OYRatio = axisY.Maximum - axisY.Minimum;

            double xScale = OXRatio / realOXSize;
            double yScale = OYRatio / realOYSize;

            // Pixel-aspect correction. After a render PlotArea is non-empty; use the real pixel-per-data
            // ratio so scaled-Y and X spans render as equal pixel distances (isotropic solver space).
            double plotWidth = plotModel.PlotArea.Width;
            double plotHeight = plotModel.PlotArea.Height;
            if (plotWidth > 0 && plotHeight > 0 && OXRatio > 0 && OYRatio > 0)
            {
                double pixelsPerX = plotWidth / OXRatio;
                double pixelsPerY = plotHeight / OYRatio;
                return pixelsPerY / pixelsPerX;
            }

            return yScale == 0 ? 1 : xScale / yScale;
        }
    }
}
