// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using SAM.Core.Mollier.UI.Classes;
using SAM.Geometry.Mollier;
using SAM.Geometry.Planar;
using System.Collections.Generic;
using System.Linq;
using SystemColor = System.Drawing.Color;

namespace SAM.Core.Mollier.UI
{
    public static partial class Modify
    {
        /// <summary>
        /// Creates labels for all Mollier objects on the chart, using the 2D collision solver (OxyPlot port).
        /// Must be called after the plot has been rendered once (PlotArea + axis transforms valid).
        /// </summary>
        public static List<Annotation> AddLabels(this PlotModel plotModel, MollierControlSettings mollierControlSettings)
        {
            Axis axisX = plotModel.Axes.FirstOrDefault(a => a.IsHorizontal());
            Axis axisY = plotModel.Axes.FirstOrDefault(a => a.IsVertical());
            if (axisX == null || axisY == null)
            {
                return null;
            }

            Vector2D scaleVector = Query.ScaleVector2D(plotModel, mollierControlSettings);
            double axesRatio = Query.AxesRatio(plotModel, mollierControlSettings);

            List<IClosed2D> obstacles = Query.Obstacles(plotModel, mollierControlSettings);
            List<Solver2DData> solver2DDatas = Create.Solver2DDatas(plotModel, mollierControlSettings);

            Point2D chartMinPoint = new Point2D(axisX.Minimum, axisY.Minimum * axesRatio);
            Point2D chartMaxPoint = new Point2D(axisX.Maximum, axisY.Maximum * axesRatio);
            Rectangle2D chartArea = new Rectangle2D(new BoundingBox2D(chartMinPoint, chartMaxPoint));

            Solver2D solver = new Solver2D(chartArea, obstacles);
            solver.AddRange(solver2DDatas);

            if (solver2DDatas == null || solver2DDatas.Count == 0)
            {
                return null;
            }

            List<Solver2DResult> solver2DResults = solver.Solve();
            if (solver2DResults == null)
            {
                return null;
            }

            List<ChartLabel> chartLabels = Create.ChartLabels(solver2DResults, mollierControlSettings, scaleVector, axesRatio, plotModel);
            for (int i = 0; i < chartLabels.Count; i++)
            {
                chartLabels[i].Position.Y = System.Math.Max(chartLabels[i].Position.Y, axisY.Minimum);
            }

            List<Annotation> result = new List<Annotation>();
            foreach (ChartLabel chartLabel in chartLabels)
            {
                TextAnnotation annotation = AddLabel(plotModel, chartLabel, mollierControlSettings);
                if (annotation != null)
                {
                    result.Add(annotation);
                }
            }

            List<Annotation> moved = AddLabels_Moved(plotModel, mollierControlSettings, axisX, axisY);
            if (moved != null)
            {
                result.AddRange(moved);
            }

            return result;
        }

        // User-positioned labels (UIMollierLabelAppearance.Vector2D). Simplified OxyPlot port:
        // handles single-point series (Tag = UIMollierPoint) and the MollierPoints scatter series
        // (ScatterPoint.Tag = UIMollierPoint). Text height is approximated by the default font size.
        private static List<Annotation> AddLabels_Moved(PlotModel plotModel, MollierControlSettings mollierControlSettings, Axis axisX, Axis axisY)
        {
            ChartType chartType = mollierControlSettings.ChartType;
            double fontHeight = plotModel.DefaultFontSize > 0 ? plotModel.DefaultFontSize : 12;

            List<UIMollierPoint> uIMollierPoints = new List<UIMollierPoint>();
            foreach (Series series in plotModel.Series.ToList())
            {
                if (series.Tag is UIMollierPoint uIMollierPoint_Tag)
                {
                    uIMollierPoints.Add(uIMollierPoint_Tag);
                }
                else if (series is ScatterSeries scatterSeries && (series.Tag is IEnumerable<UIMollierPoint>))
                {
                    foreach (ScatterPoint scatterPoint in scatterSeries.Points)
                    {
                        if (scatterPoint.Tag is UIMollierPoint uIMollierPoint_Point)
                        {
                            uIMollierPoints.Add(uIMollierPoint_Point);
                        }
                    }
                }
            }

            List<Annotation> result = new List<Annotation>();
            foreach (UIMollierPoint uIMollierPoint in uIMollierPoints)
            {
                UIMollierPointAppearance uIMollierPointAppearance = uIMollierPoint.UIMollierAppearance as UIMollierPointAppearance;
                UIMollierLabelAppearance uIMollierLabelAppearance = uIMollierPointAppearance?.UIMollierLabelAppearance;
                Vector2D vector2D = uIMollierLabelAppearance?.Vector2D;
                if (vector2D == null)
                {
                    continue;
                }

                string text = uIMollierLabelAppearance.Text;
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                SystemColor color = uIMollierLabelAppearance.Color == SystemColor.Empty ? SystemColor.Black : uIMollierLabelAppearance.Color;

                Point2D point = Convert.ToSAM(uIMollierPoint, chartType);
                point.Move(vector2D);

                // value -> pixel (shift down by text height) -> value
                point = new Point2D(axisX.Transform(point.X), axisY.Transform(point.Y));
                point = new Point2D(axisX.InverseTransform(point.X), axisY.InverseTransform(point.Y + fontHeight));

                ChartLabel chartLabel = new ChartLabel(point, text, 0, color) { Tag = uIMollierPoint };
                TextAnnotation annotation = AddLabel(plotModel, chartLabel, mollierControlSettings);
                if (annotation != null)
                {
                    result.Add(annotation);
                }
            }

            return result;
        }
    }
}
