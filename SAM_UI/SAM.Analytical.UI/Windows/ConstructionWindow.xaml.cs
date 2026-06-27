// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Architectural;
using SAM.Core;
using SAM.Core.UI.WPF;
using System.Collections.Generic;
using System.Windows;

namespace SAM.Analytical.UI
{
    /// <summary>
    /// WPF replacement for the WinForms SAM.Analytical.Windows.Forms.ConstructionForm: edits a
    /// single Construction (name, description and material layers). Mirrors the original public
    /// API (the constructors, the Construction and ConstructionLayers getters and the Enabled
    /// setter). F12 opens the JSON inspector.
    /// </summary>
    public partial class ConstructionWindow : System.Windows.Window
    {
        private readonly ConstructionLibrary constructionLibrary;
        private readonly MaterialLibrary materialLibrary;
        private readonly Construction construction;

        public ConstructionWindow()
        {
            InitializeComponent();
        }

        public ConstructionWindow(MaterialLibrary materialLibrary, ConstructionLibrary constructionLibrary = null, Construction construction = null)
        {
            this.materialLibrary = materialLibrary;
            this.constructionLibrary = constructionLibrary;
            this.construction = construction;

            InitializeComponent();

            MaterialLayersControl_Main.MaterialLibrary = materialLibrary;

            if (construction != null)
            {
                TextBox_Name.Text = construction.Name;

                MaterialLayersControl_Main.MaterialLayers = construction.ConstructionLayers?.ConvertAll(x => x as MaterialLayer);

                if (!construction.TryGetValue(ConstructionParameter.Description, out string description))
                {
                    description = null;
                }

                TextBox_Description.Text = description;
            }

            if (constructionLibrary == null)
            {
                Button_CopyFromConstruction.Visibility = Visibility.Collapsed;
            }
        }

        public List<ConstructionLayer> ConstructionLayers
        {
            get
            {
                return MaterialLayersControl_Main.ConstructionLayers();
            }
        }

        public Construction Construction
        {
            get
            {
                Construction result = null;
                if (construction != null)
                {
                    result = new Construction(construction, ConstructionLayers);
                    result = new Construction(result.Guid, result, TextBox_Name.Text);
                }

                if (result == null)
                {
                    result = new Construction(TextBox_Name.Text, ConstructionLayers);
                }

                string description = TextBox_Description.Text;
                if (string.IsNullOrEmpty(description))
                {
                    result.RemoveValue(ConstructionParameter.Description);
                }
                else
                {
                    result.SetValue(ConstructionParameter.Description, description);
                }

                return result;
            }
        }

        public bool Enabled
        {
            set
            {
                MaterialLayersControl_Main.Enabled = value;
            }
        }

        private void Button_OK_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TextBox_Name.Text))
            {
                MessageBox.Show("Provide valid name");
                return;
            }

            List<ConstructionLayer> constructionLayers = ConstructionLayers;
            if (constructionLayers == null || constructionLayers.Count == 0)
            {
                MessageBox.Show("Provide valid construction layers");
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

        private void Button_CopyFromConstruction_Click(object sender, RoutedEventArgs e)
        {
            List<Construction> constructions = constructionLibrary?.GetConstructions();
            if (constructions == null || constructions.Count == 0)
            {
                return;
            }

            ComboBoxWindow<Construction> comboBoxWindow = new ComboBoxWindow<Construction>("Select Construction", constructions, (Construction x) => x?.Name) { Owner = this };
            if (comboBoxWindow.ShowDialog() == true)
            {
                Construction construction = comboBoxWindow.SelectedItem;
                if (construction != null)
                {
                    MaterialLayersControl_Main.MaterialLayers = construction.ConstructionLayers?.ConvertAll(x => x as MaterialLayer);
                }
            }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            Construction.JsonForm(this, e);
        }
    }
}
