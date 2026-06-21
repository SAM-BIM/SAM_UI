// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.Windows.Input;

namespace SAM.Analytical.UI
{
    // In this namespace the bare name "Window" resolves to SAM.Analytical.Window (a building
    // element), so alias it (at namespace scope) to the WPF Window used for dialogs.
    using Window = System.Windows.Window;

    /// <summary>
    /// WPF replacement for the WinForms SAM.Analytical.Windows.Forms.SetProfileForm: picks a
    /// profile to apply over a range (or appended) of profile indices.
    /// </summary>
    public partial class SetProfileWindow : Window
    {
        public SetProfileWindow()
        {
            InitializeComponent();
        }

        public SetProfileWindow(int startIndex, IEnumerable<Profile> profiles)
        {
            InitializeComponent();

            TextBox_StartIndex.Text = startIndex.ToString();

            if (profiles != null)
            {
                foreach (Profile profile in profiles)
                {
                    ComboBox_Profile.Items.Add(profile);
                }
            }
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

        public Profile Profile
        {
            get
            {
                return ComboBox_Profile.SelectedItem as Profile;
            }

            set
            {
                ComboBox_Profile.SelectedItem = value;
            }
        }

        private void CheckBox_Append_Changed(object sender, System.Windows.RoutedEventArgs e)
        {
            bool append = CheckBox_Append.IsChecked == true;
            TextBox_StartIndex.IsEnabled = !append;
            Label_StartIndex.IsEnabled = !append;
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
