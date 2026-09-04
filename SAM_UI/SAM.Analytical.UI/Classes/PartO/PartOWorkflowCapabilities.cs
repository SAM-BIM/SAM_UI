// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.UI
{
    /// <summary>
    /// The facts about this session and this machine that no analytical model can answer, gathered by the
    /// caller and handed to <see cref="PartOWorkflowInspection"/>.
    /// <para>
    /// <b>This type exists so the inspection can stay a pure function of the model and the request.</b> Each
    /// field is the answer an existing authority already gives - the ventilation-unit catalogue reader,
    /// <c>PartORun.IsAssessable</c>, <c>Modify.CanOptimise</c> - carried across the layer boundary rather
    /// than re-decided. Nothing here is computed: a value that disagrees with its authority is a caller
    /// defect, not a second opinion.
    /// </para>
    /// <para>
    /// <b>Why the answers are not simply asked for here.</b> Two of them touch the filesystem
    /// (<c>IsAssessable</c> re-stats the results file, and can drop a run whose results have gone), and one
    /// of them reads a catalogue file through <c>SAM.Analytical.Systems</c>. A status list that is rebuilt on
    /// every keystroke must not do any of that, so the caller does it once and passes the result in.
    /// </para>
    /// </summary>
    public class PartOWorkflowCapabilities
    {
        /// <summary>Whether the manufacturer catalogue offers a product selection could choose from.</summary>
        public bool EquipmentAvailable { get; set; }

        /// <summary>
        /// What the catalogue reader said - which of its three states the read landed in, in its own words.
        /// Shown verbatim, so "no catalogue was found" is never presented as "no product can serve this
        /// dwelling".
        /// </summary>
        public string EquipmentDescription { get; set; }

        /// <summary>Whether there are results to review - <c>PartORun.IsAssessable</c>, asked once.</summary>
        public bool ResultsAvailable { get; set; }

        /// <summary>Why not, in that authority's own words, where there are none.</summary>
        public string ResultsRefusal { get; set; }

        /// <summary>Whether those results were reopened from a saved run rather than produced this session.</summary>
        public bool ResultsRestored { get; set; }

        /// <summary>The results file the review would read. Display only.</summary>
        public string Path_Results { get; set; }

        /// <summary>Whether Iteration 2B can start - <c>Modify.CanOptimise</c>, asked once.</summary>
        public bool OptimisationAvailable { get; set; }

        /// <summary>Why not, in that authority's own words.</summary>
        public string OptimisationRefusal { get; set; }
    }
}
