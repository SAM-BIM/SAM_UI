// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using OxyPlot;
using OxyPlot.Series;
using SAM.Geometry.Mollier;
using SAM.Geometry.Planar;
using System;
using System.Collections.Generic;
using SystemColor = System.Drawing.Color;

namespace SAM.Core.Mollier.UI
{
    public static partial class Modify
    {
        // ------------------GENERAL-ADD-PROCESSES-METHOD---------------------- (OxyPlot port)
        /// <summary>
        /// Creates series for all the processes on the chart.
        /// </summary>
        public static List<Series> AddMollierProcesses(this PlotModel plotModel, List<IMollierGroup> groups, MollierControlSettings mollierControlSettings)
        {
            List<Series> result = new List<Series>();
            if (groups == null)
            {
                return null;
            }

            ChartType chartType = mollierControlSettings.ChartType;
            List<UIMollierProcess> labeledMollierProcesses = generateLabels(groups);

            labeledMollierProcesses?.Sort((x, y) => System.Math.Max(x.Start.HumidityRatio, x.End.HumidityRatio).CompareTo(System.Math.Max(y.Start.HumidityRatio, y.End.HumidityRatio)));

            foreach (UIMollierProcess uIMollierProcess in labeledMollierProcesses)
            {
                MollierProcess mollierProcess = uIMollierProcess.MollierProcess;
                MollierPoint start = mollierProcess?.Start;
                MollierPoint end = mollierProcess?.End;

                if (start == null || end == null)
                {
                    continue;
                }

                bool visible = true;

                IVisibilitySetting visibilitySetting = mollierControlSettings.VisibilitySettings.GetVisibilitySetting(mollierControlSettings.DefaultTemplateName, ChartParameterType.Line, mollierProcess.ChartDataType);
                if (visibilitySetting != null)
                {
                    visible = visibilitySetting.Visible;
                }

                if (!visible)
                {
                    continue;
                }

                if (mollierProcess is UndefinedProcess)
                {
                    createUndefinedProcessSeries(plotModel, uIMollierProcess, mollierControlSettings);
                    continue;
                }

                if (mollierProcess is RoomProcess)
                {
                    createRoomProcessSeries(plotModel, uIMollierProcess, mollierControlSettings);
                    continue;
                }

                createProcessSeries(plotModel, uIMollierProcess, mollierControlSettings);

                if (!mollierControlSettings.DisableStartProcessPoint)
                {
                    UIMollierPoint uIMollierPoint_Start = uIMollierProcess.GetUIMollierPoint_Start();
                    AddMollierPoint(plotModel, chartType, uIMollierPoint_Start, mollierControlSettings, DisplayPointType.Process);
                }

                if (!mollierControlSettings.DisableEndProcessPoint)
                {
                    UIMollierPoint uIMollierPoint_End = uIMollierProcess.GetUIMollierPoint_End();
                    AddMollierPoint(plotModel, chartType, uIMollierPoint_End, mollierControlSettings, DisplayPointType.Process);
                }

                if (!mollierControlSettings.DisableCoolingAuxiliaryProcesses)
                {
                    // cooling process create one unique process with ADP point
                    if (labeledMollierProcesses.Count < 30 && mollierProcess is CoolingProcess)
                    {
                        createCoolingAdditionalLines(plotModel, uIMollierProcess, mollierControlSettings);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Creates series for all the processes on the chart.
        /// </summary>
        public static List<Series> AddMollierProcesses(this PlotModel plotModel, MollierModel mollierModel, MollierControlSettings mollierControlSettings)
        {
            if (mollierModel == null)
            {
                return null;
            }

            List<UIMollierProcess> uIMollierProcesses = mollierModel.GetMollierObjects<UIMollierProcess>();
            uIMollierProcesses = uIMollierProcesses?.FindAll(x => x.UIMollierAppearance.Visible == true);

            List<IMollierGroup> mollierGroups = new List<IMollierGroup>();

            if (uIMollierProcesses?.Count < 30)
            {
                mollierGroups = uIMollierProcesses?.Group();
            }
            else
            {
                MollierGroup mollierGroup = new MollierGroup("New Group");
                uIMollierProcesses?.ForEach(x => mollierGroup.Add(x));
                mollierGroups.Add(mollierGroup);
            }

            return plotModel.AddMollierProcesses(mollierGroups, mollierControlSettings);
        }

        // ------------------SERIES-------------------------------------------- (OxyPlot port)
        private static Series createDewPointDashLineSeries(PlotModel plotModel, MollierPoint mollierPoint_1, MollierPoint mollierPoint_2, UIMollierProcess uIMollierProcess, ChartType chartType, SystemColor color, int borderWidth, LineStyle lineStyle)
        {
            // Additional dash line in a Cooling process to the ADP
            Point2D point2D_1 = Convert.ToSAM(mollierPoint_1, chartType);
            Point2D point2D_2 = Convert.ToSAM(mollierPoint_2, chartType);

            LineSeries series = new LineSeries
            {
                Title = Guid.NewGuid().ToString(),
                Color = color.ToOxyColor(),
                RenderInLegend = false,
                StrokeThickness = borderWidth,
                LineStyle = lineStyle,
                Tag = uIMollierProcess,
            };
            series.Points.Add(new DataPoint(point2D_1.X, point2D_1.Y));
            series.Points.Add(new DataPoint(point2D_2.X, point2D_2.Y));

            plotModel.Series.Add(series);
            return series;
        }

        private static List<Series> createRoomProcessSeries(PlotModel plotModel, UIMollierProcess uIMollierProcess, MollierControlSettings mollierControlSettings)
        {
            List<Series> result = new List<Series>();

            MollierProcess mollierProcess = uIMollierProcess.MollierProcess;
            uIMollierProcess.UIMollierPointAppearance_End.Label = "ROOM";
            ChartType chartType = mollierControlSettings.ChartType;
            SystemColor color = uIMollierProcess.UIMollierAppearance.Color == SystemColor.Empty ? SystemColor.Gray : uIMollierProcess.UIMollierAppearance.Color;

            MollierLineSeries series = new MollierLineSeries
            {
                Title = Guid.NewGuid().ToString(),
                RenderInLegend = false,
                Color = color.ToOxyColor(),
                LineStyle = LineStyle.Dash,
                StrokeThickness = mollierControlSettings.ProccessLineThickness != -1 ? mollierControlSettings.ProccessLineThickness : 3,
                Tag = uIMollierProcess,
            };

            MollierPoint mollierPoint_Start = mollierProcess.Start;
            Point2D point2D_Start = Convert.ToSAM(mollierPoint_Start, chartType);
            MollierPoint mollierPoint_End = mollierProcess.End;
            Point2D point2D_End = Convert.ToSAM(mollierPoint_End, chartType);

            series.AddPoint(point2D_Start.X, point2D_Start.Y, mollierPoint_Start);
            series.AddPoint(point2D_End.X, point2D_End.Y, mollierPoint_End);
            series.TrackerFormatString = Query.ToolTipText(mollierPoint_Start, mollierPoint_End, mollierControlSettings.ChartType, Query.FullProcessName(uIMollierProcess));
            plotModel.Series.Add(series);

            // Room air-condition point (triangle marker)
            ScatterSeries seriesRoomPoint = new ScatterSeries
            {
                Title = Guid.NewGuid().ToString(),
                RenderInLegend = false,
                MarkerType = MarkerType.Triangle,
                MarkerFill = OxyColors.Gray,
                MarkerStroke = color.ToOxyColor(),
                MarkerStrokeThickness = 2,
                MarkerSize = 8,
                Tag = mollierProcess,
                TrackerFormatString = Query.ToolTipText(mollierPoint_End, mollierControlSettings.ChartType),
            };
            seriesRoomPoint.Points.Add(new ScatterPoint(point2D_End.X, point2D_End.Y, 8, double.NaN, mollierPoint_End));
            plotModel.Series.Add(seriesRoomPoint);

            if (!string.IsNullOrWhiteSpace(uIMollierProcess?.UIMollierPointAppearance_Start?.Label))
            {
                UIMollierPoint uIMollierPoint = new UIMollierPoint(uIMollierProcess.Start, Create.UIMollierPointAppearance(DisplayPointType.Room));
                AddMollierPoint(plotModel, chartType, uIMollierPoint, mollierControlSettings);
            }

            result.Add(series);
            result.Add(seriesRoomPoint);
            return result;
        }

        private static List<Series> createUndefinedProcessSeries(PlotModel plotModel, UIMollierProcess uIMollierProcess, MollierControlSettings mollierControlSettings)
        {
            List<Series> result = new List<Series>();

            ChartType chartType = mollierControlSettings.ChartType;
            SystemColor color = uIMollierProcess.UIMollierAppearance.Color == SystemColor.Empty ? SystemColor.Gray : uIMollierProcess.UIMollierAppearance.Color;

            MollierLineSeries series = new MollierLineSeries
            {
                Title = Guid.NewGuid().ToString(),
                RenderInLegend = false,
                Color = color.ToOxyColor(),
                LineStyle = LineStyle.Dash,
                StrokeThickness = mollierControlSettings.ProccessLineThickness != -1 ? mollierControlSettings.ProccessLineThickness : 3,
                Tag = uIMollierProcess,
            };

            Point2D start = Convert.ToSAM(uIMollierProcess.Start, chartType);
            Point2D end = Convert.ToSAM(uIMollierProcess.End, chartType);

            series.AddPoint(start.X, start.Y, uIMollierProcess.Start);
            series.AddPoint(end.X, end.Y, uIMollierProcess.End);

            plotModel.Series.Add(series);
            result.Add(series);
            return result;
        }

        private static Series createProcessSeries(PlotModel plotModel, UIMollierProcess uIMollierProcess, MollierControlSettings mollierControlSettings)
        {
            ChartType chartType = mollierControlSettings.ChartType;
            MollierProcess mollierProcess = uIMollierProcess.MollierProcess;
            MollierPoint mollierPoint_Start = mollierProcess.Start;
            Point2D point2D_Start = Convert.ToSAM(mollierPoint_Start, chartType);
            MollierPoint mollierPoint_End = mollierProcess.End;
            Point2D point2D_End = Convert.ToSAM(mollierPoint_End, chartType);

            SystemColor color = mollierControlSettings.VisibilitySettings.GetColor(mollierControlSettings.DefaultTemplateName, ChartParameterType.Line, mollierProcess);
            if (uIMollierProcess.UIMollierAppearance != null && uIMollierProcess.UIMollierAppearance.Color != SystemColor.Empty)
            {
                color = uIMollierProcess.UIMollierAppearance.Color;
            }

            MollierLineSeries series = new MollierLineSeries
            {
                Title = Guid.NewGuid().ToString(),
                RenderInLegend = false,
                StrokeThickness = mollierControlSettings.ProccessLineThickness != -1 ? mollierControlSettings.ProccessLineThickness : 4,
                Color = color.ToOxyColor(),
                Tag = uIMollierProcess,
                TrackerFormatString = Query.ToolTipText(mollierPoint_Start, mollierPoint_End, chartType, Query.FullProcessName(mollierProcess)),
            };

            series.AddPoint(point2D_Start.X, point2D_Start.Y, new UIMollierProcessPoint(uIMollierProcess, ProcessReferenceType.Start));
            series.AddPoint(point2D_End.X, point2D_End.Y, new UIMollierProcessPoint(uIMollierProcess, ProcessReferenceType.End));
            plotModel.Series.Add(series);

            AddMollierPoint(plotModel, chartType, new UIMollierProcessPoint(uIMollierProcess, ProcessReferenceType.Process), mollierControlSettings);

            return series;
        }

        private static UIMollierPoint createCoolingAdditionalLines(PlotModel plotModel, UIMollierProcess uIMollierProcess, MollierControlSettings mollierControlSettings)
        {
            ChartType chartType = mollierControlSettings.ChartType;
            MollierProcess mollierProcess = uIMollierProcess.MollierProcess;
            MollierPoint start = mollierProcess.Start;
            MollierPoint end = mollierProcess.End;

            if (mollierProcess == null || !(mollierProcess is CoolingProcess) || start == null || end == null || start.HumidityRatio == end.HumidityRatio)
            {
                return null;
            }

            // tolerance below which the real cooling line should not be displayed
            if (System.Math.Abs(start.HumidityRatio - end.HumidityRatio) < 0.001)
            {
                return null;
            }

            Series series = null;

            CoolingProcess coolingProcess = (CoolingProcess)mollierProcess;

            MollierPoint apparatusDewPoint = coolingProcess.ApparatusDewPoint();
            series = AddMollierPoint(plotModel, chartType, new UIMollierPoint(apparatusDewPoint, Create.UIMollierPointAppearance(DisplayPointType.Dew, DisplayPointType.Dew.Description())), null);
            series.Tag = uIMollierProcess;

            MollierPoint dewPoint = coolingProcess.DewPoint();
            series = AddMollierPoint(plotModel, chartType, new UIMollierPoint(dewPoint, Create.UIMollierPointAppearance(DisplayPointType.CoolingSaturation)), null);
            series.Tag = uIMollierProcess;

            int borderWidth = mollierControlSettings.ProccessLineThickness != -1 ? mollierControlSettings.ProccessLineThickness : 2;

            createDewPointDashLineSeries(plotModel, start, dewPoint, uIMollierProcess, chartType, SystemColor.LightGray, borderWidth, LineStyle.Dash);
            createDewPointDashLineSeries(plotModel, end, apparatusDewPoint, uIMollierProcess, chartType, SystemColor.LightGray, borderWidth, LineStyle.Dash);

            double pressure = end.Pressure;

            double shiftFactor = 0.992; // shift factor from relative humidity 100%

            double dryBulbTemperature = coolingProcess.Efficiency == 1 ? start.DryBulbTemperature - shiftFactor * (start.DryBulbTemperature - dewPoint.DryBulbTemperature) : dewPoint.DryBulbTemperature;
            double relativeHumidity = coolingProcess.Efficiency == 1 ? Mollier.Query.RelativeHumidity(dryBulbTemperature, dewPoint.HumidityRatio, dewPoint.Pressure) : dewPoint.RelativeHumidity;

            double dryBulbTemperatureStep = 0.5;

            LineSeries series_Temp = new LineSeries
            {
                Title = Guid.NewGuid().ToString(),
                Color = OxyColors.LightGray,
                RenderInLegend = false,
                StrokeThickness = borderWidth,
                LineStyle = LineStyle.Dash,
                Tag = uIMollierProcess,
            };

            double humidityRatio_Temp = Mollier.Query.HumidityRatio(dryBulbTemperature, relativeHumidity, pressure);
            MollierPoint mollierPoint = new MollierPoint(dryBulbTemperature, humidityRatio_Temp, pressure);
            Point2D point2D = Convert.ToSAM(mollierPoint, chartType);

            series_Temp.Points.Add(new DataPoint(point2D.X, point2D.Y));

            dryBulbTemperature = dewPoint.DryBulbTemperature;
            while (dryBulbTemperature > end.DryBulbTemperature)
            {
                humidityRatio_Temp = Mollier.Query.HumidityRatio(dryBulbTemperature, relativeHumidity, pressure);
                if (!double.IsNaN(humidityRatio_Temp))
                {
                    mollierPoint = new MollierPoint(dryBulbTemperature, humidityRatio_Temp, pressure);
                    point2D = Convert.ToSAM(mollierPoint, chartType);

                    series_Temp.Points.Add(new DataPoint(point2D.X, point2D.Y));
                }

                dryBulbTemperature -= dryBulbTemperatureStep;
            }

            plotModel.Series.Add(series_Temp);

            List<MollierPoint> mollierPoints = Mollier.Query.ProcessMollierPoints(coolingProcess);
            if (mollierPoints != null && mollierPoints.Count > 1)
            {
                for (int j = 0; j < mollierPoints.Count - 1; j++)
                {
                    createDewPointDashLineSeries(plotModel, mollierPoints[j], mollierPoints[j + 1], uIMollierProcess, chartType, SystemColor.Gray, 3, LineStyle.Solid);
                }
            }

            return new UIMollierPoint(apparatusDewPoint, new UIMollierPointAppearance(SystemColor.Empty, SystemColor.Empty, "ADP"));
        }

        // General label-generation helper (no chart dependency) — ported verbatim.
        private static List<UIMollierProcess> generateLabels(List<IMollierGroup> groups)
        {
            List<UIMollierProcess> labeledMollierProcesses = new List<UIMollierProcess>();

            foreach (IMollierGroup group in groups)
            {
                MollierGroup mollierGroup = (MollierGroup)group;
                if (mollierGroup == null)
                {
                    continue;
                }

                char name = 'A';
                List<IMollierProcess> processList = mollierGroup.GetObjects<IMollierProcess>();
                if (processList == null)
                {
                    continue;
                }

                for (int j = 0; j < processList.Count; j++)
                {
                    UIMollierProcess UI_MollierProcess = (UIMollierProcess)processList[j];
                    MollierProcess mollierProcess = UI_MollierProcess.MollierProcess;

                    if (UI_MollierProcess.UIMollierPointAppearance_Start == null)
                    {
                        UI_MollierProcess.UIMollierPointAppearance_Start = new UIMollierPointAppearance(SystemColor.Empty, SystemColor.Empty, string.Empty);
                    }

                    if (UI_MollierProcess.UIMollierPointAppearance_Start.Label == null)
                    {
                        UI_MollierProcess.UIMollierPointAppearance_Start.Label = string.Empty;
                    }

                    if (UI_MollierProcess.UIMollierPointAppearance_End == null)
                    {
                        UI_MollierProcess.UIMollierPointAppearance_End = new UIMollierPointAppearance(SystemColor.Empty, SystemColor.Empty, string.Empty);
                    }

                    if (UI_MollierProcess.UIMollierPointAppearance_End.Label == null)
                    {
                        UI_MollierProcess.UIMollierPointAppearance_End.Label = string.Empty;
                    }

                    if (UI_MollierProcess?.MollierProcess is RoomProcess)
                    {
                        UI_MollierProcess.UIMollierPointAppearance_End.Label = "ROOM";
                    }

                    if (UI_MollierProcess.UIMollierPointAppearance_Start.Label == string.Empty)
                    {
                        UI_MollierProcess.UIMollierPointAppearance_Start.Label = name + "1";
                    }

                    if (UI_MollierProcess.UIMollierPointAppearance_End.Label == string.Empty)
                    {
                        UI_MollierProcess.UIMollierPointAppearance_End.Label = name + "2";
                    }

                    (UI_MollierProcess.UIMollierAppearance as UIMollierAppearance).Label = (UI_MollierProcess.UIMollierAppearance as UIMollierAppearance).Label == null ? Query.ProcessName(mollierProcess) : (UI_MollierProcess.UIMollierAppearance as UIMollierAppearance).Label;

                    name++;
                    if (UI_MollierProcess.UIMollierAppearance.Visible == true)
                    {
                        labeledMollierProcesses.Add(new UIMollierProcess(UI_MollierProcess));
                    }
                }
            }

            return labeledMollierProcesses;
        }
    }
}
