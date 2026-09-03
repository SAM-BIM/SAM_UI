// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas;
using SAM.Core.UI.WPF;
using System.Collections.Generic;
using System.Linq;
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

            //The durable artifact: every assessment of a run persists its report beside that run's own
            //results, whether it was produced in-session or reviewed from a reopened model. A failure to
            //write is reported, never silent - but it fails nothing: the assessment itself is already done.
            bool reportSaved = SavePartOTM59Report(path_TSD, tM59AssessmentReport, out string? path_TM59Report, out string? refusal_Report);

            List<string> associationRefusals = partOTM59Assessment.AssociationRefusals;

            PartOTM59ResultWindow partOTM59ResultWindow = new()
            {
                //The production report text, verbatim.
                Report = tM59AssessmentReport.ToString(),
            };

            partOTM59ResultWindow.SetDiagnostics(associationRefusals, tM59AssessmentResult.VentilationStrategyRefusals);

            partOTM59ResultWindow.Summary = Summary(partORun, tM59AssessmentResult, reportSaved, path_TM59Report, refusal_Report);

            if (owner is not null)
            {
                new System.Windows.Interop.WindowInteropHelper(partOTM59ResultWindow).Owner = owner.Handle;
            }

            partOTM59ResultWindow.ShowDialog();
        }

        /// <summary>
        /// The one-line account of what the assessment covered. <b>"Processed" is the simulated spaces the
        /// calculation ran over; "assessed" is the ones that produced a result.</b> The two differ where a
        /// space reached the calculation but no criterion produced a verdict for it - the corridor that is
        /// processed and named as not assessed, for instance - and wording them as one ("Assessed 9
        /// space(s)") read as though every one of the nine had been.
        /// </summary>
        private static string Summary(PartORun partORun, TM59AssessmentResult tM59AssessmentResult, bool reportSaved, string? path_TM59Report, string? refusal_Report)
        {
            return Summary(
                tM59AssessmentResult.Spaces?.Count ?? 0,
                partORun.Path_TSD,
                tM59AssessmentResult.NaturalVentilationResults,
                tM59AssessmentResult.MechanicalVentilationResults,
                tM59AssessmentResult.CorridorResults,
                reportSaved,
                path_TM59Report,
                refusal_Report);
        }

        /// <summary>
        /// The same sentence from the plain counts and lists - separated from <c>TM59AssessmentResult</c> so
        /// the wording can be pinned by tests without constructing one (its constructor is internal to
        /// SAM.Analytical).
        /// </summary>
        /// <remarks>Internal rather than private so the wording is pinned by tests.</remarks>
        internal static string Summary(int processed, string? path_TSD, IEnumerable<TMResult>? naturalVentilationResults, IEnumerable<TMResult>? mechanicalVentilationResults, IEnumerable<TMResult>? corridorResults, bool reportSaved, string? path_TM59Report, string? refusal_Report)
        {
            //Counts only. Which criterion applies, what its limit is and whether a space passes are all in the
            //report below, stated by the assessment itself.
            List<TMResult> naturalVentilation = [.. naturalVentilationResults ?? []];
            List<TMResult> mechanicalVentilation = [.. mechanicalVentilationResults ?? []];
            List<TMResult> corridor = [.. corridorResults ?? []];

            //One result per space, but counted as distinct references rather than assumed: the count is the
            //answer to "how many spaces were assessed", and assuming one-result-per-space is how a duplicate
            //would hide.
            HashSet<string> references = [];
            foreach (TMResult tMResult in naturalVentilation.Concat(mechanicalVentilation).Concat(corridor))
            {
                if (!string.IsNullOrWhiteSpace(tMResult?.Reference))
                {
                    references.Add(tMResult!.Reference);
                }
            }

            int assessed = references.Count;

            string result = string.Format(
                "Processed {0} simulated space(s) from '{1}': {2} assessed ({3} natural ventilation, {4} mechanical ventilation, {5} corridor), {6} not assessed. The model assessed is the one the TAS workflow returned.",
                processed,
                path_TSD,
                assessed,
                naturalVentilation.Count,
                mechanicalVentilation.Count,
                corridor.Count,
                processed - assessed);

            result += reportSaved
                ? string.Format(" The report was saved to '{0}'.", path_TM59Report)
                : string.Format(" {0}", refusal_Report);

            return result;
        }
    }
}
