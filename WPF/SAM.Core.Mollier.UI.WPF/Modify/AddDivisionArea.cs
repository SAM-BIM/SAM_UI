// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using OxyPlot;
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
        /// Creates series for division area lines (OxyPlot port). See Query.DivisionMollierZones.
        /// </summary>
        public static List<Series> AddDivisionArea(this PlotModel plotModel, IEnumerable<UIMollierPoint> uIMollierPoints, MollierControlSettings mollierControlSettings)
        {
            if (mollierControlSettings.DivisionArea == false)
            {
                return null;
            }

            List<Series> result = new List<Series>();
            List<UIMollierZone> divisionAreas = uIMollierPoints.DivisionMollierZones(mollierControlSettings);

            foreach (UIMollierZone divisionArea in divisionAreas)
            {
                result.Add(plotModel.addDivisionArea(divisionArea, mollierControlSettings));
            }

            return result;
        }

        /// <summary>
        /// Creates series for division area lines (OxyPlot port). See Query.DivisionMollierZones.
        /// </summary>
        public static List<Series> AddDivisionArea(this PlotModel plotModel, MollierModel mollierModel, MollierControlSettings mollierControlSettings)
        {
            if (mollierModel == null)
            {
                return null;
            }

            List<UIMollierPoint> uIMollierPoints = mollierModel.GetMollierObjects<UIMollierPoint>();

            return plotModel.AddDivisionArea(uIMollierPoints, mollierControlSettings);
        }

        private static Series addDivisionArea(this PlotModel plotModel, UIMollierZone divisionArea, MollierControlSettings mollierControlSettings)
        {
            if (divisionArea == null)
            {
                return null;
            }

            if (divisionArea.MollierPoints == null || divisionArea.MollierPoints.Count < 4)
            {
                return null;
            }

            LineSeries series = new LineSeries
            {
                Title = "DivisionArea " + Guid.NewGuid().ToString(),
                RenderInLegend = false,
                StrokeThickness = 3,
                Color = divisionArea.UIMollierAppearance.Color.ToOxyColor(),
            };

            if (!mollierControlSettings.DivisionAreaLabels)
            {
                string text = "";
                divisionArea.UIMollierAppearance = new UIMollierAppearance(divisionArea.UIMollierAppearance.Color, text);
            }
            series.Tag = divisionArea;

            foreach (MollierPoint mollierPoint in divisionArea.MollierPoints)
            {
                Point2D point = mollierPoint.ToSAM(mollierControlSettings.ChartType);
                series.Points.Add(new DataPoint(point.X, point.Y));
            }

            Point2D closingPoint = divisionArea.MollierPoints[0].ToSAM(mollierControlSettings.ChartType);
            series.Points.Add(new DataPoint(closingPoint.X, closingPoint.Y));

            plotModel.Series.Add(series);
            return series;
        }
    }
}
