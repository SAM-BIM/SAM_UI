// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace MollierSpike
{
    /// <summary>
    /// Builds a representative psychrometric PlotModel using OxyPlot LineSeries + ScatterSeries.
    /// This is the OxyPlot analogue of what the WinForms Modify/AddMollier*.cs builders do
    /// against System.Windows.Forms.DataVisualization.Charting.Chart today.
    /// </summary>
    internal static class DiagramBuilder
    {
        public const double TMin = -10;
        public const double TMax = 50;
        public const double WMin = 0;
        public const double WMax = 30; // g/kg

        // Representative air-state points (T degC, RH %), the "data points" a user clicks.
        public static readonly (double T, double RH)[] StatePoints =
        {
            (20, 50), (24, 40), (26, 60), (30, 35), (16, 70), (35, 30),
        };

        public const string StateSeriesTitle = "Air states";

        public static PlotModel Build()
        {
            PlotModel model = new PlotModel
            {
                Title = "Psychrometric (spike)",
                PlotMargins = new OxyThickness(50, 10, 60, 40),
                Background = OxyColors.White,
            };

            LinearAxis xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Dry-bulb temperature (°C)",
                Minimum = TMin,
                Maximum = TMax,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Key = "X",
            };
            // Humidity ratio is conventionally drawn on the RIGHT in a psychrometric chart.
            LinearAxis yAxis = new LinearAxis
            {
                Position = AxisPosition.Right,
                Title = "Humidity ratio (g/kg)",
                Minimum = WMin,
                Maximum = WMax,
                MajorGridlineStyle = LineStyle.Solid,
                MinorGridlineStyle = LineStyle.Dot,
                Key = "Y",
            };
            model.Axes.Add(xAxis);
            model.Axes.Add(yAxis);

            // Saturation curve (100% RH) — the bounding curve.
            AddCurve(model, Psychrometrics.ConstantRelativeHumidityCurve(100, TMin, TMax), OxyColors.Blue, 2.0, LineStyle.Solid);

            // Constant-RH family.
            foreach (double rh in new[] { 10.0, 20, 30, 40, 50, 60, 70, 80, 90 })
            {
                AddCurve(model, Psychrometrics.ConstantRelativeHumidityCurve(rh, TMin, TMax), OxyColors.LightBlue, 1.0, LineStyle.Solid);
            }

            // A few constant-enthalpy lines.
            foreach (double h in new[] { 20.0, 40, 60, 80 })
            {
                AddCurve(model, Psychrometrics.ConstantEnthalpyCurve(h, TMin, TMax), OxyColors.LightGray, 1.0, LineStyle.Dash);
            }

            // State points as a hit-testable scatter series.
            ScatterSeries scatter = new ScatterSeries
            {
                Title = StateSeriesTitle,
                MarkerType = MarkerType.Circle,
                MarkerSize = 6,
                MarkerFill = OxyColors.Red,
                XAxisKey = "X",
                YAxisKey = "Y",
                TrackerFormatString = "{1}: {2:0.0}°C / {4:0.00} g/kg",
            };
            foreach ((double t, double rh) in StatePoints)
            {
                double w = Psychrometrics.HumidityRatio(t, rh / 100.0) * 1000.0;
                scatter.Points.Add(new ScatterPoint(t, w));
            }
            model.Series.Add(scatter);

            return model;
        }

        private static void AddCurve(PlotModel model, Psychrometrics.Curve curve, OxyColor color, double thickness, LineStyle style)
        {
            LineSeries series = new LineSeries
            {
                Title = curve.Name,
                Color = color,
                StrokeThickness = thickness,
                LineStyle = style,
                XAxisKey = "X",
                YAxisKey = "Y",
                CanTrackerInterpolatePoints = true,
            };
            foreach ((double t, double w) in curve.Points)
            {
                series.Points.Add(new DataPoint(t, w));
            }
            model.Series.Add(series);
        }
    }
}
