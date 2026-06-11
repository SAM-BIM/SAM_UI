// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Series;
using SAM.Geometry.Mollier;
using SAM.Geometry.Planar;
using System;
using System.Collections.Generic;

namespace SAM.Core.Mollier.UI
{
    public static partial class Modify
    {
        /// <summary>
        /// Highlights and creates series for the point calculated by % of enthalpy or temperature (OxyPlot port).
        /// </summary>
        public static List<Series> AddColorPoint(this PlotModel plotModel, List<UIMollierPoint> mollierPoints, MollierControlSettings mollierControlSettings)
        {
            if (mollierPoints == null || mollierControlSettings.FindPoint == false)
            {
                return null;
            }

            Point2D colorPoint = mollierPoints.FindPoint(mollierControlSettings);
            if (colorPoint == null)
            {
                return null;
            }

            List<Series> result = new List<Series>();

            result.Add(plotModel.addColorPoint(colorPoint, mollierControlSettings));
            result.Add(plotModel.addColorPointLabel(colorPoint, mollierControlSettings));

            return result;
        }

        /// <summary>
        /// Highlights and creates series for the point calculated by % of enthalpy or temperature (OxyPlot port).
        /// </summary>
        public static List<Series> AddColorPoint(this PlotModel plotModel, MollierModel mollierModel, MollierControlSettings mollierControlSettings)
        {
            if (mollierModel == null)
            {
                return null;
            }

            List<UIMollierPoint> uIMollierPoints = mollierModel.GetMollierObjects<UIMollierPoint>();

            return plotModel.AddColorPoint(uIMollierPoints, mollierControlSettings);
        }

        private static Series addColorPoint(this PlotModel plotModel, Point2D colorPoint, MollierControlSettings mollierControlSettings)
        {
            ScatterSeries result = new ScatterSeries
            {
                Title = Guid.NewGuid().ToString(),
                RenderInLegend = false,
                MarkerType = MarkerType.Circle,
                MarkerFill = OxyColors.Red,
                MarkerSize = 10,
                Tag = "ColorPoint",
            };
            result.Points.Add(new ScatterPoint(colorPoint.X, colorPoint.Y));

            plotModel.Series.Add(result);
            return result;
        }

        private static Series addColorPointLabel(this PlotModel plotModel, Point2D colorPoint, MollierControlSettings mollierControlSettings)
        {
            ChartType chartType = mollierControlSettings.ChartType;

            ScatterSeries result = new ScatterSeries
            {
                Title = Guid.NewGuid().ToString(),
                RenderInLegend = false,
                MarkerType = MarkerType.Circle,
                MarkerFill = OxyColors.Red,
                MarkerSize = 15,
                Tag = "ColorPointLabel",
            };

            Point2D labelPosition = chartType == ChartType.Mollier ? new Point2D(20, 0) : new Point2D(0, 15); // TODO: Changed
            result.Points.Add(new ScatterPoint(labelPosition.X, labelPosition.Y));
            plotModel.Series.Add(result);

            // WinForms set series.Label; OxyPlot uses a TextAnnotation for free text on the plot.
            string text = Query.ToolTipText(colorPoint.ToMollier(chartType, mollierControlSettings.Pressure), chartType);
            plotModel.Annotations.Add(new TextAnnotation
            {
                Text = text,
                TextPosition = new DataPoint(labelPosition.X, labelPosition.Y),
                StrokeThickness = 0,
                TextColor = OxyColors.Red,
                Tag = "ColorPointLabel",
            });

            return result;
        }
    }
}
