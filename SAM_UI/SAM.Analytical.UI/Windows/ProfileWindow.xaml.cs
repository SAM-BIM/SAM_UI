// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.UI
{
    // In this namespace the bare name "Window" resolves to SAM.Analytical.Window (a building
    // element), so alias it (at namespace scope) to the WPF Window used for dialogs.
    using Window = System.Windows.Window;

    /// <summary>
    /// WPF replacement for the WinForms SAM.Analytical.Windows.Forms.ProfileForm: hosts the
    /// <see cref="ProfileControl"/> editor. Mirrors the legacy public surface (constructors +
    /// ProfileLibrary / Profile / Editable / Category).
    /// </summary>
    public partial class ProfileWindow : Window
    {
        public ProfileWindow()
        {
            InitializeComponent();
        }

        public ProfileWindow(Profile profile, bool editable = true)
        {
            InitializeComponent();

            ProfileControl_Main.Profile = profile;
            Editable = editable;
        }

        public ProfileLibrary ProfileLibrary
        {
            get
            {
                return ProfileControl_Main.ProfileLibrary;
            }

            set
            {
                ProfileControl_Main.ProfileLibrary = value;
            }
        }

        public Profile Profile
        {
            get
            {
                return ProfileControl_Main.Profile;
            }

            set
            {
                ProfileControl_Main.Profile = value;
            }
        }

        public bool Editable
        {
            get
            {
                return ProfileControl_Main.Editable;
            }

            set
            {
                ProfileControl_Main.Editable = value;
            }
        }

        public string Category
        {
            get
            {
                return ProfileControl_Main.Category;
            }

            set
            {
                ProfileControl_Main.Category = value;
            }
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
