// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

extern alias SAMMath;

using SAM.Analytical.Tas.GenOpt;
using SAMMath::SAM.Math;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;

namespace SAM.Analytical.UI.WPF.Tests
{
    /// <summary>
    /// <see cref="TasOptimisationReport"/> and <see cref="TasOptimisationProgressState"/> over REAL SAM.Math kernel
    /// results: the kernel runs here with a delegate evaluator, so every outcome is the kernel's own, not a stand-in.
    /// The rules are SAM_Tas' NativeGenOptOutcome (PR6), shared with the SAM_Tas Grasshopper component.
    /// </summary>
    public class TasOptimisationReportTests
    {
        private static readonly IReadOnlyList<string> parameterNames = ["Setpoint"];
        private static readonly IReadOnlyList<string> objectiveNames = ["Result", "Cost"];

        private sealed class Evaluator : IObjectiveEvaluator
        {
            private readonly Func<ObjectiveEvaluationRequest, CancellationToken, ObjectiveEvaluation> func;

            public Evaluator(Func<ObjectiveEvaluationRequest, CancellationToken, ObjectiveEvaluation> func)
            {
                this.func = func;
            }

            public ObjectiveEvaluation Evaluate(ObjectiveEvaluationRequest request, CancellationToken cancellationToken)
            {
                return func(request, cancellationToken);
            }
        }

        private sealed class SynchronousProgress : IProgress<OptimisationProgress>
        {
            public List<OptimisationProgress> Reports { get; } = new List<OptimisationProgress>();

            public void Report(OptimisationProgress value)
            {
                Reports.Add(value);
            }
        }

        /// <summary>The kernel as SAM_Tas maps the algorithm, on Setpoint -5..35 starting at 10 with step 2.</summary>
        private static OptimisationResult Run(Algorithm algorithm, Func<double, double> f, OptimizationSettings optimizationSettings = null, CancellationToken cancellationToken = default, IProgress<OptimisationProgress> progress = null, Func<ObjectiveEvaluationRequest, ObjectiveEvaluation> evaluate = null)
        {
            Optimiser optimiser = Analytical.Tas.GenOpt.Convert.ToSAM_Optimiser(algorithm, optimizationSettings ?? new OptimizationSettings(), 1);
            OptimisationProblem problem = new OptimisationProblem([new OptimisationParameter("Setpoint", 10, -5, 35, 2)], 2);

            Evaluator evaluator = new Evaluator((request, token) => evaluate?.Invoke(request) ?? ObjectiveEvaluation.Success(f(request.Coordinates[0]), 2 * request.Coordinates[0]));
            return optimiser.Run(problem, evaluator, progress, cancellationToken);
        }

        private static double Quadratic(double x) => (x - 4.968943799848584) * (x - 4.968943799848584) + 7360;

        // ---- outcomes --------------------------------------------------------------------------------------

        [Fact]
        public void Golden_section_success_reports_the_lowest_entry_and_the_interval()
        {
            OptimisationResult result = Run(new GoldenSectionAlgorithm(), Quadratic);
            Assert.Equal(OptimisationOutcome.Success, result.Outcome);
            Assert.Null(result.Minimum);

            TasOptimisationReport report = new TasOptimisationReport(result, parameterNames, objectiveNames, @"C:\runs\r1", false);

            OptimisationTraceEntry lowest = result.Entries.OrderBy(x => x.Objective).First();
            Assert.True(report.Successful);
            Assert.Equal(TasOptimisationCheckStatus.Ready, report.Status);
            Assert.Equal(lowest.Coordinates, report.BestPoint);
            Assert.Equal(lowest.Outputs, report.BestObjectives);
            Assert.Equal(lowest.Simulation, report.BestSimulation);
            Assert.Same(result.Interval, report.Interval);
            Assert.Equal(result.Simulations, report.Simulations);
            Assert.Equal("Optimum found after " + result.Simulations + " simulations", report.Headline);
            Assert.Contains(report.Lines, x => x.Title == "Best design" && x.Detail.StartsWith("Setpoint = "));
            Assert.Contains(report.Lines, x => x.Title == "Objective" && x.Detail.StartsWith("Result = ") && !x.Detail.Contains("Cost = "));
            Assert.Contains(report.Lines, x => x.Title == "Recorded outputs" && x.Detail.StartsWith("Cost = ") && !x.Detail.Contains("Result = "));
            Assert.DoesNotContain(report.Lines, x => x.Title == "Best point" || x.Title == "Best objective");

            //Values stay at full precision in this PR.
            Assert.Contains(lowest.Coordinates[0].ToString("R", System.Globalization.CultureInfo.InvariantCulture), report.Lines.Single(x => x.Title == "Best design").Detail);
            Assert.Contains(report.Lines, x => x.Title == "Final interval");
            Assert.Contains(report.Lines, x => x.Title == "Run folder" && x.Detail == @"C:\runs\r1");
        }

