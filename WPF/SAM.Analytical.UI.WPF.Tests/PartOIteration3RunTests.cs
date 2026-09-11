// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using SAM.Analytical.Systems;
using SAM.Analytical.Tas.TPD;
using SAM.Analytical.UI;
using SAM.Core.Tas;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// <b>The whole Iteration 3 pipeline, end to end, with no TAS.</b>
    ///
    /// <para><b>What is actually being tested</b></para>
    /// <para>
    /// Not the four authorities PR4 calls - those are tested in their own repositories - but the
    /// sequencing, the refusal boundaries and the identity checks between them. The properties that
    /// matter are: a refusal at any stage stops the run <b>before the next delegate is called</b>, every
    /// later stage stays NOT RUN, no comparison is produced, and the record is still written so the
    /// refusal can be reopened.
    /// </para>
    /// <para>
    /// Each case drives the real <c>Modify.RunPartOIteration3</c> over a real
    /// <c>PartORun</c> and real SAM_Systems and SAM_Tas result objects.
    /// </para>
    /// </summary>
    public class PartOIteration3RunTests : IDisposable
    {
        private readonly string directory = PartOIteration3Fixture.Directory_Temp();

        public void Dispose()
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch (IOException)
            {
                //A temporary directory that will not delete is not a test failure.
            }
        }

        //-------------------------------------------------------------------------------------------------
        //The run under test
        //-------------------------------------------------------------------------------------------------

        private AdjacencyCluster adjacencyCluster;

        private List<Guid> guids_VentilationSystem;

        private List<Zone> zones;

        private List<Guid> guids_Space_Dwelling;

        private string path_TSD_ReferenceA;

        /// <summary>
        /// A completed, eligible Iteration 1a run over the fixture design - built through the production
        /// transitions, in the order production performs them.
        /// </summary>
        private PartORun Run()
        {
            adjacencyCluster = PartOIteration3Fixture.Design(out guids_VentilationSystem, out zones);

            guids_Space_Dwelling = [];
            foreach (Zone zone in zones)
            {
                foreach (Space space in PartOIteration3Fixture.Spaces(adjacencyCluster, zone))
                {
                    guids_Space_Dwelling.Add(space.Guid);
                }
            }

            AnalyticalModel analyticalModel_Prepared = PartOIteration3Fixture.Model(adjacencyCluster);

            PartORun result = new();

            Assert.True(result.Prepare(
                analyticalModel_Prepared,
                PartOIteration3Fixture.Scenarios(),
                new PartOPreparationContext(PartOIteration.BasePassive, zones, null, null),
                guids_VentilationSystem));

            path_TSD_ReferenceA = Path.Combine(directory, "Flat.tsd");

            Assert.True(result.ExpectResults(path_TSD_ReferenceA));

            File.WriteAllText(path_TSD_ReferenceA, "reference A results");

            //The design side of the assessment is the model the workflow RETURNED, and it carries the
            //run's own provenance - which the pairing copies its fingerprints from.
            AnalyticalModel analyticalModel_Workflow = PartOIteration3Fixture.Model(new AdjacencyCluster(adjacencyCluster), "Flat");

            analyticalModel_Workflow.SetValue(Analytical.AnalyticalModelParameter.OverheatingScenarios, new Core.SAMCollection<OverheatingScenario>(result.OverheatingScenarios));
            analyticalModel_Workflow.SetValue(Analytical.AnalyticalModelParameter.SimulationResultProvenance, new SimulationResultProvenance(analyticalModel_Workflow, path_TSD_ReferenceA));

            Assert.True(result.Complete(analyticalModel_Workflow, path_TSD_ReferenceA, PartOIteration3Fixture.SimulationContext(directory), out string refusal));
            Assert.Null(refusal);

            return result;
        }

        private static MechanicalVentilationMaterialisation Materialisation()
        {
            return new MechanicalVentilationMaterialisation(new Core.Systems.SystemEnergyCentre("Part O"), null, ["materialised"], null);
        }

        private static MechanicalVentilationMaterialisation Materialisation_Refused(string refusal)
        {
            return new MechanicalVentilationMaterialisation(null, [refusal], null, null);
        }

        /// <summary>A complete route over the dwelling rooms that carry a design duty.</summary>
        private SystemVentilationRoute Route(NoIzamThermalSource noIzamThermalSource, out List<SystemVentilationBinding> systemVentilationBindings)
        {
            systemVentilationBindings = [];

            List<SystemVentilationConnectionBinding> connectionBindings = [];

            foreach (Zone zone in zones)
            {
                Guid guid_AirSystem = Guid.NewGuid();

                List<Space> spaces = PartOIteration3Fixture.Spaces(adjacencyCluster, zone);

                Dictionary<Guid, Guid> dictionary_SystemSpace = [];

                foreach (Space space in spaces)
                {
                    List<VentilationTerminal> ventilationTerminals = adjacencyCluster.VentilationTerminals(space) ?? [];

                    double? supply = Analytical.Query.VentilationTerminalDesignDuty_Lps(ventilationTerminals, FlowClassification.Supply);
                    double? extract = Analytical.Query.VentilationTerminalDesignDuty_Lps(ventilationTerminals, FlowClassification.Extract);

                    systemVentilationBindings.Add(PartOIteration3Fixture.Binding(space.Guid, guid_AirSystem, supply, extract, out Guid guid_SystemSpace));

                    dictionary_SystemSpace[space.Guid] = guid_SystemSpace;
                }

                foreach (KeyValuePair<(Guid, Guid), Analytical.Query.DesignTransferAirMovement> keyValuePair in adjacencyCluster.DesignTransferSpaceAirMovements())
                {
                    Guid guid_From = keyValuePair.Value.FromGuid;
                    Guid guid_To = keyValuePair.Value.ToGuid;

                    if (!dictionary_SystemSpace.TryGetValue(guid_From, out Guid guid_SystemSpace_From) || !dictionary_SystemSpace.TryGetValue(guid_To, out Guid guid_SystemSpace_To))
                    {
                        continue;
                    }

                    connectionBindings.Add(new SystemVentilationConnectionBinding(
                        SystemVentilationConnectionType.Transfer,
                        keyValuePair.Value.SpaceAirMovement.Guid,
                        Guid.NewGuid(),
                        guid_AirSystem,
                        guid_SystemSpace_From,
                        guid_SystemSpace_To,
                        adjacencyCluster.DesignTransferFlowRate_Lps(guid_From, guid_To, out Guid _, out Guid _).Value,
                        "damper"));
                }
            }

            return new SystemVentilationRoute(
                noIzamThermalSource,
                Path.Combine(directory, "Flat-It3B.tpd"),
                PartOIteration3Fixture.Evidence(Path.Combine(directory, "Flat-It3B.tpd"), Path.Combine(directory, "Flat-It3B.tpd")),
                systemVentilationBindings,
                connectionBindings,
                PartOIteration3Fixture.ZoneTemperatures(systemVentilationBindings, 0, 23),
                null,
                null);
        }

        private static PartOIteration3Assessment Assessment(IEnumerable<Guid> guids, Func<Guid, double[]> func_Series, TM59ComplianceStatus status = TM59ComplianceStatus.Pass)
        {
            List<PartOTM59SpaceResult> spaceResults = [];
            Dictionary<Guid, double[]> resultantTemperatures = [];

            foreach (Guid guid in guids)
            {
                spaceResults.Add(new PartOTM59SpaceResult(guid, "room", "TM59 Criterion A", 10, 32, status, true));

                resultantTemperatures[guid] = func_Series(guid);
            }

            return new PartOIteration3Assessment(true, null, status, spaceResults, null, null, null, resultantTemperatures, "report", null, spaceResults.Count);
        }

        /// <summary>The pipeline for a run that completes, wired to the fixture design.</summary>
        private PartOIteration3PipelineFake Pipeline_Complete(out List<Guid> guids_Bound)
        {
            NoIzamThermalSource noIzamThermalSource = PartOIteration3Fixture.ThermalSource(directory, guids_Space_Dwelling);

            SystemVentilationRoute systemVentilationRoute = Route(noIzamThermalSource, out List<SystemVentilationBinding> systemVentilationBindings);

            Assert.True(systemVentilationRoute.IsComplete);

            guids_Bound = [];
            foreach (SystemVentilationBinding systemVentilationBinding in systemVentilationBindings)
            {
                guids_Bound.Add(systemVentilationBinding.Guid_Space);
            }

            guids_Bound.Sort();

            string path_TSD_Bridge = Path.Combine(directory, "Flat-It3B-Bridge.tsd");

            //Candidate B one degree above Reference A everywhere, so the expected statistics are exact.
            ResultantTemperatureResults resultantTemperatureResults = PartOIteration3Fixture.ResultantTemperatures(path_TSD_Bridge, guids_Bound, 0, 23, (guid, hour) => 21.0);

            List<Guid> guids = guids_Bound;

            PartOIteration3PipelineFake result = new()
            {
                Materialisation = Materialisation(),
                NoIzamThermalSource = noIzamThermalSource,
                AnalyticalModel_CandidateB = PartOIteration3Fixture.Model(new AdjacencyCluster(adjacencyCluster), "Flat-It3B"),
                SystemVentilationRoute = systemVentilationRoute,
                ResultantTemperatureResults = resultantTemperatureResults,
                Assessment_ReferenceA = Assessment(guids_Space_Dwelling, _ => Series(20.0)),
                Assessment_CandidateB = Assessment(guids, _ => Series(21.0)),
            };

            //Each stage writes what the real one writes, so the artifact-ownership rule sees this
            //attempt's files rather than the fixture's - which is what a stale attempt looks like, and is
            //tested on its own below.
            result.Paths_ThermalSource.Add(Path.Combine(directory, "Flat-It3B.tbd"));
            result.Paths_ThermalSource.Add(Path.Combine(directory, "Flat-It3B.tsd"));
            result.Paths_Route.Add(Path.Combine(directory, "Flat-It3B.tpd"));
            result.Paths_Bridge.Add(Path.Combine(directory, "Flat-It3B-Bridge.tbd"));
            result.Paths_Bridge.Add(Path.Combine(directory, "Flat-It3B-Bridge.tsd"));
            result.Paths_Persist.Add(Path.Combine(directory, "Flat-It3B-Bridge.sam"));

            return result;
        }

        private static double[] Series(double value)
        {
            double[] result = new double[24];

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = value;
            }

            return result;
        }

        //-------------------------------------------------------------------------------------------------
        //The complete run
        //-------------------------------------------------------------------------------------------------

        [Fact]
        public void A_complete_pairing_records_every_stage_and_produces_the_comparison()
        {
            PartORun partORun = Run();

            PartOIteration3PipelineFake partOIteration3PipelineFake = Pipeline_Complete(out List<Guid> guids_Bound);

            PartOIteration3Result partOIteration3Result = Modify.RunPartOIteration3(partORun, partOIteration3PipelineFake);

            Assert.False(partOIteration3Result.IsRefused);
            Assert.True(partOIteration3Result.IsComplete);
            Assert.NotNull(partOIteration3Result.Comparison);

            Assert.All(partOIteration3Result.Ledger.Stages, x => Assert.Equal(PartOIteration3StageStatus.Completed, x.Status));

            //Six rooms, twenty-four hours each, Candidate B exactly one degree warmer.
            Assert.Equal(guids_Bound.Count, partOIteration3Result.Comparison.Statistics.Count_Rooms);
            Assert.Equal(guids_Bound.Count * 24L, partOIteration3Result.Comparison.Statistics.Count_Values);
            Assert.Equal(1.0, partOIteration3Result.Comparison.Statistics.MeanBias, 12);
            Assert.Equal(1.0, partOIteration3Result.Comparison.Statistics.RootMeanSquareError, 12);

            //One per dwelling.
            Assert.Equal(2, partOIteration3Result.Comparison.Dwellings.Count);

            //And the pairing is reopenable.
            Assert.True(File.Exists(partOIteration3Result.Path_Record));
        }

        /// <summary>
        /// Candidate B must be the SAME thermal case as Reference A, writing somewhere else - which is
        /// the whole basis on which the two are comparable.
        /// </summary>
        [Fact]
        public void Candidate_B_runs_the_same_case_as_reference_A_under_its_own_project_name()
        {
            PartORun partORun = Run();

            PartOIteration3PipelineFake partOIteration3PipelineFake = Pipeline_Complete(out List<Guid> _);

            Modify.RunPartOIteration3(partORun, partOIteration3PipelineFake);

            PartOSimulationContext partOSimulationContext = partOIteration3PipelineFake.SimulationContext_CandidateB;

            Assert.NotNull(partOSimulationContext);
            Assert.Equal("Flat-It3B", partOSimulationContext.ProjectName);
            Assert.Equal(directory, partOSimulationContext.OutputDirectory);
            Assert.Same(partORun.SimulationContext.WeatherData, partOSimulationContext.WeatherData);
            Assert.Equal(partORun.SimulationContext.SolarCalculationMethod, partOSimulationContext.SolarCalculationMethod);
            Assert.Equal(partORun.SimulationContext.SimulateFrom, partOSimulationContext.SimulateFrom);
            Assert.Equal(partORun.SimulationContext.SimulateTo, partOSimulationContext.SimulateTo);
            Assert.True(partOSimulationContext.IsFullYear);
        }

        /// <summary>
        /// Reference A is captured over the dwelling scope and Candidate B over the rooms the route
        /// bound - and nothing wider. On a real project a full annual series per room is what makes this
        /// expensive.
        /// </summary>
        [Fact]
        public void Each_assessment_captures_only_the_rooms_it_needs()
        {
            PartORun partORun = Run();

            PartOIteration3PipelineFake partOIteration3PipelineFake = Pipeline_Complete(out List<Guid> guids_Bound);

            Modify.RunPartOIteration3(partORun, partOIteration3PipelineFake);

            Assert.Equal(2, partOIteration3PipelineFake.Captured.Count);

            List<Guid> guids_Dwelling = [.. guids_Space_Dwelling];
            guids_Dwelling.Sort();

            Assert.Equal(guids_Dwelling, partOIteration3PipelineFake.Captured[0]);
            Assert.Equal(guids_Bound, partOIteration3PipelineFake.Captured[1]);
        }

        /// <summary>The materialisation is given the SCOPED working copy, not the design.</summary>
        [Fact]
        public void The_materialisation_is_given_the_scoped_working_copy_and_the_dwelling_rooms()
        {
            PartORun partORun = Run();

            PartOIteration3PipelineFake partOIteration3PipelineFake = Pipeline_Complete(out List<Guid> _);

            Modify.RunPartOIteration3(partORun, partOIteration3PipelineFake);

            Assert.NotNull(partOIteration3PipelineFake.AdjacencyCluster_Materialised);
            Assert.NotSame(adjacencyCluster, partOIteration3PipelineFake.AdjacencyCluster_Materialised);
            Assert.Equal(guids_Space_Dwelling.Count, partOIteration3PipelineFake.Spaces_Materialised.Count);
        }

        //-------------------------------------------------------------------------------------------------
        //The refusal boundaries
        //-------------------------------------------------------------------------------------------------

        [Fact]
        public void A_refused_materialisation_never_reaches_the_thermal_source()
        {
            PartORun partORun = Run();

            PartOIteration3PipelineFake partOIteration3PipelineFake = Pipeline_Complete(out List<Guid> _);

            partOIteration3PipelineFake.Materialisation = Materialisation_Refused("Space 'Bedroom 2' is served by air handling units 'MVHR-02' and 'AHU1'.");

            PartOIteration3Result partOIteration3Result = Modify.RunPartOIteration3(partORun, partOIteration3PipelineFake);

            Assert.True(partOIteration3Result.IsRefused);
            Assert.Equal(PartOIteration3Stage.Materialisation, partOIteration3Result.Ledger.Stage_Refused);

            //Verbatim.
            Assert.Equal(["Space 'Bedroom 2' is served by air handling units 'MVHR-02' and 'AHU1'."], partOIteration3Result.Reasons);

            partOIteration3PipelineFake.AssertNeverCalled(nameof(IPartOIteration3Pipeline.ThermalSource), nameof(IPartOIteration3Pipeline.Route), nameof(IPartOIteration3Pipeline.ResultantTemperatures), nameof(IPartOIteration3Pipeline.Persist));

            Assert.Null(partOIteration3Result.Comparison);
        }

        [Fact]
        public void A_refused_thermal_source_never_reaches_the_conversion()
        {
            PartORun partORun = Run();

            PartOIteration3PipelineFake partOIteration3PipelineFake = Pipeline_Complete(out List<Guid> _);

            partOIteration3PipelineFake.NoIzamThermalSource = new NoIzamThermalSource(null, null, false, false, null, null, ["The thermal source workflow threw."], null);
            partOIteration3PipelineFake.AnalyticalModel_CandidateB = null;

            PartOIteration3Result partOIteration3Result = Modify.RunPartOIteration3(partORun, partOIteration3PipelineFake);

            Assert.Equal(PartOIteration3Stage.ThermalSource, partOIteration3Result.Ledger.Stage_Refused);
            Assert.Contains("The thermal source workflow threw.", partOIteration3Result.Reasons);

            partOIteration3PipelineFake.AssertNothingAfter(nameof(IPartOIteration3Pipeline.ThermalSource));
        }

        [Fact]
        public void A_cancelled_thermal_source_refuses_and_says_so()
        {
            PartORun partORun = Run();

            PartOIteration3PipelineFake partOIteration3PipelineFake = Pipeline_Complete(out List<Guid> _);

            partOIteration3PipelineFake.Cancelled = true;

            PartOIteration3Result partOIteration3Result = Modify.RunPartOIteration3(partORun, partOIteration3PipelineFake);

            Assert.Equal(PartOIteration3Stage.ThermalSource, partOIteration3Result.Ledger.Stage_Refused);
            Assert.Contains(partOIteration3Result.Reasons, x => x.Contains("cancelled"));
        }

        /// <summary>
        /// A route that produced no simulation evidence never got past the conversion, and the ledger
        /// says so rather than blaming the simulation.
        /// </summary>
        [Fact]
        public void A_route_that_never_simulated_refuses_at_the_conversion()
        {
            PartORun partORun = Run();

            PartOIteration3PipelineFake partOIteration3PipelineFake = Pipeline_Complete(out List<Guid> _);

            partOIteration3PipelineFake.SystemVentilationRoute = new SystemVentilationRoute(
                partOIteration3PipelineFake.NoIzamThermalSource,
                null,
                null,
                null,
                null,
                null,
                ["The conversion to TAS Systems did not reconcile against the source graph."],
                null);

            PartOIteration3Result partOIteration3Result = Modify.RunPartOIteration3(partORun, partOIteration3PipelineFake);

            Assert.Equal(PartOIteration3Stage.SystemsConversion, partOIteration3Result.Ledger.Stage_Refused);
            Assert.Contains("The conversion to TAS Systems did not reconcile against the source graph.", partOIteration3Result.Reasons);

            partOIteration3PipelineFake.AssertNothingAfter(nameof(IPartOIteration3Pipeline.Route));
            Assert.Equal(PartOIteration3StageStatus.NotRun, partOIteration3Result.Ledger.State(PartOIteration3Stage.SystemsSimulation).Status);
            Assert.Equal(PartOIteration3StageStatus.NotRun, partOIteration3Result.Ledger.State(PartOIteration3Stage.ZoneTemperature).Status);
        }

        /// <summary>
        /// A route whose simulation is evidenced but whose results did not reconcile refuses at the zone
        /// temperature, with the conversion and the simulation both recorded as done.
        /// </summary>
        [Fact]
        public void A_route_whose_zone_temperature_is_incomplete_refuses_there_and_not_earlier()
        {
            PartORun partORun = Run();

            PartOIteration3PipelineFake partOIteration3PipelineFake = Pipeline_Complete(out List<Guid> _);

            string path_TPD = Path.Combine(directory, "Flat-It3B.tpd");

            partOIteration3PipelineFake.SystemVentilationRoute = new SystemVentilationRoute(
                partOIteration3PipelineFake.NoIzamThermalSource,
                path_TPD,
                PartOIteration3Fixture.Evidence(path_TPD, path_TPD),
                null,
                null,
                null,
                ["Room 0c8f: 12 of 24 zone temperature value(s)."],
                null);

            PartOIteration3Result partOIteration3Result = Modify.RunPartOIteration3(partORun, partOIteration3PipelineFake);

            Assert.Equal(PartOIteration3Stage.ZoneTemperature, partOIteration3Result.Ledger.Stage_Refused);
            Assert.Equal(PartOIteration3StageStatus.Completed, partOIteration3Result.Ledger.State(PartOIteration3Stage.SystemsConversion).Status);
            Assert.Equal(PartOIteration3StageStatus.Completed, partOIteration3Result.Ledger.State(PartOIteration3Stage.SystemsSimulation).Status);

            partOIteration3PipelineFake.AssertNothingAfter(nameof(IPartOIteration3Pipeline.Route));
        }

        [Fact]
        public void A_refused_resultant_temperature_never_reaches_candidate_Bs_assessment()
        {
            PartORun partORun = Run();

            PartOIteration3PipelineFake partOIteration3PipelineFake = Pipeline_Complete(out List<Guid> _);

            partOIteration3PipelineFake.ResultantTemperatureResults = new ResultantTemperatureResults(
                "the Approved Document O thermostat bridge",
                0,
                23,
                [Guid.NewGuid()],
                null,
                null,
                null,
                ["The bridge could not write its copy of the no-IZAM building."],
                null);

            PartOIteration3Result partOIteration3Result = Modify.RunPartOIteration3(partORun, partOIteration3PipelineFake);

            Assert.Equal(PartOIteration3Stage.ResultantTemperature, partOIteration3Result.Ledger.Stage_Refused);
            Assert.Contains("The bridge could not write its copy of the no-IZAM building.", partOIteration3Result.Reasons);

            //Reference A WAS assessed - that happened before this stage. Candidate B was not.
            Assert.Single(partOIteration3PipelineFake.Captured);
            partOIteration3PipelineFake.AssertNothingAfter(nameof(IPartOIteration3Pipeline.ResultantTemperatures));
        }

        /// <summary>
        /// The two independent readers of Candidate B's own result file must agree. If they ever do not,
        /// one of them is resolving a room to the wrong zone - and both answers are complete, finite and
        /// plausible, so nothing else could see it.
        /// </summary>
        [Fact]
        public void A_provider_and_a_TM59_reading_that_disagree_refuse_before_reconciliation()
        {
            PartORun partORun = Run();

            PartOIteration3PipelineFake partOIteration3PipelineFake = Pipeline_Complete(out List<Guid> guids_Bound);

            //One hour of one room read back differently - the smallest possible disagreement.
            Guid guid = guids_Bound[0];

            Dictionary<Guid, double[]> resultantTemperatures = [];
            foreach (Guid guid_Space in guids_Bound)
            {
                double[] values = Series(21.0);

                if (guid_Space == guid)
                {
                    values[7] = 21.5;
                }

                resultantTemperatures[guid_Space] = values;
            }

            List<PartOTM59SpaceResult> spaceResults = [];
            foreach (Guid guid_Space in guids_Bound)
            {
                spaceResults.Add(new PartOTM59SpaceResult(guid_Space, "room", "TM59 Criterion A", 10, 32, TM59ComplianceStatus.Pass, true));
            }

            partOIteration3PipelineFake.Assessment_CandidateB = new PartOIteration3Assessment(true, null, TM59ComplianceStatus.Pass, spaceResults, null, null, null, resultantTemperatures, "report", null, spaceResults.Count);

            PartOIteration3Result partOIteration3Result = Modify.RunPartOIteration3(partORun, partOIteration3PipelineFake);

            Assert.Equal(PartOIteration3Stage.CandidateBTM59, partOIteration3Result.Ledger.Stage_Refused);
            Assert.Contains(partOIteration3Result.Reasons, x => x.Contains("hour 7") && x.Contains("resolving this room to different results"));
            Assert.Null(partOIteration3Result.Comparison);
        }

        [Fact]
        public void An_ineligible_run_refuses_at_the_input_and_calls_nothing()
        {
            PartORun partORun = new();

            PartOIteration3PipelineFake partOIteration3PipelineFake = new();

            PartOIteration3Result partOIteration3Result = Modify.RunPartOIteration3(partORun, partOIteration3PipelineFake);

            Assert.Equal(PartOIteration3Stage.Input, partOIteration3Result.Ledger.Stage_Refused);
            Assert.Empty(partOIteration3PipelineFake.Called);
            Assert.All(partOIteration3Result.Ledger.Stages.GetRange(1, 13), x => Assert.Equal(PartOIteration3StageStatus.NotRun, x.Status));
        }

        /// <summary>
        /// A refused pairing is still written down: its ledger is the diagnosis, and a person reopening
        /// the model tomorrow needs it more than they need a completed one.
        /// </summary>
        [Fact]
        public void A_refused_pairing_still_writes_its_record()
        {
            PartORun partORun = Run();

            PartOIteration3PipelineFake partOIteration3PipelineFake = Pipeline_Complete(out List<Guid> _);

            partOIteration3PipelineFake.Materialisation = Materialisation_Refused("no");

            PartOIteration3Result partOIteration3Result = Modify.RunPartOIteration3(partORun, partOIteration3PipelineFake);

            Assert.True(File.Exists(partOIteration3Result.Path_Record));

            PartOIteration3Record partOIteration3Record = Query.PartOIteration3PairingRecord(partOIteration3Result.Path_Record);

            Assert.False(partOIteration3Record.IsComplete);
            Assert.Equal(PartOIteration3Stage.Materialisation, partOIteration3Record.Stage_Refused);
        }

        /// <summary>
        /// An earlier attempt's reopenable Candidate B must not survive this one. Everything else is
        /// proven by ownership; these two are deleted, because they are what a later session acts on.
        /// </summary>
        [Fact]
        public void The_previous_candidate_B_model_and_record_are_deleted_at_the_start_of_an_attempt()
        {
            PartORun partORun = Run();

            string path_Model = Path.Combine(directory, "Flat-It3B-Bridge.sam");
            string path_Record = Path.Combine(directory, "Flat-Iteration3.json");

            File.WriteAllText(path_Model, "an earlier attempt's reopenable Candidate B");
            File.WriteAllText(path_Record, "an earlier attempt's pairing");

            PartOIteration3PipelineFake partOIteration3PipelineFake = Pipeline_Complete(out List<Guid> _);

            partOIteration3PipelineFake.Materialisation = Materialisation_Refused("no");

            Modify.RunPartOIteration3(partORun, partOIteration3PipelineFake);

            Assert.False(File.Exists(path_Model));

            //The record IS rewritten - by this attempt, with this attempt's refusal.
            Assert.True(File.Exists(path_Record));
            Assert.DoesNotContain("an earlier attempt's pairing", File.ReadAllText(path_Record));
        }

        /// <summary>
        /// A file left at a fixed path by an earlier attempt is never reported as this attempt's - the
        /// ownership rule, exercised through the real orchestration rather than through the artifact
        /// type alone.
        /// </summary>
        [Fact]
        public void A_stale_thermal_source_file_refuses_rather_than_being_reported_as_this_attempts()
        {
            PartORun partORun = Run();

            //Created BEFORE the attempt starts and never touched by it, which is exactly the state a
            //failed previous attempt leaves behind.
            PartOIteration3PipelineFake partOIteration3PipelineFake = Pipeline_Complete(out List<Guid> _);

            //The stage writes nothing this time: the files are exactly where the fixture left them, which
            //is the state a failed previous attempt leaves behind.
            partOIteration3PipelineFake.Paths_ThermalSource.Clear();

            PartOIteration3Result partOIteration3Result = Modify.RunPartOIteration3(partORun, partOIteration3PipelineFake);

            Assert.Equal(PartOIteration3Stage.ThermalSource, partOIteration3Result.Ledger.Stage_Refused);
            Assert.Contains(partOIteration3Result.Reasons, x => x.Contains("unchanged since this attempt started"));

            //And nothing stale is reported as evidence of this attempt.
            Assert.Empty(partOIteration3Result.Ledger.Artifacts);
        }
    }
}
