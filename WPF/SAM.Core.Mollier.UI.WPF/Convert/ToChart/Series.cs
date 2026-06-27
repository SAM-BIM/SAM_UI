// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using OxyPlot;
using OxyPlot.Series;
using SAM.Geometry.Planar;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Core.Mollier.UI
{
    public static partial class Convert
    {
        // OxyPlot port of the WinForms ConstantValueCurve -> Series builder.
        // WinForms Series (ChartType.Line) -> OxyPlot LineSeries; series.Tag carries the curve,
        // series.BorderWidth -> StrokeThickness. The series Title encodes the same metadata string
        // the WinForms version used (data type, value, unit, guid[, phase]).
        public static LineSeries ToChart(this ConstantValueCurve constantValueCurve, PlotModel plotModel, MollierControlSettings mollierControlSettings)
        {
            if (constantValueCurve == null || plotModel == null || mollierControlSettings == null)
            {
                return null;
            }

            List<MollierPoint> mollierPoints = constantValueCurve.MollierPoints;
            if (mollierPoints == null || mollierPoints.Count == 0)
            {
                return null;
            }

            ChartDataType chartDataType = constantValueCurve.ChartDataType;

            List<string> values = new List<string>();
            values.Add(Core.Query.Description(chartDataType));
            values.Add(string.Format("[{0}]", constantValueCurve.Value.ToString()));
            values.Add(Units.Query.Abbreviation(Query.DefaultUnitType(chartDataType)));
            values.Add(Guid.NewGuid().ToString());
            if (constantValueCurve is ConstantTemperatureCurve)
            {
                values.Add(Core.Query.Description(((ConstantTemperatureCurve)constantValueCurve).Phase));
            }
            else if (constantValueCurve is ConstantEnthalpyCurve)
            {
                values.Add(Core.Query.Description(((ConstantEnthalpyCurve)constantValueCurve).Phase));
            }

            string name = string.Join(" ", values);

            LineSeries result = new LineSeries
            {
                Title = name,
                RenderInLegend = false,
                Tag = constantValueCurve,
                StrokeThickness = 1,
                LineStyle = LineStyle.Solid,
            };

            ChartParameterType chartParameterType = Mollier.Query.ChartParameterType(chartDataType, constantValueCurve.Value);

            switch (chartDataType)
            {
                case ChartDataType.RelativeHumidity:
                    result.StrokeThickness = constantValueCurve.Value == 100 ? 2 : 1;
                    break;

                case ChartDataType.DiagramTemperature:
                case ChartDataType.DryBulbTemperature:
                    result.StrokeThickness = chartParameterType == ChartParameterType.Line ? 1 : 2;
                    break;

                case ChartDataType.Enthalpy:
                    result.StrokeThickness = 1;
                    break;

                default:
                    result.StrokeThickness = 1;
                    break;
            }

            result.Color = mollierControlSettings.VisibilitySettings.GetColor(mollierControlSettings.DefaultTemplateName, chartParameterType, chartDataType).ToOxyColor();

            ChartType chartType = mollierControlSettings.ChartType;

            List<Point2D> point2Ds = new List<Point2D>();
            foreach (MollierPoint mollierPoint in mollierPoints)
            {
                double temperature = mollierPoint.DryBulbTemperature;
                if (double.IsNaN(temperature) || double.IsInfinity(temperature))
                {
                    continue;
                }

                double humidityRatio = mollierPoint.HumidityRatio;
                if (double.IsNaN(humidityRatio) || double.IsInfinity(humidityRatio))
                {
                    continue;
                }

                Point2D point2D = ToSAM(new MollierPoint(temperature, humidityRatio, mollierControlSettings.Pressure), chartType);
                if (point2D == null)
                {
                    continue;
                }

                point2Ds.Add(point2D);
                result.Points.Add(new DataPoint(point2D.X, point2D.Y));
            }

            if (chartDataType == ChartDataType.Enthalpy && point2Ds.Count > 1)
            {
                Point2D point2D_1 = point2Ds[0];
                Point2D point2D_2 = point2Ds[point2Ds.Count - 1];

                double factor = result.StrokeThickness == 1 ? 3 : 4; // extend all enthalpy lines in UI

                Vector2D vector2D = new Vector2D(point2D_1, point2D_2);
                point2D_2 = point2D_2.GetMoved(vector2D.Unit * factor);
                result.Points.Add(new DataPoint(point2D_2.X, point2D_2.Y));
            }

            plotModel.Series.Add(result);
            return result;
        }

        public static List<Series> ToChart(this IEnumerable<ConstantValueCurve> constantValueCurves, PlotModel plotModel, MollierControlSettings mollierControlSettings)
        {
            if (constantValueCurves == null || constantValueCurves.Count() == 0 || plotModel == null || mollierControlSettings == null)
            {
                return null;
            }

            List<Series> result = new List<Series>();
            foreach (ConstantValueCurve constantValueCurve in constantValueCurves)
            {
                LineSeries series = constantValueCurve.ToChart(plotModel, mollierControlSettings);
                if (series == null)
                {
                    continue;
                }

                result.Add(series);
            }

            return result;
        }

        public static List<Series> ToChart(ChartDataType chartDataType, PlotModel plotModel, MollierControlSettings mollierControlSettings)
        {
            if (chartDataType == ChartDataType.Undefined || plotModel == null || mollierControlSettings == null)
            {
                return null;
            }

            List<ConstantValueCurve> constantValueCurves = Query.ConstantValueCurves(chartDataType, mollierControlSettings);
            if (constantValueCurves == null || constantValueCurves.Count == 0)
            {
                return null;
            }

            return ToChart(constantValueCurves, plotModel, mollierControlSettings);
        }
    }
}
