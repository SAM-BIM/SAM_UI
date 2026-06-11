// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace SAM.Core.Mollier.UI.Controls
{
    /// <summary>
    /// WPF port of the WinForms ParameterControl: a labelled numeric input (name · value · unit).
    /// First leaf control of Stage 2e; establishes the WinForms UserControl -> WPF UserControl pattern.
    /// The WinForms numeric KeyPress filter (Windows.EventHandler.ControlText_NumberOnly) becomes a
    /// PreviewTextInput regex filter.
    /// </summary>
    public partial class ParameterControl : UserControl
    {
        public event EventHandler ValueHanged;

        // Allows digits, a single leading sign, decimal separators and partial input while typing.
        private static readonly Regex NumericRegex = new Regex(@"^[0-9.\-,eE+]*$");

        private Units.UnitType unitType = Units.UnitType.Undefined;
        private string text = null;

        public ParameterControl()
        {
            InitializeComponent();
        }

        public ParameterControl(ProcessParameterType processParameterType)
        {
            InitializeComponent();
            SetProcessParameterType(processParameterType);
        }

        private void SetProcessParameterType(ProcessParameterType processParameterType)
        {
            if (processParameterType != ProcessParameterType.Undefined)
            {
                Label_Name.Text = processParameterType.Description();
                Label_Unit.Text = Units.Query.Abbreviation(Query.DefaultUnitType(processParameterType));

                unitType = Units.UnitType.Undefined;
                text = null;
            }
            else
            {
                Label_Name.Text = text;
                Label_Unit.Text = Units.Query.Abbreviation(unitType);
            }
        }

        public Units.UnitType UnitType
        {
            get
            {
                ProcessParameterType processParameterType = ProcessParameterType;
                return processParameterType != ProcessParameterType.Undefined ? Query.DefaultUnitType(processParameterType) : unitType;
            }
            set
            {
                unitType = value;
                Label_Unit.Text = Units.Query.Abbreviation(unitType);
            }
        }

        public new string Name
        {
            get { return Label_Name.Text; }
            set { text = value; Label_Name.Text = text; }
        }

        public ProcessParameterType ProcessParameterType
        {
            get { return Core.Query.Enum<ProcessParameterType>(Label_Name.Text); }
            set { SetProcessParameterType(value); }
        }

        public double Value
        {
            get
            {
                return Core.Query.TryConvert(TextBox_Value.Text, out double result) ? result : double.NaN;
            }
            set
            {
                TextBox_Value.Text = double.IsNaN(value) ? null : value.ToString();
            }
        }

        public bool Enabled
        {
            get { return TextBox_Value.IsEnabled; }
            set { TextBox_Value.IsEnabled = value; }
        }

        private void TextBox_Value_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            string proposed = ((TextBox)sender).Text + e.Text;
            e.Handled = !NumericRegex.IsMatch(proposed);
        }

        private void TextBox_Value_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValueHanged?.Invoke(sender, e);
        }
    }
}
