// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Mollier.UI.Controls;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SystemColor = System.Drawing.Color;

namespace SAM.Core.Mollier.UI
{
    /// <summary>WPF port of the WinForms MollierControlSettingsForm (View / Ranges / Tools settings dialog).</summary>
    public partial class MollierControlSettingsForm : Window
    {
        public event EventHandler ApplyClicked;

        private MollierControl mollierControl;

        // Empty is the "unset" sentinel: an untouched colour button has no stored colour.
        private SystemColor initialColor = SystemColor.Empty;

        /// <summary>True when closed via OK. Replaces WinForms DialogResult (WPF DialogResult throws on modeless windows).</summary>
        public bool DialogOk { get; private set; }

        public List<int> CustomColors { get; set; } = new List<int>();

        public MollierControlSettingsForm()
        {
            InitializeComponent();
        }

        public MollierControlSettingsForm(MollierControl mollierControl)
        {
            InitializeComponent();

            this.mollierControl = mollierControl;
            MollierControlSettings mollierControlSettings = mollierControl.MollierControlSettings;
            HumidityRatio_Max = mollierControlSettings.HumidityRatio_Max * 1000;
            HumidityRatio_Min = mollierControlSettings.HumidityRatio_Min * 1000;
            HumidityRatio_Interval = mollierControlSettings.HumidityRatio_Interval * 1000;
            Temperature_Max = mollierControlSettings.Temperature_Max;
            Temperature_Min = mollierControlSettings.Temperature_Min;
            Temperature_Interval = mollierControlSettings.Temperature_Interval;
            PartialVapourPressure = mollierControlSettings.PartialVapourPressure_Interval;
            Density_Interval = mollierControlSettings.Density_Interval;
            Enthalpy_Interval = mollierControlSettings.Enthalpy_Interval;
            SpecificVolume_Interval = mollierControlSettings.SpecificVolume_Interval;
            WetBulbTemperature_Interval = mollierControlSettings.WetBulbTemperature_Interval;

            GradientPoint = mollierControlSettings.GradientPoint;
            DisableUnits = mollierControlSettings.DisableUnits;
            DisableLabels = mollierControlSettings.DisableLabels;
            VisualizeSolver = mollierControlSettings.VisualizeSolver;
            PointGradientVisibilitySetting pointGradientVisibilitySetting = mollierControl.MollierControlSettings.VisibilitySettings.GetVisibilitySetting("User", ChartParameterType.Point) as PointGradientVisibilitySetting;
            if (pointGradientVisibilitySetting != null)
            {
                SetButtonColor(Button_LowIntensityColor, pointGradientVisibilitySetting.Color);
                SetButtonColor(Button_HighIntensityColor, pointGradientVisibilitySetting.GradientColor);
                CheckBox_GradientPoint.IsChecked = true;
            }
            else
            {
                PointGradientVisibilitySetting defaultPointGradientVisibilitySetting = Query.DefaultPointGradientVisibilitySetting();
                SetButtonColor(Button_LowIntensityColor, defaultPointGradientVisibilitySetting.Color);
                SetButtonColor(Button_HighIntensityColor, defaultPointGradientVisibilitySetting.GradientColor);
                CheckBox_GradientPoint.IsChecked = false;
            }

            DisableStartProcessPoint = mollierControlSettings.DisableStartProcessPoint;
            DisableEndProcessPoint = mollierControlSettings.DisableEndProcessPoint;
            DisablePointBoarder = mollierControlSettings.DisablePointBoarder;
            ProccessLineThickness = mollierControlSettings.ProccessLineThickness;

            DisableLabelStartProcessPoint = mollierControlSettings.DisableLabelStartProcessPoint;
            DisableLabelEndProcessPoint = mollierControlSettings.DisableLabelEndProcessPoint;
            DisableLabelProcess = mollierControlSettings.DisableLabelProcess;
            PointBoarderColor = mollierControlSettings.PointBorderColor;
            PointColor = mollierControlSettings.PointColor;
            DisablePoint = mollierControlSettings.DisablePoint;
            DisableCoolingAuxiliaryProcesses = mollierControlSettings.DisableCoolingAuxiliaryProcesses;

            PointBorderSize = mollierControlSettings.PointBorderSize;
            PointSize = mollierControlSettings.PointSize;

            VisibilitySettings visibilitySettings = mollierControlSettings.VisibilitySettings;
            if (visibilitySettings != null)
            {
                List<BuiltInVisibilitySetting> builtInVisibilitySettings = visibilitySettings.GetVisibilitySettings<BuiltInVisibilitySetting>(mollierControlSettings.DefaultTemplateName);
                if (builtInVisibilitySettings != null)
                {
                    foreach (BuiltInVisibilitySetting builtInVisibilitySetting in builtInVisibilitySettings)
                    {
                        BuiltInVisibilitySettingControl builtInVisibilitySettingControl = new BuiltInVisibilitySettingControl(builtInVisibilitySetting);
                        builtInVisibilitySettingControl.ColorChanged += BuiltInVisibilitySettingControl_ColorChanged;

                        FlowLayoutPanel_BuiltInVisibilitySettings.Children.Add(builtInVisibilitySettingControl);
                    }
                }
            }

            TextBox_MollierWindowHeight.Text = mollierControlSettings?.MollierWindowHeight == -1 ? string.Empty : mollierControlSettings.MollierWindowHeight.ToString();
            TextBox_MollierWindowWidth.Text = mollierControlSettings?.MollierWindowWidth == -1 ? string.Empty : mollierControlSettings.MollierWindowWidth.ToString();

            TextBox_PsychrometricWindowHeight.Text = mollierControlSettings?.PsychrometricWindowHeight == -1 ? string.Empty : mollierControlSettings.PsychrometricWindowHeight.ToString();
            TextBox_PsychrometricWindowWidth.Text = mollierControlSettings?.PsychrometricWindowWidth == -1 ? string.Empty : mollierControlSettings.PsychrometricWindowWidth.ToString();
        }

