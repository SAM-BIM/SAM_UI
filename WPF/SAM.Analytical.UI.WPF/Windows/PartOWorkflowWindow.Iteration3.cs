// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using SAM.Core.Tas;
using SAM.Weather;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// The Hub's Iteration 3 panel, its Simulation case and its last-outcome line.
    ///
    /// <para><b>Iteration 3, in the engineer's terms</b></para>
    /// <para>
    /// The reference case is the completed earlier iteration; the system case is the same design with its
    /// ventilation run as an explicit TAS system, by one chosen method. The panel says, before anything runs,
    /// which earlier iteration is the reference, what each method would change and to which product, whether
    /// a method already has a completed result, and - for a product method - why it cannot run where it
    /// cannot. Nothing here decides anything: the eligibility and the pre-flight are the authorities'
    /// answers (<see cref="Query.PartOIteration3Eligibility"/>, <see cref="Query.PartOIteration3Preflight"/>).
    /// </para>
    /// <para>
    /// <b>Cost.</b> The eligibility is read once per showing by the caller. A method's pre-flight is computed
    /// the first time that method is looked at in a showing, and kept for the rest of it - never per keystroke
    /// and never for a method nobody selected.
    /// </para>
    /// </summary>
    public partial class PartOWorkflowWindow
    {
        private PartOIteration3Eligibility? iteration3Eligibility;

        private readonly Dictionary<PartOIteration3BehaviourMode, PartOIteration3Preflight> preflights = [];

        private readonly Dictionary<PartOIteration3BehaviourMode, RadioButton> radioButtons_Method = [];

        private readonly Dictionary<PartOIteration3BehaviourMode, TextBlock> textBlocks_MethodStatus = [];

        /// <summary>The method every showing starts on, and the one a product-method run is for.</summary>
        internal const PartOIteration3BehaviourMode Iteration3DefaultMode = PartOIteration3BehaviourMode.SelectedProductManufacturerGuidance;

        private PartOIteration3BehaviourMode iteration3Mode = Iteration3DefaultMode;

        /// <summary>
        /// Whether a person chose the method (a click, or a method carried from an earlier showing). Where none was
        /// chosen and the default cannot run while another method can, the Hub selects that one itself rather than
        /// open on a refusal - see <see cref="ApplyAutomaticIteration3Method"/>.
        /// </summary>
        private bool iteration3Mode_Stated;

        private bool iteration3Automatic;

        private bool writing_Iteration3;

        private void InitialiseIteration3()
        {
            foreach (PartOIteration3BehaviourMode partOIteration3BehaviourMode in Query.PartOIteration3BehaviourModes)
            {
                TextBlock textBlock_Status = new()
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                    FontSize = 11,
                    TextWrapping = TextWrapping.Wrap,
                };

                StackPanel stackPanel = new();
                stackPanel.Children.Add(new TextBlock { Text = Query.PartOIteration3MethodLabel(partOIteration3BehaviourMode), TextWrapping = TextWrapping.Wrap });
                stackPanel.Children.Add(textBlock_Status);

                RadioButton radioButton = new()
                {
                    GroupName = "PartOIteration3Method",
                    Content = stackPanel,
                    Margin = new Thickness(0, 3, 0, 3),
                    Tag = partOIteration3BehaviourMode,
                };

                radioButton.Checked += (s, e) =>
                {
                    if (writing_Iteration3)
                    {
                        return;
                    }

                    iteration3Mode = (PartOIteration3BehaviourMode)((RadioButton)s).Tag;
                    iteration3Mode_Stated = true;

                    //Choosing a method is working in Iteration 3.
                    iteration3InFocus = true;

                    UpdateMethodSelection();

                    RefreshIteration3();
                };

                (Query.IsPartOIteration3ValidationMethod(partOIteration3BehaviourMode) ? stackPanel_It3MethodsAdvanced : stackPanel_It3Methods).Children.Add(radioButton);

                radioButtons_Method[partOIteration3BehaviourMode] = radioButton;
                textBlocks_MethodStatus[partOIteration3BehaviourMode] = textBlock_Status;
            }

            //The weather selector the Simulate dialog used, configured the same way, so the same files are
            //offered and read the same way.
            selectSAMObjectComboBoxControl_Weather.ValidateFunc = new Func<IJSAMObject, bool>(x => x is WeatherData);
            selectSAMObjectComboBoxControl_Weather.ReadFunc = new Func<string, IJSAMObject>(x => UI.Query.TryGetWeatherData(x, out WeatherData weatherData_Read) ? weatherData_Read : null);
            selectSAMObjectComboBoxControl_Weather.DialogFilter = "epw files (*.epw)|*.epw|TAS TBD files (*.tbd)|*.tbd|TAS TSD files (*.tsd)|*.tsd|TAS TWD files (*.twd)|*.twd|All files (*.*)|*.*";
            selectSAMObjectComboBoxControl_Weather.DialogFilterIndex = 1;
            selectSAMObjectComboBoxControl_Weather.SelectionChanged += (s, e) => RefreshSimulationCase();

            foreach (SolarCalculationMethod solarCalculationMethod in Enum.GetValues(typeof(SolarCalculationMethod)))
            {
                if (solarCalculationMethod != SolarCalculationMethod.Undefined)
                {
                    comboBox_SolarCalculationMethod.Items.Add(Core.Query.Description(solarCalculationMethod));
                }
            }

            comboBox_SolarCalculationMethod.SelectionChanged += (s, e) => RefreshSimulationCase();
            //Checked / Unchecked rather than Click: a click is only one way to change the box (the keyboard and UI
            //Automation change it without one), and the header and the case it stands for must follow every change.
            checkBox_DirectT3D.Checked += (s, e) => RefreshSimulationCase();
            checkBox_DirectT3D.Unchecked += (s, e) => RefreshSimulationCase();
            textBox_OutputDirectory.TextChanged += (s, e) => RefreshSimulationCase();

            button_OutputDirectory.Click += (s, e) =>
            {
                using System.Windows.Forms.FolderBrowserDialog folderBrowserDialog = new()
                {
                    SelectedPath = textBox_OutputDirectory.Text,
                };

                if (folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    textBox_OutputDirectory.Text = folderBrowserDialog.SelectedPath;
                }
            };

            UpdateMethodSelection();
        }

        //---------------------------------------------------------------------------------------------
        //Iteration 3
        //---------------------------------------------------------------------------------------------

        /// <summary>
        /// What Iteration 3 can do with the session's run - asked by the caller once per showing, because it
        /// looks for saved results on disk. Setting it forgets every pre-flight computed for the previous one.
        /// </summary>
        public PartOIteration3Eligibility? Iteration3Eligibility
        {
            get => iteration3Eligibility;
            set
            {
                iteration3Eligibility = value;

                preflights.Clear();

                RefreshIteration3();
            }
        }

        /// <summary>
        /// The Iteration 3 method the Hub is ON - the one Run Iteration 3 and Open result act on, whether a person chose it
        /// or the Hub selected it. What is carried to the next showing is <see cref="Iteration3ModeCarried"/>.
        /// </summary>
        public PartOIteration3BehaviourMode Iteration3Mode
        {
            get => iteration3Mode;
            set
            {
                iteration3Mode = value;
                iteration3Mode_Stated = value != Iteration3DefaultMode;

                UpdateMethodSelection();

                RefreshIteration3();
            }
        }

        /// <summary>
        /// The method to carry to the next showing: the one a person chose. A method the Hub selected itself is not a
        /// choice, so it is not carried - the next showing starts from the default and decides again.
        /// </summary>
        public PartOIteration3BehaviourMode Iteration3ModeCarried => iteration3Automatic ? Iteration3DefaultMode : iteration3Mode;

        /// <summary>The chosen method's pre-flight, or null where Iteration 3 cannot run at all. Exposed for tests.</summary>
        internal PartOIteration3Preflight? Iteration3Preflight => Preflight(iteration3Mode);

        /// <summary>What the chosen method's status line says. Exposed for tests.</summary>
        internal string Iteration3StatusText(PartOIteration3BehaviourMode partOIteration3BehaviourMode)
        {
            return textBlocks_MethodStatus.TryGetValue(partOIteration3BehaviourMode, out TextBlock textBlock) ? textBlock.Text : null;
        }

        /// <summary>How many methods read as chosen - always exactly one. Exposed for tests.</summary>
        internal int Iteration3CheckedCount => radioButtons_Method.Values.Count(x => x.IsChecked == true);

        /// <summary>Whether Open result is offered for the chosen method. Exposed for tests.</summary>
        internal bool CanOpenIteration3Result => button_It3Review.IsEnabled;

        /// <summary>What the Open result button currently says. Exposed for tests.</summary>
        internal string Iteration3ReviewText => button_It3Review.Content as string;

        /// <summary>What the Iteration 3 panel says about the reference case. Exposed for tests.</summary>
        internal string Iteration3ReferenceText => textBlock_It3Reference.Text;

        /// <summary>The refusal the chosen method's pre-flight shows, or empty. Exposed for tests.</summary>
        internal string Iteration3RefusalText => textBlock_It3Refusal.Text;

        /// <summary>The method the Hub is on, whether chosen or selected for the person. Exposed for tests.</summary>
        internal PartOIteration3BehaviourMode Iteration3EffectiveMode => iteration3Mode;

        /// <summary>Whether the Hub selected the method itself. Exposed for tests.</summary>
        internal bool Iteration3MethodIsAutomatic => iteration3Automatic;

        /// <summary>What the notice about an automatically selected method says, or empty. Exposed for tests.</summary>
        internal string Iteration3AutomaticText => border_It3Automatic.Visibility == Visibility.Visible ? textBlock_It3Automatic.Text : string.Empty;

        /// <summary>Whether the panel offers to open Mixed Design for the missing cooling control rooms. Exposed for tests.</summary>
        internal bool Iteration3MixedDesignOffered => button_It3MixedDesign.Visibility == Visibility.Visible;

        /// <summary>Whether the jump to the Iteration 3 panel is offered. Exposed for tests.</summary>
        internal bool GoToIteration3Offered => stackPanel_GoToIteration3.Visibility == Visibility.Visible;

        private PartOIteration3Preflight? Preflight(PartOIteration3BehaviourMode partOIteration3BehaviourMode)
        {
            if (iteration3Eligibility is null || !iteration3Eligibility.CanRun)
            {
                return null;
            }

            if (!preflights.TryGetValue(partOIteration3BehaviourMode, out PartOIteration3Preflight partOIteration3Preflight))
            {
                partOIteration3Preflight = Query.PartOIteration3Preflight(partORun, iteration3Eligibility, partOIteration3BehaviourMode, ventilationUnitCatalogue);

                preflights[partOIteration3BehaviourMode] = partOIteration3Preflight;
            }

            return partOIteration3Preflight;
        }

        private void UpdateMethodSelection()
        {
            if (!radioButtons_Method.ContainsKey(iteration3Mode))
            {
                return;
            }

            writing_Iteration3 = true;

            try
            {
                //Every button set explicitly. GroupName alone does not clear a button inside a disclosure that
                //has not been opened yet - it is not in the same visual tree - so two methods could read as
                //chosen at once.
                foreach (KeyValuePair<PartOIteration3BehaviourMode, RadioButton> keyValuePair in radioButtons_Method)
                {
                    keyValuePair.Value.IsChecked = keyValuePair.Key == iteration3Mode;
                }

                //A validation method that is chosen is never hidden behind a closed disclosure.
                if (Query.IsPartOIteration3ValidationMethod(iteration3Mode))
                {
                    expander_It3Advanced.IsExpanded = true;
                }
            }
            finally
            {
                writing_Iteration3 = false;
            }
        }

        private void RefreshIteration3()
        {
            if (!loaded)
            {
                return;
            }

            PartOIteration3Eligibility? partOIteration3Eligibility = iteration3Eligibility;

            bool canRun = partOIteration3Eligibility?.CanRun ?? false;
            bool anyRecorded = partOIteration3Eligibility is not null && partOIteration3Eligibility.PairingStatuses.Exists(x => x.Exists);

            ApplyAutomaticIteration3Method(canRun);

            //The reference case, in the engineer's terms.
            if (partORun is not null && (canRun || anyRecorded))
            {
                string results = string.IsNullOrWhiteSpace(partORun.Path_TSD) ? string.Empty : string.Format(" · results {0}", System.IO.Path.GetFileName(partORun.Path_TSD));

                textBlock_It3Reference.Text = string.Format(
                    "Reference case: {0}{1}{2}",
                    Query.PartOIterationText(partORun),
                    results,
                    partORun.IsRestored ? " · reopened from the saved run, so it does not need to be run again" : string.Empty);
            }
            else
            {
                textBlock_It3Reference.Text = partOIteration3Eligibility?.Refusal_Run
                    ?? "Available once an Iteration 1a or Iteration 2 run has completed its full-year simulation. The completed run becomes the reference case.";
            }

            stackPanel_It3.Visibility = canRun || anyRecorded ? Visibility.Visible : Visibility.Collapsed;

            //The whole panel waits until it is relevant: a completed reference run it can compare against, or
            //a method that already has a record. Before that it was a heading over a refusal, on a model that
            //had not run its Iteration 1a yet. The eligibility is unchanged; only its visibility is.
            border_Iteration3.Visibility = canRun || anyRecorded ? Visibility.Visible : Visibility.Collapsed;

            //The panel sits below the readiness list and the equipment selection, which on most screens is below
            //the fold: the actions row offers the way down to it, wherever it is relevant.
            stackPanel_GoToIteration3.Visibility = border_Iteration3.Visibility;

            //Every method's own line: a completed result, a last attempt that did not complete, or nothing.
            foreach (KeyValuePair<PartOIteration3BehaviourMode, TextBlock> keyValuePair in textBlocks_MethodStatus)
            {
                keyValuePair.Value.Text = StatusText(partOIteration3Eligibility?.PairingStatus(keyValuePair.Key));
            }

            PartOIteration3BehaviourMode partOIteration3BehaviourMode = iteration3Mode;
            PartOIteration3PairingStatus? partOIteration3PairingStatus = partOIteration3Eligibility?.PairingStatus(partOIteration3BehaviourMode);

            textBlock_It3Explanation.Text = Query.PartOIteration3MethodExplanation(partOIteration3BehaviourMode);

            //The pre-flight - only where a run is possible at all.
            PartOIteration3Preflight? partOIteration3Preflight = canRun ? Preflight(partOIteration3BehaviourMode) : null;

            if (partOIteration3Preflight is null)
            {
                textBlock_It3UnitsHeading.Visibility = Visibility.Collapsed;
                itemsControl_It3Summary.ItemsSource = null;
                expander_It3Units.Visibility = Visibility.Collapsed;
                textBlock_It3Refusal.Text = string.Empty;
            }
            else
            {
                int count = partOIteration3Preflight.Units.Count;

                textBlock_It3UnitsHeading.Visibility = count == 0 ? Visibility.Collapsed : Visibility.Visible;
                textBlock_It3UnitsHeading.Text = Query.IsPartOIteration3ProductMethod(partOIteration3BehaviourMode)
                    ? string.Format(CultureInfo.CurrentCulture, "Ventilation {0} in the system case:", count == 1 ? "unit" : string.Format(CultureInfo.CurrentCulture, "units ({0})", count))
                    : string.Format(CultureInfo.CurrentCulture, "Ventilation {0} in the system case, at the reference case's design airflows:", count == 1 ? "unit" : string.Format(CultureInfo.CurrentCulture, "units ({0})", count));

                itemsControl_It3Summary.ItemsSource = partOIteration3Preflight.Summary();

                expander_It3Units.Visibility = count > 1 ? Visibility.Visible : Visibility.Collapsed;
                expander_It3Units.Header = string.Format(CultureInfo.CurrentCulture, "Show all {0} units", count);
                listBox_It3Units.ItemsSource = partOIteration3Preflight.Units;

                //A missing step is shown as the journey it is (RefreshSteps), not as a red refusal.
                textBlock_It3Refusal.Text = IsMissingRooms(partOIteration3Preflight) ? string.Empty : Refusal(partOIteration3Preflight);
            }

            RefreshAutomaticNotice(canRun);

            RefreshSteps(canRun);

            //The two actions.
            bool reviewable = partOIteration3PairingStatus?.IsReviewable ?? false;
            bool refused = partOIteration3PairingStatus?.IsRefused ?? false;

            button_It3Review.IsEnabled = reviewable || refused;
            button_It3Review.Content = reviewable || !refused ? "Open result" : "Show last attempt";
            button_It3Review.ToolTip = reviewable
                ? "Show this method's completed result, rebuilt from the saved results. No TAS simulation is run."
                : refused
                    ? "Show what the last attempt of this method did and where it stopped. No TAS simulation is run."
                    : "This method has no saved result for this reference case yet.";

            bool canRunMethod = canRun && (partOIteration3Preflight?.CanRun ?? false);

            button_Iteration3.IsEnabled = canRunMethod;
            //Named as the iteration it is: next to Prepare & Run (which re-runs the reference iteration) a
            //"Run system case" button did not say it was the step that starts Iteration 3.
            button_Iteration3.Content = reviewable ? "Run Iteration 3 again" : "Run Iteration 3";
            button_Iteration3.ToolTip = canRunMethod
                ? "Build the system case and run it in TAS: a full-year building simulation, a TAS Systems simulation and a full-year resultant-temperature run, then the TM59 comparison. This takes several minutes."
                : !canRun
                    ? partOIteration3Eligibility?.Refusal_Run ?? "Iteration 3 needs a completed Iteration 1a or Iteration 2 run."
                    : "This method cannot run for the reason shown above.";

            textBlock_It3Hint.Text = reviewable
                ? "A completed result exists for this method. Open result shows it without running TAS; Run again replaces it."
                : !canRun && anyRecorded
                    ? partOIteration3Eligibility?.Refusal_Run ?? string.Empty
                    : canRunMethod
                        ? "Runs TAS several times over the full year, so it takes several minutes. Progress is shown in its own window."
                        : string.Empty;

            UpdateReviewTarget(reviewable);
        }

        //---------------------------------------------------------------------------------------------
        //Which case the primary Review action opens
        //---------------------------------------------------------------------------------------------

        private bool iteration3InFocus;

        private bool reviewResults_Enabled;

        private string? reviewResults_ToolTip;

        private string reviewResults_Caption = "No results yet";

        /// <summary>
        /// Whether the person is working in Iteration 3 - the Hub reopened after an Iteration 3 run or an
        /// opened Iteration 3 result, or a method was chosen here. While it holds and the chosen method has a
        /// completed result, the primary Review action opens THAT result (the comparison, with the reference
        /// and system TM59 reports inside it) rather than silently opening the reference iteration's TM59.
        /// Choosing another scenario clears it.
        /// </summary>
        public bool Iteration3InFocus
        {
            get => iteration3InFocus;
            set
            {
                iteration3InFocus = value;

                RefreshIteration3();
            }
        }

        /// <summary>Whether the primary Review action currently opens the Iteration 3 result. For a test to read.</summary>
        internal bool ReviewOpensIteration3 { get; private set; }

        /// <summary>What the primary Review action says. For a test to read.</summary>
        internal string ReviewActionText => button_Review.Content as string ?? string.Empty;

        private void UpdateReviewTarget(bool reviewable_Iteration3)
        {
            ReviewOpensIteration3 = iteration3InFocus && reviewable_Iteration3;

            if (ReviewOpensIteration3)
            {
                button_Review.Content = "Review Iteration 3 result";
                button_Review.IsEnabled = true;
                button_Review.ToolTip = string.Format(
                    "Open the Iteration 3 result for '{0}' - explicit system and cooling assessment, compared with its reference case. The reference and system TM59 reports are available inside it. No TAS simulation is run.",
                    Query.PartOIteration3MethodLabel(iteration3Mode));
                textBlock_ReviewCaption.Text = "Iteration 3";

                return;
            }

            button_Review.Content = "Review Results";
            button_Review.IsEnabled = reviewResults_Enabled;
            button_Review.ToolTip = reviewResults_ToolTip;
            textBlock_ReviewCaption.Text = reviewResults_Caption;
        }

        /// <summary>One method's status line.</summary>
        private static string StatusText(PartOIteration3PairingStatus? partOIteration3PairingStatus)
        {
            if (partOIteration3PairingStatus is null)
            {
                return "Not run yet";
            }

            if (partOIteration3PairingStatus.IsReviewable)
            {
                PartOIteration3Record partOIteration3Record = partOIteration3PairingStatus.Record;

                return string.Format(
                    CultureInfo.CurrentCulture,
                    "✓ Result available{0} · reference {1} / system {2}",
                    partOIteration3PairingStatus.When.HasValue ? " · " + partOIteration3PairingStatus.When.Value.ToString("d MMM yyyy HH:mm", CultureInfo.CurrentCulture) : string.Empty,
                    Verdict(partOIteration3Record.Status_ReferenceA),
                    Verdict(partOIteration3Record.Status_CandidateB));
            }

            if (partOIteration3PairingStatus.IsRefused)
            {
                PartOIteration3Stage? partOIteration3Stage = partOIteration3PairingStatus.Record.Stage_Refused;

                return string.Format(
                    CultureInfo.CurrentCulture,
                    "Last attempt did not complete{0}{1} — it can be run again",
                    partOIteration3Stage.HasValue ? " (" + Core.Query.Description(partOIteration3Stage.Value).ToLowerInvariant() + ")" : string.Empty,
                    partOIteration3PairingStatus.When.HasValue ? " · " + partOIteration3PairingStatus.When.Value.ToString("d MMM yyyy HH:mm", CultureInfo.CurrentCulture) : string.Empty);
            }

            return partOIteration3PairingStatus.Refusal_Read is not null ? "A saved result could not be read" : "Not run yet";
        }

        private static string Verdict(TM59ComplianceStatus tM59ComplianceStatus)
        {
            //Undefined is a saved record with no status - unknown, not "not assessed".
            return tM59ComplianceStatus == TM59ComplianceStatus.Undefined ? "—" : Query.PartOVerdictText(tM59ComplianceStatus);
        }

        /// <summary>
        /// The pre-flight's refusal, bounded: a reason that is not about one unit in full, otherwise how many
        /// units cannot run and the first unit's reason - on a block of flats sharing one product the reason
        /// is the same sentence per flat, and repeating it a thousand times says nothing more. Every unit's own
        /// reason is on its row in the unit list.
        /// </summary>
        private static string Refusal(PartOIteration3Preflight partOIteration3Preflight)
        {
            if (partOIteration3Preflight is null || partOIteration3Preflight.CanRun)
            {
                return string.Empty;
            }

            List<PartOIteration3PreflightUnit> units_Refused = partOIteration3Preflight.Units.FindAll(x => !x.IsReady);

            List<string> refusals_General = partOIteration3Preflight.Refusals.FindAll(x => !units_Refused.Exists(y => y.Refusal == x));

            if (refusals_General.Count != 0)
            {
                string result = string.Format("This method cannot run yet: {0}", refusals_General[0]);

                int count_More = refusals_General.Count - 1 + units_Refused.Count;

                return count_More == 0 ? result : result + string.Format(CultureInfo.CurrentCulture, " (and {0} more reason(s))", count_More);
            }

            //Every unit refused for having no product: that is not a unit fault, it is a missing step, and the only
            //place that said which one was under Advanced / Details. Say it here, where the method is chosen.
            if (units_Refused.Count == partOIteration3Preflight.Units.Count && units_Refused.TrueForAll(x => x.Product == "No product selected"))
            {
                return "This method compares against a selected manufacturer unit, and no unit in this reference case has one. Run Iteration 2 (MVHR with manufacturer unit) first: it selects a product for each unit, and Iteration 3 then runs against that Iteration 2 result.";
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                "This method cannot run yet: {0} of {1} ventilation unit(s) cannot run. {2}{3}",
                units_Refused.Count,
                partOIteration3Preflight.Units.Count,
                units_Refused[0].Refusal,
                units_Refused.Count > 1 ? " Each unit's reason is in the unit list." : string.Empty);
        }

        /// <summary>
        /// Whether the method is refused for the one reason that is a missing STEP rather than a fault: no saved
        /// cooling control rooms. It is shown as the steps that supply them
        /// (<see cref="Query.PartOIteration3ProductMethodSteps"/>), not as a refusal.
        /// </summary>
        private static bool IsMissingRooms(PartOIteration3Preflight? partOIteration3Preflight)
        {
            return partOIteration3Preflight is not null && !partOIteration3Preflight.CanRun && partOIteration3Preflight.Refusals.Contains(Query.PartOIteration3NoDwellingStrategiesRefusal);
        }

        /// <summary>
        /// The journey to a runnable manufacturer-guidance method, where that is what is missing: what the method needs,
        /// then each step in order - done, current, later - with a button on the current one where the Hub can do it.
        /// Shown for the method a person chose, or for the default while another is selected on their behalf.
        /// </summary>
        private void RefreshSteps(bool canRun)
        {
            stackPanel_It3StepRows.Children.Clear();
            button_It3RemoveResults.Visibility = Visibility.Collapsed;
            button_It3MixedDesign.Visibility = Visibility.Collapsed;

            PartOIteration3Preflight? partOIteration3Preflight = canRun ? Preflight(iteration3Automatic ? Iteration3DefaultMode : iteration3Mode) : null;

            if (!IsMissingRooms(partOIteration3Preflight))
            {
                border_It3Steps.Visibility = Visibility.Collapsed;
                textBlock_It3StepsLead.Text = string.Empty;

                return;
            }

            List<PartOIteration3Step> steps = Query.PartOIteration3ProductMethodSteps(analyticalModel, partORun, out string lead);

            textBlock_It3StepsLead.Text = lead;

            for (int i = 0; i < steps.Count; i++)
            {
                PartOIteration3Step step = steps[i];

                (string glyph, Brush brush, FontWeight fontWeight) = step.State switch
                {
                    PartOIteration3StepState.Done => ("✓", (Brush)new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)), FontWeights.Normal),
                    PartOIteration3StepState.Current => ("→", new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)), FontWeights.SemiBold),
                    _ => ("○", new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)), FontWeights.Normal),
                };

                Grid grid = new() { Margin = new Thickness(0, 2, 0, 2) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                grid.Children.Add(new TextBlock { Text = glyph, Foreground = brush, FontWeight = fontWeight });

                TextBlock textBlock = new() { Text = string.Format(CultureInfo.CurrentCulture, "{0}. {1}", i + 1, step.Text), Foreground = brush, FontWeight = fontWeight, TextWrapping = TextWrapping.Wrap };
                Grid.SetColumn(textBlock, 1);
                grid.Children.Add(textBlock);

                stackPanel_It3StepRows.Children.Add(grid);

                if (step.State == PartOIteration3StepState.Current && step.ButtonText is not null)
                {
                    Button button = step.Action == PartOWorkflowAction.RemoveResults ? button_It3RemoveResults : step.Action == PartOWorkflowAction.MixedDesign ? button_It3MixedDesign : null;

                    if (button is not null)
                    {
                        button.Content = step.ButtonText;
                        button.Visibility = Visibility.Visible;
                    }
                }
            }

            border_It3Steps.Visibility = Visibility.Visible;
        }

        /// <summary>The steps as shown - the sentence, then one line per step - or empty. Exposed for tests.</summary>
        internal string Iteration3StepsText => border_It3Steps.Visibility == Visibility.Visible
            ? string.Join("\n", new[] { textBlock_It3StepsLead.Text }.Concat(stackPanel_It3StepRows.Children.OfType<Grid>().Select(x => string.Join(" ", x.Children.OfType<TextBlock>().Select(y => y.Text)))))
            : string.Empty;

        /// <summary>Whether the panel offers to open Remove Results. Exposed for tests.</summary>
        internal bool Iteration3RemoveResultsOffered => button_It3RemoveResults.Visibility == Visibility.Visible;

        /// <summary>
        /// Opens on a method that can run. Where nobody chose a method and the default cannot run - typically
        /// because the reference case has no product or no saved cooling control rooms - the first method that
        /// can run is selected instead, product methods before validation methods, and
        /// <see cref="RefreshAutomaticNotice"/> says so and why. A method a person chose is never replaced.
        /// Pre-flights are cached per method, so this costs another walk only where the default is refused.
        /// </summary>
        private void ApplyAutomaticIteration3Method(bool canRun)
        {
            iteration3Automatic = false;

            if (!canRun || iteration3Mode_Stated)
            {
                return;
            }

            PartOIteration3BehaviourMode partOIteration3BehaviourMode = Iteration3DefaultMode;

            PartOIteration3Preflight preflight_Default = Preflight(Iteration3DefaultMode);

            //Only where the default is missing a STEP (no saved cooling control rooms, no product yet). A fault - an
            //invalid saved room, a product the catalogue does not hold - leaves the default selected and refused, so
            //Run stays off for the method that is wrong rather than quietly running another.
            if (preflight_Default is not null && !preflight_Default.CanRun && IsMissingStep(preflight_Default))
            {
                foreach (bool validation in new[] { false, true })
                {
                    PartOIteration3BehaviourMode? partOIteration3BehaviourMode_Runnable = Query.PartOIteration3BehaviourModes.Where(x => x != Iteration3DefaultMode && Query.IsPartOIteration3ValidationMethod(x) == validation).Cast<PartOIteration3BehaviourMode?>().FirstOrDefault(x => Preflight(x.Value)?.CanRun ?? false);

                    if (partOIteration3BehaviourMode_Runnable.HasValue)
                    {
                        partOIteration3BehaviourMode = partOIteration3BehaviourMode_Runnable.Value;
                        break;
                    }
                }
            }

            iteration3Automatic = partOIteration3BehaviourMode != Iteration3DefaultMode;

            if (partOIteration3BehaviourMode != iteration3Mode)
            {
                iteration3Mode = partOIteration3BehaviourMode;

                UpdateMethodSelection();
            }
        }

        /// <summary>
        /// Whether a method's refusal is a missing step rather than a fault: no cooling control rooms saved, or no
        /// unit has a product yet. Both are put right by doing the next step of the journey, not by correcting data.
        /// </summary>
        private static bool IsMissingStep(PartOIteration3Preflight partOIteration3Preflight)
        {
            if (partOIteration3Preflight.Refusals.Contains(Query.PartOIteration3NoDwellingStrategiesRefusal))
            {
                return true;
            }

            List<PartOIteration3PreflightUnit> units_Refused = partOIteration3Preflight.Units.FindAll(x => !x.IsReady);

            return units_Refused.Count != 0 && units_Refused.Count == partOIteration3Preflight.Units.Count && units_Refused.TrueForAll(x => x.Product == "No product selected")
                && partOIteration3Preflight.Refusals.TrueForAll(x => units_Refused.Exists(y => y.Refusal == x));
        }

        /// <summary>
        /// The notice over the method's explanation when the Hub selected it: the default method, why it cannot run,
        /// and that the choice is the person's to change. Also keeps the Mixed Design step on offer while the default
        /// is what is missing it.
        /// </summary>
        private void RefreshAutomaticNotice(bool canRun)
        {
            if (!iteration3Automatic || !canRun)
            {
                border_It3Automatic.Visibility = Visibility.Collapsed;
                textBlock_It3Automatic.Text = string.Empty;

                return;
            }

            PartOIteration3Preflight preflight_Default = Preflight(Iteration3DefaultMode);
            string why = IsMissingRooms(preflight_Default)
                ? "What the first method needs is listed below."
                : Refusal(preflight_Default).Replace("This method cannot run yet: ", string.Empty);

            textBlock_It3Automatic.Text = string.Format(
                CultureInfo.CurrentCulture,
                "'{0}' cannot run yet, so '{1}' is selected because it can run now. Choose another method above at any time. {2}",
                Query.PartOIteration3MethodLabel(Iteration3DefaultMode),
                Query.PartOIteration3MethodLabel(iteration3Mode),
                why).TrimEnd();

            border_It3Automatic.Visibility = Visibility.Visible;
        }

        private void button_It3Review_Click(object sender, RoutedEventArgs e)
        {
            Action = PartOWorkflowAction.Iteration3Review;

            DialogResult = true;
        }

        private void button_It3MixedDesign_Click(object sender, RoutedEventArgs e)
        {
            Action = PartOWorkflowAction.MixedDesign;

            DialogResult = true;
        }

        private void button_It3RemoveResults_Click(object sender, RoutedEventArgs e)
        {
            Action = PartOWorkflowAction.RemoveResults;

            DialogResult = true;
        }

        private void button_GoToIteration3_Click(object sender, RoutedEventArgs e)
        {
            border_Iteration3.BringIntoView();
        }

        //---------------------------------------------------------------------------------------------
        //Simulation case
        //---------------------------------------------------------------------------------------------

        /// <summary>
        /// The Simulation case as the controls state it. Setting it writes the controls; the caller builds the
        /// first one with <see cref="PartOSimulationCase.Create"/> - the values the Simulate dialog opened with.
        /// </summary>
        public PartOSimulationCase SimulationCase
        {
            get
            {
                SolarCalculationMethod solarCalculationMethod = comboBox_SolarCalculationMethod.SelectedItem is string text ? Core.Query.Enum<SolarCalculationMethod>(text) : SolarCalculationMethod.Undefined;

                return new PartOSimulationCase
                {
                    WeatherData = selectSAMObjectComboBoxControl_Weather.GetJSAMObject<WeatherData>(),
                    OutputDirectory = textBox_OutputDirectory.Text?.Trim(),
                    SolarCalculationMethod = solarCalculationMethod,

                    //Direct T3D converts the geometry into a T3D, which only the TAS solar calculation does.
                    DirectT3D = checkBox_DirectT3D.IsChecked == true && solarCalculationMethod == SolarCalculationMethod.TAS,
                };
            }

            set
            {
                simulationCase_Stated = value is not null;

                PartOSimulationCase partOSimulationCase = value ?? new PartOSimulationCase();

                //Written control by control, and each write raises its own change - so without this the
                //first of them was judged over a half-written case (weather, no folder yet), found wanting,
                //and opened the section for good. The case is judged once, when it is complete.
                writing_SimulationCase = true;

                try
                {
                    if (partOSimulationCase.WeatherData is not null)
                    {
                        string text = string.IsNullOrWhiteSpace(partOSimulationCase.WeatherData.Name) ? SimulateControl.InternalText : partOSimulationCase.WeatherData.Name;

                        selectSAMObjectComboBoxControl_Weather.Add(text, new WeatherData(partOSimulationCase.WeatherData));
                        selectSAMObjectComboBoxControl_Weather.SelectedText = text;
                    }

                    textBox_OutputDirectory.Text = partOSimulationCase.OutputDirectory ?? string.Empty;
                    comboBox_SolarCalculationMethod.SelectedItem = Core.Query.Description(partOSimulationCase.SolarCalculationMethod == SolarCalculationMethod.Undefined ? SolarCalculationMethod.TAS : partOSimulationCase.SolarCalculationMethod);
                    checkBox_DirectT3D.IsChecked = partOSimulationCase.DirectT3D;
                }
                finally
                {
                    writing_SimulationCase = false;
                }

                RefreshSimulationCase();
            }
        }

        /// <summary>
        /// Whether a caller has stated a Simulation case. The production command always does; a window no case
        /// was given to (a test host) offers Run on the other stages' say alone, as it did before the case
        /// moved here.
        /// </summary>
        private bool simulationCase_Stated;

        /// <summary>Whether the setter is writing the case's controls; see <see cref="SimulationCase"/>.</summary>
        private bool writing_SimulationCase;

        /// <summary>Why the stated Simulation case cannot run, or null - including where none was stated.</summary>
        private string? SimulationCaseRefusal => simulationCase_Stated ? SimulationCase.Refusal() : null;

        /// <summary>The Simulation case's one-line summary, as the collapsed header shows it. Exposed for tests.</summary>
        internal string SimulationCaseSummary => run_SimulationCaseSummary.Text;

        private void RefreshSimulationCase()
        {
            if (!loaded || writing_SimulationCase)
            {
                return;
            }

            PartOSimulationCase partOSimulationCase = SimulationCase;

            //Offered only where it has meaning: the TAS solar calculation is the one that converts through a T3D.
            checkBox_DirectT3D.IsEnabled = partOSimulationCase.SolarCalculationMethod == SolarCalculationMethod.TAS;

            string refusal = partOSimulationCase.Refusal();

            if (!simulationCase_Stated)
            {
                run_SimulationCaseSummary.Text = string.Empty;

                return;
            }

            //The header names the weather and the solar method; the output folder - a long path that
            //dominated the line - is on the tooltip with the complete case, and in the field when opened.
            string weather = string.IsNullOrWhiteSpace(partOSimulationCase.WeatherData?.Name) ? "model weather" : partOSimulationCase.WeatherData!.Name;

            run_SimulationCaseSummary.Text = refusal is not null
                ? string.Format(" — {0}", refusal)
                : string.Format(" — {0} · {1} solar{2}", weather, Core.Query.Description(partOSimulationCase.SolarCalculationMethod), partOSimulationCase.DirectT3D ? " · Direct T3D" : string.Empty);

            textBlock_SimulationCaseHeader.ToolTip = refusal ?? string.Format(
                "Weather: {0}\nSolar calculation: {1}\nOutput folder: {2}\nFull year, days 1 to 365.",
                weather,
                Core.Query.Description(partOSimulationCase.SolarCalculationMethod),
                partOSimulationCase.OutputDirectory);

            run_SimulationCaseSummary.Foreground = refusal is not null ? Brushes.Firebrick : new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));

            //A case that cannot run is not left collapsed out of sight.
            if (refusal is not null)
            {
                expander_SimulationCase.IsExpanded = true;
            }

            //Whether Run is offered depends on it, so the actions are re-derived over the inspection already
            //built - never a re-inspection of the model.
            RefreshWorkflowInput();
        }

        //---------------------------------------------------------------------------------------------
        //Last outcome
        //---------------------------------------------------------------------------------------------

        /// <summary>
        /// What the last action in this Hub did - a session-only record, carried by the caller to the next
        /// showing and no further. Null where nothing was done in this Hub session.
        /// <para>
        /// What is SHOWN is <see cref="DisplayedOutcome"/>: this record while the run still says what it
        /// claims, otherwise the run's own standing state (<see cref="Modify.HubOutcome"/>).
        /// </para>
        /// </summary>
        public PartOWorkflowOutcome? LastOutcome
        {
            get => lastOutcome;
            set
            {
                lastOutcome = value;

                RenderLastOutcome();
            }
        }

        private PartOWorkflowOutcome? lastOutcome;

        /// <summary>The line as shown: the session record where it still holds, else the run's own state. Null hides it.</summary>
        internal PartOWorkflowOutcome? DisplayedOutcome { get; private set; }

        /// <summary>
        /// Re-derived from the record, the run and the capabilities already taken - no filesystem, no
        /// re-inspection - so it can follow every refresh.
        /// </summary>
        private void RenderLastOutcome()
        {
            PartOWorkflowOutcome? value = Modify.HubOutcome(lastOutcome, partORun, partOWorkflowCapabilities);

            DisplayedOutcome = value;

            if (value is null || string.IsNullOrWhiteSpace(value.Headline))
            {
                border_LastOutcome.Visibility = Visibility.Collapsed;

                return;
            }

            border_LastOutcome.Visibility = Visibility.Visible;

            textBlock_LastOutcomeGlyph.Text = value.Glyph ?? string.Empty;
            textBlock_LastOutcomeGlyph.Visibility = value.Glyph is null ? Visibility.Collapsed : Visibility.Visible;
            textBlock_LastOutcome.Text = value.Headline;
            textBlock_LastOutcomeDetail.Text = value.Detail ?? string.Empty;
            textBlock_LastOutcomeDetail.Visibility = value.Detail is null ? Visibility.Collapsed : Visibility.Visible;

            //The complete explanation: always on the tooltip, and under the line with the Hub's one Show
            //details switch - the same switch the readiness rows answer to.
            textBlock_LastOutcomeFull.Text = value.ToolTip ?? string.Empty;
            textBlock_LastOutcomeFull.Visibility = value.ToolTip is not null && ShowStatusDetails ? Visibility.Visible : Visibility.Collapsed;
            border_LastOutcome.ToolTip = value.ToolTip is null ? null : value.ToolTip;

            (Color background, Color border) = value.Kind switch
            {
                PartOWorkflowOutcomeKind.Success => (Color.FromRgb(0xE8, 0xF5, 0xE9), Color.FromRgb(0x81, 0xC7, 0x84)),
                PartOWorkflowOutcomeKind.Warning => (Color.FromRgb(0xFF, 0xF8, 0xE1), Color.FromRgb(0xFF, 0xD5, 0x4F)),
                PartOWorkflowOutcomeKind.Fail => (Color.FromRgb(0xFF, 0xEB, 0xE9), Color.FromRgb(0xFF, 0x81, 0x82)),
                _ => (Color.FromRgb(0xF5, 0xF5, 0xF5), Color.FromRgb(0xCC, 0xCC, 0xCC)),
            };

            border_LastOutcome.Background = new SolidColorBrush(background);
            border_LastOutcome.BorderBrush = new SolidColorBrush(border);
        }

        /// <summary>The line as shown, glyph, headline and caption, or empty. Exposed for tests.</summary>
        internal string LastOutcomeText => border_LastOutcome.Visibility == Visibility.Visible ? DisplayedOutcome?.Text ?? string.Empty : string.Empty;

        /// <summary>Whether the complete explanation is shown under the line. Exposed for tests.</summary>
        internal bool LastOutcomeDetailsShown => border_LastOutcome.Visibility == Visibility.Visible && textBlock_LastOutcomeFull.Visibility == Visibility.Visible;
    }
}
