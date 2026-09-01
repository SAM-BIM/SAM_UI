// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using SAM.Analytical.UI;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// <b>The model-lineage lock.</b> A TM59 assessment must be given the model the TAS workflow
    /// <i>returned</i>, never the model that was handed to it.
    /// <para>
    /// The query resolves a simulated space back to a design space through <c>SpaceParameter.ZoneGuid</c>, and
    /// only the workflow's output carries the current TAS zone identities - a preparation output can still hold
    /// stale guids from an earlier round trip. Measured both ways on the licensed acceptance run: preparation
    /// output gives an incomplete <c>SimulationSpaceMap</c> and every space refused; workflow output resolves
    /// all nine. The failure mode is a silent empty answer, not an error, which is why it is pinned here rather
    /// than left to be noticed.
    /// </para>
    /// <para>
    /// <b>These tests are about SAM_UI's wiring, not about <c>SimulationSpaceMap</c>.</b> Whether that map
    /// resolves a given pair of space lists is <c>SAM_Tas</c>'s own behaviour and is tested there. What is
    /// tested here is that this UI hands over the right model at all, that it refuses rather than substitutes
    /// when it cannot, and that a stale pending run cannot be paired with someone else's results.
    /// </para>
    /// </summary>
    public class PartORunLineageTests
    {
        private static AnalyticalModel Model(string name)
        {
            return new AnalyticalModel(name, null, null, null, new AdjacencyCluster(), null, null);
        }

        private static List<OverheatingScenario> Scenarios()
        {
            return [new OverheatingScenario(PartOAssessmentScope.Dwelling, Guid.NewGuid(), PartOIteration.BasePassive)];
        }

        /// <summary>Creates a real file, because a completed run requires results that exist.</summary>
        private static string TemporaryTsd()
        {
            string result = TemporaryTsdPath();

            File.WriteAllText(result, "not a real tsd - only its existence, length and write time are read here");

            return result;
        }

        /// <summary>A path with no file behind it, for the "this workflow created it" case.</summary>
        private static string TemporaryTsdPath()
        {
            return Path.Combine(Path.GetTempPath(), string.Format("SAM_PartORunTests_{0}.tsd", Guid.NewGuid()));
        }

        /// <summary>
        /// What the workflow does to the results file: writes it. Deliberately a different length as well as a
        /// new write time, so the change is detectable whatever the filesystem's timestamp granularity.
        /// </summary>
        private static void WriteResults(string path_TSD)
        {
            File.WriteAllText(path_TSD, string.Format("results this workflow wrote - {0}", Guid.NewGuid()));
        }

        /// <summary>
        /// The production sequence, in the order <c>Modify.Simulate</c> performs it: announce the results file
        /// <b>before</b> the workflow - which it does only where <c>Query.IsPartOFullYearSimulation</c> says the
        /// settings describe a full-year run - let the workflow write that file, then complete.
        /// <para>
        /// Every successful completion in these tests goes through here. A test that called <c>Complete</c>
        /// without arming would be exercising a caller that does not exist, and would keep passing while the
        /// guarantee this helper stands for was broken.
        /// </para>
        /// </summary>
        private static bool CompleteThroughAFullYearWorkflow(PartORun partORun, AnalyticalModel analyticalModel_Workflow, string path_TSD, out string refusal)
        {
            Assert.True(partORun.ExpectResults(path_TSD));

            WriteResults(path_TSD);

            return partORun.Complete(analyticalModel_Workflow, path_TSD, out refusal);
        }

        /// <summary>A fresh run has nothing to assess and says so rather than offering a default.</summary>
        [Fact]
        public void AFreshRun_CannotBeAssessed()
        {
            PartORun partORun = new();

            Assert.Equal(PartORunState.None, partORun.State);
            Assert.False(partORun.CanAssess);
            Assert.False(partORun.IsAssessable(out string refusal));
            Assert.False(string.IsNullOrWhiteSpace(refusal));
            Assert.Null(partORun.AnalyticalModel_Assessment);
            Assert.Null(partORun.Path_TSD);
        }

        /// <summary>
        /// A prepared but unsimulated run cannot be assessed, and exposes no assessment model - so there is
        /// nothing for a caller to fall back to even if it wanted to.
        /// </summary>
        [Fact]
        public void APreparedRun_CannotBeAssessedAndExposesNoAssessmentModel()
        {
            PartORun partORun = new();

            Assert.True(partORun.Prepare(Model("prepared"), Scenarios()));
            Assert.Equal(PartORunState.Prepared, partORun.State);

            Assert.False(partORun.CanAssess);
            Assert.False(partORun.IsAssessable(out string refusal));
            Assert.Contains("not been simulated", refusal);

            //The point of the whole type: the prepared model is present and is still not offered as the model
            //to assess.
            Assert.NotNull(partORun.AnalyticalModel_Prepared);
            Assert.Null(partORun.AnalyticalModel_Assessment);
        }

        /// <summary>
        /// A completed run assesses the model the workflow returned. Asserted by reference against both
        /// candidates, so "the right one" cannot be satisfied by an equal-looking copy of the wrong one.
        /// </summary>
        [Fact]
        public void ACompletedRun_AssessesTheWorkflowModelAndNotThePreparationModel()
        {
            AnalyticalModel analyticalModel_Prepared = Model("prepared");
            AnalyticalModel analyticalModel_Workflow = Model("workflow");

            string path_TSD = TemporaryTsd();

            try
            {
                PartORun partORun = new();

                Assert.True(partORun.Prepare(analyticalModel_Prepared, Scenarios()));
                Assert.True(CompleteThroughAFullYearWorkflow(partORun, analyticalModel_Workflow, path_TSD, out string refusal));
                Assert.Null(refusal);

                Assert.Equal(PartORunState.WorkflowCompleted, partORun.State);
                Assert.True(partORun.CanAssess);
                Assert.True(partORun.IsAssessable(out string _));

                Assert.Same(analyticalModel_Workflow, partORun.AnalyticalModel_Assessment);
                Assert.NotSame(analyticalModel_Prepared, partORun.AnalyticalModel_Assessment);
                Assert.Equal(path_TSD, partORun.Path_TSD);

                //Carried over from the preparation this run was built on - the completed state needs them to
                //attribute results, and they must be that preparation's and no other's.
                Assert.Single(partORun.OverheatingScenarios);
            }
            finally
            {
                File.Delete(path_TSD);
            }
        }

        /// <summary>
        /// A workflow that returned no model does not complete a run, and nothing else is put in its place.
        /// </summary>
        [Fact]
        public void AMissingWorkflowModel_RefusesWithNoFallback()
        {
            string path_TSD = TemporaryTsd();

            try
            {
                PartORun partORun = new();
                partORun.Prepare(Model("prepared"), Scenarios());

                Assert.False(partORun.Complete(null, path_TSD, out string refusal));
                Assert.Contains("no analytical model", refusal);

                Assert.Equal(PartORunState.None, partORun.State);
                Assert.Null(partORun.AnalyticalModel_Assessment);
                Assert.Null(partORun.AnalyticalModel_Prepared);
            }
            finally
            {
                File.Delete(path_TSD);
            }
        }

        /// <summary>
        /// A derived results path with no file behind it does not complete a run. A sizing-only run writes no
        /// TSD, and a guessed file name is not a result.
        /// </summary>
        [Fact]
        public void AMissingTsd_RefusesWithNoFallback()
        {
            PartORun partORun = new();
            partORun.Prepare(Model("prepared"), Scenarios());

            string path_TSD = Path.Combine(Path.GetTempPath(), string.Format("SAM_PartORunTests_{0}.tsd", Guid.NewGuid()));

            Assert.False(partORun.Complete(Model("workflow"), path_TSD, out string refusal));
            Assert.Contains("No simulation results were found", refusal);

            Assert.Equal(PartORunState.None, partORun.State);
            Assert.False(partORun.CanAssess);
        }

        /// <summary>
        /// <b>The staleness lock.</b> A model replacement the Part O flow did not announce drops the prepared
        /// run, so a workflow that follows it cannot be paired with the earlier preparation's scenarios.
        /// </summary>
        [Fact]
        public void AnUnannouncedModelChange_DropsAPreparedRunAndBlocksCompletion()
        {
            string path_TSD = TemporaryTsd();

            try
            {
                PartORun partORun = new();
                partORun.Prepare(Model("prepared"), Scenarios());

                //An edit, an import, an undo - anything that replaces the loaded model.
                partORun.NotifyModified();

                Assert.Equal(PartORunState.None, partORun.State);
                Assert.False(string.IsNullOrWhiteSpace(partORun.InvalidationReason));

                //And the workflow that follows cannot complete the dropped run.
                Assert.False(partORun.Complete(Model("workflow"), path_TSD, out string refusal));
                Assert.Contains("No Part O iteration is prepared", refusal);
                Assert.False(partORun.CanAssess);
            }
            finally
            {
                File.Delete(path_TSD);
            }
        }

        /// <summary>
        /// The Part O flow's own write does not drop the run: it arms one expectation first, and exactly one is
        /// consumed. A second, unannounced change still drops it.
        /// </summary>
        [Fact]
        public void AnAnnouncedModelChange_IsConsumedOnceAndOnlyOnce()
        {
            PartORun partORun = new();
            partORun.Prepare(Model("prepared"), Scenarios());

            partORun.ExpectModification();
            partORun.NotifyModified();

            Assert.Equal(PartORunState.Prepared, partORun.State);

            //The expectation was one shot.
            partORun.NotifyModified();

            Assert.Equal(PartORunState.None, partORun.State);
        }

        /// <summary>
        /// A second simulation cannot re-point a finished run at its results while the run keeps the first
        /// simulation's model. Completing is legal only from Prepared.
        /// </summary>
        [Fact]
        public void ASecondWorkflowResult_CannotBePairedWithAnAlreadyCompletedRun()
        {
            AnalyticalModel analyticalModel_Workflow_1 = Model("workflow 1");

            string path_TSD = TemporaryTsd();

            try
            {
                PartORun partORun = new();
                partORun.Prepare(Model("prepared"), Scenarios());
                Assert.True(CompleteThroughAFullYearWorkflow(partORun, analyticalModel_Workflow_1, path_TSD, out string _));

                Assert.False(partORun.Complete(Model("workflow 2"), path_TSD, out string refusal));
                Assert.Contains("already has results", refusal);

                //Dropped rather than left holding a mismatched pair.
                Assert.Equal(PartORunState.None, partORun.State);
                Assert.Null(partORun.AnalyticalModel_Assessment);
            }
            finally
            {
                File.Delete(path_TSD);
            }
        }

        /// <summary>
        /// Results rewritten after the run produced them are no longer the results the run's scenarios
        /// describe, so the assessment is refused even though the state says completed.
        /// </summary>
        [Fact]
        public void ResultsRewrittenAfterTheRun_AreNotAssessed()
        {
            string path_TSD = TemporaryTsd();

            try
            {
                PartORun partORun = new();
                partORun.Prepare(Model("prepared"), Scenarios());
                Assert.True(CompleteThroughAFullYearWorkflow(partORun, Model("workflow"), path_TSD, out string _));
                Assert.True(partORun.IsAssessable(out string _));

                //Another session, or a rerun from outside this window.
                File.SetLastWriteTimeUtc(path_TSD, File.GetLastWriteTimeUtc(path_TSD).AddMinutes(1));

                Assert.False(partORun.IsAssessable(out string refusal));
                Assert.Contains("rewritten", refusal);
            }
            finally
            {
                File.Delete(path_TSD);
            }
        }

        /// <summary>
        /// A preparation that stated no scenario is not a pending run: every space would be refused a
        /// criterion, and that is worth saying before a simulation rather than after one.
        /// </summary>
        [Fact]
        public void APreparationWithNoScenario_StartsNoRun()
        {
            PartORun partORun = new();

            Assert.False(partORun.Prepare(Model("prepared"), []));
            Assert.Equal(PartORunState.None, partORun.State);
            Assert.Contains("no overheating scenario", partORun.InvalidationReason);
        }

        /// <summary>Closing or opening a model clears the run outright, with no reason to explain.</summary>
        [Fact]
        public void Reset_ClearsTheRunWithoutARetainedReason()
        {
            PartORun partORun = new();
            partORun.Prepare(Model("prepared"), Scenarios());
            partORun.NotifyModified();

            Assert.NotNull(partORun.InvalidationReason);

            partORun.Reset();

            Assert.Equal(PartORunState.None, partORun.State);
            Assert.Null(partORun.InvalidationReason);
        }

        // ------------------------------------------------------------------------------------------------
        // The full-year requirement. WorkflowCompleted must mean the prepared run produced the FULL annual
        // series a TM59 assessment reads - a workflow returning an analytical model proves nothing, since
        // sizing alone returns one.
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// The three fields <c>Modify.Simulate</c> hands to <c>WorkflowCalculator</c> that decide this. Each
        /// caller below states the combination one branch of that method actually produces.
        /// </summary>
        private static SAM.Analytical.Tas.WorkflowSettings Settings(bool simulate, int simulateFrom, int simulateTo, bool sizing = true)
        {
            return new SAM.Analytical.Tas.WorkflowSettings
            {
                Simulate = simulate,
                SimulateFrom = simulateFrom,
                SimulateTo = simulateTo,
                Sizing = sizing,
            };
        }

        /// <summary>
        /// <b>Full Year Simulation ticked, days 1 to 365.</b> <c>Modify.Simulate</c> sets
        /// <c>simulate = fullYearSimulation</c> and takes the range from the two text boxes, which the dialog
        /// ships defaulted to 1 and 365. This is the only combination that may complete a Part O run, and the
        /// run does complete on it.
        /// </summary>
        [Fact]
        public void AFullYearWorkflow_MayCompleteAPreparedPartORun()
        {
            Assert.True(Query.IsPartOFullYearSimulation(Settings(true, 1, 365)));

            string path_TSD = TemporaryTsd();

            try
            {
                PartORun partORun = new();
                Assert.True(partORun.Prepare(Model("prepared"), Scenarios()));

                Assert.True(CompleteThroughAFullYearWorkflow(partORun, Model("workflow"), path_TSD, out string refusal));
                Assert.Null(refusal);

                Assert.Equal(PartORunState.WorkflowCompleted, partORun.State);
                Assert.True(partORun.CanAssess);
            }
            finally
            {
                File.Delete(path_TSD);
            }
        }

        /// <summary>
        /// <b>Full Year Simulation unticked.</b> <c>simulate</c> stays false and the range stays at its -1
        /// sentinels, so <c>WorkflowCalculator</c> sizes and returns a model without simulating a single day.
        /// The gate refuses it, and because <c>Modify.Simulate</c> arms <c>ExpectResults</c> only for a
        /// full-year run, the run cannot be completed even if <c>Complete</c> is reached anyway.
        /// </summary>
        [Fact]
        public void FullYearUnticked_CannotCompleteAPreparedPartORun()
        {
            Assert.False(Query.IsPartOFullYearSimulation(Settings(false, -1, -1)));

            string path_TSD = TemporaryTsd();

            try
            {
                PartORun partORun = new();
                partORun.Prepare(Model("prepared"), Scenarios());

                //Unarmed, exactly as this path leaves it. A model and a real results file are both present, so
                //nothing but the arming stands between this and a completed run.
                Assert.False(partORun.Complete(Model("workflow"), path_TSD, out string refusal));
                Assert.Contains("not announced as this Part O run's", refusal);

                AssertNothingToAssess(partORun);
            }
            finally
            {
                File.Delete(path_TSD);
            }
        }

        /// <summary>
        /// <b>A one-day run forced because shading changed.</b> With Full Year unticked and
        /// <c>shadingUpdated</c> true, <c>Modify.Simulate</c> turns the run into days 1 to 1 - a real
        /// simulation that returns a real model and writes a real TSD holding 24 hours. This is the case a
        /// "did the workflow return a model?" test would have blessed.
        /// </summary>
        [Fact]
        public void AShadingForcedOneDaySimulation_CannotCompleteAPreparedPartORun()
        {
            Assert.False(Query.IsPartOFullYearSimulation(Settings(true, 1, 1)));

            string path_TSD = TemporaryTsd();

            try
            {
                PartORun partORun = new();
                partORun.Prepare(Model("prepared"), Scenarios());

                //A one-day workflow really does write a TSD, so the file is rewritten here: existence and
                //freshness are both satisfied and the run is still refused, because it was never armed.
                WriteResults(path_TSD);

                Assert.False(partORun.Complete(Model("workflow"), path_TSD, out string refusal));
                Assert.Contains("not announced as this Part O run's", refusal);

                AssertNothingToAssess(partORun);
            }
            finally
            {
                File.Delete(path_TSD);
            }
        }

        /// <summary>
        /// <b>A partial date range.</b> Full Year ticked does not on its own mean 1 to 365 - the range is still
        /// read from the text boxes - so a run of days 90 to 200 reaches the gate looking ticked and is refused.
        /// Days 1 to 364 and 2 to 366 are refused for the same reason: a different year from the one the TM59
        /// criteria are defined over.
        /// </summary>
        [Fact]
        public void APartialDateRange_IsNotAFullYear()
        {
            Assert.False(Query.IsPartOFullYearSimulation(Settings(true, 90, 200)));
            Assert.False(Query.IsPartOFullYearSimulation(Settings(true, 1, 364)));
            Assert.False(Query.IsPartOFullYearSimulation(Settings(true, 2, 366)));
        }

        /// <summary>
        /// <b>Sizing only.</b> The workflow path that returns a model having simulated nothing - the one the
        /// old predicate could not tell apart from a completed annual run.
        /// </summary>
        [Fact]
        public void ASizingOnlyWorkflow_IsNotAFullYear()
        {
            Assert.False(Query.IsPartOFullYearSimulation(Settings(false, -1, -1, sizing: true)));
            Assert.False(Query.IsPartOFullYearSimulation(null));
        }

        /// <summary>
        /// A refused completion leaves nothing behind that an assessment could pick up: no model, no results
        /// path, no <c>CanAssess</c>, and a reason the ribbon can show.
        /// </summary>
        private static void AssertNothingToAssess(PartORun partORun)
        {
            Assert.Equal(PartORunState.None, partORun.State);
            Assert.False(partORun.CanAssess);
            Assert.Null(partORun.AnalyticalModel_Assessment);
            Assert.Null(partORun.Path_TSD);
            Assert.Null(partORun.AnalyticalModel_Prepared);

            Assert.False(partORun.IsAssessable(out string refusal_IsAssessable));
            Assert.False(string.IsNullOrWhiteSpace(refusal_IsAssessable));
            Assert.False(string.IsNullOrWhiteSpace(partORun.InvalidationReason));
        }

        // ------------------------------------------------------------------------------------------------
        // The results-lineage requirement. Complete must not accept an old <project>.tsd merely because it is
        // at the derived path: the workflow deletes only the TBD, so an earlier session's results survive a
        // non-simulating run.
        // ------------------------------------------------------------------------------------------------

        /// <summary>
        /// <b>An old TSD this workflow did not write.</b> The file was already at the derived path before the
        /// workflow ran and is byte-for-byte unchanged after it - so this workflow wrote nothing, and pairing
        /// those results with the model just prepared is the stale pairing the type exists to prevent.
        /// Recording the write time only after the run would have blessed it.
        /// </summary>
        [Fact]
        public void AnOldResultsFileThisWorkflowDidNotWrite_CannotCompleteTheRun()
        {
            string path_TSD = TemporaryTsd();

            try
            {
                PartORun partORun = new();
                partORun.Prepare(Model("prepared"), Scenarios());

                //Announced for a full-year run that then produced nothing: the file is untouched.
                Assert.True(partORun.ExpectResults(path_TSD));

                Assert.False(partORun.Complete(Model("workflow"), path_TSD, out string refusal));
                Assert.Contains("unchanged from before this workflow ran", refusal);

                AssertNothingToAssess(partORun);
            }
            finally
            {
                File.Delete(path_TSD);
            }
        }

        /// <summary>
        /// <b>A results file this workflow created.</b> Nothing was at the path when the run was announced, so
        /// the file existing afterwards is unambiguously this workflow's.
        /// </summary>
        [Fact]
        public void AResultsFileThisWorkflowCreated_CompletesTheRun()
        {
            string path_TSD = TemporaryTsdPath();

            Assert.False(File.Exists(path_TSD));

            try
            {
                PartORun partORun = new();
                partORun.Prepare(Model("prepared"), Scenarios());

                Assert.True(partORun.ExpectResults(path_TSD));

                WriteResults(path_TSD);

                Assert.True(partORun.Complete(Model("workflow"), path_TSD, out string refusal));
                Assert.Null(refusal);

                Assert.True(partORun.CanAssess);
                Assert.True(partORun.IsAssessable(out string _));
            }
            finally
            {
                if (File.Exists(path_TSD))
                {
                    File.Delete(path_TSD);
                }
            }
        }

        /// <summary>
        /// <b>A results file this workflow rewrote.</b> An older TSD was there and this run replaced it, which
        /// is the ordinary case on a re-simulated project. The fingerprint changed, so it completes.
        /// </summary>
        [Fact]
        public void AResultsFileThisWorkflowRewrote_CompletesTheRun()
        {
            string path_TSD = TemporaryTsd();

            try
            {
                long length_Before = new FileInfo(path_TSD).Length;

                PartORun partORun = new();
                partORun.Prepare(Model("prepared"), Scenarios());

                Assert.True(partORun.ExpectResults(path_TSD));

                WriteResults(path_TSD);

                Assert.NotEqual(length_Before, new FileInfo(path_TSD).Length);

                Assert.True(partORun.Complete(Model("workflow"), path_TSD, out string refusal));
                Assert.Null(refusal);

                Assert.True(partORun.CanAssess);
                Assert.Equal(path_TSD, partORun.Path_TSD);
            }
            finally
            {
                File.Delete(path_TSD);
            }
        }

        /// <summary>
        /// The arming is for one path. A run announced against one results file cannot be completed by another,
        /// so a changed output directory or project name between the two is not a way in.
        /// </summary>
        [Fact]
        public void ResultsAnnouncedForADifferentPath_CannotCompleteTheRun()
        {
            string path_TSD_Announced = TemporaryTsd();
            string path_TSD_Other = TemporaryTsd();

            try
            {
                PartORun partORun = new();
                partORun.Prepare(Model("prepared"), Scenarios());

                Assert.True(partORun.ExpectResults(path_TSD_Announced));

                WriteResults(path_TSD_Other);

                Assert.False(partORun.Complete(Model("workflow"), path_TSD_Other, out string refusal));
                Assert.Contains("not announced as this Part O run's", refusal);

                AssertNothingToAssess(partORun);
            }
            finally
            {
                File.Delete(path_TSD_Announced);
                File.Delete(path_TSD_Other);
            }
        }

        /// <summary>
        /// Arming is state-gated and is dropped with the run: a workflow announced to a run that has since been
        /// dropped cannot complete its successor.
        /// </summary>
        [Fact]
        public void AnnouncedResults_AreDroppedWithTheRun()
        {
            string path_TSD = TemporaryTsd();

            try
            {
                PartORun partORun = new();

                //Nothing is prepared, so there is no run to announce results to.
                Assert.False(partORun.ExpectResults(path_TSD));

                partORun.Prepare(Model("prepared"), Scenarios());
                Assert.True(partORun.ExpectResults(path_TSD));

                //An edit drops the run, and the announcement with it.
                partORun.NotifyModified();

                partORun.Prepare(Model("prepared again"), Scenarios());

                WriteResults(path_TSD);

                Assert.False(partORun.Complete(Model("workflow"), path_TSD, out string refusal));
                Assert.Contains("not announced as this Part O run's", refusal);
            }
            finally
            {
                File.Delete(path_TSD);
            }
        }
    }
}
