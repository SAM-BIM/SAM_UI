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
        /// Prepares an Approved Document O base iteration over the model's dwelling zones, shows what it
        /// produced, and - on OK - adopts the prepared model and starts the session's Part O run.
        /// <para>
        /// <b>Orchestration only.</b> The engineering is one call:
        /// <c>SAM.Analytical.Modify.PreparePartOIteration</c>. This method chooses nothing it could get from
        /// there - not the dwelling scope (<c>Query.PartFDwellingZones</c>), not the route the iteration is
        /// defined over (<c>Query.PartOIterationVentilationMode</c>, through
        /// <see cref="PartOVentilationStrategyOption"/>), and above all not the ventilation unit. The
        /// catalogue is passed in as descriptors and the smallest-capable-unit rule stays inside the
        /// preparation, run per dwelling against the realized terminal network's duty. Selecting a product
        /// never writes a design airflow.
        /// </para>
        /// <para>
        /// <b>The run is started only after the model is adopted</b>, and
        /// <see cref="PartORun.ExpectModification"/> is armed immediately before that write so the run's own
        /// change is not read as somebody else's edit. Everything else that replaces the model between here
        /// and a completed workflow drops the run - see <see cref="PartORun"/>.
        /// </para>
        /// </summary>
        /// <param name="uIAnalyticalModel">The loaded model. Not modified unless the user accepts.</param>
        /// <param name="partORun">The session's Part O run, which this command moves to Prepared.</param>
        /// <param name="owner">Owner window for the dialogs.</param>
        public static void PreparePartOIteration(this UIAnalyticalModel? uIAnalyticalModel, PartORun partORun, IWin32Window? owner = null)
        {
            AnalyticalModel? analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel is null || partORun is null)
            {
                return;
            }

            List<Zone> zones = analyticalModel.GetZones() ?? [];
            if (zones.Count == 0)
            {
                MessageBox.Show("The model has no zones, so no dwelling can be assessed. Zone the model, mark its dwellings, and size it against Approved Document F first.");

                return;
            }

            //Read before the dialog so the dialog can say which of the three catalogue states it is in - and
            //so "the catalogue is missing" can never be presented as "no product can serve this dwelling".
            VentilationUnitCatalogue ventilationUnitCatalogue = VentilationUnitCatalogue.Read();

            PartOIterationWindow partOIterationWindow = new()
            {
                Zones = zones,
                VentilationUnitCatalogue = ventilationUnitCatalogue,
            };

            //The PROJECT's own equipment preselection, restored off the model it belongs to. This is what
            //makes the mode and the permitted pool survive closing and reopening this dialog, and reopening
            //the project - and what keeps one project's pool out of the next one, which a global application
            //setting could not. Absent reads as the historic default. See PartOEquipmentSelection.
            //
            //Assigned AFTER VentilationUnitCatalogue, because the pool is restored by ticking catalogue rows
            //and those rows do not exist until the catalogue has been set.
            partOIterationWindow.EquipmentSelection = analyticalModel.GetValue<PartOEquipmentSelection>(Analytical.AnalyticalModelParameter.PartOEquipmentSelection);

            if (owner is not null)
            {
                new System.Windows.Interop.WindowInteropHelper(partOIterationWindow).Owner = owner.Handle;
            }

            bool? showDialog = partOIterationWindow.ShowDialog();
            if (showDialog is null || !showDialog.Value)
            {
                return;
            }

            PartOVentilationStrategyOption? option = partOIterationWindow.SelectedOption;
            List<Zone> zones_Dwelling = partOIterationWindow.Zones_Dwelling;

            if (option is null || zones_Dwelling.Count == 0)
            {
                return;
            }

            //Everything the dialog collected, in the one shape the preparation seam takes. The picker and
            //the high-level Prepare & Run dialog differ only in how a request is arrived at; from here on
            //there is one path.
            PartOWorkflowRequest partOWorkflowRequest = new(
                option,
                partOIterationWindow.Isolate ? PartOWorkflowScope.SelectedDwellingsIsolated : PartOWorkflowScope.SelectedDwellings,
                zones_Dwelling,
                partOIterationWindow.SelectVentilationUnit)
            {
                OptimisationSettings = partOIterationWindow.OptimisationSettings,
                EquipmentSelection = partOIterationWindow.EquipmentSelection,
            };

            PreparePartOIteration(uIAnalyticalModel, partORun, partOWorkflowRequest, ventilationUnitCatalogue, owner);
        }

        /// <summary>
        /// The preparation itself, over a request that has already been made: prepare, show what it produced,
        /// and - on OK - adopt the prepared model and start the session's Part O run.
        /// <para>
        /// <b>The one implementation.</b> The Prepare Iteration picker builds a request from its own controls
        /// and calls this; so does <see cref="RunPartOWorkflow"/>. Neither has a preparation of its own, so
        /// the two cannot drift, and the engineering below is still the single call
        /// <c>SAM.Analytical.Modify.PreparePartOIteration</c>.
        /// </para>
        /// <para>
        /// <b>The preparation summary is shown either way.</b> It states the thermal model scope, the route,
        /// the duty and the equipment, and it is the last point at which a person can decline before a
        /// full-year TAS run starts. A high-level workflow that skipped it would be hiding the one screen
        /// that says what is about to be simulated.
        /// </para>
        /// </summary>
        /// <returns>
        /// True where the prepared model was adopted and <paramref name="partORun"/> is now
        /// <see cref="PartORunState.Prepared"/>. False for a refusal, a decline, or a failed adoption - each
        /// of which has already been reported to the user by the time this returns.
        /// </returns>
        public static bool PreparePartOIteration(this UIAnalyticalModel? uIAnalyticalModel, PartORun partORun, PartOWorkflowRequest partOWorkflowRequest, VentilationUnitCatalogue ventilationUnitCatalogue, IWin32Window? owner = null)
        {
            AnalyticalModel? analyticalModel = uIAnalyticalModel?.JSAMObject;

            PartOVentilationStrategyOption? option = partOWorkflowRequest?.Option;

            List<Zone> zones_Dwelling = partOWorkflowRequest?.Zones_Dwelling ?? [];

            if (analyticalModel is null || partORun is null || option is null || zones_Dwelling.Count == 0)
            {
                return false;
            }

            ventilationUnitCatalogue ??= VentilationUnitCatalogue.Read();

            //One canonical word for every zone in scope. There is no path by which anything else can be in
            //this dictionary - the option carries the word and neither dialog has a text field.
            Dictionary<Guid, string> dictionary_VentilationStrategy = partOWorkflowRequest!.VentilationStrategies();

            //Null, not an empty list, where no selection is wanted: the preparation reads null as "no
            //catalogue was offered" and leaves AirHandlingUnitParameter.VentilationUnitReference untouched,
            //which is Iteration 1a. An empty list would be a catalogue that offers nothing.
            //
            //The request states the INTENT and this reads the capability, which is why the two are separate:
            //an Iteration 2 request on a machine with no readable catalogue hands the preparation an EMPTY
            //list, so it refuses per dwelling and says so, rather than silently becoming an Iteration 1a run.
            //The Prepare & Run dialog blocks that combination before it gets here - see
            //PartOWorkflowInspection's Equipment stage - and this is what happens if anything else reaches it.
            List<VentilationUnitCapacityDescriptor>? ventilationUnitCapacityDescriptors = partOWorkflowRequest.SelectVentilationUnit
                ? ventilationUnitCatalogue.CapacityDescriptors
                : null;

            //The request, or failing that the PROJECT, or failing that the historic default - in that order,
            //and the order is load-bearing. Query.PartOEquipmentSelection says at length why, and is a named
            //function precisely because the failure it prevents is silent.
            PartOEquipmentSelection partOEquipmentSelection = Query.PartOEquipmentSelection(partOWorkflowRequest, analyticalModel);

            //TWO different lists, and conflating them is the one mistake that would break this feature.
            //
            //  ventilationUnitCapacityDescriptors  - the WHOLE selectable catalogue. A CAPABILITY LOOKUP.
            //      It goes into PartOPreparationContext, where Iteration 2B and the capacity envelope read
            //      it through Query.SelectedVentilationUnitCapacityDescriptor to find what each dwelling's
            //      ALREADY SELECTED product is rated at. Narrowing it to the pool would make a dwelling
            //      manually assigned a product that has since left the pool report "capacity unknown", and
            //      2B would lose the ceiling it stops at.
            //
            //  ventilationUnitCapacityDescriptors_Candidate - what an automatic selection may CHOOSE FROM.
            //      The pool, applied once, here. Null under manual authority, which is how
            //      Analytical.Modify.PreparePartOIteration is already told "run no rule and leave every
            //      existing identity alone" - so manual mode needs no new code path in SAM at all. An empty
            //      list under the pooled mode is an explicit refusal and is never widened back.
            List<VentilationUnitCapacityDescriptor>? ventilationUnitCapacityDescriptors_Candidate = partOWorkflowRequest.SelectVentilationUnit
                ? partOEquipmentSelection.CandidateDescriptors(ventilationUnitCatalogue.CapacityDescriptors)
                : null;

            //Everything this preparation was asked for, kept so an Iteration 2B optimisation can repeat it
            //identically over a changed design. Also carries the optimisation the user asked for, which is
            //not a preparation input and does not affect the call below - see PartOPreparationContext.
            PartOPreparationContext partOPreparationContext = new(option.PartOIteration, zones_Dwelling, dictionary_VentilationStrategy, ventilationUnitCapacityDescriptors)
            {
                OptimisationSettings = partOWorkflowRequest.OptimisationSettings,
                Isolated = partOWorkflowRequest.Isolate,
                EquipmentSelection = partOEquipmentSelection,
            };

            PartOIterationPreparation partOIterationPreparation = Analytical.Modify.PreparePartOIteration(analyticalModel, option.PartOIteration, zones_Dwelling, dictionary_VentilationStrategy, ventilationUnitCapacityDescriptors_Candidate, partOWorkflowRequest.Isolate);

            //A refusal returns no model at all, by contract. Nothing is adopted and the run is dropped with
            //the reason, so the ribbon can say why an assessment is unavailable.
            if (partOIterationPreparation.Refusal is not null)
            {
                partORun.Invalidate(partOIterationPreparation.Refusal);

                MessageBox.Show(string.Format("The Part O iteration was not prepared.\n\n{0}", partOIterationPreparation.Refusal));

                return false;
            }

            //An isolated run gets its own project name, so its TBD, TSD, .sam and TM59 report cannot land
            //on a full run's or on another selection's. Naming only - see Query.ProjectName_Isolated; the
            //context stamped on the model remains the authority for what this run actually was.
            PartOIsolationContext? partOIsolationContext = partOIterationPreparation.AnalyticalModel?.GetValue<PartOIsolationContext>(Analytical.AnalyticalModelParameter.PartOIsolationContext);

            if (partOIsolationContext is not null && partOIsolationContext.IsValid)
            {
                partOIterationPreparation.AnalyticalModel!.Name = Query.ProjectName_Isolated(analyticalModel.Name, partOIsolationContext.ScopeToken);
            }

            //THE prepared model, and ONE working copy of its cluster.
            //
            //AnalyticalModel.AdjacencyCluster hands back a FRESH COPY on every access, so a cluster written
            //through one access is discarded the moment the next access is made. That makes an authored
            //equipment assignment exactly the kind of change that can be applied, reported as applied, and
            //silently lost - so the copy is taken once here, the commit below writes into THAT object, and
            //the model is rebuilt from it. The same discipline, and the same reason, as the
            //`new AnalyticalModel(analyticalModel, adjacencyCluster)` that ends
            //Analytical.Modify.PreparePartOIteration's own work.
            AnalyticalModel analyticalModel_Prepared = partOIterationPreparation.AnalyticalModel!;

            AdjacencyCluster adjacencyCluster_Prepared = analyticalModel_Prepared.AdjacencyCluster;

            //The assignment table, built once against the WHOLE catalogue so that every assigned product
            //can resolve its own rating, and carrying the mode and pool so the window knows who decides.
            PartOEquipmentAssignmentSet? partOEquipmentAssignmentSet = partOWorkflowRequest.SelectVentilationUnit
                ? EquipmentAssignmentSet(adjacencyCluster_Prepared, partOIterationPreparation, zones_Dwelling, ventilationUnitCapacityDescriptors, partOEquipmentSelection)
                : null;

            PartOPreparationWindow partOPreparationWindow = new()
            {
                Summary = Summary(partOIterationPreparation, option, ventilationUnitCatalogue, partOWorkflowRequest.SelectVentilationUnit, partOIsolationContext, partOEquipmentAssignmentSet),
                SpaceRows = (adjacencyCluster_Prepared.GetSpaces() ?? []).ConvertAll(x => new PartOSpaceRow(x)),
            };

            if (partOEquipmentAssignmentSet is not null)
            {
                partOPreparationWindow.EquipmentAssignmentSet = partOEquipmentAssignmentSet;
            }
            else
            {
                //Iteration 1a, or a catalogue that could not be read: the rows still say what each dwelling
                //is designed to move, and say plainly that no product was selected.
                partOPreparationWindow.EquipmentRows = EquipmentRows(adjacencyCluster_Prepared, partOIterationPreparation, null);
            }

            partOPreparationWindow.SetDiagnostics(partOIterationPreparation.Notes, partOIterationPreparation.Warnings, partOIterationPreparation.Refusals);

            if (owner is not null)
            {
                new System.Windows.Interop.WindowInteropHelper(partOPreparationWindow).Owner = owner.Handle;
            }

            bool? showDialog_Preparation = partOPreparationWindow.ShowDialog();
            if (showDialog_Preparation is null || !showDialog_Preparation.Value)
            {
                //Declined. The loaded model is untouched - the preparation worked on a copy - and no run is
                //started, so nothing can later be simulated and assessed against scenarios nobody accepted.
                return false;
            }

            //THE ONE WRITE, and only what the engineer actually changed - so a table that was merely
            //converted to manual, which changes no identity, writes nothing and leaves the prepared model
            //bit-for-bit as the preparation built it. Each write goes through
            //Analytical.Modify.AssignVentilationUnit, which moves no airflow of any kind.
            if (partOEquipmentAssignmentSet is not null)
            {
                if (!partOEquipmentAssignmentSet.Commit(adjacencyCluster_Prepared, out List<string> notes_Commit, out List<string> refusals_Commit))
                {
                    MessageBox.Show(string.Format("The equipment assignments were not applied, so the prepared model was not adopted.\n\n{0}", string.Join("\n\n", refusals_Commit)));

                    return false;
                }

                partOIterationPreparation.Notes.AddRange(notes_Commit);

                if (partOEquipmentAssignmentSet.HasChanges)
                {
                    //Rebuilt from the cluster the assignments were written into - see the comment where that
                    //copy was taken. Guarded, so an unchanged table costs no rebuild.
                    analyticalModel_Prepared = new AnalyticalModel(analyticalModel_Prepared, adjacencyCluster_Prepared);
                }

                //The mode and pool as they now stand - "Convert to Manual" changed the mode, and this is
                //where that becomes the project's own recorded preference rather than a fact about one
                //dialog. It rides on the model, so it survives the project being saved and reopened and
                //cannot leak into another project.
                partOPreparationContext.EquipmentSelection = partOEquipmentAssignmentSet.EquipmentSelection;

                analyticalModel_Prepared.SetValue(Analytical.AnalyticalModelParameter.PartOEquipmentSelection, partOEquipmentAssignmentSet.EquipmentSelection);
            }

            if (!partORun.Prepare(analyticalModel_Prepared, partOIterationPreparation.OverheatingScenarios, partOPreparationContext, partOIterationPreparation.Refusal))
            {
                MessageBox.Show(string.Format("The prepared model was not adopted.\n\n{0}", partORun.InvalidationReason));

                return false;
            }

            //Armed immediately before the write, so this replacement is not read as an outside edit.
            partORun.ExpectModification();

            uIAnalyticalModel!.SetJSAMObject(analyticalModel_Prepared, new FullModification());

            return partORun.State == PartORunState.Prepared;
        }

        /// <summary>
        /// One equipment row per air handling unit the preparation built, each value read from its own
        /// authority: the duty from <c>Query.AirHandlingUnitDesignDuty</c>, the product from
        /// <c>Query.SelectedVentilationUnitReference</c>, and the capacity from the descriptor that product
        /// resolves to in the offered catalogue.
        /// <para>
        /// Rows are keyed on the air handling unit rather than paired positionally with
        /// <c>VentilationUnitSelections</c>, which is explicitly not item-for-item with
        /// <c>AirHandlingUnits</c> - a dwelling nothing could serve contributes a refusal and no selection.
        /// Reading the selection off the unit is what the preparation's own documentation says to do where the
        /// pairing matters.
        /// </para>
        /// </summary>
        /// <summary>
        /// Builds the dwelling assignment table from a completed preparation - the one place every model
        /// read for it happens.
        ///
        /// <para><b>Scoped to this run's own dwelling units</b></para>
        /// <para>
        /// <c>PartOIterationPreparation.AirHandlingUnits</c> and <c>.VentilationSystems</c> are what this
        /// preparation built, index for index, so nothing here has to re-derive which units belong to the
        /// run. A legacy unit the model was drawn with is not one of them and does not appear.
        /// </para>
        ///
        /// <para><b>The whole catalogue, never the pool</b></para>
        /// <para>
        /// <paramref name="ventilationUnitCapacityDescriptors"/> is the capability lookup. A dwelling
        /// assigned a product that is no longer permitted still resolves its own rating from it and is
        /// flagged as outside the pool - which is the difference between telling an engineer about a
        /// procurement change and losing their design to one.
        /// </para>
        ///
        /// <para><b>Cost</b></para>
        /// <para>
        /// One relation lookup per dwelling zone to name the dwellings, one per system to find its spaces,
        /// and one duty derivation per unit - then the table is captured numbers. No <c>GetSpaces</c> or
        /// <c>GetZones</c> rebuild happens inside a dwelling loop, and none happens again when a row is
        /// edited or the pool changes.
        /// </para>
        /// </summary>
        private static PartOEquipmentAssignmentSet EquipmentAssignmentSet(AdjacencyCluster? adjacencyCluster, PartOIterationPreparation partOIterationPreparation, List<Zone> zones_Dwelling, IEnumerable<VentilationUnitCapacityDescriptor>? ventilationUnitCapacityDescriptors, PartOEquipmentSelection partOEquipmentSelection)
        {
            List<AirHandlingUnit> airHandlingUnits = partOIterationPreparation.AirHandlingUnits;
            List<VentilationSystem> ventilationSystems = partOIterationPreparation.VentilationSystems;

            //Space -> the dwelling it belongs to, built ONCE over the run's zones. Naming a dwelling from
            //inside the per-unit loop would mean a zone-membership lookup per unit.
            Dictionary<Guid, string> dictionary_DwellingName_Space = [];

            if (adjacencyCluster is not null)
            {
                foreach (Zone zone in zones_Dwelling ?? [])
                {
                    if (zone is null)
                    {
                        continue;
                    }

                    foreach (Space space in adjacencyCluster.GetRelatedObjects<Space>(zone) ?? [])
                    {
                        if (space is not null)
                        {
                            dictionary_DwellingName_Space[space.Guid] = zone.Name;
                        }
                    }
                }
            }

            Dictionary<Guid, string> dictionary_VentilationSystemName = [];
            Dictionary<Guid, string> dictionary_DwellingName = [];

            for (int i = 0; i < airHandlingUnits.Count; i++)
            {
                AirHandlingUnit airHandlingUnit = airHandlingUnits[i];
                if (airHandlingUnit is null)
                {
                    continue;
                }

                VentilationSystem? ventilationSystem = i < ventilationSystems.Count ? ventilationSystems[i] : null;

                if (ventilationSystem is not null)
                {
                    dictionary_VentilationSystemName[airHandlingUnit.Guid] = ventilationSystem.FullName;
                }

                //The dwelling this unit serves, found through the system's own spaces. Left absent rather
                //than guessed where nothing resolves - the row then falls back to the system or the unit
                //name, and never to an invented dwelling.
                if (adjacencyCluster is null || ventilationSystem is null)
                {
                    continue;
                }

                foreach (Space space in adjacencyCluster.GetRelatedObjects<Space>(ventilationSystem) ?? [])
                {
                    if (space is not null && dictionary_DwellingName_Space.TryGetValue(space.Guid, out string name_Dwelling))
                    {
                        dictionary_DwellingName[airHandlingUnit.Guid] = name_Dwelling;

                        break;
                    }
                }
            }

            return PartOEquipmentAssignmentSet.Create(
                adjacencyCluster,
                airHandlingUnits,
                dictionary_VentilationSystemName,
                dictionary_DwellingName,
                ventilationUnitCapacityDescriptors,
                partOEquipmentSelection);
        }

        private static List<PartOEquipmentRow> EquipmentRows(AdjacencyCluster? adjacencyCluster, PartOIterationPreparation partOIterationPreparation, IEnumerable<VentilationUnitCapacityDescriptor>? ventilationUnitCapacityDescriptors)
        {
            List<PartOEquipmentRow> result = [];

            if (adjacencyCluster is null)
            {
                return result;
            }

            List<AirHandlingUnit> airHandlingUnits = partOIterationPreparation!.AirHandlingUnits;
            List<VentilationSystem> ventilationSystems = partOIterationPreparation.VentilationSystems;

            for (int i = 0; i < airHandlingUnits.Count; i++)
            {
                AirHandlingUnit airHandlingUnit = airHandlingUnits[i];
                if (airHandlingUnit is null)
                {
                    continue;
                }

                //Item for item with AirHandlingUnits, per PartOIterationPreparation's own contract.
                string? systemName = i < ventilationSystems.Count ? ventilationSystems[i]?.FullName : null;

                if (!Analytical.Query.AirHandlingUnitDesignDuty(adjacencyCluster, airHandlingUnit, out double supplyDuty_Lps, out double extractDuty_Lps))
                {
                    supplyDuty_Lps = double.NaN;
                    extractDuty_Lps = double.NaN;
                }

                //Null where nothing was selected, or where the selected reference is not in the offered
                //catalogue. Either way there is no capacity to show, and none is invented.
                VentilationUnitCapacityDescriptor? ventilationUnitCapacityDescriptor = ventilationUnitCapacityDescriptors is null
                    ? null
                    : Analytical.Query.SelectedVentilationUnitCapacityDescriptor(airHandlingUnit, ventilationUnitCapacityDescriptors);

                //A refusal is only stated where a catalogue was actually offered - without one, "not
                //applicable" is the truth and "refused" would be a fabrication.
                string? refusal = ventilationUnitCapacityDescriptors is not null && ventilationUnitCapacityDescriptor is null
                    ? Analytical.Query.IsVentilationUnitSufficient(adjacencyCluster, airHandlingUnit, ventilationUnitCapacityDescriptors, out string reason) ? null : reason
                    : null;

                result.Add(new PartOEquipmentRow(airHandlingUnit.Name, systemName, supplyDuty_Lps, extractDuty_Lps, ventilationUnitCapacityDescriptor, refusal));
            }

            return result;
        }

        private static string Summary(PartOIterationPreparation partOIterationPreparation, PartOVentilationStrategyOption option, VentilationUnitCatalogue ventilationUnitCatalogue, bool selectVentilationUnit, PartOIsolationContext? partOIsolationContext, PartOEquipmentAssignmentSet? partOEquipmentAssignmentSet)
        {
            //Said FIRST, and said as a scope rather than as a setting. An isolated run is a different
            //thermal model from the whole building - the interfaces to the dwellings left out are simulated
            //as adiabatic - and a person reading these results later has to be told that without having to
            //go looking for it.
            string scope = partOIsolationContext is not null && partOIsolationContext.IsValid
                ? string.Format(
                    "Thermal model scope: ISOLATED. Selected dwellings: {0}. Interfaces to excluded spaces are simulated as adiabatic and surrounding external geometry is retained as shading context, so these results may differ from a whole-building simulation of the same dwellings. The Part O criteria and the Part F requirements are unchanged.\n",
                    string.Join(", ", partOIsolationContext.Names_Dwelling))
                : "Thermal model scope: WHOLE BUILDING.\n";

            //The whole-run totals, which are sums across every dwelling this run built - NOT any one
            //dwelling's duty. Said so explicitly, because a three-flat model summing to 156 l/s beside a
            //150 l/s product would otherwise read as an exceeded unit.
            string duty = double.IsNaN(partOIterationPreparation.DesignSupplyDuty_Lps)
                ? "No mechanical design duty (the natural ventilation route realizes no continuous mechanical terminals)."
                : string.Format("Design duty totalled across {0} dwelling system(s): {1:N1} l/s supply, {2:N1} l/s extract. Per-dwelling duties are in the equipment table below.", partOIterationPreparation.VentilationSystems.Count, partOIterationPreparation.DesignSupplyDuty_Lps, partOIterationPreparation.DesignExtractDuty_Lps);

            //Asked of the assignment table rather than restated: it knows the mode, how many dwellings
            //carry a product and how many need looking at, and a second count here could disagree with the
            //grid immediately below it.
            string equipment = selectVentilationUnit
                ? partOEquipmentAssignmentSet?.Description ?? string.Format("Equipment selection ran against {0} selectable product(s). A selected product's Maximum is its capability ceiling and is never a design airflow.", ventilationUnitCatalogue.CapacityDescriptors.Count)
                : string.Format("No equipment selection ran, so no product is selected. {0}", ventilationUnitCatalogue.Description);

            return string.Format("{6}{0}. Route stated: {1} ({2}). {3} {4}\n{5} overheating scenario(s) stated. Simulate this model to produce results the TM59 assessment can read.",
                option.Text,
                partOIterationPreparation.VentilationMode,
                option.VentilationStrategy,
                duty,
                equipment,
                partOIterationPreparation.OverheatingScenarios.Count,
                scope);
        }
    }
}
