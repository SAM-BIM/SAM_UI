// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.UI.WPF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;

namespace SAM.Analytical.UI
{
    // In this namespace the bare name "Window" resolves to SAM.Analytical.Window (a building
    // element), so alias it (at namespace scope) to the WPF Window used for dialogs.
    using Window = System.Windows.Window;

    /// <summary>
    /// WPF replacement for the WinForms SAM.Analytical.Windows.Forms.ProfileLibraryForm: a profile
    /// browser (type filter + search + Add/Duplicate/Remove/Import/Export, double-click to edit via
    /// <see cref="ProfileWindow"/>). Mirrors the legacy public surface (Type, TypeEnabled,
    /// MultiSelect, Enabled, ProfileLibrary, GetProfiles).
    /// </summary>
    public partial class ProfileLibraryWindow : Window
    {
        private class ProfileRow
        {
            public Profile Profile { get; set; }
            public string Name { get; set; }
            public string Type { get; set; }
        }

        private ProfileLibrary profileLibrary;
        private Profile profile_Selected;
        private readonly List<ProfileRow> rows = new List<ProfileRow>();

        public ProfileLibraryWindow()
        {
            InitializeComponent();
            AddTypes();
            Loaded += ProfileLibraryWindow_Loaded;
        }

        public ProfileLibraryWindow(ProfileLibrary profileLibrary)
            : this()
        {
            this.profileLibrary = profileLibrary;
        }

        public ProfileLibraryWindow(ProfileLibrary profileLibrary, Profile profile)
            : this()
        {
            this.profileLibrary = profileLibrary;
            profile_Selected = profile;
        }

        private void ProfileLibraryWindow_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (profileLibrary == null)
            {
                profileLibrary = new ProfileLibrary("Profile Library");
            }

            BuildRows();
            RefreshView();

            if (profile_Selected != null)
            {
                string uniqueId = profileLibrary.GetUniqueId(profile_Selected);
                ProfileRow row = rows.Find(x => profileLibrary.GetUniqueId(x.Profile) == uniqueId);
                if (row != null)
                {
                    DataGrid_Profiles.SelectedItem = row;
                }
            }
        }

        private void AddTypes()
        {
            List<string> typeDescriptions = Query.CategoryEnums().ConvertAll(x => Core.Query.Description(x));

            Column_Type.ItemsSource = typeDescriptions;

            ComboBox_Type.Items.Add(string.Empty);
            typeDescriptions.ForEach(x => ComboBox_Type.Items.Add(x));
            ComboBox_Type.SelectedIndex = 0;
        }

        private void BuildRows()
        {
            rows.Clear();

            List<Profile> profiles = profileLibrary?.GetProfiles();
            profiles?.ForEach(x => rows.Add(ToRow(x)));
        }

        private static ProfileRow ToRow(Profile profile)
        {
            Enum type = profile.ProfileType;
            if ((ProfileType)type == ProfileType.Undefined)
            {
                type = profile.ProfileGroup;
            }

            return new ProfileRow { Profile = profile, Name = profile.Name, Type = Core.Query.Description(type) };
        }

        private void RefreshView()
        {
            List<ProfileRow> visible = rows.FindAll(x => IsValid(x.Profile));

            if (!string.IsNullOrWhiteSpace(TextBox_Search.Text))
            {
                visible = Core.Query.Search(visible, TextBox_Search.Text, (ProfileRow x) => string.Join(" ", new[] { x.Name, x.Type }.Where(y => !string.IsNullOrWhiteSpace(y))));
            }

            DataGrid_Profiles.ItemsSource = null;
            DataGrid_Profiles.ItemsSource = visible;
        }

        private bool IsValid(Profile profile)
        {
            if (profile == null)
            {
                return false;
            }

            Enum type = profile.ProfileType;
            if ((ProfileType)type == ProfileType.Undefined)
            {
                type = profile.ProfileGroup;
            }

            if (type == null)
            {
                return true;
            }

            Enum type_Selected = Type;
            if (type_Selected == null || type.Equals(type_Selected))
            {
                return true;
            }

            if (type_Selected is ProfileType profileType_Selected)
            {
                return type is ProfileGroup profileGroup && profileGroup.Equals(profileType_Selected.ProfileGroup());
            }

            if (type_Selected is ProfileGroup profileGroup_Selected)
            {
                return type is ProfileType profileType && profileGroup_Selected.Equals(profileType.ProfileGroup());
            }

            return true;
        }

        public Enum Type
        {
            get
            {
                string text = ComboBox_Type.SelectedItem as string;
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                Enum result = Core.Query.Enum<ProfileType>(text);
                if ((ProfileType)result == ProfileType.Undefined)
                {
                    result = Core.Query.Enum<ProfileGroup>(text);
                }

                return result;
            }

            set
            {
                ComboBox_Type.SelectedItem = value == null ? string.Empty : Core.Query.Description(value);
            }
        }

        public bool TypeEnabled
        {
            get { return ComboBox_Type.IsEnabled; }
            set { ComboBox_Type.IsEnabled = value; }
        }

        public bool MultiSelect
        {
            get { return DataGrid_Profiles.SelectionMode == DataGridSelectionMode.Extended; }
            set { DataGrid_Profiles.SelectionMode = value ? DataGridSelectionMode.Extended : DataGridSelectionMode.Single; }
        }

        public bool Enabled
        {
            set
            {
                System.Windows.Visibility visibility = value ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                Button_Add.Visibility = visibility;
                Button_Duplicate.Visibility = visibility;
                Button_Remove.Visibility = visibility;
                Column_Type.IsReadOnly = !value;
            }
        }

