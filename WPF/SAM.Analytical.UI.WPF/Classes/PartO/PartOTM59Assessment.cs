// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Tas;
using System;
using System.Collections.Generic;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// One production CIBSE TM59 assessment of one completed Part O run, with every result resolved back to
    /// the <b>design</b> space it belongs to.
    ///
    /// <para><b>Why this exists as a class</b></para>
    /// <para>
    /// The assessment sequence - convert the TSD, build the calculator, map the scenarios, restore the
    /// design internal conditions, calculate, report - has two callers now: the command that shows an
    /// engineer the report, and the Iteration 2B optimisation that reads the verdicts to decide what to
    /// target next. Writing it twice would give the two a way to disagree about what the assessment said,
    /// which is the one thing neither may do.
    /// </para>
    ///
    /// <para><b>The assessment itself is entirely SAM's</b></para>
    /// <para>
    /// This performs the same sequence the accepted <c>Tas.TSDQueryTM59Results</c> component performs and
    /// computes no criterion, limit or verdict of its own. <see cref="Report"/> is the production
    /// <c>TM59AssessmentReport</c>, and every <see cref="SpaceResults"/> row carries that report's own
    /// <c>ComplianceStatus</c> verbatim - never a status re-derived from Actual and Limit, which would
    /// overrule the calculation for any room sitting exactly on its limit.
    /// </para>
    ///
    /// <para><b>Simulated results, design identities</b></para>
    /// <para>
    /// A TM59 result is produced for a <i>simulated</i> space, and an optimisation has to move a design
    /// terminal, which belongs to a <i>design</i> space. The translation is <c>SimulationSpaceMap</c>'s and
    /// it is by identity: a result whose simulated space does not resolve to exactly one design space is
    /// reported in <see cref="AssociationRefusals"/> and left out, never matched to a same-named room in
    /// another flat. That refusal is what stops an optimisation raising Flat 3's kitchen because Flat 2's
    /// failed.
    /// </para>
    ///
    /// <para><b>The model assessed is the workflow's</b></para>
    /// <para>
    /// The design side of the calculation is the model the TAS workflow <b>returned</b>. Only that one
    /// carries the current TAS zone identities the map matches on; a preparation output can still hold
    /// guids from an earlier round trip, which produces an incomplete map and a silent empty answer. This
    /// class takes the model it is given and never reads one back off disk, so the caller's own
    /// <c>PartORun.AnalyticalModel_Assessment</c> discipline is what governs.
    /// </para>
    /// </summary>
    public class PartOTM59Assessment
    {
        private PartOTM59Assessment(TM59AssessmentResult tM59AssessmentResult, TM59AssessmentReport tM59AssessmentReport, List<PartOTM59SpaceResult> spaceResults, List<string> associationRefusals, List<Guid> spaceGuids_Unassessed, string refusal)
        {
            Result = tM59AssessmentResult;
            Report = tM59AssessmentReport;
            SpaceResults = spaceResults ?? [];
            AssociationRefusals = associationRefusals ?? [];
            SpaceGuids_Unassessed = spaceGuids_Unassessed ?? [];
            Refusal = refusal;
        }

        /// <summary>The production assessment result, or null where none could be produced.</summary>
        public TM59AssessmentResult? Result { get; }

        /// <summary>The production report - the text an engineer reads, and the checks a policy reads.</summary>
        public TM59AssessmentReport? Report { get; }

        /// <summary>
        /// Every occupied-space criterion outcome, resolved to its design space. Natural- and
        /// mechanical-ventilation criteria both, distinguished by
        /// <see cref="PartOTM59SpaceResult.Mechanical"/> - a policy that may only optimise mechanical
        /// airflow has to be able to see that a natural failure exists without being able to mistake it for
        /// one it can act on.
        /// <para>
        /// Corridor and supplementary &gt;28 °C rows are deliberately not here. They are reported as a risk
        /// rather than as an occupied-space compliance verdict, and folding them in would state a regulatory
        /// failure TM59 does not make.
        /// </para>
        /// </summary>
        public List<PartOTM59SpaceResult> SpaceResults { get; }

        /// <summary>Spaces that could not be resolved between the simulation and the design, one sentence each.</summary>
        public List<string> AssociationRefusals { get; }

        /// <summary>
        /// The <b>design</b> spaces this assessment produced no result for - those whose simulated
        /// counterpart could not be resolved to exactly one design space, so they were excluded before the
        /// calculation ran.
        /// <para>
        /// <b>Why a caller must look at this before believing a pass.</b> The report's combined status is a
        /// verdict over the spaces that WERE assessed. Where an occupied room in scope failed to resolve it
        /// is dropped with a warning, and the remaining rooms can then all pass - so an unguarded reading of
        /// <see cref="OccupiedSpaceComplianceStatus"/> would announce that every eligible space passes on
        /// the strength of an assessment that never looked at one of them. The refusal is recorded in
        /// <see cref="AssociationRefusals"/> as prose; this is the same fact as identities, so a caller can
        /// act on it.
        /// </para>
        /// </summary>
        public List<Guid> SpaceGuids_Unassessed { get; }

        /// <summary>Why no assessment was produced at all, or null where one was.</summary>
        public string? Refusal { get; }

        /// <summary>Whether there is an assessment to read.</summary>
        public bool IsAssessed => Result is not null && Report is not null && Refusal is null;

        /// <summary>
        /// The production verdict over the occupied spaces, combined by the report itself.
        /// <b>Never recomputed</b> from <see cref="SpaceResults"/>.
        /// </summary>
        public TM59ComplianceStatus OccupiedSpaceComplianceStatus => Report?.OccupiedSpaceComplianceStatus ?? TM59ComplianceStatus.Undefined;

        /// <summary>
        /// Runs the production assessment.
        /// </summary>
        /// <param name="analyticalModel_Workflow">
        /// <b>The model the TAS workflow returned</b>, and nothing else - see the class documentation.
        /// </param>
        /// <param name="path_TSD">The results file that workflow wrote.</param>
        /// <param name="overheatingScenarios">
        /// The scenarios of the preparation this run was built on. They are authoritative over which TM59
        /// criterion applies to which space, and are never derived from an internal condition or a name.
        /// </param>
        public static PartOTM59Assessment Assess(AnalyticalModel? analyticalModel_Workflow, string? path_TSD, IEnumerable<OverheatingScenario>? overheatingScenarios)
        {
            if (analyticalModel_Workflow is null || string.IsNullOrWhiteSpace(path_TSD))
            {
                return new PartOTM59Assessment(null, null, null, null, null, "No workflow model or no results path was supplied, so nothing could be assessed.");
            }

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
                return new PartOTM59Assessment(null, null, null, null, null, string.Format("The simulation results at '{0}' could not be read.", path_TSD));
            }

            List<string> associationRefusals = [];

            //The design side of this call is the WORKFLOW model. Its spaces carry the zone guids TAS
            //stamped on the round trip, which is what the map matches on.
            TM59AssessmentCalculator tM59AssessmentCalculator = analyticalModel_TSD.TM59AssessmentCalculator(analyticalModel_Workflow);

            OverheatingScenarioMap overheatingScenarioMap = new(overheatingScenarios, analyticalModel_Workflow, tM59AssessmentCalculator.SimulationSpaceMap);
            tM59AssessmentCalculator.VentilationStrategyMap = overheatingScenarioMap.VentilationStrategyMap;

            associationRefusals.AddRange(overheatingScenarioMap.Refusals ?? []);

            tM59AssessmentCalculator.RestoreDesignInternalConditions();

            associationRefusals.AddRange(tM59AssessmentCalculator.AssociationRefusals);

            //Null spaces and null zones: the whole model, which for this calculator means every simulated
            //space that resolved to exactly one design space.
            List<Space> spaces = tM59AssessmentCalculator.Spaces(null, null);

            associationRefusals.AddRange(tM59AssessmentCalculator.AssociationRefusals);

            TM59AssessmentResult tM59AssessmentResult = tM59AssessmentCalculator.Calculate(spaces);
            if (tM59AssessmentResult is null)
            {
                return new PartOTM59Assessment(null, null, null, associationRefusals, null, string.Format("The simulation results at '{0}' could not be assessed.", path_TSD));
            }

            TM59AssessmentReport tM59AssessmentReport = new(tM59AssessmentResult, path_TSD);

            List<PartOTM59SpaceResult> spaceResults = ResolvedSpaceResults(tM59AssessmentReport, tM59AssessmentCalculator.SimulationSpaceMap, spaces, associationRefusals);

            //Design side, not simulation side: which rooms of the model being assessed produced nothing.
            List<Guid> spaceGuids_Unassessed = [];

            foreach (Space space_Design in analyticalModel_Workflow.GetSpaces() ?? [])
            {
                if (space_Design is not null && tM59AssessmentCalculator.SimulationSpaceMap?.Simulation(space_Design) is null)
                {
                    spaceGuids_Unassessed.Add(space_Design.Guid);
                }
            }

            return new PartOTM59Assessment(tM59AssessmentResult, tM59AssessmentReport, spaceResults, associationRefusals, spaceGuids_Unassessed, null);
        }

        /// <summary>
        /// Every occupied-space check of the report, carried over unchanged and keyed to the design space
        /// its simulated space resolves to.
        /// <para>
        /// A check whose <c>Reference</c> names no simulated space this assessment calculated, or whose
        /// simulated space does not resolve to exactly one design space, is <b>reported and dropped</b>. It
        /// is still in the report an engineer reads; what it must not be is an identity an automatic
        /// optimisation then acts on.
        /// </para>
        /// </summary>
        private static List<PartOTM59SpaceResult> ResolvedSpaceResults(TM59AssessmentReport tM59AssessmentReport, SimulationSpaceMap simulationSpaceMap, List<Space> spaces_Simulation, List<string> associationRefusals)
        {
            List<PartOTM59SpaceResult> result = [];

            Dictionary<string, Space> dictionary_Simulation = [];
            foreach (Space space_Simulation in spaces_Simulation ?? [])
            {
                if (space_Simulation is not null)
                {
                    dictionary_Simulation[space_Simulation.Guid.ToString()] = space_Simulation;
                }
            }

            Add(result, tM59AssessmentReport.MechanicalVentilationChecks, true, dictionary_Simulation, simulationSpaceMap, associationRefusals);
            Add(result, tM59AssessmentReport.NaturalVentilationChecks, false, dictionary_Simulation, simulationSpaceMap, associationRefusals);

            return result;
        }

        private static void Add(List<PartOTM59SpaceResult> result, List<TM59AssessmentReportCheck>? tM59AssessmentReportChecks, bool mechanical, Dictionary<string, Space> dictionary_Simulation, SimulationSpaceMap simulationSpaceMap, List<string> associationRefusals)
        {
            foreach (TM59AssessmentReportCheck tM59AssessmentReportCheck in tM59AssessmentReportChecks ?? [])
            {
                if (tM59AssessmentReportCheck is null)
                {
                    continue;
                }

                Space? space_Design = tM59AssessmentReportCheck.Reference is not null && dictionary_Simulation.TryGetValue(tM59AssessmentReportCheck.Reference, out Space? space_Simulation)
                    ? simulationSpaceMap?.Design(space_Simulation)
                    : null;

                if (space_Design is null)
                {
                    associationRefusals.Add(string.Format(
                        "The TM59 result for '{0}' ({1}) could not be resolved to exactly one design space, so it is reported but cannot be acted on automatically.",
                        tM59AssessmentReportCheck.SpaceName,
                        tM59AssessmentReportCheck.Check));

                    continue;
                }

                result.Add(new PartOTM59SpaceResult(
                    space_Design.Guid,
                    space_Design.Name,
                    tM59AssessmentReportCheck.Check,
                    tM59AssessmentReportCheck.Actual,
                    tM59AssessmentReportCheck.Limit,
                    tM59AssessmentReportCheck.ComplianceStatus,
                    mechanical));
            }
        }
    }
}