        [Fact]
        public void Pattern_search_success_reports_the_kernels_minimum()
        {
            OptimisationResult result = Run(new GPSHookeJeevesAlgorithm(), Quadratic);
            Assert.Equal(OptimisationOutcome.Success, result.Outcome);
            Assert.NotNull(result.Minimum);

            TasOptimisationReport report = new TasOptimisationReport(result, parameterNames, objectiveNames, null, false);

            Assert.Same(result.Minimum, NativeGenOptOutcome.Best(result));
            Assert.Equal(result.Minimum.Coordinates, report.BestPoint);
            Assert.Null(report.Interval);
        }

        [Fact]
        public void The_simulation_limit_is_a_normal_end_with_a_warning()
        {
            OptimisationResult result = Run(new GPSHookeJeevesAlgorithm(), Quadratic, new OptimizationSettings() { MaxIterations = 3 });
            Assert.Equal(OptimisationOutcome.MaximumSimulationsReached, result.Outcome);

            TasOptimisationReport report = new TasOptimisationReport(result, parameterNames, objectiveNames, null, false);

            Assert.True(report.Successful);
            Assert.Equal(TasOptimisationCheckStatus.Warning, report.Status);
            Assert.NotEmpty(report.BestPoint);
            Assert.Equal("Stopped at the simulation limit (" + report.Simulations + " simulations) \u2013 best design so far", report.Headline);
        }

