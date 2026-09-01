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

            UpdateVentilationStrategyText();
            UpdateCatalogueText();
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
            DialogResult = true;
        }

        private void button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
