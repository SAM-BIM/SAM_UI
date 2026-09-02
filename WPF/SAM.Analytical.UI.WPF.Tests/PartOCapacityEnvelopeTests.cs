// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using SAM.Analytical.UI;
using SAM.Analytical.UI.WPF;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// <b>The Iteration 2B diagnostic capacity envelope, as the UI orchestrates and reports it.</b>
    /// <para>
    /// The ordinary optimisation stops with rooms still failing. The envelope then answers a different
    /// question - what the <i>already-selected</i> unit could deliver at its own ceiling - and the whole
    /// danger of it is that its lifecycle is indistinguishable from a round's: it is prepared, simulated
    /// over the same full year, and assessed with production TM59, and it completes. Read as a round it
    /// would be the run's best result and its newest accepted design, and it is neither.
    /// </para>
    /// <para>
    /// So these tests are almost entirely about <b>separation</b>: the round count, the last valid design,
    /// the results file identity, the stored assessment and every row of the presentation. Plus the no-run
    /// decisions, each of which is settled before any TAS work and so can be asserted here directly.
    /// </para>
    /// <para>
    /// <b>What the envelope arithmetic comes to is not tested here.</b> That belongs to
    /// <c>SAM.Analytical.Modify.EvaluateDesignAirFlowCapacityEnvelope</c> and is pinned by
    /// <c>SAM.Tests.PartOCapacityEnvelopeTests</c>; restating it in the UI would be a second copy of an
    /// engineering rule the UI does not own.
    /// </para>
    /// </summary>
    public class PartOCapacityEnvelopeTests
    {
        private const string name_Bedroom = "Bedroom";

        private const string name_Kitchen = "Kitchen";

        private static readonly Guid guid_Kitchen = new("11111111-1111-1111-1111-111111111111");

        private static readonly Guid guid_Bedroom = new("22222222-2222-2222-2222-222222222222");

        // ---- 11. The ordinary accepted design is not replaced ---------------------------------------------

        /// <summary>
        /// <b>11.</b> A <b>completed</b> capacity envelope step does not become the run's last valid design.
        /// <para>
        /// This is the single most important separation here. An envelope is prepared, simulated and
        /// assessed exactly as a round is, and it sets <c>IsCompleted</c> - so a "last step that completed"
        /// which did not ask <i>what kind</i> of step it was would answer with the diagnostic, and the
        /// command would then replace the user's model with a design the optimiser's own all-or-nothing
        /// policy refuses.
        /// </para>
        /// </summary>
        [Fact]
        public void ACompletedCapacityEnvelope_IsNotTheRunsLastValidDesign()
        {
            PartOOptimisationRun partOOptimisationRun = History(out AnalyticalModel analyticalModel_LastValid);

            PartOOptimisationStep partOOptimisationStep = Envelope(partOOptimisationRun);

            Assert.True(partOOptimisationStep.IsCompleted);

            //Still the ordinary round, by identity - not merely by iteration number.
            Assert.Same(partOOptimisationRun.Steps[1], partOOptimisationRun.Step_LastValid);
            Assert.Equal(1, partOOptimisationRun.Step_LastValid.Iteration);
            Assert.True(partOOptimisationRun.Step_LastValid.IsOptimisationRound);

            Assert.Same(analyticalModel_LastValid, partOOptimisationRun.AnalyticalModel_LastValid);
            Assert.NotSame(partOOptimisationRun.AnalyticalModel_LastValid, partOOptimisationRun.AnalyticalModel_CapacityEnvelope);
        }

        /// <summary>
        /// <b>11, and the round count.</b> The envelope is not a round and is not counted as one. Counting
        /// it would have the run report one more successful step at the configured airflow than it took -
        /// and the envelope's step is precisely not that.
        /// </summary>
        [Fact]
        public void ACapacityEnvelope_IsNotCountedAsAnOptimisationRound()
        {
            PartOOptimisationRun partOOptimisationRun = History(out AnalyticalModel _);

            Assert.Equal(1, partOOptimisationRun.Rounds);

            Envelope(partOOptimisationRun);

            //Three steps, still one round.
            Assert.Equal(3, partOOptimisationRun.Steps.Count);
            Assert.Equal(1, partOOptimisationRun.Rounds);

            Assert.Contains("1 optimisation round(s)", partOOptimisationRun.Description);
        }

        /// <summary>
        /// <b>11, in the run's own words.</b> The envelope is described separately and labelled a
        /// diagnostic, and the optimisation's own stop reason and answer are unchanged by its presence.
        /// </summary>
        [Fact]
        public void TheDescription_KeepsTheEnvelopeApartFromTheOptimisationsAnswer()
        {
            PartOOptimisationRun partOOptimisationRun = History(out AnalyticalModel _);

            partOOptimisationRun.StopReason = PartOOptimisationStopReason.CapacityReached;

            string description_Before = partOOptimisationRun.Description;

            Envelope(partOOptimisationRun);

            //The optimisation's own account is still there, word for word, with the envelope appended as its
            //own labelled clause rather than folded into it.
            Assert.Contains("cannot carry another full", partOOptimisationRun.Description);
            Assert.Contains("CAPACITY ENVELOPE:", partOOptimisationRun.Description);
            Assert.DoesNotContain("CAPACITY ENVELOPE:", description_Before);

            Assert.Equal(PartOOptimisationStopReason.CapacityReached, partOOptimisationRun.StopReason);
            Assert.False(partOOptimisationRun.IsPassed);
        }

        // ---- 15. The envelope's production TM59 is stored as the envelope's ------------------------------

        /// <summary>
        /// <b>15.</b> The envelope's production TM59 result is kept on the envelope's own step, beside its
        /// own results file - never merged into the last valid design's. The two designs are different, so
        /// their verdicts are different facts.
        /// </summary>
        [Fact]
        public void TheEnvelopesProductionTM59Result_IsStoredSeparately()
        {
            PartOOptimisationRun partOOptimisationRun = History(out AnalyticalModel _);

            PartOOptimisationStep partOOptimisationStep_LastValid = partOOptimisationRun.Step_LastValid;

            PartOOptimisationStep partOOptimisationStep = Envelope(partOOptimisationRun);

            Assert.Same(partOOptimisationStep, partOOptimisationRun.Step_CapacityEnvelope);
            Assert.True(partOOptimisationRun.HasCapacityEnvelope);

            //The envelope passed where the last accepted design failed - which is the whole point of
            //calculating it, and would be unreadable if the two verdicts shared a slot.
            Assert.Equal(TM59ComplianceStatus.Fail, partOOptimisationStep_LastValid.OccupiedSpaceComplianceStatus);
            Assert.Equal(TM59ComplianceStatus.Pass, partOOptimisationStep.OccupiedSpaceComplianceStatus);

            Assert.NotEmpty(partOOptimisationStep.TM59Results);
            Assert.NotEqual(partOOptimisationStep_LastValid.Path_TSD, partOOptimisationStep.Path_TSD);

            Assert.Equal(partOOptimisationStep.Path_TSD, partOOptimisationRun.Path_TSD_CapacityEnvelope);

            //And the optimisation's answer is still a failure. An envelope that passes does not make the
            //run a pass: nobody accepted that design.
            Assert.False(partOOptimisationRun.IsPassed);
        }

        /// <summary>
        /// The envelope's results file is its own <c>-OptMax</c> identity, so it overwrites no round's
        /// evidence - and, read back, that name is <b>not</b> an iteration number, so a later optimisation
        /// could not renumber from it and land on a round's files.
        /// </summary>
        [Fact]
        public void TheEnvelope_HasItsOwnOptMaxIdentityThatIsNotAnIterationNumber()
        {
            PartOSimulationContext partOSimulationContext = new(Path.GetTempPath(), "Fixture", null, SolarCalculationMethod.SAM, 1, 365);

            Assert.Equal("Fixture-OptMax", partOSimulationContext.ProjectName_CapacityEnvelope());

            //Distinct from every round's name, whatever the round.
            for (int i = 0; i <= 20; i++)
            {
                Assert.NotEqual(partOSimulationContext.ProjectName_Iteration(i), partOSimulationContext.ProjectName_CapacityEnvelope());
            }

            //And it reads back as no iteration at all, rather than as some number a round also uses.
            Assert.Equal(0, PartOSimulationContext.Iteration_ProjectName(partOSimulationContext.ProjectName_CapacityEnvelope()));
        }

        // ---- Presentation: the three stages are told apart ------------------------------------------------

        /// <summary>
        /// Baseline, optimisation round and capacity envelope are three different things, and every row of
        /// both histories says which it is. The envelope's run label is <c>MAX</c> rather than a number,
        /// because a number would place it in the rounds' sequence, where the last one is the answer.
        /// </summary>
        [Fact]
        public void TheHistory_DistinguishesBaselineFromOptimisationFromCapacityEnvelope()
        {
            PartOOptimisationRun partOOptimisationRun = History(out AnalyticalModel _);

            Envelope(partOOptimisationRun);

            List<PartOOptimisationAirFlowRow> rows = PartOOptimisationAirFlowRow.Rows(partOOptimisationRun);

            Assert.Contains(rows, x => x.Stage == "BASELINE" && x.Run == "0");
            Assert.Contains(rows, x => x.Stage == "OPTIMISATION" && x.Run == "1");
            Assert.Contains(rows, x => x.Stage == "CAPACITY ENVELOPE" && x.Run == "MAX");

            //TARGETED and DERIVED are still told apart WITHIN the envelope - a diagnostic still has to say
            //which rooms were chosen and which only moved to keep the dwelling balanced.
            Assert.Contains(rows, x => x.Stage == "CAPACITY ENVELOPE" && x.Type == "TARGETED" && x.Space == name_Kitchen);
            Assert.Contains(rows, x => x.Stage == "CAPACITY ENVELOPE" && x.Type == "DERIVED" && x.Space == name_Bedroom);

            List<PartOOptimisationUnitRow> rows_Unit = PartOOptimisationUnitRow.Rows(partOOptimisationRun);

            Assert.Contains(rows_Unit, x => x.Stage == "CAPACITY ENVELOPE" && x.Run == "MAX");

            //Never reselected, on the envelope's row as much as on a round's: the envelope's whole subject
            //is what the CURRENT product can deliver.
            Assert.All(rows_Unit, x => Assert.DoesNotContain("Reselect", x.Equipment));
            Assert.All(rows_Unit, x => Assert.Contains("MVHR-150", x.Product));
        }

        /// <summary>
        /// A room whose first ever movement is in the capacity envelope must not contribute a synthesised
        /// <c>BASELINE</c> row.
        /// <para>
        /// The baseline block works by reading the <c>Before_Lps</c> of each room's first adjustment, which
        /// is a recorded fact for an ordinary round - the round read it off the design it was given. The
        /// envelope's <c>Before_Lps</c> is the <b>last accepted</b> design's airflow, several rounds along,
        /// so treating it the same way would print a run-0 row stating a figure the baseline never carried.
        /// </para>
        /// </summary>
        [Fact]
        public void ARoomThatOnlyTheEnvelopeMoves_ContributesNoSynthesisedBaselineRow()
        {
            PartOOptimisationRun partOOptimisationRun = History(out AnalyticalModel _);

            PartOOptimisationStep partOOptimisationStep = Envelope(partOOptimisationRun);

            //A room nothing before the envelope ever touched, whose "before" is the last accepted design's
            //63 l/s and certainly not the baseline's.
            partOOptimisationStep.TargetedAdjustments.Add(new DesignAirFlowAdjustment(new Guid("44444444-4444-4444-4444-444444444444"), "Ensuite", FlowClassification.Extract, 63, 70, 8, false));

            List<PartOOptimisationAirFlowRow> rows = PartOOptimisationAirFlowRow.Rows(partOOptimisationRun);

            Assert.Contains(rows, x => x.Space == "Ensuite" && x.Stage == "CAPACITY ENVELOPE");
            Assert.DoesNotContain(rows, x => x.Space == "Ensuite" && x.Stage == "BASELINE");
        }

        // ---- The no-run cases, each stated -----------------------------------------------------------------

        /// <summary>
        /// Not asked for means not calculated - and said so, rather than left blank. An optional diagnostic
        /// that silently produces nothing leaves a reader unable to tell it was considered at all.
        /// </summary>
        [Fact]
        public void AnEnvelopeThatWasNotAskedFor_IsNotCalculatedAndSaysSo()
        {
            PartOOptimisationRun partOOptimisationRun = Failing(PartOOptimisationStopReason.CapacityReached, out PartOPreparationContext partOPreparationContext, out PartOSimulationContext partOSimulationContext);

            Modify.CapacityEnvelope(partOOptimisationRun, new PartOOptimisationSettings { CapacityEnvelope = false }, partOPreparationContext, partOSimulationContext);

            Assert.Null(partOOptimisationRun.Step_CapacityEnvelope);
            Assert.Null(partOOptimisationRun.CapacityEnvelope);
            Assert.False(partOOptimisationRun.HasCapacityEnvelope);

            Assert.Contains("was not asked for", partOOptimisationRun.CapacityEnvelopeDescription);
        }

        /// <summary>
        /// A run that <b>passed</b> has nothing to diagnose, so no envelope is calculated and no TAS time is
        /// spent discovering that.
        /// </summary>
        [Fact]
        public void ARunThatPassed_HasNoEnvelopeToCalculate()
        {
            PartOOptimisationRun partOOptimisationRun = Failing(PartOOptimisationStopReason.Passed, out PartOPreparationContext partOPreparationContext, out PartOSimulationContext partOSimulationContext);

            Modify.CapacityEnvelope(partOOptimisationRun, new PartOOptimisationSettings(), partOPreparationContext, partOSimulationContext);

            Assert.Null(partOOptimisationRun.Step_CapacityEnvelope);
            Assert.Contains("nothing for a capacity envelope to diagnose", partOOptimisationRun.CapacityEnvelopeDescription);
        }

        /// <summary>
        /// The envelope answers a stop on the selected unit's capacity or on the iteration guard. Every
        /// other stop is a run that did not finish rather than a design limited by its equipment, and
        /// enveloping from one would diagnose a design whose own optimisation never established what it
        /// could do.
        /// </summary>
        [Theory]
        [InlineData(PartOOptimisationStopReason.CapacityReached, true)]
        [InlineData(PartOOptimisationStopReason.IterationLimitReached, true)]
        [InlineData(PartOOptimisationStopReason.NoEligibleTargets, false)]
        [InlineData(PartOOptimisationStopReason.RebalanceRefused, false)]
        [InlineData(PartOOptimisationStopReason.PreparationFailed, false)]
        [InlineData(PartOOptimisationStopReason.SimulationFailed, false)]
        [InlineData(PartOOptimisationStopReason.AssessmentFailed, false)]
        [InlineData(PartOOptimisationStopReason.Cancelled, false)]
        public void OnlyACapacityOrIterationLimitStop_IsEnvelopped(PartOOptimisationStopReason partOOptimisationStopReason, bool considered)
        {
            //Deliberately a design already sitting on its unit's rating, so no stop reason can reach a
            //simulation and the only thing that varies is whether the stop reason was considered at all.
            PartOOptimisationRun partOOptimisationRun = Failing(partOOptimisationStopReason, out PartOPreparationContext partOPreparationContext, out PartOSimulationContext partOSimulationContext, 30);

            Modify.CapacityEnvelope(partOOptimisationRun, new PartOOptimisationSettings(), partOPreparationContext, partOSimulationContext);

            Assert.Null(partOOptimisationRun.Step_CapacityEnvelope);
            Assert.NotNull(partOOptimisationRun.CapacityEnvelopeDescription);

            Assert.Equal(considered, !partOOptimisationRun.CapacityEnvelopeDescription.Contains("a run that did not finish"));

            //A stop the envelope does not answer is refused BEFORE the equipment is even looked at, so no
            //envelope was calculated at all - not merely one that found nothing.
            Assert.Equal(considered, partOOptimisationRun.CapacityEnvelope is not null);
        }

        /// <summary>
        /// "Not Pass" is not "Fail". An envelope calculated from an assessment that reached no verdict would
        /// scale a design towards its equipment's ceiling on the strength of results that said nothing about
        /// any of its rooms - the same rule the optimisation itself applies to its own stopping.
        /// </summary>
        [Fact]
        public void ALastValidDesignWithNoFailingVerdict_IsNotEnvelopped()
        {
            PartOOptimisationRun partOOptimisationRun = Failing(PartOOptimisationStopReason.IterationLimitReached, out PartOPreparationContext partOPreparationContext, out PartOSimulationContext partOSimulationContext);

            partOOptimisationRun.Steps[0].OccupiedSpaceComplianceStatus = TM59ComplianceStatus.Undefined;

            Modify.CapacityEnvelope(partOOptimisationRun, new PartOOptimisationSettings(), partOPreparationContext, partOSimulationContext);

            Assert.Null(partOOptimisationRun.Step_CapacityEnvelope);
            Assert.Contains("which is not a failure", partOOptimisationRun.CapacityEnvelopeDescription);
        }

        /// <summary>
        /// <b>13.</b> Nothing eligible left to target means no envelope <b>and no simulation</b> - a
        /// diagnostic with nothing to say must not cost a full-year TAS run to say it. The reason each
        /// failing room could not be targeted goes on the design it was learned about.
        /// </summary>
        [Fact]
        public void NoEligibleTarget_ProducesNoEnvelopeAndNoSimulation()
        {
            PartOOptimisationRun partOOptimisationRun = Failing(PartOOptimisationStopReason.CapacityReached, out PartOPreparationContext partOPreparationContext, out PartOSimulationContext partOSimulationContext);

            //Every failing room is outside the Part O dwelling scope, so the +5 policy can target none of
            //them - and the envelope scales exactly what that policy would have asked for.
            partOOptimisationRun.Steps[0].TM59Results.Clear();
            partOOptimisationRun.Steps[0].TM59Results.Add(new PartOTM59SpaceResult(Guid.NewGuid(), "Somewhere else", ">26 C hours", 300, 142, TM59ComplianceStatus.Fail, true));

            Modify.CapacityEnvelope(partOOptimisationRun, new PartOOptimisationSettings(), partOPreparationContext, partOSimulationContext);

            Assert.Null(partOOptimisationRun.Step_CapacityEnvelope);
            Assert.Null(partOOptimisationRun.CapacityEnvelope);

            Assert.Contains("no deliberate target vector", partOOptimisationRun.CapacityEnvelopeDescription);

            //No step was appended at all, so nothing appears in the history or the round count that never
            //happened.
            Assert.Single(partOOptimisationRun.Steps);
        }

        /// <summary>
        /// <b>12.</b> A passing room is never a deliberate target of the envelope either. The envelope
        /// scales the vector the <i>ordinary</i> policy would have asked for next, and that policy targets
        /// only rooms the production assessment failed - so a design in which every room passes yields no
        /// vector, however much headroom the equipment has.
        /// </summary>
        [Fact]
        public void PassingRooms_AreNotTurnedIntoDeliberateEnvelopeTargets()
        {
            PartOOptimisationRun partOOptimisationRun = Failing(PartOOptimisationStopReason.IterationLimitReached, out PartOPreparationContext partOPreparationContext, out PartOSimulationContext partOSimulationContext);

            //The run's own status still says Fail - so the guard above it is not what refuses this - while
            //every recorded room result is a pass.
            partOOptimisationRun.Steps[0].TM59Results.Clear();

            foreach (Space space in partOOptimisationRun.AnalyticalModel_LastValid.AdjacencyCluster.GetSpaces() ?? [])
            {
                partOOptimisationRun.Steps[0].TM59Results.Add(new PartOTM59SpaceResult(space.Guid, space.Name, ">26 C hours", 100, 142, TM59ComplianceStatus.Pass, true));
            }

            Modify.CapacityEnvelope(partOOptimisationRun, new PartOOptimisationSettings(), partOPreparationContext, partOSimulationContext);

            Assert.Null(partOOptimisationRun.Step_CapacityEnvelope);
            Assert.Contains("no deliberate target vector", partOOptimisationRun.CapacityEnvelopeDescription);
        }

        /// <summary>
        /// <b>14.</b> A design whose equipment has nothing left to give produces no envelope model, and the
        /// reason is recorded rather than merely implied - that reason <i>is</i> the diagnostic. Still no
        /// simulation: there is no design to simulate.
        /// </summary>
        [Fact]
        public void NoUsefulHeadroom_ProducesAnExplicitReasonAndNoSimulation()
        {
            PartOOptimisationRun partOOptimisationRun = Failing(PartOOptimisationStopReason.CapacityReached, out PartOPreparationContext partOPreparationContext, out PartOSimulationContext partOSimulationContext, 30);

            Modify.CapacityEnvelope(partOOptimisationRun, new PartOOptimisationSettings(), partOPreparationContext, partOSimulationContext);

            Assert.Null(partOOptimisationRun.Step_CapacityEnvelope);
            Assert.False(partOOptimisationRun.HasCapacityEnvelope);

            Assert.NotNull(partOOptimisationRun.CapacityEnvelope);
            Assert.Equal(DesignAirFlowCapacityEnvelopeOutcome.NoHeadroom, partOOptimisationRun.CapacityEnvelope.Outcome);

            Assert.Contains("no useful headroom", partOOptimisationRun.CapacityEnvelopeDescription);

            //Nothing was appended to the history, so nothing that did not happen is in the round count.
            Assert.Single(partOOptimisationRun.Steps);
        }

        /// <summary>
        /// The settings default the diagnostic on, because the case it answers - a run stopping short of a
        /// pass - is exactly the case in which the run on its own does not tell an engineer what to do next.
        /// </summary>
        [Fact]
        public void TheCapacityEnvelope_IsOnByDefault()
        {
            Assert.True(new PartOOptimisationSettings().CapacityEnvelope);

            //And it is not one of the things that can make the settings unusable: it is a diagnostic
            //switch, not an airflow.
            Assert.True(new PartOOptimisationSettings { CapacityEnvelope = false }.IsValid(out string _));
        }

        // ---- Fixture ---------------------------------------------------------------------------------------

        /// <summary>
        /// A baseline and one completed ordinary round, built directly - so what the run and the
        /// presentation do with a given history can be stated without a TAS simulation behind it.
        /// </summary>
        private static PartOOptimisationRun History(out AnalyticalModel analyticalModel_LastValid)
        {
            VentilationUnitCapacityDescriptor ventilationUnitCapacityDescriptor = new(new VentilationUnitReference("Test Fixture", "MVHR-150", null), 150, 150, 0);

            PartOOptimisationRun result = new(new PartOOptimisationSettings());

            PartOOptimisationStep partOOptimisationStep_Baseline = new(0)
            {
                ProjectName = "Fixture-Opt00",
                Path_TSD = Path.Combine(Path.GetTempPath(), "Fixture-Opt00.tsd"),
                WeatherData = "CIBSE Future Z1",
                OccupiedSpaceComplianceStatus = TM59ComplianceStatus.Fail,
                IsCompleted = true,
            };

            partOOptimisationStep_Baseline.UnitStates.Add(new PartOOptimisationUnitState("MVHR-01", "Flat 1", 30, 30, ventilationUnitCapacityDescriptor.VentilationUnitReference, ventilationUnitCapacityDescriptor, VentilationUnitSelectionOutcome.Kept, null));

            result.Steps.Add(partOOptimisationStep_Baseline);

            PartOOptimisationStep partOOptimisationStep_Round = new(1)
            {
                ProjectName = "Fixture-Opt01",
                Path_TSD = Path.Combine(Path.GetTempPath(), "Fixture-Opt01.tsd"),
                WeatherData = "CIBSE Future Z1",
                OccupiedSpaceComplianceStatus = TM59ComplianceStatus.Fail,
                IsCompleted = true,
            };

            partOOptimisationStep_Round.TargetedAdjustments.Add(new DesignAirFlowAdjustment(guid_Kitchen, name_Kitchen, FlowClassification.Extract, 22, 27, 13, false));
            partOOptimisationStep_Round.DerivedAdjustments.Add(new DesignAirFlowAdjustment(guid_Bedroom, name_Bedroom, FlowClassification.Supply, 30, 35, 13, true));

            partOOptimisationStep_Round.TM59Results.Add(new PartOTM59SpaceResult(guid_Kitchen, name_Kitchen, ">26 C hours", 300, 142, TM59ComplianceStatus.Fail, true));

            partOOptimisationStep_Round.UnitStates.Add(new PartOOptimisationUnitState("MVHR-01", "Flat 1", 35, 35, ventilationUnitCapacityDescriptor.VentilationUnitReference, ventilationUnitCapacityDescriptor, VentilationUnitSelectionOutcome.Kept, null));

            result.Steps.Add(partOOptimisationStep_Round);

            analyticalModel_LastValid = new AnalyticalModel("last valid", null, null, null, new AdjacencyCluster(), null, null);

            result.AnalyticalModel_LastValid = analyticalModel_LastValid;
            result.Path_TSD_LastValid = partOOptimisationStep_Round.Path_TSD;
            result.StopReason = PartOOptimisationStopReason.CapacityReached;

            return result;
        }

        /// <summary>
        /// A completed capacity envelope step appended to a history - the state whose separation from the
        /// ordinary answer every test above is about.
        /// </summary>
        private static PartOOptimisationStep Envelope(PartOOptimisationRun partOOptimisationRun)
        {
            VentilationUnitCapacityDescriptor ventilationUnitCapacityDescriptor = new(new VentilationUnitReference("Test Fixture", "MVHR-150", null), 150, 150, 0);

            PartOOptimisationStep result = new(2, PartOOptimisationStepKind.CapacityEnvelope)
            {
                ProjectName = "Fixture-OptMax",
                Path_TSD = Path.Combine(Path.GetTempPath(), "Fixture-OptMax.tsd"),
                WeatherData = "CIBSE Future Z1",
                OccupiedSpaceComplianceStatus = TM59ComplianceStatus.Pass,
                IsCompleted = true,
            };

            //The scaled vector: the kitchen taken from 27 to 63 and the balancing supply following it.
            result.TargetedAdjustments.Add(new DesignAirFlowAdjustment(guid_Kitchen, name_Kitchen, FlowClassification.Extract, 27, 63, 13, false));
            result.DerivedAdjustments.Add(new DesignAirFlowAdjustment(guid_Bedroom, name_Bedroom, FlowClassification.Supply, 35, 71, 13, true));

            result.TM59Results.Add(new PartOTM59SpaceResult(guid_Kitchen, name_Kitchen, ">26 C hours", 100, 142, TM59ComplianceStatus.Pass, true));

            result.UnitStates.Add(new PartOOptimisationUnitState("MVHR-01", "Flat 1", 150, 150, ventilationUnitCapacityDescriptor.VentilationUnitReference, ventilationUnitCapacityDescriptor, VentilationUnitSelectionOutcome.Kept, null));

            partOOptimisationRun.Steps.Add(result);

            partOOptimisationRun.AnalyticalModel_CapacityEnvelope = new AnalyticalModel("capacity envelope", null, null, null, new AdjacencyCluster(), null, null);
            partOOptimisationRun.Path_TSD_CapacityEnvelope = result.Path_TSD;
            partOOptimisationRun.CapacityEnvelopeDescription = "DIAGNOSTIC ONLY - this is not an optimisation round and the design the optimisation accepted is unchanged.";

            return result;
        }

        /// <summary>
        /// A run that stopped with one failing room, over a real dwelling model whose serving unit is
        /// selected as a product rated at <paramref name="maximum_Lps"/> - so the envelope's own no-run
        /// decisions can be reached and asserted without any TAS work.
        /// </summary>
        private static PartOOptimisationRun Failing(PartOOptimisationStopReason partOOptimisationStopReason, out PartOPreparationContext partOPreparationContext, out PartOSimulationContext partOSimulationContext, double maximum_Lps = 150)
        {
            List<VentilationUnitCapacityDescriptor> ventilationUnitCapacityDescriptors =
            [
                new VentilationUnitCapacityDescriptor(new VentilationUnitReference("Test Fixture", "MVHR", null), maximum_Lps, maximum_Lps, 0),
            ];

            AdjacencyCluster adjacencyCluster = new();

            Zone zone = new("Flat 1");

            adjacencyCluster.AddObject(zone);

            AirHandlingUnit airHandlingUnit = new("MVHR-01", 20, 20);

            airHandlingUnit.SetValue(AirHandlingUnitParameter.VentilationUnitReference, ventilationUnitCapacityDescriptors[0].VentilationUnitReference);

            adjacencyCluster.AddObject(airHandlingUnit);

            VentilationSystem ventilationSystem = new("Flat 1", new VentilationSystemType("Fixture MVHR", "Fixture"));
            ventilationSystem.SetValue(VentilationSystemParameter.SupplyUnitName, airHandlingUnit.Name);

            adjacencyCluster.AddObject(ventilationSystem);

            Space space_Bedroom = Room(adjacencyCluster, ventilationSystem, name_Bedroom, PartFTerminalRole.Supply, FlowClassification.Supply, 13, 30);
            Space space_Kitchen = Room(adjacencyCluster, ventilationSystem, name_Kitchen, PartFTerminalRole.LocalKitchenExtract, FlowClassification.Extract, 13, 30);

            adjacencyCluster.AddRelation(zone, space_Bedroom);
            adjacencyCluster.AddRelation(zone, space_Kitchen);

            AnalyticalModel analyticalModel = new("Fixture", null, null, null, adjacencyCluster, null, null);

            partOPreparationContext = new PartOPreparationContext(PartOIteration.BasePassive, [zone], [], ventilationUnitCapacityDescriptors);
            partOSimulationContext = new PartOSimulationContext(Path.GetTempPath(), "Fixture", null, SolarCalculationMethod.SAM, 1, 365);

            PartOOptimisationRun result = new(new PartOOptimisationSettings());

            PartOOptimisationStep partOOptimisationStep = new(0)
            {
                ProjectName = "Fixture-Opt00",
                Path_TSD = Path.Combine(Path.GetTempPath(), "Fixture-Opt00.tsd"),
                OccupiedSpaceComplianceStatus = TM59ComplianceStatus.Fail,
                IsCompleted = true,
            };

            partOOptimisationStep.TM59Results.Add(new PartOTM59SpaceResult(space_Kitchen.Guid, space_Kitchen.Name, ">26 C hours", 300, 142, TM59ComplianceStatus.Fail, true));

            result.Steps.Add(partOOptimisationStep);

            result.AnalyticalModel_LastValid = analyticalModel;
            result.Path_TSD_LastValid = partOOptimisationStep.Path_TSD;
            result.StopReason = partOOptimisationStopReason;

            return result;
        }

        private static Space Room(AdjacencyCluster adjacencyCluster, VentilationSystem ventilationSystem, string name, PartFTerminalRole partFTerminalRole, FlowClassification flowClassification, double requirement_Lps, double designFlowRate_Lps)
        {
            Space result = new(name);

            PartFVentilationTerminalRequirement partFVentilationTerminalRequirement = new(name + " requirement", result.Guid, partFTerminalRole)
            {
                ContinuousDesignFlowRate_Lps = requirement_Lps,
            };

            PartFSpaceData partFSpaceData = new();
            partFSpaceData.Terminals.Add(partFVentilationTerminalRequirement);

            result.SetValue(SpaceParameter.PartFSpaceData, partFSpaceData);

            adjacencyCluster.AddObject(result);

            VentilationTerminal ventilationTerminal = new(name + " terminal", flowClassification, designFlowRate_Lps);
            ventilationTerminal.SetValue(VentilationTerminalParameter.PartFTerminalReference, new PartFTerminalReference(partFVentilationTerminalRequirement));

            adjacencyCluster.AddObject(ventilationTerminal);
            adjacencyCluster.AddRelation(ventilationTerminal, result);
            adjacencyCluster.AddRelation(ventilationTerminal, ventilationSystem);

            adjacencyCluster.AddRelation(ventilationSystem, result);

            return result;
        }
    }
}
