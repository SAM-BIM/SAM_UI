// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using OxyPlot;
using OxyPlot.Series;
using SAM.Geometry.Mollier;
using SAM.Geometry.Planar;
using System.Collections.Generic;

namespace SAM.Core.Mollier.UI
{
    public static partial class Modify
    {
        /// <summary>
        /// Creates series for all the zones (OxyPlot port).
        /// </summary>
        public static List<Series> AddMollierZones(this PlotModel plotModel, IEnumerable<UIMollierZone> mollierZones, MollierControlSettings mollierControlSettings)
        {
            if (mollierZones == null)
            {
                return null;
            }

            List<Series> result = new List<Series>();
            ChartType chartType = mollierControlSettings.ChartType;

            foreach (UIMollierZone uIMollierZone in mollierZones)
            {
                LineSeries series = new LineSeries
                {
                    Title = "Mollier Zone " + System.Guid.NewGuid().ToString(),
                    RenderInLegend = false,
                    StrokeThickness = 2,
                    Color = uIMollierZone.UIMollierAppearance.Color.ToOxyColor(),
                    Tag = uIMollierZone,
                };

                foreach (MollierPoint mollierPoint in uIMollierZone.MollierPoints)
                {
                    Point2D point = Convert.ToSAM(mollierPoint, chartType);
                    series.Points.Add(new DataPoint(point.X, point.Y));
                }

                plotModel.Series.Add(series);
                result.Add(series);
            }

            return result;
        }

        /// <summary>
        /// Creates series for all the zones (OxyPlot port).
        /// </summary>
        public static List<Series> AddMollierZones(this PlotModel plotModel, MollierModel mollierModel, MollierControlSettings mollierControlSettings)
        {
            if (mollierModel == null)
            {
                return null;
            }

            List<UIMollierZone> uIMollierZones = mollierModel.GetMollierObjects<UIMollierZone>();

            uIMollierZones = uIMollierZones?.FindAll(x => x.UIMollierAppearance.Visible == true);
            return plotModel.AddMollierZones(uIMollierZones, mollierControlSettings);
        }
    }
}