        [Fact]
        public void A_golden_section_nullspace_stop_is_a_normal_end_with_a_warning()
        {
            Optimiser optimiser = new GoldenSection() { MaximumSimulations = 50 };
            OptimisationResult result = optimiser.Run(new OptimisationProblem([new OptimisationParameter("Setpoint", 0, -5, 35, 1)], 1), new Evaluator((r, t) => ObjectiveEvaluation.Success(1.0)));
            Assert.Equal(OptimisationOutcome.Nullspace, result.Outcome);

            TasOptimisationReport report = new TasOptimisationReport(result, parameterNames, ["Result"], null, false);

            Assert.True(report.Successful);
            Assert.Equal(TasOptimisationCheckStatus.Warning, report.Status);
            Assert.Equal("Stopped: golden section found equal objective values (" + report.Simulations + " simulations)", report.Headline);
            Assert.Contains(report.Lines, x => x.Detail.Contains("two equal objective values"));
            Assert.DoesNotContain("nullspace", string.Join(" ", report.Lines.Select(x => x.Detail)) + report.Headline, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Nullspace", report.DiagnosticsText);
        }

        [Fact]
        public void Cancellation_reports_no_best_point()
        {
            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            OptimisationResult result = Run(new GPSHookeJeevesAlgorithm(), Quadratic, cancellationToken: cancellationTokenSource.Token, evaluate: request =>
            {
                if (request.Simulation == 2)
                {
                    cancellationTokenSource.Cancel();
                }

                return ObjectiveEvaluation.Success(Quadratic(request.Coordinates[0]), 0);
            });
            Assert.Equal(OptimisationOutcome.Cancelled, result.Outcome);

            TasOptimisationReport report = new TasOptimisationReport(result, parameterNames, objectiveNames, @"C:\runs\r2", true);

            Assert.False(report.Successful);
            Assert.Equal(TasOptimisationCheckStatus.Warning, report.Status);
            Assert.Equal("Cancelled", report.Headline);
            Assert.Empty(report.BestPoint);
            Assert.Empty(report.BestObjectives);
            Assert.Contains(report.Lines, x => x.Detail.Contains("allowed to finish"));
            Assert.Contains(report.Lines, x => x.Title == "Run folder");

            //The kernel's simulation-count bookkeeping is Diagnostics material, not part of the cancel text.
            Assert.DoesNotContain(report.Lines, x => x.Detail.Contains("kernel", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("Cancelled", report.DiagnosticsText);
            Assert.Contains("simulation count includes the number it had assigned", report.DiagnosticsText);
        }

        [Fact]
        public void A_cancel_requested_as_a_successful_run_finished_withholds_the_result()
        {
            OptimisationResult result = Run(new GoldenSectionAlgorithm(), Quadratic);
            Assert.Equal(OptimisationOutcome.Success, result.Outcome);

            TasOptimisationReport report = new TasOptimisationReport(result, parameterNames, objectiveNames, null, true);

            Assert.False(report.Successful);
            Assert.Empty(report.BestPoint);
            Assert.Equal(OptimisationOutcome.Success, report.Outcome);
            Assert.Contains(report.Lines, x => x.Detail.Contains("withheld"));
            Assert.DoesNotContain(report.Lines, x => x.Detail.Contains("kernel", StringComparison.OrdinalIgnoreCase));
            Assert.Contains("Success", report.DiagnosticsText);
        }

        [Fact]
        public void An_evaluation_failure_names_the_simulation_and_the_evaluators_message()
        {
            OptimisationResult result = Run(new GoldenSectionAlgorithm(), Quadratic, evaluate: request => ObjectiveEvaluation.Failure("TasGenExecute reported an error in Error.txt: boom"));
            Assert.Equal(OptimisationOutcome.EvaluationFailed, result.Outcome);

            TasOptimisationReport report = new TasOptimisationReport(result, parameterNames, objectiveNames, null, false);

            Assert.False(report.Successful);
            Assert.Equal(TasOptimisationCheckStatus.Blocked, report.Status);
            Assert.Equal("A Tas simulation failed (simulation " + result.FailedSimulation + ")", report.Headline);
            Assert.Contains(report.Lines, x => x.Detail.Contains("TasGenExecute reported an error in Error.txt: boom"));
            Assert.Empty(report.BestPoint);
        }

        [Fact]
        public void An_infeasible_start_point_is_an_error()
        {
            Optimiser optimiser = Analytical.Tas.GenOpt.Convert.ToSAM_Optimiser(new GPSHookeJeevesAlgorithm(), new OptimizationSettings(), 1);
            OptimisationResult result = optimiser.Run(new OptimisationProblem([new OptimisationParameter("Setpoint", 50, -5, 35, 2)], 1), new Evaluator((r, t) => ObjectiveEvaluation.Success(1.0)));
            Assert.Equal(OptimisationOutcome.InitialPointInfeasible, result.Outcome);

            TasOptimisationReport report = new TasOptimisationReport(result, parameterNames, ["Result"], null, false);

            Assert.False(report.Successful);
            Assert.Equal(TasOptimisationCheckStatus.Blocked, report.Status);
            Assert.Equal("Start values are outside the design-variable ranges", report.Headline);
        }

        // ---- the best-point rule -------------------------------------------------------------------------

        [Fact]
        public void Golden_section_best_is_the_first_lowest_entry_on_a_tie()
        {
            OptimisationResult result = Run(new GoldenSectionAlgorithm(), x => 5.0);
            Assert.Null(result.Minimum);
            Assert.True(result.Entries.Count > 1);

            Assert.Same(result.Entries[0], NativeGenOptOutcome.Best(result));
            Assert.Equal(result.Entries[0].Coordinates, new TasOptimisationReport(result, parameterNames, objectiveNames, null, false).BestPoint);
        }

        [Fact]
        public void Golden_section_best_skips_NaN_objectives()
        {
            OptimisationResult result = Run(new GoldenSectionAlgorithm(), Quadratic, evaluate: request => ObjectiveEvaluation.Success(request.Simulation == 1 ? double.NaN : Quadratic(request.Coordinates[0]), 0));

            OptimisationTraceEntry best = NativeGenOptOutcome.Best(result);
            Assert.NotNull(best);
            Assert.False(double.IsNaN(best.Objective));
            Assert.Equal(result.Entries.Where(x => !double.IsNaN(x.Objective)).Min(x => x.Objective), best.Objective);
        }

        // ---- refusals and messages -----------------------------------------------------------------------

        [Fact]
        public void A_refused_run_is_reported_with_SAM_Tas_message_and_no_result()
        {
            TasOptimisationReport report = new TasOptimisationReport(new GenOptCompatibilityException("MaxIte must be at least 1; got 0."));

            Assert.False(report.Successful);
            Assert.Null(report.Result);
            Assert.Equal(OptimisationOutcome.Undefined, report.Outcome);
            Assert.Equal(TasOptimisationCheckStatus.Blocked, report.Status);
            //A refusal outside the setup check keeps the Grasshopper component's wording.
            Assert.Equal("Invalid GenOpt settings for the native route: MaxIte must be at least 1; got 0.", Assert.Single(report.Lines).Detail);
        }

        [Fact]
        public void Messages_are_worded_as_the_Grasshopper_component_words_them()
        {
            Assert.StartsWith("Not supported by the native route: ", TasOptimisationReport.Message(new NotSupportedException("x")));
            Assert.Equal("TasGenExecute was not found. Path: 'C:\\x\\TasGenExecute.exe'.", TasOptimisationReport.Message(new FileNotFoundException("TasGenExecute was not found.", @"C:\x\TasGenExecute.exe")));
            Assert.Equal("The optimisation workspace does not exist: 'C:\\x'.", TasOptimisationReport.Message(new DirectoryNotFoundException("The optimisation workspace does not exist: 'C:\\x'.")));
            Assert.StartsWith("Native optimisation failed (InvalidOperationException): ", TasOptimisationReport.Message(new InvalidOperationException("y")));
        }

        [Fact]
        public void The_summary_text_carries_every_line()
        {
            OptimisationResult result = Run(new GoldenSectionAlgorithm(), Quadratic);
            TasOptimisationReport report = new TasOptimisationReport(result, parameterNames, objectiveNames, @"C:\runs\r1", false);

            string text = report.ToText();

            Assert.StartsWith("Design Optimisation", text);
            Assert.Contains(report.Headline, text);
            Assert.Contains("Diagnostics: ", text);
            Assert.All(report.Lines, x => Assert.Contains(x.Title + ": " + x.Detail, text));
        }

        // ---- live progress -------------------------------------------------------------------------------

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void The_live_trace_is_exactly_the_kernels_entries_and_the_lowest_so_far_is_tracked(bool goldenSection)
        {
            SynchronousProgress progress = new SynchronousProgress();
            OptimisationResult result = Run(goldenSection ? new GoldenSectionAlgorithm() : new GPSHookeJeevesAlgorithm(), Quadratic, progress: progress);

            TasOptimisationProgressState state = new TasOptimisationProgressState(parameterNames, objectiveNames);
            foreach (OptimisationProgress optimisationProgress in progress.Reports)
            {
                state.Add(optimisationProgress);
            }

            Assert.Equal(result.Entries, state.Rows.Select(x => x.Entry));
            Assert.Equal(result.Entries.Where(x => !double.IsNaN(x.Objective)).Min(x => x.Objective), state.Lowest.Objective);
            Assert.Same(result.Entries.Last(), state.Last);
            Assert.Equal(progress.Reports.Last().Simulations, state.Simulations);
            Assert.Equal(new OptimizationSettings().MaxIterations, state.MaximumSimulations);
            Assert.StartsWith("Simulation ", state.LastText());
            Assert.Equal("Simulation " + state.Simulations + " (limit " + state.MaximumSimulations + ")", state.SimulationText());
            Assert.Contains("Setpoint = ", state.LowestText());
            Assert.Contains("Result = ", state.LowestText());
        }

        [Fact]
        public void A_trace_row_shows_the_entry_as_it_is()
        {
            OptimisationResult result = Run(new GPSHookeJeevesAlgorithm(), Quadratic);
            OptimisationTraceEntry entry = result.Entries[0];

            TasOptimisationTraceRow row = new TasOptimisationTraceRow(entry);

            Assert.Equal(entry.Simulation, row.Simulation);
            Assert.Equal(entry.MainIteration + "." + entry.SubIteration, row.Iteration);
            Assert.Equal(entry.Event.ToString(), row.Event);
            Assert.Same(entry.Coordinates, row.Coordinates);
            Assert.Same(entry.Outputs, row.Outputs);
        }
    }
}
