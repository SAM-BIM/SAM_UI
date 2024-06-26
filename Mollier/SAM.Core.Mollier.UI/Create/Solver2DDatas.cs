﻿using SAM.Geometry.Mollier;
using SAM.Geometry.Planar;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms.DataVisualization.Charting;

namespace SAM.Core.Mollier.UI
{
    public static partial class Create
    {
        public static List<Solver2DData> Solver2DDatas(this Chart chart, MollierControlSettings mollierControlSettings)
        {
            List<Solver2DData> result = new List<Solver2DData>();
            ChartType chartType = mollierControlSettings.ChartType;
            Vector2D scaleVector = Query.ScaleVector2D(chart.Parent, mollierControlSettings);
            double axesRatio = Query.AxesRatio(chart, mollierControlSettings);

            bool addProcesses = true;
            bool addPoints = true;
            checkQuantity(chart, ref addProcesses, ref addPoints);
            List<UIMollierProcess> uIMollierProcesses = new List<UIMollierProcess>();
            foreach (Series series in chart.Series)
            {
                if (series.Tag is UIMollierZone)
                {
                    UIMollierZone zone = (UIMollierZone)series.Tag;
                    result.AddRange(Solver2DDatas_Zone(zone, chartType, scaleVector, axesRatio));
                    continue;
                }

                if (addPoints && series.Name == "MollierPoints")
                {
                    foreach (DataPoint dataPoint in series.Points)
                    {
                        if (dataPoint.Tag is UIMollierPoint)
                        {
                            UIMollierPoint point = (UIMollierPoint)dataPoint.Tag;
                            result.Add(Solver2DData(point, chartType, scaleVector, axesRatio));
                        }
                    }
                    continue;
                }

                if (addProcesses && series.Tag is UIMollierProcess)
                {
                    UIMollierProcess uIMollierProcess = (UIMollierProcess)series.Tag;
                    if (uIMollierProcess.MollierProcess is CoolingProcess && series.BorderDashStyle == ChartDashStyle.Dash)
                    {
                        continue;
                    }

                    if(uIMollierProcesses.Contains(uIMollierProcess))
                    {
                        continue;
                    }

                    uIMollierProcesses.Add(uIMollierProcess);

                    result.AddRange(Solver2DDatas_Process(uIMollierProcess, chartType, scaleVector, axesRatio, mollierControlSettings));
                    continue;
                }

                if (series.Tag is ConstantValueCurve && !(series.Tag is ConstantTemperatureCurve))
                {
                    ConstantValueCurve curve = (ConstantValueCurve)series.Tag;

                    bool visible = mollierControlSettings.VisibilitySettings.GetVisibilitySetting(mollierControlSettings.DefaultTemplateName, ChartParameterType.Unit, curve.ChartDataType).Visible;
                    if (!visible)
                    {
                        continue;
                    }
                    result.AddRange(Solver2DDatas_CurveUnit(chart, curve, mollierControlSettings, scaleVector, axesRatio));
                    continue;
                }
            }
            result.AddRange(Solver2DDatas_CurveNames(chart, mollierControlSettings));
            return result;
        }
    
