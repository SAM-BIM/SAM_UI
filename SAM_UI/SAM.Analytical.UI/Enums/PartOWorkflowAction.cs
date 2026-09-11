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
        /// The Approved Document O Iteration 3 A/B pairing - <c>Modify.RunPartOIteration3</c> on an
        /// eligible in-session run, and <c>Modify.ReviewPartOIteration3</c> where there is a persisted
        /// record to reopen. One action rather than two: which of the two it is is a property of the state
        /// the run is in, and asking a person to work that out from two greyed buttons is worse than
        /// telling them.
        /// </summary>
        [Description("Iteration 3 (A/B)")] Iteration3,
    }
}
