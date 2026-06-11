// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using OxyPlot;
using OxyPlot.Axes;
using SAM.Core.Mollier.UI.Classes;
using SAM.Geometry.Mollier;
using SAM.Geometry.Planar;
using System;
using System.Collections.Generic;
using System.Linq;
using SystemColor = System.Drawing.Color;

namespace SAM.Core.Mollier.UI
{
    public static partial class Create
    {
        public static List<ChartLabel> ChartLabels(List<Solver2DResult> solver2DResults, MollierControlSettings mollierControlSettings, Vector2D scaleVector, double axesRatio, PlotModel plotModel)
        {
            if (solver2DResults == null)
            {
                return null;
            }

            Axis axisX = plotModel.Axes.FirstOrDefault(a => a.IsHorizontal());
            Axis axisY = plotModel.Axes.FirstOrDefault(a => a.IsVertical());
            if (axisX == null || axisY == null)
            {
                return null;
            }

            List<ChartLabel> result = new List<ChartLabel>();
            ChartType chartType = mollierControlSettings.ChartType;

            Func<Vector2D, int> vectorToAngle = new Func<Vector2D, int>((Vector2D vector) =>
            {
                if (vector.Y == 0) return 0;
                if (vector.X == 0) return 90;

                double tan = vector.Y / vector.X;
                int angle = -System.Convert.ToInt32(System.Math.Atan(tan) * 180 / System.Math.PI);
                return angle;
            });

            double fontHeight = plotModel.DefaultFontSize > 0 ? plotModel.DefaultFontSize : 12;

            Func<Rectangle2D, Tuple<Point2D, double>> getPositionAngle = new Func<Rectangle2D, Tuple<Point2D, double>>((Rectangle2D rectangle) =>
            {
                if (rectangle == null)
                {
                    return null;
                }

                double distanceFromCenter = (chartType == ChartType.Mollier ? 0.8 : 0.4) * scaleVector.Y;
                Point2D center = rectangle.GetCentroid().GetScaledY(1 / axesRatio);
                Point2D point = new Point2D(center.X, center.Y - distanceFromCenter);

                double angle = vectorToAngle(rectangle.WidthDirection);
                return new Tuple<Point2D, double>(point, angle);
            });

            foreach (Solver2DResult solver2DResult in solver2DResults)
            {
                Solver2DData solver2DData = solver2DResult.Solver2DData;
                Rectangle2D rectangle2D = solver2DResult.Closed2D<Rectangle2D>();
                if (rectangle2D == null)
                {
                    continue;
                }

                UIMollierPoint uIMollierPoint = solver2DData.Tag as UIMollierPoint;
                if (uIMollierPoint == null)
                {
                    continue;
                }

                Tuple<Point2D, double> positionAngleLabel = getPositionAngle(rectangle2D);

                Point2D point = rectangle2D.GetCentroid().GetScaledY(1 / axesRatio);

                Vector2D vector2D = Vector2D.WorldY * (fontHeight * 1.2);

                // value -> pixel -> shift up by ~font height -> pixel -> value
                point = new Point2D(axisX.Transform(point.X), axisY.Transform(point.Y));
                point = point.GetMoved(vector2D);
                point = new Point2D(axisX.InverseTransform(point.X), axisY.InverseTransform(point.Y));

                positionAngleLabel = new Tuple<Point2D, double>(point, positionAngleLabel.Item2);

                string text = null;
                SystemColor color = SystemColor.Empty;

                UIMollierAppearance uIMollierAppearance = uIMollierPoint.UIMollierAppearance as UIMollierAppearance;
                if (uIMollierAppearance != null)
                {
                    text = uIMollierAppearance.Label;
                    color = uIMollierPoint.UIMollierAppearance.Color;

                    if (uIMollierAppearance.UIMollierLabelAppearance != null)
                    {
                        color = uIMollierAppearance.UIMollierLabelAppearance.Color;
                    }

                    if (color == SystemColor.Empty)
                    {
                        color = SystemColor.Black;
                    }
                }

                result.Add(new ChartLabel(positionAngleLabel.Item1, text, positionAngleLabel.Item2, color) { Tag = uIMollierPoint });
            }

            return result;
        }
    }
}