        #region Colour-button helpers (WinForms Button.BackColor → WPF Tag + Background)
        private SystemColor GetButtonColor(Button button)
        {
            return button.Tag is SystemColor color ? color : SystemColor.Empty;
        }

        private void SetButtonColor(Button button, SystemColor color)
        {
            button.Tag = color;
            button.Background = color.IsEmpty
                ? System.Windows.Media.Brushes.Transparent
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
        }
        #endregion

        private void IntegerOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, "^[0-9]+$");
        }

        private void BuiltInVisibilitySettingControl_ColorChanged(object sender, EventArgs e)
        {
            BuiltInVisibilitySettingControl builtInVisibilitySettingControl = sender as BuiltInVisibilitySettingControl;
            if (builtInVisibilitySettingControl == null)
            {
                return;
            }

            CustomColors = builtInVisibilitySettingControl.CustomColors;
            foreach (object control in FlowLayoutPanel_BuiltInVisibilitySettings.Children)
            {
                BuiltInVisibilitySettingControl builtInVisibilitySettingControl_Temp = control as BuiltInVisibilitySettingControl;
                if (builtInVisibilitySettingControl_Temp == null)
                {
                    continue;
                }

                builtInVisibilitySettingControl_Temp.CustomColors = CustomColors;
            }
        }

        private void Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogOk = false;
            Close();
        }

        private void Button_OK_Click(object sender, RoutedEventArgs e)
        {
            Apply();
            DialogOk = true;
            Close();
        }

        private void Button_Apply_Click(object sender, RoutedEventArgs e)
        {
            Apply();
        }

        public double HumidityRatio_Max
        {
            get
            {
                if (!Core.Query.TryConvert(HumidityRatioMaximumValueTextbox.Text, out double humidityRatio_Max))
                {
                    return double.NaN;
                }
                if (humidityRatio_Max > Limit.HumidityRatio_Max)
                {
                    MessageBox.Show("Wrong range\nMaximal Humidity Ratio is " + Limit.HumidityRatio_Max * 1000 + "!");
                    return double.NaN;
                }
                if (humidityRatio_Max <= System.Convert.ToDouble(HumidityRatioMinimumValueTextbox.Text))
                {
                    MessageBox.Show("Wrong range\nMaximal Humidity Ratio must be greater than minimal Humidity Ratio!");
                    return double.NaN;
                }
                return humidityRatio_Max / 1000;
            }

            set
            {
                HumidityRatioMaximumValueTextbox.Text = System.Math.Round(value, 2).ToString();
            }
        }
        public double HumidityRatio_Min
        {
            get
            {
                if (!Core.Query.TryConvert(HumidityRatioMinimumValueTextbox.Text, out double humidityRatio_Min))
                {
                    return double.NaN;
                }
                if (humidityRatio_Min < Limit.HumidityRatio_Min)
                {
                    MessageBox.Show("Wrong range\nMinimal Humidity Ratio is " + Limit.HumidityRatio_Min * 1000 + "!");
                    return double.NaN;
                }
                if (humidityRatio_Min >= System.Convert.ToDouble(HumidityRatioMaximumValueTextbox.Text))
                {
                    MessageBox.Show("Wrong range\nMinimal Humidity Ratio must be less than maximal Humidity Ratio!");
                    return double.NaN;
                }

                return humidityRatio_Min / 1000;
            }
            set
            {
                HumidityRatioMinimumValueTextbox.Text = System.Math.Round(value, 2).ToString();
            }
        }
        public double HumidityRatio_Interval
        {
            get
            {
                if (!Core.Query.TryConvert(HumidityRatioIntervalTextbox.Text, out double humidityRatio_Interval))
                {
                    return double.NaN;
                }
                if (humidityRatio_Interval <= 0)
                {
                    MessageBox.Show("Wrong range\nInterval has to be positive!");
                    return double.NaN;
                }
                if (humidityRatio_Interval > System.Convert.ToDouble(HumidityRatioMaximumValueTextbox.Text) - System.Convert.ToDouble(HumidityRatioMinimumValueTextbox.Text))
                {
                    MessageBox.Show("Wrong range\nInterval can not be greater than the axis lenght!");
                    return double.NaN;
                }
                return humidityRatio_Interval / 1000;
            }
            set
            {
                HumidityRatioIntervalTextbox.Text = value.ToString();
            }
        }
        public double Temperature_Max
        {
            get
            {
                if (!Core.Query.TryConvert(TemperatureMaximumValueTextbox.Text, out double temperature_Max))
                {
                    return double.NaN;
                }
                if (temperature_Max > Limit.DryBulbTemperature_Max)
                {
                    MessageBox.Show("Wrong range\nMaximal possibly temperature is " + Limit.DryBulbTemperature_Max + "!");
                    return double.NaN;
                }
                if (System.Convert.ToDouble(TemperatureMinimumValueTextbox.Text) >= temperature_Max)
                {
                    MessageBox.Show("Wrong range\nMaximal Temperature must be greater than minimal Temperature!");
                    return double.NaN;
                }
                return temperature_Max;
            }
            set
            {
                TemperatureMaximumValueTextbox.Text = System.Math.Round(value, 2).ToString();
            }
        }
        public double Temperature_Min
        {
            get
            {
                if (!Core.Query.TryConvert(TemperatureMinimumValueTextbox.Text, out double temperature_Min))
                {
                    return double.NaN;
                }
                if (temperature_Min < Limit.DryBulbTemperature_Min)
                {
                    MessageBox.Show("Wrong range\nMinimal possibly temperature is " + Limit.DryBulbTemperature_Min + "!");
                    return double.NaN;
                }
                if (temperature_Min >= System.Convert.ToDouble(TemperatureMaximumValueTextbox.Text))
                {
                    MessageBox.Show("Wrong range\nMinimal Temperature must be less than maximal Temperature!");
                    return double.NaN;
                }
                return temperature_Min;
            }
            set
            {
                TemperatureMinimumValueTextbox.Text = System.Math.Round(value, 2).ToString();
            }
        }
        public double Temperature_Interval
        {
            get
            {
                if (!Core.Query.TryConvert(TemperatureIntervalTextbox.Text, out double temperature_Interval))
                {
                    return double.NaN;
                }
                if (temperature_Interval <= 0)
                {
                    MessageBox.Show("Wrong range\nInterval has to be positive!");
                    return double.NaN;
                }
                if (temperature_Interval > System.Convert.ToDouble(TemperatureMaximumValueTextbox.Text) - System.Convert.ToDouble(TemperatureMinimumValueTextbox.Text))
                {
                    MessageBox.Show("Wrong range\nInterval can not be greater than the axis lenght!");
                    return double.NaN;
                }
                return temperature_Interval;
            }
            set
            {
                TemperatureIntervalTextbox.Text = value.ToString();
            }
        }
        public double PartialVapourPressure
        {
            get
            {
                if (!Core.Query.TryConvert(PartialVapourPressure_IntervalTextBox.Text, out double PartialVapourPressure))
                {
                    return double.NaN;
                }
                if (PartialVapourPressure <= 0)
                {
                    MessageBox.Show("Wrong range\n Interval has to be positive!");
                    return double.NaN;
                }
                return PartialVapourPressure;
            }
            set
            {
                PartialVapourPressure_IntervalTextBox.Text = value.ToString();
            }
        }

        public double Density_Interval
        {
            get
            {
                if (!Core.Query.TryConvert(DensityIntervalTextBox.Text, out double DensityInterval))
                {
                    return double.NaN;
                }
                if (DensityInterval <= 0)
                {
                    MessageBox.Show("Wrong range\n Interval has to be positive!");
                    return double.NaN;
                }
                return DensityInterval;
            }
            set
            {
                DensityIntervalTextBox.Text = value.ToString();
            }
        }
        public double Enthalpy_Interval
        {
            get
            {
                if (!Core.Query.TryConvert(EnthalpyIntervalTextBox.Text, out double EnthalpyInterval))
                {
                    return double.NaN;
                }
                if (EnthalpyInterval <= 0)
                {
                    MessageBox.Show("Wrong range\n Interval has to be positive!");
                    return double.NaN;
                }
                return EnthalpyInterval * 1000;
            }
            set
            {
                EnthalpyIntervalTextBox.Text = (value / 1000).ToString();
            }
        }
        public double SpecificVolume_Interval
        {
            get
            {
                if (!Core.Query.TryConvert(SpecificVolumeIntervalTextBox.Text, out double SpecificVolumeInterval))
                {
                    return double.NaN;
                }
                if (SpecificVolumeInterval <= 0)
                {
                    MessageBox.Show("Wrong range\n Interval has to be positive!");
                    return double.NaN;
                }
                return SpecificVolumeInterval;
            }
            set
            {
                SpecificVolumeIntervalTextBox.Text = value.ToString();
            }
        }
        public double WetBulbTemperature_Interval
        {
            get
            {
                if (!Core.Query.TryConvert(WetBulbTemperatureIntervalTextBox.Text, out double WetBulbTemperatureInterval))
                {
                    return double.NaN;
                }
                if (WetBulbTemperatureInterval <= 0)
                {
                    MessageBox.Show("Wrong range\n Interval has to be positive!");
                    return double.NaN;
                }
                return WetBulbTemperatureInterval;
            }
            set
            {
                WetBulbTemperatureIntervalTextBox.Text = value.ToString();
            }
        }

        public bool GradientPoint
        {
            get { return CheckBox_GradientPoint.IsChecked == true; }
            set { CheckBox_GradientPoint.IsChecked = value; }
        }
        public bool DisableUnits
        {
            get { return CheckBox_DisableUnits.IsChecked == true; }
            set { CheckBox_DisableUnits.IsChecked = value; }
        }
        public bool DisableLabels
        {
            get { return CheckBox_DisableLabels.IsChecked == true; }
            set { CheckBox_DisableLabels.IsChecked = value; }
        }
        public bool VisualizeSolver
        {
            get { return VisualizeSolver_Checkbox.IsChecked == true; }
            set { VisualizeSolver_Checkbox.IsChecked = value; }
        }

        public bool DisableStartProcessPoint
        {
            get { return CheckBox_EnableStartProcessPoint.IsChecked != true; }
            set { CheckBox_EnableStartProcessPoint.IsChecked = !value; }
        }

        public bool DisableEndProcessPoint
        {
            get { return CheckBox_EnableEndProcessPoint.IsChecked != true; }
            set { CheckBox_EnableEndProcessPoint.IsChecked = !value; }
        }

        public bool DisablePointBoarder
        {
            get { return CheckBox_DisablePointBorder.IsChecked != true; }
            set { CheckBox_DisablePointBorder.IsChecked = !value; }
        }

        public int ProccessLineThickness
        {
            get { return CheckBox_ProccessLineThickness.IsChecked == true ? 1 : -1; }
            set { CheckBox_ProccessLineThickness.IsChecked = value > 0; }
        }

        public bool DisableLabelStartProcessPoint
        {
            get { return checkBox_EnableProcessStartPointLabel.IsChecked != true; }
            set { checkBox_EnableProcessStartPointLabel.IsChecked = !value; }
        }

        public bool DisableLabelEndProcessPoint
        {
            get { return checkBox_EnableProcessEndPointLabel.IsChecked != true; }
            set { checkBox_EnableProcessEndPointLabel.IsChecked = !value; }
        }

        public bool DisableLabelProcess
        {
            get { return CheckBox_EnableProcessLabel.IsChecked != true; }
            set { CheckBox_EnableProcessLabel.IsChecked = !value; }
        }

        public SystemColor PointBoarderColor
        {
            get
            {
                SystemColor color = GetButtonColor(Button_PointBorderColor);
                return color == initialColor ? SystemColor.Empty : color;
            }
            set { SetButtonColor(Button_PointBorderColor, value); }
        }

        public SystemColor PointColor
        {
            get
            {
                SystemColor color = GetButtonColor(Button_PointColor);
                return color == initialColor ? SystemColor.Empty : color;
            }
            set { SetButtonColor(Button_PointColor, value); }
        }

        public bool DisablePoint
        {
            get { return CheckBox_DisablePoint.IsChecked != true; }
            set { CheckBox_DisablePoint.IsChecked = !value; }
        }

        public bool DisableCoolingAuxiliaryProcesses
        {
            get { return CheckBox_EnableCoolingAuxiliaryProcesses.IsChecked != true; }
            set { CheckBox_EnableCoolingAuxiliaryProcesses.IsChecked = !value; }
        }

        public int MollierWindowWidth
        {
            get
            {
                if (!Core.Query.TryConvert(TextBox_MollierWindowWidth.Text, out int result))
                {
                    return -1;
                }
                if (result == 0)
                {
                    return -1;
                }
                return result;
            }
            set { TextBox_MollierWindowWidth.Text = value.ToString(); }
        }

        public int MollierWindowHeight
        {
            get
            {
                if (!Core.Query.TryConvert(TextBox_MollierWindowHeight.Text, out int result))
                {
                    return -1;
                }
                if (result == 0)
                {
                    return -1;
                }
                return result;
            }
            set { TextBox_MollierWindowHeight.Text = value.ToString(); }
        }

        public int PsychrometricWindowWidth
        {
            get
            {
                if (!Core.Query.TryConvert(TextBox_PsychrometricWindowWidth.Text, out int result))
                {
                    return -1;
                }
                if (result == 0)
                {
                    return -1;
                }
                return result;
            }
            set { TextBox_PsychrometricWindowWidth.Text = value.ToString(); }
        }

        public int PsychrometricWindowHeight
        {
            get
            {
                if (!Core.Query.TryConvert(TextBox_PsychrometricWindowHeight.Text, out int result))
                {
                    return -1;
                }
                if (result == 0)
                {
                    return -1;
                }
                return result;
            }
            set { TextBox_PsychrometricWindowHeight.Text = value.ToString(); }
        }

        public int PointSize
        {
            get
            {
                if (!Core.Query.TryConvert(TextBox_PointSize.Text, out int result))
                {
                    return -1;
                }
                if (result == 0)
                {
                    return -1;
                }
                return result;
            }
            set { TextBox_PointSize.Text = value == -1 ? string.Empty : value.ToString(); }
        }

        public int PointBorderSize
        {
            get
            {
                if (!Core.Query.TryConvert(TextBox_PointBorderSize.Text, out int result))
                {
                    return -1;
                }
                if (result == 0)
                {
                    return -1;
                }
                return result;
            }
            set { TextBox_PointBorderSize.Text = value == -1 ? string.Empty : value.ToString(); }
        }

        private void Apply()
        {
            MollierControlSettings mollierControlSettings = mollierControl.MollierControlSettings;
            if (HumidityRatio_Max.ToString() != double.NaN.ToString())
                mollierControlSettings.HumidityRatio_Max = HumidityRatio_Max;
            if (HumidityRatio_Min.ToString() != double.NaN.ToString())
                mollierControlSettings.HumidityRatio_Min = HumidityRatio_Min;
            if (HumidityRatio_Interval.ToString() != double.NaN.ToString())
                mollierControlSettings.HumidityRatio_Interval = HumidityRatio_Interval;
            if (Temperature_Min.ToString() != double.NaN.ToString())
                mollierControlSettings.Temperature_Min = Temperature_Min;
            if (Temperature_Max.ToString() != double.NaN.ToString())
                mollierControlSettings.Temperature_Max = Temperature_Max;
            if (Temperature_Interval.ToString() != double.NaN.ToString())
                mollierControlSettings.Temperature_Interval = Temperature_Interval;
            if (PartialVapourPressure.ToString() != double.NaN.ToString())
                mollierControlSettings.PartialVapourPressure_Interval = PartialVapourPressure;
            if (Density_Interval.ToString() != double.NaN.ToString())
                mollierControlSettings.Density_Interval = Density_Interval;
            if (Enthalpy_Interval.ToString() != double.NaN.ToString())
                mollierControlSettings.Enthalpy_Interval = Enthalpy_Interval;
            if (SpecificVolume_Interval.ToString() != double.NaN.ToString())
                mollierControlSettings.SpecificVolume_Interval = SpecificVolume_Interval;
            if (WetBulbTemperature_Interval.ToString() != double.NaN.ToString())
                mollierControlSettings.WetBulbTemperature_Interval = WetBulbTemperature_Interval;

            mollierControlSettings.DisableUnits = DisableUnits;
            mollierControlSettings.DisableLabels = DisableLabels;
            mollierControlSettings.VisualizeSolver = VisualizeSolver;

            mollierControlSettings.DisableStartProcessPoint = DisableStartProcessPoint;
            mollierControlSettings.DisableEndProcessPoint = DisableEndProcessPoint;
            mollierControlSettings.DisablePointBoarder = DisablePointBoarder;
            mollierControlSettings.ProccessLineThickness = ProccessLineThickness;

            mollierControlSettings.DisableLabelStartProcessPoint = DisableLabelStartProcessPoint;
            mollierControlSettings.DisableLabelEndProcessPoint = DisableLabelEndProcessPoint;
            mollierControlSettings.DisableLabelProcess = DisableLabelProcess;
            mollierControlSettings.PointBorderColor = PointBoarderColor;
            mollierControlSettings.PointColor = PointColor;
            mollierControlSettings.DisablePoint = DisablePoint;
            mollierControlSettings.DisableCoolingAuxiliaryProcesses = DisableCoolingAuxiliaryProcesses;

            mollierControlSettings.MollierWindowHeight = MollierWindowHeight;
            mollierControlSettings.MollierWindowWidth = MollierWindowWidth;

            mollierControlSettings.PsychrometricWindowHeight = PsychrometricWindowHeight;
            mollierControlSettings.PsychrometricWindowWidth = PsychrometricWindowWidth;

            mollierControlSettings.PointBorderSize = PointBorderSize;
            mollierControlSettings.PointSize = PointSize;

            VisibilitySettings visibilitySettings = mollierControlSettings.VisibilitySettings;
            if (visibilitySettings == null)
            {
                visibilitySettings = new VisibilitySettings();
            }

            List<IVisibilitySetting> visibilitySettingsList = new List<IVisibilitySetting>();
            foreach (object control in FlowLayoutPanel_BuiltInVisibilitySettings.Children)
            {
                BuiltInVisibilitySettingControl builtInVisibilitySettingControl = control as BuiltInVisibilitySettingControl;
                if (builtInVisibilitySettingControl == null)
                {
                    continue;
                }

                visibilitySettingsList.Add(builtInVisibilitySettingControl.BuiltInVisibilitySetting);
            }
            if (CheckBox_GradientPoint.IsChecked == true)
            {
                PointGradientVisibilitySetting pointGradientVisibilitySetting = new PointGradientVisibilitySetting(GetButtonColor(Button_LowIntensityColor), GetButtonColor(Button_HighIntensityColor));
                visibilitySettingsList.Add(pointGradientVisibilitySetting);
            }

            visibilitySettings.SetVisibilitySettings("User", visibilitySettingsList);
            mollierControlSettings.DefaultTemplateName = "User";

            mollierControlSettings.VisibilitySettings = visibilitySettings;

            mollierControl.MollierControlSettings = mollierControlSettings;
            mollierControl.Regenerate();

            ApplyClicked?.Invoke(this, EventArgs.Empty);
        }

        private void Button_LowIntensityColor_Click(object sender, RoutedEventArgs e)
        {
            if (CustomColors == null)
            {
                CustomColors = new List<int>();
            }

            if (Query.TryGetColor(GetButtonColor(Button_LowIntensityColor), CustomColors, out SystemColor selectedColor))
            {
                SetButtonColor(Button_LowIntensityColor, selectedColor);
            }
        }

        private void Button_HighIntensityColor_Click(object sender, RoutedEventArgs e)
        {
            if (CustomColors == null)
            {
                CustomColors = new List<int>();
            }

            if (Query.TryGetColor(GetButtonColor(Button_HighIntensityColor), CustomColors, out SystemColor selectedColor))
            {
                SetButtonColor(Button_HighIntensityColor, selectedColor);
            }
        }

        private void Button_PointColor_Click(object sender, RoutedEventArgs e)
        {
            if (CustomColors == null)
            {
                CustomColors = new List<int>();
            }

            SystemColor current = GetButtonColor(Button_PointColor);
            if (Query.TryGetColor(current == initialColor ? (SystemColor?)null : current, CustomColors, out SystemColor selectedColor))
            {
                SetButtonColor(Button_PointColor, selectedColor);
            }
        }

        private void Button_PointBorderColor_Click(object sender, RoutedEventArgs e)
        {
            if (CustomColors == null)
            {
                CustomColors = new List<int>();
            }

            SystemColor current = GetButtonColor(Button_PointBorderColor);
            if (Query.TryGetColor(current == initialColor ? (SystemColor?)null : current, CustomColors, out SystemColor selectedColor))
            {
                SetButtonColor(Button_PointBorderColor, selectedColor);
            }
        }
    }
}
