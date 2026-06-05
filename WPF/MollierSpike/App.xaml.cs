// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace MollierSpike
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Headless capability gate: `MollierSpike.exe --validate [outputDir]`.
            // Runs every OxyPlot check, writes artifacts + report.txt, prints to console,
            // exits 0 (all pass) or 1 (any fail) WITHOUT opening a window. Lets the spike
            // be verified in a no-display environment.
            if (e.Args.Any(a => string.Equals(a, "--controls", StringComparison.OrdinalIgnoreCase)))
            {
                string dir = e.Args.FirstOrDefault(a => !a.StartsWith("--"))
                    ?? Path.Combine(AppContext.BaseDirectory, "spike-output");
                Shutdown(ControlsSmokeTest.Run(dir));
                return;
            }

            if (e.Args.Any(a => string.Equals(a, "--mollier", StringComparison.OrdinalIgnoreCase)))
            {
                string dir = e.Args.FirstOrDefault(a => !a.StartsWith("--"))
                    ?? Path.Combine(AppContext.BaseDirectory, "spike-output");
                Shutdown(MollierBuilderSmokeTest.Run(dir));
                return;
            }

            if (e.Args.Any(a => string.Equals(a, "--validate", StringComparison.OrdinalIgnoreCase)))
            {
                string outputDir = e.Args.FirstOrDefault(a => !a.StartsWith("--"))
                    ?? Path.Combine(AppContext.BaseDirectory, "spike-output");

                List<SpikeValidation.Check> checks = SpikeValidation.Run(outputDir);
                string report = SpikeValidation.Format(checks, out bool allPass);

                Console.WriteLine(report);
                File.WriteAllText(Path.Combine(outputDir, "report.txt"), report);

                Shutdown(allPass ? 0 : 1);
                return;
            }

            new MainWindow().Show();
        }
    }
}