        public ProfileLibrary ProfileLibrary
        {
            get
            {
                if (profileLibrary == null)
                {
                    return null;
                }

                ProfileLibrary result = new ProfileLibrary(profileLibrary);
                profileLibrary.GetProfiles()?.ForEach(x => result.Remove(x));
                GetProfiles(false)?.ForEach(x => result.Add(x));

                return result;
            }
        }

        public List<Profile> GetProfiles(bool selected = true)
        {
            IEnumerable<ProfileRow> source = selected ? DataGrid_Profiles.SelectedItems?.Cast<ProfileRow>() : rows;
            if (source == null)
            {
                return null;
            }

            List<Profile> result = new List<Profile>();
            foreach (ProfileRow row in source)
            {
                if (row?.Profile == null)
                {
                    continue;
                }

                result.Add(new Profile(row.Profile.Guid, row.Profile, row.Type));
            }

            return result;
        }

        private void ComboBox_Type_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshView();
        }

        private void TextBox_Search_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            RefreshView();
        }

        private void Button_Add_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ProfileWindow profileWindow = new ProfileWindow((Profile)null) { Owner = this, ProfileLibrary = ProfileLibrary, Category = ComboBox_Type.SelectedItem as string };
            if (profileWindow.ShowDialog() != true)
            {
                return;
            }

            Profile profile = profileWindow.Profile;
            profileLibrary = profileWindow.ProfileLibrary;
            if (profile == null)
            {
                return;
            }

            profileLibrary?.Add(profile);
            BuildRows();
            RefreshView();
            SelectProfileRow(profile);
        }

        private void Button_Duplicate_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Profile profile = (DataGrid_Profiles.SelectedItem as ProfileRow)?.Profile;
            if (profile == null)
            {
                return;
            }

            string name = (string.IsNullOrWhiteSpace(profile.Name) ? string.Empty : profile.Name).Trim();
            string name_Temp = name;
            int index = 1;
            while (profileLibrary?.GetProfiles()?.Find(x => x.Name == name_Temp) != null)
            {
                name_Temp = string.Format("{0} {1}", name, index.ToString());
                index++;
            }

            profile = new Profile(Guid.NewGuid(), profile, name_Temp, profile.Category);

            ProfileWindow profileWindow = new ProfileWindow(profile) { Owner = this, ProfileLibrary = ProfileLibrary };
            if (profileWindow.ShowDialog() != true)
            {
                return;
            }

            profile = profileWindow.Profile;
            profileLibrary = profileWindow.ProfileLibrary;
            if (profile == null)
            {
                return;
            }

            profileLibrary?.Add(profile);
            BuildRows();
            RefreshView();
            SelectProfileRow(profile);
        }

        private void Button_Remove_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            List<ProfileRow> selected = DataGrid_Profiles.SelectedItems?.Cast<ProfileRow>().ToList();
            if (selected == null || selected.Count == 0)
            {
                return;
            }

            foreach (ProfileRow row in selected)
            {
                profileLibrary.Remove(row.Profile);
            }

            BuildRows();
            RefreshView();
        }

        private void Button_Import_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Func<Profile, bool> func = null;
            if (!ComboBox_Type.IsEnabled)
            {
                func = new Func<Profile, bool>(IsValid);
            }

            List<Profile> profiles = Query.Import<Profile>(out List<Core.IJSAMObject> _, func, null, this);
            if (profiles == null)
            {
                return;
            }

            profiles.ForEach(x => profileLibrary?.Add(x));
            BuildRows();
            RefreshView();
        }

        private void Button_Export_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            List<Profile> profiles = GetProfiles(false);
            if (profiles == null || profiles.Count == 0)
            {
                return;
            }

            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "json files (*.json)|*.json|All files (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true,
                FileName = "SAM_ProfileLibrary_CustomVer00.json"
            };

            if (saveFileDialog.ShowDialog(this) != true)
            {
                return;
            }

            string path = saveFileDialog.FileName;
            string name = System.IO.Path.GetFileNameWithoutExtension(path);

            ProfileLibrary profileLibrary_Export = new ProfileLibrary(name);
            profiles.ForEach(x => profileLibrary_Export.Add(x));

            System.Windows.MessageBox.Show(Core.Convert.ToFile(profileLibrary_Export, path) ? "Library exported successfully." : "Library could not be exported.");
        }

        private void DataGrid_Profiles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Profile profile = (DataGrid_Profiles.SelectedItem as ProfileRow)?.Profile;
            if (profile == null)
            {
                return;
            }

            string uniqueId = ProfileLibrary?.GetUniqueId(profile);

            ProfileWindow profileWindow = new ProfileWindow(new Profile(profile)) { Owner = this, ProfileLibrary = ProfileLibrary };
            if (profileWindow.ShowDialog() != true)
            {
                return;
            }

            profile = profileWindow.Profile;
            profileLibrary = profileWindow.ProfileLibrary;

            if (string.IsNullOrWhiteSpace(uniqueId))
            {
                profileLibrary?.Add(profile);
            }
            else
            {
                profileLibrary?.Replace(uniqueId, profile);
            }

            BuildRows();
            RefreshView();
            SelectProfileRow(profile);
        }

        private void SelectProfileRow(Profile profile)
        {
            if (profile == null)
            {
                return;
            }

            string uniqueId = profileLibrary?.GetUniqueId(profile);
            if (string.IsNullOrWhiteSpace(uniqueId))
            {
                return;
            }

            ProfileRow row = (DataGrid_Profiles.ItemsSource as IEnumerable<ProfileRow>)?.FirstOrDefault(x => profileLibrary.GetUniqueId(x.Profile) == uniqueId);
            if (row != null)
            {
                DataGrid_Profiles.SelectedItem = row;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            profileLibrary.JsonForm(this, e);
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
