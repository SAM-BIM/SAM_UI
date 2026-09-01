// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas;
using SAM.Core;
using SAM.Core.Tas;
using SAM.Core.UI;
using SAM.Core.UI.WPF;
using SAM.Core.Windows.WPF;
using SAM.Weather;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        public static void Simulate(this UIAnalyticalModel uIAnalyticalModel)
        {
            Simulate(uIAnalyticalModel, null);
        }

        /// <summary>
        /// Runs the TAS workflow, optionally completing the session's Approved Document O run with the model
        /// the workflow returned and the results it wrote.
        /// </summary>
        /// <param name="partORun">
        /// The session's Part O run, or null where the caller has none.
        /// <para>
        /// <b>Only a run that is <see cref="PartORunState.Prepared"/> is completed here</b>, and only by a
        /// workflow that actually ran. In every other case this method's own model replacement reaches the run
        /// as an unexpected modification and drops it - which is the intended outcome, not a side effect: a
        /// second simulation, or a simulation of a model that was edited after preparation, must not be paired
        /// with the earlier preparation's overheating scenarios.
        /// </para>
        /// </summary>
        public static void Simulate(this UIAnalyticalModel uIAnalyticalModel, PartORun partORun)
        {
            AnalyticalModel analyticalModel = uIAnalyticalModel?.JSAMObject;
            if(analyticalModel == null)
            {
                return;
            }

            SimulateWindow simulateWindow = new SimulateWindow();
            ActiveSetting.Setting.TryGetValue(AnalyticalSettingParameter.SimulateOptions, out SimulateOptions simulateOptions);
            if(simulateOptions == null)
            {
                simulateOptions = UI.Create.SimulateOptions(uIAnalyticalModel);
            }

            simulateWindow.ProjectName = analyticalModel.Name;

            if (!string.IsNullOrWhiteSpace(uIAnalyticalModel.Path))
            {
                simulateWindow.OutputDirectory = System.IO.Path.GetDirectoryName(uIAnalyticalModel.Path);
            }

            simulateWindow.SimulateOptions = simulateOptions;

            if (analyticalModel.TryGetValue(Analytical.AnalyticalModelParameter.WeatherData, out WeatherData weatherData))
            {
                simulateWindow.WeatherData = weatherData;
            }

            bool? showdialog = simulateWindow.ShowDialog();
            if(showdialog == null || !showdialog.HasValue || !showdialog.Value)
            {
                return;
            }

            ActiveSetting.Setting.SetValue(AnalyticalSettingParameter.SimulateOptions, simulateWindow.SimulateOptions);

            string projectName = simulateWindow.ProjectName;
            string outputDirectory = simulateWindow.OutputDirectory;
            bool unmetHours = simulateWindow.UnmetHours;
            bool printRoomDataSheets = simulateWindow.RoomDataSheets;

            bool fullYearSimulation = simulateWindow.FullYearSimulation;
            int fullYearSimulation_From = simulateWindow.FullYearSimulation_From;
            int fullYearSimulation_To = simulateWindow.FullYearSimulation_To;

            bool createSAP = simulateWindow.CreateSAP;
            bool createTM59 = simulateWindow.CreateTM59;
            bool createTPD = simulateWindow.CreateTPD;
            bool createPartL = simulateWindow.CreatePartL;

            bool sizing = simulateWindow.Sizing;

            bool useWidths = simulateWindow.UseWidths;

            SolarCalculationMethod solarCalculationMethod = simulateWindow.SolarCalculationMethod;
            bool updateConstructionLayersByPanelType = simulateWindow.UpdateConstructionLayersByPanelType;

            TextMap textMap = simulateWindow.SelectedTextMap;
            weatherData = simulateWindow.SelectedWeatherData;
            string zoneCategory = simulateWindow.SelectedZoneCategory;

            if (!simulateWindow.Simulate && !createSAP && !createTM59)
            {
                return;
            }

            // From here on the model is mutated in place - the name immediately below, materials at "Update
            // Materials". Those writes would land on the instance the user still has open, so a cancelled run
            // would leave it renamed and re-materialled with no modification notification ever issued: changed
            // behind the UI's back. Work on a copy instead. On success the copy is what SetJSAMObject adopts,
            // which is already how this ends - UpdateConstructionLayersByPanelType and the workflow both return
            // fresh instances. The copy constructor carries the Guid over, so identity does not change.
            analyticalModel = new AnalyticalModel(analyticalModel);

            analyticalModel.Name = projectName;

            DateTime dateTime = DateTime.Now;

            string path_Xml = null;
            if(solarCalculationMethod == SolarCalculationMethod.TAS)
            {
                path_Xml = System.IO.Path.Combine(outputDirectory, projectName + ".xml");
                if(!gbXML.Convert.ToFile(analyticalModel, path_Xml))
                {
                    MessageBox.Show("Could not create gbXML file.");
                    return;
                }
            }

            string path_TBD = System.IO.Path.Combine(outputDirectory, projectName + ".tbd");

            bool shadingUpdated = false;

            bool converted = false;

            bool cancelled = false;

            // Whether WorkflowCalculator actually ran and returned a model. The only condition under which the
            // adopted model is a workflow output, and so the only one under which a Part O run may be completed.
            bool workflowCompleted = false;

            // Reported at the end rather than raised as they happen: a space left without a TAS zone identity
            // is a gap in whatever is exported next - and the DomOv XML does NOT refuse such a space, it
            // writes the SAM space guid in place of the TAS zone guid, so nothing downstream announces the
            // gap. A run that says "converted" while quietly having produced that is the failure mode being
            // closed. A dialog per space would be unusable on a block of flats.
            List<string> notes_Simulate = [];

            if(simulateWindow.Simulate)
            {
                // One token spans the preparation steps below and the workflow that follows them, so a single
                // Cancel click aborts whichever of the two is running.
                using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
                {
                    CancellationToken cancellationToken = cancellationTokenSource.Token;

                    // Hosted off this thread rather than shown on it: "Converting to TBD" and "Updating
                    // Shading" below are single COM calls that run for minutes, and Windows ghosts a window
                    // whose thread has stopped pumping and then discards clicks on the ghost - so a Cancel
                    // button on this thread would silently lose the click. Not a using either: see
                    // Modify.RunWorkflow for why the host must be disposed before the final token check.
                    ProgressWindowHost progressWindowHost = new ProgressWindowHost("Preparing Model", 8, true, Analytical.Tas.Query.CancelNote(null));

                    // Announce the stage, then observe - the order WorkflowCalculator.Step uses, so a click
                    // that lands while the note is being updated is still seen before the stage starts work.
                    Action<string> step = description =>
                    {
                        progressWindowHost.Note = Analytical.Tas.Query.CancelNote(description);
                        progressWindowHost.Update(description);
                        cancellationToken.ThrowIfCancellationRequested();
                    };

                    try
                    {
                        progressWindowHost.CancelRequested += (s, e) => cancellationTokenSource.Cancel();

                        step("Update Materials");

                        IEnumerable<IMaterial> materials = Analytical.Query.Materials(analyticalModel.AdjacencyCluster, Analytical.Query.DefaultMaterialLibrary());
                        if (materials != null)
                        {
                            foreach (IMaterial material in materials)
                            {
                                if (analyticalModel.HasMaterial(material))
                                {
                                    continue;
                                }

                                analyticalModel.AddMaterial(material);
                            }
                        }

                        step("Update ConstructionLayers By PanelTypes");

                        analyticalModel = updateConstructionLayersByPanelType ? analyticalModel.UpdateConstructionLayersByPanelType() : analyticalModel;

                        if (System.IO.File.Exists(path_TBD))
                        {
                            try
                            {
                                System.IO.File.Delete(path_TBD);
                            }
                            catch
                            {
                                // Take the dialog down before the message box: it is topmost and lives on
                                // another thread, so a modal shown under it can end up hidden behind it.
                                progressWindowHost.Dispose();
                                MessageBox.Show("Cannot override existing TBD file.");
                                return;
                            }
                        }

                        if (solarCalculationMethod == SolarCalculationMethod.SAM)
                        {
                            List<int> hoursOfYear = Analytical.Query.DefaultHoursOfYear();

                            SolarCalculator.Modify.Simulate(analyticalModel, hoursOfYear.ConvertAll(x => new DateTime(2018, 1, 1).AddHours(x)), false, Tolerance.MacroDistance, Tolerance.MacroDistance, 0.012, Tolerance.Distance);

                            using (SAMTBDDocument sAMTBDDocument = new SAMTBDDocument(path_TBD))
                            {
                                TBD.TBDDocument tBDDocument = sAMTBDDocument.TBDDocument;

                                step("Updating WeatherData");
                                Weather.Tas.Modify.UpdateWeatherData(tBDDocument, weatherData, analyticalModel == null ? 0 : analyticalModel.AdjacencyCluster.BuildingHeight());

                                TBD.Calendar calendar = tBDDocument.Building.GetCalendar();

                                List<TBD.dayType> dayTypes = Query.DayTypes(calendar);
                                if (dayTypes.Find(x => x.name == "HDD") == null)
                                {
                                    TBD.dayType dayType = calendar.AddDayType();
                                    dayType.name = "HDD";
                                }

                                if (dayTypes.Find(x => x.name == "CDD") == null)
                                {
                                    TBD.dayType dayType = calendar.AddDayType();
                                    dayType.name = "CDD";
                                }

                                step("Converting to TBD");
                                Tas.Convert.ToTBD(analyticalModel, tBDDocument, true);

                                step("Updating Zones");
                                Tas.Modify.UpdateZones(tBDDocument.Building, analyticalModel, true);

                                step("Updating Shading");
                                shadingUpdated = Tas.Modify.UpdateShading(tBDDocument, analyticalModel);

                                sAMTBDDocument.Save();
                            }

                        }
                    }
                    catch (OperationCanceledException)
                    {
                        cancelled = true;
                    }
                    finally
                    {
                        progressWindowHost.Dispose();
                    }

                    // The host is down, so no further click can arrive and any in-flight one has already run:
                    // this observation is final - but only once the host confirms it actually shut down. If
                    // it could not, its thread is still live and may be sitting on a click nothing observed,
                    // so preparation cannot be reported as having completed uninterrupted.
                    if (!cancelled && (cancellationTokenSource.IsCancellationRequested || !progressWindowHost.ShutdownCompleted))
                    {
                        cancelled = true;
                    }

                    if (!cancelled)
                    {
                        List<DesignDay> heatingDesignDays = new List<DesignDay>() { Analytical.Query.HeatingDesignDay(weatherData) };
                        List<DesignDay> coolingDesignDays = new List<DesignDay>() { Analytical.Query.CoolingDesignDay(weatherData) };

                        SurfaceOutputSpec surfaceOutputSpec = new SurfaceOutputSpec("Tas.Simulate")
                        {
                            SolarGain = true,
                            Conduction = true,
                            ApertureData = true,
                            Condensation = false,
                            Convection = false,
                            LongWave = false,
                            Temperature = true
                        };

                        List<SurfaceOutputSpec> surfaceOutputSpecs = new List<SurfaceOutputSpec>() { surfaceOutputSpec };

                        int simulate_From = -1;
                        int simulate_To = -1;

                        bool simulate = fullYearSimulation;

                        if (simulate)
                        {
                            simulate_From = fullYearSimulation_From;
                            simulate_To = fullYearSimulation_To;
                        }

                        if (shadingUpdated)
                        {
                            if (!simulate)
                            {
                                simulate_From = 1;
                                simulate_To = 1;
                                simulate = true;
                            }
                        }

                        WeatherData weatherData_Workflow = null;
                        bool updateZones_Workflow = false;
                        if(solarCalculationMethod == SolarCalculationMethod.TAS)
                        {
                            weatherData_Workflow = weatherData;
                            updateZones_Workflow = true;
                        }

                        WorkflowSettings workflowSettings = new WorkflowSettings()
                        {
                            Path_TBD = path_TBD,
                            Path_gbXML = path_Xml,
                            WeatherData = weatherData_Workflow,
                            DesignDays_Heating = heatingDesignDays,
                            DesignDays_Cooling = coolingDesignDays,
                            SurfaceOutputSpecs = surfaceOutputSpecs,
                            UnmetHours = unmetHours,
                            Simulate = simulate,
                            Sizing = sizing,
                            UpdateZones = updateZones_Workflow,
                            UseWidths = useWidths,
                            SimulateFrom = simulate_From,
                            SimulateTo = simulate_To
                        };

                        analyticalModel = Modify.RunWorkflow(analyticalModel, workflowSettings, cancellationToken, out cancelled);

                        // A cancelled workflow returns null, so everything below - including writing the
                        // weather data back onto the model - has to be skipped, not just the room data sheets.
                        if (!cancelled && analyticalModel != null)
                        {
                            // The one place a Part O run may be completed from. Set here rather than inferred
                            // later from `converted`, which is also true when the .tbd was written without the
                            // workflow ever running - a model that never went through WorkflowCalculator does
                            // not carry the current TAS zone identities, so it is not a workflow output and must
                            // not be assessed as one.
                            workflowCompleted = true;

                            if (printRoomDataSheets)
                            {
                                if (!System.IO.Directory.Exists(outputDirectory))
                                {
                                    System.IO.Directory.CreateDirectory(outputDirectory);
                                }

                                UI.Modify.PrintRoomDataSheets(analyticalModel, outputDirectory);
                            }

                            analyticalModel.SetValue(Analytical.AnalyticalModelParameter.WeatherData, weatherData);
                            converted = true;
                        }
                    }
                }
            }

            // Skipped outright once cancelled: these read back the .tbd the cancelled run was part-way through
            // writing, and a partial file converts into a model that looks complete but is not.
            if(!cancelled && (createSAP || createTM59 || createPartL))
            {
                using (ProgressBarWindowManager progressBarWindowManager = new ProgressBarWindowManager("Convert to TBD", "Converting..."))
                {
                    if (!converted)
                    {
                        converted = Tas.Convert.ToTBD(analyticalModel, path_TBD, null, null, null, true);
                    }

                    if (converted)
                    {
                        AnalyticalModel analyticalModel_TBD = Tas.Convert.ToSAM(path_TBD, false, false);
                        if(analyticalModel != null && analyticalModel_TBD != null)
                        {
                            AdjacencyCluster adjacencyCluster = analyticalModel.AdjacencyCluster;

                            // Fills in a TAS zone identity for any space that has none, and leaves every space
                            // that already has one exactly as the workflow stamped it. This used to match by
                            // NAME for every space and overwrite unconditionally, which discarded the strong
                            // identity Modify.UpdateIds had just written and collapsed same-named rooms in
                            // different dwellings onto one zone - see Modify.RestampSimulationZoneIdentity for
                            // both defects, and for why the name fallback is still needed by the
                            // Simulate-unticked DomOv path (only that one: SAP is handed analyticalModel_TBD,
                            // which Tas.Convert.ToSAM stamps itself, and Part L reads no ZoneGuid).
                            List<Space> spaces_Restamped = Modify.RestampSimulationZoneIdentity(adjacencyCluster.GetSpaces(), analyticalModel_TBD.AdjacencyCluster?.GetSpaces(), out List<string> notes_ZoneIdentity);

                            foreach (Space space_Restamped in spaces_Restamped)
                            {
                                adjacencyCluster.AddObject(space_Restamped);
                            }

                            notes_Simulate.AddRange(notes_ZoneIdentity);

                            analyticalModel = new AnalyticalModel(analyticalModel, adjacencyCluster);
                        }

                        if (createTM59)
                        {
                            if (Tas.TM59.Modify.TryCreatePath(path_TBD, out string path_TM59))
                            {
                                Tas.TM59.Convert.ToXml(analyticalModel, path_TM59, new TM59Manager(textMap));
                            }
                        }

                        if (createSAP)
                        {
                            if (string.IsNullOrWhiteSpace(zoneCategory))
                            {
                                converted = false;
                            }
                            else
                            {
                                if (!Tas.SAP.Modify.TryCreatePath(path_TBD, out string path_SAP))
                                {
                                    converted = false;
                                }
                                else
                                {
                                    converted = Tas.SAP.Convert.ToFile(analyticalModel_TBD, path_SAP, zoneCategory, textMap);
                                }

                            }
                        }

                        if(createPartL)
                        {
                            converted = Tas.Create.TBD_ByPartL(analyticalModel, path_TBD, out string path_TBD_Destination);
                        }
                    }
                }
            }

            if(!cancelled && createTPD)
            {
                string directory = System.IO.Path.GetDirectoryName(path_TBD);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path_TBD);

                string path_TSD = System.IO.Path.Combine(directory, string.Format("{0}.{1}", fileName, "tsd"));
                if(System.IO.File.Exists(path_TSD))
                {
                    string path_TPD = System.IO.Path.Combine(directory, string.Format("{0}.{1}", fileName, "tpd"));

                    Tas.Create.TPD(path_TPD, path_TSD, analyticalModel);
                }
            }

            TimeSpan timeSpan = new TimeSpan(DateTime.Now.Ticks - dateTime.Ticks);

            string message;
            if (cancelled)
            {
                message = "Simulation cancelled.";
            }
            else
            {
                message = converted ? "Model successfuly converted." : "Model could not be converted.";
            }

            message += string.Format("\n Time elapsed: {0}min{1}sec", timeSpan.Minutes, timeSpan.Seconds);

            if (notes_Simulate.Count != 0)
            {
                // Capped, with the remainder counted rather than dropped silently. A model with many unzoned
                // or unexported spaces can produce one note each, and a dialog listing all of them is one
                // nobody reads - but a cap that hid how much it was hiding would be worse than either.
                const int count_NotesShown = 5;

                message += string.Format("\n\n{0}", string.Join("\n", notes_Simulate.GetRange(0, Math.Min(count_NotesShown, notes_Simulate.Count))));

                if (notes_Simulate.Count > count_NotesShown)
                {
                    message += string.Format("\n... and {0} more space(s) with no TAS zone identity.", notes_Simulate.Count - count_NotesShown);
                }
            }

            // Whether this run may complete a pending Part O run. Read BEFORE the model is adopted, because
            // adopting it raises the modification that consumes the armed expectation below.
            bool completePartORun = partORun != null && partORun.State == PartORunState.Prepared && workflowCompleted && !cancelled && analyticalModel != null;

            MessageBox.Show(message);

            if (completePartORun)
            {
                // Armed immediately before the write it belongs to, so nothing can consume it in between.
                // Deliberately NOT armed on any other path: a simulation that is not completing a prepared
                // Part O run must drop it, and the unexpected modification is how that happens.
                partORun.ExpectModification();
            }

            // A cancelled run leaves analyticalModel null, and pushing that back would replace the model the
            // user still has open with nothing. Not adopting it really does leave the loaded model untouched,
            // because everything above this point worked on the copy taken before the first mutation.
            if (!cancelled && analyticalModel != null)
            {
                uIAnalyticalModel.SetJSAMObject(analyticalModel, new FullModification());
            }

            if (completePartORun)
            {
                // The model handed over is the one this workflow produced and the window has just adopted -
                // not the preparation output, and not read back from uIAnalyticalModel (whose getter clones).
                // The TSD is required to exist: the file name is derived, and a derived name is a guess until
                // the file behind it is there. A sizing-only run writes none, and is correctly not completable.
                string path_TSD = System.IO.Path.ChangeExtension(path_TBD, "tsd");

                if (!partORun.Complete(analyticalModel, path_TSD, out string refusal_PartORun))
                {
                    MessageBox.Show(string.Format("The Part O run was not completed, so its TM59 assessment is not available.\n\n{0}", refusal_PartORun));
                }
            }
        }

        public static AnalyticalModel Simulate(this AnalyticalModel analyticalModel, string path, IWin32Window owner = null)
        {
            if (analyticalModel == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (!analyticalModel.TryGetValue(Analytical.AnalyticalModelParameter.WeatherData, out WeatherData weatherData))
            {
                weatherData = null;
            }

            string projectName = null;
            string outputDirectory = null;
            bool unmetHours = false;
            bool printRoomDataSheets = false;

            bool fullYearSimulation = false;
            int fullYearSimulation_From = -1;
            int fullYearSimulation_To = -1;
            bool sizing = true;

            SolarCalculationMethod solarCalculationMethod = SolarCalculationMethod.None;
            bool updateConstructionLayersByPanelType = false;

            SimulateWindow simulateWindow_Path = new SimulateWindow();
            simulateWindow_Path.ProjectName = System.IO.Path.GetFileNameWithoutExtension(path);
            simulateWindow_Path.OutputDirectory = System.IO.Path.GetDirectoryName(path);
            simulateWindow_Path.WeatherData = weatherData;
            // Mirror the WinForms SimulateForm default that this overload replaced:
            // sizing is on by default for the path-based simulation flow.
            simulateWindow_Path.Sizing = true;
            simulateWindow_Path.UpdateConstructionLayersByPanelType = true;

            if (owner != null)
            {
                new System.Windows.Interop.WindowInteropHelper(simulateWindow_Path).Owner = owner.Handle;
            }

            if (simulateWindow_Path.ShowDialog() != true)
            {
                return null;
            }

            projectName = simulateWindow_Path.ProjectName;
            outputDirectory = simulateWindow_Path.OutputDirectory;
            unmetHours = simulateWindow_Path.UnmetHours;
            sizing = simulateWindow_Path.Sizing;
            weatherData = simulateWindow_Path.SelectedWeatherData;
            solarCalculationMethod = simulateWindow_Path.SolarCalculationMethod;
            updateConstructionLayersByPanelType = simulateWindow_Path.UpdateConstructionLayersByPanelType;
            printRoomDataSheets = simulateWindow_Path.RoomDataSheets;
            fullYearSimulation = simulateWindow_Path.FullYearSimulation;
            if (fullYearSimulation)
            {
                fullYearSimulation_From = simulateWindow_Path.FullYearSimulation_From;
                fullYearSimulation_To = simulateWindow_Path.FullYearSimulation_To;
            }

            if (string.IsNullOrWhiteSpace(projectName))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(outputDirectory) || !System.IO.Directory.Exists(outputDirectory))
            {
                return null;
            }

            if (weatherData == null)
            {
                return null;
            }

            string path_TBD = System.IO.Path.Combine(outputDirectory, projectName + ".tbd");

            // Same reason as the overload above: "Update Materials" writes into the model, and this method
            // signals cancellation by returning null. Mutating the caller's instance and then handing back
            // null would change it while telling the caller nothing came of the run. The copy keeps the Guid.
            analyticalModel = new AnalyticalModel(analyticalModel);

            bool shadingUpdated = false;

            bool cancelled = false;

            // One token spans the preparation steps below and the workflow that follows them, so a single
            // Cancel click aborts whichever of the two is running.
            using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
            {
                CancellationToken cancellationToken = cancellationTokenSource.Token;

                // Hosted off this thread rather than shown on it: "Converting to TBD" and "Updating Shading"
                // below are single COM calls that run for minutes, and Windows ghosts a window whose thread
                // has stopped pumping and then discards clicks on the ghost - so a Cancel button on this
                // thread would silently lose the click. Not a using either: see Modify.RunWorkflow for why the
                // host must be disposed before the final token check.
                ProgressWindowHost progressWindowHost = new ProgressWindowHost("Preparing Model", 8, true, Analytical.Tas.Query.CancelNote(null));

                // Announce the stage, then observe - the order WorkflowCalculator.Step uses, so a click that
                // lands while the note is being updated is still seen before the stage starts work.
                Action<string> step = description =>
                {
                    progressWindowHost.Note = Analytical.Tas.Query.CancelNote(description);
                    progressWindowHost.Update(description);
                    cancellationToken.ThrowIfCancellationRequested();
                };

                try
                {
                    progressWindowHost.CancelRequested += (s, e) => cancellationTokenSource.Cancel();

                    step("Update Materials");

                    IEnumerable<Core.IMaterial> materials = Analytical.Query.Materials(analyticalModel.AdjacencyCluster, Analytical.Query.DefaultMaterialLibrary());
                    if (materials != null)
                    {
                        foreach (Core.IMaterial material in materials)
                        {
                            if (analyticalModel.HasMaterial(material))
                            {
                                continue;
                            }

                            analyticalModel.AddMaterial(material);
                        }
                    }

                    step("Update ConstructionLayers By PanelTypes");

                    analyticalModel = updateConstructionLayersByPanelType ? analyticalModel.UpdateConstructionLayersByPanelType() : analyticalModel;

                    if (System.IO.File.Exists(path_TBD))
                    {
                        System.IO.File.Delete(path_TBD);
                    }

                    List<int> hoursOfYear = Analytical.Query.DefaultHoursOfYear();

                    step("Solar Calculations");
                    if (solarCalculationMethod != SolarCalculationMethod.None)
                    {
                        SolarCalculator.Modify.Simulate(analyticalModel, hoursOfYear.ConvertAll(x => new DateTime(2018, 1, 1).AddHours(x)), false, Core.Tolerance.MacroDistance, Core.Tolerance.MacroDistance, 0.012, Core.Tolerance.Distance);
                    }

                    using (SAMTBDDocument sAMTBDDocument = new SAMTBDDocument(path_TBD))
                    {
                        TBD.TBDDocument tBDDocument = sAMTBDDocument.TBDDocument;

                        step("Updating WeatherData");
                        Weather.Tas.Modify.UpdateWeatherData(tBDDocument, weatherData, analyticalModel == null ? 0 : analyticalModel.AdjacencyCluster.BuildingHeight());

                        TBD.Calendar calendar = tBDDocument.Building.GetCalendar();

                        List<TBD.dayType> dayTypes = Query.DayTypes(calendar);
                        if (dayTypes.Find(x => x.name == "HDD") == null)
                        {
                            TBD.dayType dayType = calendar.AddDayType();
                            dayType.name = "HDD";
                        }

                        if (dayTypes.Find(x => x.name == "CDD") == null)
                        {
                            TBD.dayType dayType = calendar.AddDayType();
                            dayType.name = "CDD";
                        }

                        step("Converting to TBD");
                        Tas.Convert.ToTBD(analyticalModel, tBDDocument, true);

                        step("Updating Zones");
                        Tas.Modify.UpdateZones(tBDDocument.Building, analyticalModel, true);

                        step("Updating Shading");
                        shadingUpdated = Tas.Modify.UpdateShading(tBDDocument, analyticalModel);

                        sAMTBDDocument.Save();
                    }

                    step("Printing Room Data Sheets");
                    if (printRoomDataSheets && analyticalModel != null)
                    {
                        if (!System.IO.Directory.Exists(outputDirectory))
                        {
                            System.IO.Directory.CreateDirectory(outputDirectory);
                        }

                        UI.Modify.PrintRoomDataSheets(analyticalModel, outputDirectory);
                    }
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }
                finally
                {
                    progressWindowHost.Dispose();
                }

                // The host is down, so no further click can arrive and any in-flight one has already run:
                // this observation is final - but only once the host confirms it actually shut down. If it
                // could not, its thread is still live and may be sitting on a click nothing observed, so
                // preparation cannot be reported as having completed uninterrupted.
                if (!cancelled && (cancellationTokenSource.IsCancellationRequested || !progressWindowHost.ShutdownCompleted))
                {
                    cancelled = true;
                }

                if (cancelled)
                {
                    return null;
                }

                // Still inside the token source's scope: the workflow shares the token with the preparation
                // above, so one Cancel click covers both halves of the run.
                List<DesignDay> heatingDesignDays = new List<DesignDay>() { Analytical.Query.HeatingDesignDay(weatherData) };
                List<DesignDay> coolingDesignDays = new List<DesignDay>() { Analytical.Query.CoolingDesignDay(weatherData) };

                SurfaceOutputSpec surfaceOutputSpec = new SurfaceOutputSpec("Tas.Simulate")
                {
                    SolarGain = true,
                    Conduction = true,
                    ApertureData = false,
                    Condensation = false,
                    Convection = false,
                    LongWave = false,
                    Temperature = false
                };

                List<SurfaceOutputSpec> surfaceOutputSpecs = new List<SurfaceOutputSpec>() { surfaceOutputSpec };

                int simulate_From = -1;
                int simulate_To = -1;

                bool simulate = fullYearSimulation;

                if (simulate)
                {
                    simulate_From = fullYearSimulation_From;
                    simulate_To = fullYearSimulation_To;
                }

                if (shadingUpdated)
                {
                    if (!simulate)
                    {
                        simulate_From = 1;
                        simulate_To = 1;
                        simulate = true;
                    }
                }

                WorkflowSettings workflowSettings = new WorkflowSettings()
                {
                    Path_TBD = path_TBD,
                    Path_gbXML = null,
                    WeatherData = null,
                    DesignDays_Heating = heatingDesignDays,
                    DesignDays_Cooling = coolingDesignDays,
                    SurfaceOutputSpecs = surfaceOutputSpecs,
                    UnmetHours = unmetHours,
                    Simulate = simulate,
                    Sizing = sizing,
                    UpdateZones = false,
                    UseWidths = false,
                    SimulateFrom = simulate_From,
                    SimulateTo = simulate_To
                };

                analyticalModel = Modify.RunWorkflow(analyticalModel, workflowSettings, cancellationToken, out cancelled);

                // A cancelled workflow returns null; returning null rather than a half-populated model is what
                // tells the caller nothing usable came back.
                if (cancelled || analyticalModel == null)
                {
                    return null;
                }

                analyticalModel.SetValue(Analytical.AnalyticalModelParameter.WeatherData, weatherData);

                return analyticalModel;
            }
        }
    }
}
