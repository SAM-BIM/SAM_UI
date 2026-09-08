// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// One heading of the Prepare and Run status list, and the rows under it.
    ///
    /// <para><b>Why the list is divided at all</b></para>
    /// <para>
    /// Because a legitimate mixed state reads as a contradiction. A model can honestly report
    /// <c>Ventilation design: NEEDS PREPARATION</c> beside <c>Results: READY</c> - the results were saved by
    /// an earlier run and can be reviewed without simulating anything - and read as one flat list that looks
    /// like the dialog disagreeing with itself. Splitting the same rows into what the CURRENT CONFIGURATION
    /// says and what an EXISTING RUN produced makes the mix obvious instead of puzzling.
    /// </para>
    ///
    /// <para><b>It decides nothing about staleness</b></para>
    /// <para>
    /// This is a heading over rows, and that is all it is. No configuration is hashed, no timestamp is read
    /// or written, nothing is compared between the two groups and no new status is inferred. Every row still
    /// carries exactly the <see cref="PartOWorkflowStageStatus"/> the inspection assigned it, and where the
    /// application already knows a run is reused or reopened it is the inspection's own detail sentence that
    /// says so.
    /// </para>
    ///
    /// <para><b>Membership is fixed by stage, not judged</b></para>
    /// <para>
    /// A stage belongs to one group by which question it answers, and a stage the inspection stops
    /// reporting simply leaves its group. A stage this class has no group for is put in the configuration
    /// group rather than dropped - a row must never be lost by a presentation change.
    /// </para>
    /// </summary>
    public class PartOWorkflowStatusGroup
    {
        /// <summary>The heading over the stages the current scenario, scope and equipment choice describe.</summary>
        public const string Name_Configuration = "Current configuration";

        /// <summary>The heading over the stages that report what a run has already produced.</summary>
        public const string Name_Run = "Existing run / results";

        private PartOWorkflowStatusGroup(string name, List<PartOWorkflowStatusRow> partOWorkflowStatusRows)
        {
            Name = name;
            Rows = partOWorkflowStatusRows;
        }

        /// <summary>The heading.</summary>
        public string Name { get; }

        /// <summary>The rows under it, in the order the inspection reported them.</summary>
        public List<PartOWorkflowStatusRow> Rows { get; }

        /// <summary>Whether this group has anything to show. An empty group is not rendered.</summary>
        public bool HasRows => Rows is not null && Rows.Count != 0;

        /// <summary>
        /// The two groups, over exactly the rows handed in and in the order they were handed in.
        /// <para>
        /// Every row is placed in exactly one group and none is filtered out, so the union of the groups is
        /// the inspection's own list.
        /// </para>
        /// </summary>
        public static List<PartOWorkflowStatusGroup> Groups(IEnumerable<PartOWorkflowStatusRow> partOWorkflowStatusRows)
        {
            List<PartOWorkflowStatusRow> rows_Configuration = [];
            List<PartOWorkflowStatusRow> rows_Run = [];

            foreach (PartOWorkflowStatusRow partOWorkflowStatusRow in partOWorkflowStatusRows ?? [])
            {
                if (partOWorkflowStatusRow is null)
                {
                    continue;
                }

                if (IsRun(partOWorkflowStatusRow.State?.Stage))
                {
                    rows_Run.Add(partOWorkflowStatusRow);
                }
                else
                {
                    rows_Configuration.Add(partOWorkflowStatusRow);
                }
            }

            List<PartOWorkflowStatusGroup> result = [];

            if (rows_Configuration.Count != 0)
            {
                result.Add(new PartOWorkflowStatusGroup(Name_Configuration, rows_Configuration));
            }

            if (rows_Run.Count != 0)
            {
                result.Add(new PartOWorkflowStatusGroup(Name_Run, rows_Run));
            }

            return result;
        }

        /// <summary>
        /// Whether a stage reports on a run rather than on the configuration. The three that do are the ones
        /// that only a run can answer: the pre-simulation check, the simulation, and its results.
        /// </summary>
        private static bool IsRun(PartOWorkflowStage? partOWorkflowStage)
        {
            return partOWorkflowStage is PartOWorkflowStage.ModelCheck
                or PartOWorkflowStage.Simulation
                or PartOWorkflowStage.Results;
        }
    }
}
