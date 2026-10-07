// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

extern alias SAMMath;

using SAMMath::SAM.Math;
using System.Collections.Generic;
using System.Globalization;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// What the Optimisation window shows while a run is going, folded from the kernel's <see cref="OptimisationProgress"/>
    /// notifications and nothing else. The kernel reports every entry once to OutputListingAll and a main-iteration
    /// entry once more to OutputListingMain; the trace keeps the former, so its rows are exactly
    /// <see cref="OptimisationResult.Entries"/>. The kernel reports an entry only after its evaluation, so there is no
    /// "current point" while TasGenExecute runs - only the last one reported.
    /// </summary>
    public sealed class TasOptimisationProgressState
    {
        private readonly List<TasOptimisationTraceRow> rows = new List<TasOptimisationTraceRow>();

        public TasOptimisationProgressState(IReadOnlyList<string>? parameterNames, IReadOnlyList<string>? objectiveNames)
        {
            ParameterNames = parameterNames ?? new List<string>();
            ObjectiveNames = objectiveNames ?? new List<string>();
        }

        public IReadOnlyList<string> ParameterNames { get; }

        public IReadOnlyList<string> ObjectiveNames { get; }

        /// <summary>The trace so far: the OutputListingAll entries in order.</summary>
        public IReadOnlyList<TasOptimisationTraceRow> Rows => rows;

        /// <summary>Simulations counted by the kernel so far.</summary>
        public int Simulations { get; private set; }

        /// <summary>The simulation limit (MaxIte).</summary>
        public int MaximumSimulations { get; private set; }

        /// <summary>The last entry reported.</summary>
        public OptimisationTraceEntry? Last { get; private set; }

        /// <summary>The lowest objective reported so far (first one on a tie, NaN skipped). Not the final best point.</summary>
        public OptimisationTraceEntry? Lowest { get; private set; }

        /// <summary>Folds one notification in; returns the new trace row, or null for a main-iteration repeat.</summary>
        public TasOptimisationTraceRow? Add(OptimisationProgress optimisationProgress)
        {
            if (optimisationProgress?.Entry == null)
            {
                return null;
            }

            Simulations = optimisationProgress.Simulations;
            MaximumSimulations = optimisationProgress.MaximumSimulations;

            if (optimisationProgress.MainIteration)
            {
                return null;
            }

            OptimisationTraceEntry entry = optimisationProgress.Entry;
            Last = entry;

            if (!double.IsNaN(entry.Objective) && (Lowest == null || entry.Objective < Lowest.Objective))
            {
                Lowest = entry;
            }

            TasOptimisationTraceRow result = new TasOptimisationTraceRow(entry);
            rows.Add(result);
            return result;
        }

        /// <summary>"Simulation n (limit m)".</summary>
        public string SimulationText()
        {
            return string.Format(CultureInfo.InvariantCulture, "Simulation {0} (limit {1})", Simulations, MaximumSimulations);
        }

        /// <summary>The last reported point, e.g. "Simulation 4: Setpoint = 10 -> Result = 7360.04".</summary>
        public string? LastText()
        {
            return Last == null ? null : Text(Last);
        }

        /// <summary>The lowest point so far.</summary>
        public string? LowestText()
        {
            return Lowest == null ? null : Text(Lowest);
        }

        private string Text(OptimisationTraceEntry optimisationTraceEntry)
        {
            List<double> objective = new List<double> { optimisationTraceEntry.Objective };
            return string.Format(
                CultureInfo.InvariantCulture,
                "Simulation {0}: {1} -> {2}",
                optimisationTraceEntry.Simulation,
                TasOptimisationReport.Text(ParameterNames, optimisationTraceEntry.Coordinates),
                TasOptimisationReport.Text(ObjectiveNames, objective));
        }
    }
}
