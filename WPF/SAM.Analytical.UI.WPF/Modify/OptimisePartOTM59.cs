// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        /// <summary>
        /// Runs the automatic Approved Document O <b>Iteration 2B</b> optimisation over a completed
        /// Iteration 2 run: raise the design airflow of every eligible failing mechanical space by a fixed
        /// step, rebalance, rebuild the Part O state, re-simulate the same full year, reassess with
        /// production TM59, and repeat until something explicit stops it.
        ///
        /// <para><b>What each round is</b></para>
        /// <code>
        /// last valid design
        ///   -> deliberate targets            production TM59 FAIL, mechanical, in dwelling scope, +step
        ///   -> ONE design airflow round      Modify.EvaluateTargetedDesignAirFlows - all targets at once
        ///   -> re-prepare Part O             transfer air, network and unit duties rebuilt by SAM
        ///   -> full-year TAS, unique TSD     the SAME weather case, under its own project name
        ///   -> production TM59               on the model the workflow RETURNED
        ///   -> next targets, or stop
        /// </code>
        ///
        /// <para><b>This is design optimisation, and only that</b></para>
        /// <para>
        /// Design airflow moves. The Approved Document F requirement is a floor and is never written. The
        /// selected product is a ceiling and is never changed. No operating or profile airflow is written at
        /// all, so nothing here is the Iteration 3 behaviour of running a room at one airflow normally and
        /// another during hot hours.
        /// </para>
        ///
        /// <para><b>Why the re-preparation is offered NO catalogue</b></para>
        /// <para>
        /// <c>Modify.PreparePartOIteration</c> given a catalogue runs its smallest-capable-unit rule against
        /// the realized duty - which is right for Iteration 2 and would be a disaster here: every round the
        /// design grew, it would quietly buy the next product up, and the optimisation would never reach
        /// capacity because capacity would keep moving. It is therefore called with <b>null</b>, which
        /// leaves <c>AirHandlingUnitParameter.VentilationUnitReference</c> untouched; the preparation reuses
        /// the existing system and unit through their design terminals, so the product selected at
        /// Iteration 2 survives every round unchanged. The catalogue is still used - by the design airflow
        /// round, to read what that selected product is rated at and refuse a round beyond it.
        /// </para>
        ///
        /// <para><b>A full step, or no step</b></para>
        /// <para>
        /// <c>EvaluateTargetedDesignAirFlows</c> never clamps: a round is adopted at exactly the airflows it
        /// asked for, or refused. Where the refusal is the selected unit's rating, the dwellings that
        /// refused are marked at capacity, their targets dropped, and the round retried for the dwellings
        /// that can still take a full step - so one flat reaching its unit does not stop the others. When
        /// nothing is left that can take a full step, the run stops at
        /// <see cref="PartOOptimisationStopReason.CapacityReached"/> with the last valid design intact.
        /// </para>
        ///
        /// <para><b>Every PR #76 guarantee is kept, not worked around</b></para>
        /// <para>
        /// Each round is a complete <see cref="PartORun"/> lifecycle - prepared, armed before the workflow,
        /// completed only from a full-year run that wrote its own TSD, and assessed only from the model that
        /// workflow returned. Nothing here reads a model back off disk, assesses a preparation output, or
        /// completes a run from a results file it did not watch being written. A cancelled or failed round
        /// leaves the run dropped and contributes a recorded step with no model, so it can never become a
        /// false successful result.
        /// </para>
        /// </summary>
        /// <param name="partORun">
        /// The session's Part O run, which must be a completed Iteration 2 run. It is driven through a fresh
        /// lifecycle for every round and left holding the last valid one.
        /// </param>
        /// <param name="partOOptimisationSettings">The step and the iteration guard.</param>
        /// <param name="refusal">Why the optimisation could not be started at all.</param>
        /// <returns>The whole run - the baseline, every round, why it stopped, and the last valid design.</returns>
        public static PartOOptimisationRun? OptimisePartOTM59(this PartORun? partORun, PartOOptimisationSettings? partOOptimisationSettings, out string? refusal)
        {
            refusal = null;

            partOOptimisationSettings ??= new PartOOptimisationSettings();

            if (!CanOptimise(partORun, partOOptimisationSettings, out refusal))
            {
                return null;
            }

            PartOPreparationContext partOPreparationContext = partORun!.PreparationContext!;
            PartOSimulationContext partOSimulationContext = partORun.SimulationContext!;

            PartOOptimisationRun result = new(partOOptimisationSettings);

            //Run 0. The Iteration 2 design exactly as it stands - not re-simulated, because it already has
            //its full-year results and its own assessment, and re-running it would only prove TAS is
            //deterministic while costing an engineer several minutes.
            AnalyticalModel? analyticalModel_LastValid = partORun.AnalyticalModel_Assessment;
            string? path_TSD_LastValid = partORun.Path_TSD;
            List<OverheatingScenario> overheatingScenarios_LastValid = partORun.OverheatingScenarios;

            PartOTM59Assessment partOTM59Assessment = PartOTM59Assessment.Assess(analyticalModel_LastValid, path_TSD_LastValid, overheatingScenarios_LastValid);

            PartOOptimisationStep partOOptimisationStep = Step(0, partOSimulationContext.ProjectName, path_TSD_LastValid, partOSimulationContext, analyticalModel_LastValid, partOPreparationContext, partOTM59Assessment);

            result.Steps.Add(partOOptimisationStep);

            if (!partOTM59Assessment.IsAssessed)
            {
                return Stop(result, PartOOptimisationStopReason.AssessmentFailed, partOTM59Assessment.Refusal, analyticalModel_LastValid, path_TSD_LastValid, overheatingScenarios_LastValid);
            }

            partOOptimisationStep.IsCompleted = true;

            if (partOTM59Assessment.OccupiedSpaceComplianceStatus != TM59ComplianceStatus.Fail)
            {
                return Stop(result, PartOOptimisationStopReason.Passed, "The Iteration 2 baseline already passes, so no optimisation round was run.", analyticalModel_LastValid, path_TSD_LastValid, overheatingScenarios_LastValid);
            }

            //Dwellings whose selected unit has already refused a full step. Their rooms stay out of every
            //later round: a dwelling at its unit's ceiling cannot take another step, and asking again every
            //round would rerun the same refusal.
            HashSet<Guid> guids_AtCapacity = [];

            using CancellationTokenSource cancellationTokenSource = new();

            for (int iteration = 1; iteration <= partOOptimisationSettings.MaximumIterations; iteration++)
            {
                partOOptimisationStep = new PartOOptimisationStep(iteration)
                {
                    ProjectName = partOSimulationContext.ProjectName_Iteration(iteration),
                    WeatherData = partOSimulationContext.WeatherData?.Name,
                };

                result.Steps.Add(partOOptimisationStep);

                // ---- The targets, and the one round they make ------------------------------------------

                PartOOptimisationTargetSelection partOOptimisationTargetSelection = Query.PartOOptimisationTargets(analyticalModel_LastValid, partOTM59Assessment.SpaceResults, partOPreparationContext.Zones, partOOptimisationSettings.AirFlowStep_Lps);

                partOOptimisationStep.Notes.AddRange(partOOptimisationTargetSelection.NotOptimisable);

                List<DesignAirFlowTarget> designAirFlowTargets = partOOptimisationTargetSelection.Targets.FindAll(x => !guids_AtCapacity.Contains(x.SpaceGuid));

                if (designAirFlowTargets.Count == 0)
                {
                    //Told apart deliberately: nothing left to try because the equipment is full is a
                    //different answer from nothing left to try at all, and an engineer does different things
                    //about them.
                    return Stop(
                        result,
                        guids_AtCapacity.Count == 0 ? PartOOptimisationStopReason.NoEligibleTargets : PartOOptimisationStopReason.CapacityReached,
                        guids_AtCapacity.Count == 0
                            ? "No failing space remained that this optimisation could target."
                            : "Every remaining failing space is in a dwelling whose selected ventilation unit cannot carry another full step.",
                        analyticalModel_LastValid,
                        path_TSD_LastValid,
                        overheatingScenarios_LastValid);
                }

                DesignAirFlowRoundCandidate? designAirFlowRoundCandidate = Round(analyticalModel_LastValid, designAirFlowTargets, partOPreparationContext, partOOptimisationSettings, guids_AtCapacity, partOOptimisationStep);

                if (designAirFlowRoundCandidate is null)
                {
                    return Stop(result, PartOOptimisationStopReason.CapacityReached, "No dwelling could take another full step within its selected ventilation unit.", analyticalModel_LastValid, path_TSD_LastValid, overheatingScenarios_LastValid);
                }

                if (!designAirFlowRoundCandidate.IsAccepted)
                {
                    partOOptimisationStep.Refusals.AddRange(designAirFlowRoundCandidate.Refusals);

                    return Stop(result, PartOOptimisationStopReason.RebalanceRefused, string.Join(" ", designAirFlowRoundCandidate.Refusals), analyticalModel_LastValid, path_TSD_LastValid, overheatingScenarios_LastValid);
                }

                partOOptimisationStep.TargetedAdjustments.AddRange(designAirFlowRoundCandidate.TargetedAdjustments);
                partOOptimisationStep.DerivedAdjustments.AddRange(designAirFlowRoundCandidate.DerivedAdjustments);
                partOOptimisationStep.TargetRefusals.AddRange(designAirFlowRoundCandidate.TargetRefusals);
                partOOptimisationStep.Notes.AddRange(designAirFlowRoundCandidate.Notes);

                AnalyticalModel analyticalModel_Round = new(analyticalModel_LastValid, designAirFlowRoundCandidate.AdjacencyCluster);

                // ---- Rebuild the real Part O state around the new design -------------------------------

                //NULL catalogue - see the method documentation. The unit selected at Iteration 2 is reused
                //through its design terminals and is not re-chosen against the grown duty.
                PartOIterationPreparation partOIterationPreparation = Analytical.Modify.PreparePartOIteration(analyticalModel_Round, partOPreparationContext.PartOIteration, partOPreparationContext.Zones, partOPreparationContext.VentilationStrategies, null);

                partOOptimisationStep.Notes.AddRange(partOIterationPreparation.Notes);
                partOOptimisationStep.Warnings.AddRange(partOIterationPreparation.Warnings);

                if (!partORun.Prepare(partOIterationPreparation, partOPreparationContext))
                {
                    partOOptimisationStep.Refusals.Add(partOIterationPreparation.Refusal ?? partORun.InvalidationReason ?? "The Part O iteration could not be re-prepared over the optimised design.");

                    return Stop(result, PartOOptimisationStopReason.PreparationFailed, string.Join(" ", partOOptimisationStep.Refusals), analyticalModel_LastValid, path_TSD_LastValid, overheatingScenarios_LastValid);
                }

                // ---- The same full-year case, under this iteration's own name --------------------------

                AnalyticalModel analyticalModel_Workflow = RunPartOSimulation(partOIterationPreparation.AnalyticalModel, partOSimulationContext, partOOptimisationStep.ProjectName, partORun, cancellationTokenSource.Token, out string _, out string path_TSD, out bool cancelled, out bool fullYear, out List<string> notes_Simulation, out string refusal_Simulation);

                partOOptimisationStep.Notes.AddRange(notes_Simulation);
                partOOptimisationStep.Path_TSD = path_TSD;

                if (cancelled)
                {
                    partORun.Invalidate("The Part O optimisation was cancelled during this round's simulation, so this round has no results.");

                    return Stop(result, PartOOptimisationStopReason.Cancelled, "Cancelled during the simulation of this round.", analyticalModel_LastValid, path_TSD_LastValid, overheatingScenarios_LastValid);
                }

                if (analyticalModel_Workflow is null || !fullYear)
                {
                    partOOptimisationStep.Refusals.Add(refusal_Simulation ?? (analyticalModel_Workflow is null
                        ? "The TAS workflow did not run over this round's design, so there are no results to assess."
                        : "The simulation that ran was not the full year a TM59 assessment reads."));

                    partORun.Invalidate(string.Join(" ", partOOptimisationStep.Refusals));

                    return Stop(result, PartOOptimisationStopReason.SimulationFailed, string.Join(" ", partOOptimisationStep.Refusals), analyticalModel_LastValid, path_TSD_LastValid, overheatingScenarios_LastValid);
                }

                if (!partORun.Complete(analyticalModel_Workflow, path_TSD, partOSimulationContext, out string refusal_Complete))
                {
                    partOOptimisationStep.Refusals.Add(refusal_Complete);

                    return Stop(result, PartOOptimisationStopReason.SimulationFailed, refusal_Complete, analyticalModel_LastValid, path_TSD_LastValid, overheatingScenarios_LastValid);
                }

                // ---- Production TM59, on the model the workflow returned -------------------------------

                partOTM59Assessment = PartOTM59Assessment.Assess(partORun.AnalyticalModel_Assessment, partORun.Path_TSD, partORun.OverheatingScenarios);

                Record(partOOptimisationStep, partORun.AnalyticalModel_Assessment, partOPreparationContext, partOTM59Assessment);

                if (!partOTM59Assessment.IsAssessed)
                {
                    partOOptimisationStep.Refusals.Add(partOTM59Assessment.Refusal ?? "The production TM59 assessment could not be produced for this round.");

                    //DROPPED, like every other failed round. The workflow completed, so the run is sitting
                    //in WorkflowCompleted holding THIS round's model and TSD - but the optimisation is about
                    //to hand back the previous design, and leaving the run assessable would pair a model the
                    //user is not looking at with results from a round that was never assessed. That is the
                    //stale pairing PartORun exists to make unreachable, and an unassessable round has no
                    //business surviving as one.
                    partORun.Invalidate(partOTM59Assessment.Refusal ?? "The production TM59 assessment could not be produced for this optimisation round, so it is not a run that can be assessed.");

                    return Stop(result, PartOOptimisationStopReason.AssessmentFailed, partOTM59Assessment.Refusal, analyticalModel_LastValid, path_TSD_LastValid, overheatingScenarios_LastValid);
                }

                //This round is now the last valid design: it was prepared, simulated over the full year and
                //assessed, and every one of those was checked rather than assumed.
                partOOptimisationStep.IsCompleted = true;

                analyticalModel_LastValid = partORun.AnalyticalModel_Assessment;
                path_TSD_LastValid = partORun.Path_TSD;
                overheatingScenarios_LastValid = partORun.OverheatingScenarios;

                if (partOTM59Assessment.OccupiedSpaceComplianceStatus != TM59ComplianceStatus.Fail)
                {
                    return Stop(result, PartOOptimisationStopReason.Passed, null, analyticalModel_LastValid, path_TSD_LastValid, overheatingScenarios_LastValid);
                }
            }

            return Stop(result, PartOOptimisationStopReason.IterationLimitReached, null, analyticalModel_LastValid, path_TSD_LastValid, overheatingScenarios_LastValid);
        }

        /// <summary>
        /// Whether a run is an Iteration 2B starting point at all.
        /// <para>
        /// <b>Every condition is checked before anything runs</b>, because the first thing an optimisation
        /// does otherwise is spend minutes of TAS time discovering it should not have started.
        /// </para>
        /// </summary>
        public static bool CanOptimise(this PartORun? partORun, PartOOptimisationSettings? partOOptimisationSettings, out string? refusal)
        {
            refusal = null;

            if (partORun is null)
            {
                refusal = "There is no Part O run to optimise.";

                return false;
            }

            if (partOOptimisationSettings is not null && !partOOptimisationSettings.IsValid(out refusal))
            {
                return false;
            }

            //The same gate the assessment command uses, and it re-checks the results file rather than
            //trusting the state alone.
            if (!partORun.IsAssessable(out refusal))
            {
                return false;
            }

            PartOPreparationContext? partOPreparationContext = partORun.PreparationContext;
            if (partOPreparationContext is null)
            {
                refusal = "This Part O run does not record how it was prepared, so the same preparation cannot be repeated over an optimised design. Prepare the iteration again and re-run the simulation.";

                return false;
            }

            //The route, asked of SAM rather than decided here. Iteration 2B raises MECHANICAL design
            //airflow; a naturally ventilated dwelling has no design airflow for it to raise, and pretending
            //otherwise would be an optimisation of a quantity that does not govern the result.
            PartOVentilationMode partOVentilationMode = Analytical.Query.PartOIterationVentilationMode(partOPreparationContext.PartOIteration, out string _);

            if (partOVentilationMode != PartOVentilationMode.MVHR)
            {
                refusal = string.Format("Iteration 2B optimises mechanical design airflow, and this run was prepared over the {0} route. Natural ventilation is not a mechanical airflow optimisation target.", Core.Query.Description(partOVentilationMode));

                return false;
            }

            if (!partOPreparationContext.HasVentilationUnitCatalogue)
            {
                refusal = "This Part O run was prepared without equipment selection, so it is an Iteration 1a run and there is no selected ventilation unit for an optimisation to work within. Prepare the iteration again with a manufacturer ventilation unit selected.";

                return false;
            }

            PartOSimulationContext? partOSimulationContext = partORun.SimulationContext;
            if (partOSimulationContext is null || !partOSimulationContext.IsFullYear)
            {
                refusal = "This Part O run does not record a full-year TAS case that can be repeated, so an optimisation could not rerun the same weather case over each round. Re-run the simulation with Full Year Simulation ticked over days 1 to 365.";

                return false;
            }

            return true;
        }

        /// <summary>
        /// One design airflow round, retried without the dwellings whose <b>selected</b> unit refused it.
        /// <para>
        /// <b>Only an equipment refusal is retried, and only by removing whole dwellings.</b> A dwelling at
        /// its unit's ceiling cannot take another full step and is out of the optimisation from here; every
        /// other dwelling can still take one, and stopping them because of it would waste capacity the
        /// design has. Any other refusal - an Approved Document F floor, an unbalanced dwelling, terminals
        /// that cannot be attributed - is <b>not</b> retried: it is a design that needs a person.
        /// </para>
        /// <para>
        /// The round itself is never partially adopted. Each attempt is a whole transaction over the
        /// targets it was given, at exactly the airflows they asked for.
        /// </para>
        /// </summary>
        /// <returns>The accepted round, the refused one where the refusal was not about equipment, or null
        /// where no dwelling can take another full step.</returns>
        private static DesignAirFlowRoundCandidate? Round(AnalyticalModel? analyticalModel, List<DesignAirFlowTarget> designAirFlowTargets, PartOPreparationContext partOPreparationContext, PartOOptimisationSettings partOOptimisationSettings, HashSet<Guid> guids_AtCapacity, PartOOptimisationStep partOOptimisationStep)
        {
            AdjacencyCluster? adjacencyCluster = analyticalModel?.AdjacencyCluster;
            if (adjacencyCluster is null)
            {
                return null;
            }

            while (designAirFlowTargets.Count != 0)
            {
                DesignAirFlowRoundCandidate designAirFlowRoundCandidate = adjacencyCluster.EvaluateTargetedDesignAirFlows(designAirFlowTargets, PartFExtractAllocationStrategy.MinimumFirstCookingPriority, partOOptimisationSettings.Tolerance_Lps, partOPreparationContext.VentilationUnitCapacityDescriptors);

                if (designAirFlowRoundCandidate.IsAccepted)
                {
                    return designAirFlowRoundCandidate;
                }

                List<DwellingDesignAirFlowRound> dwellingDesignAirFlowRounds = designAirFlowRoundCandidate.VentilationUnitRefusals;
                if (dwellingDesignAirFlowRounds.Count == 0)
                {
                    //Not an equipment refusal. Handed back refused, so the caller stops rather than
                    //quietly retrying a design problem with fewer rooms.
                    return designAirFlowRoundCandidate;
                }

                foreach (DwellingDesignAirFlowRound dwellingDesignAirFlowRound in dwellingDesignAirFlowRounds)
                {
                    foreach (DesignAirFlowAdjustment designAirFlowAdjustment in dwellingDesignAirFlowRound.TargetedAdjustments)
                    {
                        guids_AtCapacity.Add(designAirFlowAdjustment.SpaceGuid);
                    }

                    partOOptimisationStep.Notes.Add(string.Format(
                        "Ventilation system '{0}' cannot take another {1:0.###} l/s step within its selected ventilation unit '{2}': the round would have designed {3:0.###}/{4:0.###} l/s against a maximum of {5:0.###}/{6:0.###} l/s. Its rooms are out of the optimisation from here; the selected product is unchanged.",
                        dwellingDesignAirFlowRound.VentilationSystem?.FullName ?? "?",
                        partOOptimisationSettings.AirFlowStep_Lps,
                        dwellingDesignAirFlowRound.VentilationUnitReference,
                        dwellingDesignAirFlowRound.SupplyDuty_After_Lps,
                        dwellingDesignAirFlowRound.ExtractDuty_After_Lps,
                        dwellingDesignAirFlowRound.VentilationUnitCapacityDescriptor?.MaximumSupplyFlowRate_Lps ?? double.NaN,
                        dwellingDesignAirFlowRound.VentilationUnitCapacityDescriptor?.MaximumExtractFlowRate_Lps ?? double.NaN));
                }

                designAirFlowTargets = designAirFlowTargets.FindAll(x => !guids_AtCapacity.Contains(x.SpaceGuid));
            }

            return null;
        }

        /// <summary>The baseline step - iteration 0, no targets, its own results and its own assessment.</summary>
        private static PartOOptimisationStep Step(int iteration, string? projectName, string? path_TSD, PartOSimulationContext partOSimulationContext, AnalyticalModel? analyticalModel, PartOPreparationContext partOPreparationContext, PartOTM59Assessment partOTM59Assessment)
        {
            PartOOptimisationStep result = new(iteration)
            {
                ProjectName = projectName,
                Path_TSD = path_TSD,
                WeatherData = partOSimulationContext.WeatherData?.Name,
            };

            Record(result, analyticalModel, partOPreparationContext, partOTM59Assessment);

            return result;
        }

        /// <summary>
        /// Writes one iteration's unit states and production TM59 results onto its step - the audit trail
        /// that makes the run reproducible.
        /// </summary>
        private static void Record(PartOOptimisationStep partOOptimisationStep, AnalyticalModel? analyticalModel, PartOPreparationContext partOPreparationContext, PartOTM59Assessment partOTM59Assessment)
        {
            partOOptimisationStep.UnitStates.AddRange(UnitStates(analyticalModel, partOPreparationContext.VentilationUnitCapacityDescriptors));

            if (!partOTM59Assessment.IsAssessed)
            {
                return;
            }

            partOOptimisationStep.TM59Results.AddRange(partOTM59Assessment.SpaceResults);
            partOOptimisationStep.OccupiedSpaceComplianceStatus = partOTM59Assessment.OccupiedSpaceComplianceStatus;
            partOOptimisationStep.Warnings.AddRange(partOTM59Assessment.AssociationRefusals);
        }

        /// <summary>
        /// What every air handling unit in the model is carrying, beside what its selected product is rated
        /// to move.
        /// <para>
        /// Every value from its own authority: the duty from <c>Query.AirHandlingUnitDesignDuty</c>, which
        /// sums every system the unit supplies rather than assuming it serves one; the product from
        /// <c>Query.SelectedVentilationUnitReference</c>; the rating from the descriptor that product
        /// resolves to in the run's own catalogue. Nothing here is stored on the unit, so nothing here can
        /// go stale against the design.
        /// </para>
        /// </summary>
        private static List<PartOOptimisationUnitState> UnitStates(AnalyticalModel? analyticalModel, IEnumerable<VentilationUnitCapacityDescriptor>? ventilationUnitCapacityDescriptors)
        {
            List<PartOOptimisationUnitState> result = [];

            AdjacencyCluster? adjacencyCluster = analyticalModel?.AdjacencyCluster;
            if (adjacencyCluster is null)
            {
                return result;
            }

            List<AirHandlingUnit> airHandlingUnits = adjacencyCluster.GetObjects<AirHandlingUnit>() ?? [];

            airHandlingUnits.Sort((x, y) => string.CompareOrdinal(x?.Name, y?.Name));

            foreach (AirHandlingUnit airHandlingUnit in airHandlingUnits)
            {
                if (airHandlingUnit is null)
                {
                    continue;
                }

                if (!Analytical.Query.AirHandlingUnitDesignDuty(adjacencyCluster, airHandlingUnit, out double supplyDuty_Lps, out double extractDuty_Lps))
                {
                    supplyDuty_Lps = double.NaN;
                    extractDuty_Lps = double.NaN;
                }

                List<VentilationSystem> ventilationSystems = Analytical.Query.VentilationSystems(adjacencyCluster, airHandlingUnit) ?? [];

                VentilationUnitReference? ventilationUnitReference = airHandlingUnit.SelectedVentilationUnitReference();

                VentilationUnitCapacityDescriptor? ventilationUnitCapacityDescriptor = ventilationUnitCapacityDescriptors is null
                    ? null
                    : Analytical.Query.SelectedVentilationUnitCapacityDescriptor(airHandlingUnit, ventilationUnitCapacityDescriptors);

                VentilationUnitSelectionOutcome ventilationUnitSelectionOutcome = VentilationUnitSelectionOutcome.NotApplicable;
                string? reason = null;

                if (ventilationUnitCapacityDescriptors is not null && ventilationUnitReference is not null)
                {
                    ventilationUnitSelectionOutcome = Analytical.Query.IsVentilationUnitSufficient(adjacencyCluster, airHandlingUnit, ventilationUnitCapacityDescriptors, out reason)
                        ? VentilationUnitSelectionOutcome.Kept
                        : VentilationUnitSelectionOutcome.Refused;
                }

                result.Add(new PartOOptimisationUnitState(
                    airHandlingUnit.Name,
                    string.Join(", ", ventilationSystems.ConvertAll(x => x?.FullName)),
                    supplyDuty_Lps,
                    extractDuty_Lps,
                    ventilationUnitReference,
                    ventilationUnitCapacityDescriptor,
                    ventilationUnitSelectionOutcome,
                    ventilationUnitSelectionOutcome == VentilationUnitSelectionOutcome.Refused ? reason : null));
            }

            return result;
        }

        /// <summary>
        /// Ends the run with an explicit reason and the last design that was actually valid.
        /// <para>
        /// <b>The last valid model is always the one from a step that completed</b> - prepared, simulated
        /// over the full year and assessed - never a round that was refused, cancelled or unsimulated. That
        /// is what makes a <see cref="PartOOptimisationStopReason.CapacityReached"/> stop usable: the design
        /// it leaves behind is a real, simulated, assessed one step below the unit's ceiling.
        /// </para>
        /// </summary>
        private static PartOOptimisationRun Stop(PartOOptimisationRun partOOptimisationRun, PartOOptimisationStopReason partOOptimisationStopReason, string? description, AnalyticalModel? analyticalModel, string? path_TSD, IEnumerable<OverheatingScenario>? overheatingScenarios)
        {
            partOOptimisationRun.StopReason = partOOptimisationStopReason;
            partOOptimisationRun.StopDescription = description;

            partOOptimisationRun.AnalyticalModel_LastValid = analyticalModel;
            partOOptimisationRun.Path_TSD_LastValid = path_TSD;

            foreach (OverheatingScenario overheatingScenario in overheatingScenarios ?? [])
            {
                if (overheatingScenario is not null)
                {
                    partOOptimisationRun.OverheatingScenarios_LastValid.Add(overheatingScenario);
                }
            }

            return partOOptimisationRun;
        }
    }
}
