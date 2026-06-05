// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace MollierSpike
{
    /// <summary>
    /// Interactive harness mirroring MollierControl's mouse behaviour against OxyPlot:
    /// click -> nearest-point hit-test, drag -> RectangleAnnotation rubber-band select.
    /// Code-behind, matching the repo's existing WPF style (no MVVM).
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly PlotModel _model;
        private ScreenPoint _dragStart;
        private bool _dragging;
        private RectangleAnnotation _selection;

        public MainWindow()
        {
            InitializeComponent();
            _model = DiagramBuilder.Build();
            PlotView.Model = _model;
        }

        private Axis XAxis => _model.DefaultXAxis;
        private Axis YAxis => _model.DefaultYAxis;

        private void PlotView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            System.Windows.Point p = e.GetPosition(PlotView);
            ScreenPoint screen = new ScreenPoint(p.X, p.Y);

            // Hit-test: nearest data point (replaces Chart.HitTest(x,y,DataPoint)).
            Series series = _model.GetSeriesFromPoint(screen, 10);
            TrackerHitResult tracker = series?.GetNearestPoint(screen, false);
            if (tracker != null)
            {
                StatusText.Text = $"Hit '{series.Title}' near ({tracker.DataPoint.X:0.0} °C, {tracker.DataPoint.Y:0.00} g/kg)";
                return;
            }

            // Otherwise begin a rubber-band drag-select.
            _dragging = true;
            _dragStart = screen;
            if (_selection != null)
            {
                _model.Annotations.Remove(_selection);
            }
            _selection = new RectangleAnnotation
            {
                Fill = OxyColor.FromAColor(60, OxyColors.SteelBlue),
                Stroke = OxyColors.SteelBlue,
                StrokeThickness = 1,
            };
            _model.Annotations.Add(_selection);
        }

        private void PlotView_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging || _selection == null) return;

            System.Windows.Point p = e.GetPosition(PlotView);
            double x1 = XAxis.InverseTransform(_dragStart.X);
            double x2 = XAxis.InverseTransform(p.X);
            double y1 = YAxis.InverseTransform(_dragStart.Y);
            double y2 = YAxis.InverseTransform(p.Y);

            _selection.MinimumX = System.Math.Min(x1, x2);
            _selection.MaximumX = System.Math.Max(x1, x2);
            _selection.MinimumY = System.Math.Min(y1, y2);
            _selection.MaximumY = System.Math.Max(y1, y2);
            _model.InvalidatePlot(false);
        }

        private void PlotView_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragging || _selection == null) return;
            _dragging = false;

            int count = 0;
            foreach ((double t, double rh) in DiagramBuilder.StatePoints)
            {
                double w = Psychrometrics.HumidityRatio(t, rh / 100.0) * 1000.0;
                if (t >= _selection.MinimumX && t <= _selection.MaximumX &&
                    w >= _selection.MinimumY && w <= _selection.MaximumY)
                {
                    count++;
                }
            }
            StatusText.Text = $"Selected region X[{_selection.MinimumX:0.0}..{_selection.MaximumX:0.0}] " +
                              $"Y[{_selection.MinimumY:0.0}..{_selection.MaximumY:0.0}] -> {count} point(s)";
        }

        private void ExportPng_Click(object sender, RoutedEventArgs e)
        {
            string path = Prompt("PNG image (*.png)|*.png", ".png");
            if (path == null) return;
            OxyPlot.Wpf.PngExporter exporter = new OxyPlot.Wpf.PngExporter { Width = 1000, Height = 700 };
            using (FileStream fs = File.Create(path)) exporter.Export(_model, fs);
            StatusText.Text = "Exported " + path;
        }

        private void ExportSvg_Click(object sender, RoutedEventArgs e)
        {
            string path = Prompt("SVG image (*.svg)|*.svg", ".svg");
            if (path == null) return;
            SvgExporter exporter = new SvgExporter { Width = 1000, Height = 700 };
            using (FileStream fs = File.Create(path)) exporter.Export(_model, fs);
            StatusText.Text = "Exported " + path;
        }

        private void ExportEmf_Click(object sender, RoutedEventArgs e)
        {
            string path = Prompt("EMF image (*.emf)|*.emf", ".emf");
            if (path == null) return;
            EmfBridge.Export(_model, path, 1000, 700);
            StatusText.Text = "Exported " + path;
        }

        private static string Prompt(string filter, string ext)
        {
            SaveFileDialog dialog = new SaveFileDialog { Filter = filter, DefaultExt = ext };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}
