// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace SAM.Analytical.UI
{
    /// <summary>
    /// WPF replacement for the WinForms SAM.Analytical.Windows.Controls.ProfileControl: edits a
    /// <see cref="Profile"/>'s daily values via a grid + bar chart, with Set Value / Set Profile /
    /// Remove operations. Mirrors the legacy public surface (Profile, ProfileLibrary, Category,
    /// Editable). Edits are applied live to the working profile (the legacy rebuilt it from the
    /// grid on demand). The chart auto-ranges its value axis (the legacy forced 0 into range,
    /// which flattened small ranges like a 26-28 thermostat).
    /// </summary>
    public partial class ProfileControl : UserControl
    {
        private ProfileLibrary profileLibrary;
        private Profile profile;
        private bool editable = true;

        private class ProfileValue
        {
            public int Index { get; set; }
            public double Value { get; set; }
            public string ProfileName { get; set; }
        }

        public ProfileControl()
        {
            InitializeComponent();
        }

        public ProfileControl(Profile profile, bool editable = true)
        {
            InitializeComponent();

            this.profile = profile;
            Editable = editable;

            LoadProfile(profile);
        }

        private void LoadCategories()
        {
            if (ComboBox_Category.Items.Count != 0)
            {
                return;
            }

            Query.CategoryEnums()?.ForEach(x => ComboBox_Category.Items.Add(Core.Query.Description(x)));
        }

        private void LoadProfile(Profile profile)
        {
            LoadCategories();

            TextBox_Name.Text = profile?.Name;
            DataGrid_Values.ItemsSource = null;
            TextBox_MinValue.Text = string.Empty;
            TextBox_MaxValue.Text = string.Empty;
            Chart_Main.Model = null;

            if (profile == null)
            {
                return;
            }

            Enum @enum = profile.ProfileType;
            if ((ProfileType)@enum == ProfileType.Undefined)
            {
                @enum = profile.ProfileGroup;
            }
            ComboBox_Category.Text = Core.Query.Description(@enum);

            List<ProfileValue> rows = new List<ProfileValue>();
            double minValue = double.MaxValue;
            double maxValue = double.MinValue;

            int min = profile.Min;
            int max = profile.Max;
            if (min != -1 && max != -1)
            {
                for (int i = min; i < max + 1; i++)
                {
                    if (!profile.TryGetValue(i, out Profile profile_Temp, out double value))
                    {
                        continue;
                    }

                    rows.Add(new ProfileValue { Index = i, Value = value, ProfileName = profile_Temp?.Name });

                    if (value > maxValue)
                    {
                        maxValue = value;
                    }

                    if (value < minValue)
                    {
                        minValue = value;
                    }
                }
            }

            DataGrid_Values.ItemsSource = rows;

            TextBox_MaxValue.Text = maxValue != double.MinValue ? maxValue.ToString() : string.Empty;
            TextBox_MinValue.Text = minValue != double.MaxValue ? minValue.ToString() : string.Empty;

            Profile[] profiles = profile.GetProfiles();
            Column_ProfileName.Visibility = profiles != null && profiles.Length != 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

            LoadChart(profile);
        }

        private void LoadChart(Profile profile)
        {
            double[] values = profile?.GetDailyValues();
            if (values == null || values.Length == 0)
            {
                Chart_Main.Model = null;
                return;
            }

            PlotModel plotModel = new PlotModel { Title = profile.Name };
            plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, MinimumPadding = 0, MaximumPadding = 0, MajorGridlineStyle = LineStyle.Solid, MajorGridlineColor = OxyColors.LightGray });
            plotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, MajorGridlineStyle = LineStyle.Solid, MajorGridlineColor = OxyColors.LightGray });

            LinearBarSeries linearBarSeries = new LinearBarSeries { FillColor = OxyColor.FromRgb(79, 129, 189), StrokeThickness = 0 };
            for (int i = 0; i < values.Length; i++)
            {
                linearBarSeries.Points.Add(new DataPoint(i, values[i]));
            }
            plotModel.Series.Add(linearBarSeries);

            Chart_Main.Model = plotModel;
        }

        public Profile Profile
        {
            get
            {
                string category = ComboBox_Category.Text;
                string name = TextBox_Name.Text;

                return profile == null ? new Profile(name, category) : new Profile(profile.Guid, profile, name, category);
            }

            set
            {
                profile = value;
                LoadProfile(profile);
            }
        }

        public ProfileLibrary ProfileLibrary
        {
            get
            {
                return profileLibrary;
            }

            set
            {
                profileLibrary = value;
                Button_SetProfile.IsEnabled = profileLibrary != null && editable;
            }
        }

        public string Category
        {
            get
            {
                return ComboBox_Category.Text;
            }

            set
            {
                LoadCategories();
                ComboBox_Category.Text = value;
            }
        }

        public bool Editable
        {
            get
            {
                return editable;
            }

            set
            {
                editable = value;

                TextBox_Name.IsEnabled = value;
                ComboBox_Category.IsEnabled = value;
                Column_Value.IsReadOnly = !value;
                Button_Remove.IsEnabled = value;
                Button_SetProfile.IsEnabled = value && profileLibrary != null;
                Button_SetValue.IsEnabled = value;
            }
        }

        private int[] SelectedIndices()
        {
            return DataGrid_Values.SelectedItems?.Cast<ProfileValue>().Select(x => x.Index).OrderBy(x => x).ToArray() ?? new int[0];
        }

        private void DataGrid_Values_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit || e.Column != Column_Value || profile == null)
            {
                return;
            }

            if (!(e.Row.Item is ProfileValue row) || !(e.EditingElement is TextBox textBox))
            {
                return;
            }

            if (!Core.Query.TryConvert(textBox.Text, out double value))
            {
                return;
            }

            if (profile.Update(row.Index, value))
            {
                Dispatcher.BeginInvoke(new Action(() => LoadProfile(profile)));
            }
        }

        private void Button_SetValue_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (profile == null)
            {
                return;
            }

            int[] indices = SelectedIndices();
            int count = indices.Length != 0 ? indices.Length : (DataGrid_Values.Items?.Count ?? 1);
            int startIndex = indices.Length != 0 ? indices[0] : 0;

            double? value = null;
            HashSet<double> values = new HashSet<double>();
            foreach (ProfileValue profileValue in DataGrid_Values.SelectedItems.Cast<ProfileValue>())
            {
                values.Add(profileValue.Value);
            }
            if (values.Count == 1)
            {
                value = values.First();
            }

            SetProfileValueWindow setProfileValueWindow = new SetProfileValueWindow(startIndex, count, value) { Owner = System.Windows.Window.GetWindow(this) };
            if (setProfileValueWindow.ShowDialog() != true)
            {
                return;
            }

            count = setProfileValueWindow.Count;
            value = setProfileValueWindow.Value;
            startIndex = setProfileValueWindow.Append ? profile.Max + 1 : setProfileValueWindow.StartIndex;

            if (value == null || !value.HasValue)
            {
                return;
            }

            profile.Update(startIndex, count, value.Value);

            LoadProfile(profile);
        }

        private void Button_Remove_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (profile == null)
            {
                return;
            }

            int[] indices = SelectedIndices();
            if (indices.Length == 0)
            {
                return;
            }

            int startIndex = indices[0];
            int count = indices.Length;

            if (startIndex + count - 1 == profile.Max)
            {
                profile.Remove(count);
            }
            else
            {
                profile.Update(startIndex, count, 0);
            }

            LoadProfile(profile);
        }

        private void Button_SetProfile_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (profile == null)
            {
                return;
            }

            int[] indices = SelectedIndices();
            if (indices.Length == 0)
            {
                return;
            }

            List<Profile> profiles = profileLibrary?.GetProfiles(profile.ProfileGroup, true);
            if (profiles == null)
            {
                return;
            }

            profiles.RemoveAll(x => x.Guid == profile.Guid);
            if (profiles.Count == 0)
            {
                return;
            }

            int startIndex = indices[0];

            SetProfileWindow setProfileWindow = new SetProfileWindow(startIndex, profiles) { Owner = System.Windows.Window.GetWindow(this) };
            if (setProfileWindow.ShowDialog() != true)
            {
                return;
            }

            Profile profile_ToBeAdded = setProfileWindow.Profile;
            if (profile_ToBeAdded == null)
            {
                return;
            }

            startIndex = setProfileWindow.Append ? profile.Max + 1 : setProfileWindow.StartIndex;

            profile.Update(startIndex, profile_ToBeAdded);

            LoadProfile(profile);
        }
    }
}
