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
    /// place, selection held on the record. A Select All, a None or a restored scope is ONE selection change
    /// however many dwellings it moves, never one per row.
    /// </para>
    /// <para>
    /// <b>The analytical model is inspected only when an inspection input moved.</b> A change of scenario,
    /// scope, dwelling selection, model, run, catalogue or session capability rebuilds the status list
    /// (<see cref="Refresh"/>); the Iteration 2B step, the iteration limit, the follow-on tick and the search
    /// text cannot move a single stage, so they re-derive only this dialog's own state
    /// (<see cref="RefreshWorkflowInput"/>) over the inspection already built. Nothing here touches the
    /// filesystem or the catalogue, both of which the caller reads once and hands in through
    /// <see cref="Capabilities"/>.
    /// </para>
    /// <para>
    /// What ONE rebuild costs is the authorities' own cost - <c>Query.PartFRequiredFlowRate_Lps</c> resolves
    /// a space through the cluster per call, and this window does not second-guess it or cache its answers.
    /// So the guarantee this window makes is not that an inspection is a single pass; it is that a UI-only
    /// interaction does not ask for one at all.
    /// </para>
    /// <para>
    /// <b>And that opening the window is one interaction.</b> Setting the dialog up moves seven inspection
    /// inputs - the model, the run, the catalogue, the capabilities, then the restored scenario, scope and
    /// dwelling scope - and answering each of them separately inspected an initial state nobody would ever
    /// see. Those are deferred and paid once, over the fully restored state; see <see cref="initialising"/>.
    /// After that the window is eager again, and every genuine change inspects when it happens.
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
        /// change of scenario, scope or dwelling selection - so asking it per rebuild would put a file read
        /// behind a checkbox. Null is a valid value and the inspection reports it as such.
        /// </para>
        /// </summary>
        private readonly TextMap textMap_TM59 = Analytical.Query.DefaultInternalConditionTextMap_TM59();

        private bool loaded;

        /// <summary>
        /// Whether the dialog is still being set up, and therefore whether an inspection input moving should
        /// inspect now or be answered once at the end.
        ///
        /// <para><b>The problem this exists for</b></para>
        /// <para>
        /// Opening the hub is one gesture, and it moved seven inspection inputs one at a time. The
        /// constructor settled the controls; the caller then set the model, the run, the catalogue and the
        /// session capabilities; and <see cref="Restore"/> then put back the scenario, the scope and the
        /// saved dwelling scope, each through the very control events the window answers with a full
        /// inspection. Every one of those was a correct response to a genuine change, and all but the last
        /// was a response to a state nobody would ever see - <b>nine inspections of a model to show one
        /// window</b>, eight of them of an initial state that had already been superseded before it was
        /// drawn. On a five thousand space project every one of them walks the dwelling scope.
        /// </para>
        ///
        /// <para><b>It is a deferral, not a cache</b></para>
        /// <para>
        /// Nothing is remembered, compared or reused: the pending flag says an inspection is owed, and when
        /// it is paid it is a full inspection of whatever the window then holds, asking every authority
        /// exactly what it asked before. There is no stored engineering answer here and no attempt to decide
        /// whether an input "really" changed - that would be a second opinion about the model, which is the
        /// thing this window is not allowed to have.
        /// </para>
        ///
        /// <para><b>It ends by itself, and after it ends the window is eager again</b></para>
        /// <para>
        /// Initialisation ends at the first moment the answer is actually needed - the window being shown,
        /// or any derived state being read - as well as at <see cref="CompleteInitialisation"/>, which the
        /// caller calls when it has finished setting the dialog up. From that point every genuine change of
        /// scenario, scope, dwelling selection, model, run, catalogue or capability inspects immediately, as
        /// it always did: a status list that updated only when somebody happened to read it would be a
        /// window showing the scope the user came from.
        /// </para>
        /// </summary>
        private bool initialising = true;

        private bool refresh_Pending;

        /// <summary>
        /// Whether a rebuild is already running, so that a control this window writes <b>during</b> one is not
        /// mistaken for a person moving an inspection input.
        ///
        /// <para><b>The re-entrancy this closes, which predates the deferral above</b></para>
        /// <para>
        /// <see cref="UpdateOptimiseControls"/> clears the Iteration 2B tick where the chosen scenario cannot
        /// carry one - and clearing it raises <c>Unchecked</c>, which this window answers with
        /// <see cref="RefreshWorkflowInput"/>. That reuses the inspection already built, except that on the
        /// FIRST rebuild there is not one yet, so it fell back to a full <see cref="Refresh"/> - from inside
        /// the rebuild that was about to produce the very inspection it was missing. One rebuild therefore
        /// inspected the model twice, every time the tick had to be cleared.
        /// </para>
        /// <para>
        /// Suppressing the nested rebuild loses nothing. The outer one has not reached
        /// <c>PartOWorkflowInspection.Inspect</c> yet, and it derives the actions and the Iteration 2B note
        /// afterwards, from the inspection it then produces and over the controls as the clearing left them.
        /// </para>
        /// </summary>
        private bool refreshing;

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

            //Workflow input, not model input. None of these four can move a single stage of the inspection:
            //the Iteration 2B step, the iteration limit and the follow-on tick are deliberately outside the
            //engineering-preparation match, and no authority the inspection asks looks at them. They are
            //answered by the lightweight refresh, which reuses the inspection already built.
            checkBox_Optimise.Checked += (s, e) => RefreshWorkflowInput();
            checkBox_Optimise.Unchecked += (s, e) => RefreshWorkflowInput();
            textBox_AirFlowStep.TextChanged += (s, e) => RefreshWorkflowInput();
            textBox_MaximumIterations.TextChanged += (s, e) => RefreshWorkflowInput();

            //The search narrows the VIEW, never the selection: a dwelling filtered out of sight keeps its
            //state and reappears with it intact.
            textBox_Search.TextChanged += (s, e) =>
            {
                dwellingSelection.SearchText = textBox_Search.Text;

                (listBox_Dwellings.ItemsSource as ICollectionView)?.Refresh();

                //Deliberately NOT a full Refresh: the search changes what is visible, never what is
                //selected, so no stage's status can have moved. On a block with thousands of dwellings a
                //per-keystroke re-inspection would be a pass over every space in scope for nothing - which
                //is why setting SearchText raises PartODwellingSelection.SearchTextChanged and not its
                //SelectionChanged, the event this window answers with a full inspection.
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

                //The SCOPE moving is a genuine inspection input - different dwellings are different spaces,
                //different Part F requirements and a different preparation match - so this one is answered
                //with the full refresh. It is raised once per gesture, never once per row.
                dwellingSelection.SelectionChanged += (s, e) => Refresh();

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
        /// this window rebuilds its status whenever the requested scenario or scope moves.
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
                //One batched selection change, not one per flipped dwelling. Restoring a narrowed scope on a
                //block-scale model flips hundreds of rows, and this window answers a selection change with a
                //full inspection - so a row-by-row restore ran that inspection hundreds of times.
                dwellingSelection.RestoreSelection(guids_Dwelling);
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
        /// The dwelling search exactly as typed. Exposed for tests, which have to reach the real
        /// <c>TextChanged</c> handler rather than the selection model behind it - the whole point of the
        /// search is what that handler does and does not do.
        /// </summary>
        internal string SearchText
        {
            get => textBox_Search.Text;
            set => textBox_Search.Text = value;
        }

        /// <summary>
        /// How many dwellings the bound list currently shows - the filtered view itself, not the predicate.
        /// Exposed so a test can assert that a search really narrowed what a person sees.
        /// </summary>
        internal int VisibleDwellingCount
        {
            get
            {
                int result = 0;

                if (listBox_Dwellings.ItemsSource is ICollectionView view)
                {
                    foreach (object item in view)
                    {
                        result++;
                    }
                }

                return result;
            }
        }

        /// <summary>What the window says about the current selection and search. Exposed for tests.</summary>
        internal string SelectionDescription
        {
            get { EnsureInspected(); return textBlock_Selection.Text; }
        }

        /// <summary>
        /// The Iteration 2B airflow step exactly as typed. Exposed for tests, which have to state text a
        /// <c>PartOOptimisationSettings</c> cannot represent - the unparseable case
        /// <see cref="OptimisationRefusal"/> exists for.
        /// </summary>
        internal string AirFlowStepText
        {
            get => textBox_AirFlowStep.Text;
            set => textBox_AirFlowStep.Text = value;
        }

        /// <summary>The Iteration 2B iteration limit exactly as typed. Exposed for tests, as above.</summary>
        internal string MaximumIterationsText
        {
            get => textBox_MaximumIterations.Text;
            set => textBox_MaximumIterations.Text = value;
        }

        /// <summary>Whether the Iteration 2B tick is on. Exposed for tests.</summary>
        internal bool OptimiseChecked
        {
            get => checkBox_Optimise.IsChecked ?? false;
            set => checkBox_Optimise.IsChecked = value;
        }

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
        public bool Optimise
        {
            //IsEnabled is written by UpdateOptimiseControls, so this is derived state - and it is what
            //OptimisationSettings, OptimisationRefusal and Request all read, which is why they need no
            //EnsureInspected of their own.
            get { EnsureInspected(); return (checkBox_Optimise.IsChecked ?? false) && checkBox_Optimise.IsEnabled; }
        }

        /// <summary>
        /// Why the Iteration 2B settings as typed cannot be used, or null where they can - or where no
        /// optimisation was asked for at all.
        /// <para>
        /// <b>This is validation of an explicit workflow input, not a statement about the building.</b> It is
        /// deliberately not a <see cref="PartOWorkflowInspection"/> stage: the stages report what the model
        /// and the run provide, and a number somebody mistyped in this dialog is neither. It blocks Run in
        /// its own right, beside them.
        /// </para>
        /// <para>
        /// <b>Ticked 2B with unusable settings must never quietly become no 2B.</b>
        /// <see cref="OptimisationSettings"/> returns null for an unparseable step, an unparseable limit or a
        /// pair <c>PartOOptimisationSettings.IsValid</c> refuses - and a null there is indistinguishable from
        /// "the user did not ask for an optimisation". Without this the baseline ran, discarded the 2B setup
        /// the user could see was ticked, and the follow-on was then unavailable on a run that had already
        /// cost a full-year TAS simulation.
        /// </para>
        /// <para>
        /// <b>The rule is <c>PartOOptimisationSettings.IsValid</c>'s</b>, asked here rather than restated -
        /// exactly as the Prepare Iteration picker asks it. Nothing about a step or an iteration limit is
        /// decided in this window.
        /// </para>
        /// </summary>
        public string? OptimisationRefusal
        {
            get
            {
                if (!Optimise)
                {
                    return null;
                }

                if (!double.TryParse(textBox_AirFlowStep.Text, out double airFlowStep_Lps))
                {
                    return string.Format("'{0}' is not an airflow step. Enter the number of litres per second each failing room's design airflow is raised by each round.", textBox_AirFlowStep.Text);
                }

                if (!int.TryParse(textBox_MaximumIterations.Text, out int maximumIterations))
                {
                    return string.Format("'{0}' is not a number of iterations. Enter the most optimisation rounds the run may take.", textBox_MaximumIterations.Text);
                }

                PartOOptimisationSettings partOOptimisationSettings = new()
                {
                    AirFlowStep_Lps = airFlowStep_Lps,
                    MaximumIterations = maximumIterations,
                };

                return partOOptimisationSettings.IsValid(out string? refusal) ? null : refusal;
            }
        }

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
        public PartOWorkflowInspection? Inspection
        {
            get { EnsureInspected(); return inspection; }

            private set { inspection = value; }
        }

        private PartOWorkflowInspection? inspection;

        /// <summary>What the window says about the chosen scenario. Exposed for tests.</summary>
        public string ScenarioDescription
        {
            get { EnsureInspected(); return textBlock_Scenario.Text; }
        }

        /// <summary>What the window says about the chosen scope. Exposed for tests.</summary>
        public string ScopeDescription
        {
            get { EnsureInspected(); return textBlock_Scope.Text; }
        }

        /// <summary>Why Run is unavailable, as one block of text. Empty where it is available. Exposed for tests.</summary>
        public string BlockerDescription
        {
            get { EnsureInspected(); return textBlock_Blockers.Text; }
        }

        /// <summary>What the window says beside the Iteration 2B fields. Exposed so it is assertable.</summary>
        public string OptimisationDescription
        {
            get { EnsureInspected(); return textBlock_Optimise.Text; }
        }

        /// <summary>Whether Prepare and Run is currently offered. Exposed for tests.</summary>
        public bool CanRun
        {
            get { EnsureInspected(); return button_Run.IsEnabled; }
        }

        /// <summary>Whether Review Results is currently offered. Exposed for tests.</summary>
        public bool CanReviewResults
        {
            get { EnsureInspected(); return button_Review.IsEnabled; }
        }

        /// <summary>Whether Optimise (2B) is currently offered. Exposed for tests.</summary>
        public bool CanOptimise
        {
            get { EnsureInspected(); return button_Optimise.IsEnabled; }
        }

        /// <summary>
        /// Rebuilds every derived part of the window from the current controls: the scenario and scope notes,
        /// which controls apply, the status list, and which actions are offered.
        /// <para>
        /// <b>This is the expensive one, and it is called only when an INSPECTION input moved</b> - the
        /// scenario, the scope, the selected dwellings, the model, the run, the catalogue or the session
        /// capabilities. What it costs is whatever the authorities it asks cost over the spaces in scope; it
        /// reads no file and remembers nothing. Anything that changes only this dialog's own workflow input
        /// goes to <see cref="RefreshWorkflowInput"/> instead.
        /// </para>
        /// </summary>
        private void Refresh()
        {
            if (!loaded)
            {
                return;
            }

            //Still being set up: record that an inspection is owed and pay it once, over the final restored
            //state, rather than once per input the setup moves. See the field.
            if (initialising)
            {
                refresh_Pending = true;

                return;
            }

            //Already rebuilding: a control this window wrote is not a person moving an input. See the field.
            if (refreshing)
            {
                return;
            }

            refreshing = true;

            try
            {
                RefreshCore();
            }
            finally
            {
                refreshing = false;
            }
        }

        /// <summary>
        /// Ends the deferred-initialisation window: <b>one</b> inspection, of the state the dialog was set up
        /// into, and eager refreshing from then on.
        /// <para>
        /// Called by the caller once it has finished setting the dialog up, and by the window itself the
        /// moment the answer is needed - it is shown, or something derived from an inspection is read - so a
        /// caller that never calls it is never left looking at a window derived from nothing. Calling it
        /// twice does nothing the second time.
        /// </para>
        /// <para>
        /// It inspects only where an inspection is actually owed. Ending initialisation on a dialog nothing
        /// was set on does not manufacture one.
        /// </para>
        /// </summary>
        public void CompleteInitialisation()
        {
            if (!initialising)
            {
                return;
            }

            initialising = false;

            if (!refresh_Pending)
            {
                return;
            }

            refresh_Pending = false;

            Refresh();
        }

        /// <summary>
        /// Called from every member whose value a <see cref="RefreshCore"/> writes, so reading the window's
        /// derived state always ends initialisation first and reads the real answer rather than a blank one.
        /// </summary>
        private void EnsureInspected()
        {
            CompleteInitialisation();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            //Showing the window is the last possible moment: whatever else the caller did or did not do, what
            //a person is about to look at is inspected before they look at it.
            CompleteInitialisation();

            base.OnSourceInitialized(e);
        }

        /// <summary>
        /// How many times this window has inspected the analytical model since it was constructed.
        /// <para>
        /// <b>Exposed for tests, and it is the only honest way to assert the claim.</b> Object identity says
        /// an inspection did or did not happen between two reads; it cannot say that opening the dialog ran
        /// one rather than nine, which is exactly what the deferral above is for. A count is exact and the
        /// same on every machine; a stopwatch is neither.
        /// </para>
        /// </summary>
        internal int InspectionCount { get; private set; }

        private void RefreshCore()
        {
            InspectionCount++;

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

            UpdateActions(partOWorkflowInspection);
        }

        /// <summary>
        /// Re-derives only what this dialog's own workflow input can change: the Iteration 2B note, the
        /// combined Run blocker line and which actions are offered.
        /// <para>
        /// <b>It reuses the inspection already built and never asks for another one.</b> The Iteration 2B
        /// step, the iteration limit and the follow-on tick cannot move the dwelling scope, the TM59
        /// mapping, the Approved Document F requirements, the prepared ventilation design, the equipment
        /// availability, the model-check state, the simulation or results state, or the
        /// engineering-preparation reuse match - the settings are deliberately excluded from that match, and
        /// nothing <see cref="PartOWorkflowInspection.Inspect"/> asks reads them. Re-inspecting on a
        /// keystroke re-asked every authority over every space in scope to re-read two numbers none of them
        /// looks at.
        /// </para>
        /// <para>
        /// Falls back to the full <see cref="Refresh"/> only where there is no inspection to reuse yet, so
        /// the window is never left showing actions derived from nothing.
        /// </para>
        /// <para>
        /// <b>It does nothing at all while the dialog is still being set up</b>, and it reads the inspection
        /// off the field rather than the property. Restoring the saved optimisation settings writes four of
        /// these controls, and each write lands here; going through the property would have ended the
        /// deferral - and inspected - on the first of them, over a state the restore had not finished
        /// building. There is nothing to lose by returning: a full refresh is already owed, and it derives
        /// everything this does.
        /// </para>
        /// </summary>
        private void RefreshWorkflowInput()
        {
            //`refreshing` for the same reason Refresh checks it: the rebuild already running writes these
            //very controls and derives everything below afterwards.
            if (!loaded || initialising || refreshing)
            {
                return;
            }

            PartOWorkflowInspection? partOWorkflowInspection = inspection;
            if (partOWorkflowInspection is null)
            {
                Refresh();

                return;
            }

            UpdateOptimiseControls(Scenario);

            UpdateActions(partOWorkflowInspection);
        }

        /// <summary>
        /// Which actions are offered, and why not - from the supplied inspection plus this dialog's own
        /// workflow-input refusal. Shared by the full and the lightweight refresh so both state exactly the
        /// same rule.
        /// </summary>
        private void UpdateActions(PartOWorkflowInspection partOWorkflowInspection)
        {
            //Two kinds of reason, kept apart on purpose. The inspection's blockers are what the MODEL and the
            //run do not provide; the optimisation refusal is what was typed into THIS dialog and cannot be
            //used. Both stop Run, and the text says which is which.
            List<string> reasons = [.. partOWorkflowInspection.Blockers];

            string? refusal_Optimisation = OptimisationRefusal;
            if (refusal_Optimisation is not null)
            {
                reasons.Add(string.Format("Iteration 2B is ticked, but its settings cannot be used: {0} Correct them, or untick Iteration 2B to run the baseline without it.", refusal_Optimisation));
            }

            bool canRun = reasons.Count == 0;

            textBlock_Blockers.Text = canRun
                ? string.Empty
                : string.Format("Run is unavailable: {0}", string.Join(" ", reasons));

            button_Run.IsEnabled = canRun;
            button_Run.ToolTip = canRun
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
            PartOIsolationContext? partOIsolationContext = analyticalModel?.GetValue<PartOIsolationContext>(Analytical.AnalyticalModelParameter.PartOIsolationContext);

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

            //Said at the fields as well as in the blocker line below the status list: the blocker line is
            //always visible and states that Run is stopped, and this states it where the numbers are typed.
            string? refusal = OptimisationRefusal;
            if (refusal is not null)
            {
                textBlock_Optimise.Text = string.Format("These settings cannot be used, so Prepare & Run is unavailable. {0}", refusal);

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
