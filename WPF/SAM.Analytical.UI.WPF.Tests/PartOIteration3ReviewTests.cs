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
using System.Threading;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// <b>Reopening a pairing rebuilds its comparison from the results that already exist, and starts no
    /// TAS.</b>
    ///
    /// <para><b>How "no TAS" is asserted rather than assumed</b></para>
    /// <para>
    /// The review is handed a pipeline whose four TAS-running members <b>throw</b>. If the review ever
    /// reached one of them the test would fail with that exception rather than with a subtle difference
    /// in a number - which is the only way to assert a negative about a process that would otherwise take
    /// an hour to start.
    /// </para>
    ///
    /// <para><b>And what it refuses</b></para>
    /// <para>
    /// A pairing is a statement about two results files produced from one design state. Any of the three
    /// moving makes it false, and each can move between sessions. Every one of those is refused
    /// <b>by name</b>, because "this is stale" without saying what moved is not something a person can
    /// act on.
    /// </para>
    /// </summary>
    public class PartOIteration3ReviewTests : IDisposable
    {
        private readonly string directory = PartOIteration3Fixture.Directory_Temp();

        private AdjacencyCluster adjacencyCluster;

        private List<Guid> guids_VentilationSystem;

        private List<Zone> zones;

        private List<Guid> guids_Space_Dwelling;

        public void Dispose()
        {
            try
            {
                Directory.Delete(directory, true);
            }
            catch (IOException)
            {
            }
        }

        /// <summary>A pipeline that refuses to do anything a TAS run would do.</summary>
        private class PartOIteration3PipelineReviewOnly : IPartOIteration3Pipeline
        {
            internal Func<IEnumerable<Guid>, PartOIteration3Assessment> Func_Assess { get; set; }

            internal int Count_Assess { get; private set; }

            public MechanicalVentilationMaterialisation Materialise(AdjacencyCluster adjacencyCluster, IEnumerable<Space> spaces)
            {
                throw new InvalidOperationException("A review must not materialise anything.");
            }

            public NoIzamThermalSource ThermalSource(AnalyticalModel analyticalModel_Prepared, PartOSimulationContext partOSimulationContext, string projectName, CancellationToken cancellationToken, out AnalyticalModel analyticalModel_Source, out bool cancelled, out List<string> notes, out string refusal)
            {
                throw new InvalidOperationException("A review must not run TAS.");
            }

            public SystemVentilationRoute Route(NoIzamThermalSource noIzamThermalSource, MechanicalVentilationMaterialisation mechanicalVentilationMaterialisation, string path_TPD, int startHour, int endHour)
            {
                throw new InvalidOperationException("A review must not convert or simulate anything.");
            }

            public ResultantTemperatureResults ResultantTemperatures(SystemVentilationRoute systemVentilationRoute, string path_TBD_Bridge)
            {
                throw new InvalidOperationException("A review must not run the resultant temperature bridge.");
            }

            public PartOIteration3Assessment Assess(AnalyticalModel analyticalModel_Workflow, string path_TSD, IEnumerable<OverheatingScenario> overheatingScenarios, IEnumerable<Guid> spaceGuids_Capture)
            {
                Count_Assess++;

                return Func_Assess(spaceGuids_Capture);
            }

            public bool Persist(AnalyticalModel analyticalModel, string path_TSD, string path_TBD, out string note)
            {
                throw new InvalidOperationException("A review must not write Candidate B again.");
            }
        }

        //-------------------------------------------------------------------------------------------------
        //A completed pairing, produced once and then reopened
        //-------------------------------------------------------------------------------------------------

        private PartORun Run(out PartOIteration3Result partOIteration3Result, out List<Guid> guids_Bound)
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

            PartORun partORun = new();

            Assert.True(partORun.Prepare(
                PartOIteration3Fixture.Model(adjacencyCluster),
                PartOIteration3Fixture.Scenarios(),
                new PartOPreparationContext(PartOIteration.BasePassive, zones, null, null),
                guids_VentilationSystem));

            string path_TSD = Path.Combine(directory, "Flat.tsd");

            Assert.True(partORun.ExpectResults(path_TSD));

            File.WriteAllText(path_TSD, "reference A results");

            AnalyticalModel analyticalModel_Workflow = PartOIteration3Fixture.Model(new AdjacencyCluster(adjacencyCluster), "Flat");

            analyticalModel_Workflow.SetValue(Analytical.AnalyticalModelParameter.OverheatingScenarios, new Core.SAMCollection<OverheatingScenario>(partORun.OverheatingScenarios));
            analyticalModel_Workflow.SetValue(Analytical.AnalyticalModelParameter.SimulationResultProvenance, new SimulationResultProvenance(analyticalModel_Workflow, path_TSD));

            Assert.True(partORun.Complete(analyticalModel_Workflow, path_TSD, PartOIteration3Fixture.SimulationContext(directory), out string _));

            NoIzamThermalSource noIzamThermalSource = PartOIteration3Fixture.ThermalSource(directory, guids_Space_Dwelling);

            List<SystemVentilationBinding> bindings = [];

            Guid guid_AirSystem = Guid.NewGuid();

            foreach (Guid guid in guids_Space_Dwelling)
            {
                Space space = adjacencyCluster.GetObject<Space>(guid);

                List<VentilationTerminal> ventilationTerminals = adjacencyCluster.VentilationTerminals(space) ?? [];

                bindings.Add(PartOIteration3Fixture.Binding(
                    guid,
                    guid_AirSystem,
                    Analytical.Query.VentilationTerminalDesignDuty_Lps(ventilationTerminals, FlowClassification.Supply),
                    Analytical.Query.VentilationTerminalDesignDuty_Lps(ventilationTerminals, FlowClassification.Extract),
                    out Guid _));
            }

            guids_Bound = [];
            foreach (SystemVentilationBinding systemVentilationBinding in bindings)
            {
                guids_Bound.Add(systemVentilationBinding.Guid_Space);
            }

            guids_Bound.Sort();

            List<SystemVentilationConnectionBinding> connectionBindings = [];

            Dictionary<Guid, Guid> dictionary_SystemSpace = [];
            foreach (SystemVentilationBinding systemVentilationBinding in bindings)
            {
                dictionary_SystemSpace[systemVentilationBinding.Guid_Space] = systemVentilationBinding.Guid_SystemSpace;
            }

            foreach (KeyValuePair<(Guid, Guid), Analytical.Query.DesignTransferAirMovement> keyValuePair in adjacencyCluster.DesignTransferSpaceAirMovements())
            {
                connectionBindings.Add(new SystemVentilationConnectionBinding(
                    SystemVentilationConnectionType.Transfer,
                    keyValuePair.Value.SpaceAirMovement.Guid,
                    Guid.NewGuid(),
                    guid_AirSystem,
                    dictionary_SystemSpace[keyValuePair.Value.FromGuid],
                    dictionary_SystemSpace[keyValuePair.Value.ToGuid],
                    adjacencyCluster.DesignTransferFlowRate_Lps(keyValuePair.Value.FromGuid, keyValuePair.Value.ToGuid, out Guid _, out Guid _).Value,
                    "damper"));
            }

            string path_TPD = Path.Combine(directory, "Flat-It3B.tpd");
            string path_TSD_Bridge = Path.Combine(directory, "Flat-It3B-Bridge.tsd");

            PartOIteration3PipelineFake partOIteration3PipelineFake = new()
            {
                Materialisation = new MechanicalVentilationMaterialisation(new Core.Systems.SystemEnergyCentre("Part O"), null, null, null),
                NoIzamThermalSource = noIzamThermalSource,
                AnalyticalModel_CandidateB = PartOIteration3Fixture.Model(new AdjacencyCluster(adjacencyCluster), "Flat-It3B"),
                SystemVentilationRoute = new SystemVentilationRoute(
                    noIzamThermalSource,
                    path_TPD,
                    PartOIteration3Fixture.Evidence(path_TPD, path_TPD),
                    bindings,
                    connectionBindings,
                    PartOIteration3Fixture.ZoneTemperatures(bindings, 0, 23),
                    null,
                    null),
                ResultantTemperatureResults = PartOIteration3Fixture.ResultantTemperatures(path_TSD_Bridge, guids_Bound, 0, 23, (guid, hour) => 21.0),
                Assessment_ReferenceA = Assessment(guids_Space_Dwelling, 20.0),
                Assessment_CandidateB = Assessment(guids_Bound, 21.0),
                Persist_ForReal = true,
            };

            partOIteration3PipelineFake.Paths_ThermalSource.Add(Path.Combine(directory, "Flat-It3B.tbd"));
            partOIteration3PipelineFake.Paths_ThermalSource.Add(Path.Combine(directory, "Flat-It3B.tsd"));
            partOIteration3PipelineFake.Paths_Route.Add(path_TPD);
            partOIteration3PipelineFake.Paths_Bridge.Add(Path.Combine(directory, "Flat-It3B-Bridge.tbd"));
            partOIteration3PipelineFake.Paths_Bridge.Add(path_TSD_Bridge);

            partOIteration3Result = Modify.RunPartOIteration3(partORun, partOIteration3PipelineFake);

            Assert.True(partOIteration3Result.IsComplete);

            return partORun;
        }

        private static PartOIteration3Assessment Assessment(IEnumerable<Guid> guids, double value)
        {
            List<PartOTM59SpaceResult> spaceResults = [];
            Dictionary<Guid, double[]> resultantTemperatures = [];

            foreach (Guid guid in guids)
            {
                spaceResults.Add(new PartOTM59SpaceResult(guid, "room", "TM59 Criterion A", 10, 32, TM59ComplianceStatus.Pass, true));

                double[] values = new double[24];

                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = value;
                }

                resultantTemperatures[guid] = values;
            }

            return new PartOIteration3Assessment(true, null, TM59ComplianceStatus.Pass, spaceResults, null, null, null, resultantTemperatures, "report", null, spaceResults.Count);
        }

        private PartOIteration3PipelineReviewOnly ReviewPipeline(List<Guid> guids_Bound)
        {
            return new PartOIteration3PipelineReviewOnly
            {
                Func_Assess = guids => Assessment(guids, guids_Bound.Count != 0 ? 20.0 : 20.0),
            };
        }

        //-------------------------------------------------------------------------------------------------
        //The review
        //-------------------------------------------------------------------------------------------------

        [Fact]
        public void A_completed_pairing_reopens_and_rebuilds_its_comparison_without_running_TAS()
        {
            PartORun partORun = Run(out PartOIteration3Result _, out List<Guid> guids_Bound);

            PartOIteration3PipelineReviewOnly partOIteration3PipelineReviewOnly = new()
            {
                //Reference A at 20, Candidate B at 21 - the same one-degree offset the run recorded, so
                //the rebuilt statistics are the run's.
                Func_Assess = guids => Assessment(guids, 20.0),
            };

            int count = 0;

            partOIteration3PipelineReviewOnly.Func_Assess = guids => Assessment(guids, ++count == 1 ? 20.0 : 21.0);

            PartOIteration3Result partOIteration3Result = Modify.ReviewPartOIteration3(partORun, partOIteration3PipelineReviewOnly);

            Assert.True(partOIteration3Result.IsRestored);
            Assert.True(partOIteration3Result.IsComplete);
            Assert.NotNull(partOIteration3Result.Comparison);

            Assert.Equal(guids_Bound.Count, partOIteration3Result.Comparison.Statistics.Count_Rooms);
            Assert.Equal(1.0, partOIteration3Result.Comparison.Statistics.MeanBias, 12);

            //Only the two assessments were reached; every TAS member throws.
            Assert.Equal(2, partOIteration3PipelineReviewOnly.Count_Assess);

            Assert.Contains(partOIteration3Result.Notes, x => x.Contains("No TAS simulation was run and no TAS file was written."));
        }

        /// <summary>
        /// Re-exporting the unchanged completed comparison is deterministic - the same statistics, in the
        /// same order, from the record's own room set and dwelling grouping.
        /// </summary>
        [Fact]
        public void Reopening_twice_rebuilds_the_same_comparison()
        {
            PartORun partORun = Run(out PartOIteration3Result _, out List<Guid> _);

            string First()
            {
                int count = 0;

                PartOIteration3Result partOIteration3Result = Modify.ReviewPartOIteration3(partORun, new PartOIteration3PipelineReviewOnly
                {
                    Func_Assess = guids => Assessment(guids, ++count == 1 ? 20.0 : 21.0),
                });

                System.Text.StringBuilder stringBuilder = new();

                foreach (PartOIteration3RoomComparison partOIteration3RoomComparison in partOIteration3Result.Comparison.Rooms)
                {
                    stringBuilder.AppendLine(partOIteration3RoomComparison.ToString());
                }

                foreach (PartOIteration3DwellingStatistics partOIteration3DwellingStatistics in partOIteration3Result.Comparison.Dwellings)
                {
                    stringBuilder.AppendLine(partOIteration3DwellingStatistics.ToString());
                }

                return stringBuilder.ToString();
            }

            Assert.Equal(First(), First());
        }

        [Fact]
        public void A_touched_bridge_result_file_refuses_by_name()
        {
            PartORun partORun = Run(out PartOIteration3Result _, out List<Guid> guids_Bound);

            string path = Path.Combine(directory, "Flat-It3B-Bridge.tsd");

            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(1));

            PartOIteration3Result partOIteration3Result = Modify.ReviewPartOIteration3(partORun, ReviewPipeline(guids_Bound));

            Assert.True(partOIteration3Result.IsRefused);
            Assert.Null(partOIteration3Result.Comparison);
            Assert.Contains(partOIteration3Result.Reasons, x => x.Contains(PartOIteration3Roles.Bridge_TSD) && x.Contains("rewritten"));
        }

        [Fact]
        public void A_missing_candidate_B_model_refuses_by_name()
        {
            PartORun partORun = Run(out PartOIteration3Result _, out List<Guid> guids_Bound);

            File.Delete(Path.Combine(directory, "Flat-It3B-Bridge.sam"));

            PartOIteration3Result partOIteration3Result = Modify.ReviewPartOIteration3(partORun, ReviewPipeline(guids_Bound));

            Assert.True(partOIteration3Result.IsRefused);
            Assert.Contains(partOIteration3Result.Reasons, x => x.Contains(PartOIteration3Roles.CandidateB_Model) && x.Contains("no longer at"));
        }

        [Fact]
        public void A_reference_A_whose_design_state_has_moved_refuses()
        {
            PartORun partORun = Run(out PartOIteration3Result _, out List<Guid> guids_Bound);

            //The design state fingerprint the record copied no longer describes the model in front of us.
            PartOIteration3Record partOIteration3Record = Query.PartOIteration3PairingRecord(Path.Combine(directory, "Flat-Iteration3.json"));

            string text = partOIteration3Record.ToString().Replace(partOIteration3Record.Fingerprint_Model_ReferenceA, "0000000000000000");

            File.WriteAllText(Path.Combine(directory, "Flat-Iteration3.json"), text);

            PartOIteration3Result partOIteration3Result = Modify.ReviewPartOIteration3(partORun, ReviewPipeline(guids_Bound));

            Assert.True(partOIteration3Result.IsRefused);
            Assert.Contains(partOIteration3Result.Reasons, x => x.Contains("design state has changed"));
        }

        [Fact]
        public void A_record_of_another_schema_refuses()
        {
            PartORun partORun = Run(out PartOIteration3Result _, out List<Guid> guids_Bound);

            string path_Record = Path.Combine(directory, "Flat-Iteration3.json");

            File.WriteAllText(path_Record, File.ReadAllText(path_Record).Replace(PartOIteration3Record.CurrentSchema, "PartOIteration3Record:v0"));

            PartOIteration3Result partOIteration3Result = Modify.ReviewPartOIteration3(partORun, ReviewPipeline(guids_Bound));

            Assert.True(partOIteration3Result.IsRefused);
            Assert.Contains(partOIteration3Result.Reasons, x => x.Contains("schema"));
        }

        [Fact]
        public void No_record_at_all_refuses_rather_than_inventing_a_pairing()
        {
            PartORun partORun = Run(out PartOIteration3Result _, out List<Guid> guids_Bound);

            File.Delete(Path.Combine(directory, "Flat-Iteration3.json"));

            PartOIteration3Result partOIteration3Result = Modify.ReviewPartOIteration3(partORun, ReviewPipeline(guids_Bound));

            Assert.True(partOIteration3Result.IsRefused);
            Assert.Null(partOIteration3Result.Comparison);
        }

        /// <summary>
        /// A refused pairing reopens to show its ledger - and reads nothing, because there is nothing to
        /// read. That is the whole reason a refusal is written down.
        /// </summary>
        [Fact]
        public void A_refused_pairing_reopens_to_its_ledger_and_reads_no_results()
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

            PartORun partORun = new();

            partORun.Prepare(
                PartOIteration3Fixture.Model(adjacencyCluster),
                PartOIteration3Fixture.Scenarios(),
                new PartOPreparationContext(PartOIteration.BasePassive, zones, null, null),
                guids_VentilationSystem);

            string path_TSD = Path.Combine(directory, "Flat.tsd");

            partORun.ExpectResults(path_TSD);

            File.WriteAllText(path_TSD, "reference A results");

            AnalyticalModel analyticalModel_Workflow = PartOIteration3Fixture.Model(new AdjacencyCluster(adjacencyCluster), "Flat");

            analyticalModel_Workflow.SetValue(Analytical.AnalyticalModelParameter.OverheatingScenarios, new Core.SAMCollection<OverheatingScenario>(partORun.OverheatingScenarios));
            analyticalModel_Workflow.SetValue(Analytical.AnalyticalModelParameter.SimulationResultProvenance, new SimulationResultProvenance(analyticalModel_Workflow, path_TSD));

            partORun.Complete(analyticalModel_Workflow, path_TSD, PartOIteration3Fixture.SimulationContext(directory), out string _);

            PartOIteration3PipelineFake partOIteration3PipelineFake = new()
            {
                Materialisation = new MechanicalVentilationMaterialisation(null, ["The topology template could not be resolved."], null, null),
                Assessment_ReferenceA = Assessment(guids_Space_Dwelling, 20.0),
            };

            PartOIteration3Result partOIteration3Result_Run = Modify.RunPartOIteration3(partORun, partOIteration3PipelineFake);

            Assert.True(partOIteration3Result_Run.IsRefused);

            PartOIteration3PipelineReviewOnly partOIteration3PipelineReviewOnly = new();

            PartOIteration3Result partOIteration3Result = Modify.ReviewPartOIteration3(partORun, partOIteration3PipelineReviewOnly);

            Assert.True(partOIteration3Result.IsRestored);
            Assert.True(partOIteration3Result.IsRefused);
            Assert.Null(partOIteration3Result.Comparison);

            //The recorded refusal, verbatim, and its ledger.
            Assert.Equal(PartOIteration3Stage.Materialisation, partOIteration3Result.Ledger.Stage_Refused);
            Assert.Contains("The topology template could not be resolved.", partOIteration3Result.Reasons);

            //Nothing was read.
            Assert.Equal(0, partOIteration3PipelineReviewOnly.Count_Assess);
        }
    }
}
