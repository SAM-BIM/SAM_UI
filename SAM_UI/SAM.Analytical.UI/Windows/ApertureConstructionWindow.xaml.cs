// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Architectural;
using SAM.Core;
using SAM.Core.UI.WPF;
using System;
using System.Collections.Generic;
using System.Windows;

namespace SAM.Analytical.UI
{
    /// <summary>
    /// WPF replacement for the WinForms SAM.Analytical.Windows.Forms.ApertureConstructionForm: edits a
    /// single ApertureConstruction (name, description, aperture type and the pane / frame material
    /// layers). Mirrors the original public API (constructors, the ApertureConstruction,
    /// PaneConstructionLayers and FrameConstructionLayers getters and the Enabled setter). F12 opens
    /// the JSON inspector.
    /// </summary>
    public partial class ApertureConstructionWindow : System.Windows.Window
    {
        private readonly MaterialLibrary materialLibrary;
        private readonly ApertureConstructionLibrary apertureConstructionLibrary;
        private readonly ApertureConstruction apertureConstruction;

        public ApertureConstructionWindow()
        {
            InitializeComponent();
        }

        public ApertureConstructionWindow(MaterialLibrary materialLibrary, ApertureConstructionLibrary apertureConstructionLibrary = null, ApertureConstruction apertureConstruction = null)
        {
            this.materialLibrary = materialLibrary;
            this.apertureConstructionLibrary = apertureConstructionLibrary;
            this.apertureConstruction = apertureConstruction;

            InitializeComponent();

            MaterialLayersControl_Pane.MaterialLibrary = materialLibrary;
            MaterialLayersControl_Frame.MaterialLibrary = materialLibrary;

            foreach (ApertureType apertureType in Enum.GetValues(typeof(ApertureType)))
            {
                if (apertureType == ApertureType.Undefined)
                {
                    continue;
                }

                ComboBox_ApertureType.Items.Add(Core.Query.Description(apertureType));
            }

            if (ComboBox_ApertureType.Items.Count > 0)
            {
                ComboBox_ApertureType.SelectedIndex = 0;
            }

            if (apertureConstruction != null)
            {
                TextBox_Name.Text = apertureConstruction.Name;

                MaterialLayersControl_Pane.MaterialLayers = apertureConstruction.PaneConstructionLayers?.ConvertAll(x => (MaterialLayer)x);
                MaterialLayersControl_Frame.MaterialLayers = apertureConstruction.FrameConstructionLayers?.ConvertAll(x => (MaterialLayer)x);

                ComboBox_ApertureType.SelectedItem = Core.Query.Description(apertureConstruction.ApertureType);

                if (apertureConstruction.TryGetValue(ApertureConstructionParameter.Description, out string description))
                {
                    TextBox_Description.Text = description;
                }
            }
        }

        public List<ConstructionLayer> PaneConstructionLayers
        {
            get
            {
                return MaterialLayersControl_Pane?.ConstructionLayers();
            }
        }

        public List<ConstructionLayer> FrameConstructionLayers
        {
            get
            {
                return MaterialLayersControl_Frame?.ConstructionLayers();
            }
        }

        public ApertureConstruction ApertureConstruction
        {
            get
            {
                ApertureConstruction result = null;
                if (apertureConstruction != null)
                {
                    result = new ApertureConstruction(apertureConstruction, PaneConstructionLayers, FrameConstructionLayers);
                    result = new ApertureConstruction(result.Guid, result, TextBox_Name.Text);
                }

                if (result == null)
                {
                    result = new ApertureConstruction(Guid.NewGuid(), TextBox_Name.Text, Core.Query.Enum<ApertureType>(ComboBox_ApertureType.SelectedItem as string), PaneConstructionLayers, FrameConstructionLayers);
                }

                string description = TextBox_Description.Text;
                if (string.IsNullOrEmpty(description))
                {
                    result.RemoveValue(ApertureConstructionParameter.Description);
                }
                else
                {
                    result.SetValue(ApertureConstructionParameter.Description, description);
                }

                return result;
            }
        }

        public bool Enabled
        {
            set
            {
                if (materialLibrary != null)
                {
                    MaterialLayersControl_Pane.Enabled = value;
                    MaterialLayersControl_Frame.Enabled = value;
                }

                ComboBox_ApertureType.IsEnabled = value;
                TextBox_Name.IsEnabled = value;
                TextBox_Description.IsEnabled = value;
            }
        }

        private void Button_OK_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextBox_Name.Text))
            {
                MessageBox.Show("Provide valid name");
                return;
            }

            List<ConstructionLayer> constructionLayers = PaneConstructionLayers;
            if (constructionLayers == null || constructionLayers.Count == 0)
            {
                MessageBox.Show("Provide valid pane construction layers");
                return;
            }

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
            ApertureConstruction.JsonForm(this, e);
        }
    }
}