        private static List<Solver2DData> Solver2DDatas_Process(UIMollierProcess uIMollierProcess, ChartType chartType, Vector2D scaleVector, double axesRatio, MollierControlSettings mollierControlSettings)
        { 
            List<Solver2DData> result = new List<Solver2DData>();
            if(uIMollierProcess == null)
            {
                return result;
            }
            
            if (mollierControlSettings == null || (mollierControlSettings != null && !mollierControlSettings.DisableLabelProcess))
            {
                UIMollierProcessPoint uIMollierProcessPoint = new UIMollierProcessPoint(uIMollierProcess, ProcessReferenceType.Process);

                Solver2DData solver2DData = Solver2DData(uIMollierProcessPoint, chartType, scaleVector, axesRatio);
                if(solver2DData != null)
                {
                    solver2DData.Tag = uIMollierProcessPoint;
                    result.Add(solver2DData);
                }

            }

            if(mollierControlSettings == null || (mollierControlSettings != null && !mollierControlSettings.DisableLabelStartProcessPoint))
            {
                //UIMollierPoint start = new UIMollierPoint(uIMollierProcess.Start, new UIMollierPointAppearance(Color.Empty, Color.Black, uIMollierProcess.UIMollierPointAppearance_Start.Label));

                UIMollierProcessPoint uIMollierProcessPoint = new UIMollierProcessPoint(uIMollierProcess, ProcessReferenceType.Start);
                Solver2DData solver2DData = Solver2DData(uIMollierProcessPoint, chartType, scaleVector, axesRatio);
                
                if(solver2DData != null)
                {
                    solver2DData.Tag = uIMollierProcessPoint;
                    result.Add(solver2DData);
                }
            }

            if (mollierControlSettings == null || (mollierControlSettings != null && !mollierControlSettings.DisableLabelEndProcessPoint))
            {
                //UIMollierPoint end = new UIMollierPoint(uIMollierProcess.End, new UIMollierPointAppearance(Color.Empty, Color.Black, uIMollierProcess.UIMollierPointAppearance_End.Label));

                UIMollierProcessPoint uIMollierProcessPoint = new UIMollierProcessPoint(uIMollierProcess, ProcessReferenceType.End);
                Solver2DData solver2DData = Solver2DData(uIMollierProcessPoint, chartType, scaleVector, axesRatio);
                if(solver2DData != null)
                {
                    solver2DData.Tag = uIMollierProcessPoint;
                    result.Add(solver2DData);
                }
            }

            return result;
        }
        private static List<Solver2DData> Solver2DDatas_Zone(UIMollierZone uIMollierZone, ChartType chartType, Vector2D scaleVector, double axesRatio)
        {
            List<Solver2DData> result = new List<Solver2DData>();
            if (uIMollierZone == null)
            {
                return result;
            }

            UIMollierPointAppearance zoneCenterAppearance = new UIMollierPointAppearance(System.Drawing.Color.Empty, uIMollierZone.UIMollierAppearance.Color, (uIMollierZone.UIMollierAppearance as UIMollierAppearance).Label);
            UIMollierPoint zoneCenter;
            if(chartType == ChartType.Mollier)
            {
                MollierPoint center = new MollierPoint(uIMollierZone.GetCenter().DryBulbTemperature - 0.8 * scaleVector.Y, uIMollierZone.GetCenter().HumidityRatio, uIMollierZone.GetCenter().Pressure);
                zoneCenter = new UIMollierPoint(center, zoneCenterAppearance);
            }
            else
            {
                double pressure = uIMollierZone.MollierPoints[0].Pressure;
                List<double> drybulbTemperatures = uIMollierZone.MollierPoints.ConvertAll(x => x.DryBulbTemperature);
                List<double> humidityRatios = uIMollierZone.MollierPoints.ConvertAll(x => x.HumidityRatio);

                MollierPoint center = new MollierPoint(drybulbTemperatures.Average(), humidityRatios.Average(), pressure);

                //new MollierPoint(uIMollierZone.GetCenter().DryBulbTemperature, uIMollierZone.GetCenter().HumidityRatio - 0.4 * scaleVector.X, uIMollierZone.GetCenter().Pressure);
                zoneCenter = new UIMollierPoint(center, zoneCenterAppearance);
            }

            result.Add(Solver2DData(zoneCenter, chartType, scaleVector, axesRatio));
            return result;
        }
        private static List<Solver2DData> Solver2DDatas_CurveUnit(Chart chart, ConstantValueCurve curve, MollierControlSettings mollierControlSettings, Vector2D scaleVector, double axesRatio)
        {
            List<Solver2DData> result = new List<Solver2DData>();
            ChartType chartType = mollierControlSettings.ChartType;

            if (mollierControlSettings.DisableUnits || (curve is ConstantEnthalpyCurve && curve.Value % 10000 != 0))
            {
                return result;
            }

            string text = getCurveUnitText(curve);
            Color color = mollierControlSettings.VisibilitySettings.GetColor(mollierControlSettings.DefaultTemplateName, ChartParameterType.Unit, curve.ChartDataType);

            Point2D defaultPoint2D = Query.DefaultLabelPoint2D(mollierControlSettings, curve, chartType);
            if (defaultPoint2D == null) return result;

            Polyline2D polyline = curveToPolyline(curve, chartType, axesRatio);
            if (curve.ChartDataType == ChartDataType.Enthalpy)
            {
                polyline = extendEnthalpyCurve(curve, defaultPoint2D, chartType, axesRatio);
            }

            Rectangle2D rectangle = getLabelRectangle(defaultPoint2D, polyline, text, mollierControlSettings, scaleVector, axesRatio);
            if (rectangle == null) return result;

            Solver2DData solver2DData = new Solver2DData(rectangle, polyline);
            solver2DData.Tag = new UIMollierPoint(Convert.ToMollier(rectangle.GetCentroid(), chartType, mollierControlSettings.Pressure), new UIMollierPointAppearance(System.Drawing.Color.Empty, color, text));
            solver2DData.Priority = getChartDataTypePriority(curve.ChartDataType);

            Solver2DSettings solver2DSettings = new Solver2DSettings();
            solver2DSettings.IterationCount = 1000;
            solver2DSettings.ShiftDistance = 0.1 * scaleVector.X;
            solver2DData.Solver2DSettings = solver2DSettings;

            result.Add(solver2DData);

            return result;
        }
        private static List<Solver2DData> Solver2DDatas_CurveNames(Chart chart, MollierControlSettings mollierControlSettings)
        {
            if (mollierControlSettings.DisableLabels) return new List<Solver2DData>();

            List<Solver2DData> result = new List<Solver2DData>();
            ChartType chartType = mollierControlSettings.ChartType;
            Vector2D scaleVector = Query.ScaleVector2D(chart.Parent, mollierControlSettings);
            double axesRatio = Query.AxesRatio(chart, mollierControlSettings);

            Dictionary<ChartDataType, List<ConstantValueCurve>> orderedCurves = orderCurves(chart, chartType);

            foreach (KeyValuePair<ChartDataType, List<ConstantValueCurve>> curves in orderedCurves)
            {
                ChartDataType chartDataType = curves.Key;
                ConstantValueCurve curve = curves.Value[curves.Value.Count / 2]; //MD here we define which line to label on chart
                string text = getCurveNameText(chartDataType);

                bool visible = mollierControlSettings.VisibilitySettings.GetVisibilitySetting(mollierControlSettings.DefaultTemplateName, ChartParameterType.Label, curve.ChartDataType).Visible;
                if (!visible)
                {
                    continue;
                }
                Point2D defaultPoint2D = Query.DefaultLabelPoint2D(mollierControlSettings, curve, chartType, ChartParameterType.Label);
                if (defaultPoint2D == null)
                {
                    return result;
                }
                Polyline2D polyline = curveToPolyline(curve, chartType, axesRatio);
                Rectangle2D rectangle = getLabelRectangle(defaultPoint2D, polyline, text, mollierControlSettings, scaleVector, axesRatio);

                Solver2DData solver2DData = new Solver2DData(rectangle, polyline);

                Color color = mollierControlSettings.VisibilitySettings.GetColor(mollierControlSettings.DefaultTemplateName, ChartParameterType.Label, curve.ChartDataType);
                solver2DData.Tag = new UIMollierPoint(Convert.ToMollier(defaultPoint2D, chartType, mollierControlSettings.Pressure), new UIMollierPointAppearance(System.Drawing.Color.Empty, color, text));
                solver2DData.Priority = getChartDataTypePriority(chartDataType);

                Solver2DSettings solver2DSettings = new Solver2DSettings();
                solver2DSettings.IterationCount = 100;
                solver2DSettings.ShiftDistance = 0.1 * scaleVector.X;
                solver2DData.Solver2DSettings = solver2DSettings;

                result.Add(solver2DData);
            }

            return result;
        }


