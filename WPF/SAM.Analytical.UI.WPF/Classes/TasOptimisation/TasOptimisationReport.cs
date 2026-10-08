// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

extern alias SAMMath;

using SAM.Analytical.Tas.GenOpt;
using SAMMath::SAM.Math;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// How one Simulate &gt; Optimisation run ended, read from the SAM.Math kernel's <see cref="OptimisationResult"/> and
    /// nothing else (no GenOpt output file is read). It is presentation only: the headline, status and lines. The rules
    /// are SAM_Tas' <see cref="NativeGenOptOutcome"/> (PR6), shared with the Grasshopper GenOpt component, so a run
    /// reads the same in both places:
    /// <list type="bullet">
    /// <item>Success, the simulation limit and a golden-section nullspace stop are normal ends and report a best point
    /// (the last two with a warning). Cancellation, an evaluation failure and the other outcomes report none.</item>
    /// <item>A run the user asked to stop is never reported as successful, even if the kernel finished first: the
    /// result is withheld.</item>
    /// <item>The best point is the kernel's reported minimum. Golden section reports none, so its best point is the
    /// lowest objective among all entries, the first one on a tie, NaN skipped (the PR3 acceptance definition).</item>
    /// </list>
    /// </summary>
    public sealed class TasOptimisationReport
    {
        private static readonly IReadOnlyList<double> none = Array.AsReadOnly(new double[0]);

        private readonly List<TasOptimisationCheck> lines = new List<TasOptimisationCheck>();

        /// <summary>The run was refused or failed outside the kernel: nothing was evaluated.</summary>
        public TasOptimisationReport(Exception exception)
        {
            Outcome = OptimisationOutcome.Undefined;
            ParameterNames = new List<string>();
            ObjectiveNames = new List<string>();
            BestPoint = none;
            BestObjectives = none;
            Status = TasOptimisationCheckStatus.Blocked;
            Headline = "The optimisation did not start";
            lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, "Error", Message(exception)));
        }

        public TasOptimisationReport(NativeGenOptRun nativeGenOptRun, bool cancelRequested)
            : this(nativeGenOptRun?.Result!, nativeGenOptRun?.ParameterNames, nativeGenOptRun?.ObjectiveNames, nativeGenOptRun?.Workspace?.RunDirectory, cancelRequested)
        {
        }

        /// <param name="result">The kernel result.</param>
        /// <param name="parameterNames">Parameter names in coordinate order.</param>
        /// <param name="objectiveNames">Objective names in output order; the first is the one minimised.</param>
        /// <param name="runDirectory">The run folder (project snapshot and evaluation folders).</param>
        /// <param name="cancelRequested">The user asked to stop before the run returned.</param>
        public TasOptimisationReport(OptimisationResult result, IReadOnlyList<string>? parameterNames, IReadOnlyList<string>? objectiveNames, string? runDirectory, bool cancelRequested)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            NativeGenOptOutcome nativeGenOptOutcome = new NativeGenOptOutcome(result, cancelRequested);

            Result = result;
            Outcome = result.Outcome;
            Successful = nativeGenOptOutcome.Successful;
            Simulations = result.Simulations;
            Retries = result.Retries;
            FailedSimulation = result.FailedSimulation;
            FailureMessage = result.FailureMessage;
            ParameterNames = parameterNames?.ToList() ?? new List<string>();
            ObjectiveNames = objectiveNames?.ToList() ?? new List<string>();
            RunDirectory = runDirectory;
            BestPoint = none;
            BestObjectives = none;

            switch (result.Outcome)
            {
                case OptimisationOutcome.Success:
                    Status = TasOptimisationCheckStatus.Ready;
                    Headline = string.Format(CultureInfo.InvariantCulture, "Finished: Success after {0} simulations", result.Simulations);
                    break;

                case OptimisationOutcome.MaximumSimulationsReached:
                    Status = TasOptimisationCheckStatus.Warning;
                    Headline = string.Format(CultureInfo.InvariantCulture, "Stopped at the simulation limit ({0} simulations)", result.Simulations);
                    lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Warning, "Outcome", "The simulation limit was reached before the stopping criterion was met; the lowest point found is reported."));
                    break;

                case OptimisationOutcome.Nullspace:
                    Status = TasOptimisationCheckStatus.Warning;
                    Headline = string.Format(CultureInfo.InvariantCulture, "Stopped on equal objective values after {0} simulations", result.Simulations);
                    lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Warning, "Outcome", "Golden section stopped on two consecutive equal objective values (nullspace); the lowest point found is reported."));
                    break;

                case OptimisationOutcome.Cancelled:
                    Status = TasOptimisationCheckStatus.Warning;
                    Headline = "Cancelled";
                    lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Warning, "Outcome", string.Format(CultureInfo.InvariantCulture, "Cancelled by user. The running Tas evaluation was allowed to finish and no further one was started. No best point is reported; the completed evaluations remain in the run folder. (The kernel's simulation count, {0}, includes the number it had assigned when the cancel was observed.)", result.Simulations)));
                    break;

                case OptimisationOutcome.EvaluationFailed:
                    Status = TasOptimisationCheckStatus.Blocked;
                    Headline = string.Format(CultureInfo.InvariantCulture, "A Tas evaluation failed (simulation {0})", result.FailedSimulation);
                    lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, "Evaluation", string.Format(CultureInfo.InvariantCulture, "Tas evaluation failed at simulation {0}: {1}", result.FailedSimulation, result.FailureMessage)));
                    break;

                case OptimisationOutcome.InitialPointInfeasible:
                    Status = TasOptimisationCheckStatus.Blocked;
                    Headline = "The start point is outside the bounds";
                    lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, "Outcome", "The initial point is outside the parameter bounds."));
                    break;

                default:
                    Status = TasOptimisationCheckStatus.Blocked;
                    Headline = "The optimisation stopped with an error";
                    lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, "Outcome", "The optimisation stopped with an error: " + result.Outcome + "."));
                    break;
            }

            if (nativeGenOptOutcome.Withheld)
            {
                Status = TasOptimisationCheckStatus.Warning;
                Headline = "Cancelled as the run finished";
                lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Warning, "Outcome", "Cancelled by user as the run finished (kernel outcome: " + result.Outcome + "). The result is withheld; the evaluation folders remain in the run folder."));
            }

            if (Successful)
            {
                OptimisationTraceEntry? best = nativeGenOptOutcome.BestEntry;
                if (best != null)
                {
                    BestPoint = best.Coordinates;
                    BestObjectives = best.Outputs;
                    BestSimulation = best.Simulation;
                    lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, "Best point", Text(ParameterNames, best.Coordinates) + " (simulation " + best.Simulation.ToString(CultureInfo.InvariantCulture) + ")"));
                    lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, "Best objective", Text(ObjectiveNames, best.Outputs)));
                }

                if (nativeGenOptOutcome.Interval != null)
                {
                    Interval = nativeGenOptOutcome.Interval;
                    lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, "Final interval", string.Format(CultureInfo.InvariantCulture, "[{0}, {1}]", Interval.Lower, Interval.Upper)));
                }
            }

            if (result.Retries > 0)
            {
                lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Warning, "Retries", string.Format(CultureInfo.InvariantCulture, "{0} evaluation(s) failed once and were retried.", result.Retries)));
            }

            if (!string.IsNullOrWhiteSpace(runDirectory))
            {
                lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, "Run folder", runDirectory!));
            }
        }

        /// <summary>The kernel result; null when the run never started.</summary>
        public OptimisationResult? Result { get; }

        /// <summary>True when the run ended normally (Success, simulation limit or nullspace) and was not cancelled.</summary>
        public bool Successful { get; }

        /// <summary>The kernel outcome; <see cref="OptimisationOutcome.Undefined"/> when the run never started.</summary>
        public OptimisationOutcome Outcome { get; }

        /// <summary>Ready for success, Warning for a normal end worth a note or a cancel, Blocked for a failure.</summary>
        public TasOptimisationCheckStatus Status { get; }

        public string Headline { get; }

        public int Simulations { get; }

        public int Retries { get; }

        public int FailedSimulation { get; }

        public string? FailureMessage { get; }

        public IReadOnlyList<string> ParameterNames { get; }

        public IReadOnlyList<string> ObjectiveNames { get; }

        /// <summary>The best point's parameter values in coordinate order; empty unless <see cref="Successful"/>.</summary>
        public IReadOnlyList<double> BestPoint { get; }

        /// <summary>The best point's outputs in output order (the first is the minimised one); empty unless <see cref="Successful"/>.</summary>
        public IReadOnlyList<double> BestObjectives { get; }

        /// <summary>The simulation that produced the best point, or 0.</summary>
        public int BestSimulation { get; }

        /// <summary>Golden section's final interval, when the run was successful.</summary>
        public GoldenSectionInterval? Interval { get; }

        public string? RunDirectory { get; }

        /// <summary>The report's lines after the headline: outcome notes, best point, interval, retries, run folder.</summary>
        public IReadOnlyList<TasOptimisationCheck> Lines => lines;

        /// <summary>The whole report as plain text, for the clipboard.</summary>
        public string ToText()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("SAM Optimisation (native)");
            stringBuilder.AppendLine(Headline);
            stringBuilder.AppendLine("Outcome: " + Outcome);
            if (Result != null)
            {
                stringBuilder.AppendLine("Simulations: " + Simulations.ToString(CultureInfo.InvariantCulture));
            }

            foreach (TasOptimisationCheck line in lines)
            {
                stringBuilder.AppendLine(line.Title + ": " + line.Detail);
            }

            return stringBuilder.ToString();
        }

        /// <summary>"name = value" pairs, invariant round-trip numbers.</summary>
        public static string Text(IReadOnlyList<string>? names, IReadOnlyList<double>? values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            return string.Join(", ", values.Select((x, i) => (names != null && i < names.Count ? names[i] : "#" + i) + " = " + x.ToString("R", CultureInfo.InvariantCulture)));
        }

        /// <summary>
        /// A refusal or failure outside the kernel: a stale or missing assembly first (SAM_UI's own check), otherwise SAM_Tas'
        /// <see cref="NativeGenOptOutcome.RefusalMessage"/>, as the Grasshopper component words it.
        /// </summary>
        public static string Message(Exception? exception)
        {
            if (Query.IsTasOptimisationLoadFailure(exception))
            {
                return Query.TasOptimisationLoadFailure(exception);
            }

            return RefusalMessage(exception);
        }

        // Its own method, never inlined, so Message still reports a stale SAM.Analytical.Tas.GenOpt.dll (one without
        // NativeGenOptOutcome) through the load-failure branch instead of failing to compile.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static string RefusalMessage(Exception? exception)
        {
            return NativeGenOptOutcome.RefusalMessage(exception);
        }
    }
}
