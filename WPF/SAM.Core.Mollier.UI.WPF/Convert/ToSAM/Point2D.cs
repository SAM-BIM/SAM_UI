// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.Planar;

namespace SAM.Core.Mollier.UI
{
    public static partial class Convert
    {
        // OxyPlot port of the WinForms DataPoint -> Point2D converter. OxyPlot's DataPoint is a
        // struct with a single Y (no YValues[] array), so the indexY parameter is dropped.
        public static Point2D ToSAM(this OxyPlot.DataPoint dataPoint)
        {
            return new Point2D(dataPoint.X, dataPoint.Y);
        }

        // Unchanged from the WinForms version: maps a MollierPoint to chart coordinates.
        // Mollier chart:        X = humidity ratio (g/kg), Y = diagram temperature.
        // Psychrometric chart:  X = dry-bulb temperature,  Y = humidity ratio (g/kg).
        public static Point2D ToSAM(this MollierPoint mollierPoint, ChartType chartType)
        {
            if (mollierPoint == null)
            {
                return null;
            }

            double humidityRatio = mollierPoint.HumidityRatio;
            double dryBulbTemperature = mollierPoint.DryBulbTemperature;
            double diagramTemperature = double.NaN;

            if (chartType == ChartType.Mollier)
            {
                diagramTemperature = Mollier.Query.DiagramTemperature(mollierPoint);
            }

            double x = chartType == ChartType.Mollier ? humidityRatio * 1000 : dryBulbTemperature;
            double y = chartType == ChartType.Mollier ? diagramTemperature : humidityRatio * 1000;

            return new Point2D(x, y);
        }
    }
}