        private static void checkQuantity(Chart chart, ref bool addProcesses, ref bool addPoints)
        {
            int numberOfProcesses = 0;
            int numberOfPoints = 0;
            foreach (Series series in chart.Series)
            {
                if (series.Tag is UIMollierProcess)
                {
                    numberOfProcesses++;
                }
                if (series.Name == "MollierPoints")
                {
                    numberOfPoints++;
                }
            }
            addProcesses = numberOfProcesses > 30 ? false : true;
            addPoints = numberOfPoints > 30 ? false : true;
        }

        // TODO: [LABELS] Methods used above and to move from there
        private static Rectangle2D getLabelRectangle(Point2D point, Polyline2D polyline, string text, MollierControlSettings mollierControlSettings, Vector2D scaleVector, double axesRatio)
        {
            ChartType chartType = mollierControlSettings.ChartType;
            Point2D labelCenter = getLabelCenter(point, chartType, scaleVector);
            Point2D scaledPoint = point.GetScaledY(axesRatio);

            Rectangle2D rectangleShape = textToRectangle(labelCenter, text, chartType, scaleVector, axesRatio);
            if (rectangleShape == null) return null;

            List<Segment2D> segments = polyline.ClosestSegment2Ds(scaledPoint);
            if (segments == null) return null;

            Segment2D curveSegment = segments[0];
            double distance = rectangleShape.GetCentroid().Y - point.GetScaledY(axesRatio).Y;
            bool clockwise = curveSegment.Direction.GetPerpendicular().Y < 0;

            Rectangle2D result = Geometry.Planar.Query.MoveToSegment2D(rectangleShape, curveSegment, scaledPoint, distance, clockwise);
            return fixRectangleShape(result, rectangleShape);
        }

