// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// How one saved 2D view presents the Approved Document F airflow annotation.
    /// <para>
    /// Presentation only. Every control here writes to a <see cref="PartFAirflowViewSettings"/>, which holds
    /// no flow rate, no compliance status and no terminal - those are re-read from the model each time the
    /// view is drawn, so a saved drawing can never disagree with its own assessment.
    /// </para>
    /// <para>
    /// The colour scheme is deliberately NOT here. <c>PartF Data</c> is a space appearance setting, edited
    /// where every other colour scheme is edited, and the two are meant to be used together: the fills say
    /// what each room is, the tags say what its air does.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The base class is fully qualified because this namespace sits inside <c>SAM.Analytical</c>, which has a
    /// <c>Window</c> of its own - the architectural element. An unqualified <c>Window</c> here binds to that
    /// one, not to the WPF class.
    /// </remarks>
    public partial class PartFAirflowViewSettingsWindow : System.Windows.Window
    {
        private List<Zone> zones = [];

        public PartFAirflowViewSettingsWindow()
        {
            InitializeComponent();

            foreach (PartFOperatingMode partFOperatingMode in Enum.GetValues(typeof(PartFOperatingMode)))
            {
                ComboBox_Mode.Items.Add(Core.Query.Description(partFOperatingMode));
            }

            ComboBox_Mode.SelectedIndex = 0;
        }

        /// <summary>
        /// The model the view belongs to, read for the zone categories and dwelling zones a person can pick
        /// from. Nothing is calculated here.
        /// </summary>
        public AdjacencyCluster AdjacencyCluster
        {
            set
            {
                ComboBox_ZoneCategory.Items.Clear();

                foreach (string zoneCategory in value?.GetZoneCategories() ?? [])
                {
                    ComboBox_ZoneCategory.Items.Add(zoneCategory);
                }

                zones = [.. (value?.GetZones() ?? []).Where(x => x is not null).OrderBy(x => x.Name, StringComparer.Ordinal)];

                ComboBox_Dwelling.Items.Clear();
                ComboBox_Dwelling.Items.Add("All dwellings on this level");

                foreach (Zone zone in zones)
                {
                    ComboBox_Dwelling.Items.Add(zone.Name);
                }

                ComboBox_Dwelling.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// The settings being edited. Reading builds a new instance from the controls; the annotation
        /// overrides - the labels somebody has dragged - are carried through untouched, because moving a label
        /// is not something this dialog has any business discarding.
        /// </summary>
        public PartFAirflowViewSettings PartFAirflowViewSettings
        {
            get
            {
                PartFAirflowViewSettings result = new(partFAirflowViewSettings)
                {
                    Enabled = CheckBox_Enabled.IsChecked == true,
                    OperatingMode = (PartFOperatingMode)System.Math.Max(0, ComboBox_Mode.SelectedIndex),

                    ZoneCategoryName = string.IsNullOrWhiteSpace(ComboBox_ZoneCategory.Text) ? null : ComboBox_ZoneCategory.Text,

                    ShowSupply = CheckBox_Supply.IsChecked == true,
                    ShowGeneralExtract = CheckBox_GeneralExtract.IsChecked == true,
                    ShowLocalKitchenExtract = CheckBox_LocalKitchenExtract.IsChecked == true,
                    ShowTransfer = CheckBox_Transfer.IsChecked == true,
                    ShowUnresolved = CheckBox_Unresolved.IsChecked == true,
                    ShowValues = CheckBox_Values.IsChecked == true,
                    ShowCompliance = CheckBox_Compliance.IsChecked == true,
                    ShowDoorRequirements = CheckBox_DoorData.IsChecked == true,
                    ShowContextGeometry = CheckBox_Context.IsChecked == true,
                };

                //Index 0 is "all dwellings"; anything after it is one zone, keyed by GUID so renaming a flat
                //does not silently change what this view shows.
                int index = ComboBox_Dwelling.SelectedIndex - 1;

                if (index >= 0 && index < zones.Count)
                {
                    result.DwellingFilter = PartFDwellingFilter.SelectedDwelling;
                    result.DwellingGuid = zones[index].Guid;
                }
                else
                {
                    result.DwellingFilter = PartFDwellingFilter.AllDwellingsOnLevel;
                    result.DwellingGuid = Guid.Empty;
                }

                if (Core.Query.TryConvert(TextBox_AnnotationScale.Text, out double annotationScale) && annotationScale > 0)
                {
                    result.AnnotationScale = annotationScale;
                }

                return result;
            }

            set
            {
                partFAirflowViewSettings = value is null ? new PartFAirflowViewSettings() : new PartFAirflowViewSettings(value);

                CheckBox_Enabled.IsChecked = partFAirflowViewSettings.Enabled;
                ComboBox_Mode.SelectedIndex = (int)partFAirflowViewSettings.OperatingMode;
                ComboBox_ZoneCategory.Text = partFAirflowViewSettings.ZoneCategoryName ?? string.Empty;
                TextBox_AnnotationScale.Text = partFAirflowViewSettings.AnnotationScale.ToString(System.Globalization.CultureInfo.InvariantCulture);

                CheckBox_Supply.IsChecked = partFAirflowViewSettings.ShowSupply;
                CheckBox_GeneralExtract.IsChecked = partFAirflowViewSettings.ShowGeneralExtract;
                CheckBox_LocalKitchenExtract.IsChecked = partFAirflowViewSettings.ShowLocalKitchenExtract;
                CheckBox_Transfer.IsChecked = partFAirflowViewSettings.ShowTransfer;
                CheckBox_Unresolved.IsChecked = partFAirflowViewSettings.ShowUnresolved;
                CheckBox_Values.IsChecked = partFAirflowViewSettings.ShowValues;
                CheckBox_Compliance.IsChecked = partFAirflowViewSettings.ShowCompliance;
                CheckBox_DoorData.IsChecked = partFAirflowViewSettings.ShowDoorRequirements;
                CheckBox_Context.IsChecked = partFAirflowViewSettings.ShowContextGeometry;

                int index = partFAirflowViewSettings.DwellingFilter == PartFDwellingFilter.SelectedDwelling
                    ? zones.FindIndex(x => x.Guid == partFAirflowViewSettings.DwellingGuid)
                    : -1;

                ComboBox_Dwelling.SelectedIndex = index >= 0 ? index + 1 : 0;

                UpdateEnabled();
            }
        }

        private PartFAirflowViewSettings partFAirflowViewSettings = new();

        private void CheckBox_Enabled_Click(object sender, RoutedEventArgs e)
        {
            UpdateEnabled();
        }

        /// <summary>
        /// Everything below the switch is greyed out when the annotation is off - the settings are still there
        /// and are still saved, so turning it back on restores the drawing rather than resetting it.
        /// </summary>
        private void UpdateEnabled()
        {
            bool enabled = CheckBox_Enabled.IsChecked == true;

            GroupBox_Scope.IsEnabled = enabled;
            GroupBox_Show.IsEnabled = enabled;
        }

        private void Button_OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
