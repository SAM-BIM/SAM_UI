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
            List<VentilationUnitCapacityDescriptor>? ventilationUnitCapacityDescriptors = partOWorkflowRequest.SelectVentilationUnit
                ? ventilationUnitCatalogue.CapacityDescriptors
                : null;

            //Everything this preparation was asked for, kept so an Iteration 2B optimisation can repeat it
            //identically over a changed design. Also carries the optimisation the user asked for, which is
            //not a preparation input and does not affect the call below - see PartOPreparationContext.
            PartOPreparationContext partOPreparationContext = new(option.PartOIteration, zones_Dwelling, dictionary_VentilationStrategy, ventilationUnitCapacityDescriptors)
            {
                OptimisationSettings = partOWorkflowRequest.OptimisationSettings,
                Isolated = partOWorkflowRequest.Isolate,
            };

            PartOIterationPreparation partOIterationPreparation = Analytical.Modify.PreparePartOIteration(analyticalModel, option.PartOIteration, zones_Dwelling, dictionary_VentilationStrategy, ventilationUnitCapacityDescriptors, partOWorkflowRequest.Isolate);

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

            PartOPreparationWindow partOPreparationWindow = new()
            {
                Summary = Summary(partOIterationPreparation, option, ventilationUnitCatalogue, partOWorkflowRequest.SelectVentilationUnit, partOIsolationContext),
                EquipmentRows = EquipmentRows(partOIterationPreparation, ventilationUnitCapacityDescriptors),
                SpaceRows = (partOIterationPreparation.AnalyticalModel.GetSpaces() ?? []).ConvertAll(x => new PartOSpaceRow(x)),
            };

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

            if (!partORun.Prepare(partOIterationPreparation, partOPreparationContext))
            {
                MessageBox.Show(string.Format("The prepared model was not adopted.\n\n{0}", partORun.InvalidationReason));

                return false;
            }

            //Armed immediately before the write, so this replacement is not read as an outside edit.
            partORun.ExpectModification();

            uIAnalyticalModel!.SetJSAMObject(partOIterationPreparation.AnalyticalModel, new FullModification());

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
        private static List<PartOEquipmentRow> EquipmentRows(PartOIterationPreparation partOIterationPreparation, IEnumerable<VentilationUnitCapacityDescriptor>? ventilationUnitCapacityDescriptors)
        {
            List<PartOEquipmentRow> result = [];

            AdjacencyCluster? adjacencyCluster = partOIterationPreparation?.AnalyticalModel?.AdjacencyCluster;
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

        private static string Summary(PartOIterationPreparation partOIterationPreparation, PartOVentilationStrategyOption option, VentilationUnitCatalogue ventilationUnitCatalogue, bool selectVentilationUnit, PartOIsolationContext? partOIsolationContext)
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

            string equipment = selectVentilationUnit
                ? string.Format("Equipment selection ran against {0} selectable product(s). A selected product's Maximum is its capability ceiling and is never a design airflow.", ventilationUnitCatalogue.CapacityDescriptors.Count)
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
