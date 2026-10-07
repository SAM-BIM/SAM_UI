// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Windows.Forms;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>What a run found where it is about to write, and what the person decided.</summary>
    internal enum PartOExistingResultsDecision
    {
        /// <summary>Nothing of another run is in the way: go on, ask nothing.</summary>
        None,

        /// <summary>Another run's results are there and the person confirmed replacing them.</summary>
        Replace,

        /// <summary>Another run's results are there and the person cancelled: nothing is changed and nothing is run.</summary>
        Cancel,
    }

    public static partial class Modify
    {
        /// <summary>
        /// A run is about to write into a Part O case folder. Where that folder holds another run's results it is
        /// <b>asked about</b> - the person replaces them or cancels - instead of the run being refused after the review,
        /// which left a person no choice but another folder. Pure decision: it deletes nothing and writes nothing;
        /// <paramref name="confirm"/> is the question, so a test answers it without a window.
        /// </summary>
        /// <param name="partOOutputPaths">Where the run would write, or null where it has no case folder (nothing to ask).</param>
        /// <param name="guid_Run">The run asking; <see cref="Guid.Empty"/> for one not yet prepared, which owns nothing.</param>
        /// <param name="caseKey">The run's TAS case key.</param>
        /// <param name="confirm">Shows what is in the folder and returns whether to replace it.</param>
        internal static PartOExistingResultsDecision ResolveExistingPartOResults(PartOOutputPaths? partOOutputPaths, Guid guid_Run, string? caseKey, Func<PartOOutputOccupancy, bool> confirm)
        {
            if (partOOutputPaths is null)
            {
                return PartOExistingResultsDecision.None;
            }

            PartOOutputOccupancy partOOutputOccupancy = partOOutputPaths.Occupancy(guid_Run, caseKey);

            if (!partOOutputOccupancy.NeedsReplacement)
            {
                return PartOExistingResultsDecision.None;
            }

            return confirm(partOOutputOccupancy) ? PartOExistingResultsDecision.Replace : PartOExistingResultsDecision.Cancel;
        }

        /// <summary>
        /// The question, as a window: names the case and the folder, what is in it, and what replacing does and does not
        /// touch. <paramref name="removesGeneratedFiles"/> is what replacing means for the caller (Prepare &amp; Run removes
        /// the case's generated files; Iteration 3 overwrites only what its new run writes).
        /// </summary>
        internal static bool ConfirmReplacePartOResults(string caseName, PartOOutputOccupancy partOOutputOccupancy, bool removesGeneratedFiles, IWin32Window? owner)
        {
            PartOReplaceResultsWindow partOReplaceResultsWindow = new(caseName, partOOutputOccupancy, removesGeneratedFiles);

            if (owner is not null)
            {
                new System.Windows.Interop.WindowInteropHelper(partOReplaceResultsWindow).Owner = owner.Handle;
            }

            return partOReplaceResultsWindow.ShowDialog() == true;
        }

        /// <summary>The Hub's line after the person cancelled replacing existing results: nothing was changed or run.</summary>
        internal static PartOWorkflowOutcome ReplaceDeclinedOutcome(string? iteration)
        {
            return new PartOWorkflowOutcome(
                PartOWorkflowOutcomeKind.Information,
                "○",
                string.Format("{0} not run — the existing results were kept", Name(iteration)),
                "Choose another output folder, or press Prepare & Run again and replace them · nothing was changed");
        }
    }
}
