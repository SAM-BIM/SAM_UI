// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using SAM.Core.UI;
using SAM.Core.UI.WPF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace SAM.Analytical.UI
{
    /// <summary>
    /// WPF replacement for the WinForms SAM.Analytical.Windows.Forms.PanelForm: edits a single
    /// Panel (read-only identity/geometry, editable PanelType and custom parameters) and lets the
    /// user pick a Construction from the (WPF) <see cref="ConstructionLibraryWindow"/>. Mirrors the
    /// original public API (the constructors and the read-only Panel / ConstructionLibrary getters).
    /// F12 opens the JSON inspector.
    /// </summary>
    public partial class PanelWindow : System.Windows.Window
    {
        private ConstructionLibrary constructionLibrary;
        private readonly MaterialLibrary materialLibrary;

        private Panel panel;
        private readonly HashSet<Enum> enums;

        public PanelWindow()
        {
            InitializeComponent();
        }

        public PanelWindow(Panel panel, MaterialLibrary materialLibrary, ConstructionLibrary constructionLibrary = null, IEnumerable<Enum> enums = null)
        {
            InitializeComponent();

            this.constructionLibrary = constructionLibrary == null ? null : new ConstructionLibrary(constructionLibrary);
            this.materialLibrary = materialLibrary == null ? null : new MaterialLibrary(materialLibrary);

            this.panel = panel;
            if (enums != null)
            {
                this.enums = new HashSet<Enum>(enums);
            }

            Load();
        }

        private void Load()
        {
            foreach (PanelType panelType in Enum.GetValues(typeof(PanelType)))
            {
                ComboBox_PanelType.Items.Add(Core.Query.Description(panelType));
            }

            if (panel != null)
            {
                TextBox_Name.Text = panel.Name;
                TextBox_Guid.Text = panel.Guid.ToString();
                TextBox_Construction.Text = panel.Construction?.Name;

                ComboBox_PanelType.Text = Core.Query.Description(panel.PanelType);

                ParametersControl_Main.CustomParameters = SAM.Core.UI.Create.CustomParameters(panel, enums?.ToArray());

                TextBox_Area.Text = Math.Round(panel.GetArea(), 1).ToString();
                TextBox_NetArea.Text = Math.Round(panel.GetAreaNet(), 1).ToString();

                Range<double> elevationRange = panel.GetElevationRange();
                if (elevationRange != null)
                {
                    TextBox_MinElevation.Text = Math.Round(elevationRange.Min, 2).ToString();
                    TextBox_MaxElevation.Text = Math.Round(elevationRange.Max, 2).ToString();
                }

                TextBox_Azimuth.Text = Math.Round(panel.Azimuth(), 2).ToString();
            }

            if (constructionLibrary == null)
            {
                Button_SelectConstruction.Visibility = Visibility.Collapsed;
            }
        }

        public Panel Panel
        {
            get
            {
                if (panel == null)
                {
                    return null;
                }

                PanelType panelType = Core.Query.Enum<PanelType>(ComboBox_PanelType.Text);

                Panel result = Analytical.Create.Panel(panel, panelType);

                CustomParameters customParameters = ParametersControl_Main.CustomParameters;

                SAM.Core.UI.Modify.SetValues(result, customParameters);
                return result;
            }
        }

        public ConstructionLibrary ConstructionLibrary
        {
            get
            {
                return constructionLibrary;
            }
        }

        private void Button_SelectConstruction_Click(object sender, RoutedEventArgs e)
        {
            Construction construction = null;
            ConstructionLibraryWindow constructionLibraryWindow = new ConstructionLibraryWindow(materialLibrary, constructionLibrary)
            {
                Owner = this,
                Title = "Constructions",
                MultiSelect = false
            };

            if (constructionLibraryWindow.ShowDialog() != true)
            {
                return;
            }

            construction = constructionLibraryWindow.GetConstructions(true)?.FirstOrDefault();
            constructionLibrary = constructionLibraryWindow.ConstructionLibrary;

            if (construction == null)
            {
                return;
            }

            panel = Analytical.Create.Panel(panel, construction);

            TextBox_Name.Text = panel.Name;
            TextBox_Construction.Text = construction.Name;
        }

        private void Button_OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            Panel.JsonForm(this, e);
        }
    }
}
