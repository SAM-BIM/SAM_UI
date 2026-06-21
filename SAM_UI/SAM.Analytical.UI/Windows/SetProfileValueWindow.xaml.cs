// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Windows.Input;

namespace SAM.Analytical.UI
{
    // In this namespace the bare name "Window" resolves to SAM.Analytical.Window (a building
    // element), so alias it (at namespace scope) to the WPF Window used for dialogs.
    using Window = System.Windows.Window;

    /// <summary>
    /// WPF replacement for the WinForms SAM.Analytical.Windows.Forms.SetProfileValueForm:
    /// prompts for a value to apply over a range (or appended) of profile indices.
    /// </summary>
    public partial class SetProfileValueWindow : Window
    {
        public SetProfileValueWindow()
        {
            InitializeComponent();
        }

        public SetProfileValueWindow(int startIndex, int count, double? value)
        {
            InitializeComponent();

            TextBox_Value.Text = value == null || !value.HasValue ? null : value.Value.ToString();
            TextBox_StartIndex.Text = startIndex.ToString();
            TextBox_Count.Text = count.ToString();
        }

        public int StartIndex
        {
            get
            {
                return Core.Query.TryConvert(TextBox_StartIndex.Text, out int value) ? value : -1;
            }

            set
            {
                TextBox_StartIndex.Text = value.ToString();
            }
        }

        public int Count
        {
            get
            {
                return Core.Query.TryConvert(TextBox_Count.Text, out int value) ? value : -1;
            }

            set
            {
                TextBox_Count.Text = value.ToString();
            }
        }

        public double? Value
        {
            get
            {
                return Core.Query.TryConvert(TextBox_Value.Text, out double value) ? value : (double?)null;
            }

            set
            {
                TextBox_Value.Text = value.ToString();
            }
        }

        public bool Append
        {
            get
            {
                return CheckBox_Append.IsChecked == true;
            }

            set
            {
                CheckBox_Append.IsChecked = value;
            }
        }

        private void CheckBox_Append_Changed(object sender, System.Windows.RoutedEventArgs e)
        {
            bool append = CheckBox_Append.IsChecked == true;
            TextBox_StartIndex.IsEnabled = !append;
            Label_StartIndex.IsEnabled = !append;
        }

        private void NumberOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            SAM.Core.UI.WPF.Query.ControlText_NumberOnly(sender, e);
        }

        private void IntegerOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            SAM.Core.UI.WPF.Query.ControlText_IntegerOnly(sender, e);
        }

        private void Button_OK_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Button_Cancel_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
