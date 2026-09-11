// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.UI.WPF;
using System.Threading;
using System.Windows.Forms;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        /// <summary>
        /// The Approved Document O Iteration 3 (A/B) command: run a Candidate B for this session's
        /// completed Iteration 1a run, or reopen the pairing it already produced, and show the result.
        ///
        /// <para><b>One command, because it is one question</b></para>
        /// <para>
        /// Whether this runs or reviews is a property of the state the run is in, not a choice a person
        /// should have to make - and getting it wrong costs an hour of TAS or a stale answer. The
        /// eligibility authority decides, once, and the two paths below are the two existing methods.
        /// </para>
        ///
        /// <para><b>Nothing here decides anything else</b></para>
        /// <para>
        /// It constructs the production pipeline, hosts a progress dialog for the run, and shows the
        /// result window. Every stage, refusal, statistic and verdict belongs to
        /// <see cref="RunPartOIteration3(PartORun, IPartOIteration3Pipeline, CancellationToken)"/> and
        /// <see cref="ReviewPartOIteration3"/>.
        /// </para>
        /// <para>
        /// <b>The review path shows no progress dialog</b>, deliberately: it reads two results files that
        /// already exist and starts no TAS process, so a dialog saying "Simulating" would be a lie about
        /// what is happening.
        /// </para>
        /// </summary>
        /// <param name="partORun">The session's Part O run.</param>
        /// <param name="owner">Owner window for the dialogs.</param>
        public static void PartOIteration3(this PartORun? partORun, IWin32Window? owner = null)
        {
            if (partORun is null)
            {
                return;
            }

            PartOIteration3Eligibility partOIteration3Eligibility = Query.PartOIteration3Eligibility(partORun, partORun.IsAssessable(out string? refusal_Assessable), refusal_Assessable);

            if (!partOIteration3Eligibility.Available)
            {
                MessageBox.Show(string.Format("The Approved Document O Iteration 3 comparison is not available.\n\n{0}", partOIteration3Eligibility.Refusal));

                return;
            }

            IPartOIteration3Pipeline iPartOIteration3Pipeline = new PartOIteration3Pipeline();

            PartOIteration3Result partOIteration3Result;

            if (partOIteration3Eligibility.Review)
            {
                using (ProgressBarWindowManager progressBarWindowManager = new("Part O Iteration 3", "Reading the recorded pairing..."))
                {
                    partOIteration3Result = ReviewPartOIteration3(partORun, iPartOIteration3Pipeline);

                    progressBarWindowManager.Text = partOIteration3Result.IsComplete ? "Rebuilding the comparison..." : "Refused";
                }
            }
            else
            {
                //The TAS steps host their own cancellable progress dialogs - the no-IZAM workflow's and
                //the bridge's - so this adds none of its own around them. What it does add is the token,
                //so one Cancel aborts the whole pairing rather than one of its simulations.
                partOIteration3Result = RunPartOIteration3(partORun, iPartOIteration3Pipeline, CancellationToken.None);
            }

            PartOIteration3ResultWindow partOIteration3ResultWindow = new()
            {
                Result = partOIteration3Result,
            };

            if (owner is not null)
            {
                new System.Windows.Interop.WindowInteropHelper(partOIteration3ResultWindow).Owner = owner.Handle;
            }

            partOIteration3ResultWindow.ShowDialog();
        }
    }
}
