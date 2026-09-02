// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;

namespace SAM.Analytical.UI
{
    /// <summary>
    /// One iteration of an automatic Iteration 2B optimisation, recorded in enough detail to audit and
    /// reproduce it: what was deliberately targeted, what that derived, what the dwellings and their units
    /// then had to carry, which results file it produced, and what the production TM59 assessment made of it.
    /// <para>
    /// <b>Iteration 0 is the baseline.</b> It has no targets and no round - it is the Iteration 2 design as
    /// it stood, with its own results and its own assessment, and it is what every later iteration is read
    /// against. An optimisation that returned only its final model would leave nobody able to say what was
    /// tried or why it stopped.
    /// </para>
    /// <para>
    /// <b>The four authorities stay apart on every row.</b>
    /// <c>PartFRequiredAirFlow != DesignAirFlow != SelectedEquipmentCapacity != OperatingAirFlow</c>. The
    /// requirement is carried on each adjustment and never moves; the capacity is the unit's rating and is
    /// never a target; nothing here is a runtime airflow.
    /// </para>
    /// </summary>
    public class PartOOptimisationStep
    {
        /// <param name="iteration">0 for the baseline, then 1 upwards - one per optimisation round.</param>
        public PartOOptimisationStep(int iteration)
        {
            Iteration = iteration;
        }

        /// <summary>Which iteration this is. <b>0 is the baseline</b>, not the first round.</summary>
        public int Iteration { get; }

        /// <summary>Whether this step is the baseline the optimisation started from.</summary>
        public bool IsBaseline => Iteration == 0;

        /// <summary>
        /// The project name this iteration's TAS case ran as - <c>&lt;project&gt;-Opt00</c> upwards. The
        /// identity that ties the model, the TBD and the TSD of one iteration together.
        /// </summary>
        public string ProjectName { get; set; }

        /// <summary>
        /// The results file this iteration produced. <b>Unique per iteration</b>, so no round overwrites the
        /// evidence for another and any of them can be re-opened afterwards.
        /// </summary>
        public string Path_TSD { get; set; }

        /// <summary>The weather this iteration was simulated against - the same one every iteration uses.</summary>
        public string WeatherData { get; set; }

        /// <summary>
        /// The rooms this round <b>deliberately</b> targeted, with the airflow each was designed at before
        /// the round, what was requested of it, and what it achieved. Empty on the baseline.
        /// <para>
        /// An automatic round achieves exactly what it requested or it is not adopted, so these agree - which
        /// is the point of recording both rather than one.
        /// </para>
        /// </summary>
        public List<DesignAirFlowAdjustment> TargetedAdjustments { get; } = [];

        /// <summary>
        /// The rooms that moved to keep their dwellings balanced. <b>Not optimisation targets</b>, and kept
        /// apart from them for exactly that reason: a report that merged the two would claim every room that
        /// moved was chosen.
        /// </summary>
        public List<DesignAirFlowAdjustment> DerivedAdjustments { get; } = [];

        /// <summary>
        /// Targets this round could not take, with the reason - the explicit "not automatically optimisable"
        /// record for a room with no Approved Document O design terminal to move.
        /// </summary>
        public List<DesignAirFlowTargetRefusal> TargetRefusals { get; } = [];

        /// <summary>
        /// What each dwelling and its air handling unit carried after this iteration - the duty, the
        /// selected product, its maximum, the remaining headroom and the equipment outcome.
        /// </summary>
        public List<PartOOptimisationUnitState> UnitStates { get; } = [];

        /// <summary>
        /// The production TM59 result for every resolved design space of this iteration, criterion by
        /// criterion, exactly as the assessment produced them.
        /// </summary>
        public List<PartOTM59SpaceResult> TM59Results { get; } = [];

        /// <summary>
        /// The production TM59 verdict for the occupied spaces of this iteration, combined by the assessment
        /// itself. <b>Never recomputed here</b> from the rows above.
        /// </summary>
        public TM59ComplianceStatus OccupiedSpaceComplianceStatus { get; set; } = TM59ComplianceStatus.Undefined;

        /// <summary>Notes worth keeping from the round, the preparation and the assessment.</summary>
        public List<string> Notes { get; } = [];

        /// <summary>Advisories that did not stop the iteration.</summary>
        public List<string> Warnings { get; } = [];

        /// <summary>Why this iteration did not complete, where it did not.</summary>
        public List<string> Refusals { get; } = [];

        /// <summary>
        /// Whether this iteration produced a design that was simulated and assessed - and is therefore a
        /// step the optimisation could carry forward. False on an iteration that was refused, cancelled or
        /// could not be simulated.
        /// </summary>
        public bool IsCompleted { get; set; }

        public override string ToString()
        {
            return string.Format(
                "Run {0} ({1}): {2} targeted, {3} derived, TM59 {4}",
                Iteration,
                IsBaseline ? "baseline" : "optimisation round",
                TargetedAdjustments.Count,
                DerivedAdjustments.Count,
                Core.Query.Description(OccupiedSpaceComplianceStatus));
        }
    }
}
