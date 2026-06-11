// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using SAM.Geometry.Mollier;
using SAM.Geometry.Planar;
using SAM.Core.Mollier.UI.Forms;
using SystemColor = System.Drawing.Color;

namespace SAM.Core.Mollier.UI.Controls
{
    /// <summary>
    /// WPF/OxyPlot port of the WinForms MollierControl (Stage 2d, core).
    /// Hosts an OxyPlot PlotView, sets up axes for both chart types, drives the ported 2c
    /// chart-builders via Regenerate(), and provides hit-test / pixel↔value transforms,
    /// drag-select zoom, hover highlight and PNG/SVG/EMF export.
    ///
    /// DEFERRED (tracked): the label-collision solver (AddLabels + ScaleVector2D/AxesRatio/
    /// Obstacles/Solver2DDatas/ChartLabels), the partial-vapour-pressure secondary axis,
    /// the NetOffice Excel PDF export and WinForms printing, and the Edit dialogs
    /// (UIMollierPointForm / UIMollierProcessForm — Stage 2e).
    /// </summary>
    public partial class MollierControl : UserControl
    {
        public event MollierPointSelectedEventHandler MollierPointSelected;

        private readonly PlotModel plotModel;
        private MollierControlSettings mollierControlSettings;
        private MollierModel mollierModel = new MollierModel();

        private LinearAxis axisX;
        private LinearAxis axisY;

        // Solver-placed label annotations (recomputed after each render once PlotArea is valid).
        private readonly List<OxyPlot.Annotations.Annotation> labelAnnotations = new List<OxyPlot.Annotations.Annotation>();

        // Drag-select zoom state.
        private bool selection = false;
        private bool dragging = false;
        private ScreenPoint mouseDown;
        private RectangleAnnotation selectionAnnotation;

        // Hover highlight state (replaces the WinForms HooverTimer; OxyPlot hit-test is cheap enough inline).
        private readonly List<Tuple<Series, double>> hoverData = new List<Tuple<Series, double>>();
        private const double SelectedObjectWidth_Process = 4;
        private const double SelectedObjectWidth_Default = 2;

        public MollierControl()
        {
            InitializeComponent();

            mollierControlSettings = new MollierControlSettings();
            plotModel = new PlotModel { Background = OxyColors.White, PlotMargins = new OxyThickness(double.NaN) };
            MollierChart.Model = plotModel;

            // OxyPlot's default PlotController binds the mouse buttons (track/pan) and a hover tracker, and
            // marks the WPF routed mouse events handled. That swallowed our own handlers — breaking
            // (1) the rectangle-select zoom, (4) click-to-select that drives the ε / SHR tools, and (5) the
            // "..." point picker (all of which depend on MollierChart_MouseUp raising MollierPointSelected) —
            // and its built-in tracker popped a second, raw value-box (series title + full-precision X/Y) that
            // competed with our formatted hover box. Clear all default bindings so those events reach our
            // handlers and only our tracker shows; keep just the wheel-zoom.
            PlotController controller = new PlotController();
            controller.UnbindAll();
            controller.BindMouseWheel(PlotCommands.ZoomWheel);
            MollierChart.Controller = controller;
        }

        /// <summary>Hover highlight toggle. (WinForms used a HooverTimer; here it is inline mouse-move highlighting.)</summary>
        public bool EnableHoover { get; set; }

        // ----------------------------------------------------------------- Axes

        private void setAxisGraph_Mollier()
        {
            if (mollierControlSettings == null || !mollierControlSettings.IsValid())
            {
                mollierControlSettings = new MollierControlSettings();
            }

            double humidityRatio_Min = mollierControlSettings.HumidityRatio_Min;
            double humidityRatio_Max = mollierControlSettings.HumidityRatio_Max;
            double humidityRatio_interval = mollierControlSettings.HumidityRatio_Interval;
            double temperature_Min = mollierControlSettings.Temperature_Min;
            double temperature_Max = mollierControlSettings.Temperature_Max;
            double temperature_interval = mollierControlSettings.Temperature_Interval;

            // OX: humidity ratio [g/kg]
            axisX = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Humidity Ratio x [g/kg]",
                Minimum = humidityRatio_Min * 1000,
                Maximum = humidityRatio_Max * 1000,
                MajorStep = humidityRatio_interval * 1000,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.Gray,
                MinorGridlineStyle = LineStyle.Solid,
                MinorGridlineColor = OxyColors.LightGray,
                StringFormat = "0.##",
            };

            // OY: dry-bulb temperature [°C]
            axisY = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Dry Bulb Temperature t [°C]",
                Minimum = temperature_Min,
                Maximum = temperature_Max,
                MajorStep = temperature_interval,
                MajorGridlineStyle = LineStyle.None,
                StringFormat = "0.##",
            };

            plotModel.Axes.Add(axisX);
            plotModel.Axes.Add(axisY);

