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

        private PartOIteration3BehaviourMode iteration3Mode = PartOIteration3BehaviourMode.SelectedProductManufacturerGuidance;

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

        /// <summary>The Iteration 3 method chosen. Carried across showings by the caller.</summary>
        public PartOIteration3BehaviourMode Iteration3Mode
        {
            get => iteration3Mode;
            set
            {
                iteration3Mode = value;

                UpdateMethodSelection();

                RefreshIteration3();
            }
        }

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

                textBlock_It3Refusal.Text = Refusal(partOIteration3Preflight);
            }

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
            button_Iteration3.Content = reviewable ? "Run again" : "Run system case";
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
            return tM59ComplianceStatus == TM59ComplianceStatus.Undefined ? "—" : Core.Query.Description(tM59ComplianceStatus);
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

            return string.Format(
                CultureInfo.CurrentCulture,
                "This method cannot run yet: {0} of {1} ventilation unit(s) cannot run. {2}{3}",
                units_Refused.Count,
                partOIteration3Preflight.Units.Count,
                units_Refused[0].Refusal,
                units_Refused.Count > 1 ? " Each unit's reason is in the unit list." : string.Empty);
        }

        private void button_It3Review_Click(object sender, RoutedEventArgs e)
        {
            Action = PartOWorkflowAction.Iteration3Review;

            DialogResult = true;
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
                return new PartOSimulationCase
                {
                    WeatherData = selectSAMObjectComboBoxControl_Weather.GetJSAMObject<WeatherData>(),
                    OutputDirectory = textBox_OutputDirectory.Text?.Trim(),
                    SolarCalculationMethod = comboBox_SolarCalculationMethod.SelectedItem is string text ? Core.Query.Enum<SolarCalculationMethod>(text) : SolarCalculationMethod.Undefined,
                };
            }

            set
            {
                simulationCase_Stated = value is not null;

                PartOSimulationCase partOSimulationCase = value ?? new PartOSimulationCase();

                if (partOSimulationCase.WeatherData is not null)
                {
                    string text = string.IsNullOrWhiteSpace(partOSimulationCase.WeatherData.Name) ? SimulateControl.InternalText : partOSimulationCase.WeatherData.Name;

                    selectSAMObjectComboBoxControl_Weather.Add(text, new WeatherData(partOSimulationCase.WeatherData));
                    selectSAMObjectComboBoxControl_Weather.SelectedText = text;
                }

                textBox_OutputDirectory.Text = partOSimulationCase.OutputDirectory ?? string.Empty;
                comboBox_SolarCalculationMethod.SelectedItem = Core.Query.Description(partOSimulationCase.SolarCalculationMethod == SolarCalculationMethod.Undefined ? SolarCalculationMethod.TAS : partOSimulationCase.SolarCalculationMethod);

                RefreshSimulationCase();
            }
        }

        /// <summary>
        /// Whether a caller has stated a Simulation case. The production command always does; a window no case
        /// was given to (a test host) offers Run on the other stages' say alone, as it did before the case
        /// moved here.
        /// </summary>
        private bool simulationCase_Stated;

        /// <summary>Why the stated Simulation case cannot run, or null - including where none was stated.</summary>
        private string? SimulationCaseRefusal => simulationCase_Stated ? SimulationCase.Refusal() : null;

        /// <summary>The Simulation case's one-line summary, as the collapsed header shows it. Exposed for tests.</summary>
        internal string SimulationCaseSummary => run_SimulationCaseSummary.Text;

        private void RefreshSimulationCase()
        {
            if (!loaded)
            {
                return;
            }

            PartOSimulationCase partOSimulationCase = SimulationCase;

            string refusal = partOSimulationCase.Refusal();

            if (!simulationCase_Stated)
            {
                run_SimulationCaseSummary.Text = string.Empty;

                return;
            }

            run_SimulationCaseSummary.Text = refusal is not null
                ? string.Format(" — {0}", refusal)
                : string.Format(
                    " — {0} · {1} solar · {2}",
                    string.IsNullOrWhiteSpace(partOSimulationCase.WeatherData.Name) ? "model weather" : partOSimulationCase.WeatherData.Name,
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
        /// What the last action in this Hub did, shown inline at the top of the status list - the place a
        /// successful run's "Time elapsed ... OK" box used to be. Null hides the line.
        /// </summary>
        public PartOWorkflowOutcome? LastOutcome
        {
            get => lastOutcome;
            set
            {
                lastOutcome = value;

                if (value is null || string.IsNullOrWhiteSpace(value.Text))
                {
                    border_LastOutcome.Visibility = Visibility.Collapsed;

                    return;
                }

                border_LastOutcome.Visibility = Visibility.Visible;
                textBlock_LastOutcome.Text = value.Text;

                (Color background, Color border) = value.Kind switch
                {
                    PartOWorkflowOutcomeKind.Success => (Color.FromRgb(0xE8, 0xF5, 0xE9), Color.FromRgb(0x81, 0xC7, 0x84)),
                    PartOWorkflowOutcomeKind.Warning => (Color.FromRgb(0xFF, 0xF8, 0xE1), Color.FromRgb(0xFF, 0xD5, 0x4F)),
                    _ => (Color.FromRgb(0xF5, 0xF5, 0xF5), Color.FromRgb(0xCC, 0xCC, 0xCC)),
                };

                border_LastOutcome.Background = new SolidColorBrush(background);
                border_LastOutcome.BorderBrush = new SolidColorBrush(border);
            }
        }

        private PartOWorkflowOutcome? lastOutcome;

        /// <summary>The last-outcome line as shown, or empty. Exposed for tests.</summary>
        internal string LastOutcomeText => border_LastOutcome.Visibility == Visibility.Visible ? textBlock_LastOutcome.Text : string.Empty;
    }

    public enum PartOWorkflowOutcomeKind
    {
        Information,
        Success,
        Warning,
    }

    /// <summary>One line about what the last Hub action did - "✓ TAS simulation complete · 7m 48s".</summary>
    public class PartOWorkflowOutcome
    {
        public PartOWorkflowOutcome(PartOWorkflowOutcomeKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }

        public PartOWorkflowOutcomeKind Kind { get; }

        public string Text { get; }

        public override string ToString()
        {
            return Text;
        }
    }
}
