// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Optimisation;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// "Test one simulation" of the "tas-model" engine as the window shows it: the point tested (each variable's start,
    /// or its minimum; option 1, the model as it is, for a choice), its outputs with their units, how long the one Tas
    /// simulation took, and the run's duration estimate from it (simulations × that time). Values are formatted as the
    /// results are (PR5b); the tooltip of each line keeps full precision.
    /// </summary>
    public sealed class TasOptimisationTestReport
    {
        private readonly List<TasOptimisationCheck> lines = new List<TasOptimisationCheck>();

        /// <param name="coordinates">The point tested, in variable order.</param>
        /// <param name="succeeded">True when the simulation gave its outputs.</param>
        /// <param name="outputs">The outputs, objective first (empty on failure).</param>
        /// <param name="message">The evaluator's message on failure.</param>
        /// <param name="duration">The wall time of the simulation.</param>
        /// <param name="evaluationDirectory">The evaluation folder (Variables.txt, Output.txt, the trace).</param>
        /// <param name="optimisationDefinition">The definition tested.</param>
        /// <param name="tasOptimisationFormatter">How its values are shown.</param>
        /// <param name="maximumSimulations">The simulation limit the kernel runs.</param>
        public TasOptimisationTestReport(IReadOnlyList<double> coordinates, bool succeeded, IReadOnlyList<double>? outputs, string? message, TimeSpan duration, string? evaluationDirectory, OptimisationDefinition optimisationDefinition, TasOptimisationFormatter tasOptimisationFormatter, int maximumSimulations)
        {
            if (optimisationDefinition == null)
            {
                throw new ArgumentNullException(nameof(optimisationDefinition));
            }

            if (tasOptimisationFormatter == null)
            {
                throw new ArgumentNullException(nameof(tasOptimisationFormatter));
            }

            Succeeded = succeeded;
            Duration = duration;
            Coordinates = coordinates ?? new List<double>();
            Outputs = outputs ?? new List<double>();

            List<string> names_Variable = tasOptimisationFormatter.Variables.Select(x => x.Name).ToList();
            List<string> names_Output = tasOptimisationFormatter.Outputs.Select(x => x.Name).ToList();

            lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, "Design tested", tasOptimisationFormatter.Pairs(tasOptimisationFormatter.Variables, Coordinates), TasOptimisationFormatter.RawPairs(names_Variable, Coordinates)));

            if (succeeded)
            {
                Status = TasOptimisationCheckStatus.Ready;
                Headline = "One Tas simulation took " + DurationText(duration);
                lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, "Outputs", tasOptimisationFormatter.Pairs(tasOptimisationFormatter.Outputs, Outputs), TasOptimisationFormatter.RawPairs(names_Output, Outputs)));

                Estimate = EstimateText(optimisationDefinition, duration, maximumSimulations);
                lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, "Run estimate", Estimate));
            }
            else
            {
                Status = TasOptimisationCheckStatus.Blocked;
                Headline = "The test simulation failed after " + DurationText(duration);
                lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, "Simulation", string.IsNullOrWhiteSpace(message) ? "Tas gave no outputs." : message!));
            }

            if (!string.IsNullOrWhiteSpace(evaluationDirectory))
            {
                lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Ready, "Simulation folder", evaluationDirectory!));
            }
        }

        /// <summary>The test could not start (the definition was refused, Tas or TasGenExecute missing).</summary>
        public TasOptimisationTestReport(Exception exception)
        {
            Succeeded = false;
            Status = TasOptimisationCheckStatus.Blocked;
            Headline = "The test simulation did not start";
            Coordinates = new List<double>();
            Outputs = new List<double>();
            lines.Add(new TasOptimisationCheck(TasOptimisationCheckStatus.Blocked, "Error", TasOptimisationReport.Message(exception)));
        }

        public bool Succeeded { get; }

        public TasOptimisationCheckStatus Status { get; }

        public string Headline { get; }

        public TimeSpan Duration { get; }

        public IReadOnlyList<double> Coordinates { get; }

        public IReadOnlyList<double> Outputs { get; }

        /// <summary>The run's duration estimate; null when the test failed.</summary>
        public string? Estimate { get; }

        public IReadOnlyList<TasOptimisationCheck> Lines => lines;

        /// <summary>
        /// The run's duration from one simulation's time. Try every option runs exactly one simulation per option. Golden
        /// section and Hooke–Jeeves stop when they converge, so their number of simulations is not known in advance: the
        /// time of 10 and 50 simulations is given, and the time at the simulation limit.
        /// </summary>
        public static string EstimateText(OptimisationDefinition optimisationDefinition, TimeSpan duration, int maximumSimulations)
        {
            if (optimisationDefinition?.Method?.Algorithm == OptimisationAlgorithm.TryEveryOption)
            {
                int options = optimisationDefinition.Variables.Where(x => x?.Target?.Options != null).Select(x => x.Target.Options.Count).DefaultIfEmpty(0).Max();
                if (options == 0)
                {
                    options = optimisationDefinition.Variables.Select(x => (int)System.Math.Max(0, x.Maximum - x.Minimum + 1)).DefaultIfEmpty(0).Max();
                }

                return string.Format(CultureInfo.InvariantCulture, "Try every option runs one simulation per option: {0} simulations, about {1}.", options, DurationText(Multiply(duration, options)));
            }

            return string.Format(
                CultureInfo.InvariantCulture,
                "About {0} per simulation. {1} stops when it has converged, so the number of simulations is not known in advance: 10 simulations take about {2}, 50 about {3}; the limit of {4} simulations would take about {5}.",
                DurationText(duration),
                optimisationDefinition?.Method?.Algorithm.TasOptimisationAlgorithmName() ?? "The search",
                DurationText(Multiply(duration, 10)),
                DurationText(Multiply(duration, 50)),
                maximumSimulations,
                DurationText(Multiply(duration, maximumSimulations)));
        }

        /// <summary>"7.2 s", "3 min 40 s", "2 h 5 min".</summary>
        public static string DurationText(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
            {
                duration = TimeSpan.Zero;
            }

            if (duration.TotalSeconds < 60)
            {
                return duration.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
            }

            if (duration.TotalMinutes < 60)
            {
                int seconds = (int)System.Math.Round(duration.TotalSeconds);
                return string.Format(CultureInfo.InvariantCulture, "{0} min {1} s", seconds / 60, seconds % 60);
            }

            int minutes = (int)System.Math.Round(duration.TotalMinutes);
            return string.Format(CultureInfo.InvariantCulture, "{0} h {1} min", minutes / 60, minutes % 60);
        }

        /// <summary>The report as plain text, full precision.</summary>
        public string ToText()
        {
            return Headline + Environment.NewLine + string.Join(Environment.NewLine, lines.Select(x => x.Title + ": " + (x.Raw ?? x.Detail)));
        }

        private static TimeSpan Multiply(TimeSpan timeSpan, int count)
        {
            return TimeSpan.FromTicks(timeSpan.Ticks * System.Math.Max(0, count));
        }
    }
}
