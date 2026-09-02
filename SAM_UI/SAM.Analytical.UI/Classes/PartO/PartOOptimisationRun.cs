// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.UI
{
    /// <summary>
    /// One automatic Approved Document O Iteration 2B optimisation, start to finish: the baseline it began
    /// from, every round it ran, why it stopped, and the last design that was actually valid.
    ///
    /// <para><b>Why the history is the deliverable, not the model</b></para>
    /// <para>
    /// An optimisation that handed back only its final model would answer "here is a design" and nothing
    /// else. The questions an engineer has afterwards are all about the path: which rooms were chosen and
    /// which only moved to keep a dwelling balanced, what each round asked for and got, what the units were
    /// carrying at each stage, which results file each iteration produced, and - above all - whether the
    /// process ran out of equipment or ran out of failures. None of that is recoverable from the final
    /// model, so it is recorded as it happens.
    /// </para>
    ///
    /// <para><b>What a pass at a fixed step is, and is not</b></para>
    /// <para>
    /// The optimisation raises the same step every round. A design that passes is therefore <b>the first
    /// tested passing design at the configured step</b>. It is <i>not</i> a minimum required airflow: no
    /// search was run between the last failing design and this one, so nothing here establishes that a
    /// smaller airflow would not also have passed. <see cref="Description"/> says so in those words, every
    /// time.
    /// </para>
    ///
    /// <para><b>The last valid design survives every stop</b></para>
    /// <para>
    /// <see cref="AnalyticalModel_LastValid"/> is the model of the last iteration that was prepared,
    /// simulated and assessed successfully - which on a
    /// <see cref="PartOOptimisationStopReason.CapacityReached"/> stop is the design one full step below the
    /// unit's ceiling, and never a partial or unsimulated round. A cancelled or failed iteration contributes
    /// a recorded step and no model, so it can never become a false successful result.
    /// </para>
    /// </summary>
    public class PartOOptimisationRun
    {
        public PartOOptimisationRun(PartOOptimisationSettings partOOptimisationSettings)
        {
            Settings = partOOptimisationSettings ?? new PartOOptimisationSettings();
        }

        /// <summary>The step and the iteration guard this run was given.</summary>
        public PartOOptimisationSettings Settings { get; }

        /// <summary>
        /// Every iteration, in order, starting with the baseline at index 0. A refused, cancelled or failed
        /// iteration is here too - what was attempted is part of the record.
        /// </summary>
        public List<PartOOptimisationStep> Steps { get; } = [];

        /// <summary>Why the optimisation stopped. <see cref="PartOOptimisationStopReason.Running"/> until it has.</summary>
        public PartOOptimisationStopReason StopReason { get; set; } = PartOOptimisationStopReason.Running;

        /// <summary>The stop reason in the run's own words - what happened, and what an engineer does next.</summary>
        public string StopDescription { get; set; }

        /// <summary>
        /// The model of the last iteration that completed - prepared, simulated with the full year and
        /// assessed. Null only where even the baseline could not be established.
        /// </summary>
        public AnalyticalModel AnalyticalModel_LastValid { get; set; }

        /// <summary>The results file of that same iteration.</summary>
        public string Path_TSD_LastValid { get; set; }

        /// <summary>The scenarios of that same iteration's preparation, so its assessment can be re-run.</summary>
        public List<OverheatingScenario> OverheatingScenarios_LastValid { get; } = [];

        /// <summary>The baseline, or null where the run never established one.</summary>
        public PartOOptimisationStep Step_Baseline => Steps.Count == 0 ? null : Steps[0];

        /// <summary>The last iteration that completed - the one <see cref="AnalyticalModel_LastValid"/> came from.</summary>
        public PartOOptimisationStep Step_LastValid => Steps.FindLast(x => x.IsCompleted);

        /// <summary>How many optimisation rounds ran, not counting the baseline.</summary>
        public int Rounds => Steps.Count == 0 ? 0 : Steps.Count - 1;

        /// <summary>Whether the optimisation ended with every eligible occupied space passing.</summary>
        public bool IsPassed => StopReason == PartOOptimisationStopReason.Passed;

        /// <summary>
        /// One paragraph stating what happened, in language that cannot be mistaken for a claim the run did
        /// not make.
        /// </summary>
        public string Description
        {
            get
            {
                PartOOptimisationStep partOOptimisationStep = Step_LastValid;

                string outcome = StopReason switch
                {
                    PartOOptimisationStopReason.Passed => string.Format(
                        "Every eligible occupied space passes its production TM59 criteria. This is the FIRST TESTED PASSING DESIGN at the configured {0:0.###} l/s step - it is not a minimum required airflow, because no search was run between the last failing design and this one.",
                        Settings.AirFlowStep_Lps),

                    PartOOptimisationStopReason.CapacityReached => string.Format(
                        "The selected ventilation unit cannot carry another full {0:0.###} l/s step, so the optimisation stopped at the last valid design. TM59 results that still fail are reported as they stand - a design limited by the equipment that was selected is a real answer, not a failure of the process. Selecting a larger product is a deliberate engineering decision and is never made automatically.",
                        Settings.AirFlowStep_Lps),

                    PartOOptimisationStopReason.NoEligibleTargets =>
                        "No failing space is left that this optimisation can move: either every remaining failure is outside the Part O dwelling scope, or it has no Approved Document O design terminal to raise. No terminal was invented to create one.",

                    PartOOptimisationStopReason.RebalanceRefused =>
                        "A design airflow round was refused, so no further design was produced. The last valid design is kept.",

                    PartOOptimisationStopReason.PreparationFailed =>
                        "The Part O iteration could not be re-prepared over the optimised design, so it was never simulated. The last valid design is kept.",

                    PartOOptimisationStopReason.SimulationFailed =>
                        "The TAS workflow did not produce the full-year results a TM59 assessment reads, so the round could not be assessed. The last valid design is kept.",

                    PartOOptimisationStopReason.AssessmentFailed =>
                        "The production TM59 assessment could not be produced for the simulated model, so the round's outcome is unknown. The last valid design is kept.",

                    PartOOptimisationStopReason.IterationLimitReached => string.Format(
                        "The safety limit of {0} optimisation iteration(s) was reached with eligible spaces still failing. Raise the limit, or reconsider the design or the selected equipment.",
                        Settings.MaximumIterations),

                    PartOOptimisationStopReason.Cancelled =>
                        "Cancelled. The last iteration that completed in full is kept; the cancelled one is recorded and is not a result.",

                    _ => "The optimisation has not finished.",
                };

                return string.Format(
                    "{0} optimisation round(s) after the baseline, at a {1:0.###} l/s step. Last valid design: run {2}. {3}{4}",
                    Rounds,
                    Settings.AirFlowStep_Lps,
                    partOOptimisationStep is null ? "none" : partOOptimisationStep.Iteration.ToString(),
                    outcome,
                    string.IsNullOrWhiteSpace(StopDescription) ? string.Empty : " " + StopDescription);
            }
        }
    }
}
