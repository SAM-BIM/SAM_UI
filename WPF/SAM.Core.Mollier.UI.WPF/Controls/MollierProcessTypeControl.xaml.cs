// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Windows.Controls;
using SAM.Geometry.Mollier;

namespace SAM.Core.Mollier.UI.Controls
{
    /// <summary>
    /// WPF port of the WinForms MollierProcessTypeControl: a process-type chooser combo box.
    /// </summary>
    public partial class MollierProcessTypeControl : UserControl
    {
        private MollierProcessType processType = MollierProcessType.Undefined;

        public MollierProcessTypeControl()
        {
            InitializeComponent();

            MollierProcessType_ComboBox.Items.Add("Heating");
            MollierProcessType_ComboBox.Items.Add("Cooling");
            MollierProcessType_ComboBox.Items.Add("Heat Recovery");
            MollierProcessType_ComboBox.Items.Add("Mixing");
            MollierProcessType_ComboBox.Items.Add("Adiabatic Humidification");
            MollierProcessType_ComboBox.Items.Add("Isothermal Humidification");
        }

        public MollierProcessType ProcessType
        {
            get { return processType; }
        }

        private void MollierProcessType_ComboBox_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (MollierProcessType_ComboBox.SelectedItem as string)
            {
                case "Heating":
                    processType = MollierProcessType.Heating;
                    break;
                case "Cooling":
                    processType = MollierProcessType.Cooling;
                    break;
                case "Heat Recovery":
                    processType = MollierProcessType.HeatRecovery;
                    break;
                case "Mixing":
                    processType = MollierProcessType.Mixing;
                    break;
                case "Adiabatic Humidification":
                    break;
                case "Isothermal Humidification":
                    break;
            }
        }
    }
}
