// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using OxyPlot.Series;
using SAM.Geometry.Planar;

namespace SAM.Core.Mollier.UI
{
    public static partial class Query
    {
        // OxyPlot port: points live in a ScatterSeries (ScatterPoint has X/Y, no YValues[] array).
        public static bool Contains(this ScatterSeries series, ChartType chartType, MollierPoint mollierPoint, double tolerance = Tolerance.Distance)
        {
            if (series == null || chartType == ChartType.Undefined || mollierPoint == null)
            {
                return false;
            }

            foreach (ScatterPoint scatterPoint in series.Points)
            {
                Point2D point2D = new Point2D(scatterPoint.X, scatterPoint.Y);
                MollierPoint mollierPoint_Temp = Convert.ToMollier(point2D, chartType, mollierPoint.Pressure);
                if (mollierPoint.DryBulbTemperature.AlmostEqual(mollierPoint_Temp.DryBulbTemperature, tolerance) && mollierPoint.HumidityRatio.AlmostEqual(mollierPoint_Temp.HumidityRatio, tolerance))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool Contains(this ScatterSeries series, double x, double y, double tolerance = Tolerance.Distance)
        {
            if (series == null || double.IsNaN(x) || double.IsNaN(y))
            {
                return false;
            }

            foreach (ScatterPoint scatterPoint in series.Points)
            {
                if (scatterPoint.X.AlmostEqual(x, tolerance) && scatterPoint.Y.AlmostEqual(y, tolerance))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
