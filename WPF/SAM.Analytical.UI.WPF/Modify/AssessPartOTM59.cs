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

            TM59AssessmentResult? tM59AssessmentResult = null;
            TM59AssessmentReport? tM59AssessmentReport = null;
            List<string> associationRefusals = [];

            using (ProgressBarWindowManager progressBarWindowManager = new("Part O TM59", "Reading simulation results..."))
            {
                //The same conversion settings the production query uses - the two series the assessment reads,
                //plus the zones and weather data it needs.
                TSDConversionSettings tSDConversionSettings = new()
                {
                    SpaceDataTypes = new HashSet<SpaceDataType>() { SpaceDataType.ResultantTemperature, SpaceDataType.OccupantSensibleGain },
                    ConvertWeaterData = true,
                    ConvertZones = true
                };

                AnalyticalModel analyticalModel_TSD = Analytical.Tas.Convert.ToSAM(path_TSD, tSDConversionSettings);
                if (analyticalModel_TSD is null)
                {
                    progressBarWindowManager.Text = "Failed";
                }
                else
                {
                    progressBarWindowManager.Text = "Assessing...";

                    //The design side of this call is the WORKFLOW model. Its spaces carry the zone guids TAS
                    //stamped on the round trip, which is what the map matches on.
                    TM59AssessmentCalculator tM59AssessmentCalculator = analyticalModel_TSD.TM59AssessmentCalculator(analyticalModel_Workflow);

                    //Authoritative over the TM59 criterion, and stated by the scenarios of the preparation
                    //this run was built on - not derived from an internal condition or a zone name.
                    OverheatingScenarioMap overheatingScenarioMap = new(partORun.OverheatingScenarios, analyticalModel_Workflow, tM59AssessmentCalculator.SimulationSpaceMap);
                    tM59AssessmentCalculator.VentilationStrategyMap = overheatingScenarioMap.VentilationStrategyMap;

                    associationRefusals.AddRange(overheatingScenarioMap.Refusals ?? []);

                    tM59AssessmentCalculator.RestoreDesignInternalConditions();

                    associationRefusals.AddRange(tM59AssessmentCalculator.AssociationRefusals);

                    //Null spaces and null zones: the whole model, which for this calculator means every
                    //simulated space that resolved to exactly one design space.
                    List<Space> spaces = tM59AssessmentCalculator.Spaces(null, null);

                    associationRefusals.AddRange(tM59AssessmentCalculator.AssociationRefusals);

                    tM59AssessmentResult = tM59AssessmentCalculator.Calculate(spaces);

                    if (tM59AssessmentResult is not null)
                    {
                        tM59AssessmentReport = new TM59AssessmentReport(tM59AssessmentResult, path_TSD);
                    }
                }
            }

            if (tM59AssessmentResult is null || tM59AssessmentReport is null)
            {
                MessageBox.Show(string.Format("The simulation results at '{0}' could not be assessed.", path_TSD));

                return;
            }

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
