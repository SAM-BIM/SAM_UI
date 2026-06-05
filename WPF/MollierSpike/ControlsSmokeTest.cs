// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.IO;
using System.Windows;
using SAM.Core.Mollier.UI.Controls;

namespace MollierSpike
{
    /// <summary>
    /// Throwaway runtime smoke test for the ported 2e controls. Instantiating each WPF UserControl
    /// runs InitializeComponent + the ctor (combo population, child-control wiring, ParameterControl
    /// placement), proving they construct without throwing.
    /// </summary>
    internal static class ControlsSmokeTest
    {
        public static int Run(string outputDir)
        {
            Directory.CreateDirectory(outputDir);
            var report = new System.Text.StringBuilder();
            bool pass = true;

            pass &= Try(report, "NumberBoxControl", () => new NumberBoxControl());
            pass &= Try(report, "ParameterControl", () => new ParameterControl());
            pass &= Try(report, "MollierProcessTypeControl", () => new MollierProcessTypeControl());
            pass &= Try(report, "UIMollierAppearanceControl", () => new UIMollierAppearanceControl());
            pass &= Try(report, "BuiltInVisibilitySettingControl", () => new BuiltInVisibilitySettingControl());
            pass &= Try(report, "MollierPointControl", () => new MollierPointControl());
            pass &= Try(report, "HeatingProcessControl", () => new HeatingProcessControl());
            pass &= Try(report, "CoolingProcessControl", () => new CoolingProcessControl());
            pass &= Try(report, "HeatRecoveryProcessControl", () => new HeatRecoveryProcessControl());
            pass &= Try(report, "MixingProcessControl", () => new MixingProcessControl());
            pass &= Try(report, "AdiabaticHumidificationProcessControl", () => new AdiabaticHumidificationProcessControl());
            pass &= Try(report, "IsothermalHumidificationProcessControl", () => new IsothermalHumidificationProcessControl());
            pass &= Try(report, "RoomProcessControl", () => new RoomProcessControl());

            // Batch 3 composites + dialogs.
            pass &= Try(report, "UIMollierProcessPointControl", () => new UIMollierProcessPointControl());
            pass &= Try(report, "UIMollierProcessControl", () => new UIMollierProcessControl());
            pass &= Try(report, "UIMollierProcessControl_Limited", () => new UIMollierProcessControl_Limited());
            pass &= Try(report, "UIMollierPointControl", () => new UIMollierPointControl());
            pass &= TryWindow(report, "MollierPointForm", () => new SAM.Core.Mollier.UI.Forms.MollierPointForm());
            pass &= TryWindow(report, "MollierProcessForm", () => new SAM.Core.Mollier.UI.Forms.MollierProcessForm());
            pass &= TryWindow(report, "UIMollierProcessForm_Limited", () => new SAM.Core.Mollier.UI.Forms.UIMollierProcessForm_Limited());
            pass &= TryWindow(report, "UIMollierPointForm", () => new SAM.Core.Mollier.UI.Forms.UIMollierPointForm());
            pass &= TryWindow(report, "UIMollierProcessForm", () => new SAM.Core.Mollier.UI.UIMollierProcessForm());

            report.Insert(0, pass ? "RESULT: PASS\n" : "RESULT: FAIL\n");
            string text = report.ToString();
            Console.WriteLine(text);
            File.WriteAllText(Path.Combine(outputDir, "controls_report.txt"), text);
            return pass ? 0 : 1;
        }

        private static bool TryWindow(System.Text.StringBuilder report, string name, Func<Window> factory)
        {
            try
            {
                Window window = factory();
                report.AppendLine($"[PASS] {name} (window constructed: {window.Title})");
                window.Close();
                return true;
            }
            catch (Exception ex)
            {
                report.AppendLine($"[FAIL] {name}: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        private static bool Try(System.Text.StringBuilder report, string name, Func<FrameworkElement> factory)
        {
            try
            {
                FrameworkElement control = factory();
                control.Measure(new Size(800, 600));
                control.Arrange(new Rect(0, 0, 800, 600));
                report.AppendLine($"[PASS] {name} (size {control.DesiredSize.Width:0}x{control.DesiredSize.Height:0})");
                return true;
            }
            catch (Exception ex)
            {
                report.AppendLine($"[FAIL] {name}: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }
    }
}
