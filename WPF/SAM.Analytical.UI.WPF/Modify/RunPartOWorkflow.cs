// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.UI;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        /// <summary>
        /// The high-level Approved Document O command: one dialog that says what the model already provides,
        /// and one button that carries a scenario and a dwelling scope all the way to a reviewed TM59 result.
        ///
        /// <para><b>Orchestration only - there is no second Part O implementation here</b></para>
        /// <para>
        /// Every stage is an existing command, called in the order they have always run in:
        /// </para>
        /// <list type="number">
        /// <item><see cref="PreparePartOIteration(UIAnalyticalModel, PartORun, PartOWorkflowRequest, VentilationUnitCatalogue, IWin32Window)"/>
        /// - which is <c>SAM.Analytical.Modify.PreparePartOIteration</c> plus the summary a person accepts it
        /// on, and which the Prepare Iteration picker also calls.</item>
        /// <item><see cref="Simulate(UIAnalyticalModel, PartORun, bool)"/> - the Simulate dialog, and beneath it
        /// <see cref="RunPartOSimulation"/>, which is where <c>SAMAnalytical.Check</c> gates the normalized
        /// model before TAS converts it and where the run is completed.</item>
        /// <item><see cref="AssessPartOTM59(PartORun, IWin32Window)"/> - the production TM59 assessment over
        /// the model the workflow returned.</item>
        /// </list>
        /// <para>
        /// Nothing is inlined, nothing is reimplemented, and no engineering decision is taken here. A Part O
        /// behaviour that changes in one of those methods changes here with it.
        /// </para>
        ///
        /// <para><b>Already-valid work is reused, and the model is what says so</b></para>
        /// <para>
        /// Where the session's run is already prepared for exactly this scenario and dwelling scope -
        /// <see cref="PartOWorkflowInspection.ReusePreparation"/>, decided by comparing the request against
        /// the preparation's own <see cref="PartOPreparationContext"/> - the preparation is skipped and the
        /// prepared model is simulated as it stands. Anything else differing prepares again. There is no
        /// UI-side cache: the run and the model are re-read on every showing of the dialog.
        /// </para>
        ///
        /// <para><b>Existing results are reviewed, never re-run</b></para>
        /// <para>
        /// Review Results and Optimise (2B) are the same two commands the Results tab exposes, gated by the
        /// same two authorities - <c>PartORun.IsAssessable</c> and <c>Modify.CanOptimise</c> - so a reopened
        /// <c>.sam</c> whose provenance validates is reviewable here without a TAS run, and one whose
        /// provenance does not is refused here for the reason that authority gives.
        /// </para>
        ///
        /// <para><b>The dialog is a hub, not a wizard</b></para>
        /// <para>
        /// It reopens after each action with its status rebuilt, so the run a person just completed is
        /// visible as READY and the follow-on 2B becomes available in the place they were already looking.
        /// The choices they made are carried across; the state is not - it is re-inspected every time.
        /// </para>
        /// </summary>
        /// <param name="uIAnalyticalModel">The loaded model. Replaced only by the commands below, on their own existing terms.</param>
        /// <param name="partORun">The session's Part O run.</param>
        /// <param name="owner">Owner window for the dialogs.</param>
        public static void RunPartOWorkflow(this UIAnalyticalModel? uIAnalyticalModel, PartORun partORun, IWin32Window? owner = null)
        {
            if (uIAnalyticalModel?.JSAMObject is null || partORun is null)
            {
                return;
            }

            //Read once, outside the loop: the read touches a file, and the dialog rebuilds its status on
            //every keystroke. Re-reading between rounds could also change what a selected unit is understood
            //to be rated at halfway through a session.
            VentilationUnitCatalogue ventilationUnitCatalogue = VentilationUnitCatalogue.Read();

            PartOWorkflowScenario? partOWorkflowScenario = null;
            PartOWorkflowScope partOWorkflowScope = PartOWorkflowScope.AllDwellings;
            List<Guid>? guids_Dwelling = null;
            PartOOptimisationSettings? partOOptimisationSettings = null;

            while (true)
            {
                PartOWorkflowWindow partOWorkflowWindow = new()
                {
                    //Order matters: the model builds the dwelling list the restored selection is applied to.
                    AnalyticalModel = uIAnalyticalModel.JSAMObject,
                    PartORun = partORun,
                    VentilationUnitCatalogue = ventilationUnitCatalogue,
                    Capabilities = Capabilities(partORun),
                };

                partOWorkflowWindow.Restore(partOWorkflowScenario, partOWorkflowScope, guids_Dwelling, partOOptimisationSettings);

                //Setting up is finished, so the ONE inspection this whole gesture owes is paid here - over
                //the fully restored state, which is the only one anybody will ever see. Every line above
                //moves an inspection input, and each used to be answered with an inspection of its own: nine
                //passes over the dwelling scope to show one window, on a model that may carry five thousand
                //spaces. Nothing is skipped and nothing is remembered; see CompleteInitialisation.
                partOWorkflowWindow.CompleteInitialisation();

                if (owner is not null)
                {
                    new System.Windows.Interop.WindowInteropHelper(partOWorkflowWindow).Owner = owner.Handle;
                }

                bool? showDialog = partOWorkflowWindow.ShowDialog();

                //Carried whether the dialog was accepted or closed, so reopening the command in the same
                //session does not start over.
                partOWorkflowScenario = partOWorkflowWindow.Scenario;
                partOWorkflowScope = partOWorkflowWindow.Scope;
                partOOptimisationSettings = partOWorkflowWindow.OptimisationSettings;

                guids_Dwelling = [];
                foreach (Zone zone in partOWorkflowWindow.Zones_Dwelling)
                {
                    guids_Dwelling.Add(zone.Guid);
                }

                if (showDialog is null || !showDialog.Value)
                {
                    return;
                }

                switch (partOWorkflowWindow.Action)
                {
                    case PartOWorkflowAction.PrepareAndRun:
                        PrepareAndRun(uIAnalyticalModel, partORun, partOWorkflowWindow.Request, partOWorkflowWindow.Inspection, ventilationUnitCatalogue, owner);
                        break;

                    case PartOWorkflowAction.ReviewResults:
                        AssessPartOTM59(partORun, owner);
                        break;

                    case PartOWorkflowAction.Optimise:
                        RunPartOOptimisation(uIAnalyticalModel, partORun, owner);
                        break;

                    default:
                        return;
                }
            }
        }

        /// <summary>
        /// Prepare - or reuse a preparation - then simulate, then assess. The three existing commands, in the
        /// one order they have always run in.
        /// <para>
        /// <b>Each step is gated by the previous one's own outcome</b>, read off the run rather than assumed.
        /// A preparation the user declined leaves the run unprepared and nothing is simulated; a simulation
        /// that was cancelled, refused by the pre-simulation check or not a full year leaves the run
        /// uncompleted - with its own reason already shown - and nothing is assessed.
        /// </para>
        /// <para>
        /// <b>Every dialog those commands own is still shown.</b> The Simulate window in particular: the
        /// weather file, the output directory and the full-year range are the run's own inputs, and a
        /// high-level command that chose them silently would be choosing engineering inputs.
        /// </para>
        /// <para>
        /// <b>The reuse path records the current Iteration 2B choice.</b> A reused preparation is the same
        /// ENGINEERING case, which is what <see cref="PartOWorkflowInspection.ReusePreparation"/> matches on;
        /// the 2B step and limit are not engineering and were therefore not matched - but they are read off
        /// the run afterwards by <c>Modify.CanOptimise</c>, so they have to be the ones this invocation asked
        /// for. <see cref="PartORun.AdoptOptimisationSettings"/> is that record, and where the run will not
        /// take it the preparation is rebuilt instead - correctness before saving a preparation.
        /// </para>
        /// </summary>
        private static void PrepareAndRun(UIAnalyticalModel uIAnalyticalModel, PartORun partORun, PartOWorkflowRequest partOWorkflowRequest, PartOWorkflowInspection? partOWorkflowInspection, VentilationUnitCatalogue ventilationUnitCatalogue, IWin32Window? owner)
        {
            //Reused only where the run's own record of what it was prepared with describes this request.
            //Otherwise prepared again - including where an iteration is prepared for something else, which
            //would otherwise be simulated as though it were this one.
            if (!ReuseWithCurrentOptimisation(partORun, partOWorkflowRequest, partOWorkflowInspection?.ReusePreparation ?? false))
            {
                if (!PreparePartOIteration(uIAnalyticalModel, partORun, partOWorkflowRequest, ventilationUnitCatalogue, owner))
                {
                    //Refused, declined or not adopted. Every one of those has already told the user why.
                    return;
                }
            }

            //Not an assumption: PreparePartOIteration returns true only for an adopted preparation, and the
            //reuse path was reached only from a Prepared run - but a model modified between the inspection
            //and here drops the run, and simulating an unprepared run would silently produce a result that
            //cannot be assessed.
            if (partORun.State != PartORunState.Prepared)
            {
                MessageBox.Show(string.Format("The Part O iteration is no longer prepared, so nothing was simulated.\n\n{0}", partORun.InvalidationReason ?? "Prepare the iteration again."));

                return;
            }

            //The Simulate dialog, the SAM Check gate, the TAS workflow, the run completion and the run's own
            //persisted evidence - all of it already lives here.
            //
            //The third argument is what makes the dialog a PART O dialog: the annual full-year case chosen
            //rather than inherited from whatever the manual command last ran, and the settings that are not
            //Part O decisions locked so an accepted dialog cannot produce a run that is refused after the
            //TAS time has been spent. The weather, the output directory, the project name and the solar
            //method stay open, because those are the run's own inputs. See Create.SimulateOptions_PartO.
            Simulate(uIAnalyticalModel, partORun, true);

            if (!partORun.CanAssess)
            {
                //Simulate has already shown what happened - a cancellation, a partial year, a refused
                //pre-simulation check. Saying it twice in different words would be worse than saying nothing.
                return;
            }

            AssessPartOTM59(partORun, owner);
        }

        /// <summary>
        /// Settles the reuse decision for one invocation of Prepare &amp; Run - and, where the preparation is
        /// reused, records on the run the Iteration 2B choice this invocation actually asked for.
        ///
        /// <para><b>Why the record has to be written here</b></para>
        /// <para>
        /// <see cref="PartOWorkflowInspection.ReusePreparation"/> matches the ENGINEERING case: the
        /// iteration, the dwelling scope, the stated routes, the catalogue, the isolation - everything that
        /// reached <c>SAM.Analytical.Modify.PreparePartOIteration</c> and therefore decides whether the
        /// prepared model is the same model. The Iteration 2B step and limit are the one thing on the
        /// preparation context that preparation neither reads nor is affected by, so they are deliberately
        /// not matched - but <c>Modify.CanOptimise</c> reads them off the run afterwards. Skipping the
        /// preparation therefore skipped the only thing that used to record them, and a user who ticked,
        /// unticked or retuned 2B and pressed Run got the choice made at the earlier preparation.
        /// </para>
        ///
        /// <para><b>Why not simply match on them and re-prepare</b></para>
        /// <para>
        /// Because it would rebuild an analytical preparation - and on an isolated run re-derive its
        /// geometry and re-cut its adiabatic interfaces - to change two numbers nothing in that preparation
        /// looks at. Correctness does not require it: the run's own record is what is wrong, and
        /// <see cref="PartORun.AdoptOptimisationSettings"/> is the transition that fixes exactly that.
        /// </para>
        ///
        /// <para><b>Correctness first where they disagree</b></para>
        /// <para>
        /// Where the run will not take the record it was not a reuse target after all, and this returns
        /// false so the caller prepares again. A redundant preparation is always right; a run whose recorded
        /// optimisation is not the one that was asked for is not.
        /// </para>
        ///
        /// <para><b>Nothing is written unless the preparation is reused</b>, and nothing is written during
        /// inspection: on the prepare-again path the new preparation records the choice itself, and a status
        /// refresh must not change the run it is describing.</para>
        /// </summary>
        /// <returns>Whether the existing preparation may be reused. False means prepare it again.</returns>
        internal static bool ReuseWithCurrentOptimisation(PartORun partORun, PartOWorkflowRequest partOWorkflowRequest, bool reuse)
        {
            if (!reuse || partORun is null)
            {
                return false;
            }

            return partORun.AdoptOptimisationSettings(partOWorkflowRequest?.OptimisationSettings);
        }

        /// <summary>
        /// The session facts the status list needs and no model can answer, each taken from the authority
        /// that owns it.
        /// <para>
        /// <b>Asked once per showing of the dialog, deliberately.</b> <c>PartORun.IsAssessable</c> re-stats
        /// the results file and can drop a run whose results have gone - the real gate, and not something a
        /// status list may do on every keystroke. <c>Modify.CanOptimise</c> reads it in turn.
        /// </para>
        /// <para>
        /// The Iteration 2B answer additionally requires the settings to be there at all, matching
        /// <see cref="RunPartOOptimisation"/>'s own second gate: <c>CanOptimise</c> accepts null settings as
        /// "nothing to validate", but the command cannot run without a step and a limit.
        /// </para>
        /// </summary>
        private static PartOWorkflowCapabilities Capabilities(PartORun? partORun)
        {
            PartOWorkflowCapabilities result = new();

            if (partORun is null)
            {
                return result;
            }

            result.ResultsAvailable = partORun.IsAssessable(out string refusal_Results);
            result.ResultsRefusal = refusal_Results;
            result.ResultsRestored = partORun.IsRestored;
            result.Path_Results = partORun.Path_TSD;

            PartOOptimisationSettings? partOOptimisationSettings = partORun.PreparationContext?.OptimisationSettings;

            result.OptimisationAvailable = partORun.CanOptimise(partOOptimisationSettings, out string? refusal_Optimisation) && partOOptimisationSettings is not null;

            result.OptimisationRefusal = refusal_Optimisation ?? (partOOptimisationSettings is null
                ? "This Part O run was not prepared with automatic TM59 optimisation enabled, so there is no airflow step or iteration limit to run it at. Prepare and run the iteration again with the follow-on optimisation ticked."
                : null);

            return result;
        }
    }
}
