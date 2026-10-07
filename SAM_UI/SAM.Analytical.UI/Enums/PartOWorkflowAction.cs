// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.ComponentModel;

namespace SAM.Analytical.UI
{
    /// <summary>
    /// What the Prepare and Run dialog was closed to do.
    /// <para>
    /// <b>Each one is an existing command.</b> The dialog decides which to invoke and nothing about what it
    /// does - <see cref="PrepareAndRun"/> is the preparation, the pre-simulation check, the TAS workflow and
    /// the assessment in their existing order; <see cref="ReviewResults"/> is
    /// <c>Modify.AssessPartOTM59</c>; <see cref="Optimise"/> is <c>Modify.RunPartOOptimisation</c>.
    /// </para>
    /// </summary>
    public enum PartOWorkflowAction
    {
        [Description("None")] None,

        [Description("Prepare & Run")] PrepareAndRun,

        [Description("Review Results")] ReviewResults,

        [Description("Optimise (2B)")] Optimise,

        /// <summary>
        /// Run the Approved Document O Iteration 3 system case for the chosen method -
        /// <c>Modify.RunPartOIteration3</c> on an eligible run. The Hub's Iteration 3 panel says, per method,
        /// whether a completed result already exists, and offers <see cref="Iteration3Review"/> for it.
        /// </summary>
        [Description("Iteration 3 — run")] Iteration3,

        /// <summary>
        /// Open the saved Iteration 3 result for the chosen method - <c>Modify.ReviewPartOIteration3</c>,
        /// which reads existing results and runs no TAS. Also opens a method's last, incomplete attempt, whose
        /// stage record is the diagnosis.
        /// </summary>
        [Description("Iteration 3 — open result")] Iteration3Review,

        /// <summary>
        /// Open Mixed Design (<c>Modify.RunPartOMixedDesign</c>) to choose each dwelling's cooling control room,
        /// which the Iteration 3 manufacturer-guidance method needs. The Hub reopens afterwards, re-inspected.
        /// </summary>
        [Description("Mixed Design — cooling control rooms")] MixedDesign,

        /// <summary>
        /// Remove Results (<c>Modify.RemovePartOResults</c>): save a copy of the model without its simulation
        /// results, which Mixed Design needs. Opening the copy replaces the model, so the Hub does not reopen.
        /// </summary>
        [Description("Remove Results")] RemoveResults,
    }
}
