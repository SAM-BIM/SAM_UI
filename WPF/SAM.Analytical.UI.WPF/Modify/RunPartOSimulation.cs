// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas;
using SAM.Core;
using SAM.Core.Tas;
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
        /// <summary>
        /// Runs <b>one</b> stated TAS case over one model, start to finish, and hands back the model the
        /// workflow returned and the results it wrote.
        ///
        /// <para><b>Why this is a method and not a second copy of <see cref="Simulate(UIAnalyticalModel, PartORun)"/></b></para>
        /// <para>
        /// An Iteration 2B optimisation runs the same thermal case ten times over ten designs. It cannot ask
        /// a person for the settings ten times, and it must not run a case that differs in any respect from
        /// the one the baseline was produced by - a change in weather, day range or solar method would leave
        /// the movement in TM59 results unattributable to the airflow change that was made. Writing the
        /// pipeline out again in the optimiser would be exactly the way those two drift apart, so
        /// <c>Simulate</c> collects the settings into a <see cref="PartOSimulationContext"/> and then calls
        /// this, and the optimiser calls this with the context the completed run recorded.
        /// </para>
        ///
        /// <para><b>What it does, and what it deliberately does not</b></para>
        /// <para>
        /// Materials, construction layers, the TBD, the solar calculation, the zones, the shading and the
        /// workflow - the whole path to a TSD. It does <b>not</b> print room data sheets or write the SAP,
        /// Part L, domestic-overheating or TPD exports: those are deliverables of a run somebody asked for,
        /// not part of the thermal case, and producing a dozen copies of each during an optimisation would
        /// be noise. <see cref="Simulate(UIAnalyticalModel, PartORun)"/> still does all of them, after this
        /// returns.
        /// </para>
        ///
        /// <para><b>Each call is its own project name, and therefore its own TSD</b></para>
        /// <para>
        /// <paramref name="projectName"/> decides the TBD and the TSD beside it. An optimisation passes
        /// <c>&lt;project&gt;-Opt01</c>, <c>-Opt02</c> and so on, so no round can overwrite the results that
        /// are the evidence for another round.
        /// </para>
        ///
        /// <para><b>The Part O arming happens here, before the workflow, or not at all</b></para>
        /// <para>
        /// <paramref name="partORun"/> is armed with <c>ExpectResults</c> only where the case about to run
        /// is the full annual series a TM59 assessment can read - read off the settings that will actually
        /// be handed to the workflow, never off an intention. A partial, one-day or sizing-only case
        /// therefore leaves the run unarmed and unable to be completed, which is the guarantee PR #76
        /// established and this does not weaken.
        /// </para>
        /// </summary>
        /// <param name="analyticalModel">The design to simulate. <b>Copied first</b>, so a cancelled run leaves it untouched.</param>
        /// <param name="partOSimulationContext">The case to run it as.</param>
        /// <param name="projectName">The project name for this run - what makes its TBD and TSD its own.</param>
        /// <param name="partORun">The run to arm, or null where the caller has none.</param>
        /// <param name="externalCancellationToken">Lets one Cancel click abort a whole optimisation, not just this run.</param>
        /// <param name="path_TBD">The TBD this run wrote or would have written.</param>
        /// <param name="path_TSD">The results file beside it. Existence is not guaranteed - a run that did not simulate writes none.</param>
        /// <param name="cancelled">Whether the run was cancelled. A cancelled run returns null.</param>
        /// <param name="fullYear">Whether the case that actually ran was the full annual series.</param>
        /// <param name="notes">What was worth saying about the run - unzoned spaces and the like.</param>
        /// <param name="refusal">
        /// Why the run could not <b>start</b> - the gbXML the TAS solar calculation reads could not be
        /// written, or an existing TBD could not be overwritten. Null where it started, whatever it then did.
        /// <para>
        /// Told apart from a workflow that ran and produced nothing, because the two need different answers:
        /// a run that never started leaves no TBD for anything downstream to convert or export from, and
        /// carrying on past it is how a later step ends up reading a file that is not there.
        /// </para>
        /// </param>
        /// <returns>The model the workflow returned, or null where it did not run, failed or was cancelled.</returns>
        public static AnalyticalModel RunPartOSimulation(AnalyticalModel analyticalModel, PartOSimulationContext partOSimulationContext, string projectName, PartORun partORun, CancellationToken externalCancellationToken, out string path_TBD, out string path_TSD, out bool cancelled, out bool fullYear, out List<string> notes, out string refusal)
        {
            path_TBD = null;
            path_TSD = null;
            cancelled = false;
            fullYear = false;
            notes = [];
            refusal = null;

            if (analyticalModel is null || partOSimulationContext is null || string.IsNullOrWhiteSpace(projectName))
            {
                refusal = "No model, no TAS case or no project name was supplied, so nothing could be simulated.";

                return null;
            }

            string outputDirectory = partOSimulationContext.OutputDirectory;
            WeatherData weatherData = partOSimulationContext.WeatherData;
            SolarCalculationMethod solarCalculationMethod = partOSimulationContext.SolarCalculationMethod;

            //A copy, for the same reason Simulate takes one: everything below mutates in place - the name
            //here, the materials at "Update Materials" - and a cancelled run must leave the caller's model
            //exactly as it was rather than renamed and re-materialled behind its back.
            analyticalModel = new AnalyticalModel(analyticalModel)
            {
                Name = projectName,
            };

            string path_Xml = null;
            if (solarCalculationMethod == SolarCalculationMethod.TAS)
            {
                path_Xml = System.IO.Path.Combine(outputDirectory, projectName + ".xml");
                if (!gbXML.Convert.ToFile(analyticalModel, path_Xml))
                {
                    refusal = string.Format("The gbXML file '{0}' could not be created, so the TAS solar calculation has nothing to read.", path_Xml);

                    return null;
                }
            }

            path_TBD = System.IO.Path.Combine(outputDirectory, projectName + ".tbd");
            path_TSD = System.IO.Path.ChangeExtension(path_TBD, "tsd");

            bool shadingUpdated = false;

            AnalyticalModel result = null;

            // One token spans the COM preparation steps below and the workflow that follows them, so a
            // single Cancel click aborts whichever of the two is running - and, through
            // externalCancellationToken, a whole optimisation rather than one of its rounds.
            using (CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken))
            {
                CancellationToken cancellationToken = cancellationTokenSource.Token;

                // Hosted off this thread: the steps below are single COM calls that run for minutes, and
                // Windows ghosts a window whose thread has stopped pumping and then discards clicks on the
                // ghost. Not a using - see below for why the host must be disposed before the final check.
                ProgressWindowHost progressWindowHost = new(string.Format("Preparing Model ({0})", projectName), 8, true, Analytical.Tas.Query.CancelNote(null));

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
                    if (materials is not null)
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

                    analyticalModel = partOSimulationContext.UpdateConstructionLayersByPanelType ? analyticalModel.UpdateConstructionLayersByPanelType() : analyticalModel;

                    if (System.IO.File.Exists(path_TBD))
                    {
                        try
                        {
                            System.IO.File.Delete(path_TBD);
                        }
                        catch
                        {
                            // Take the dialog down before saying anything: it is topmost and lives on another
                            // thread, so a message shown under it can end up hidden behind it.
                            progressWindowHost.Dispose();

                            refusal = string.Format("The existing TBD file '{0}' could not be overwritten.", path_TBD);

                            return null;
                        }
                    }

                    if (solarCalculationMethod == SolarCalculationMethod.SAM)
                    {
                        List<int> hoursOfYear = Analytical.Query.DefaultHoursOfYear();

                        SolarCalculator.Modify.Simulate(analyticalModel, hoursOfYear.ConvertAll(x => new DateTime(2018, 1, 1).AddHours(x)), false, Tolerance.MacroDistance, Tolerance.MacroDistance, 0.012, Tolerance.Distance);

                        using (SAMTBDDocument sAMTBDDocument = new(path_TBD))
                        {
                            TBD.TBDDocument tBDDocument = sAMTBDDocument.TBDDocument;

                            step("Updating WeatherData");
                            Weather.Tas.Modify.UpdateWeatherData(tBDDocument, weatherData, analyticalModel is null ? 0 : analyticalModel.AdjacencyCluster.BuildingHeight());

                            TBD.Calendar calendar = tBDDocument.Building.GetCalendar();

                            List<TBD.dayType> dayTypes = Query.DayTypes(calendar);
                            if (dayTypes.Find(x => x.name == "HDD") is null)
                            {
                                TBD.dayType dayType = calendar.AddDayType();
                                dayType.name = "HDD";
                            }

                            if (dayTypes.Find(x => x.name == "CDD") is null)
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
                // this observation is final - but only once the host confirms it actually shut down. If it
                // could not, its thread is still live and may be sitting on a click nothing observed.
                if (!cancelled && (cancellationTokenSource.IsCancellationRequested || !progressWindowHost.ShutdownCompleted))
                {
                    cancelled = true;
                }

                if (cancelled)
                {
                    return null;
                }

                List<DesignDay> heatingDesignDays = [Analytical.Query.HeatingDesignDay(weatherData)];
                List<DesignDay> coolingDesignDays = [Analytical.Query.CoolingDesignDay(weatherData)];

                SurfaceOutputSpec surfaceOutputSpec = new("Tas.Simulate")
                {
                    SolarGain = true,
                    Conduction = true,
                    ApertureData = true,
                    Condensation = false,
                    Convection = false,
                    LongWave = false,
                    Temperature = true
                };

                int simulate_From = partOSimulationContext.SimulateFrom;
                int simulate_To = partOSimulationContext.SimulateTo;

                bool simulate = simulate_From > 0 && simulate_To > 0;

                if (!simulate && shadingUpdated)
                {
                    //Unchanged from Simulate: a shading update forces a one-day run even where none was
                    //asked for, and that run is then correctly NOT a full year.
                    simulate_From = 1;
                    simulate_To = 1;
                    simulate = true;
                }

                WorkflowSettings workflowSettings = new()
                {
                    Path_TBD = path_TBD,
                    Path_gbXML = path_Xml,
                    WeatherData = solarCalculationMethod == SolarCalculationMethod.TAS ? weatherData : null,
                    DesignDays_Heating = heatingDesignDays,
                    DesignDays_Cooling = coolingDesignDays,
                    SurfaceOutputSpecs = [surfaceOutputSpec],
                    UnmetHours = partOSimulationContext.UnmetHours,
                    Simulate = simulate,
                    Sizing = partOSimulationContext.Sizing,
                    UpdateZones = solarCalculationMethod == SolarCalculationMethod.TAS,
                    UseWidths = partOSimulationContext.UseWidths,
                    SimulateFrom = simulate_From,
                    SimulateTo = simulate_To
                };

                // Read off the settings that are about to run, never off an intention - see
                // Query.IsPartOFullYearSimulation for why nothing less may complete a Part O run.
                fullYear = workflowSettings.IsPartOFullYearSimulation();

                // Announced BEFORE the workflow, and only for the full-year case. Two guarantees in one
                // arming: a partial/one-day/sizing-only workflow leaves the run unarmed and so cannot
                // complete it, and the results file is fingerprinted now so an older TSD at this path
                // cannot be accepted as this run's.
                if (fullYear && partORun is not null && partORun.State == PartORunState.Prepared)
                {
                    partORun.ExpectResults(path_TSD);
                }

                result = Modify.RunWorkflow(analyticalModel, workflowSettings, cancellationToken, out cancelled);
            }

            if (cancelled || result is null)
            {
                return null;
            }

            result.SetValue(Analytical.AnalyticalModelParameter.WeatherData, weatherData);

            return result;
        }
    }
}
