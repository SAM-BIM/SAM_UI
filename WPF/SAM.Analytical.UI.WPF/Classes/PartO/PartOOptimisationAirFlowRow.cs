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
    /// <para>
    /// <b><see cref="Stage"/> tells the three kinds of step apart</b> - BASELINE, OPTIMISATION and CAPACITY
    /// ENVELOPE. The envelope's rows are partial (or several times over) steps that the all-or-nothing
    /// policy deliberately refuses, evaluated to say what the equipment already bought could deliver; read
    /// as optimisation rows they would be the run's best result, which they are not. Its <see cref="Run"/>
    /// is <c>MAX</c> rather than a number, for the same reason its results file is <c>-OptMax</c>: a number
    /// would place it in the rounds' sequence, where the last of them is the answer.
    /// </para>
    /// </summary>
    public class PartOOptimisationAirFlowRow
    {
        private PartOOptimisationAirFlowRow(PartOOptimisationStep partOOptimisationStep, string spaceName, string type, string direction, double before_Lps, double? requested_Lps, double achieved_Lps, double requirement_Lps, TM59ComplianceStatus tM59ComplianceStatus)
        {
            Run = partOOptimisationStep.IsCapacityEnvelope ? "MAX" : partOOptimisationStep.Iteration.ToString();
            Stage = Stages(partOOptimisationStep.Kind);
            Space = spaceName;
            Type = type;
            Direction = direction;
            DesignBefore_Lps = before_Lps;
            Requested_Lps = requested_Lps;
            Achieved_Lps = achieved_Lps;
            Requirement_Lps = requirement_Lps;
            ComplianceStatus = tM59ComplianceStatus;
        }

        /// <summary>Which iteration - 0 is the baseline, and MAX is the diagnostic capacity envelope.</summary>
        public string Run { get; }

        /// <summary>
        /// BASELINE, OPTIMISATION or CAPACITY ENVELOPE. <b>Read this before reading any airflow on the
        /// row</b>: an envelope row is a diagnostic and not a design the optimisation accepted.
        /// </summary>
        public string Stage { get; }

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

            result.AddRange(BaselineRows(partOOptimisationRun));

            foreach (PartOOptimisationStep partOOptimisationStep in partOOptimisationRun.Steps)
            {
                foreach (DesignAirFlowAdjustment designAirFlowAdjustment in partOOptimisationStep.TargetedAdjustments)
                {
                    result.Add(new PartOOptimisationAirFlowRow(
                        partOOptimisationStep,
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
                        partOOptimisationStep,
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
        /// Where every room the optimisation later touched started, as run 0.
        ///
        /// <para><b>Why these are synthesised rather than recorded</b></para>
        /// <para>
        /// The baseline is the Iteration 2 design as it stood: it has no targets and no round, so it holds
        /// no adjustments to read rows from. But a before-and-after history that begins at run 1 cannot be
        /// read as a before-and-after history - a reader looking at "Kitchen_4 55 -> 60" in run 1 has to
        /// take on trust that 55 was where it started, and a room whose first move is in run 4 has no
        /// stated origin at all.
        /// </para>
        /// <para>
        /// Each room's baseline design airflow is exactly the <c>Before_Lps</c> of the FIRST adjustment
        /// that ever moved it, and that is a recorded fact rather than an inference: the round that made
        /// that adjustment read the value off the design it was given. The baseline TM59 verdict comes from
        /// the baseline step's own production results. Nothing here is computed.
        /// </para>
        /// <para>
        /// Rooms nothing ever happened to are deliberately not listed - their design airflow is in the
        /// model, and printing every room would bury the ones that moved.
        /// </para>
        /// </summary>
        private static List<PartOOptimisationAirFlowRow> BaselineRows(PartOOptimisationRun partOOptimisationRun)
        {
            List<PartOOptimisationAirFlowRow> result = [];

            PartOOptimisationStep? partOOptimisationStep_Baseline = partOOptimisationRun.Step_Baseline;
            if (partOOptimisationStep_Baseline is null)
            {
                return result;
            }

            HashSet<string> keys = [];

            foreach (PartOOptimisationStep partOOptimisationStep in partOOptimisationRun.Steps)
            {
                //The ENVELOPE is excluded as well as the baseline. Its Before_Lps is the last ACCEPTED
                //design's airflow, not the baseline's, so a room whose first ever movement is in the
                //envelope would otherwise contribute a "baseline" row stating a figure the baseline never
                //carried. Its own rows print that Before honestly, beside its own stage.
                if (!partOOptimisationStep.IsOptimisationRound)
                {
                    continue;
                }

                //Targeted first, then derived - the order the round itself settled on, so the baseline
                //block reads in the same order as the rounds beneath it.
                foreach (DesignAirFlowAdjustment designAirFlowAdjustment in partOOptimisationStep.Adjustments())
                {
                    if (!keys.Add(string.Format("{0}|{1}", designAirFlowAdjustment.SpaceGuid, designAirFlowAdjustment.FlowClassification)))
                    {
                        continue;
                    }

                    result.Add(new PartOOptimisationAirFlowRow(
                        partOOptimisationStep_Baseline,
                        designAirFlowAdjustment.SpaceName,
                        "BASELINE",
                        Core.Query.Description(designAirFlowAdjustment.FlowClassification),
                        designAirFlowAdjustment.Before_Lps,
                        null,
                        designAirFlowAdjustment.Before_Lps,
                        designAirFlowAdjustment.Requirement_Lps,
                        Status(partOOptimisationStep_Baseline, designAirFlowAdjustment.SpaceGuid)));
                }
            }

            return result;
        }

        /// <summary>
        /// The stage label, in the words the grid shows - the three things a reader has to tell apart, taken
        /// from what the step says it is rather than from its number.
        /// </summary>
        private static string Stages(PartOOptimisationStepKind partOOptimisationStepKind)
        {
            return partOOptimisationStepKind switch
            {
                PartOOptimisationStepKind.Baseline => "BASELINE",
                PartOOptimisationStepKind.CapacityEnvelope => "CAPACITY ENVELOPE",
                _ => "OPTIMISATION",
            };
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
