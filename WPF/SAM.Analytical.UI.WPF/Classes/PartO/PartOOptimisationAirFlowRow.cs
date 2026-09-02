// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// One row of the Iteration 2B airflow history: what happened to one room at one iteration, and what
    /// the production TM59 assessment said about it afterwards.
    /// <para>
    /// <b><see cref="Type"/> is the column that matters most.</b> TARGETED means an engineering decision was
    /// made about this room; DERIVED means it moved to keep its dwelling balanced and nobody chose it. A
    /// history that merged the two would read as though every room that moved had been optimised, which is
    /// the misreading the whole design airflow round exists to prevent.
    /// </para>
    /// <para>
    /// <b>Requested and Achieved are both shown, and on an automatic round they agree.</b> That is the
    /// point of printing both: a round is adopted at exactly what it asked for or not at all, so a
    /// disagreement here would be visible rather than silent. A derived row has no request - nobody asked
    /// for it - and shows a dash.
    /// </para>
    /// </summary>
    public class PartOOptimisationAirFlowRow
    {
        private PartOOptimisationAirFlowRow(int iteration, string spaceName, string type, string direction, double before_Lps, double? requested_Lps, double achieved_Lps, double requirement_Lps, TM59ComplianceStatus tM59ComplianceStatus)
        {
            Run = iteration;
            Space = spaceName;
            Type = type;
            Direction = direction;
            DesignBefore_Lps = before_Lps;
            Requested_Lps = requested_Lps;
            Achieved_Lps = achieved_Lps;
            Requirement_Lps = requirement_Lps;
            ComplianceStatus = tM59ComplianceStatus;
        }

        /// <summary>Which iteration - 0 is the baseline.</summary>
        public int Run { get; }

        /// <summary>The room.</summary>
        public string Space { get; }

        /// <summary>BASELINE, TARGETED or DERIVED.</summary>
        public string Type { get; }

        /// <summary>Supply or extract.</summary>
        public string Direction { get; }

        /// <summary>What the room was designed at before this iteration [l/s].</summary>
        public double DesignBefore_Lps { get; }

        /// <summary>What was asked of it [l/s], or null on a baseline or derived row - nobody asked.</summary>
        public double? Requested_Lps { get; }

        /// <summary>What it is designed at after this iteration [l/s].</summary>
        public double Achieved_Lps { get; }

        /// <summary>
        /// What Approved Document F requires of the room [l/s]. Shown so a reader can see at a glance that
        /// the design stayed above the floor - it is never altered by an optimisation.
        /// </summary>
        public double Requirement_Lps { get; }

        /// <summary>The production TM59 verdict for this room after this iteration.</summary>
        public TM59ComplianceStatus ComplianceStatus { get; }

        /// <summary>The verdict as the report words it.</summary>
        public string TM59Status => Core.Query.Description(ComplianceStatus);

        /// <summary>
        /// Every airflow row of a whole optimisation run, iteration by iteration.
        /// <para>
        /// The baseline contributes one row per room the optimisation later touched, so a reader can see
        /// where each of them started. Rooms nothing ever happened to are not listed: their design airflow
        /// is in the model and printing it here would bury the rooms that moved.
        /// </para>
        /// </summary>
        public static List<PartOOptimisationAirFlowRow> Rows(PartOOptimisationRun? partOOptimisationRun)
        {
            List<PartOOptimisationAirFlowRow> result = [];

            if (partOOptimisationRun is null)
            {
                return result;
            }

            foreach (PartOOptimisationStep partOOptimisationStep in partOOptimisationRun.Steps)
            {
                foreach (DesignAirFlowAdjustment designAirFlowAdjustment in partOOptimisationStep.TargetedAdjustments)
                {
                    result.Add(new PartOOptimisationAirFlowRow(
                        partOOptimisationStep.Iteration,
                        designAirFlowAdjustment.SpaceName,
                        "TARGETED",
                        Core.Query.Description(designAirFlowAdjustment.FlowClassification),
                        designAirFlowAdjustment.Before_Lps,
                        designAirFlowAdjustment.After_Lps,
                        designAirFlowAdjustment.After_Lps,
                        designAirFlowAdjustment.Requirement_Lps,
                        Status(partOOptimisationStep, designAirFlowAdjustment.SpaceGuid)));
                }

                foreach (DesignAirFlowAdjustment designAirFlowAdjustment in partOOptimisationStep.DerivedAdjustments)
                {
                    result.Add(new PartOOptimisationAirFlowRow(
                        partOOptimisationStep.Iteration,
                        designAirFlowAdjustment.SpaceName,
                        "DERIVED",
                        Core.Query.Description(designAirFlowAdjustment.FlowClassification),
                        designAirFlowAdjustment.Before_Lps,
                        null,
                        designAirFlowAdjustment.After_Lps,
                        designAirFlowAdjustment.Requirement_Lps,
                        Status(partOOptimisationStep, designAirFlowAdjustment.SpaceGuid)));
                }
            }

            return result;
        }

        /// <summary>
        /// The production verdict for one design space at one iteration - the mechanical criterion where
        /// there is one, and any failure winning over a pass.
        /// <para>
        /// <b>Read off the assessment's own statuses, never derived from Actual and Limit</b>, which differ
        /// in whether a zero margin passes. Combining is only ever "a Fail beats a Pass", which is the same
        /// rule the production report combines by.
        /// </para>
        /// </summary>
        private static TM59ComplianceStatus Status(PartOOptimisationStep partOOptimisationStep, System.Guid guid)
        {
            TM59ComplianceStatus result = TM59ComplianceStatus.Undefined;

            foreach (PartOTM59SpaceResult partOTM59SpaceResult in partOOptimisationStep.TM59Results)
            {
                if (partOTM59SpaceResult.SpaceGuid_Design != guid)
                {
                    continue;
                }

                if (partOTM59SpaceResult.ComplianceStatus == TM59ComplianceStatus.Fail)
                {
                    return TM59ComplianceStatus.Fail;
                }

                if (partOTM59SpaceResult.ComplianceStatus == TM59ComplianceStatus.Pass)
                {
                    result = TM59ComplianceStatus.Pass;
                }
            }

            return result;
        }
    }
}
