// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

#nullable enable

using SAM.Analytical.Tas.GenOpt;
using SAM.Core;
using SAM.Core.Optimisation;
using SAM.Weather;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Xunit;
using File = System.IO.File;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// Opt-in licensed acceptance of "Apply best design" (PR9). Does nothing unless <c>SAM_APPLY_ACCEPTANCE</c> names an
    /// output folder (and needs Tas with a licence). Every input is a copy.
    /// <list type="bullet">
    /// <item>SAM model cases (S1 setpoint, S2 glazing): the source <c>.sam</c> (<c>SAM_APPLY_ACCEPTANCE_SAM</c>) is copied,
    /// opened, simulated with the ordinary Energy Simulation core (<see cref="Modify.RunPartOSimulation"/> with no Part O run,
    /// the dialog's options, full year), then the real Design Optimisation window reads its folder on screen with the
    /// licensed readers and the application's glazing pool (the open model first), runs the optimisation with the installed
    /// TasGenExecute, shows the plan and applies it with the licensed writers. The Tas files and the model are checked
    /// against the best point, then Energy Simulation runs again on the changed model and its TBD and TSD are compared with
    /// the best simulation.</item>
    /// <item>Systems Demo case (D1, <c>SAM_APPLY_ACCEPTANCE_DEMO</c>): no model is open, a plant controller by golden section;
    /// the TPD alone is changed.</item>
    /// </list>
    /// <c>SAM_APPLY_ACCEPTANCE_CASES</c> selects cases (default S1,S2,D1).
    /// </summary>
    [Collection(WpfCollection.Name)]
    public class TasModelApplyAcceptanceHarness : IDisposable
    {
        private string log = string.Empty;

        public TasModelApplyAcceptanceHarness()
        {
            TasOptimisationWindow.ResetSession();
        }

        public void Dispose()
        {
            TasOptimisationWindow.ResetSession();
        }

        private void Log(string text)
        {
            File.AppendAllText(log, DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " " + text + Environment.NewLine, Encoding.UTF8);
        }

        private static string Raw(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string Raw(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        [WpfFact]
        public async Task Licensed_acceptance_of_apply_best_design()
        {
            string? output = Environment.GetEnvironmentVariable("SAM_APPLY_ACCEPTANCE");
            if (string.IsNullOrWhiteSpace(output))
            {
                return;
            }

            string sam = Environment.GetEnvironmentVariable("SAM_APPLY_ACCEPTANCE_SAM") ?? @"C:\Users\michal\Documents\SAM_daily\2026-07-15 PartO\SAM_zoningAM.sam";
            string demo = Environment.GetEnvironmentVariable("SAM_APPLY_ACCEPTANCE_DEMO") ?? @"C:\TasOut\pr7b\demo";
            HashSet<string> selected = new HashSet<string>((Environment.GetEnvironmentVariable("SAM_APPLY_ACCEPTANCE_CASES") ?? "S1,S2,D1").Split(',').Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase);
            Directory.CreateDirectory(output);

            if (selected.Contains("S1"))
            {
                await SamCase("S1", sam, output, false);
            }

            if (selected.Contains("S2"))
            {
                await SamCase("S2", sam, output, true);
            }

            if (selected.Contains("D1"))
            {
                await DemoCase("D1", demo, output);
            }
        }

        private async Task SamCase(string id, string source, string output, bool glazing)
        {
            log = Path.Combine(output, id + ".log");
            File.WriteAllText(log, string.Empty);
            string work = Path.Combine(output, id + "-ws");
            if (Directory.Exists(work))
            {
                Directory.Delete(work, true);
            }

            Directory.CreateDirectory(work);
            string path = Path.Combine(work, Path.GetFileName(source));
            File.Copy(source, path);
            Log("case " + id + " source " + source + " (sha256 " + Analytical.Tas.GenOpt.Query.FileHash(source) + ") copied to " + path);

            TasOptimisationWindow? window = null;
            try
            {
                AnalyticalModel model = Core.Convert.ToSAM<AnalyticalModel>(path).OfType<AnalyticalModel>().Single();
                Log("model '" + model.Name + "': " + model.AdjacencyCluster.GetSpaces().Count + " spaces, " + model.AdjacencyCluster.GetApertures().Count + " apertures, aperture constructions " + string.Join(", ", model.AdjacencyCluster.GetApertureConstructions().GroupBy(x => x.Guid).Select(x => x.First().Name + " (" + model.AdjacencyCluster.GetApertures(x.First()).Count + ")")));

                GlazingSource user = GlazingSource.FromUserLibrary(new UserGlazingLibrary());
                Log("My glazing systems: " + (user.ConstructionManager?.ApertureConstructions?.Count ?? 0) + " systems" + (string.IsNullOrWhiteSpace(user.Note) ? string.Empty : " (" + user.Note + ")"));
                if (glazing)
                {
                    //The open model as a glazing source: the windows get the default library's SIM_EXT_GLZ_SKY (higher g) the
                    //way the Glazing window assigns a system, and the model keeps its SIM_EXT_GLZ, now assigned to nothing.
                    GlazingSource library = GlazingSource.FromDefaultLibrary();
                    ApertureConstruction current = model.AdjacencyCluster.GetApertureConstructions().First(x => x.Name == "SIM_EXT_GLZ");
                    ApertureConstruction sky = library.ConstructionManager.ApertureConstructions.First(x => x.Name == "SIM_EXT_GLZ_SKY" && x.ApertureType == ApertureType.Window);
                    List<IMaterial> materials = (sky.PaneConstructionLayers ?? new List<ConstructionLayer>()).Concat(sky.FrameConstructionLayers ?? new List<ConstructionLayer>())
                        .Select(x => library.ConstructionManager.GetMaterial(x.Name)).Where(x => x != null).Cast<IMaterial>().ToList();
                    SetGlazingRequest request = new SetGlazingRequest()
                    {
                        SourceApertureConstructionGuid = current.Guid,
                        ApertureConstruction = sky,
                        MaterialsToAdd = materials,
                        Scope = ThermalApplyScope.AllUsing,
                    };

                    AnalyticalModel? glazed = Modify.SetGlazing(model, request, null!, out SetGlazingResult setGlazingResult);
                    if (glazed == null || !setGlazingResult.Succeeded)
                    {
                        Log("SetGlazing failed: " + setGlazingResult?.Error);
                        return;
                    }

                    model = glazed;
                    Log("windows given " + sky.Name + " " + sky.Guid + " (" + setGlazingResult.ApertureCount + " apertures); kept in the model, assigned to nothing: " + current.Name + " " + current.Guid);
                }

                if (!glazing)
                {
                    //The source has cooling off everywhere: two rooms get one shared 24-hour cooling profile (23 °C 08-20,
                    //"no cooling" otherwise), as an engineer would before optimising its setpoint.
                    Profile cooling = new Profile("PR9 Cooling 23 08-20", ProfileType.Cooling, Enumerable.Range(0, 24).Select(h => h >= 8 && h < 20 ? 23.0 : 150.0));
                    ProfileLibrary profileLibrary = model.ProfileLibrary;
                    profileLibrary.Add(cooling);
                    AdjacencyCluster adjacencyCluster = model.AdjacencyCluster;
                    foreach (Space space in adjacencyCluster.GetSpaces().Where(x => x.Name == "Studio 1_0" || x.Name == "Bedroom 2_3"))
                    {
                        InternalCondition internalCondition = space.InternalCondition;
                        internalCondition.SetProfileName(ProfileType.Cooling, cooling.Name);
                        adjacencyCluster.AddObject(new Space(space) { InternalCondition = internalCondition });
                        Log("space " + space.Name + " (internal condition " + internalCondition.Name + ") cooling profile set to " + cooling.Name);
                    }

                    model = new AnalyticalModel(model, adjacencyCluster, model.MaterialLibrary, profileLibrary);
                }

                UIAnalyticalModel ui = new UIAnalyticalModel(model) { Path = path };
                string projectName = model.Name;

                // Energy Simulation 1: the ordinary core, the dialog's options with Full Year 1-365.
                Stopwatch stopwatch = Stopwatch.StartNew();
                AnalyticalModel? simulated = EnergySimulation(ui, projectName, work, out string? path_TSD);
                Log("energy simulation 1 in " + stopwatch.Elapsed.TotalSeconds.ToString("0", CultureInfo.InvariantCulture) + " s: " + (simulated == null ? "FAILED" : "TSD " + path_TSD));
                if (simulated == null)
                {
                    return;
                }

                ui.SetJSAMObject(simulated, new Core.UI.FullModification());
                TasModelInventory inventory_1 = Analytical.Tas.GenOpt.Query.TasModelInventory(work);
                LogInventory("after energy simulation 1", inventory_1);

                if (!glazing)
                {
                    //For the real-application smoke: the simulated model saved beside a copy of its Tas files.
                    string app = Path.Combine(output, "app-ws");
                    if (Directory.Exists(app))
                    {
                        Directory.Delete(app, true);
                    }

                    Directory.CreateDirectory(app);
                    UIAnalyticalModel saved = new UIAnalyticalModel(ui.JSAMObject) { Path = Path.Combine(app, Path.GetFileName(path)) };
                    Log("app workspace " + app + ": model saved " + saved.Save());
                    foreach (string file in Directory.GetFiles(work).Where(NativeGenOptWorkspace.IsTasFile))
                    {
                        File.Copy(file, Path.Combine(app, Path.GetFileName(file)));
                    }
                }

                // The window, as Simulate > Optimisation opens it for this model.
                TasOptimisationWindow.ResetSession();
                window = new TasOptimisationWindow(work, ui.JSAMObject, null, () => new[] { GlazingSource.FromModel(ui.JSAMObject), GlazingSource.FromDefaultLibrary(), user })
                {
                    UIAnalyticalModel = ui,
                };

                Text(window, "textBox_RunsDirectory", Path.Combine(output, id + "-r"));
                window.Show();
                await ReadModel(window);

                if (glazing)
                {
                    //The default filter, untouched; the objective is overheating (the model has no cooling).
                    window.AddTarget(window.CanChangeItems.First(x => x.IsChoice));
                    window.AddMeasure(Item(window.CanMeasureItems, "Overheating hours"));
                    window.AddMeasure(Item(window.CanMeasureItems, "Annual cooling demand"));
                }
                else
                {
                    TasOptimisationParameterRow row = window.AddTarget(Item(window.CanChangeItems, "Studio 1_0 cooling setpoint"))!;
                    row.Minimum = "23";
                    row.Maximum = "26";
                    Text(window, "textBox_Tolerance", "10");
                    window.AddMeasure(Item(window.CanMeasureItems, "Annual cooling demand"));
                    window.AddMeasure(Item(window.CanMeasureItems, "Overheating hours"));
                }

                Refresh(window);

                if (!await RunOptimisation(window, output, id, 30))
                {
                    return;
                }

                await Apply(window, output, id, work);

                // Energy Simulation 2: the ordinary core again, on the model as it now is.
                stopwatch.Restart();
                AnalyticalModel? simulated_2 = EnergySimulation(ui, projectName, work, out string? path_TSD_2);
                Log("energy simulation 2 in " + stopwatch.Elapsed.TotalSeconds.ToString("0", CultureInfo.InvariantCulture) + " s: " + (simulated_2 == null ? "FAILED" : "TSD " + path_TSD_2));
                if (simulated_2 == null)
                {
                    return;
                }

                TasModelInventory inventory_2 = Analytical.Tas.GenOpt.Query.TasModelInventory(work);
                LogInventory("after energy simulation 2", inventory_2);
                CompareWithBest(window, inventory_1, inventory_2, glazing);

                if (glazing)
                {
                    //The objective (overheating) has no reader outside the generated script: measure Energy Simulation 2's
                    //model with "Test one simulation" of a fresh window (option 1 = the model as it now is).
                    double best = window.Report!.BestObjectives[0];
                    window.Close();
                    TasOptimisationWindow.ResetSession();
                    window = new TasOptimisationWindow(work, ui.JSAMObject, null, () => new[] { GlazingSource.FromModel(ui.JSAMObject), GlazingSource.FromDefaultLibrary() }) { UIAnalyticalModel = ui };
                    Text(window, "textBox_RunsDirectory", Path.Combine(output, id + "-r2"));
                    window.Show();
                    await ReadModel(window);
                    Text(window, "textBox_GlazingUgAllowance", "5");
                    Text(window, "textBox_GlazingLightAllowance", "1");
                    Refresh(window);
                    window.AddTarget(window.CanChangeItems.First(x => x.IsChoice));
                    window.AddMeasure(Item(window.CanMeasureItems, "Overheating hours"));
                    window.AddMeasure(Item(window.CanMeasureItems, "Annual cooling demand"));
                    Refresh(window);
                    await window.TestAsync();
                    double measured = window.TestReport?.Outputs.FirstOrDefault() ?? double.NaN;
                    Log("energy simulation 2's model, Test one simulation (option 1 = as applied): " + window.TestReport?.ToText().Replace(Environment.NewLine, " | "));
                    Log("overheating hours: best simulation of the run " + Raw(best) + ", energy simulation 2's model " + Raw(measured) + " -> " + (measured == best ? "PASS (equal)" : "DIFFERENT"));
                }
            }
            catch (Exception exception)
            {
                Log("EXCEPTION " + exception);
            }
            finally
            {
                window?.Close();
                LogProcesses();
            }
        }

        private async Task DemoCase(string id, string source, string output)
        {
            log = Path.Combine(output, id + ".log");
            File.WriteAllText(log, string.Empty);
            string work = Path.Combine(output, id + "-ws");
            if (Directory.Exists(work))
            {
                Directory.Delete(work, true);
            }

            Directory.CreateDirectory(work);
            foreach (string path in Directory.GetFiles(source).Where(NativeGenOptWorkspace.IsTasFile))
            {
                File.Copy(path, Path.Combine(work, Path.GetFileName(path)));
            }

            Log("case " + id + " source " + source + " copied to " + work + " " + string.Join(", ", Analytical.Tas.GenOpt.Query.TasFileHashes(work).Select(x => x.Key + " " + x.Value.Substring(0, 8))));
            TasOptimisationWindow? window = null;
            try
            {
                TasOptimisationWindow.ResetSession();
                window = new TasOptimisationWindow(work, null, null, () => new[] { GlazingSource.FromDefaultLibrary() });
                Text(window, "textBox_RunsDirectory", Path.Combine(output, id + "-r"));
                window.Show();
                await ReadModel(window);

                TasOptimisationParameterRow row = window.AddTarget(Item(window.CanChangeItems, "HeatPumpController setpoint (Plant Room)"))!;
                row.Minimum = "-5";
                row.Maximum = "35";
                window.AddMeasure(Item(window.CanMeasureItems, "Annual plant energy"));
                window.AddMeasure(Item(window.CanMeasureItems, "Annual plant CO2"));
                Text(window, "textBox_Tolerance", "4");
                Refresh(window);

                if (!await RunOptimisation(window, output, id, 30))
                {
                    return;
                }

                Dictionary<string, string> hashes = Analytical.Tas.GenOpt.Query.TasFileHashes(work);
                await Apply(window, output, id, work);
                Dictionary<string, string> hashes_After = Analytical.Tas.GenOpt.Query.TasFileHashes(work);
                foreach (KeyValuePair<string, string> pair in hashes)
                {
                    Log("file " + pair.Key + ": " + (hashes_After[pair.Key] == pair.Value ? "unchanged" : "CHANGED"));
                }
            }
            catch (Exception exception)
            {
                Log("EXCEPTION " + exception);
            }
            finally
            {
                window?.Close();
                LogProcesses();
            }
        }

        /// <summary>
        /// The ordinary Energy Simulation core (no Part O run), with the dialog's options, Full Year 1-365 and Sizing off: the
        /// dialog's default Sizing leaves two more TBDs (_HDDCDD, _Uncapped) that the "tas-model" reader refuses (PR9 finding).
        /// </summary>
        private static AnalyticalModel? EnergySimulation(UIAnalyticalModel ui, string projectName, string outputDirectory, out string? path_TSD)
        {
            SimulateOptions simulateOptions = UI.Create.SimulateOptions(ui);
            WeatherData? weatherData = simulateOptions.WeatherData;
            PartOSimulationContext partOSimulationContext = new PartOSimulationContext(outputDirectory, projectName, weatherData, simulateOptions.SolarCalculationMethod, 1, 365)
            {
                UnmetHours = simulateOptions.UnmetHours,
                Sizing = false,
                UseWidths = simulateOptions.UseWidths,
                UpdateConstructionLayersByPanelType = simulateOptions.UpdateConstructionLayersByPanelType,
                DirectT3D = simulateOptions.DirectT3D,
            };

            AnalyticalModel analyticalModel = new AnalyticalModel(ui.JSAMObject) { Name = projectName };
            AnalyticalModel? result = Modify.RunPartOSimulation(analyticalModel, partOSimulationContext, projectName, null!, CancellationToken.None, out string _, out path_TSD, out bool cancelled, out bool _, out List<string> _, out string refusal);
            return cancelled || refusal != null ? null : result;
        }

        private async Task ReadModel(TasOptimisationWindow window)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            await Until(() => window.ModelSession.State == TasModelReadState.Ready || window.ModelSession.State == TasModelReadState.Failed, 300);
            await Until(() => !window.ModelSession.PoolBusy && (window.ModelSession.State != TasModelReadState.Ready || window.ModelSession.Sources.Count != 0), 300);
            await window.ModelTask;
            Log("model " + window.ModelSession.State + " in " + window.ModelSession.ReadDuration.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s, pool ready after " + stopwatch.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s: " + ((TextBlock)window.FindName("textBlock_ModelStatus")).Text);
            foreach (TasGlazingSystem system in window.ModelSession.Pool)
            {
                Log(string.Format(CultureInfo.InvariantCulture, "pool system: {0} [{1}] {2} {3} transparent={4} g={5} Ug={6} light={7}", system.Name, system.ShortId, system.Source, system.ApertureType, system.Transparent, system.G, system.Ug, system.Light));
            }

            Refresh(window);
        }

        private async Task<bool> RunOptimisation(TasOptimisationWindow window, string output, string id, int minutes)
        {
            foreach (TasModelCatalogueItem item in window.CanChangeItems.Where(x => x.IsChoice))
            {
                Log("can change: " + item.Name + " | " + item.OptionsText.Replace("\n", " | "));
            }

            foreach (TasOptimisationCheck check in window.Checks)
            {
                Log("check: " + check);
            }

            window.Input.TryGetDefinition(out OptimisationDefinition? definition, out _);
            if (definition != null)
            {
                File.WriteAllText(Path.Combine(output, id + "-definition.json"), definition.ToJson(), new UTF8Encoding(false));
            }

            Render(window, output, id + "-1-setup.png", 1700);
            if (!window.Checks.CanRun())
            {
                Log("NOT RUNNABLE");
                return false;
            }

            DateTime deadline = DateTime.UtcNow.AddMinutes(minutes);
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
            Stopwatch stopwatch = Stopwatch.StartNew();
            await window.RunAsync();
            watchdog.Stop();

            Log("run in " + stopwatch.Elapsed.TotalSeconds.ToString("0", CultureInfo.InvariantCulture) + " s: " + window.Report?.ToText().Replace(Environment.NewLine, " | "));
            foreach (TasOptimisationTraceRow row in window.TraceRows)
            {
                Log("trace: " + row.Simulation + " " + row.Event + " " + string.Join(", ", row.CoordinateTexts) + " -> " + string.Join(", ", row.OutputTexts) + " | raw " + string.Join(", ", row.CoordinateRaw) + " -> " + string.Join(", ", row.OutputRaw));
            }

            Render(window, output, id + "-2-result.png", 1100);
            return window.Report?.Successful == true;
        }

        private async Task Apply(TasOptimisationWindow window, string output, string id, string work)
        {
            Dictionary<string, string> hashes = Analytical.Tas.GenOpt.Query.TasFileHashes(work);
            TasModelApplyPlan plan = window.PrepareApply();
            Log("plan: " + plan.Mode + " | " + plan.Headline + " | can apply " + plan.CanApply);
            foreach (TasModelApplyItem item in plan.Items)
            {
                Log("plan item: " + item.Line.Detail + " || raw: " + item.Line.ToolTipText);
            }

            foreach (string note in plan.Notes)
            {
                Log("plan note: " + note);
            }

            Render(window, output, id + "-3-plan.png", 1300);
            if (!plan.CanApply)
            {
                Log("NOT APPLICABLE");
                return;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            TasModelApplyOutcome? outcome = await window.ApplyAsync();
            Log("apply in " + stopwatch.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s: " + (outcome == null ? "nothing" : (outcome.Succeeded ? "SUCCEEDED " : "FAILED ") + outcome.Headline));
            foreach (TasOptimisationCheck line in outcome?.Lines ?? new List<TasOptimisationCheck>())
            {
                Log("applied: " + line.Title + ": " + line.Detail + " || " + line.ToolTipText);
            }

            Render(window, output, id + "-4-applied.png", 1300);
            if (outcome?.Succeeded != true)
            {
                return;
            }

            // The backup holds the files as they were.
            string? backup = outcome.TasResult?.BackupFolder;
            foreach (string file in outcome.TasResult?.FilesReplaced ?? new List<string>())
            {
                Log("backup " + file + ": " + (Analytical.Tas.GenOpt.Query.FileHash(Path.Combine(backup!, file)) == hashes[file] ? "equals the original" : "DIFFERS from the original"));
            }

            // The Tas files read back (licensed, read-only), and the open model.
            TasModelInventory inventory = Analytical.Tas.GenOpt.Query.TasModelInventory(work);
            double best = window.Report!.BestPoint[0];
            TasModelApplyItem applied = plan.Items[0];
            TasModelDesignChange change = applied.Change;
            if (change.IsSetpoint)
            {
                TasSetpointProfile? profile = inventory.InternalConditions.Single(x => x.Name == change.InternalCondition).Cooling;
                List<float> hours = profile?.Hours?.ToList() ?? new List<float>();
                Log("TBD " + change.InternalCondition + " cooling hours now: " + string.Join(" ", hours.Select(Raw)) + "; (float)best = " + Raw((float)best) + " in " + hours.Count(x => x == (float)best) + " hours -> " + (hours.Count(x => x == (float)best) > 0 ? "PASS" : "FAIL"));
                AnalyticalModel model = window.UIAnalyticalModel!.JSAMObject;
                Space space = model.AdjacencyCluster.GetSpaces().Single(x => x.Name == change.InternalCondition);
                Profile cooling = space.InternalCondition.GetProfile(ProfileType.Cooling, model.ProfileLibrary);
                double[] values = Query.TasHours(cooling);
                int exact = values.Count(x => BitConverter.DoubleToInt64Bits(x) == BitConverter.DoubleToInt64Bits(best));
                Log("SAM model space " + space.Name + " cooling profile '" + cooling.Name + "': " + string.Join(" ", values.Select(Raw)) + "; best " + Raw(best) + " bit-exact in " + exact + " hours -> " + (exact > 0 ? "PASS" : "FAIL"));
            }
            else if (change.IsGlazing)
            {
                TasGlazingOption option = change.GlazingOption!;
                TasGlazingConstructionInfo? info = inventory.GlazingConstructions.FirstOrDefault(x => x.Name == option.PaneConstruction);
                Log("TBD glazing: " + string.Join(" | ", inventory.GlazingConstructions.Select(x => x.Name + " on " + x.Elements.Count + " elements g " + Raw(x.G) + " U " + Raw(x.U) + " light " + Raw(x.Light))) + "; option " + option.Number + " " + option.Text + " (" + option.Source + ") pane " + option.PaneConstruction + " g " + Raw(option.G) + " -> " + (info != null ? "PASS" : "FAIL"));
                AnalyticalModel model = window.UIAnalyticalModel!.JSAMObject;
                foreach (IGrouping<Guid, ApertureConstruction> group in model.AdjacencyCluster.GetApertureConstructions().GroupBy(x => x.Guid))
                {
                    ApertureConstruction apertureConstruction = group.First();
                    Log("SAM model aperture construction " + apertureConstruction.Name + " (" + model.AdjacencyCluster.GetApertures(apertureConstruction).Count + " apertures): pane " + string.Join("/", apertureConstruction.PaneConstructionLayers.Select(x => x.Name + " " + Raw(x.Thickness))) + "; frame " + string.Join("/", (apertureConstruction.FrameConstructionLayers ?? new List<ConstructionLayer>()).Select(x => x.Name + " " + Raw(x.Thickness))));
                }
            }
            else
            {
                TasPlantControllerInfo controller = inventory.PlantRooms.Single(x => x.Name == change.PlantRoom).Controllers.Single(x => x.Name == change.Controller);
                Log("TPD " + change.Controller + " setpoint now " + Raw(controller.Setpoint) + "; best " + Raw(best) + " -> " + (BitConverter.DoubleToInt64Bits(controller.Setpoint) == BitConverter.DoubleToInt64Bits(best) ? "PASS (bit-exact)" : "FAIL"));
            }
        }

        /// <summary>Energy Simulation of the applied model against the best simulation of the run.</summary>
        private void CompareWithBest(TasOptimisationWindow window, TasModelInventory inventory_1, TasModelInventory inventory_2, bool glazing)
        {
            TasOptimisationReport report = window.Report!;
            double objective = report.BestObjectives[glazing ? 1 : 0];
            double? cooling = inventory_2.CoolingDemand;
            Log("annual cooling demand: energy simulation 1 " + (inventory_1.CoolingDemand.HasValue ? Raw(inventory_1.CoolingDemand.Value) : "-") + ", best simulation of the run " + Raw(objective) + ", energy simulation 2 " + (cooling.HasValue ? Raw(cooling.Value) : "-") + (cooling.HasValue ? "; difference " + Raw(cooling.Value - objective) + (cooling.Value == objective ? " (bit-identical)" : string.Empty) : string.Empty));

            if (!glazing)
            {
                TasSetpointProfile? before = inventory_1.InternalConditions.Single(x => x.Name == "Studio 1_0").Cooling;
                TasSetpointProfile? after = inventory_2.InternalConditions.Single(x => x.Name == "Studio 1_0").Cooling;
                float best = (float)report.BestPoint[0];
                float setpoint = (float)(before?.Setpoint ?? double.NaN);
                List<float> expected = (before?.Hours ?? new List<float>()).Select(x => x == setpoint ? best : x).ToList();
                bool same = after?.Hours != null && after.Hours.SequenceEqual(expected);
                Log("energy simulation 2 TBD Studio 1_0 cooling hours: " + string.Join(" ", (after?.Hours ?? new List<float>()).Select(Raw)) + " -> " + (same ? "PASS (the best value in the hours at the setpoint, the setback kept)" : "FAIL"));
                foreach (TasInternalConditionInfo info in inventory_1.InternalConditions.Where(x => x.Name != "Studio 1_0"))
                {
                    TasInternalConditionInfo? other = inventory_2.InternalConditions.FirstOrDefault(x => x.Name == info.Name);
                    bool unchanged = other != null && Same(info.Heating, other.Heating) && Same(info.Cooling, other.Cooling);
                    if (!unchanged)
                    {
                        Log("energy simulation 2: internal condition " + info.Name + " CHANGED");
                    }
                }
            }
            else
            {
                Log("energy simulation 2 TBD glazing: " + string.Join(" | ", inventory_2.GlazingConstructions.Select(x => x.Name + " on " + string.Join("/", x.Elements) + " g " + Raw(x.G) + " U " + Raw(x.U) + " light " + Raw(x.Light))));
            }
        }

        private static bool Same(TasSetpointProfile? x, TasSetpointProfile? y)
        {
            if (x == null || y == null)
            {
                return x == null && y == null;
            }

            return x.Type == y.Type && x.Setpoint == y.Setpoint && (x.Hours == null ? y.Hours == null : y.Hours != null && x.Hours.SequenceEqual(y.Hours));
        }

        private void LogInventory(string title, TasModelInventory inventory)
        {
            Log(title + ": files " + inventory.TbdFileName + " " + inventory.TsdFileName + " " + inventory.TpdFileName + "; cooling demand " + (inventory.CoolingDemand.HasValue ? Raw(inventory.CoolingDemand.Value) : "-") + " kWh");
            foreach (TasInternalConditionInfo info in inventory.InternalConditions.Where(x => x.Name.StartsWith("Studio", StringComparison.Ordinal)))
            {
                Log("  " + info.Name + ": cooling " + info.Cooling?.Type + " " + (info.Cooling?.Setpoint.HasValue == true ? Raw(info.Cooling.Setpoint!.Value) : "-") + " hours " + string.Join(" ", info.Cooling?.Hours?.Select(Raw) ?? new string[0]));
            }

            foreach (TasGlazingConstructionInfo info in inventory.GlazingConstructions)
            {
                Log("  glazing " + info.Name + " on " + string.Join("/", info.Elements) + " g " + Raw(info.G) + " U " + Raw(info.U) + " light " + Raw(info.Light));
            }
        }

        private void LogProcesses()
        {
            string[] names = { "TBD", "TSD", "TPD", "TCD", "TasGenExecute", "Tas3D", "TAS3D" };
            List<string> running = Process.GetProcesses().Where(x => names.Any(y => string.Equals(x.ProcessName, y, StringComparison.OrdinalIgnoreCase))).Select(x => x.ProcessName + " " + x.Id).ToList();
            Log("Tas processes left: " + (running.Count == 0 ? "none" : string.Join(", ", running)));
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
