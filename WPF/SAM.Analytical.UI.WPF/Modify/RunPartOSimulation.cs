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
        /// <para><b>Warm starting from a canonical TBD</b></para>
        /// <para>
        /// Where <paramref name="partOCanonicalTBD"/> is supplied, this run does <b>not</b> convert the
        /// geometry: the canonical TBD is copied to this run's own TBD by
        /// <c>WorkflowSettings.Path_TBD_Canonical</c> and everything after the conversion still runs on the
        /// copy - the zone identity stamps, the zones, the ventilation network, and a real full-year
        /// simulation. Between Iteration 2B rounds only the ventilation state changes, and on the licensed
        /// acceptance model the conversion is 41.6 s of a 64.2 s round against 3.6 s of simulation.
        /// </para>
        /// <para>
        /// <b>No gbXML is written on that path</b>, which is the point: the export, the T3D import and the
        /// shading calculation are the work being skipped. The solar calculation the canonical TBD already
        /// carries is the one this round uses, which is correct precisely because the geometry and the
        /// shading inputs are what did not change.
        /// </para>
        /// <para>
        /// <b>Whether the canonical is still valid is decided before this is called</b> - see
        /// <see cref="PartOCanonicalTBD.IsValidFor"/>. A caller that cannot prove it passes null and gets
        /// the full conversion, which is always available and always authoritative.
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
        ///
        /// <para><b>And the returned model carries the run's provenance</b></para>
        /// <para>
        /// On the full-year path, the model handed back is stamped with the overheating scenarios the run was
        /// prepared with and with the results file it was produced from
        /// (<c>AnalyticalModelParameter.SimulationResultProvenance</c>, which fingerprints both the design
        /// state and those scenarios), and that model is written beside the TBD as this run's own
        /// <c>&lt;project&gt;.sam</c> - the native SAM model form, at the one path
        /// <see cref="Query.Path_PartORunModel(string)"/> states. That is what lets a later session reopen
        /// the saved model and review its TM59 assessment from the existing results, without simulating
        /// again; see <c>PartORun.Restore</c>.
        /// </para>
        /// </summary>
        /// <param name="analyticalModel">The design to simulate. <b>Copied first</b>, so a cancelled run leaves it untouched.</param>
        /// <param name="partOSimulationContext">The case to run it as.</param>
        /// <param name="projectName">The project name for this run - what makes its TBD and TSD its own.</param>
        /// <param name="partORun">The run to arm, or null where the caller has none.</param>
        /// <param name="partOCanonicalTBD">
        /// An already-converted TBD to start this run from instead of converting the geometry again, or null
        /// for the full conversion. <b>Only ever read</b>; this run works on its own copy of it.
        /// </param>
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
        public static AnalyticalModel RunPartOSimulation(AnalyticalModel analyticalModel, PartOSimulationContext partOSimulationContext, string projectName, PartORun partORun, CancellationToken externalCancellationToken, out string path_TBD, out string path_TSD, out bool cancelled, out bool fullYear, out List<string> notes, out string refusal, PartOCanonicalTBD partOCanonicalTBD = null)
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

            //Skipped entirely on the warm-start path: the gbXML exists to be imported into a T3D and
            //converted, and a canonical TBD is the product of having done exactly that. Writing one and then
            //not converting it would cost the export for nothing.
            string path_Xml = null;
            if (solarCalculationMethod == SolarCalculationMethod.TAS && partOCanonicalTBD is null)
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

                    //NOT on the warm-start path: the copy from the canonical overwrites this run's TBD
                    //anyway, and deleting first would only widen the window in which the run has no TBD.
                    if (partOCanonicalTBD is null && System.IO.File.Exists(path_TBD))
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

                    //The SAM solar path builds the TBD here, from scratch, which is the very work a warm
                    //start exists to avoid - so a canonical TBD supersedes it and the workflow does the
                    //rest on the copy.
                    if (solarCalculationMethod == SolarCalculationMethod.SAM && partOCanonicalTBD is null)
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

                    //The seam. WorkflowCalculator copies this to Path_TBD, skips the conversion a canonical
                    //TBD already carries, and runs everything after it - see
                    //WorkflowSettings.Path_TBD_Canonical.
                    Path_TBD_Canonical = partOCanonicalTBD?.Path_TBD,

                    Path_gbXML = path_Xml,
                    WeatherData = solarCalculationMethod == SolarCalculationMethod.TAS ? weatherData : null,
                    DesignDays_Heating = heatingDesignDays,
                    DesignDays_Cooling = coolingDesignDays,
                    SurfaceOutputSpecs = [surfaceOutputSpec],
                    UnmetHours = partOSimulationContext.UnmetHours,
                    Simulate = simulate,
                    Sizing = partOSimulationContext.Sizing,
                    //TRUE on the warm-start path whatever the solar method. The zones carry the internal
                    //conditions, and re-deriving them from the current model is half of what makes a
                    //warm-started round the current design rather than the baseline's.
                    UpdateZones = partOCanonicalTBD is not null || solarCalculationMethod == SolarCalculationMethod.TAS,
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

            //The run's self-description, persisted onto the model the workflow returned, so a SAVED copy of
            //it - the per-run <project>.sam this writes beside the TBD, or the user's own .sam saved later -
            //can be reopened in a later session and its results reviewed WITHOUT rerunning the simulation.
            //The scenarios are the assessment's authority over which TM59 criterion applies to which space;
            //the provenance is the proof of which results file the model belongs to, and it fingerprints the
            //scenarios along with the design so neither can move underneath the results. See
            //PartORun.Restore.
            //
            //A run with no scenarios - a plain, non-Part-O simulation - is left entirely unstamped: there is
            //nothing to review it against, and no run model is written for it.
            List<OverheatingScenario> overheatingScenarios = partORun?.OverheatingScenarios;
            if (overheatingScenarios is not null && overheatingScenarios.Count != 0)
            {
                result.SetValue(Analytical.AnalyticalModelParameter.OverheatingScenarios, new SAMCollection<OverheatingScenario>(overheatingScenarios));

                //Only the full annual series a TM59 assessment can read is recorded - a partial, one-day or
                //sizing-only run writes no provenance, exactly as it cannot complete a Part O run.
                //
                //AND only where the results are provably THIS run's, asked of PartORun.IsResultsOfThisRun -
                //the same lineage rule PartORun.Complete refuses on, a moment later, in the caller. Asked
                //HERE because everything below this line is reopenable: a workflow that returned a model
                //while leaving an existing TSD untouched would otherwise be stamped and persisted into a
                //fully self-consistent .sam - model, scenarios and file fingerprints all agreeing - which a
                //later session would restore and offer for review against an EARLIER run's results. Complete
                //would then refuse the run, correctly, and the misleading artifact would already be written.
                //Nothing is stamped and nothing is written for a run that cannot be completed.
                string refusal_Lineage = "there is no prepared Part O run to have produced them.";

                bool ofThisRun = partORun is not null && partORun.IsResultsOfThisRun(path_TSD, out refusal_Lineage);

                if (fullYear && !ofThisRun)
                {
                    //Noted rather than silent: "no reviewable model was written, and why" is the diagnostic.
                    //The run itself is refused by Complete, which is where that verdict belongs.
                    notes.Add(string.Format("No persisted run model was written for these results, because they are not provably this run's: {0}", refusal_Lineage));
                }

                if (fullYear && ofThisRun)
                {
                    //Constructed AFTER the scenarios are stamped above, deliberately: the record fingerprints
                    //both the design state and the scenarios it finds on the model, and a record taken before
                    //them would bind an empty assessment context.
                    result.SetValue(Analytical.AnalyticalModelParameter.SimulationResultProvenance, new SimulationResultProvenance(result, path_TSD));

                    //This run's own persisted model, beside its results and named from them by the single
                    //naming authority - Query.Path_PartORunModel, which is where the extension is stated.
                    //Written through Core.Convert.ToFile under SAMFileType.SAM: SAM's native model writer,
                    //the one Save As uses, so this file reopens through the ordinary Open path with no
                    //special case anywhere.
                    //
                    //And then, ONLY once that has succeeded, the workflow's own "Saving Model" export for
                    //this run - the plain-text <run>.json beside the TBD - is removed, so a Part O run
                    //leaves one reviewable model artifact rather than the same model twice. The ordering is
                    //the safety property and lives in Modify.PersistPartORunModel: a failed .sam write
                    //deletes nothing and leaves the JSON as the fallback copy, and a JSON that could not be
                    //removed is a note, never a failed run. WorkflowCalculator itself is untouched - every
                    //ordinary non-Part-O TAS run in SAM still writes and keeps its <project>.json.
                    Modify.PersistPartORunModel(result, path_TSD, path_TBD, out string note_Persistence);

                    if (!string.IsNullOrWhiteSpace(note_Persistence))
                    {
                        notes.Add(note_Persistence);
                    }
                }
            }

            return result;
        }
    }
}
