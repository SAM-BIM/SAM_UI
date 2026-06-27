// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.Windows.Controls;
using SAM.Geometry.Mollier;
using SAM.Core.Mollier.UI.Forms;
using SystemColor = System.Drawing.Color;

namespace SAM.Core.Mollier.UI.Controls
{
    /// <summary>WPF port of the WinForms HeatingProcessControl.</summary>
    public partial class HeatingProcessControl : UserControl, IMollierProcessControl
    {
        private MollierForm mollierForm;

        public event SelectMollierPointEventHandler SelectMollierPoint;

        public HeatingProcessControl()
        {
            InitializeComponent();

            processCalculateType_ComboBox.Items.Add("Dry Bulb Temperature");
            processCalculateType_ComboBox.Items.Add("Enthalpy Difference");
            processCalculateType_ComboBox.Items.Add("Dry Bulb Temperature Difference");
            processCalculateType_ComboBox.SelectedIndex = 0;

            MollierPointControl_Start.SelectMollierPoint += MollierPointControl_Start_SelectMollierPoint;
        }

        private string CalculationTypeText => processCalculateType_ComboBox.SelectedItem as string;

        private void MollierPointControl_Start_SelectMollierPoint(object sender, SelectMollierPointEventArgs e)
        {
            SelectMollierPoint?.Invoke(this, e);

            if (MollierForm != null)
            {
                MollierForm.MollierPointSelected += MollierForm_MollierPointSelected;
            }
        }

        private void MollierForm_MollierPointSelected(object sender, MollierPointSelectedEventArgs e)
        {
            if (MollierForm != null)
            {
                MollierForm.MollierPointSelected -= MollierForm_MollierPointSelected;
            }

            Start = e.MollierPoint;
        }

        public UIMollierProcess GetUIMollierProcess()
        {
            ProcessCalculationType processCalculationType = Core.Query.Enum<ProcessCalculationType>(CalculationTypeText);
            MollierProcess mollierProcess = null;
            MollierPoint start = Start;

            switch (processCalculationType)
            {
                case ProcessCalculationType.DryBulbTemperature:
                    double dryBulbTemperature = Query.ParameterValue<double>(flowLayoutPanel_Main, ProcessParameterType.DryBulbTemperature);
                    mollierProcess = Mollier.Create.HeatingProcess(start, dryBulbTemperature);
                    break;
                case ProcessCalculationType.SpecificEnthalpyDifference:
                    double enthalpyDifference = Query.ParameterValue<double>(flowLayoutPanel_Main, ProcessParameterType.SpecificEnthalpyDifference);
                    mollierProcess = Mollier.Create.HeatingProcess_ByEnthalpyDifference(start, enthalpyDifference * 1000);
                    break;
                case ProcessCalculationType.DryBulbTemperatureDifference:
                    double dryBulbTemperatureDifference = Query.ParameterValue<double>(flowLayoutPanel_Main, ProcessParameterType.DryBulbTemperatureDifference);
                    mollierProcess = Mollier.Create.HeatingProcess_ByTemperatureDifference(start, dryBulbTemperatureDifference);
                    break;
            }

            return new UIMollierProcess(mollierProcess, SystemColor.Empty);
        }

        public MollierPoint Start
        {
            get { return MollierPointControl_Start.MollierPoint; }
            set
            {
                MollierPointControl_Start.MollierPoint = value;
                MollierPointControl_Start.SelectMollierPointVisible = value != null;
            }
        }

        public MollierForm MollierForm
        {
            get { return mollierForm; }
            set
            {
                mollierForm = value;
                MollierPointControl_Start.SelectMollierPointVisible = value != null;
                MollierPointControl_Start.PressureEnabled = value == null;
            }
        }

        private void processCalculateType_ComboBox_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
        {
            ProcessCalculationType processCalculationType = Core.Query.Enum<ProcessCalculationType>(CalculationTypeText);

            List<ProcessParameterType> processParameterTypes = Query.ProcessParameterTypes(processCalculationType);
            if (processParameterTypes == null || processParameterTypes.Count == 0)
            {
                System.Windows.MessageBox.Show("Wrong Heating Data");
                return;
            }

            flowLayoutPanel_Main.Children.Clear();
            List<ParameterControl> controls = Create.Controls(processParameterTypes);
            foreach (ParameterControl control in controls)
            {
                flowLayoutPanel_Main.Children.Add(control);
            }
        }
    }
}
