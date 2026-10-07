// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

extern alias SAMMath;

using SAMMath::SAM.Math;
using System.Collections.Generic;
using System.Globalization;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// One row of the Optimisation window's trace: a kernel <see cref="OptimisationTraceEntry"/> of
    /// <see cref="OptimisationResult.Entries"/> (GenOpt's OutputListingAll), shown as it is.
    /// </summary>
    public sealed class TasOptimisationTraceRow
    {
        public TasOptimisationTraceRow(OptimisationTraceEntry optimisationTraceEntry)
        {
            Entry = optimisationTraceEntry;
        }

        public OptimisationTraceEntry Entry { get; }

        public int Simulation => Entry.Simulation;

        /// <summary>Main and sub-iteration counters, "main.sub".</summary>
        public string Iteration => string.Format(CultureInfo.InvariantCulture, "{0}.{1}", Entry.MainIteration, Entry.SubIteration);

        public string Event => Entry.Event.ToString();

        /// <summary>Parameter values in coordinate order (bound by index in the grid).</summary>
        public IReadOnlyList<double> Coordinates => Entry.Coordinates;

        /// <summary>Outputs in output order; the first is the minimised one (bound by index in the grid).</summary>
        public IReadOnlyList<double> Outputs => Entry.Outputs;
    }
}
