// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using OxyPlot;
using OxyPlot.Series;
using SAM.Geometry.Mollier;
using SAM.Geometry.Planar;
using System.Collections.Generic;
using System.Linq;
using SystemColor = System.Drawing.Color;

namespace SAM.Core.Mollier.UI
{
    public static partial class Modify
    {
        // Title shared by every "MollierPoints" scatter series. OxyPlot has no per-point colour within a
        // single ScatterSeries, so points are grouped into one ScatterSeries PER colour (the agreed native
        // approach). 2d locates/clears them by this title; per-point payload is carried on ScatterPoint.Tag.
        public const string MollierPointsTitle = "MollierPoints";

        /// <summary>
        /// Creates scatter series for all the model points (OxyPlot port; one series per colour).
        /// </summary>
        public static List<Series> AddMollierPoints(this PlotModel plotModel, IEnumerable<UIMollierPoint> uIMollierPoints, MollierControlSettings mollierControlSettings)
        {
            if (uIMollierPoints == null || mollierControlSettings.DivisionArea)
            {
                return null;
            }

            // Reuse semantics: drop any previously-created MollierPoints series before re-adding.
            foreach (Series series_Existing in plotModel.Series.Where(x => (x.Tag as string) == null && x.Title == MollierPointsTitle).ToList())
            {
                plotModel.Series.Remove(series_Existing);
            }

            ChartType chartType = mollierControlSettings.ChartType;

            Dictionary<UIMollierPoint, SystemColor> colorDictionary = uIMollierPoints.ColorDictionary(mollierControlSettings);

            Dictionary<OxyColor, ScatterSeries> seriesByColor = new Dictionary<OxyColor, ScatterSeries>();
            List<Point2D> addedPoints = new List<Point2D>();
            bool anchorAdded = false;

            foreach (UIMollierPoint uIMollierPoint in uIMollierPoints)
            {
                MollierPoint mollierPoint = uIMollierPoint;
                if (mollierPoint == null)
                {
                    continue;
                }

                Point2D point = Convert.ToSAM(mollierPoint, chartType);
                if (addedPoints.Any(x => x.X.AlmostEqual(point.X, Tolerance.MacroDistance) && x.Y.AlmostEqual(point.Y, Tolerance.MacroDistance)))
                {
                    continue;
                }

                SystemColor color = colorDictionary[uIMollierPoint] == SystemColor.Empty ? uIMollierPoint.UIMollierAppearance.Color : colorDictionary[uIMollierPoint];
                OxyColor oxyColor = color.ToOxyColor(OxyColors.Automatic);

                if (!seriesByColor.TryGetValue(oxyColor, out ScatterSeries series))
                {
                    series = new ScatterSeries
                    {
                        Title = MollierPointsTitle,
                        RenderInLegend = false,
                        MarkerType = MarkerType.Circle,
                        MarkerSize = 7,
                        MarkerFill = oxyColor,
                        Tag = uIMollierPoints,
                    };
                    seriesByColor[oxyColor] = series;
                    plotModel.Series.Add(series);

                    if (!anchorAdded)
                    {
                        // Anchor at HumidityRatio = 0 so the first point renders correctly (WinForms parity); size 0 = invisible.
                        series.Points.Add(new ScatterPoint(1, 0, 0));
                        anchorAdded = true;
                    }
                }

                series.Points.Add(new ScatterPoint(point.X, point.Y, 7, double.NaN, new UIMollierPoint(uIMollierPoint)));
                addedPoints.Add(point);
            }

            return seriesByColor.Values.Cast<Series>().ToList();
        }

        /// <summary>
        /// Creates scatter series for all the model points (OxyPlot port; one series per colour).
        /// </summary>
        public static List<Series> AddMollierPoints(this PlotModel plotModel, MollierModel mollierModel, MollierControlSettings mollierControlSettings)
        {
            if (mollierModel == null)
            {
                return null;
            }

            List<UIMollierPoint> uIMollierPoints = mollierModel.GetMollierObjects<UIMollierPoint>();

            uIMollierPoints = uIMollierPoints?.FindAll(x => x.UIMollierAppearance.Visible == true);
            return plotModel.AddMollierPoints(uIMollierPoints, mollierControlSettings);
        }
    }
}
