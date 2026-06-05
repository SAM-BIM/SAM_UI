// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using SAM.Core.Mollier.UI;

namespace MollierSpike
{
    /// <summary>
    /// Throwaway smoke test for the ported 2c chart-builders. Drives the real
    /// SAM.Core.Mollier.UI (WPF) pipeline — Query.ConstantValueCurves (domain) -> Convert.ToChart
    /// -> PlotModel.AddLinesSeries — then renders, proving the port runs end-to-end (not just compiles).
    /// </summary>
    internal static class MollierBuilderSmokeTest
    {
        public static int Run(string outputDir)
        {
            Directory.CreateDirectory(outputDir);
            var report = new System.Text.StringBuilder();
            bool pass = true;

            try
            {
                pass &= Render(SAM.Core.Mollier.ChartType.Psychrometric, "Psychrometric (ported 2c builders)",
                    "Dry-bulb temperature (°C)", AxisPosition.Bottom, "Humidity ratio (g/kg)", AxisPosition.Right,
                    Path.Combine(outputDir, "grid_psychrometric.png"), report);

                pass &= Render(SAM.Core.Mollier.ChartType.Mollier, "Mollier h-x (ported 2c builders)",
                    "Humidity ratio (g/kg)", AxisPosition.Top, "Diagram temperature (°C)", AxisPosition.Left,
                    Path.Combine(outputDir, "grid_mollier.png"), report);

                // 2d: render through the actual MollierControl (axis setup + Regenerate pipeline).
                pass &= RenderViaControl(SAM.Core.Mollier.ChartType.Psychrometric, Path.Combine(outputDir, "control_psychrometric.png"), report);
                pass &= RenderViaControl(SAM.Core.Mollier.ChartType.Mollier, Path.Combine(outputDir, "control_mollier.png"), report);
            }
            catch (Exception ex)
            {
                pass = false;
                report.AppendLine("EXCEPTION: " + ex);
            }

            report.Insert(0, pass ? "RESULT: PASS\n" : "RESULT: FAIL\n");
            string text = report.ToString();
            Console.WriteLine(text);
            File.WriteAllText(Path.Combine(outputDir, "mollier_report.txt"), text);
            return pass ? 0 : 1;
        }

        private static bool Render(SAM.Core.Mollier.ChartType chartType, string title,
            string xTitle, AxisPosition xPos, string yTitle, AxisPosition yPos, string png, System.Text.StringBuilder report)
        {
            MollierControlSettings settings = Query.DefaultMollierControlSettings();
            settings.ChartType = chartType;

            PlotModel model = new PlotModel { Title = title, Background = OxyColors.White };
            model.Axes.Add(new LinearAxis { Position = xPos, Title = xTitle });
            model.Axes.Add(new LinearAxis { Position = yPos, Title = yTitle });

            List<Series> series = model.AddLinesSeries(settings);

            int lineCount = model.Series.OfType<LineSeries>().Count();
            int pointCount = model.Series.OfType<LineSeries>().Sum(s => s.Points.Count);
            report.AppendLine($"[{chartType}] AddLinesSeries -> {series?.Count ?? 0} returned; {lineCount} LineSeries / {pointCount} vertices.");

            bool ok = lineCount >= 5 && pointCount >= 100;
            if (!ok) { report.AppendLine($"  FAIL: too few curves/points for {chartType}."); }

            var exporter = new OxyPlot.Wpf.PngExporter { Width = 1100, Height = 800 };
            using (FileStream fs = File.Create(png)) exporter.Export(model, fs);
            long size = new FileInfo(png).Length;
            report.AppendLine($"  rendered {png} ({size} bytes).");
            if (size <= 0) { ok = false; report.AppendLine("  FAIL: empty PNG."); }

            return ok;
        }

        private static bool RenderViaControl(SAM.Core.Mollier.ChartType chartType, string png, System.Text.StringBuilder report)
        {
            var control = new SAM.Core.Mollier.UI.Controls.MollierControl();
            MollierControlSettings settings = Query.DefaultMollierControlSettings();
            settings.ChartType = chartType;
            control.MollierControlSettings = settings;
            control.Regenerate();

            bool ok = control.SaveImage(png, 1100, 800); // first render warms PlotArea + axis transforms
            long size = ok && File.Exists(png) ? new FileInfo(png).Length : 0;
            report.AppendLine($"[Control:{chartType}] Regenerate + SaveImage -> {png} ({size} bytes).");
            if (size <= 0) { report.AppendLine("  FAIL: control produced no image."); ok = false; }

            // 2d label-collision solver: now that PlotArea is valid, place labels and re-render.
            int labelCount = control.UpdateLabels();
            string labeledPng = png.Replace(".png", "_labeled.png");
            bool okLabels = control.SaveImage(labeledPng, 1100, 800);
            long labeledSize = okLabels && File.Exists(labeledPng) ? new FileInfo(labeledPng).Length : 0;
            report.AppendLine($"[Control:{chartType}] UpdateLabels -> {labelCount} labels; rendered {labeledPng} ({labeledSize} bytes).");
            if (labelCount <= 0 || labeledSize <= 0) { report.AppendLine("  WARN: no labels placed (solver heuristic may need live-app tuning)."); }

            return ok;
        }
    }
}
