// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SAM.Units;

namespace SAM.Core.Mollier.UI.Controls
{
    /// <summary>
    /// WPF port of the WinForms MollierPointControl: a two-parameter (+ pressure) Mollier point editor.
    /// ComboBox.Text ↔ SelectedItem (string items), .Visible ↔ .Visibility, ComboBox.Click ↔ DropDownOpened,
    /// SelectedIndexChanged ↔ SelectionChanged. NumberBox is the ported WPF NumberBoxControl.
    /// </summary>
    public partial class MollierPointControl : UserControl
    {
        private List<ChartDataType> chartDataTypes = new List<ChartDataType>() { ChartDataType.DryBulbTemperature, ChartDataType.RelativeHumidity, ChartDataType.HumidityRatio, ChartDataType.WetBulbTemperature, ChartDataType.DewPointTemperature, ChartDataType.Enthalpy };

        public event SelectMollierPointEventHandler SelectMollierPoint;
        public event EventHandler ValueHanged;

        private MollierPoint mollierPoint;

        public MollierPointControl()
        {
            InitializeComponent();

            chartDataTypes.ForEach(x => ComboBox_FirstParameter.Items.Add(x.Description()));
            chartDataTypes.ForEach(x => ComboBox_SecondParameter.Items.Add(x.Description()));

            FirstParameterText = ChartDataType.DryBulbTemperature.Description();
            SecondParameterText = ChartDataType.RelativeHumidity.Description();

            NumberBoxControl_FirstParameter.ValueChanged += NumberBoxControl_ValueChanged;
            NumberBoxControl_SecondParameter.ValueChanged += NumberBoxControl_ValueChanged;
            NumberBoxControl_Pressure.ValueChanged += NumberBoxControl_ValueChanged;
        }

        // Helpers mapping the WinForms DropDownList ComboBox.Text onto the WPF SelectedItem (string items).
        private string FirstParameterText
        {
            get { return ComboBox_FirstParameter.SelectedItem as string; }
            set { ComboBox_FirstParameter.SelectedItem = value; }
        }

        private string SecondParameterText
        {
            get { return ComboBox_SecondParameter.SelectedItem as string; }
            set { ComboBox_SecondParameter.SelectedItem = value; }
        }

        public MollierPoint MollierPoint
        {
            get { return GetMollierPoint(); }
            set { SetMollierPoint(value); }
        }

        private void SetMollierPoint(MollierPoint mollierPoint)
        {
            if (mollierPoint == null)
            {
                return;
            }

            NumberBoxControl_Pressure.Value = mollierPoint.Pressure;

            ChartDataType chartDataType = Core.Query.Enum<ChartDataType>(FirstParameterText);
            double value = mollierPoint.Value(chartDataType);
            if (!double.IsNaN(value))
            {
                switch (chartDataType)
                {
                    case ChartDataType.HumidityRatio:
                        value = value * 1000;
                        break;
                }
            }
            NumberBoxControl_FirstParameter.Value = value;

            chartDataType = Core.Query.Enum<ChartDataType>(SecondParameterText);
            value = mollierPoint.Value(chartDataType);
            if (!double.IsNaN(value))
            {
                switch (chartDataType)
                {
                    case ChartDataType.HumidityRatio:
                        value = value * 1000;
                        break;
                }
            }
            NumberBoxControl_SecondParameter.Value = value;
        }

        private MollierPoint GetMollierPoint()
        {
            double dryBulbTemperature = double.NaN;
            double relativeHumidity = double.NaN;
            double humidityRatio = double.NaN;
            double wetBulbTemperature = double.NaN;
            double dewPointTemperature = double.NaN;
            double pressure = Standard.Pressure;
            double enthalpy = double.NaN;

            ChartDataType chartDataType = Core.Query.Enum<ChartDataType>(FirstParameterText);
            switch (chartDataType)
            {
                case ChartDataType.DryBulbTemperature: dryBulbTemperature = NumberBoxControl_FirstParameter.Value; break;
                case ChartDataType.RelativeHumidity: relativeHumidity = NumberBoxControl_FirstParameter.Value; break;
                case ChartDataType.HumidityRatio: humidityRatio = NumberBoxControl_FirstParameter.Value; break;
                case ChartDataType.DewPointTemperature: dewPointTemperature = NumberBoxControl_FirstParameter.Value; break;
                case ChartDataType.WetBulbTemperature: wetBulbTemperature = NumberBoxControl_FirstParameter.Value; break;
                case ChartDataType.Enthalpy: enthalpy = NumberBoxControl_FirstParameter.Value; break;
            }

            chartDataType = Core.Query.Enum<ChartDataType>(SecondParameterText);
            switch (chartDataType)
            {
                case ChartDataType.DryBulbTemperature: dryBulbTemperature = NumberBoxControl_SecondParameter.Value; break;
                case ChartDataType.RelativeHumidity: relativeHumidity = NumberBoxControl_SecondParameter.Value; break;
                case ChartDataType.HumidityRatio: humidityRatio = NumberBoxControl_SecondParameter.Value; break;
                case ChartDataType.DewPointTemperature: dewPointTemperature = NumberBoxControl_SecondParameter.Value; break;
                case ChartDataType.WetBulbTemperature: wetBulbTemperature = NumberBoxControl_SecondParameter.Value; break;
                case ChartDataType.Enthalpy: enthalpy = NumberBoxControl_SecondParameter.Value; break;
            }

            pressure = NumberBoxControl_Pressure.Value;

            if (!double.IsNaN(humidityRatio))
            {
                humidityRatio /= 1000;
            }

            if (!double.IsNaN(enthalpy))
            {
                enthalpy *= 1000;
            }

            if (!double.IsNaN(dewPointTemperature))
            {
                humidityRatio = double.NaN;
                enthalpy = double.NaN;
                dryBulbTemperature = double.NaN;
                relativeHumidity = double.NaN;
            }

            return Query.MollierPointByTwoParametersOrDewPoint(pressure: pressure, humidityRatio: humidityRatio, dryBulbTemperature: dryBulbTemperature, relativeHumidity: relativeHumidity, wetBulbTemperature: wetBulbTemperature, dewPointTemperature: dewPointTemperature, enthalpy: enthalpy);
        }

