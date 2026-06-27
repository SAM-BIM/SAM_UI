// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using SAM.Core.UI.WPF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// WPF replacement for the WinForms SAM.Analytical.Windows.Forms.InternalConditionLibraryForm: an
    /// internal-condition browser (search + Add/Duplicate/Remove/Import/Export, double-click to edit
    /// via <see cref="InternalConditionWindow"/>). Mirrors the legacy public surface (MultiSelect,
    /// Enabled, InternalConditionLibrary / ProfileLibrary / AdjacencyCluster, GetInternalConditions).
    /// </summary>
    public partial class InternalConditionLibraryWindow : System.Windows.Window
    {
        private class InternalConditionRow
        {
            public InternalCondition InternalCondition { get; set; }
            public string Name { get; set; }
        }

        private InternalConditionLibrary internalConditionLibrary;
        private ProfileLibrary profileLibrary;
        private AdjacencyCluster adjacencyCluster;
        private InternalCondition internalCondition_Selected;

        public InternalConditionLibraryWindow()
        {
            InitializeComponent();
        }

        public InternalConditionLibraryWindow(InternalConditionLibrary internalConditionLibrary, ProfileLibrary profileLibrary, AdjacencyCluster adjacencyCluster = null, InternalCondition internalCondition = null)
            : this()
        {
            this.internalConditionLibrary = internalConditionLibrary;
            this.profileLibrary = profileLibrary;
            this.adjacencyCluster = adjacencyCluster;
            internalCondition_Selected = internalCondition;

            Loaded += InternalConditionLibraryWindow_Loaded;
        }

        private void InternalConditionLibraryWindow_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (internalConditionLibrary == null)
            {
                internalConditionLibrary = new InternalConditionLibrary("Internal Condition Library");
            }

            RefreshView();

            if (internalCondition_Selected != null)
            {
                string uniqueId = internalConditionLibrary.GetUniqueId(internalCondition_Selected);
                InternalConditionRow row = (DataGrid_InternalConditions.ItemsSource as IEnumerable<InternalConditionRow>)?.FirstOrDefault(x => internalConditionLibrary.GetUniqueId(x.InternalCondition) == uniqueId);
                if (row != null)
                {
                    DataGrid_InternalConditions.SelectedItem = row;
                }
            }
        }

        private void RefreshView()
        {
            List<InternalCondition> internalConditions = internalConditionLibrary?.GetInternalConditions() ?? new List<InternalCondition>();
            internalConditions.Sort((x, y) => x.Name.CompareTo(y.Name));

            if (!string.IsNullOrWhiteSpace(TextBox_Search.Text))
            {
                internalConditions = Core.Query.Search(internalConditions, TextBox_Search.Text, (InternalCondition x) => x?.Name);
            }

            DataGrid_InternalConditions.ItemsSource = internalConditions.ConvertAll(x => new InternalConditionRow { InternalCondition = x, Name = x.Name });
        }

        private AnalyticalModel TemporaryAnalyticalModel()
        {
            return new AnalyticalModel("Temporary AnalyticalModel", null, null, null, adjacencyCluster ?? new AdjacencyCluster(), null, profileLibrary);
        }

        public bool MultiSelect
        {
            get { return DataGrid_InternalConditions.SelectionMode == DataGridSelectionMode.Extended; }
            set { DataGrid_InternalConditions.SelectionMode = value ? DataGridSelectionMode.Extended : DataGridSelectionMode.Single; }
        }

        public bool Enabled
        {
            set
            {
                System.Windows.Visibility visibility = value ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                Button_Add.Visibility = visibility;
                Button_Duplicate.Visibility = visibility;
                Button_Remove.Visibility = visibility;
            }
        }

        public List<InternalCondition> GetInternalConditions(bool selected = true)
        {
            IEnumerable<InternalConditionRow> source = selected
                ? DataGrid_InternalConditions.SelectedItems?.Cast<InternalConditionRow>()
                : DataGrid_InternalConditions.ItemsSource as IEnumerable<InternalConditionRow>;

            if (source == null)
            {
                return null;
            }

            List<InternalCondition> result = new List<InternalCondition>();
            foreach (InternalConditionRow row in source)
            {
                if (row?.InternalCondition == null)
                {
                    continue;
                }

                result.Add(new InternalCondition(row.InternalCondition));
            }

            return result;
        }

        public InternalConditionLibrary InternalConditionLibrary
        {
            get
            {
                if (internalConditionLibrary == null)
                {
                    return null;
                }

                InternalConditionLibrary result = new InternalConditionLibrary(internalConditionLibrary);
                internalConditionLibrary.GetInternalConditions()?.ForEach(x => result.Remove(x));
                GetInternalConditions(false)?.ForEach(x => result.Add(x));

                return result;
            }
        }

        public ProfileLibrary ProfileLibrary
        {
            get { return profileLibrary; }
        }

        public AdjacencyCluster AdjacencyCluster
        {
            get
            {
                if (adjacencyCluster == null)
                {
                    return null;
                }

                AdjacencyCluster result = new AdjacencyCluster(adjacencyCluster);
                GetInternalConditions(false)?.ForEach(x => result.AddObject(x));

                return result;
            }
        }

        private void Edit(InternalCondition internalCondition, bool add)
        {
            InternalConditionWindow internalConditionWindow = new InternalConditionWindow(TemporaryAnalyticalModel(), internalCondition) { Owner = this };
            if (internalConditionWindow.ShowDialog() != true)
            {
                return;
            }

            InternalCondition result = internalConditionWindow.InternalCondition;
            if (result == null)
            {
                return;
            }

            if (add)
            {
                internalConditionLibrary?.Add(result);
            }
            else
            {
                string uniqueId = internalConditionLibrary?.GetUniqueId(internalCondition);
                if (string.IsNullOrWhiteSpace(uniqueId))
                {
                    internalConditionLibrary?.Add(result);
                }
                else
                {
                    internalConditionLibrary?.Replace(uniqueId, result);
                }
            }

            RefreshView();
            SelectRow(result);
        }

        private void SelectRow(InternalCondition internalCondition)
        {
            string uniqueId = internalConditionLibrary?.GetUniqueId(internalCondition);
            if (string.IsNullOrWhiteSpace(uniqueId))
            {
                return;
            }

            InternalConditionRow row = (DataGrid_InternalConditions.ItemsSource as IEnumerable<InternalConditionRow>)?.FirstOrDefault(x => internalConditionLibrary.GetUniqueId(x.InternalCondition) == uniqueId);
            if (row != null)
            {
                DataGrid_InternalConditions.SelectedItem = row;
            }
        }

        private void Button_Add_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Edit(new InternalCondition("New Internal Condition"), true);
        }

        private void Button_Duplicate_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            InternalCondition internalCondition = (DataGrid_InternalConditions.SelectedItem as InternalConditionRow)?.InternalCondition;
            if (internalCondition == null)
            {
                return;
            }

            string name = (string.IsNullOrWhiteSpace(internalCondition.Name) ? string.Empty : internalCondition.Name).Trim();
            string name_Temp = name;
            int index = 1;
            while (internalConditionLibrary?.GetInternalConditions()?.Find(x => x.Name == name_Temp) != null)
            {
                name_Temp = string.Format("{0} {1}", name, index.ToString());
                index++;
            }

            Edit(new InternalCondition(name_Temp, Guid.NewGuid(), internalCondition), true);
        }

        private void Button_Remove_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            List<InternalConditionRow> selected = DataGrid_InternalConditions.SelectedItems?.Cast<InternalConditionRow>().ToList();
            if (selected == null || selected.Count == 0)
            {
                return;
            }

            foreach (InternalConditionRow row in selected)
            {
                internalConditionLibrary.Remove(row.InternalCondition);
            }

            RefreshView();
        }

        private void DataGrid_InternalConditions_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            InternalCondition internalCondition = (DataGrid_InternalConditions.SelectedItem as InternalConditionRow)?.InternalCondition;
            if (internalCondition == null)
            {
                return;
            }

            Edit(internalCondition, false);
        }

        private void Button_Import_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            List<InternalCondition> internalConditions = SAM.Analytical.UI.Query.Import<InternalCondition>(out List<IJSAMObject> jSAMObjects_All, null, null, this);
            if (internalConditions == null || internalConditions.Count == 0)
            {
                return;
            }

            internalConditions.ForEach(x => internalConditionLibrary.Add(x));

            // Pull in any profiles parsed from the same file so the imported conditions resolve.
            jSAMObjects_All?.OfType<Profile>().ToList().ForEach(x => profileLibrary?.Add(x));

            RefreshView();
        }

        private void Button_Export_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            List<InternalCondition> internalConditions = GetInternalConditions(false);
            if (internalConditions == null || internalConditions.Count == 0)
            {
                return;
            }

            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "json files (*.json)|*.json|All files (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true,
                FileName = "SAM_InternalConditionLibrary_CustomVer00.json"
            };

            if (saveFileDialog.ShowDialog(this) != true)
            {
                return;
            }

            string path = saveFileDialog.FileName;
            string name = System.IO.Path.GetFileNameWithoutExtension(path);

            InternalConditionLibrary internalConditionLibrary_Export = new InternalConditionLibrary(name);
            internalConditions.ForEach(x => internalConditionLibrary_Export.Add(x));

            System.Windows.MessageBox.Show(Core.Convert.ToFile(internalConditionLibrary_Export, path) ? "Library exported successfully." : "Library could not be exported.");
        }

        private void TextBox_Search_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            RefreshView();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            internalConditionLibrary.JsonForm(this, e);
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
