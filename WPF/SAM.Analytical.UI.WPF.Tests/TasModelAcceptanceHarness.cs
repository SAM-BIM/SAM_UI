// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

#nullable enable

using SAM.Analytical.Tas.GenOpt;
using SAM.Core.Optimisation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Xunit;
using File = System.IO.File;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// Opt-in licensed acceptance of the "tas-model" journey (PR8). Does nothing unless <c>SAM_OPT_ACCEPTANCE</c> names an
    /// output folder (and needs Tas with a licence): each case copies a Tas project into its own folder, opens the real
    /// Design Optimisation window on the "Tas model" engine, lets it read the model and the glazing pool with the
    /// licensed readers, picks items exactly as the Add buttons do, sets fields as a user types them, then runs Test one
    /// simulation and the optimisation with the installed TasGenExecute. Every state is logged and rendered.
    /// <c>SAM_OPT_ACCEPTANCE_CASES</c> selects cases (default all). Sources: <c>SAM_OPT_ACCEPTANCE_DEMO</c> (Systems Demo
    /// folder) and <c>SAM_OPT_ACCEPTANCE_SAM</c> (a SAM-generated TBD + TSD folder).
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class TasModelAcceptanceHarness : IDisposable
    {
        public TasModelAcceptanceHarness()
        {
            TasOptimisationWindow.ResetSession();
        }

        public void Dispose()
        {
            TasOptimisationWindow.ResetSession();
        }

        private sealed class Case
        {
            public Case(string id, string source, Action<TasOptimisationWindow> setup, int minutes)
            {
                Id = id;
                Source = source;
                Setup = setup;
                Minutes = minutes;
            }

            public string Id { get; }

            public string Source { get; }

            public Action<TasOptimisationWindow> Setup { get; }

            public int Minutes { get; }
        }

        [WpfFact]
        public async Task Licensed_acceptance_of_the_tas_model_journey()
        {
            string? output = Environment.GetEnvironmentVariable("SAM_OPT_ACCEPTANCE");
            if (string.IsNullOrWhiteSpace(output))
            {
                return;
            }

            string demo = Environment.GetEnvironmentVariable("SAM_OPT_ACCEPTANCE_DEMO") ?? @"C:\TasOut\pr7b\demo";
            string sam = Environment.GetEnvironmentVariable("SAM_OPT_ACCEPTANCE_SAM") ?? @"C:\TasOut\pr7b\sam";
            HashSet<string> selected = new HashSet<string>((Environment.GetEnvironmentVariable("SAM_OPT_ACCEPTANCE_CASES") ?? "A,B,C,F,D,E").Split(',').Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase);
            Directory.CreateDirectory(output);

            List<Case> cases = new List<Case>
            {
                //A. Demo: the plant controller by golden section, minimising plant cost (the PR3/PR5/PR7b acceptance point).
                new Case("A", demo, window =>
                {
                    TasOptimisationParameterRow row = window.AddTarget(Item(window.CanChangeItems, "HeatPumpController setpoint (Plant Room)"))!;
                    row.Minimum = "-5";
                    row.Maximum = "35";
                    window.AddMeasure(Item(window.CanMeasureItems, "Annual plant cost"));
                    window.AddMeasure(Item(window.CanMeasureItems, "Annual plant CO2"));
                    window.AddMeasure(Item(window.CanMeasureItems, "Annual plant energy"));
                }, 30),

                //B. Demo: the office heating and cooling setpoints by Hooke–Jeeves, minimising cooling demand.
                new Case("B", demo, window =>
                {
                    window.AddTarget(Item(window.CanChangeItems, "Office Weekday heating setpoint"));
                    window.AddTarget(Item(window.CanChangeItems, "Office Weekday cooling setpoint"));
                    window.AddMeasure(Item(window.CanMeasureItems, "Annual cooling demand"));
                    window.AddMeasure(Item(window.CanMeasureItems, "Annual heating demand"));
                    TasModelCatalogueItem overheating = Item(window.CanMeasureItems, "Overheating hours");
                    overheating.ParameterText = "25";
                    window.AddMeasure(overheating);
                    Text(window, "textBox_MaximumSimulations", "40");
                }, 40),

                //C. Demo: a glazing choice of up to 5 systems (Ug allowance widened as a user would), minimising cooling demand.
                new Case("C", demo, window =>
                {
                    Text(window, "textBox_GlazingUgAllowance", "5");
                    Text(window, "textBox_GlazingLightAllowance", "1");
                    Text(window, "textBox_GlazingMaximumOptions", "5");
                    Refresh(window);
                    window.AddTarget(Item(window.CanChangeItems, "Glazing system (Suncool Example)"));
                    window.AddMeasure(Item(window.CanMeasureItems, "Annual cooling demand"));
                    window.AddMeasure(Item(window.CanMeasureItems, "Annual heating demand"));
                    TasModelCatalogueItem overheating = Item(window.CanMeasureItems, "Overheating hours");
                    overheating.ParameterText = "25";
                    window.AddMeasure(overheating);
                }, 20),

                //F. Demo: a glazing choice with the default filter, untouched (what a user gets without opening Glazing options).
                new Case("F", demo, window =>
                {
                    window.AddTarget(Item(window.CanChangeItems, "Glazing system (Suncool Example)"));
                    window.AddMeasure(Item(window.CanMeasureItems, "Annual cooling demand"));
                    window.AddMeasure(Item(window.CanMeasureItems, "Annual heating demand"));
                    TasModelCatalogueItem overheating = Item(window.CanMeasureItems, "Overheating hours");
                    overheating.ParameterText = "25";
                    window.AddMeasure(overheating);
                }, 20),

                //D. SAM-generated model: a glazing choice (default filter widened to 4 options), minimising cooling demand.
                new Case("D", sam, window =>
                {
                    Text(window, "textBox_GlazingUgAllowance", "5");
                    Text(window, "textBox_GlazingLightAllowance", "1");
                    Text(window, "textBox_GlazingMaximumOptions", "4");
                    Refresh(window);
                    window.AddTarget(window.CanChangeItems.First(x => x.IsChoice));
                    window.AddMeasure(Item(window.CanMeasureItems, "Annual cooling demand"));
                    window.AddMeasure(Item(window.CanMeasureItems, "Overheating hours"));
                }, 20),

                //E. SAM-generated model: a cooling setpoint (24-hour profile) by golden section from 23 to 26 °C.
                new Case("E", sam, window =>
                {
                    TasOptimisationParameterRow row = window.AddTarget(window.CanChangeItems.FirstOrDefault(x => x.Name == "Studio 1_0 cooling setpoint") ?? window.CanChangeItems.First(x => x.Name.EndsWith("cooling setpoint", StringComparison.Ordinal)))!;
                    row.Minimum = "23";
                    row.Maximum = "26";
                    window.AddMeasure(Item(window.CanMeasureItems, "Annual cooling demand"));
                    window.AddMeasure(Item(window.CanMeasureItems, "Overheating hours"));
                    Text(window, "textBox_Tolerance", "10");
                }, 20),
            };

            foreach (Case @case in cases.Where(x => selected.Contains(x.Id)))
            {
                await Run(@case, output);
            }
        }

        private static async Task Run(Case @case, string output)
        {
            string log = Path.Combine(output, @case.Id + ".log");
            File.WriteAllText(log, string.Empty);
            void Log(string text) => File.AppendAllText(log, DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " " + text + Environment.NewLine, Encoding.UTF8);

            string workspace = Path.Combine(output, @case.Id + "-ws");
            string runs = Path.Combine(output, @case.Id + "-r");
            if (Directory.Exists(workspace))
            {
                Directory.Delete(workspace, true);
            }

            Directory.CreateDirectory(workspace);
            foreach (string path in Directory.GetFiles(@case.Source).Where(NativeGenOptWorkspace.IsTasFile))
            {
                File.Copy(path, Path.Combine(workspace, Path.GetFileName(path)));
            }

            Log("case " + @case.Id + " source " + @case.Source + " workspace " + workspace);

            TasOptimisationWindow.ResetSession();

            //The application's glazing pool: the test assembly points UserGlazingLibrary.Shared at an empty temporary
            //library (TestIsolation), so the user's own "My glazing systems" is given here, read only, as the app reads it.
            TasOptimisationWindow window = new TasOptimisationWindow(null, null, null, () => new[] { GlazingSource.FromDefaultLibrary(), GlazingSource.FromUserLibrary(new UserGlazingLibrary()) });
            try
            {
                window.SwitchEngine("tas-model");
                Text(window, "textBox_Directory", workspace);
                Text(window, "textBox_RunsDirectory", runs);
                window.Show();

                Stopwatch stopwatch = Stopwatch.StartNew();
                await Until(() => window.ModelSession.State == TasModelReadState.Ready || window.ModelSession.State == TasModelReadState.Failed, 300);
                await Until(() => !window.ModelSession.PoolBusy && (window.ModelSession.State != TasModelReadState.Ready || window.ModelSession.Sources.Count != 0), 300);
                await window.ModelTask;
                Log("model " + window.ModelSession.State + " in " + window.ModelSession.ReadDuration.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s, pool ready after " + stopwatch.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s: " + ((TextBlock)window.FindName("textBlock_ModelStatus")).Text);
                Log("pool: " + ((TextBlock)window.FindName("textBlock_GlazingPool")).Text.Replace(Environment.NewLine, " | "));
                foreach (TasGlazingSystem system in window.ModelSession.Pool)
                {
                    Log(string.Format(CultureInfo.InvariantCulture, "pool system: {0} [{1}] {2} {3} transparent={4} g={5} Ug={6} light={7}", system.Name, system.ShortId, system.Source, system.ApertureType, system.Transparent, system.G, system.Ug, system.Light));
                }

                foreach (TasGlazingConstructionInfo glazing in window.ModelSession.Inventory?.GlazingConstructions ?? new List<TasGlazingConstructionInfo>())
                {
                    Log(string.Format(CultureInfo.InvariantCulture, "model glazing: {0} g={1} U={2} light={3} elements={4}", glazing.Name, glazing.G, glazing.U, glazing.Light, glazing.Elements.Count));
                }
                Refresh(window);

                @case.Setup(window);
                Refresh(window);

                Log("glazing filter: " + ((TextBlock)window.FindName("textBlock_GlazingFilter")).Text.Replace(Environment.NewLine, " | "));
                foreach (TasModelCatalogueItem item in window.CanChangeItems)
                {
                    Log("can change: " + item.Name + " · " + item.Detail + (item.HasOptionsText ? " | " + item.OptionsText.Replace("\n", " | ") : string.Empty));
                }

                foreach (TasModelCatalogueItem item in window.CanMeasureItems)
                {
                    Log("can measure: " + item.Name + " · " + item.Detail);
                }

                foreach (TasOptimisationCheck check in window.Checks)
                {
                    Log("check: " + check);
                }

                window.Input.TryGetDefinition(out OptimisationDefinition? definition, out _);
                if (definition != null)
                {
                    File.WriteAllText(Path.Combine(output, @case.Id + "-definition.json"), definition.ToJson(), new UTF8Encoding(false));
                }

                File.WriteAllText(Path.Combine(output, @case.Id + "-script.csx"), window.GeneratedScript(out string? problem) ?? ("(none) " + problem));
                Render(window, output, @case.Id + "-1-setup.png", 1700);

                if (!window.Checks.CanRun())
                {
                    Log("NOT RUNNABLE");
                    return;
                }

                await window.TestAsync();
                Log("test: " + window.TestReport?.ToText().Replace(Environment.NewLine, " | "));
                Render(window, output, @case.Id + "-2-test.png", 1700);

                //A watchdog: a run over its time is cancelled (after the running simulation).
                DateTime deadline = DateTime.UtcNow.AddMinutes(@case.Minutes);
                System.Windows.Threading.DispatcherTimer watchdog = new System.Windows.Threading.DispatcherTimer() { Interval = TimeSpan.FromSeconds(5) };
                watchdog.Tick += (s, e) =>
                {
                    if (DateTime.UtcNow > deadline)
                    {
                        Log("WATCHDOG: cancelling");
                        window.RequestCancel();
                        watchdog.Stop();
                    }
                };
                watchdog.Start();
                stopwatch.Restart();
                await window.RunAsync();
                watchdog.Stop();

                Log("run in " + stopwatch.Elapsed.TotalSeconds.ToString("0", CultureInfo.InvariantCulture) + " s: " + window.Report?.ToText().Replace(Environment.NewLine, " | "));
                File.WriteAllText(Path.Combine(output, @case.Id + "-trace.txt"), window.TraceText(), new UTF8Encoding(false));
                window.ExportTrace(Path.Combine(output, @case.Id + "-trace.csv"));
                foreach (TasOptimisationTraceRow row in window.TraceRows)
                {
                    Log("trace: " + row.Simulation + " " + row.Event + " " + string.Join(", ", row.CoordinateTexts) + " -> " + string.Join(", ", row.OutputTexts) + " | raw " + string.Join(", ", row.CoordinateRaw) + " -> " + string.Join(", ", row.OutputRaw));
                }

                Render(window, output, @case.Id + "-3-result.png", 1100);
            }
            catch (Exception exception)
            {
                Log("EXCEPTION " + exception);
            }
            finally
            {
                window.Close();
            }
        }

        private static TasModelCatalogueItem Item(IEnumerable<TasModelCatalogueItem> items, string name)
        {
            return items.FirstOrDefault(x => x.Name == name) ?? throw new InvalidOperationException("'" + name + "' is not offered. Offered: " + string.Join("; ", items.Select(x => x.Name)));
        }

        private static void Text(TasOptimisationWindow window, string name, string text)
        {
            ((TextBox)window.FindName(name)).Text = text;
        }

        private static void Refresh(TasOptimisationWindow window)
        {
            window.SetInput(new TasOptimisationInput(window.Input));
        }

        private static void Render(TasOptimisationWindow window, string directory, string name, double height)
        {
            PartOWorkflowEvidenceHarness.Render(window, Path.Combine(directory, name), 860, height);
        }

        private static async Task Until(Func<bool> condition, int seconds)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline)
                {
                    throw new TimeoutException("The licensed read did not finish in " + seconds + " s.");
                }

                await Task.Delay(100);
            }
        }
    }
}