            // Partial-vapour-pressure secondary axis (WinForms CreateXAxis): a top axis in [kPa].
            AddPartialVapourPressureAxis(AxisPosition.Top);
        }

        private void setAxisGraph_Psychrometric()
        {
            double humidityRatio_Min = mollierControlSettings.HumidityRatio_Min;
            double humidityRatio_Max = mollierControlSettings.HumidityRatio_Max;
            double humidityRatio_interval = mollierControlSettings.HumidityRatio_Interval;
            double temperature_Min = mollierControlSettings.Temperature_Min;
            double temperature_Max = mollierControlSettings.Temperature_Max;
            double temperature_interval = mollierControlSettings.Temperature_Interval;

            // OX: dry-bulb temperature [°C]
            axisX = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Dry Bulb Temperature t [°C]",
                Minimum = temperature_Min,
                Maximum = temperature_Max,
                MajorStep = temperature_interval,
                MajorGridlineStyle = LineStyle.None,
                MinorGridlineStyle = LineStyle.None,
                StringFormat = "0.##",
            };

            // OY (right): humidity ratio [g/kg]
            axisY = new LinearAxis
            {
                Position = AxisPosition.Right,
                Title = "Humidity Ratio  x [g/kg]",
                Minimum = humidityRatio_Min * 1000,
                Maximum = humidityRatio_Max * 1000,
                MajorStep = humidityRatio_interval * 1000,
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineColor = OxyColors.Gray,
                MinorGridlineStyle = LineStyle.Solid,
                MinorGridlineColor = OxyColors.LightGray,
                StringFormat = "0.###",
            };

            plotModel.Axes.Add(axisX);
            plotModel.Axes.Add(axisY);

            // Partial-vapour-pressure secondary axis (WinForms CreateYAxis): a left axis in [kPa]
            // (the humidity-ratio axis sits on the right in the psychrometric chart).
            AddPartialVapourPressureAxis(AxisPosition.Left);
        }

        // Adds the partial-vapour-pressure scale as a secondary axis (top for Mollier, left for
        // Psychrometric), mirroring the WinForms CreateXAxis/CreateYAxis. pW is derived from the
        // humidity-ratio range via the saturation relation; the axis is linear in pW [kPa], which is a
        // close approximation of the (mildly non-linear) x↔pW mapping over the chart range. When the
        // View ▸ Partial Vapour Pressure toggle is off the axis is omitted entirely.
        private void AddPartialVapourPressureAxis(AxisPosition axisPosition)
        {
            if (mollierControlSettings == null || !mollierControlSettings.PartialVapourPressure_Axis)
            {
                return;
            }

            double pressure = mollierControlSettings.Pressure;
            double partialVapourPressure_Min = Mollier.Query.PartialVapourPressure_ByHumidityRatio(mollierControlSettings.HumidityRatio_Min, 45, pressure) / 1000;
            double partialVapourPressure_Max = Mollier.Query.PartialVapourPressure_ByHumidityRatio(mollierControlSettings.HumidityRatio_Max, 45, pressure) / 1000;

            if (double.IsNaN(partialVapourPressure_Min) || double.IsNaN(partialVapourPressure_Max) || partialVapourPressure_Max <= partialVapourPressure_Min)
            {
                return;
            }

            double interval = mollierControlSettings.PartialVapourPressure_Interval;

            LinearAxis partialVapourPressureAxis = new LinearAxis
            {
                Key = "PartialVapourPressure",
                Position = axisPosition,
                Title = "Partial Vapour Pressure pW [kPa]",
                Minimum = partialVapourPressure_Min,
                Maximum = partialVapourPressure_Max,
                MajorStep = interval > 0 ? interval : double.NaN,
                MajorGridlineStyle = LineStyle.None,
                MinorGridlineStyle = LineStyle.None,
                MinorTickSize = 0,
                StringFormat = "0.##",
                IsZoomEnabled = false,
                IsPanEnabled = false,
            };

            plotModel.Axes.Add(partialVapourPressureAxis);
        }

        // ----------------------------------------------------------------- Regenerate

        public void Regenerate()
        {
            if (mollierControlSettings == null)
            {
                return;
            }

            plotModel.Series.Clear();
            plotModel.Annotations.Clear();
            plotModel.Axes.Clear();
            selectionAnnotation = null;
            labelAnnotations.Clear();

            if (mollierControlSettings.ChartType == ChartType.Mollier)
            {
                setAxisGraph_Mollier();
            }
            else
            {
                setAxisGraph_Psychrometric();
            }

            plotModel.AddLinesSeries(mollierControlSettings);
            plotModel.AddMollierLines(mollierModel, mollierControlSettings);
            plotModel.AddMollierPoints(mollierModel, mollierControlSettings);
            plotModel.AddMollierProcesses(mollierModel, mollierControlSettings);
            plotModel.AddMollierZones(mollierModel, mollierControlSettings);
            plotModel.AddDivisionArea(mollierModel, mollierControlSettings);
            plotModel.AddColorPoint(mollierModel, mollierControlSettings);
            // TODO (2d): plotModel.AddLabels(mollierControlSettings) once the label-solver layer is ported.

            ReorderSeries();

            plotModel.InvalidatePlot(true);

            // The label-collision solver needs valid pixel geometry (PlotArea + axis transforms),
            // which is only available after a render. Run it on the next dispatcher cycle.
            Dispatcher.BeginInvoke(new Action(() => UpdateLabels()), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// Recomputes the solver-placed labels against the current pixel geometry. Safe to call after a
        /// render (PlotArea valid); returns the number of labels placed. NOTE: exact placement is a
        /// heuristic that should be eyeballed in the live app by a domain user (per the migration plan).
        /// </summary>
        public int UpdateLabels()
        {
            if (axisX == null || mollierControlSettings == null || plotModel.PlotArea.Width <= 0 || plotModel.PlotArea.Height <= 0)
            {
                return 0;
            }

            foreach (OxyPlot.Annotations.Annotation annotation in labelAnnotations)
            {
                plotModel.Annotations.Remove(annotation);
            }
            labelAnnotations.Clear();

            List<OxyPlot.Annotations.Annotation> added = plotModel.AddLabels(mollierControlSettings);
            if (added != null)
            {
                labelAnnotations.AddRange(added);
            }

            plotModel.InvalidatePlot(false);
            return labelAnnotations.Count;
        }

        // Z-order fix-up: bring relative-humidity curves and cooling-process dash series to the front
        // (OxyPlot draws series in list order; later = on top), mirroring the WinForms re-insert.
        private void ReorderSeries()
        {
            List<Series> relativeHumidity = new List<Series>();
            foreach (Series series in plotModel.Series)
            {
                ConstantValueCurve constantValueCurve = series.Tag as ConstantValueCurve;
                if (constantValueCurve != null && constantValueCurve.ChartDataType == ChartDataType.RelativeHumidity)
                {
                    relativeHumidity.Add(series);
                }
            }

            foreach (Series series in relativeHumidity)
            {
                plotModel.Series.Remove(series);
                plotModel.Series.Add(series);
            }
        }

        // ----------------------------------------------------------------- Object model API

        public List<IUIMollierObject> AddMollierObjects<T>(IEnumerable<T> mollierObjects, bool checkPressure = true, bool regenerate = true) where T : IMollierObject
        {
            if (mollierObjects == null || mollierObjects.Count() == 0 || mollierModel == null)
            {
                return null;
            }

            List<IUIMollierObject> result = new List<IUIMollierObject>();
            foreach (IMollierObject mollierObject in mollierObjects)
            {
                if (mollierObject is IMollierPoint)
                {
                    result.AddRange(AddPoints(new List<IMollierPoint>() { (IMollierPoint)mollierObject }, checkPressure));
                }
                else if (mollierObject is IMollierProcess)
                {
                    result.AddRange(AddProcesses(new List<IMollierProcess>() { (IMollierProcess)mollierObject }, checkPressure));
                }
                else if (mollierObject is IMollierGroup)
                {
                    result.AddRange(AddGroups(new List<IMollierGroup>() { (IMollierGroup)mollierObject }));
                }
                else if (mollierObject is IMollierZone)
                {
                    result.AddRange(AddZones(new List<IMollierZone>() { (IMollierZone)mollierObject }));
                }
                else if (mollierObject is IMollierCurve)
                {
                    result.AddRange(AddCurves(new List<IMollierCurve>() { (IMollierCurve)mollierObject }));
                }
            }

            if (regenerate)
            {
                Regenerate();
            }

            return result;
        }

        private List<UIMollierPoint> AddPoints(IEnumerable<IMollierPoint> mollierPoints, bool checkPressure = true)
        {
            if (mollierPoints == null || mollierModel == null)
            {
                return null;
            }

            List<UIMollierPoint> result = new List<UIMollierPoint>();
            foreach (IMollierPoint mollierPoint in mollierPoints)
            {
                if (mollierPoint == null)
                {
                    continue;
                }

                if (checkPressure && !(mollierPoint.Pressure.AlmostEqual(mollierControlSettings.Pressure)))
                {
                    continue;
                }

                UIMollierPoint uIMollierPoint = mollierPoint as UIMollierPoint;
                if (uIMollierPoint == null)
                {
                    MollierPoint mollierPoint_Temp = mollierPoint as MollierPoint;
                    UIMollierPointAppearance uIMollierPointAppearance = Create.UIMollierPointAppearance(DisplayPointType.Default, string.Empty);
                    uIMollierPoint = new UIMollierPoint(mollierPoint_Temp, uIMollierPointAppearance);
                }

                result.Add(uIMollierPoint);
            }

            mollierModel.AddRange(result);
            return result;
        }

        private List<UIMollierProcess> AddProcesses(IEnumerable<IMollierProcess> mollierProcesses, bool checkPressure = true)
        {
            if (mollierProcesses == null || mollierModel == null)
            {
                return null;
            }

            List<UIMollierProcess> result = new List<UIMollierProcess>();
            foreach (IMollierProcess mollierProcess in mollierProcesses)
            {
                if (mollierProcess == null || mollierProcess.Start == null || mollierProcess.End == null)
                {
                    continue;
                }

                if (checkPressure && !(mollierProcess.Start.Pressure.AlmostEqual(mollierControlSettings.Pressure)))
                {
                    continue;
                }

                if (mollierProcess is UIMollierProcess)
                {
                    result.Add((UIMollierProcess)mollierProcess);
                    continue;
                }

                MollierProcess mollierProcess_Temp = (MollierProcess)mollierProcess;
                if (mollierProcess_Temp == null)
                {
                    continue;
                }

                SystemColor color = mollierControlSettings.VisibilitySettings.GetColor(mollierControlSettings.DefaultTemplateName, ChartParameterType.Line, mollierProcess_Temp);
                string label = Query.ProcessName(mollierProcess);

                UIMollierAppearance uIMollierAppearance = new UIMollierAppearance(color, label) { Visible = true };

                UIMollierProcess uIMollierProcess = new UIMollierProcess(mollierProcess_Temp, uIMollierAppearance);
                UIMollierPointAppearance uIMollierPointAppearance = Create.UIMollierPointAppearance(DisplayPointType.Process);

                uIMollierProcess.UIMollierPointAppearance_Start = uIMollierPointAppearance;
                uIMollierProcess.UIMollierPointAppearance_End = uIMollierPointAppearance;

                result.Add(uIMollierProcess);
            }

            mollierModel.AddRange(result);
            return result;
        }

        private List<UIMollierGroup> AddGroups(IEnumerable<IMollierGroup> mollierGroups, bool checkPressure = true)
        {
            if (mollierGroups == null || mollierModel == null)
            {
                return null;
            }

            List<UIMollierGroup> result = new List<UIMollierGroup>();
            foreach (IMollierGroup mollierGroup in mollierGroups)
            {
                MollierGroup mollierGroup1 = (MollierGroup)mollierGroup;
                UIMollierGroup uIMollierGroup = mollierGroup1.ToSAM_UI(true, mollierControlSettings);
                result.Add(uIMollierGroup);
            }

            mollierModel.AddRange(result);
            return result;
        }

        private List<UIMollierZone> AddZones(IEnumerable<IMollierZone> mollierZones)
        {
            if (mollierZones == null || mollierZones.Count() == 0 || mollierModel == null)
            {
                return null;
            }

            List<UIMollierZone> result = new List<UIMollierZone>();
            foreach (IMollierZone mollierZone in mollierZones)
            {
                SystemColor color = SystemColor.Blue;
                string label = "";
                MollierZone mollierZone1 = mollierZone as MollierZone;

                if (mollierZone is UIMollierZone)
                {
                    color = ((UIMollierZone)mollierZone).UIMollierAppearance.Color;
                    label = (((UIMollierZone)mollierZone).UIMollierAppearance as UIMollierAppearance).Label;
                }

                UIMollierZone uIMollierZone = new UIMollierZone(mollierZone1, new UIMollierAppearance(color, label));
                result.Add(uIMollierZone);
            }

            mollierModel.AddRange(result);
            return result;
        }

        private List<UIMollierCurve> AddCurves(IEnumerable<IMollierCurve> mollierCurves)
        {
            if (mollierCurves == null || mollierModel == null)
            {
                return null;
            }

            List<UIMollierCurve> result = new List<UIMollierCurve>();
            foreach (IMollierCurve mollierCurve in mollierCurves)
            {
                if (mollierCurve is UIMollierCurve)
                {
                    if (((UIMollierCurve)mollierCurve).MollierCurve is MollierLine)
                    {
                        result.Add((UIMollierCurve)mollierCurve);
                    }

                    continue;
                }

                if (mollierCurve is MollierLine)
                {
                    result.Add(new UIMollierCurve((MollierCurve)mollierCurve, SystemColor.LightGray));
                }
            }

            mollierModel.AddRange(result);
            return result;
        }

        public bool ClearObjects(bool regenerate = true)
        {
            mollierModel?.Clear();
            if (regenerate)
            {
                Regenerate();
            }

            return true;
        }

        public void RemoveProcesses(IEnumerable<IMollierProcess> mollierProcesses)
        {
            if (mollierProcesses == null || mollierModel == null)
            {
                return;
            }

            foreach (IMollierProcess mollierProcess in mollierProcesses)
            {
                mollierModel.Remove(mollierProcess, false);
            }

            Regenerate();
        }

        public void RemovePoints(IEnumerable<IMollierPoint> mollierPoints)
        {
            if (mollierPoints == null || mollierModel == null)
            {
                return;
            }

            foreach (IMollierPoint mollierPoint in mollierPoints)
            {
                mollierModel.Remove(mollierPoint, false);
            }

            Regenerate();
        }

        public void RemoveZones(IEnumerable<IMollierZone> mollierZones)
        {
            if (mollierZones == null || mollierModel == null)
            {
                return;
            }

            foreach (IMollierZone mollierZone in mollierZones)
            {
                mollierModel.Remove(mollierZone, false);
            }

            Regenerate();
        }

        public List<IUIMollierObject> UIMollierObjects(Type type, bool includeNestedObjects = true)
        {
            List<IUIMollierObject> result = new List<IUIMollierObject>();

            List<IMollierObject> mollierObjects = mollierModel.GetMollierObjects(type, includeNestedObjects);
            if (mollierObjects == null)
            {
                return null;
            }

            foreach (IMollierObject mollierObject in mollierObjects)
            {
                if (mollierObject is IUIMollierObject)
                {
                    result.Add((IUIMollierObject)mollierObject);
                }
            }

            return result;
        }

        public List<T> UIMollierObjects<T>(bool includeNestedObjects = true) where T : IUIMollierObject
        {
            return UIMollierObjects(typeof(T), includeNestedObjects)?.FindAll(x => x is T)?.ConvertAll(x => (T)(object)x);
        }

        public MollierControlSettings MollierControlSettings
        {
            get { return mollierControlSettings == null ? null : new MollierControlSettings(mollierControlSettings); }
            set { mollierControlSettings = value == null ? null : new MollierControlSettings(value); }
        }

        public MollierModel MollierModel
        {
            get { return mollierModel; }
            set { if (value != null) { mollierModel = value; } }
        }

        // ----------------------------------------------------------------- Coordinate transforms

        public MollierPoint GetMollierPoint(double x, double y, out Point2D point2D)
        {
            point2D = GetPixelPositionToValue(new Point2D(x, y));
            if (point2D == null)
            {
                return null;
            }

            return Convert.ToMollier(point2D, mollierControlSettings.ChartType, mollierControlSettings.Pressure);
        }

        public Point2D GetPoint2D(MollierPoint mollierPoint)
        {
            if (mollierPoint == null)
            {
                return null;
            }

            Point2D point2D = Convert.ToSAM(mollierPoint, mollierControlSettings.ChartType);
            return point2D == null ? null : GetValueToPixelPosition(point2D);
        }

        public Point2D GetPixelPositionToValue(Point2D point2D)
        {
            if (point2D == null || axisX == null || axisY == null)
            {
                return null;
            }

            return new Point2D(axisX.InverseTransform(point2D.X), axisY.InverseTransform(point2D.Y));
        }

        public Point2D GetValueToPixelPosition(Point2D point2D)
        {
            if (point2D == null || axisX == null || axisY == null)
            {
                return null;
            }

            return new Point2D(axisX.Transform(point2D.X), axisY.Transform(point2D.Y));
        }

        // ----------------------------------------------------------------- Export

        /// <summary>Saves the chart, choosing the encoder by file extension (.png/.svg/.emf/.jpg).</summary>
        public bool SaveImage(string path, int width = 1100, int height = 800)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
            switch (extension)
            {
                case ".svg":
                    using (FileStream fs = File.Create(path))
                    {
                        new SvgExporter { Width = width, Height = height }.Export(plotModel, fs);
                    }
                    return true;

                case ".emf":
                    plotModel.SaveEmf(path, width, height);
                    return true;

                default: // .png, .jpg (PngExporter is raster; SVG is the preferred vector default)
                    using (FileStream fs = File.Create(path))
                    {
                        new OxyPlot.Wpf.PngExporter { Width = width, Height = height }.Export(plotModel, fs);
                    }
                    return true;
            }
        }

        public bool Save(ChartExportType chartExportType, string path = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                string name = mollierControlSettings.ChartType == ChartType.Mollier ? "Mollier" : "Psychrometric";
                Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog { FileName = name };
                switch (chartExportType)
                {
                    case ChartExportType.JPG: saveFileDialog.Filter = "PNG image (*.png)|*.png|JPG image (*.jpg)|*.jpg|SVG image (*.svg)|*.svg"; break;
                    case ChartExportType.EMF: saveFileDialog.Filter = "EMF document (*.emf)|*.emf|All files (*.*)|*.*"; break;
                    case ChartExportType.PDF: return false; // TODO (2d): NetOffice Excel PDF pipeline not yet ported.
                }
                if (saveFileDialog.ShowDialog() != true)
                {
                    return false;
                }
                path = saveFileDialog.FileName;
            }

            if (chartExportType == ChartExportType.PDF)
            {
                return false; // TODO (2d): NetOffice Excel PDF pipeline not yet ported.
            }

            return SaveImage(path);
        }

        // ----------------------------------------------------------------- Selection / highlight API

        public void Select(IMollierObject mollierObject)
        {
            RestoreHover();

            foreach (Series series in plotModel.Series)
            {
                if (mollierObject.AlmostEqual(series.Tag as IMollierObject))
                {
                    BumpThickness(series, mollierObject is MollierProcess ? SelectedObjectWidth_Process : SelectedObjectWidth_Default);
                    break;
                }
            }

            plotModel.InvalidatePlot(false);
        }

        private void BumpThickness(Series series, double delta)
        {
            if (series is LineSeries lineSeries)
            {
                hoverData.Add(new Tuple<Series, double>(series, lineSeries.StrokeThickness));
                lineSeries.StrokeThickness += delta;
            }
            else if (series is ScatterSeries scatterSeries)
            {
                hoverData.Add(new Tuple<Series, double>(series, scatterSeries.MarkerStrokeThickness));
                scatterSeries.MarkerStrokeThickness += delta;
            }
        }

        private void RestoreHover()
        {
            foreach (Tuple<Series, double> data in hoverData)
            {
                if (data.Item1 is LineSeries lineSeries) { lineSeries.StrokeThickness = data.Item2; }
                else if (data.Item1 is ScatterSeries scatterSeries) { scatterSeries.MarkerStrokeThickness = data.Item2; }
            }

            hoverData.Clear();
        }

        // ----------------------------------------------------------------- Mouse interaction

        private void MollierChart_MouseDown(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Point p = e.GetPosition(MollierChart);

            if (selection && e.ChangedButton == MouseButton.Left)
            {
                dragging = true;
                mouseDown = new ScreenPoint(p.X, p.Y);
                if (selectionAnnotation != null) { plotModel.Annotations.Remove(selectionAnnotation); }
                selectionAnnotation = new RectangleAnnotation
                {
                    Fill = OxyColor.FromAColor(60, OxyColors.SteelBlue),
                    Stroke = OxyColors.SteelBlue,
                    StrokeThickness = 1,
                    Layer = AnnotationLayer.AboveSeries,
                };
                plotModel.Annotations.Add(selectionAnnotation);
            }
        }

        private void MollierChart_MouseMove(object sender, MouseEventArgs e)
        {
            if (axisX == null || axisY == null)
            {
                return;
            }

            System.Windows.Point p = e.GetPosition(MollierChart);

            if (selection && dragging && selectionAnnotation != null)
            {
                double x1 = axisX.InverseTransform(mouseDown.X);
                double x2 = axisX.InverseTransform(p.X);
                double y1 = axisY.InverseTransform(mouseDown.Y);
                double y2 = axisY.InverseTransform(p.Y);
                selectionAnnotation.MinimumX = System.Math.Min(x1, x2);
                selectionAnnotation.MaximumX = System.Math.Max(x1, x2);
                selectionAnnotation.MinimumY = System.Math.Min(y1, y2);
                selectionAnnotation.MaximumY = System.Math.Max(y1, y2);
                plotModel.InvalidatePlot(false);
                return;
            }

            if (EnableHoover)
            {
                RestoreHover();
                ScreenPoint screenPoint = new ScreenPoint(p.X, p.Y);

                // Existing points sit on top of (and often exactly on) the grid lines, so OxyPlot's nearest
                // series would otherwise resolve to a line and the point would never highlight. Give points
                // priority; if none is within proximity fall back to the nearest line / process series.
                ScatterSeries pointSeries = NearestPointSeries(screenPoint, 12);
                Series series = pointSeries ?? plotModel.GetSeriesFromPoint(screenPoint, 10);
                if (series != null)
                {
                    // Highlight any hovered series (grid lines included — this is the "select lines" feedback)...
                    BumpThickness(series, series.Tag is MollierProcess ? SelectedObjectWidth_Process : SelectedObjectWidth_Default);

                    // ...but only show the value-box for the meaningful objects (clean, GUID-free text).
                    if (IsHoverable(series))
                    {
                        ShowHoverTracker(series, screenPoint);
                    }
                    else
                    {
                        ((IPlotView)MollierChart).HideTracker();
                    }
                }
                else
                {
                    ((IPlotView)MollierChart).HideTracker();
                }
                plotModel.InvalidatePlot(false);
            }
        }

        // Only the meaningful series (processes, points) get the hover value-box; the background grid
        // lines carry GUID/auto titles and would otherwise show a useless "<guid> X Y" tracker.
        private static bool IsHoverable(Series series)
        {
            return series.Tag is UIMollierProcess
                || series.Tag is UIMollierPoint
                || series.Title == Modify.MollierPointsTitle;
        }

        // Restores the WinForms "yellow box" hover tooltip: shows OxyPlot's tracker with the GUID-free
        // value text. Process / single-point series already carry a clean TrackerFormatString; the
        // multi-point scatter has none, so its text is built on the fly from the nearest point's tag.
        private void ShowHoverTracker(Series series, ScreenPoint screenPoint)
        {
            TrackerHitResult trackerHitResult = series.GetNearestPoint(screenPoint, series is LineSeries);
            if (trackerHitResult == null)
            {
                ((IPlotView)MollierChart).HideTracker();
                return;
            }

            if (string.IsNullOrEmpty(series.TrackerFormatString))
            {
                // Multi-point scatter ("MollierPoints"): recover the UIMollierPoint under the cursor.
                ScatterSeries scatterSeries = series as ScatterSeries;
                object tag = scatterSeries == null ? null : NearestScatterTag(scatterSeries, screenPoint);
                if (tag is UIMollierPoint uIMollierPoint)
                {
                    trackerHitResult.Text = Query.ToolTipText(uIMollierPoint, mollierControlSettings.ChartType);
                }
                else
                {
                    ((IPlotView)MollierChart).HideTracker();
                    return;
                }
            }

            ((IPlotView)MollierChart).ShowTracker(trackerHitResult);
        }

        // Pops the value-box at an arbitrary clicked location, showing the same formatted text as the
        // hover tracker for the Mollier point there (or the snapped existing point).
        private void ShowPointTracker(MollierPoint mollierPoint, ScreenPoint screenPoint)
        {
            if (mollierPoint == null)
            {
                return;
            }

            string text = Query.ToolTipText(mollierPoint, mollierControlSettings.ChartType);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            TrackerHitResult trackerHitResult = new TrackerHitResult
            {
                Position = screenPoint,
                Text = text,
                PlotModel = plotModel,
            };

            ((IPlotView)MollierChart).ShowTracker(trackerHitResult);
        }

        private void MollierChart_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!selection || !dragging)
            {
                // Plain left-click -> point selection. If an existing chart point is within proximity,
                // snap to it so the picker returns that point's exact values; otherwise use the click location.
                if (e.ChangedButton == MouseButton.Left)
                {
                    System.Windows.Point pc = e.GetPosition(MollierChart);
                    ScreenPoint screenPoint = new ScreenPoint(pc.X, pc.Y);
                    MollierPoint mollierPoint = GetNearestMollierPoint(screenPoint, 12);
                    Point2D point2D;
                    if (mollierPoint != null)
                    {
                        point2D = Convert.ToSAM(mollierPoint, mollierControlSettings.ChartType);
                    }
                    else
                    {
                        mollierPoint = GetMollierPoint(pc.X, pc.Y, out point2D);
                    }
                    MollierPointSelected?.Invoke(this, new MollierPointSelectedEventArgs(mollierPoint, point2D));

                    // With Hover on, a click also pops the value-box for the clicked location (or the
                    // snapped point), using the same formatted text as the hover tracker.
                    if (EnableHoover)
                    {
                        ShowPointTracker(mollierPoint, screenPoint);
                    }
                }
                return;
            }

            dragging = false;
            ChartType chartType = mollierControlSettings.ChartType;
            System.Windows.Point p = e.GetPosition(MollierChart);

            double X_Min = chartType == ChartType.Mollier ? mollierControlSettings.HumidityRatio_Min : mollierControlSettings.Temperature_Min;
            double Y_Min = chartType == ChartType.Mollier ? mollierControlSettings.Temperature_Min : mollierControlSettings.HumidityRatio_Min;
            double X_Max = chartType == ChartType.Mollier ? mollierControlSettings.HumidityRatio_Max : mollierControlSettings.Temperature_Max;
            double Y_Max = chartType == ChartType.Mollier ? mollierControlSettings.Temperature_Max : mollierControlSettings.HumidityRatio_Max;

            double x_Min = System.Math.Min(axisX.InverseTransform(p.X), axisX.InverseTransform(mouseDown.X));
            double x_Max = System.Math.Max(axisX.InverseTransform(p.X), axisX.InverseTransform(mouseDown.X));
            double y_Min = System.Math.Min(axisY.InverseTransform(p.Y), axisY.InverseTransform(mouseDown.Y));
            double y_Max = System.Math.Max(axisY.InverseTransform(p.Y), axisY.InverseTransform(mouseDown.Y));

            // [g/kg] -> [kg/kg]
            x_Min = chartType == ChartType.Mollier ? x_Min / 1000 : x_Min;
            x_Max = chartType == ChartType.Mollier ? x_Max / 1000 : x_Max;
            y_Min = chartType == ChartType.Mollier ? y_Min : y_Min / 1000;
            y_Max = chartType == ChartType.Mollier ? y_Max : y_Max / 1000;

            double x_Difference = x_Max - x_Min;
            double y_Difference = y_Max - y_Min;
            if ((chartType == ChartType.Mollier && (x_Difference < 0.001 || y_Difference < 1)) || (chartType == ChartType.Psychrometric && (x_Difference < 1 || y_Difference < 0.001)))
            {
                if (selectionAnnotation != null) { plotModel.Annotations.Remove(selectionAnnotation); selectionAnnotation = null; }
                plotModel.InvalidatePlot(false);
                return;
            }

            mollierControlSettings.HumidityRatio_Min = chartType == ChartType.Mollier ? System.Math.Max(x_Min, X_Min) : System.Math.Max(y_Min, Y_Min);
            mollierControlSettings.HumidityRatio_Max = chartType == ChartType.Mollier ? System.Math.Min(x_Max, X_Max) : System.Math.Min(y_Max, Y_Max);
            mollierControlSettings.Temperature_Min = chartType == ChartType.Mollier ? System.Math.Max(y_Min, Y_Min) : System.Math.Max(x_Min, X_Min);
            mollierControlSettings.Temperature_Max = chartType == ChartType.Mollier ? System.Math.Min(y_Max, Y_Max) : System.Math.Min(x_Max, X_Max);

            mollierControlSettings.HumidityRatio_Min = System.Math.Round(mollierControlSettings.HumidityRatio_Min, 3);
            mollierControlSettings.HumidityRatio_Max = System.Math.Round(mollierControlSettings.HumidityRatio_Max, 3);
            mollierControlSettings.Temperature_Min = System.Math.Round(mollierControlSettings.Temperature_Min);
            mollierControlSettings.Temperature_Max = System.Math.Round(mollierControlSettings.Temperature_Max);

            selection = false;
            Regenerate();
        }

        // ----------------------------------------------------------------- Context menu

        private void ContextMenuStrip_Chart_Opening(object sender, RoutedEventArgs e)
        {
            IUIMollierObject uIMollierObject = GetUIMollierObject();
            bool visible = uIMollierObject != null;

            ToolStripMenuItem_Edit.Visibility = visible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            ToolStripMenuItem_Remove.Visibility = visible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            ToolStripSeparator_Edit.Visibility = visible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

            ToolStripMenuItem_Edit.Tag = uIMollierObject;
            ToolStripMenuItem_Remove.Tag = uIMollierObject;
        }

        private void ToolStripMenuItem_ProcessesAndPoints_Click(object sender, RoutedEventArgs e)
        {
            if (mollierModel == null || mollierControlSettings.DivisionArea)
            {
                System.Windows.MessageBox.Show("There is no process or point to zoom");
                return;
            }

            ChartType chartType = mollierControlSettings.ChartType;
            Query.ZoomParameters(plotModel.Series, chartType, out double x_Min, out double x_Max, out double y_Min, out double y_Max);
            mollierControlSettings.HumidityRatio_Min = chartType == ChartType.Mollier ? x_Min : y_Min;
            mollierControlSettings.HumidityRatio_Max = chartType == ChartType.Mollier ? x_Max : y_Max;
            mollierControlSettings.Temperature_Min = chartType == ChartType.Mollier ? y_Min : x_Min;
            mollierControlSettings.Temperature_Max = chartType == ChartType.Mollier ? y_Max : x_Max;
            Regenerate();
        }

        private void ToolStripMenuItem_Selection_Click(object sender, RoutedEventArgs e)
        {
            selection = true;
        }

        private void ToolStripMenuItem_Reset_Click(object sender, RoutedEventArgs e)
        {
            MollierControlSettings defaults = new MollierControlSettings();
            mollierControlSettings.HumidityRatio_Min = defaults.HumidityRatio_Min;
            mollierControlSettings.HumidityRatio_Max = defaults.HumidityRatio_Max;
            mollierControlSettings.Temperature_Min = defaults.Temperature_Min;
            mollierControlSettings.Temperature_Max = defaults.Temperature_Max;
            Regenerate();
        }

        private void ToolStripMenuItem_Edit_Click(object sender, RoutedEventArgs e)
        {
            IUIMollierObject uIMollierObject = ToolStripMenuItem_Remove.Tag as IUIMollierObject;
            if (uIMollierObject == null)
            {
                return;
            }

            if (uIMollierObject is UIMollierPoint)
            {
                UIMollierPointForm uIMollierPointForm = new UIMollierPointForm((UIMollierPoint)uIMollierObject) { MollierControl = this };
                uIMollierPointForm.Closed += CustomizeForm_Closed;
                uIMollierPointForm.Show();
                return;
            }

            if (uIMollierObject is UIMollierProcess)
            {
                UIMollierProcessForm uIMollierProcessForm = new UIMollierProcessForm((UIMollierProcess)uIMollierObject) { MollierControl = this };
                uIMollierProcessForm.Closed += CustomizeForm_Closed;
                uIMollierProcessForm.Show();
                return;
            }
        }

        // Replaces the WinForms CustomizePointForm_FormClosing: on OK-close of a modeless edit dialog, apply.
        private void CustomizeForm_Closed(object sender, EventArgs e)
        {
            if (sender is UIMollierPointForm pointForm && pointForm.DialogOk)
            {
                Apply(pointForm);
            }
            else if (sender is UIMollierProcessForm processForm && processForm.DialogOk)
            {
                Apply(processForm);
            }
        }

        /// <summary>Reads the edited object from the dialog and updates the model + chart (OxyPlot port of WinForms Apply).</summary>
        public void Apply(Window window)
        {
            IUIMollierObject uIMollierObject = null;

            if (window is UIMollierPointForm pointForm)
            {
                uIMollierObject = pointForm.UIMollierPoint;
                if (uIMollierObject is UIMollierProcessPoint uIMollierProcessPoint)
                {
                    uIMollierObject = uIMollierProcessPoint.UIMollierProcess;
                }
            }
            else if (window is UIMollierProcessForm processForm)
            {
                uIMollierObject = processForm.UIMollierProcess;
            }
            else
            {
                return;
            }

            if (uIMollierObject == null)
            {
                return;
            }

            mollierModel.Update(uIMollierObject, true);
            Regenerate();
        }

        private void ToolStripMenuItem_Remove_Click(object sender, RoutedEventArgs e)
        {
            IUIMollierObject uIMollierObject = ToolStripMenuItem_Remove.Tag as IUIMollierObject;
            if (uIMollierObject == null)
            {
                return;
            }

            if (System.Windows.MessageBox.Show("Are you sure to remove item?", "Remove", System.Windows.MessageBoxButton.YesNo) == System.Windows.MessageBoxResult.No)
            {
                return;
            }

            mollierModel.Remove(uIMollierObject);
            Regenerate();
        }

        // Hit-test for the context menu: recover the UI object under the cursor.
        private IUIMollierObject GetUIMollierObject()
        {
            if (axisX == null)
            {
                return null;
            }

            System.Windows.Point p = Mouse.GetPosition(MollierChart);
            ScreenPoint screen = new ScreenPoint(p.X, p.Y);

            Series series = plotModel.GetSeriesFromPoint(screen, 10);
            if (series == null)
            {
                return null;
            }

            if (series.Tag is UIMollierProcess uIMollierProcess)
            {
                return uIMollierProcess;
            }

            if (series.Tag is UIMollierPoint uIMollierPoint)
            {
                IReference reference = Create.Reference(uIMollierPoint);
                if (Geometry.Mollier.Query.UIMollierObject<IUIMollierObject>(mollierModel, reference) == null)
                {
                    return null;
                }

                return uIMollierPoint;
            }

            // Multi-point "MollierPoints" scatter series: recover the nearest point's tag.
            if (series is ScatterSeries scatterSeries)
            {
                object tag = NearestScatterTag(scatterSeries, screen);
                if (tag is UIMollierPoint nearest)
                {
                    return nearest;
                }
            }

            return null;
        }

        private object NearestScatterTag(ScatterSeries scatterSeries, ScreenPoint screen)
        {
            object result = null;
            double best = double.MaxValue;
            foreach (ScatterPoint scatterPoint in scatterSeries.Points)
            {
                if (scatterPoint.Tag == null) { continue; }
                ScreenPoint sp = new ScreenPoint(axisX.Transform(scatterPoint.X), axisY.Transform(scatterPoint.Y));
                double dx = sp.X - screen.X;
                double dy = sp.Y - screen.Y;
                double distance = dx * dx + dy * dy;
                if (distance < best)
                {
                    best = distance;
                    result = scatterPoint.Tag;
                }
            }

            return best <= 144 ? result : null; // within ~12 px
        }

        // Returns the existing chart point (standalone or process marker) nearest the cursor within
        // 'proximity' pixels, or null. Used to snap the click-to-pick so it returns an exact existing point.
        private MollierPoint GetNearestMollierPoint(ScreenPoint screen, double proximity)
        {
            if (axisX == null || axisY == null)
            {
                return null;
            }

            MollierPoint result = null;
            double best = proximity * proximity;
            foreach (Series series in plotModel.Series)
            {
                if (!(series is ScatterSeries scatterSeries))
                {
                    continue;
                }

                foreach (ScatterPoint scatterPoint in scatterSeries.Points)
                {
                    if (!(scatterPoint.Tag is MollierPoint mollierPoint))
                    {
                        continue;
                    }

                    double dx = axisX.Transform(scatterPoint.X) - screen.X;
                    double dy = axisY.Transform(scatterPoint.Y) - screen.Y;
                    double distance = dx * dx + dy * dy;
                    if (distance < best)
                    {
                        best = distance;
                        result = mollierPoint;
                    }
                }
            }

            return result;
        }

        // Returns the scatter series owning the existing chart point nearest the cursor within 'proximity'
        // pixels (or null). Used so the Hover highlight/value-box prefers points over the grid lines.
        private ScatterSeries NearestPointSeries(ScreenPoint screen, double proximity)
        {
            if (axisX == null || axisY == null)
            {
                return null;
            }

            ScatterSeries result = null;
            double best = proximity * proximity;
            foreach (Series series in plotModel.Series)
            {
                if (!(series is ScatterSeries scatterSeries))
                {
                    continue;
                }

                foreach (ScatterPoint scatterPoint in scatterSeries.Points)
                {
                    if (!(scatterPoint.Tag is MollierPoint))
                    {
                        continue;
                    }

                    double dx = axisX.Transform(scatterPoint.X) - screen.X;
                    double dy = axisY.Transform(scatterPoint.Y) - screen.Y;
                    double distance = dx * dx + dy * dy;
                    if (distance < best)
                    {
                        best = distance;
                        result = scatterSeries;
                    }
                }
            }

            return result;
        }
    }
}
