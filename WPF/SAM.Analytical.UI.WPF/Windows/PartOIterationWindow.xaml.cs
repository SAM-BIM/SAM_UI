// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Windows;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// What to prepare: which Approved Document O base provision, over which dwellings, and whether a
    /// manufacturer ventilation unit is to be selected.
    /// <para>
    /// <b>The ventilation route is picked, never typed.</b> The base provision combo offers the two options
    /// <see cref="PartOVentilationStrategyOption.Options"/> builds, each carrying the canonical word for its
    /// route, and there is no text field anywhere on this window that reaches
    /// <c>Modify.PreparePartOIteration</c>. That is what keeps the long-form synonym - which prepares
    /// successfully and is then refused by every space at assessment - out of reach from the UI.
    /// </para>
    /// <para>
    /// <b>The dwelling scope is SAM's, not this window's.</b> <c>Query.PartFDwellingZones</c> is the single
    /// source of that policy, including the legacy case where nothing carries
    /// <c>ZoneParameter.IsDwelling</c> at all; this window shows what it returns and names what it left out.
    /// A zone outside the scope is reported as outside it - it is not quietly given a strategy, and no
    /// common-space criterion is chosen for it here.
    /// </para>
    /// </summary>
    public partial class PartOIterationWindow : System.Windows.Window
    {
        private List<Zone> zones_Dwelling = [];

        private VentilationUnitCatalogue ventilationUnitCatalogue;

        public PartOIterationWindow()
        {
            InitializeComponent();

            comboBox_BaseProvision.ItemsSource = PartOVentilationStrategyOption.Options;
            comboBox_BaseProvision.SelectedIndex = 0;
            comboBox_BaseProvision.SelectionChanged += (s, e) => UpdateVentilationStrategyText();

            checkBox_SelectVentilationUnit.Checked += (s, e) => UpdateOptimiseAvailability();
            checkBox_SelectVentilationUnit.Unchecked += (s, e) => UpdateOptimiseAvailability();
            checkBox_Optimise.Checked += (s, e) => UpdateOptimiseText();
            checkBox_Optimise.Unchecked += (s, e) => UpdateOptimiseText();

            textBox_AirFlowStep.Text = PartOOptimisationSettings.DefaultAirFlowStep_Lps.ToString();
            textBox_MaximumIterations.Text = PartOOptimisationSettings.DefaultMaximumIterations.ToString();

            UpdateVentilationStrategyText();
            UpdateCatalogueText();
            UpdateOptimiseAvailability();
        }

        /// <summary>
        /// The model's zones. Setting them classifies the dwellings through SAM and fills the scope report.
        /// </summary>
        public List<Zone> Zones
        {
            set
            {
                //The policy call, not a local IsDwelling filter.
                zones_Dwelling = Analytical.Query.PartFDwellingZones(value) ?? [];

                listBox_Zones.ItemsSource = zones_Dwelling.ConvertAll(x => x?.Name);

                //What is out of scope is derived from what the policy RETURNED, by difference - never from a
                //second reading of IsDwelling. Where nothing in the model states the parameter,
                //PartFDwellingZones treats every zone as a dwelling and the unmarked zones are then IN scope;
                //an independent rule here for deciding that would be a second policy, and it got this wrong.
                HashSet<Guid> guids_Dwelling = [];
                foreach (Zone zone in zones_Dwelling)
                {
                    if (zone is not null)
                    {
                        guids_Dwelling.Add(zone.Guid);
                    }
                }

                //The classification supplies only the REASON a zone is out of scope, not the fact.
                Analytical.Query.PartFClassifyDwellingZones(value, out List<Zone> _, out List<Zone> zones_NotDwelling, out List<Zone> zones_Unmarked);

                HashSet<Guid> guids_Unmarked = [];
                foreach (Zone zone in zones_Unmarked ?? [])
                {
                    if (zone is not null)
                    {
                        guids_Unmarked.Add(zone.Guid);
                    }
                }

                List<string> descriptions = [];

                foreach (Zone zone in value ?? [])
                {
                    if (zone is null || guids_Dwelling.Contains(zone.Guid))
                    {
                        continue;
                    }

                    descriptions.Add(guids_Unmarked.Contains(zone.Guid)
                        ? string.Format("'{0}' (not marked either way, beside zones that are)", zone.Name)
                        : string.Format("'{0}' (marked not a dwelling)", zone.Name));
                }

                textBlock_Scope.Text = descriptions.Count == 0
                    ? string.Format("{0} dwelling zone(s) in scope.", zones_Dwelling.Count)
                    : string.Format("{0} dwelling zone(s) in scope. Outside current Part O dwelling preparation scope: {1}. Common spaces are not covered by Iteration 1a or 1b and are assessed separately.", zones_Dwelling.Count, string.Join(", ", descriptions));

                button_OK.IsEnabled = zones_Dwelling.Count != 0;
            }
        }

        /// <summary>
        /// The catalogue this session read. Setting it decides whether equipment selection can be offered at
        /// all, and says which of the three states the read landed in.
        /// </summary>
        public VentilationUnitCatalogue VentilationUnitCatalogue
        {
            get
            {
                return ventilationUnitCatalogue;
            }
            set
            {
                ventilationUnitCatalogue = value;

                //Offered only where there is something to select from. Ticked by default in that case,
                //because Iteration 2 is the current stage - but never forced, since Iteration 1a (no
                //selection) stays a legitimate run.
                bool hasProducts = value?.HasSelectableProducts ?? false;

                checkBox_SelectVentilationUnit.IsEnabled = hasProducts;
                checkBox_SelectVentilationUnit.IsChecked = hasProducts;

                UpdateCatalogueText();
                UpdateOptimiseAvailability();
            }
        }

        /// <summary>The dwelling zones the iteration will be prepared over. SAM's selection, unmodified.</summary>
        public List<Zone> Zones_Dwelling => zones_Dwelling;

        /// <summary>
        /// What the window says about the scope: how many dwellings are in it, and which zones are outside it
        /// and why. Exposed so what the user is told is assertable, rather than only visible.
        /// </summary>
        public string ScopeDescription => textBlock_Scope.Text;

        /// <summary>The chosen base provision - its iteration and its canonical ventilation strategy.</summary>
        public PartOVentilationStrategyOption SelectedOption => comboBox_BaseProvision.SelectedItem as PartOVentilationStrategyOption;

        /// <summary>
        /// Whether the catalogue is to be offered to the preparation's selection rule. False leaves
        /// <c>AirHandlingUnitParameter.VentilationUnitReference</c> untouched, which is Iteration 1a.
        /// </summary>
        public bool SelectVentilationUnit => (checkBox_SelectVentilationUnit.IsChecked ?? false) && (ventilationUnitCatalogue?.HasSelectableProducts ?? false);

        /// <summary>
        /// The Iteration 2B optimisation this run is set up to allow afterwards, or <b>null</b> where none
        /// was asked for or the run is not an Iteration 2B starting point.
        /// <para>
        /// <b>Null unless a ventilation unit is being selected.</b> 2B optimises a design within a selected
        /// product's capacity; without one there is no ceiling to explore inside, and the run is Iteration
        /// 1a. Offering it anyway would let somebody start an optimisation that
        /// <c>Modify.CanOptimise</c> is going to refuse minutes later.
        /// </para>
        /// <para>
        /// Null too where the step or the iteration limit typed in is not usable - the settings say why
        /// through <see cref="OptimisationRefusal"/>, and the window will not close on them.
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
                };

                return result.IsValid(out string? _) ? result : null;
            }
        }

        /// <summary>Whether the automatic Iteration 2B optimisation was asked for and is available.</summary>
        public bool Optimise => (checkBox_Optimise.IsChecked ?? false) && checkBox_Optimise.IsEnabled;

        /// <summary>
        /// Why the optimisation settings as typed cannot be used, or null where they can - or where no
        /// optimisation was asked for at all.
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
        /// 2B needs a selected product to optimise within, so it follows the selection tick rather than
        /// standing on its own - and is cleared, not merely greyed, when that tick comes off.
        /// </summary>
        private void UpdateOptimiseAvailability()
        {
            bool available = SelectVentilationUnit;

            checkBox_Optimise.IsEnabled = available;

            if (!available)
            {
                checkBox_Optimise.IsChecked = false;
            }

            textBox_AirFlowStep.IsEnabled = available;
            textBox_MaximumIterations.IsEnabled = available;

            UpdateOptimiseText();
        }

        private void UpdateOptimiseText()
        {
            if (!checkBox_Optimise.IsEnabled)
            {
                textBlock_Optimise.Text = "Iteration 2B raises the design airflow of failing mechanically ventilated rooms within the capacity of the unit already selected for their dwelling, so it needs a ventilation unit to be selected above.";

                return;
            }

            textBlock_Optimise.Text = Optimise
                ? "After the full-year simulation and the TM59 assessment, each eligible failing room's DESIGN airflow is raised by the step, the dwelling is rebalanced, the Part O state is rebuilt and the same weather case is re-run - until every eligible space passes, or the selected unit cannot carry another full step. The selected product is never changed and no Approved Document F requirement is altered. A design that passes at this step is the first tested passing design, not a minimum."
                : "Iteration 2B is not a base provision - it is an optimisation performed on this Iteration 2 design, and it can be run once this iteration has been simulated and assessed.";
        }

        private void UpdateVentilationStrategyText()
        {
            PartOVentilationStrategyOption option = SelectedOption;

            textBlock_VentilationStrategy.Text = option is null
                ? string.Empty
                : string.Format("Ventilation route stated for every dwelling in scope: {0}. This is the canonical value the assessment reads; it cannot be edited here.", option.VentilationStrategy);
        }

        private void UpdateCatalogueText()
        {
            textBlock_Catalogue.Text = ventilationUnitCatalogue?.Description ?? "The ventilation unit catalogue has not been read.";
        }

        private void button_OK_Click(object sender, RoutedEventArgs e)
        {
            //Refused here rather than silently ignored: an unreadable step would otherwise prepare the
            //iteration, run a full-year simulation, and only then reveal that the optimisation nobody
            //could start was never going to.
            string? refusal = OptimisationRefusal;
            if (refusal is not null)
            {
                System.Windows.MessageBox.Show(string.Format("The automatic optimisation settings cannot be used.\n\n{0}", refusal));

                return;
            }

            DialogResult = true;
        }

        private void button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
