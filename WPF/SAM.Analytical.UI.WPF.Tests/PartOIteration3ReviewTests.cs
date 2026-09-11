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

            /// <summary>
            /// Whether each assessment rewrites its TM59 report, as the production one does - which is
            /// what a review that fingerprinted the reports tripped over on the next review.
            /// </summary>
            internal bool Write_Reports { get; set; }

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

                PartOIteration3Assessment result = Func_Assess(spaceGuids_Capture);

                return Write_Reports && result is not null && result.IsAssessed
                    ? PartOIteration3PipelineFake.WithReport(result, PartOIteration3PipelineFake.WriteReport(path_TSD))
                    : result;
            }

            public bool Persist(AnalyticalModel analyticalModel, string path_TSD, string path_TBD, out string note)
            {
                throw new InvalidOperationException("A review must not write Candidate B again.");
            }
        }

        //-------------------------------------------------------------------------------------------------
        //A completed pairing, produced once and then reopened
        //-------------------------------------------------------------------------------------------------

        private PartORun Run(out PartOIteration3Result partOIteration3Result, out List<Guid> guids_Bound, bool writeReports = false)
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
                Write_Reports = writeReports,
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

        //-------------------------------------------------------------------------------------------------
        //The TM59 reports a review regenerates
        //-------------------------------------------------------------------------------------------------

        /// <summary>The fingerprint of every file that defines the comparison - everything but the reports.</summary>
        private static Dictionary<string, (long Length, long Ticks)> Fingerprints_Defining(PartOIteration3Record partOIteration3Record)
        {
            Dictionary<string, (long, long)> result = [];

            foreach (PartOIteration3FileRecord partOIteration3FileRecord in partOIteration3Record.Files)
            {
                if (partOIteration3FileRecord.Role == PartOIteration3Roles.ReferenceA_TM59Report || partOIteration3FileRecord.Role == PartOIteration3Roles.CandidateB_TM59Report)
                {
                    continue;
                }

                Assert.True(PartOIteration3Artifacts.TryRead(partOIteration3FileRecord.Path, out long length, out long ticks));

                result[partOIteration3FileRecord.Role] = (length, ticks);
            }

            return result;
        }

        /// <summary>
        /// A review reassesses both results files through the production TM59 path, and that path writes
        /// each report beside its results. A review that fingerprinted the reports therefore made the NEXT
        /// review of the same, unchanged pairing refuse as stale (Codex P1 on <c>bdc48ef</c>). The reports
        /// are regenerated evidence; the comparison is defined by the simulation artifacts and the model.
        /// </summary>
        [Fact]
        public void An_unchanged_pairing_reviews_repeatedly_although_each_review_regenerates_its_TM59_reports()
        {
            PartORun partORun = Run(out PartOIteration3Result partOIteration3Result_Run, out List<Guid> _, true);

            //The run recorded both reports, so there is something a review could wrongly hold it to.
            string path_Report_A = partOIteration3Result_Run.Record.File(PartOIteration3Roles.ReferenceA_TM59Report)?.Path;
            string path_Report_B = partOIteration3Result_Run.Record.File(PartOIteration3Roles.CandidateB_TM59Report)?.Path;

            Assert.NotNull(path_Report_A);
            Assert.NotNull(path_Report_B);

            Dictionary<string, (long Length, long Ticks)> fingerprints_Defining = Fingerprints_Defining(partOIteration3Result_Run.Record);

            Assert.Equal(6, fingerprints_Defining.Count);

            for (int i = 1; i <= 3; i++)
            {
                long length_Before = new FileInfo(path_Report_B).Length;

                int count = 0;

                PartOIteration3PipelineReviewOnly partOIteration3PipelineReviewOnly = new()
                {
                    Write_Reports = true,
                    Func_Assess = guids => Assessment(guids, ++count == 1 ? 20.0 : 21.0),
                };

                PartOIteration3Result partOIteration3Result = Modify.ReviewPartOIteration3(partORun, partOIteration3PipelineReviewOnly);

                Assert.True(partOIteration3Result.IsComplete, string.Format("Review {0} refused: {1}", i, string.Join(" | ", partOIteration3Result.Reasons)));
                Assert.NotNull(partOIteration3Result.Comparison);
                Assert.Equal(1.0, partOIteration3Result.Comparison.Statistics.MeanBias, 12);

                //Only the two assessments were reached; every TAS member throws.
                Assert.Equal(2, partOIteration3PipelineReviewOnly.Count_Assess);

                //This review genuinely regenerated the reports - and offers the ones it wrote.
                Assert.NotEqual(length_Before, new FileInfo(path_Report_B).Length);
                Assert.Equal(path_Report_A, partOIteration3Result.Path_TM59Report_ReferenceA);
                Assert.Equal(path_Report_B, partOIteration3Result.Path_TM59Report_CandidateB);
            }

            //And not one comparison-defining artifact moved in three reviews.
            Assert.Equal(fingerprints_Defining, Fingerprints_Defining(partOIteration3Result_Run.Record));
        }

        /// <summary>
        /// Leaving the reports out of the freshness check leaves every other file in it: after a review has
        /// regenerated the reports, touching any one comparison-defining artifact still refuses, by name,
        /// before anything is read.
        /// </summary>
        [Theory]
        [InlineData(PartOIteration3Roles.ThermalSource_TBD)]
        [InlineData(PartOIteration3Roles.ThermalSource_TSD)]
        [InlineData(PartOIteration3Roles.Systems_TPD)]
        [InlineData(PartOIteration3Roles.Bridge_TBD)]
        [InlineData(PartOIteration3Roles.Bridge_TSD)]
        [InlineData(PartOIteration3Roles.CandidateB_Model)]
        public void After_a_review_regenerated_the_reports_a_touched_comparison_artifact_still_refuses_by_name(string role)
        {
            PartORun partORun = Run(out PartOIteration3Result partOIteration3Result_Run, out List<Guid> _, true);

            int count = 0;

            Assert.True(Modify.ReviewPartOIteration3(partORun, new PartOIteration3PipelineReviewOnly
            {
                Write_Reports = true,
                Func_Assess = guids => Assessment(guids, ++count == 1 ? 20.0 : 21.0),
            }).IsComplete);

            string path = partOIteration3Result_Run.Record.File(role).Path;

            File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddHours(1));

            PartOIteration3PipelineReviewOnly partOIteration3PipelineReviewOnly = new()
            {
                Write_Reports = true,
                Func_Assess = guids => Assessment(guids, 20.0),
            };

            PartOIteration3Result partOIteration3Result = Modify.ReviewPartOIteration3(partORun, partOIteration3PipelineReviewOnly);

            Assert.True(partOIteration3Result.IsRefused);
            Assert.Null(partOIteration3Result.Comparison);
            Assert.Contains(partOIteration3Result.Reasons, x => x.Contains(string.Format("Iteration 3 {0} at '{1}'", role, path)) && x.Contains("rewritten"));

            //Refused before either results file was read.
            Assert.Equal(0, partOIteration3PipelineReviewOnly.Count_Assess);

            //And a refused review offers no report as though it described this pairing.
            Assert.Null(partOIteration3Result.Path_TM59Report_ReferenceA);
            Assert.Null(partOIteration3Result.Path_TM59Report_CandidateB);
        }

        /// <summary>
        /// The reports a record names are lineage, not something the review validated - so a review offers
        /// only the reports ITS OWN assessments wrote, and none where they wrote none.
        /// </summary>
        [Fact]
        public void A_review_offers_only_the_TM59_reports_it_wrote_itself()
        {
            PartORun partORun = Run(out PartOIteration3Result partOIteration3Result_Run, out List<Guid> _, true);

            Assert.NotNull(partOIteration3Result_Run.Record.File(PartOIteration3Roles.ReferenceA_TM59Report));
            Assert.NotNull(partOIteration3Result_Run.Record.File(PartOIteration3Roles.CandidateB_TM59Report));

            int count = 0;

            //Assesses, and writes no report - the state a locked or read-only report leaves behind.
            PartOIteration3Result partOIteration3Result = Modify.ReviewPartOIteration3(partORun, new PartOIteration3PipelineReviewOnly
            {
                Func_Assess = guids => Assessment(guids, ++count == 1 ? 20.0 : 21.0),
            });

            Assert.True(partOIteration3Result.IsComplete);

            Assert.Null(partOIteration3Result.Path_TM59Report_ReferenceA);
            Assert.Null(partOIteration3Result.Path_TM59Report_CandidateB);
        }

        //-------------------------------------------------------------------------------------------------
        //The persisted A/B review report
        //-------------------------------------------------------------------------------------------------

        /// <summary>
        /// A successful pairing writes its whole review beside the pairing record, in both forms, and the
        /// result names what it wrote.
        /// </summary>
        [Fact]
        public void A_completed_run_writes_its_A_B_review_report()
        {
            Run(out PartOIteration3Result partOIteration3Result, out List<Guid> _);

            string path_Report = Path.Combine(directory, "Flat-Iteration3-Review.txt");
            string path_Report_Json = Path.Combine(directory, "Flat-Iteration3-Review.json");

            Assert.Equal(path_Report, partOIteration3Result.Path_Report);
            Assert.Equal(path_Report_Json, partOIteration3Result.Path_Report_Json);
            Assert.Null(partOIteration3Result.Refusal_Report);

            Assert.True(File.Exists(path_Report));
            Assert.True(File.Exists(path_Report_Json));
        }

        /// <summary>
        /// The report has to answer "which A/B pairing is this, of which design state?" on its own, away
        /// from the session and the model that produced it.
        /// </summary>
        [Fact]
        public void The_report_records_the_pairing_identity_and_provenance()
        {
            Run(out PartOIteration3Result partOIteration3Result, out List<Guid> guids_Bound);

            string text = File.ReadAllText(partOIteration3Result.Path_Report);

            PartOIteration3Record partOIteration3Record = partOIteration3Result.Record;

            Assert.Contains("PAIRING PROVENANCE", text);
            Assert.Contains(PartOIteration3ReportText.CurrentSchema, text);
            Assert.Contains(partOIteration3Record.Guid_Run.ToString(), text);
            Assert.Contains(partOIteration3Record.ProjectName_ReferenceA, text);
            Assert.Contains(partOIteration3Record.ProjectName_CandidateB, text);
            Assert.Contains(partOIteration3Record.Path_TSD_ReferenceA, text);
            Assert.Contains(partOIteration3Record.Fingerprint_Model_ReferenceA, text);
            Assert.Contains(partOIteration3Record.Fingerprint_Scenarios_ReferenceA, text);
            Assert.Contains(partOIteration3Record.Fingerprint_Scenario, text);
            Assert.Contains(partOIteration3Result.Path_Record, text);

            //Every bound room, by identity, so the report names the rooms it compared.
            foreach (Guid guid in guids_Bound)
            {
                Assert.Contains(guid.ToString(), text);
            }

            //Both the pooled and the per-room statistics an engineer read on screen.
            Assert.Contains("room(s), ", text);
            Assert.Contains("Dwelling\tSpace\tTM59 criterion", text);

            //And the structured sibling embeds the record whole rather than re-spelling it.
            System.Text.Json.Nodes.JsonObject jsonObject = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(partOIteration3Result.Path_Report_Json)) as System.Text.Json.Nodes.JsonObject;

            Assert.NotNull(jsonObject);
            Assert.Equal(PartOIteration3ReportText.CurrentSchema, (string)jsonObject["Schema"]);
            Assert.NotNull(jsonObject["Record"]);
            Assert.NotNull(jsonObject["Comparison"]);
            Assert.Equal(
                partOIteration3Record.Guid_Run,
                PartOIteration3Record.FromJsonObject(jsonObject["Record"] as System.Text.Json.Nodes.JsonObject).Guid_Run);
        }

        /// <summary>
        /// The report survives the session that wrote it: a later review of the same unchanged pairing
        /// finds it, rewrites it, and a second review of that same pairing produces the same bytes.
        /// </summary>
        [Fact]
        public void The_report_survives_reopening_and_is_deterministic()
        {
            PartORun partORun = Run(out PartOIteration3Result partOIteration3Result_Run, out List<Guid> _);

            string path_Report = partOIteration3Result_Run.Path_Report;

            string text_Run = File.ReadAllText(path_Report);

            int count = 0;

            PartOIteration3Result partOIteration3Result = Modify.ReviewPartOIteration3(partORun, new PartOIteration3PipelineReviewOnly
            {
                Func_Assess = guids => Assessment(guids, ++count == 1 ? 20.0 : 21.0),
            });

            Assert.True(partOIteration3Result.IsComplete);
            Assert.Equal(path_Report, partOIteration3Result.Path_Report);
            Assert.True(File.Exists(path_Report));

            string text_Review = File.ReadAllText(path_Report);

            //A review states its own provenance - that it IS a review, and which reports it wrote - so it
            //is not byte-identical to the run's. What it must be is reproducible, which is what a second
            //review of the same unchanged pairing proves.
            count = 0;

            PartOIteration3Result partOIteration3Result_Again = Modify.ReviewPartOIteration3(partORun, new PartOIteration3PipelineReviewOnly
            {
                Func_Assess = guids => Assessment(guids, ++count == 1 ? 20.0 : 21.0),
            });

            Assert.True(partOIteration3Result_Again.IsComplete);
            Assert.Equal(text_Review, File.ReadAllText(path_Report));

            //And every version names the same pairing.
            Assert.Contains(partOIteration3Result_Run.Record.Guid_Run.ToString(), text_Run);
            Assert.Contains(partOIteration3Result_Run.Record.Guid_Run.ToString(), text_Review);
        }

        /// <summary>
        /// <b>The rule this whole feature turns on.</b> Once Reference A's design state moves, Review
        /// correctly refuses - and the report that described the successful pairing is left exactly where
        /// it is. A refusal that overwrote it would replace the only durable evidence of a real A/B
        /// comparison with a record of a refusal.
        /// </summary>
        [Fact]
        public void A_refused_review_neither_writes_nor_overwrites_the_last_successful_report()
        {
            PartORun partORun = Run(out PartOIteration3Result partOIteration3Result_Run, out List<Guid> guids_Bound);

            string path_Report = partOIteration3Result_Run.Path_Report;
            string path_Report_Json = partOIteration3Result_Run.Path_Report_Json;

            string text = File.ReadAllText(path_Report);
            string text_Json = File.ReadAllText(path_Report_Json);

            long ticks = File.GetLastWriteTimeUtc(path_Report).Ticks;

            //The design state the record copied no longer describes the model in front of us.
            string path_Record = Path.Combine(directory, "Flat-Iteration3.json");

            PartOIteration3Record partOIteration3Record = Query.PartOIteration3PairingRecord(path_Record);

            File.WriteAllText(path_Record, partOIteration3Record.ToString().Replace(partOIteration3Record.Fingerprint_Model_ReferenceA, "0000000000000000"));

            PartOIteration3Result partOIteration3Result = Modify.ReviewPartOIteration3(partORun, ReviewPipeline(guids_Bound));

            Assert.True(partOIteration3Result.IsRefused);
            Assert.Contains(partOIteration3Result.Reasons, x => x.Contains("design state has changed"));

            //The refused review claims no report of its own...
            Assert.Null(partOIteration3Result.Path_Report);
            Assert.Null(partOIteration3Result.Path_Report_Json);
            Assert.Null(partOIteration3Result.Refusal_Report);

            //...and the successful one is untouched, byte for byte.
            Assert.Equal(text, File.ReadAllText(path_Report));
            Assert.Equal(text_Json, File.ReadAllText(path_Report_Json));
            Assert.Equal(ticks, File.GetLastWriteTimeUtc(path_Report).Ticks);

            //It is still reachable as history, at the path the window derives from the pairing record.
            Assert.Equal(path_Report, PartOIteration3Paths.Path_Report_ForRecord(partOIteration3Result.Path_Record));
        }

        /// <summary>
        /// A pairing that refused on its own run writes no report at all - there is no comparison to
        /// persist, and a file named "review report" holding nothing but a refusal is exactly the artifact
        /// this design exists to prevent.
        /// </summary>
        [Fact]
        public void A_refused_run_writes_no_report()
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

            PartOIteration3Result partOIteration3Result = Modify.RunPartOIteration3(partORun, new PartOIteration3PipelineFake
            {
                Materialisation = new MechanicalVentilationMaterialisation(null, ["The topology template could not be resolved."], null, null),
                Assessment_ReferenceA = Assessment(guids_Space_Dwelling, 20.0),
            });

            Assert.True(partOIteration3Result.IsRefused);
            Assert.Null(partOIteration3Result.Path_Report);
            Assert.False(File.Exists(Path.Combine(directory, "Flat-Iteration3-Review.txt")));
            Assert.False(File.Exists(Path.Combine(directory, "Flat-Iteration3-Review.json")));
        }

        /// <summary>
        /// A later save that fails part way through - here, because another handle has the .json sibling
        /// open exclusively, the same symptom a locked or permission-denied file produces - must leave the
        /// pairing exactly as the earlier successful save left it: not just "a report still exists", but
        /// the <b>same bytes</b> on <b>both</b> siblings, never one replaced by this attempt's (different)
        /// content while the other is left from the earlier one.
        /// <para>
        /// The second review is given different Reference A/Candidate B temperatures than the run used, so
        /// its report would be textually different from the run's if it were written - which is what makes
        /// this test able to tell "overwritten" apart from "untouched". A writer that composed straight
        /// onto <c>path_Report</c> before attempting <c>path_Report_Json</c> would let this review's
        /// content land on the first file while the run's content is still on the second - passing a
        /// "some report still exists" check while failing this one.
        /// </para>
        /// </summary>
        [Fact]
        public void A_locked_destination_leaves_both_siblings_of_the_earlier_successful_report_untouched()
        {
            PartORun partORun = Run(out PartOIteration3Result partOIteration3Result_Run, out List<Guid> _);

            string path_Report = partOIteration3Result_Run.Path_Report;
            string path_Report_Json = partOIteration3Result_Run.Path_Report_Json;

            string text = File.ReadAllText(path_Report);
            string text_Json = File.ReadAllText(path_Report_Json);

            PartOIteration3Result partOIteration3Result_Review;

            //FileShare.None reproduces, from this same process, exactly what a locked or permission-denied
            //file looks like to the writer: neither a rename nor a delete of this path can go through while
            //the handle is open.
            using (new FileStream(path_Report_Json, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                int count = 0;

                partOIteration3Result_Review = Modify.ReviewPartOIteration3(partORun, new PartOIteration3PipelineReviewOnly
                {
                    Func_Assess = guids => Assessment(guids, ++count == 1 ? 20.0 : 25.0),
                });

                Assert.True(partOIteration3Result_Review.IsComplete);

                //The review itself is genuine - it is only the save that could not complete.
                Assert.Null(partOIteration3Result_Review.Path_Report);
                Assert.Null(partOIteration3Result_Review.Path_Report_Json);
                Assert.NotNull(partOIteration3Result_Review.Refusal_Report);
            }

            //Both siblings the run wrote are exactly as the run left them - not one of them replaced by
            //this review's (different) numbers while the lock was held.
            Assert.Equal(text, File.ReadAllText(path_Report));
            Assert.Equal(text_Json, File.ReadAllText(path_Report_Json));

            //And no temporary or backup file from the failed attempt was left beside them.
            Assert.False(File.Exists(path_Report + ".tmp"));
            Assert.False(File.Exists(path_Report_Json + ".tmp"));
            Assert.False(File.Exists(path_Report + ".bak"));
            Assert.False(File.Exists(path_Report_Json + ".bak"));
        }
    }
}