        private static Dictionary<ChartDataType, List<ConstantValueCurve>> orderCurves(Chart chart, ChartType chartType)
        {
            Dictionary<ChartDataType, List<ConstantValueCurve>> result = new Dictionary<ChartDataType, List<ConstantValueCurve>>();

            foreach (Series series in chart.Series)
            {
                if (series.Tag is ConstantValueCurve && !(series.Tag is ConstantTemperatureCurve))
                {
                    ConstantValueCurve curve = (ConstantValueCurve)series.Tag;
                    if (onChart(curve, chart, chartType))
                    {
                        if (curve.ChartDataType == ChartDataType.Enthalpy && curve.Value % 10000 != 0) continue;

                        if (!result.ContainsKey(curve.ChartDataType))
                        {
                            result.Add(curve.ChartDataType, new List<ConstantValueCurve>());
                        }
                        result[curve.ChartDataType].Add(curve);
                    }

                }
            }

            return result;
        }

        private static Point2D getLabelCenter(Point2D point, ChartType chartType, Vector2D scaleVector)
        {
            // Method returns point's label
            if (chartType == ChartType.Mollier)
            {
                return new Point2D(point.X, point.Y + 0.7 * scaleVector.Y);
            }
            else
            {
                return new Point2D(point.X, point.Y + 0.35 * scaleVector.Y);
            }
        }

        private static Rectangle2D textToRectangle(Point2D center, string text, ChartType chartType, Vector2D scaleVector, double axesRatio)
        {
            double capitalLetterHeight = (chartType == ChartType.Mollier ? 0.8 : 0.45) * scaleVector.Y;
            double lowercaseHeight = (chartType == ChartType.Mollier ? 0.8 : 0.4) * scaleVector.Y;
            double letterWidth = (chartType == ChartType.Mollier ? 0.1 : 0.2) * scaleVector.X;

            double width = letterWidth * text.Length;
            double height = containsCapitalLetter(text) ? capitalLetterHeight : lowercaseHeight;

            List<Point2D> rectanglePoints = new List<Point2D>();
            rectanglePoints.Add(new Point2D(center.X - width / 2, center.Y - height / 2).GetScaledY(axesRatio));
            rectanglePoints.Add(new Point2D(center.X - width / 2, center.Y + height / 2).GetScaledY(axesRatio));
            rectanglePoints.Add(new Point2D(center.X + width / 2, center.Y - height / 2).GetScaledY(axesRatio));
            rectanglePoints.Add(new Point2D(center.X + width / 2, center.Y + height / 2).GetScaledY(axesRatio));

            return Geometry.Planar.Create.Rectangle2D(rectanglePoints);
        }

        private static bool containsCapitalLetter(string text)
        {
            return !text.ToLower().Equals(text);
        }

        private static Polyline2D curveToPolyline(ConstantValueCurve curve, ChartType chartType, double axesRatio = 1)
        {
            if(curve.MollierPoints == null || curve.MollierPoints.Count < 2)
            {
                return null;
            }

            List<Segment2D> segments = new List<Segment2D>();
            for (int i = 0; i < curve.MollierPoints.Count - 1; i++)
            {
                Point2D start = Convert.ToSAM(curve.MollierPoints[i], chartType).GetScaledY(axesRatio);
                Point2D end = Convert.ToSAM(curve.MollierPoints[i + 1], chartType).GetScaledY(axesRatio);

                segments.Add(new Segment2D(start, end));
            }

            return new Polyline2D(segments);
        }

