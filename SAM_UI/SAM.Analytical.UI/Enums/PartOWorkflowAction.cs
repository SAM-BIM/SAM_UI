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
    }
}
