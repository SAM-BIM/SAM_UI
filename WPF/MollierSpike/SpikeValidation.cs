// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace MollierSpike
{
    /// <summary>
    /// Headless capability gate for Stage 2a. Each check maps a WinForms
    /// System.Windows.Forms.DataVisualization.Chart feature the Mollier port needs
    /// onto its OxyPlot equivalent, and asserts it actually works. Writes export
    /// artifacts to <paramref name="outputDir"/> and returns a PASS/FAIL report.
    /// </summary>
    internal static class SpikeValidation
    {
        private const int Width = 1000;
        private const int Height = 700;

        public sealed class Check
        {
            public string Name;
            public bool Pass;
            public string Detail;
        }

        public static List<Check> Run(string outputDir)
        {
            Directory.CreateDirectory(outputDir);
            List<Check> checks = new List<Check>();

            PlotModel model = DiagramBuilder.Build();

            // 1. Render + PNG export (OxyPlot.Wpf.PngExporter). Also forces axis transform setup.
            checks.Add(Guard("Render + PNG export (PngExporter)", () =>
            {
                string path = Path.Combine(outputDir, "diagram.png");
                OxyPlot.Wpf.PngExporter exporter = new OxyPlot.Wpf.PngExporter { Width = Width, Height = Height };
                using (FileStream fs = File.Create(path))
                {
                    exporter.Export(model, fs);
                }
                long size = new FileInfo(path).Length;
                if (size <= 0) throw new Exception("PNG is empty");
                return $"wrote {path} ({size} bytes)";
            }));

            // 2. SVG export (OxyPlot.SvgExporter) — the new default vector export.
            checks.Add(Guard("SVG export (SvgExporter)", () =>
            {
                string path = Path.Combine(outputDir, "diagram.svg");
                SvgExporter exporter = new SvgExporter { Width = Width, Height = Height };
                using (FileStream fs = File.Create(path))
                {
                    exporter.Export(model, fs);
                }
                long size = new FileInfo(path).Length;
                if (size <= 0) throw new Exception("SVG is empty");
                return $"wrote {path} ({size} bytes)";
            }));

            // 3. EMF via OxyPlot.WindowsForms.GraphicsRenderContext + System.Drawing.Imaging.Metafile.
            //    THE risky claim: this works with UseWindowsForms=false. If this check passes,
            //    the EMF compat path in Stage 2d is viable without re-enabling WinForms.
            checks.Add(Guard("EMF export (GraphicsRenderContext + Metafile bridge)", () =>
            {
                string path = Path.Combine(outputDir, "diagram.emf");
                EmfBridge.Export(model, path, Width, Height);
                long size = new FileInfo(path).Length;
                if (size <= 0) throw new Exception("EMF is empty");
                return $"wrote {path} ({size} bytes)";
            }));

            // Axis handles (transforms are valid after the PNG render above).
            // With one bottom + one right axis, the defaults resolve to ours.
            Axis xAxis = model.DefaultXAxis;
            Axis yAxis = model.DefaultYAxis;

            // 4. Axis pixel<->value round-trip — replaces ValueToPixelPosition / PixelPositionToValue.
            checks.Add(Guard("Axis Transform / InverseTransform round-trip", () =>
            {
                double[] testValues = { 0, 10, 20, 35 };
                double maxErr = 0;
                foreach (double v in testValues)
                {
                    double screen = xAxis.Transform(v);
                    double back = xAxis.InverseTransform(screen);
                    maxErr = Math.Max(maxErr, Math.Abs(back - v));
                }
                double wScreen = yAxis.Transform(12.0);
                double wBack = yAxis.InverseTransform(wScreen);
                maxErr = Math.Max(maxErr, Math.Abs(wBack - 12.0));
                if (maxErr > 1e-6) throw new Exception($"round-trip error {maxErr:E3} exceeds tolerance");
                return $"max round-trip error {maxErr:E3} (X screen range valid: {xAxis.ScreenMin.X:0}..{xAxis.ScreenMax.X:0})";
            }));

            // 5. Click-to-find-nearest-point — replaces Chart.HitTest(x, y, DataPoint).
            checks.Add(Guard("Hit-test nearest point (GetSeriesFromPoint + GetNearestPoint)", () =>
            {
                (double t, double rh) = DiagramBuilder.StatePoints[2]; // (26, 60)
                double w = Psychrometrics.HumidityRatio(t, rh / 100.0) * 1000.0;
                ScreenPoint screen = new ScreenPoint(xAxis.Transform(t), yAxis.Transform(w));

                Series hitSeries = model.GetSeriesFromPoint(screen, 20);
                if (hitSeries == null) throw new Exception("GetSeriesFromPoint returned null");
                if (hitSeries.Title != DiagramBuilder.StateSeriesTitle)
                    throw new Exception($"hit the wrong series: {hitSeries.Title}");

                TrackerHitResult tracker = hitSeries.GetNearestPoint(screen, false);
                if (tracker == null) throw new Exception("GetNearestPoint returned null");
                double dt = Math.Abs(tracker.DataPoint.X - t);
                double dw = Math.Abs(tracker.DataPoint.Y - w);
                if (dt > 0.5 || dw > 0.5)
                    throw new Exception($"nearest point off by ({dt:0.00}, {dw:0.00})");
                return $"clicked ({t:0.0},{w:0.00}) -> nearest ({tracker.DataPoint.X:0.0},{tracker.DataPoint.Y:0.00}) on '{hitSeries.Title}'";
            }));

            // 6. Drag-select rectangle — replaces the MouseDown/Move/Up + PixelPositionToValue
            //    rubber-band in MollierControl, drawn as a RectangleAnnotation overlay.
            checks.Add(Guard("Drag-select via RectangleAnnotation overlay", () =>
            {
                // Simulate a mouse drag in SCREEN space, then map corners to data via InverseTransform
                // exactly as the WinForms MouseUp handler does.
                ScreenPoint down = new ScreenPoint(xAxis.Transform(22), yAxis.Transform(12));
                ScreenPoint up = new ScreenPoint(xAxis.Transform(28), yAxis.Transform(5));

                double xMin = Math.Min(xAxis.InverseTransform(down.X), xAxis.InverseTransform(up.X));
                double xMax = Math.Max(xAxis.InverseTransform(down.X), xAxis.InverseTransform(up.X));
                double yMin = Math.Min(yAxis.InverseTransform(down.Y), yAxis.InverseTransform(up.Y));
                double yMax = Math.Max(yAxis.InverseTransform(down.Y), yAxis.InverseTransform(up.Y));

                RectangleAnnotation rect = new RectangleAnnotation
                {
                    MinimumX = xMin,
                    MaximumX = xMax,
                    MinimumY = yMin,
                    MaximumY = yMax,
                    Fill = OxyColor.FromAColor(60, OxyColors.SteelBlue),
                    Stroke = OxyColors.SteelBlue,
                    StrokeThickness = 1,
                    XAxisKey = "X",
                    YAxisKey = "Y",
                };
                model.Annotations.Add(rect);

                // Count state points inside the selection.
                int selected = 0;
                foreach ((double t, double rh) in DiagramBuilder.StatePoints)
                {
                    double w = Psychrometrics.HumidityRatio(t, rh / 100.0) * 1000.0;
                    if (t >= xMin && t <= xMax && w >= yMin && w <= yMax) selected++;
                }

                // Re-render with the annotation to confirm the overlay renders without error.
                string path = Path.Combine(outputDir, "diagram_selected.png");
                OxyPlot.Wpf.PngExporter exporter = new OxyPlot.Wpf.PngExporter { Width = Width, Height = Height };
                using (FileStream fs = File.Create(path))
                {
                    exporter.Export(model, fs);
                }
                return $"selection data-rect X[{xMin:0.0}..{xMax:0.0}] Y[{yMin:0.0}..{yMax:0.0}] contains {selected} point(s); overlay rendered";
            }));

            return checks;
        }

        private static Check Guard(string name, Func<string> body)
        {
            try
            {
                string detail = body();
                return new Check { Name = name, Pass = true, Detail = detail };
            }
            catch (Exception ex)
            {
                return new Check { Name = name, Pass = false, Detail = ex.GetType().Name + ": " + ex.Message };
            }
        }

        public static string Format(List<Check> checks, out bool allPass)
        {
            allPass = true;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=== OxyPlot.Wpf spike capability gate (Stage 2a) ===");
            foreach (Check c in checks)
            {
                if (!c.Pass) allPass = false;
                sb.AppendLine($"[{(c.Pass ? "PASS" : "FAIL")}] {c.Name}");
                sb.AppendLine($"        {c.Detail}");
            }
            sb.AppendLine(allPass ? "RESULT: ALL CHECKS PASSED" : "RESULT: ONE OR MORE CHECKS FAILED");
            return sb.ToString();
        }
    }
}