        private static string getCurveUnitText(ConstantValueCurve curve)
        {
            if (curve == null)
            {
                return "";
            }
            switch (curve.ChartDataType)
            {
                case ChartDataType.RelativeHumidity:
                    return curve.Value.ToString() + "%";

                case ChartDataType.Enthalpy:
                    return (curve.Value / 1000).ToString();

                default:
                    return curve.Value.ToString();
            }
        }

        private static string getCurveNameText(ChartDataType chartDataType)
        {
            switch (chartDataType)
            {
                case ChartDataType.RelativeHumidity:
                    return "φ [%]";//"Relative Humidity φ [%]"

                case ChartDataType.Density:
                    return "ρ [kg/m³]";//"Density ρ [kg/m³]"

                case ChartDataType.Enthalpy:
                    return "h [kJ/kg]";//"Enthalpy h [kJ/kg]"

                case ChartDataType.SpecificVolume:
                    return "v [m³/kg]";//Specific volume v [m³/kg]

                case ChartDataType.WetBulbTemperature:
                    return "t_wb [°C]";//"Wet Bulb Temperature t_wb [°C]"

                default:
                    return "";
            }
        }

        private static Polyline2D extendEnthalpyCurve(ConstantValueCurve curve, Point2D newEnd, ChartType chartType, double axesRatio)
        {
            Point2D start = Convert.ToSAM(curve.Start, chartType).GetScaledY(axesRatio);
            Segment2D segment = new Segment2D(start, newEnd.GetScaledY(axesRatio));

            return new Polyline2D(new List<Segment2D>() { segment });
        }

        private static int getChartDataTypePriority(ChartDataType chartDataType)
        {
            switch (chartDataType)
            {
                case ChartDataType.HeatingProcess:
                    return 1;

                case ChartDataType.CoolingProcess:
                    return 1;

                case ChartDataType.HeatRecoveryProcess:
                    return 1;

                case ChartDataType.MixingProcess:
                    return 1;

                case ChartDataType.AdiabaticHumidificationProcess:
                    return 1;

                case ChartDataType.SteamHumidificationProcess:
                    return 1;

                case ChartDataType.RoomProcess:
                    return 1;

                case ChartDataType.RelativeHumidity:
                    return 2;

                case ChartDataType.Enthalpy:
                    return 3;

                case ChartDataType.Density:
                    return 4;

                case ChartDataType.SpecificVolume:
                    return 5;

                case ChartDataType.WetBulbTemperature:
                    return 6;

                default: return 10;
            }
        }

        private static bool onChart(ConstantValueCurve curve, Chart chart, ChartType chartType)
        {
            Point2D chartMinPoint = new Point2D(chart.ChartAreas[0].AxisX.Minimum, chart.ChartAreas[0].AxisY.Minimum);
            Point2D chartMaxPoint = new Point2D(chart.ChartAreas[0].AxisX.Maximum, chart.ChartAreas[0].AxisY.Maximum);
            Rectangle2D chartArea = new Rectangle2D(new BoundingBox2D(chartMinPoint, chartMaxPoint));

            Segment2D segment = new Segment2D(Convert.ToSAM(curve.Start, chartType), Convert.ToSAM(curve.End, chartType));
            return chartArea.InRange(segment);
        }
        private static Rectangle2D fixRectangleShape(Rectangle2D calculatedRectangle, Rectangle2D shapeRectangle)
        {
            // Calculated rectangle should have same shape
            // Especially they should have same width and heigth
            if (calculatedRectangle == null || shapeRectangle == null)
            {
                return calculatedRectangle;
            }
            if (System.Math.Abs(shapeRectangle.Width - calculatedRectangle.Width) < Tolerance.MacroDistance)
            {
                return calculatedRectangle;
            }

            Rectangle2D result = new Rectangle2D(calculatedRectangle.Origin, -calculatedRectangle.Height, calculatedRectangle.Width, calculatedRectangle.WidthDirection);
            return result;
        }

    }
}
