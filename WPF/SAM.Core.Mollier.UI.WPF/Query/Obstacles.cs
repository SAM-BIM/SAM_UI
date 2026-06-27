// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using OxyPlot;
using OxyPlot.Series;
using SAM.Geometry.Mollier;
using SAM.Geometry.Planar;
using System.Collections.Generic;

namespace SAM.Core.Mollier.UI
{
    public static partial class Query
    {
        /// <summary>
        /// Calculates all the obstacles on the chart that labels must avoid (OxyPlot port).
        /// </summary>
        public static List<IClosed2D> Obstacles(this PlotModel plotModel, MollierControlSettings mollierControlSettings)
        {
            List<IClosed2D> result = new List<IClosed2D>();
            ChartType chartType = mollierControlSettings.ChartType;

            Vector2D scaleVector = ScaleVector2D(plotModel, mollierControlSettings);
            double axesRatio = AxesRatio(plotModel, mollierControlSettings);

            bool addProcesses = true;
            bool addPoints = true;
            checkQuantity(plotModel, ref addProcesses, ref addPoints);

            foreach (Series series in plotModel.Series)
            {
                if (addProcesses && series.Tag is UIMollierProcess process)
                {
                    result.AddRange(obstacles_Process(process, chartType, scaleVector, axesRatio));
                }
                if (addPoints && series.Tag is UIMollierPoint point)
                {
                    result.AddRange(obstacles_Point(point, chartType, scaleVector, axesRatio));
                }
                if (series.Tag is UIMollierZone zone)
                {
                    result.AddRange(obstacles_Zone(zone, chartType, scaleVector, axesRatio));
                }
            }

            return result;
        }

        private static List<IClosed2D> obstacles_Point(UIMollierPoint point, ChartType chartType, Vector2D scaleVector, double axesRatio)
        {
            List<IClosed2D> result = new List<IClosed2D>();
            double pointRadius = chartType == ChartType.Mollier ? 0.14 * scaleVector.X : 0.3 * scaleVector.X;

            Point2D center = Convert.ToSAM(point, chartType).GetScaledY(axesRatio);
            Circle2D circle = new Circle2D(center, pointRadius);
            result.Add(circle);

            return result;
        }

        private static List<IClosed2D> obstacles_Process(UIMollierProcess process, ChartType chartType, Vector2D scaleVector, double axesRatio)
        {
            List<IClosed2D> result = new List<IClosed2D>();
            double processWidth = chartType == ChartType.Mollier ? 0.2 * scaleVector.X : 0.4 * scaleVector.X;

            Point2D start = Convert.ToSAM(process.Start, chartType);
            Point2D end = Convert.ToSAM(process.End, chartType);
            Segment2D processSegment = new Segment2D(start, end);

            Rectangle2D processRectangle = Geometry.Planar.Create.Rectangle2D(processSegment, processWidth, axesRatio);

            result.Add(processRectangle);
            result.AddRange(obstacles_Point(new UIMollierPoint(process.Start), chartType, scaleVector, axesRatio));
            result.AddRange(obstacles_Point(new UIMollierPoint(process.End), chartType, scaleVector, axesRatio));

            return result;
        }

        private static List<IClosed2D> obstacles_Zone(UIMollierZone uIMollierZone, ChartType chartType, Vector2D scaleVector, double axesRatio)
        {
            List<IClosed2D> result = new List<IClosed2D>();
            double zoneWidth = 0.05 * scaleVector.Y;

            for (int i = 0; i < uIMollierZone.MollierPoints.Count; i++)
            {
                int previousPointID = i == 0 ? uIMollierZone.MollierPoints.Count - 1 : i - 1;
                Point2D start = Convert.ToSAM(uIMollierZone.MollierPoints[i], chartType);
                Point2D end = Convert.ToSAM(uIMollierZone.MollierPoints[previousPointID], chartType);
                Segment2D zoneSegment = new Segment2D(start, end);

                result.Add(Geometry.Planar.Create.Rectangle2D(zoneSegment, zoneWidth, axesRatio));
            }

            return result;
        }

        private static void checkQuantity(PlotModel plotModel, ref bool addProcesses, ref bool addPoints, int maxQuantity = 30)
        {
            int numberOfProcesses = 0;
            int numberOfPoints = 0;
            foreach (Series series in plotModel.Series)
            {
                if (series.Tag is UIMollierProcess)
                {
                    numberOfProcesses++;
                }
                if (series.Title == Modify.MollierPointsTitle)
                {
                    numberOfPoints++;
                }
            }
            addProcesses = numberOfProcesses <= maxQuantity;
            addPoints = numberOfPoints <= maxQuantity;
        }
    }
}
