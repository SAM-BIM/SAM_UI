// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using OxyPlot;
using OxyPlot.Series;
using SAM.Geometry.Mollier;
using SAM.Geometry.Planar;
using SystemColor = System.Drawing.Color;

namespace SAM.Core.Mollier.UI
{
    public static partial class Modify
    {
        // WinForms Series default marker metrics (used as fallbacks where the appearance is unset).
        private const int DefaultMarkerSize = 5;
        private const int DefaultMarkerBorderWidth = 1;

        public static ScatterSeries AddMollierPoint(this PlotModel plotModel, ChartType chartType, UIMollierPoint uIMollierPoint, MollierControlSettings mollierControlSettings, DisplayPointType displayPointType = DisplayPointType.Undefined)
        {
            if (plotModel == null || chartType == ChartType.Undefined || uIMollierPoint == null)
            {
                return null;
            }

            Point2D point2D = Convert.ToSAM(uIMollierPoint, chartType);

            UIMollierPointAppearance uIMollierPointAppearance = uIMollierPoint.UIMollierAppearance as UIMollierPointAppearance;
            if (displayPointType != DisplayPointType.Undefined)
            {
                uIMollierPointAppearance = Create.UIMollierPointAppearance(displayPointType, uIMollierPointAppearance.Label);
            }

            SystemColor color = uIMollierPointAppearance.Color;
            int size = uIMollierPointAppearance.Size;
            if (mollierControlSettings != null && !Core.Query.Similar(mollierControlSettings.PointColor, SystemColor.Empty))
            {
                color = mollierControlSettings.PointColor;
            }

            if (size < 1)
            {
                size = DefaultMarkerSize;
            }

            if (mollierControlSettings != null && mollierControlSettings.PointSize != -1)
            {
                size = mollierControlSettings.PointSize;
            }

            if (mollierControlSettings != null && mollierControlSettings.DisablePoint)
            {
                color = SystemColor.Empty;
                size = 0;
            }

            SystemColor borderColor = uIMollierPointAppearance.BorderColor;
            int borderWidth = uIMollierPointAppearance.BorderSize;
            if (mollierControlSettings != null && !Core.Query.Similar(mollierControlSettings.PointBorderColor, SystemColor.Empty))
            {
                borderColor = mollierControlSettings.PointBorderColor;
            }

            if (borderWidth < 1)
            {
                borderWidth = DefaultMarkerBorderWidth;
            }

            if (mollierControlSettings != null && mollierControlSettings.PointBorderSize != -1)
            {
                borderWidth = mollierControlSettings.PointBorderSize;
            }

            if (mollierControlSettings != null && mollierControlSettings.DisablePointBoarder)
            {
                borderColor = SystemColor.Empty;
                borderWidth = 0;
            }

            ScatterSeries series = new ScatterSeries
            {
                Title = System.Guid.NewGuid().ToString(),
                RenderInLegend = false,
                MarkerType = MarkerType.Circle,
                MarkerSize = size,
                MarkerFill = color.ToOxyColor(OxyColors.Automatic),
                MarkerStroke = borderColor.ToOxyColor(OxyColors.Automatic),
                MarkerStrokeThickness = borderWidth,
                TrackerFormatString = Query.ToolTipText(uIMollierPoint, chartType),
                Tag = uIMollierPoint,
            };

            // ScatterPoint.Tag carries the domain object so 2d hit-testing can read TrackerHitResult.Item.
            series.Points.Add(new ScatterPoint(point2D.X, point2D.Y, size, double.NaN, uIMollierPoint));

            plotModel.Series.Add(series);
            return series;
        }
    }
}
