// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace SAM.Core.Mollier.UI.Controls
{
    /// <summary>
    /// WPF port of SAM.Core.Windows.NumberBoxControl (WinForms). A right-aligned numeric text box with
    /// NaN/Infinity display strings and tolerance snapping. Faithful API: Value, Tolerance, ValueChanged,
    /// String_NaN/PositiveInfinity/NegativeInfinity, Enabled. (The original stored the seed value in
    /// TextBox.Tag; here it is a private field.)
    /// </summary>
    public partial class NumberBoxControl : UserControl
    {
        private static readonly Regex NumericRegex = new Regex(@"^[0-9.\-,eE+]*$");

        private string string_NaN = string.Empty;
        private string string_PositiveInfinity = string.Empty;
        private string string_NegativeInfinity = string.Empty;
        private double tolerance = Core.Tolerance.MacroDistance;
        private double seedValue = double.NaN;

        public event EventHandler ValueChanged;

        public NumberBoxControl()
        {
            InitializeComponent();
        }

        public string String_NaN
        {
            get { return string_NaN; }
            set { string_NaN = value; }
        }

        public string String_PositiveInfinity
        {
            get { return string_PositiveInfinity; }
            set { string_PositiveInfinity = value; }
        }

        public string String_NegativeInfinity
        {
            get { return string_NegativeInfinity; }
            set { string_NegativeInfinity = value; }
        }

        public double Tolerance
        {
            get { return tolerance; }
            set { tolerance = value; }
        }

        public double Value
        {
            get { return GetValue(); }
            set { SetValue(value); }
        }

        public bool Enabled
        {
            get { return TextBox_Main.IsEnabled; }
            set { TextBox_Main.IsEnabled = value; }
        }

        public double GetValue()
        {
            string text = TextBox_Main.Text;
            if (string.IsNullOrEmpty(text))
            {
                return double.NaN;
            }

            if (string_NaN != null && text == string_NaN)
            {
                return double.NaN;
            }

            if (text == double.NaN.ToString())
            {
                return double.NaN;
            }

            if (string_PositiveInfinity != null && text == string_PositiveInfinity)
            {
                return double.PositiveInfinity;
            }

            if (text == double.PositiveInfinity.ToString())
            {
                return double.PositiveInfinity;
            }

            if (string_NegativeInfinity != null && text == string_NegativeInfinity)
            {
                return double.NegativeInfinity;
            }

            if (text == double.NegativeInfinity.ToString())
            {
                return double.NegativeInfinity;
            }

            if (!Core.Query.TryConvert(TextBox_Main.Text, out double result))
            {
                return double.NaN;
            }

            if (double.IsNaN(seedValue) || double.IsInfinity(seedValue))
            {
                return result;
            }

            if (result.AlmostEqual(seedValue, tolerance))
            {
                return seedValue;
            }

            return result;
        }

        public void SetValue(double value)
        {
            seedValue = value;

            if (double.IsNaN(value))
            {
                TextBox_Main.Text = string_NaN ?? value.ToString();
                return;
            }

            if (double.IsPositiveInfinity(value))
            {
                TextBox_Main.Text = string_PositiveInfinity ?? value.ToString();
                return;
            }

            if (double.IsNegativeInfinity(value))
            {
                TextBox_Main.Text = string_NegativeInfinity ?? value.ToString();
                return;
            }

            double displayValue = Core.Query.Round(value, tolerance);
            TextBox_Main.Text = displayValue.ToString();
        }

        private void TextBox_Main_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            string proposed = ((TextBox)sender).Text + e.Text;
            e.Handled = !NumericRegex.IsMatch(proposed);
        }

        private void TextBox_Main_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValueChanged?.Invoke(this, e);
        }
    }
}
