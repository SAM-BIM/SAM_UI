// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.Windows.Controls;
using SAM.Geometry.Mollier;
using SAM.Core.Mollier.UI.Forms;
using SystemColor = System.Drawing.Color;

namespace SAM.Core.Mollier.UI.Controls
{
    /// <summary>WPF port of the WinForms IsothermalHumidificationProcessControl.</summary>
    public partial class IsothermalHumidificationProcessControl : UserControl, IMollierProcessControl
    {
        private MollierForm mollierForm;

        public event SelectMollierPointEventHandler SelectMollierPoint;

        public IsothermalHumidificationProcessControl()
        {
            InitializeComponent();

            processCalculateType_ComboBox.Items.Add("Humidity Ratio Difference");
            processCalculateType_ComboBox.Items.Add("Relative Humidity");
            processCalculateType_ComboBox.SelectedIndex = 1;

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
                case ProcessCalculationType.HumidityRatioDifference:
                    double humidityRatioDifference = Query.ParameterValue<double>(flowLayoutPanel_Main, ProcessParameterType.HumidityRatioDifference);
                    mollierProcess = Mollier.Create.IsothermalHumidificationProcess_ByHumidityRatioDifference(start, humidityRatioDifference / 1000);
                    break;
                case ProcessCalculationType.RelativeHumidity:
                    double relativeHumidity = Query.ParameterValue<double>(flowLayoutPanel_Main, ProcessParameterType.RelativeHumidity);
                    mollierProcess = Mollier.Create.IsothermalHumidificationProcess_ByRelativeHumidity(start, relativeHumidity);
                    break;
            }

            return new UIMollierProcess(mollierProcess, SystemColor.Empty);
        }

        public MollierPoint Start
        {
            get { return MollierPointControl_Start.MollierPoint; }
            set { MollierPointControl_Start.MollierPoint = value; }
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
