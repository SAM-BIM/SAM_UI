// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using OxyPlot.Series;
using SAM.Geometry.Mollier;
using System.Collections.Generic;

namespace SAM.Core.Mollier.UI
{
    public static partial class Query
    {
        /// <summary>
        /// Zoom to processes and points (OxyPlot port). Reads vertices from process/point series.
        /// </summary>
        public static void ZoomParameters(IEnumerable<Series> series_List, ChartType chartType, out double x_Min, out double x_Max, out double y_Min, out double y_Max, double x_Factor = 5, double y_Factor = 5)
        {
            List<OxyPlot.DataPoint> dataPoints = new List<OxyPlot.DataPoint>();

            foreach (Series series in series_List)
            {
                object tag = series.Tag;
                if (!(tag is IMollierProcess) && !(tag is List<UIMollierPoint>) && !(tag is List<MollierPoint>) && !(ReferenceEquals(tag, "GradientZone") || (tag as string) == "GradientZone"))
                {
                    continue;
                }

                if (series is LineSeries lineSeries)
                {
                    foreach (OxyPlot.DataPoint dataPoint in lineSeries.Points)
                    {
                        dataPoints.Add(dataPoint);
                    }
                }
                else if (series is ScatterSeries scatterSeries)
                {
                    foreach (ScatterPoint scatterPoint in scatterSeries.Points)
                    {
                        dataPoints.Add(new OxyPlot.DataPoint(scatterPoint.X, scatterPoint.Y));
                    }
                }
            }

            x_Min = double.MaxValue;
            x_Max = double.MinValue;
            y_Min = double.MaxValue;
            y_Max = double.MinValue;

            for (int i = 0; i < dataPoints.Count; i++)
            {
                if (i == 0) continue; // first collected point is the hidden (1,0) anchor from AddMollierPoints
                OxyPlot.DataPoint dataPoint = dataPoints[i];
                if (dataPoint.X > x_Max) x_Max = dataPoint.X;
                if (dataPoint.X < x_Min) x_Min = dataPoint.X;
                if (dataPoint.Y > y_Max) y_Max = dataPoint.Y;
                if (dataPoint.Y < y_Min) y_Min = dataPoint.Y;
            }

            x_Min = x_Min % x_Factor == 0 ? System.Math.Floor(x_Min / x_Factor) * x_Factor - x_Factor : System.Math.Floor(x_Min / x_Factor) * x_Factor;
            x_Max = x_Max % x_Factor == 0 ? System.Math.Ceiling(x_Max / x_Factor) * x_Factor + x_Factor : System.Math.Ceiling(x_Max / x_Factor) * x_Factor;
            y_Min = y_Min % y_Factor == 0 ? System.Math.Floor(y_Min / y_Factor) * y_Factor - y_Factor : System.Math.Floor(y_Min / y_Factor) * y_Factor;
            y_Max = y_Max % y_Factor == 0 ? System.Math.Ceiling(y_Max / y_Factor) * y_Factor + y_Factor : System.Math.Ceiling(y_Max / y_Factor) * y_Factor;

            if (chartType == ChartType.Mollier)
            {
                x_Min /= 1000;
                x_Max /= 1000;
            }
            else
            {
                y_Min /= 1000;
                y_Max /= 1000;
            }
        }
    }
}
