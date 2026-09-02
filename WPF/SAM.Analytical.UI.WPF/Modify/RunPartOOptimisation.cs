// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.UI;
using System.Windows.Forms;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        /// <summary>
        /// The ribbon command behind the automatic Approved Document O Iteration 2B optimisation: run it,
        /// show its history, and adopt the last design that was actually valid.
        /// <para>
        /// <b>Orchestration only.</b> Every engineering decision belongs to
        /// <see cref="OptimisePartOTM59(PartORun, PartOOptimisationSettings, out string)"/> and, beneath it,
        /// to <c>SAM.Analytical</c>. This method chooses no target, no airflow and no stopping point.
        /// </para>
        /// <para>
        /// <b>The settings are the ones the run was prepared with.</b> They were stated in the preparation
        /// dialog, alongside the equipment selection they depend on, and carried on the run - so an
        /// optimisation cannot be run at a step nobody agreed to.
        /// </para>
        /// <para>
        /// <b>What is adopted is the last valid design</b> - the last iteration that was prepared, simulated
        /// over the full year and assessed. On a capacity stop that is the design one full step below the
        /// selected unit's ceiling; it is never a refused round, and never a round that was not simulated.
        /// </para>
        /// </summary>
        /// <param name="uIAnalyticalModel">The loaded model. Replaced by the last valid design on success.</param>
        /// <param name="partORun">The session's completed Iteration 2 run.</param>
        /// <param name="owner">Owner window for the dialogs.</param>
        public static void RunPartOOptimisation(this UIAnalyticalModel? uIAnalyticalModel, PartORun? partORun, IWin32Window? owner = null)
        {
            if (uIAnalyticalModel is null || partORun is null)
            {
                return;
            }

            PartOOptimisationSettings? partOOptimisationSettings = partORun.PreparationContext?.OptimisationSettings;

            //Refused BEFORE anything runs, because the alternative is spending minutes of TAS time to
            //discover the run was never an Iteration 2B starting point.
            if (!partORun.CanOptimise(partOOptimisationSettings, out string? refusal_CanOptimise))
            {
                MessageBox.Show(string.Format("The Part O Iteration 2B optimisation did not run.\n\n{0}", refusal_CanOptimise));

                return;
            }

            if (partOOptimisationSettings is null)
            {
                MessageBox.Show("This Part O run was not prepared with automatic TM59 optimisation enabled, so there is no airflow step or iteration limit to run it at. Prepare the iteration again with 'Automatically optimise TM59 failures' ticked.");

                return;
            }

            PartOOptimisationRun? partOOptimisationRun = partORun.OptimisePartOTM59(partOOptimisationSettings, out string? refusal);

            if (partOOptimisationRun is null)
            {
                MessageBox.Show(string.Format("The Part O Iteration 2B optimisation did not run.\n\n{0}", refusal));

                return;
            }

            PartOOptimisationResultWindow partOOptimisationResultWindow = new()
            {
                OptimisationRun = partOOptimisationRun,
            };

            if (owner is not null)
            {
                new System.Windows.Interop.WindowInteropHelper(partOOptimisationResultWindow).Owner = owner.Handle;
            }

            partOOptimisationResultWindow.ShowDialog();

            AnalyticalModel? analyticalModel_LastValid = partOOptimisationRun.AnalyticalModel_LastValid;
            if (analyticalModel_LastValid is null)
            {
                return;
            }

            //Armed only where the run still holds this very design as its completed one - which is every
            //clean stop, and none of the failed ones. Where the run was dropped mid-optimisation the
            //modification below is genuinely an outside edit as far as it is concerned, and it correctly
            //stays dropped: a design whose lineage broke must not remain assessable.
            if (partORun.State == PartORunState.WorkflowCompleted)
            {
                partORun.ExpectModification();
            }

            uIAnalyticalModel.SetJSAMObject(analyticalModel_LastValid, new FullModification());
        }
    }
}
