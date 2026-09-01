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
            string result = Path.Combine(Path.GetTempPath(), string.Format("SAM_PartORunTests_{0}.tsd", Guid.NewGuid()));

            File.WriteAllText(result, "not a real tsd - only its existence and write time are read here");

            return result;
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
                Assert.True(partORun.Complete(analyticalModel_Workflow, path_TSD, out string refusal));
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
                Assert.True(partORun.Complete(analyticalModel_Workflow_1, path_TSD, out string _));

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
                Assert.True(partORun.Complete(Model("workflow"), path_TSD, out string _));
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
    }
}
