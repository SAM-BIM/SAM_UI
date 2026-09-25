// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        /// <summary>
        /// The six phases an Iteration 3 run is shown as - the ledger's fifteen stages, in the engineer's
        /// terms. Every stage maps to exactly one phase, in order.
        /// </summary>
        internal static readonly IReadOnlyList<string> PartOIteration3Phases =
        [
            "Reference case",
            "System case design",
            "TAS building simulation",
            "TAS Systems simulation",
            "TAS resultant temperature",
            "TM59 comparison and reports",
        ];

        /// <summary>Which of <see cref="PartOIteration3Phases"/> a ledger stage belongs to.</summary>
        internal static int PartOIteration3Phase(PartOIteration3Stage partOIteration3Stage)
        {
            return partOIteration3Stage switch
            {
                PartOIteration3Stage.Input or PartOIteration3Stage.ReferenceA or PartOIteration3Stage.ReferenceATM59 => 0,
                PartOIteration3Stage.SystemScope or PartOIteration3Stage.EquipmentResolution or PartOIteration3Stage.Materialisation => 1,
                PartOIteration3Stage.ThermalSource => 2,
                PartOIteration3Stage.SystemsConversion or PartOIteration3Stage.SystemsSimulation or PartOIteration3Stage.ZoneTemperature => 3,
                PartOIteration3Stage.ResultantTemperature => 4,
                _ => 5,
            };
        }

        /// <summary>
        /// Runs the Approved Document O Iteration 3 system case for one method against the session's completed
        /// run, then shows the comparison.
        ///
        /// <para><b>What it asks, and when</b></para>
        /// <list type="bullet">
        /// <item>Nothing where the method can simply run. Which method, and what it will change, was chosen and
        /// shown in the Hub before this was called.</item>
        /// <item>A confirmation where the method already has a completed result, because running it again
        /// replaces that result - a destructive decision, and the one kind of question this keeps.</item>
        /// </list>
        /// <para>
        /// <b>Progress</b> is one window for the whole run (<see cref="PartOProgressHost"/>): six phases, the
        /// elapsed time, and Cancel between stages. It closes before the comparison opens. There is no
        /// completion message box: the comparison IS the completion, and the Hub states it inline afterwards.
        /// </para>
        /// </summary>
        /// <returns>The line the Hub shows about what happened, or null where nothing was run.</returns>
        public static PartOWorkflowOutcome? RunPartOIteration3Case(this PartORun? partORun, PartOIteration3BehaviourMode partOIteration3BehaviourMode, IWin32Window? owner = null)
        {
            if (partORun is null)
            {
                return null;
            }

            PartOIteration3Eligibility partOIteration3Eligibility = Query.PartOIteration3Eligibility(partORun, partORun.IsAssessable(out string? refusal_Assessable), refusal_Assessable);

            if (!partOIteration3Eligibility.CanRun)
            {
                MessageBox.Show(string.Format("Iteration 3 cannot run for this reference case.\n\n{0}", partOIteration3Eligibility.Refusal_Run), "Part O — Iteration 3");

                return null;
            }

            string label = Query.PartOIteration3MethodLabel(partOIteration3BehaviourMode);

            PartOIteration3PairingStatus partOIteration3PairingStatus = partOIteration3Eligibility.PairingStatus(partOIteration3BehaviourMode);

            if (partOIteration3PairingStatus.IsReviewable)
            {
                DialogResult dialogResult = MessageBox.Show(
                    string.Format(
                        "A completed Iteration 3 result for '{0}' already exists{1}.\n\nRunning it again replaces that result. It takes several minutes of TAS.\n\nRun it again?",
                        label,
                        partOIteration3PairingStatus.When.HasValue ? string.Format(CultureInfo.CurrentCulture, " ({0:d MMM yyyy HH:mm})", partOIteration3PairingStatus.When.Value) : string.Empty),
                    "Part O — Iteration 3",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2);

                if (dialogResult != DialogResult.Yes)
                {
                    return null;
                }
            }

            IPartOIteration3Pipeline iPartOIteration3Pipeline = new PartOIteration3Pipeline();

            string reference = Query.PartOIterationText(partORun);

            PartOIteration3Result partOIteration3Result;
            List<string> lines_Stages;
            TimeSpan elapsed;

            using (PartOProgressHost partOProgressHost = new(string.Format("Iteration 3 — {0}", label), string.Format("Reference case: {0}", reference), PartOIteration3Phases))
            {
                partOIteration3Result = RunPartOIteration3(
                    partORun,
                    iPartOIteration3Pipeline,
                    partOProgressHost.Token,
                    partOIteration3BehaviourMode,
                    partOIteration3Stage => partOProgressHost.Start(PartOIteration3Phase(partOIteration3Stage)));

                if (partOIteration3Result.IsComplete)
                {
                    partOProgressHost.State.Complete();
                }
                else
                {
                    partOProgressHost.State.Fail();
                }

                lines_Stages = partOProgressHost.State.Lines();
                elapsed = partOProgressHost.State.Elapsed;
            }

            ShowPartOIteration3Result(partORun, partOIteration3Result, lines_Stages, owner);

            if (partOIteration3Result.IsComplete)
            {
                return new PartOWorkflowOutcome(
                    PartOWorkflowOutcomeKind.Success,
                    string.Format(
                        "✓ Iteration 3 complete · {0} · {1} · reference {2} / system {3}",
                        label,
                        PartOProgressState.Format(elapsed),
                        Verdict(partOIteration3Result.Assessment_ReferenceA),
                        Verdict(partOIteration3Result.Assessment_CandidateB)));
            }

            PartOIteration3Stage? partOIteration3Stage_Refused = partOIteration3Result.Ledger.Stage_Refused;

            return new PartOWorkflowOutcome(
                PartOWorkflowOutcomeKind.Warning,
                string.Format(
                    "! Iteration 3 did not complete · {0} · stopped at {1} after {2}. It can be run again.",
                    label,
                    partOIteration3Stage_Refused.HasValue ? Core.Query.Description(partOIteration3Stage_Refused.Value).ToLowerInvariant() : "an unrecorded stage",
                    PartOProgressState.Format(elapsed)));
        }

        /// <summary>
        /// Opens one method's saved Iteration 3 result - rebuilt from the existing results, with no TAS - or,
        /// where the method's last attempt did not complete, that attempt's stage record.
        /// </summary>
        public static PartOWorkflowOutcome? ReviewPartOIteration3Case(this PartORun? partORun, PartOIteration3BehaviourMode partOIteration3BehaviourMode, IWin32Window? owner = null)
        {
            if (partORun is null)
            {
                return null;
            }

            IPartOIteration3Pipeline iPartOIteration3Pipeline = new PartOIteration3Pipeline();

            string label = Query.PartOIteration3MethodLabel(partOIteration3BehaviourMode);

            PartOIteration3Result partOIteration3Result;
            TimeSpan elapsed;

            //No Cancel: this reads two existing results files and starts no TAS process.
            using (PartOProgressHost partOProgressHost = new(
                string.Format("Iteration 3 — {0}", label),
                "Opening the saved result. No TAS simulation is run.",
                ["Read the saved result", "Re-assess the reference case", "Re-assess the system case", "Rebuild the comparison"],
                false))
            {
                int index = 0;

                partOIteration3Result = ReviewPartOIteration3(partORun, iPartOIteration3Pipeline, partOIteration3BehaviourMode, text => partOProgressHost.Start(index++));

                partOProgressHost.State.Complete();

                elapsed = partOProgressHost.State.Elapsed;
            }

            ShowPartOIteration3Result(partORun, partOIteration3Result, null, owner);

            return partOIteration3Result.IsComplete
                ? new PartOWorkflowOutcome(
                    PartOWorkflowOutcomeKind.Information,
                    string.Format(
                        "○ Opened the saved Iteration 3 result · {0} · reference {1} / system {2} · no TAS run ({3})",
                        label,
                        Verdict(partOIteration3Result.Assessment_ReferenceA),
                        Verdict(partOIteration3Result.Assessment_CandidateB),
                        PartOProgressState.Format(elapsed)))
                : null;
        }

        /// <summary>
        /// The command the Hub offered before per-method results existed, kept for callers of that era: it
        /// opens the first method with a completed result, and otherwise runs the route check - the method that
        /// needs no product, and what the old picker defaulted to.
        /// </summary>
        public static void PartOIteration3(this PartORun? partORun, IWin32Window? owner = null)
        {
            if (partORun is null)
            {
                return;
            }

            PartOIteration3Eligibility partOIteration3Eligibility = Query.PartOIteration3Eligibility(partORun, partORun.IsAssessable(out string? refusal_Assessable), refusal_Assessable);

            PartOIteration3PairingStatus? partOIteration3PairingStatus_Reviewable = partOIteration3Eligibility.PairingStatuses.Find(x => x.IsReviewable);

            if (partOIteration3PairingStatus_Reviewable is not null)
            {
                ReviewPartOIteration3Case(partORun, partOIteration3PairingStatus_Reviewable.BehaviourMode, owner);

                return;
            }

            RunPartOIteration3Case(partORun, PartOIteration3BehaviourMode.Parity, owner);
        }

        private static void ShowPartOIteration3Result(PartORun partORun, PartOIteration3Result partOIteration3Result, List<string>? lines_Stages, IWin32Window? owner)
        {
            List<PartOIteration3GuidanceEvidence> guidance = Query.PartOIteration3GuidanceEvidence(partORun, partOIteration3Result.Record, VentilationUnitCatalogue.Read(), out string? source_Guidance);

            PartOIteration3ResultWindow partOIteration3ResultWindow = new()
            {
                ReferenceText = Query.PartOIterationText(partORun),
                StageTimings = lines_Stages,
                Guidance = guidance,
                GuidanceSource = source_Guidance,
                Result = partOIteration3Result,
            };

            if (owner is not null)
            {
                new System.Windows.Interop.WindowInteropHelper(partOIteration3ResultWindow).Owner = owner.Handle;
            }

            partOIteration3ResultWindow.ShowDialog();
        }

        private static string Verdict(PartOIteration3Assessment? partOIteration3Assessment)
        {
            return partOIteration3Assessment is null || !partOIteration3Assessment.IsAssessed
                ? "—"
                : Core.Query.Description(partOIteration3Assessment.OccupiedSpaceComplianceStatus);
        }
    }
}
