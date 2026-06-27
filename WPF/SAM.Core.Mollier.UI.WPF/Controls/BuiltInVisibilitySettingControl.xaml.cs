// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SystemColor = System.Drawing.Color;

namespace SAM.Core.Mollier.UI.Controls
{
    /// <summary>
    /// WPF port of the WinForms BuiltInVisibilitySettingControl: a row with a visible checkbox,
    /// two (disabled) enum combos and a colour-swatch button. Colour picking goes through
    /// Query.TryGetColor (WinForms ColorDialog via the OxyPlot.WindowsForms transitive reference).
    /// </summary>
    public partial class BuiltInVisibilitySettingControl : UserControl
    {
        private BuiltInVisibilitySetting builtInVisibilitySetting;
        public event EventHandler ColorChanged;

        public List<int> CustomColors { get; set; } = new List<int>();

        public BuiltInVisibilitySettingControl()
        {
            InitializeComponent();
        }

        public BuiltInVisibilitySettingControl(BuiltInVisibilitySetting builtInVisibilitySetting)
        {
            InitializeComponent();
            this.builtInVisibilitySetting = builtInVisibilitySetting;
        }

        private void Button_Color_Click(object sender, RoutedEventArgs e)
        {
            if (CustomColors == null)
            {
                CustomColors = new List<int>();
            }

            if (!Query.TryGetColor(builtInVisibilitySetting?.Color, CustomColors, out SystemColor selectedColor))
            {
                return;
            }

            ApplySwatch(selectedColor);
            ColorChanged?.Invoke(this, EventArgs.Empty);
        }

        private void BuiltInVisibilitySettingControl_Load(object sender, RoutedEventArgs e)
        {
            if (ComboBox_ChartDataType.Items.Count == 0)
            {
                Enum.GetValues(typeof(ChartDataType)).Cast<ChartDataType>().ToList().ForEach(x => ComboBox_ChartDataType.Items.Add(x));
                Enum.GetValues(typeof(ChartParameterType)).Cast<ChartParameterType>().ToList().ForEach(x => ComboBox_ChartParameterType.Items.Add(x));
            }

            BuiltInVisibilitySetting = builtInVisibilitySetting;
        }

        public BuiltInVisibilitySetting BuiltInVisibilitySetting
        {
            get
            {
                if (builtInVisibilitySetting == null)
                {
                    return null;
                }

                BuiltInVisibilitySetting result = new BuiltInVisibilitySetting(builtInVisibilitySetting);
                result.ChartDataType = (ChartDataType)ComboBox_ChartDataType.SelectedItem;
                result.ChartParameterType = (ChartParameterType)ComboBox_ChartParameterType.SelectedItem;
                result.Color = swatchColor;
                result.Visible = CheckBox_Visible.IsChecked == true;

                return result;
            }

            set
            {
                builtInVisibilitySetting = value;

                if (builtInVisibilitySetting != null)
                {
                    ComboBox_ChartDataType.SelectedItem = builtInVisibilitySetting.ChartDataType;
                    ComboBox_ChartParameterType.SelectedItem = builtInVisibilitySetting.ChartParameterType;
                    ApplySwatch(builtInVisibilitySetting.Color);
                    CheckBox_Visible.IsChecked = builtInVisibilitySetting.Visible;
                }
            }
        }

        private SystemColor swatchColor = SystemColor.Empty;

        private void ApplySwatch(SystemColor color)
        {
            swatchColor = color;
            Button_Color.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            if (color != SystemColor.Empty)
            {
                Button_Color.Background = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
            }
        }
    }
}
