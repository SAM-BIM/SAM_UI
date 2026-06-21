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
    /// WPF replacement for the WinForms SAM.Analytical.Windows.Forms.ApertureForm: edits a single
    /// Aperture (read-only identity/geometry and custom parameters) and lets the user pick an
    /// ApertureConstruction from the (WPF) <see cref="ApertureConstructionLibraryWindow"/>. Mirrors
    /// the original public API (the constructors and the read-only Aperture /
    /// ApertureConstructionLibrary getters). F12 opens the JSON inspector.
    ///
    /// NOTE: the type SAM.Analytical.UI.WPF.ApertureWindow is a DIFFERENT dialog (airflow / opening
    /// properties). This one lives in SAM.Analytical.UI and is the construction/parameter editor.
    /// </summary>
    public partial class ApertureWindow : System.Windows.Window
    {
        private ApertureConstructionLibrary apertureConstructionLibrary;
        private readonly MaterialLibrary materialLibrary;

        private Aperture aperture;
        private readonly HashSet<Enum> enums;

        public ApertureWindow()
        {
            InitializeComponent();
        }

        public ApertureWindow(Aperture aperture, MaterialLibrary materialLibrary, ApertureConstructionLibrary apertureConstructionLibrary = null, IEnumerable<Enum> enums = null)
        {
            InitializeComponent();

            this.apertureConstructionLibrary = apertureConstructionLibrary == null ? null : new ApertureConstructionLibrary(apertureConstructionLibrary);
            this.materialLibrary = materialLibrary == null ? null : new MaterialLibrary(materialLibrary);

            this.aperture = aperture;
            if (enums != null)
            {
                this.enums = new HashSet<Enum>(enums);
            }

            Load();
        }

        private void Load()
        {
            foreach (ApertureType apertureType in Enum.GetValues(typeof(ApertureType)))
            {
                ComboBox_ApertureType.Items.Add(Core.Query.Description(apertureType));
            }

            if (aperture != null)
            {
                TextBox_Name.Text = aperture.Name;
                TextBox_Guid.Text = aperture.Guid.ToString();
                TextBox_Construction.Text = aperture.ApertureConstruction?.Name;

                ComboBox_ApertureType.Text = Core.Query.Description(aperture.ApertureType());

                ParametersControl_Main.CustomParameters = SAM.Core.UI.Create.CustomParameters(aperture, enums?.ToArray());

                TextBox_Area.Text = Math.Round(aperture.GetArea(), 1).ToString();
                TextBox_Azimuth.Text = Math.Round(aperture.Azimuth(), 2).ToString();
            }

            if (apertureConstructionLibrary == null)
            {
                Button_SelectConstruction.Visibility = Visibility.Collapsed;
            }
        }

        public Aperture Aperture
        {
            get
            {
                if (aperture == null)
                {
                    return null;
                }

                Aperture result = new Aperture(aperture);

                CustomParameters customParameters = ParametersControl_Main.CustomParameters;

                SAM.Core.UI.Modify.SetValues(result, customParameters);
                return result;
            }
        }

        public ApertureConstructionLibrary ApertureConstructionLibrary
        {
            get
            {
                return apertureConstructionLibrary;
            }
        }

        private void Button_SelectConstruction_Click(object sender, RoutedEventArgs e)
        {
            ApertureConstruction apertureConstruction = null;
            ApertureConstructionLibraryWindow apertureConstructionLibraryWindow = new ApertureConstructionLibraryWindow(materialLibrary, apertureConstructionLibrary)
            {
                Owner = this,
                Title = "Aperture Constructions",
                MultiSelect = false
            };

            if (apertureConstructionLibraryWindow.ShowDialog() != true)
            {
                return;
            }

            apertureConstruction = apertureConstructionLibraryWindow.GetApertureConstructions(true)?.FirstOrDefault();
            apertureConstructionLibrary = apertureConstructionLibraryWindow.ApertureConstructionLibrary;

            if (apertureConstruction == null)
            {
                return;
            }

            aperture = new Aperture(aperture, apertureConstruction);

            TextBox_Name.Text = aperture.Name;
            TextBox_Construction.Text = apertureConstruction.Name;
            ComboBox_ApertureType.Text = Core.Query.Description(apertureConstruction.ApertureType);
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
            Aperture.JsonForm(this, e);
        }
    }
}