        private void ComboBox_FirstParameter_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox_SecondParameter.SelectionChanged -= ComboBox_SecondParameter_SelectedIndexChanged;
            string text = SecondParameterText;
            ComboBox_SecondParameter.Items.Clear();
            foreach (ChartDataType chartDataType in chartDataTypes)
            {
                if (Core.Query.Enum<ChartDataType>(FirstParameterText) != chartDataType)
                {
                    ComboBox_SecondParameter.Items.Add(chartDataType.Description());
                }
            }
            SecondParameterText = text;
            ComboBox_SecondParameter.SelectionChanged += ComboBox_SecondParameter_SelectedIndexChanged;

            if (FirstParameterText == "Dew Point Temperature")
            {
                ComboBox_SecondParameter.Visibility = Visibility.Collapsed;
                NumberBoxControl_SecondParameter.Visibility = Visibility.Collapsed;
                Label_SecondParameterUnit.Visibility = Visibility.Collapsed;
                SecondParameterText = ChartDataType.DewPointTemperature.Description();
            }
            else
            {
                ComboBox_SecondParameter.Visibility = Visibility.Visible;
                NumberBoxControl_SecondParameter.Visibility = Visibility.Visible;
                Label_SecondParameterUnit.Visibility = Visibility.Visible;
            }

            if (FirstParameterText == ChartDataType.Enthalpy.Description())
            {
                SecondParameterText = ChartDataType.HumidityRatio.Description();
            }

            UnitType unitType = Query.DefaultUnitType(FirstParameterText.Enum<ChartDataType>());

            Label_FirstParameterUnit.Text = unitType.Abbreviation();
            NumberBoxControl_FirstParameter.Tolerance = Units.Query.DefaultTolerance(unitType);

            SetMollierPoint(mollierPoint);
            mollierPoint = null;
        }

        private void ComboBox_FirstParameter_Click(object sender, EventArgs e)
        {
            mollierPoint = GetMollierPoint();
        }

        private void ComboBox_SecondParameter_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SecondParameterText == ChartDataType.DewPointTemperature.Description())
            {
                FirstParameterText = ChartDataType.DewPointTemperature.Description();
                return;
            }
            ComboBox_FirstParameter.SelectionChanged -= ComboBox_FirstParameter_SelectedIndexChanged;
            string text = FirstParameterText;
            ComboBox_FirstParameter.Items.Clear();
            foreach (ChartDataType chartDataType in chartDataTypes)
            {
                if (Core.Query.Enum<ChartDataType>(SecondParameterText) != chartDataType)
                {
                    ComboBox_FirstParameter.Items.Add(chartDataType.Description());
                }
            }

            FirstParameterText = text;
            ComboBox_FirstParameter.SelectionChanged += ComboBox_FirstParameter_SelectedIndexChanged;

            if (SecondParameterText == ChartDataType.Enthalpy.Description())
            {
                FirstParameterText = ChartDataType.HumidityRatio.Description();
            }

            UnitType unitType = Query.DefaultUnitType(SecondParameterText.Enum<ChartDataType>());

            Label_SecondParameterUnit.Text = unitType.Abbreviation();
            NumberBoxControl_SecondParameter.Tolerance = Units.Query.DefaultTolerance(unitType);

            SetMollierPoint(mollierPoint);
            mollierPoint = null;
        }

        private void ComboBox_SecondParameter_Click(object sender, EventArgs e)
        {
            mollierPoint = GetMollierPoint();
        }

        private void Button_SelectMollierPoint_Click(object sender, RoutedEventArgs e)
        {
            SelectMollierPointEventArgs selectMollierPointEventArgs = new SelectMollierPointEventArgs();

            SelectMollierPoint?.Invoke(this, selectMollierPointEventArgs);

            MollierPoint mollierPoint = selectMollierPointEventArgs?.MollierPoint;
            if (mollierPoint == null)
            {
                return;
            }

            MollierPoint = mollierPoint;
        }

        private void NumberBoxControl_ValueChanged(object sender, EventArgs e)
        {
            ValueHanged?.Invoke(this, e);
        }

        public bool PressureVisible
        {
            get { return Label_Pressure.Visibility == Visibility.Visible; }
            set
            {
                Visibility visibility = value ? Visibility.Visible : Visibility.Collapsed;
                Label_Pressure.Visibility = visibility;
                NumberBoxControl_Pressure.Visibility = visibility;
                Label_PressureUnit.Visibility = visibility;
            }
        }

        public bool PressureEnabled
        {
            get { return NumberBoxControl_Pressure.Enabled; }
            set { NumberBoxControl_Pressure.Enabled = value; }
        }

        public bool SelectMollierPointVisible
        {
            get { return Button_SelectMollierPoint.Visibility == Visibility.Visible; }
            set { Button_SelectMollierPoint.Visibility = value ? Visibility.Visible : Visibility.Collapsed; }
        }

        public double Pressure
        {
            get { return NumberBoxControl_Pressure.Value; }
            set { NumberBoxControl_Pressure.Value = value; }
        }
    }
}
