﻿using SAM.Geometry.Planar;
using System;

namespace SAM.Core.Mollier.UI
{
    public static partial class Query
    {
        /// <summary>
        /// Returns default position of label for constant value
        /// curve depends on chart parameter type.
        /// </summary>
        /// <param name="mollierControlSettings"></param>
        /// <param name="curve"></param>
        /// <param name="chartType"></param>
        /// <param name="chartParameterType"></param>
        /// <returns>Default position of curve label</returns>
        /// <exception cref="NotSupportedException"></exception>
        /// <exception cref="NotImplementedException"></exception>
        public static Point2D DefaultLabelPoint2D(MollierControlSettings mollierControlSettings, ConstantValueCurve curve, ChartType chartType, ChartParameterType chartParameterType = ChartParameterType.Unit)
        {
            if (curve == null)
            {
                return null;
            }
            Point2D start = Convert.ToSAM(curve.Start, chartType);
            Point2D end = Convert.ToSAM(curve.End, chartType);

            if (chartType == ChartType.Mollier)
            {
                if (chartParameterType == ChartParameterType.Label)
                {
                    switch (curve.ChartDataType)
                    {
                        case ChartDataType.RelativeHumidity:
                            Point2D point = defaultRelativeHumidityLocations(curve.Value, chartType, chartParameterType); // fix to diagram temperature
                            if (point == null)
                            {
                                return point;
                            }

                            double relativeHumidity = curve.Value;
                            double humidityRatio = point.X / 1000;
                            double pressure = mollierControlSettings.Pressure;

                            double dryBulbTemperature = Mollier.Query.DryBulbTemperature_ByHumidityRatio(humidityRatio, relativeHumidity, pressure);

                            MollierPoint mollierPoint = new MollierPoint(dryBulbTemperature, humidityRatio, pressure);
                            point = Convert.ToSAM(mollierPoint, chartType);
                            return fix(start, end, point);
                        //Point2D point = defaultRelativeHumidityLocations(curve.Value, chartType, chartParameterType);
                        //if (point == null) return point;
                        //point = new Point2D(point.X, Mollier.Query.DiagramTemperature(point.Y, point.X / 1000, mollierControlSettings.Pressure));
                        //return fix(start, end, point);
                        case ChartDataType.Density:
                            return onSegment(start, end, 15);
                        case ChartDataType.Enthalpy:
                            return onSegment(start, end, 30);
                        case ChartDataType.SpecificVolume:
                            return onSegment(start, end, 70);
                        case ChartDataType.WetBulbTemperature:
                            return onSegment(start, end, 30);

                        default: throw new NotSupportedException();
                    }
                }
                else if (chartParameterType == ChartParameterType.Unit)
                {
                    switch (curve.ChartDataType)
                    {
                        case ChartDataType.RelativeHumidity:
                            Point2D point = defaultRelativeHumidityLocations(curve.Value, chartType, chartParameterType); // fix to diagram temperature
                            if (point == null)
                            {
                                return point;
                            }

                            double relativeHumidity = curve.Value;
                            double humidityRatio = point.X / 1000;
                            double pressure = mollierControlSettings.Pressure;

                            double dryBulbTemperature = Mollier.Query.DryBulbTemperature_ByHumidityRatio(humidityRatio, relativeHumidity, pressure);

                            MollierPoint mollierPoint = new MollierPoint(dryBulbTemperature, humidityRatio, pressure);
                            point = Convert.ToSAM(mollierPoint, chartType);
                            return fix(start, end, point);

                            //point = new Point2D(point.X, Mollier.Query.DiagramTemperature(point.Y, point.X / 1000, mollierControlSettings.Pressure));
                            //return fix(start, end, point);
                        case ChartDataType.Density:
                            return start;
                        case ChartDataType.Enthalpy:
                            return shiftEnthalpyEnd(start, end);
                        case ChartDataType.SpecificVolume:
                            return onSegment(start, end, 85);
                        case ChartDataType.WetBulbTemperature:
                            return end;

                        default: throw new NotSupportedException();
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            else
            {
                if (chartParameterType == ChartParameterType.Label)
                {
                    switch (curve.ChartDataType)
                    {
                        case ChartDataType.RelativeHumidity:
                            Point2D point = defaultRelativeHumidityLocations(curve.Value, chartType, chartParameterType); // fix to diagram temperature
                            if (point == null)
                            {
                                return point;
                            }

                            double relativeHumidity = curve.Value;
                            double humidityRatio = point.Y / 1000;
                            double pressure = mollierControlSettings.Pressure;

                            double dryBulbTemperature = Mollier.Query.DryBulbTemperature_ByHumidityRatio(humidityRatio, relativeHumidity, pressure);

                            MollierPoint mollierPoint = new MollierPoint(dryBulbTemperature, humidityRatio, pressure);
                            point = Convert.ToSAM(mollierPoint, chartType);
                            return fix(start, end, point);
                        //return defaultRelativeHumidityLocations(curve.Value, chartType, chartParameterType);
                        case ChartDataType.Density:
                            return onSegment(start, end, 15);
                        case ChartDataType.Enthalpy:
                            return onSegment(start, end, 30);
                        case ChartDataType.SpecificVolume:
                            return onSegment(start, end, 50);
                        case ChartDataType.WetBulbTemperature:
                            return onSegment(start, end, 30);

                        default: throw new NotSupportedException();
                    }
                }
                else if (chartParameterType == ChartParameterType.Unit)
                {
                    switch (curve.ChartDataType)
                    {
                        case ChartDataType.RelativeHumidity:
                            Point2D point = defaultRelativeHumidityLocations(curve.Value, chartType, chartParameterType); // fix to diagram temperature
                            if (point == null)
                            {
                                return point;
                            }

                            double relativeHumidity = curve.Value;
                            double humidityRatio = point.Y / 1000;
                            double pressure = mollierControlSettings.Pressure;

                            double dryBulbTemperature = Mollier.Query.DryBulbTemperature_ByHumidityRatio(humidityRatio, relativeHumidity, pressure);

                            MollierPoint mollierPoint = new MollierPoint(dryBulbTemperature, humidityRatio, pressure);
                            point = Convert.ToSAM(mollierPoint, chartType);
                            return fix(start, end, point);
                            //Point2D point = defaultRelativeHumidityLocations(curve.Value, chartType, chartParameterType);
                            //return fix(start, end, point);
                        case ChartDataType.Density:
                            return start;
                        case ChartDataType.Enthalpy:
                            return shiftEnthalpyEnd(start, end);
                        case ChartDataType.SpecificVolume:
                            return onSegment(start, end, 85);
                        case ChartDataType.WetBulbTemperature:
                            return end;

                        default: throw new NotSupportedException();
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }


        private static Point2D defaultRelativeHumidityLocations(double value, ChartType chartType, ChartParameterType chartParameterType)
        {
            if (chartType == ChartType.Mollier)
            {
                if (chartParameterType == ChartParameterType.Label)
                {
                    return new Point2D(21, 38.5);
                }
                else if (chartParameterType == ChartParameterType.Unit)
                {
                    switch (value)
                    {
                        case 0:
                            return null;
                        case 10:
                            //return new Point2D(4.6, 40.15);
                            return new Point2D(6, 46);
                        case 20:
                            return new Point2D(12, 46);
                        case 30:
                            return new Point2D(18, 45.5);
                        case 40:
                            return new Point2D(23, 44.5);
                        case 50:
                            return new Point2D(27.5, 43);
                        case 60:
                            return new Point2D(30, 41.0);
                        case 70:
                            return new Point2D(31.5, 39.5);
                        case 80:
                            return new Point2D(32.5, 37.5);
                        case 90:
                            return new Point2D(33.25, 35.5);
                        case 100:
                            return new Point2D(34, 34);
                        default:
                            return null;
                    }
                }
                else
                {
                    throw new Exception("Wrong chart parameter, it should be label or unit");
                }
            }
            else
            {
                if (chartParameterType == ChartParameterType.Label)
                {
                    return new Point2D(45, 30.88);
                }
                else if (chartParameterType == ChartParameterType.Unit)
                {
                    switch (value)
                    {
                        case 0:
                            return null;
                        case 10:
                            return new Point2D(48, 9);
                        case 20:
                            return new Point2D(48, 14.1);
                        case 30:
                            return new Point2D(48, 21.5);
                        case 40:
                            return new Point2D(47, 27);
                        case 50:
                            return new Point2D(44.2, 30);
                        case 60:
                            return new Point2D(41.5, 31);
                        case 70:
                            return new Point2D(39, 31.5);
                        case 80:
                            return new Point2D(37, 32);
                        case 90:
                            return new Point2D(35, 32.5);
                        case 100:
                            return new Point2D(33, 32.5);
                        default:
                            return null;
                    }
                }
                else
                {
                    throw new Exception("Wrong chart parameter, it should be label or unit");
                }
            }
        }
        private static Point2D shiftEnthalpyEnd(Point2D start, Point2D end)
        {
            double distanceFromEnd = 2.2;
            Vector2D vector = new Vector2D(start, end).Unit;

            return end.GetMoved(vector * distanceFromEnd);
        }
        private static Point2D onSegment(Point2D point1, Point2D point2, double percent)
        {
            if (point1 == null || point2 == null || percent < 0 || percent > 100) return null;

            double x = point1.X + (percent / 100) * (point2.X - point1.X);
            if(x < System.Math.Min(point1.X, point2.X) || x > System.Math.Max(point1.X, point2.X))
            {
                return null;
            }

            double a = (point2.Y - point1.Y) / (point2.X - point1.X);
            double b = point1.Y - a * point1.X;

            return new Point2D(x, a * x + b);

        }
        private static Point2D fix(Point2D start, Point2D end, Point2D point)
        {
            if(point == null || start == null || end == null)
            {
                return null;
            }

            if (point.X < System.Math.Min(start.X, end.X))
            {
                point.X = System.Math.Min(start.X, end.X);
            }
            else if (point.X > System.Math.Max(start.X, end.X))
            {
                point.X = System.Math.Max(start.X, end.X);
            }
            if (point.Y < System.Math.Min(start.Y, end.Y))
            {
                point.Y = System.Math.Min(start.Y, end.Y);
            }
            else if (point.Y > System.Math.Max(start.Y, end.Y))
            {
                point.Y = System.Math.Max(start.Y, end.Y);
            }
            return point;
        }
    }
}
