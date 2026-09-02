// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas;
using SAM.Core.UI.WPF;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        /// <summary>
        /// Assesses a completed Part O run against the CIBSE TM59 criteria and shows the production report.
        /// <para>
        /// <b>The model it assesses is the one the TAS workflow returned</b> -
        /// <see cref="PartORun.AnalyticalModel_Assessment"/>, which exists only in
        /// <see cref="PartORunState.WorkflowCompleted"/>. There is no fallback: not the loaded model, not the
        /// preparation output, not a model read back off disk. A TM59 query resolves a simulated space to a
        /// design space through <c>SpaceParameter.ZoneGuid</c>, and only the model the workflow returns carries
        /// the current TAS zone identities - handing it the preparation output produces an incomplete
        /// <c>SimulationSpaceMap</c> and refuses every space, which is a silent empty answer rather than an
        /// error.
        /// </para>
        /// <para>
        /// <b>The assessment itself is entirely SAM's.</b> This method performs the same sequence the accepted
        /// <c>Tas.TSDQueryTM59Results</c> component performs - <c>Convert.ToSAM(TSD)</c>,
        /// <c>Create.TM59AssessmentCalculator</c>, <c>OverheatingScenarioMap</c>,
        /// <c>RestoreDesignInternalConditions</c>, <c>Spaces</c>, <c>Calculate</c>,
        /// <c>TM59AssessmentReport</c> - and computes no criterion, limit or verdict of its own. The
        /// natural-ventilation criteria use their own summer and night subsets, and the mechanical criterion
        /// its annual one; none of those numbers is stated here.
        /// </para>
        /// <para>
        /// <b>Not the same thing as the simulation window's "Domestic Overheating" tick.</b> That writes the
        /// TAS DomOv XML for TAS to assess. This reads the TSD and produces SAM's own TM59 assessment.
        /// </para>
        /// </summary>
        public static void AssessPartOTM59(this PartORun partORun, IWin32Window? owner = null)
        {
            if (partORun is null)
            {
                return;
            }

            //One gate, and it re-checks the results file rather than trusting the state alone.
            if (!partORun.IsAssessable(out string refusal))
            {
                MessageBox.Show(string.Format("The Part O TM59 assessment did not run.\n\n{0}", refusal));

                return;
            }

            AnalyticalModel? analyticalModel_Workflow = partORun.AnalyticalModel_Assessment;
            string? path_TSD = partORun.Path_TSD;

            if (analyticalModel_Workflow is null || string.IsNullOrWhiteSpace(path_TSD))
            {
                //Unreachable through IsAssessable, and asserted rather than assumed: a null slipping through
                //here is exactly the substitution this command exists to make impossible.
                MessageBox.Show("The Part O run reports it can be assessed but carries no workflow model or no results path. Nothing was assessed.");

                return;
            }

            PartOTM59Assessment partOTM59Assessment;

            using (ProgressBarWindowManager progressBarWindowManager = new("Part O TM59", "Reading simulation results..."))
            {
                //The whole assessment, in the one place that owns it - so this command and the Iteration 2B
                //optimisation can never disagree about what TM59 said. See PartOTM59Assessment.
                partOTM59Assessment = PartOTM59Assessment.Assess(analyticalModel_Workflow, path_TSD, partORun.OverheatingScenarios);

                progressBarWindowManager.Text = partOTM59Assessment.IsAssessed ? "Assessing..." : "Failed";
            }

            if (!partOTM59Assessment.IsAssessed)
            {
                MessageBox.Show(string.Format("The simulation results at '{0}' could not be assessed.\n\n{1}", path_TSD, partOTM59Assessment.Refusal));

                return;
            }

            TM59AssessmentResult tM59AssessmentResult = partOTM59Assessment.Result!;
            TM59AssessmentReport tM59AssessmentReport = partOTM59Assessment.Report!;

            List<string> associationRefusals = partOTM59Assessment.AssociationRefusals;

            PartOTM59ResultWindow partOTM59ResultWindow = new()
            {
                //The production report text, verbatim.
                Report = tM59AssessmentReport.ToString(),
            };

            partOTM59ResultWindow.SetDiagnostics(associationRefusals, tM59AssessmentResult.VentilationStrategyRefusals);

            partOTM59ResultWindow.Summary = Summary(partORun, tM59AssessmentResult);

            if (owner is not null)
            {
                new System.Windows.Interop.WindowInteropHelper(partOTM59ResultWindow).Owner = owner.Handle;
            }

            partOTM59ResultWindow.ShowDialog();
        }

        private static string Summary(PartORun partORun, TM59AssessmentResult tM59AssessmentResult)
        {
            //Counts only. Which criterion applies, what its limit is and whether a space passes are all in the
            //report below, stated by the assessment itself.
            return string.Format("Assessed {0} space(s) from '{1}': {2} natural ventilation result(s), {3} mechanical ventilation result(s), {4} corridor result(s). The model assessed is the one the TAS workflow returned.",
                tM59AssessmentResult.Spaces?.Count ?? 0,
                partORun.Path_TSD,
                tM59AssessmentResult.NaturalVentilationResults?.Count ?? 0,
                tM59AssessmentResult.MechanicalVentilationResults?.Count ?? 0,
                tM59AssessmentResult.CorridorResults?.Count ?? 0);
        }
    }
}
