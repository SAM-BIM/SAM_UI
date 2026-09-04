// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using SAM.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// The one Approved Document O dialog a person who is not a SAM developer should need: pick a scenario
    /// and a scope, read what the model already provides, and run it.
    ///
    /// <para><b>What it replaces</b></para>
    /// <para>
    /// Not the expert commands - they all remain, and the note at the bottom of the window names them. What
    /// it replaces is the requirement to KNOW them: which of Map IC (TM59), AddVent PartF, Prepare Iteration,
    /// Energy Simulation and Overheating (TM59) have already been run over this model, in which order, and
    /// which of them the next step is about to refuse for.
    /// </para>
    ///
    /// <para><b>It computes nothing about the building</b></para>
    /// <para>
    /// Every status line is <see cref="PartOWorkflowInspection"/>'s, and every one of those is an existing
    /// authority's answer. The window owns three things and no more: which controls are enabled, what the
    /// dwelling list is filtered to, and which command the chosen button invokes.
    /// </para>
    ///
    /// <para><b>Built for a large model</b></para>
    /// <para>
    /// The dwelling list is one row per DWELLING ZONE and never one per space, over the same
    /// <see cref="PartODwellingSelection"/> the Prepare Iteration picker uses - virtualized, filtered in
    /// place, selection held on the record. The status list is rebuilt on a change of scenario, scope or
    /// selection, which costs one pass over the spaces in scope; nothing here touches the filesystem or the
    /// catalogue, both of which the caller reads once and hands in through
    /// <see cref="Capabilities"/>.
    /// </para>
    /// </summary>
    public partial class PartOWorkflowWindow : System.Windows.Window
    {
        private PartODwellingSelection dwellingSelection = new([]);

        private List<Zone> zones_Eligible = [];

        private AnalyticalModel? analyticalModel;

        private PartORun? partORun;

        private VentilationUnitCatalogue? ventilationUnitCatalogue;

        private PartOWorkflowCapabilities partOWorkflowCapabilities = new();

        /// <summary>
        /// The TM59 keyword map, read ONCE for the life of the dialog.
        /// <para>
        /// <c>Query.DefaultInternalConditionTextMap_TM59</c> falls back to reading and parsing a resource
        /// file where the session's settings do not carry the map, and the status list is rebuilt on every
        /// change of scenario, scope or selection - so asking it per rebuild would put a file read behind a
        /// checkbox. Null is a valid value and the inspection reports it as such.
        /// </para>
        /// </summary>
        private readonly TextMap textMap_TM59 = Analytical.Query.DefaultInternalConditionTextMap_TM59();

        private bool loaded;

        public PartOWorkflowWindow()
        {
            InitializeComponent();

            //The ceiling the auto-sizing gives way to the scroller at. Read from the work area, so an
            //enlarged system font or a small screen degrades to scrolling with the buttons reachable.
            MaxHeight = SystemParameters.WorkArea.Height * 0.92;

            comboBox_Scenario.ItemsSource = PartOWorkflowScenario.Scenarios;
            comboBox_Scenario.SelectedIndex = 0;
            comboBox_Scenario.SelectionChanged += (s, e) => Refresh();

            List<PartOWorkflowScope> scopes = [PartOWorkflowScope.AllDwellings, PartOWorkflowScope.SelectedDwellings, PartOWorkflowScope.SelectedDwellingsIsolated];

            comboBox_Scope.ItemsSource = scopes.ConvertAll(x => Core.Query.Description(x));
            comboBox_Scope.SelectedIndex = 0;
            comboBox_Scope.SelectionChanged += (s, e) => Refresh();

            textBox_AirFlowStep.Text = PartOOptimisationSettings.DefaultAirFlowStep_Lps.ToString();
            textBox_MaximumIterations.Text = PartOOptimisationSettings.DefaultMaximumIterations.ToString();

            checkBox_CapacityEnvelope.IsChecked = new PartOOptimisationSettings().CapacityEnvelope;
            checkBox_WarmStart.IsChecked = new PartOOptimisationSettings().WarmStart;

            checkBox_Optimise.Checked += (s, e) => Refresh();
            checkBox_Optimise.Unchecked += (s, e) => Refresh();
            textBox_AirFlowStep.TextChanged += (s, e) => Refresh();
            textBox_MaximumIterations.TextChanged += (s, e) => Refresh();

            //The search narrows the VIEW, never the selection: a dwelling filtered out of sight keeps its
            //state and reappears with it intact.
            textBox_Search.TextChanged += (s, e) =>
            {
                dwellingSelection.SearchText = textBox_Search.Text;

                (listBox_Dwellings.ItemsSource as ICollectionView)?.Refresh();

                //Deliberately NOT a full Refresh: the search changes what is visible, never what is
                //selected, so no stage's status can have moved. On a block with thousands of dwellings a
                //per-keystroke re-inspection would be a pass over every space in scope for nothing.
                UpdateSelectionText();
            };

            button_SelectAll.Click += (s, e) => dwellingSelection.SetSelected(true);
            button_SelectNone.Click += (s, e) => dwellingSelection.SetSelected(false);

            loaded = true;

            Refresh();
        }

        /// <summary>
        /// The model this workflow runs over. Setting it asks SAM which of its zones are dwellings - once -
        /// and builds the selectable scope from that answer.
        /// </summary>
        public AnalyticalModel? AnalyticalModel
        {
            set
            {
                analyticalModel = value;

                //The policy call, not a local IsDwelling filter. Asked here, once; nothing re-asks it per
                //click or per keystroke.
                zones_Eligible = Analytical.Query.PartFDwellingZones(value?.GetZones() ?? []) ?? [];

                dwellingSelection = new PartODwellingSelection(zones_Eligible);
                dwellingSelection.Changed += (s, e) => Refresh();

                ICollectionView view = CollectionViewSource.GetDefaultView(dwellingSelection.Items);
                view.Filter = item => dwellingSelection.IsVisible((PartODwellingSelection.Item)item);

                listBox_Dwellings.ItemsSource = view;

                Refresh();
            }
        }

        /// <summary>The session's Part O run. Read for what is already prepared or already assessable.</summary>
        public PartORun? PartORun
        {
            set
            {
                partORun = value;

                Refresh();
            }
        }

        /// <summary>
        /// The catalogue this session read. Read once by the caller, because reading it touches a file and
        /// this window rebuilds its status on every keystroke.
        /// </summary>
        public VentilationUnitCatalogue? VentilationUnitCatalogue
        {
            set
            {
                ventilationUnitCatalogue = value;

                Refresh();
            }
        }

        /// <summary>
        /// The session facts no model can answer - whether there are results to review, whether Iteration 2B
        /// can start, and why not. Supplied by the caller from the authorities that own them.
        /// </summary>
        public PartOWorkflowCapabilities? Capabilities
        {
            set
            {
                partOWorkflowCapabilities = value ?? new PartOWorkflowCapabilities();

                Refresh();
            }
        }

        /// <summary>What the window was closed to do. <see cref="PartOWorkflowAction.None"/> where it was closed.</summary>
        public PartOWorkflowAction Action { get; private set; } = PartOWorkflowAction.None;

        /// <summary>
        /// Re-applies the choices a previous showing of this dialog was closed with, so a person who runs a
        /// baseline and comes back to review or optimise it is not silently returned to the defaults.
        /// <para>
        /// <b>Choices only.</b> Nothing about the run's state is restored - the status list is rebuilt from
        /// the model and the run every time, so restoring a scope that no longer has dwellings in it shows
        /// the blocker rather than a stale READY.
        /// </para>
        /// <para>
        /// A dwelling guid that is no longer an eligible dwelling is silently dropped: the model may have
        /// been re-zoned between the two showings, and selecting a zone that is not offered would be a scope
        /// the preparation cannot honour.
        /// </para>
        /// </summary>
        public void Restore(PartOWorkflowScenario? partOWorkflowScenario, PartOWorkflowScope partOWorkflowScope, IEnumerable<Guid>? guids_Dwelling, PartOOptimisationSettings? partOOptimisationSettings)
        {
            if (partOWorkflowScenario is not null)
            {
                foreach (PartOWorkflowScenario item in comboBox_Scenario.ItemsSource)
                {
                    if (item.Option?.PartOIteration == partOWorkflowScenario.Option?.PartOIteration && item.SelectVentilationUnit == partOWorkflowScenario.SelectVentilationUnit)
                    {
                        comboBox_Scenario.SelectedItem = item;

                        break;
                    }
                }
            }

            Scope = partOWorkflowScope;

            if (guids_Dwelling is not null)
            {
                HashSet<Guid> guids = [.. guids_Dwelling];

                foreach (PartODwellingSelection.Item item in dwellingSelection.Items)
                {
                    item.IsSelected = guids.Contains(item.Guid);
                }
            }

            if (partOOptimisationSettings is not null && checkBox_Optimise.IsEnabled)
            {
                textBox_AirFlowStep.Text = partOOptimisationSettings.AirFlowStep_Lps.ToString();
                textBox_MaximumIterations.Text = partOOptimisationSettings.MaximumIterations.ToString();
                checkBox_CapacityEnvelope.IsChecked = partOOptimisationSettings.CapacityEnvelope;
                checkBox_WarmStart.IsChecked = partOOptimisationSettings.WarmStart;
                checkBox_Optimise.IsChecked = true;
            }

            Refresh();
        }

        /// <summary>The chosen scenario - a base provision plus whether a manufacturer unit is selected.</summary>
        public PartOWorkflowScenario? Scenario => comboBox_Scenario.SelectedItem as PartOWorkflowScenario;

        /// <summary>The chosen scope.</summary>
        public PartOWorkflowScope Scope
        {
            get
            {
                return comboBox_Scope.SelectedIndex switch
                {
                    1 => PartOWorkflowScope.SelectedDwellings,
                    2 => PartOWorkflowScope.SelectedDwellingsIsolated,
                    _ => PartOWorkflowScope.AllDwellings,
                };
            }
            set
            {
                comboBox_Scope.SelectedIndex = value switch
                {
                    PartOWorkflowScope.SelectedDwellings => 1,
                    PartOWorkflowScope.SelectedDwellingsIsolated => 2,
                    _ => 0,
                };
            }
        }

        /// <summary>
        /// The dwelling zones the run covers.
        /// <para>
        /// On <see cref="PartOWorkflowScope.AllDwellings"/> this is SAM's own answer unmodified, whatever the
        /// list below happens to be ticked to - the scope control is the authority, not a stale set of
        /// checkboxes. On the two selected scopes it is that answer as the user narrowed it.
        /// </para>
        /// </summary>
        public List<Zone> Zones_Dwelling => Scope == PartOWorkflowScope.AllDwellings ? [.. zones_Eligible] : dwellingSelection.SelectedZones();

        /// <summary>The selection model behind the dwelling list. Exposed for tests.</summary>
        internal PartODwellingSelection DwellingSelection => dwellingSelection;

        /// <summary>
        /// The Iteration 2B optimisation this run is set up to allow afterwards, or null where none was asked
        /// for, the scenario cannot support one, or the numbers typed in are unusable.
        /// <para>
        /// Validated by <c>PartOOptimisationSettings.IsValid</c> - this window states no rule about a step or
        /// an iteration limit of its own.
        /// </para>
        /// </summary>
        public PartOOptimisationSettings? OptimisationSettings
        {
            get
            {
                if (!Optimise)
                {
                    return null;
                }

                if (!double.TryParse(textBox_AirFlowStep.Text, out double airFlowStep_Lps) || !int.TryParse(textBox_MaximumIterations.Text, out int maximumIterations))
                {
                    return null;
                }

                PartOOptimisationSettings result = new()
                {
                    AirFlowStep_Lps = airFlowStep_Lps,
                    MaximumIterations = maximumIterations,
                    CapacityEnvelope = checkBox_CapacityEnvelope.IsChecked ?? false,
                    WarmStart = checkBox_WarmStart.IsChecked ?? false,
                };

                return result.IsValid(out string? _) ? result : null;
            }
        }

        /// <summary>Whether a follow-on Iteration 2B was asked for and the scenario can carry one.</summary>
        public bool Optimise => (checkBox_Optimise.IsChecked ?? false) && checkBox_Optimise.IsEnabled;

        /// <summary>
        /// Everything the run needs, in the shape the preparation seam takes.
        /// <para>
        /// <b>The request is the user's INTENT, never what this machine happens to be able to do.</b>
        /// <c>SelectVentilationUnit</c> is read off the chosen scenario alone: Iteration 2 stays an
        /// Iteration 2 request when no catalogue can be read, and the missing catalogue is reported as the
        /// blocker it is (<see cref="PartOWorkflowInspection"/>'s Equipment stage, from
        /// <see cref="PartOWorkflowCapabilities.EquipmentAvailable"/>). Folding the capability into the
        /// intent here silently downgraded the requested run to Iteration 1a - a different assessment,
        /// with no equipment selection and no Iteration 2B after it - and reported success.
        /// </para>
        /// </summary>
        public PartOWorkflowRequest Request
        {
            get
            {
                PartOWorkflowScenario? partOWorkflowScenario = Scenario;

                return new PartOWorkflowRequest(partOWorkflowScenario?.Option, Scope, Zones_Dwelling, partOWorkflowScenario is not null && partOWorkflowScenario.SelectVentilationUnit)
                {
                    OptimisationSettings = OptimisationSettings,
                };
            }
        }

        /// <summary>The status the window is currently showing. Exposed so what the user is told is assertable.</summary>
        public PartOWorkflowInspection? Inspection { get; private set; }

        /// <summary>What the window says about the chosen scenario. Exposed for tests.</summary>
        public string ScenarioDescription => textBlock_Scenario.Text;

        /// <summary>What the window says about the chosen scope. Exposed for tests.</summary>
        public string ScopeDescription => textBlock_Scope.Text;

        /// <summary>Why Run is unavailable, as one block of text. Empty where it is available. Exposed for tests.</summary>
        public string BlockerDescription => textBlock_Blockers.Text;

        /// <summary>Whether Prepare and Run is currently offered. Exposed for tests.</summary>
        public bool CanRun => button_Run.IsEnabled;

        /// <summary>Whether Review Results is currently offered. Exposed for tests.</summary>
        public bool CanReviewResults => button_Review.IsEnabled;

        /// <summary>Whether Optimise (2B) is currently offered. Exposed for tests.</summary>
        public bool CanOptimise => button_Optimise.IsEnabled;

        /// <summary>
        /// Rebuilds every derived part of the window from the current controls: the scenario and scope notes,
        /// which controls apply, the status list, and which actions are offered.
        /// <para>
        /// One method rather than a handler per control, because every one of those inputs feeds the same
        /// inspection. Cheap by construction - one pass over the spaces in scope, no file access.
        /// </para>
        /// </summary>
        private void Refresh()
        {
            if (!loaded)
            {
                return;
            }

            PartOWorkflowScenario? partOWorkflowScenario = Scenario;

            UpdateScenarioText(partOWorkflowScenario);
            UpdateScopeControls();
            UpdateOptimiseControls(partOWorkflowScenario);

            PartOWorkflowCapabilities capabilities = new()
            {
                EquipmentAvailable = ventilationUnitCatalogue?.HasSelectableProducts ?? false,
                EquipmentDescription = ventilationUnitCatalogue?.Description ?? "The ventilation unit catalogue has not been read.",
                ResultsAvailable = partOWorkflowCapabilities.ResultsAvailable,
                ResultsRefusal = partOWorkflowCapabilities.ResultsRefusal,
                ResultsRestored = partOWorkflowCapabilities.ResultsRestored,
                Path_Results = partOWorkflowCapabilities.Path_Results,
                OptimisationAvailable = partOWorkflowCapabilities.OptimisationAvailable,
                OptimisationRefusal = partOWorkflowCapabilities.OptimisationRefusal,
            };

            PartOWorkflowInspection partOWorkflowInspection = PartOWorkflowInspection.Inspect(analyticalModel, Request, partORun, capabilities, textMap_TM59);

            Inspection = partOWorkflowInspection;

            List<PartOWorkflowStatusRow> rows = [];
            foreach (PartOWorkflowStageState partOWorkflowStageState in partOWorkflowInspection.Stages)
            {
                rows.Add(new PartOWorkflowStatusRow(partOWorkflowStageState));
            }

            itemsControl_Status.ItemsSource = rows;

            textBlock_Blockers.Text = partOWorkflowInspection.CanRun
                ? string.Empty
                : string.Format("Run is unavailable: {0}", string.Join(" ", partOWorkflowInspection.Blockers));

            button_Run.IsEnabled = partOWorkflowInspection.CanRun;
            button_Run.ToolTip = partOWorkflowInspection.CanRun
                ? (partOWorkflowInspection.ReusePreparation
                    ? "Simulate the iteration already prepared for exactly this scenario and scope, then assess it against the CIBSE TM59 criteria."
                    : "Prepare the iteration, check the model, run the full-year TAS simulation and assess it against the CIBSE TM59 criteria.")
                : textBlock_Blockers.Text;

            button_Review.IsEnabled = partOWorkflowInspection.CanReviewResults;
            button_Review.ToolTip = partOWorkflowInspection.CanReviewResults
                ? "Read this run's existing simulation results and show the CIBSE TM59 assessment. No new simulation is run."
                : partOWorkflowInspection.ResultsRefusal ?? "There are no results to review yet.";

            button_Optimise.IsEnabled = partOWorkflowInspection.CanOptimise;
            button_Optimise.ToolTip = partOWorkflowInspection.CanOptimise
                ? "Raise the design airflow of failing mechanically ventilated rooms by the configured step, rebalance, re-prepare, re-simulate the same weather case and reassess. The selected product is never changed."
                : partOWorkflowInspection.OptimisationRefusal ?? "Iteration 2B optimises a completed Iteration 2 run.";
        }

        private void UpdateScenarioText(PartOWorkflowScenario? partOWorkflowScenario)
        {
            if (partOWorkflowScenario?.Option is null)
            {
                textBlock_Scenario.Text = string.Empty;

                return;
            }

            //The route word is SAM's, carried by the option; this states it rather than choosing it.
            string route = string.Format("Ventilation route stated for every dwelling in scope: {0}.", partOWorkflowScenario.Option.VentilationStrategy);

            string equipment = partOWorkflowScenario.SelectVentilationUnit
                ? " The smallest capable manufacturer unit is selected per dwelling against the realized design duty; a product's maximum is a capability ceiling and never becomes a design airflow."
                : partOWorkflowScenario.Option.PartOVentilationMode == PartOVentilationMode.MVHR
                    ? " No manufacturer unit is selected, so the design duty stands on its own."
                    : " No mechanical system, unit or terminal is created on this route.";

            textBlock_Scenario.Text = route + equipment;
        }

        private void UpdateScopeControls()
        {
            bool selecting = Scope != PartOWorkflowScope.AllDwellings;

            grid_Dwellings.Visibility = selecting ? Visibility.Visible : Visibility.Collapsed;

            int selected = Zones_Dwelling.Count;

            UpdateSelectionText();

            //The isolation assumption is disclosed WHERE THE CHOICE IS MADE, not only in the report a person
            //reads afterwards. Ticking an isolated run is accepting that the interfaces to everything left
            //out are simulated as adiabatic.
            textBlock_Scope.Text = Scope switch
            {
                PartOWorkflowScope.AllDwellings => string.Format("All {0} eligible dwelling zone(s) are assessed, simulated inside the whole building.", zones_Eligible.Count),
                PartOWorkflowScope.SelectedDwellings => string.Format("{0} of {1} eligible dwelling zone(s) are assessed, simulated inside the whole building.", selected, zones_Eligible.Count),
                _ => string.Format("{0} of {1} eligible dwelling zone(s) are assessed, and only those are simulated. Interfaces to excluded spaces are simulated as adiabatic and the surrounding external geometry is retained as shading context, so results may differ from a whole-building simulation. The Part O criteria and the Part F requirements are unchanged.", selected, zones_Eligible.Count),
            };

            //What the loaded model already IS, from the context the preparation stamped on it - said beside
            //the scope choice, because "all dwellings" of an isolated extract is not the whole building.
            PartOIsolationContext partOIsolationContext = analyticalModel?.GetValue<PartOIsolationContext>(Analytical.AnalyticalModelParameter.PartOIsolationContext);

            if (partOIsolationContext is not null && partOIsolationContext.IsValid)
            {
                textBlock_Scope.Text = string.Format("{0} The model currently loaded is ALREADY the isolated thermal model of {1}.", textBlock_Scope.Text, string.Join(", ", partOIsolationContext.Names_Dwelling));
            }
        }

        /// <summary>
        /// The one line about the selection, on its own so that typing in the search box can update it
        /// without rebuilding the whole status list - searching narrows the view and changes no stage.
        /// </summary>
        private void UpdateSelectionText()
        {
            textBlock_Selection.Text = string.IsNullOrWhiteSpace(dwellingSelection.SearchText)
                ? string.Format("{0} of {1} dwelling(s) selected.", dwellingSelection.SelectedCount, dwellingSelection.Count)
                : string.Format("{0} of {1} dwelling(s) selected. The search is narrowing the list; Select All and None apply to what the search matches.", dwellingSelection.SelectedCount, dwellingSelection.Count);
        }

        /// <summary>
        /// Iteration 2B needs a mechanical route and a selected product, so it is offered only on the
        /// scenario that has both - and cleared, not merely greyed, on the others.
        /// <para>
        /// The rule is the scenario's own (<see cref="PartOWorkflowScenario.SupportsOptimisation"/>), which
        /// reads the route off what SAM says the iteration is defined over. This window states nothing about
        /// what natural ventilation is.
        /// </para>
        /// </summary>
        private void UpdateOptimiseControls(PartOWorkflowScenario? partOWorkflowScenario)
        {
            bool available = (partOWorkflowScenario?.SupportsOptimisation ?? false) && (ventilationUnitCatalogue?.HasSelectableProducts ?? false);

            checkBox_Optimise.IsEnabled = available;

            if (!available && (checkBox_Optimise.IsChecked ?? false))
            {
                checkBox_Optimise.IsChecked = false;
            }

            textBox_AirFlowStep.IsEnabled = available;
            textBox_MaximumIterations.IsEnabled = available;
            checkBox_CapacityEnvelope.IsEnabled = available;
            checkBox_WarmStart.IsEnabled = available;

            expander_Optimise.IsEnabled = available;

            if (!available)
            {
                textBlock_Optimise.Text = (partOWorkflowScenario?.Option?.PartOVentilationMode) != PartOVentilationMode.MVHR
                    ? "Iteration 2B raises mechanical design airflow, and this scenario is not a mechanical route. Natural ventilation is not a mechanical airflow optimisation target."
                    : "Iteration 2B works within the capacity of a selected manufacturer unit, so it is available on Iteration 2 only.";

                return;
            }

            textBlock_Optimise.Text = Optimise
                ? "After the full-year simulation and the TM59 assessment, each eligible failing room's DESIGN airflow is raised by the step, the dwelling is rebalanced, the Part O state is rebuilt and the same weather case is re-run - until every eligible space passes, or the selected unit cannot carry another full step. The selected product is never changed and no Approved Document F requirement is altered."
                : "Iteration 2B is not a scenario of its own - it is an optimisation performed on this Iteration 2 design, and it becomes available once this run has been simulated and assessed. Tick this now if you may want it: the step and the limit have to be recorded with the preparation.";
        }

        private void button_Run_Click(object sender, RoutedEventArgs e)
        {
            Action = PartOWorkflowAction.PrepareAndRun;

            DialogResult = true;
        }

        private void button_Review_Click(object sender, RoutedEventArgs e)
        {
            Action = PartOWorkflowAction.ReviewResults;

            DialogResult = true;
        }

        private void button_Optimise_Click(object sender, RoutedEventArgs e)
        {
            Action = PartOWorkflowAction.Optimise;

            DialogResult = true;
        }

        private void button_Close_Click(object sender, RoutedEventArgs e)
        {
            Action = PartOWorkflowAction.None;

            DialogResult = false;
        }
    }
}
