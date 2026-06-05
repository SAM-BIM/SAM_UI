// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Windows;
using System.Windows.Controls;
using SAM.Core.Mollier.UI.Controls;
using SAM.Geometry.Mollier;
using SystemColor = System.Drawing.Color;

namespace SAM.Core.Mollier.UI.Forms
{
    /// <summary>
    /// WPF port of the WinForms MollierProcessForm. The WinForms SplitContainer becomes a Grid
    /// (Panel1 row + a Panel2 host grid + a button bar); the selected process type swaps the
    /// hosted process control.
    /// </summary>
    public partial class MollierProcessForm : Window
    {
        private SystemColor color;
        private string start_Label;
        private string process_Label;
        private string end_Label;

        private MollierPoint mollierPoint;
        private MollierPoint previousMollierPoint;

        public MollierProcessForm()
        {
            InitializeComponent();

            MollierProcessType_ComboBox.Items.Add("Heating");
            MollierProcessType_ComboBox.Items.Add("Cooling");
            MollierProcessType_ComboBox.Items.Add("Heat Recovery");
            MollierProcessType_ComboBox.Items.Add("Mixing");
            MollierProcessType_ComboBox.Items.Add("Adiabatic Humidification (by water spray)");
            MollierProcessType_ComboBox.Items.Add("Isothermal Humidification (by steam)");
            MollierProcessType_ComboBox.Items.Add("Room Process");
        }

        public MollierForm MollierForm { get; set; }

        /// <summary>True when closed via OK (modeless-safe replacement for WPF DialogResult).</summary>
        public bool DialogOk { get; private set; }

        public MollierPoint MollierPoint
        {
            get { return mollierPoint; }
            set { mollierPoint = value; }
        }

        public MollierPoint PreviousMollierPoint
        {
            get { return previousMollierPoint; }
            set { if (value != null) { previousMollierPoint = value; } }
        }

        public UIMollierProcess GetUIMollierProcess()
        {
            FrameworkElement control = GetControl();
            if (control == null)
            {
                return null;
            }

            IMollierProcessControl mollierProcessControl = control as IMollierProcessControl;
            if (mollierProcessControl == null)
            {
                return null;
            }

            UIMollierProcess result = mollierProcessControl.GetUIMollierProcess();
            if (result == null)
            {
                return null;
            }

            result.UIMollierAppearance.Color = color;
            (result.UIMollierAppearance as UIMollierAppearance).Label = process_Label;

            result.UIMollierPointAppearance_Start = Create.UIMollierPointAppearance(DisplayPointType.Process, start_Label);
            result.UIMollierPointAppearance_End = Create.UIMollierPointAppearance(DisplayPointType.Process, end_Label);

            return result;
        }

        private void OK_Button_Click(object sender, RoutedEventArgs e)
        {
            DialogOk = true;
            Close();
        }

        private void Cancel_Button_Click(object sender, RoutedEventArgs e)
        {
            DialogOk = false;
            Close();
        }

        private void MollierProcessType_ComboBox_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateControl();
        }

        private void Customize_Button_Click(object sender, RoutedEventArgs e)
        {
            UIMollierProcessForm_Limited customizeProcessForm = new UIMollierProcessForm_Limited { Owner = this };
            customizeProcessForm.ShowDialog();
            if (!customizeProcessForm.DialogOk)
            {
                return;
            }

            color = customizeProcessForm.UIMollierProcess.UIMollierAppearance.Color;
            start_Label = customizeProcessForm.UIMollierProcess.UIMollierPointAppearance_Start.Label;
            process_Label = (customizeProcessForm.UIMollierProcess.UIMollierAppearance as UIMollierAppearance).Label;
            end_Label = customizeProcessForm.UIMollierProcess.UIMollierPointAppearance_End.Label;
        }

        private FrameworkElement GetControl()
        {
            if (Panel2_Host.Children.Count == 0)
            {
                return null;
            }

            return Panel2_Host.Children[0] as FrameworkElement;
        }

        private FrameworkElement CreateControl()
        {
            MollierProcessType mollierProcessType = Core.Query.Enum<MollierProcessType>(MollierProcessType_ComboBox.SelectedItem as string);

            switch (mollierProcessType)
            {
                case MollierProcessType.Heating:
                    HeatingProcessControl heatingProcessControl = new HeatingProcessControl() { MollierForm = MollierForm, Start = previousMollierPoint };
                    heatingProcessControl.SelectMollierPoint += ProcessControl_SelectMollierPoint;
                    return heatingProcessControl;

                case MollierProcessType.Cooling:
                    CoolingProcessControl coolingProcessControl = new CoolingProcessControl() { MollierForm = MollierForm, Start = previousMollierPoint };
                    coolingProcessControl.SelectMollierPoint += ProcessControl_SelectMollierPoint;
                    return coolingProcessControl;

                case MollierProcessType.HeatRecovery:
                    HeatRecoveryProcessControl heatRecoveryProcessControl = new HeatRecoveryProcessControl() { MollierForm = MollierForm, Supply = previousMollierPoint };
                    heatRecoveryProcessControl.SelectSupplyMollierPoint += ProcessControl_SelectMollierPoint;
                    heatRecoveryProcessControl.SelectReturnMollierPoint += ProcessControl_SelectMollierPoint;
                    return heatRecoveryProcessControl;

                case MollierProcessType.Mixing:
                    MixingProcessControl mixingProcessControl = new MixingProcessControl() { MollierForm = MollierForm, FirstPoint = previousMollierPoint };
                    mixingProcessControl.SelectFirstMollierPoint += ProcessControl_SelectMollierPoint;
                    mixingProcessControl.SelectSecondMollierPoint += ProcessControl_SelectMollierPoint;
                    return mixingProcessControl;

                case MollierProcessType.AdiabaticHumidification:
                    AdiabaticHumidificationProcessControl adiabaticHumidificationProcessControl = new AdiabaticHumidificationProcessControl() { MollierForm = MollierForm, Start = previousMollierPoint };
                    adiabaticHumidificationProcessControl.SelectMollierPoint += ProcessControl_SelectMollierPoint;
                    return adiabaticHumidificationProcessControl;

                case MollierProcessType.IsothermalHumidification:
                    IsothermalHumidificationProcessControl isothermalHumidificationProcessControl = new IsothermalHumidificationProcessControl() { MollierForm = MollierForm, Start = previousMollierPoint };
                    isothermalHumidificationProcessControl.SelectMollierPoint += ProcessControl_SelectMollierPoint;
                    return isothermalHumidificationProcessControl;

                case MollierProcessType.Undefined:
                    RoomProcessControl roomProcessControl = new RoomProcessControl() { MollierForm = MollierForm, StartMollierPoint = previousMollierPoint };
                    roomProcessControl.SelectMollierPoint += ProcessControl_SelectMollierPoint;
                    return roomProcessControl;
            }

            return null;
        }

        private void ProcessControl_SelectMollierPoint(object sender, SelectMollierPointEventArgs e)
        {
            Hide();
        }

        private void UpdateControl()
        {
            Panel2_Host.Children.Clear();

            FrameworkElement control = CreateControl();
            if (control == null)
            {
                return;
            }

            control.HorizontalAlignment = HorizontalAlignment.Left;
            control.VerticalAlignment = VerticalAlignment.Top;
            Panel2_Host.Children.Add(control);
        }
    }
}
