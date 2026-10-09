// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

extern alias SAMMath;

using SAMMath::SAM.Math;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// One row of the Optimisation window's trace: a kernel <see cref="OptimisationTraceEntry"/> of
    /// <see cref="OptimisationResult.Entries"/> (GenOpt's OutputListingAll), shown as it is. The grid shows each value
    /// with engineering formatting and its unit (<see cref="CoordinateTexts"/>, <see cref="OutputTexts"/>); the
    /// tooltip and the copied cell carry the full-precision value (<see cref="CoordinateRaw"/>, <see cref="OutputRaw"/>).
    /// </summary>
    public sealed class TasOptimisationTraceRow
    {
        public TasOptimisationTraceRow(OptimisationTraceEntry optimisationTraceEntry, TasOptimisationFormatter? tasOptimisationFormatter = null)
        {
            Entry = optimisationTraceEntry;

            CoordinateRaw = Entry.Coordinates.Select(TasOptimisationFormatter.Raw).ToList().AsReadOnly();
            OutputRaw = Entry.Outputs.Select(TasOptimisationFormatter.Raw).ToList().AsReadOnly();
            CoordinateTexts = tasOptimisationFormatter == null ? CoordinateRaw : Entry.Coordinates.Select((x, i) => i < tasOptimisationFormatter.Variables.Count ? tasOptimisationFormatter.Text(tasOptimisationFormatter.Variables[i], x) : TasOptimisationFormatter.Raw(x)).ToList().AsReadOnly();
            OutputTexts = tasOptimisationFormatter == null ? OutputRaw : Entry.Outputs.Select((x, i) => i < tasOptimisationFormatter.Outputs.Count ? tasOptimisationFormatter.Text(tasOptimisationFormatter.Outputs[i], x) : TasOptimisationFormatter.Raw(x)).ToList().AsReadOnly();
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

        /// <summary>The parameter values as shown: engineering formatting with the declared unit.</summary>
        public IReadOnlyList<string> CoordinateTexts { get; }

        /// <summary>The outputs as shown: engineering formatting with the declared unit.</summary>
        public IReadOnlyList<string> OutputTexts { get; }

        /// <summary>The parameter values at full precision (invariant round-trip text): tooltip and copy.</summary>
        public IReadOnlyList<string> CoordinateRaw { get; }

        /// <summary>The outputs at full precision (invariant round-trip text): tooltip and copy.</summary>
        public IReadOnlyList<string> OutputRaw { get; }
    }
}
