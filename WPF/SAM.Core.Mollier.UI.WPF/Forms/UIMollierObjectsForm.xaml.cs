// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Geometry.Mollier;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace SAM.Core.Mollier.UI.Forms
{
    /// <summary>WPF port of the WinForms UIMollierObjectsForm (point/process manager with two DataGrids).</summary>
    public partial class UIMollierObjectsForm : Window
    {
        private MollierModel mollierModel;
        private MollierControlSettings mollierControlSettings;

        public event MollierModelEditedEventHandler MollierModelEdited;
        public event MollierObjectSelectedEventHandler MollierObjectSelected;

        // Default private variables
        private double airflow = 0;
        private Units.UnitType airFlowUnit = Units.UnitType.CubicMeterPerSecond;
        private string defaultGroup = "All";

        public UIMollierObjectsForm()
        {
            InitializeComponent();
        }

        public UIMollierObjectsForm(MollierModel mollierModel, MollierControlSettings mollierControlSettings)
        {
            this.mollierControlSettings = mollierControlSettings;
            InitializeComponent();
            initializeDataGridViews(mollierModel);
            Refresh(mollierModel);
        }

        public void Refresh(MollierModel mollierModel = null)
        {
            this.mollierModel = mollierModel;
            regenerateDataGridViews();
        }

        #region Initialization
        private void initializeDataGridViews(MollierModel mollierModel)
        {
            PressurePoints_TextBox.Text = mollierControlSettings.Pressure.ToString();
            PressureProcesses_TextBox.Text = mollierControlSettings.Pressure.ToString();

            // Initializing Air flow (m3/s is the first item)
            SupplyAirflow_ComboBox.SelectedIndex = 0;
            ExhaustAirflow_Combobox.SelectedIndex = 0;

            // Initializing groups selecting
            GroupSelectionProcesses_ComboBox.Items.Add(defaultGroup);
            GroupSelectionProcesses_ComboBox.SelectedItem = GroupSelectionProcesses_ComboBox.Items[0];
            GroupSelectionPoints_ComboBox.Items.Add(defaultGroup);
            GroupSelectionPoints_ComboBox.SelectedItem = GroupSelectionPoints_ComboBox.Items[0];

            if (mollierModel == null)
            {
                return;
            }
            List<MollierGroup> mollierGroups = mollierModel.GetMollierObjects<MollierGroup>(false);
            mollierGroups?.ForEach(x =>
            {
                if (!string.IsNullOrEmpty(x.Name))
                {
                    GroupSelectionProcesses_ComboBox.Items.Add(x.Name);
                    GroupSelectionPoints_ComboBox.Items.Add(x.Name);
                }
            });
        }
        #endregion

        #region Regenerate Data Grid
        private void regenerateDataGridViews()
        {
            List<UIMollierPoint> uIMollierPoints = mollierModel?.GetMollierObjects<UIMollierPoint>();
            List<UIMollierProcess> uIMollierProcesses = mollierModel?.GetMollierObjects<UIMollierProcess>();
            List<MollierGroup> mollierGroups = mollierModel?.GetMollierObjects<MollierGroup>();

            regenerateDataGridView_Points(uIMollierPoints, mollierGroups);
            regenerateDataGridView_Processes(uIMollierProcesses, mollierGroups);
        }

        private void regenerateDataGridView_Points(List<UIMollierPoint> mollierPoints, List<MollierGroup> mollierGroups)
        {
            if (mollierPoints == null)
            {
                return;
            }
            string actualGroup = (string)GroupSelectionPoints_ComboBox?.SelectedItem;
            List<DisplayUIMollierObject> dataGridViewElements = new List<DisplayUIMollierObject>();

            foreach (UIMollierPoint uIMollierPoint in mollierPoints)
            {
                string name = Query.GroupName(uIMollierPoint, mollierGroups);
                if (actualGroup != defaultGroup && name != actualGroup)
                {
                    continue;
                }
                dataGridViewElements.Add(new DisplayUIMollierObject(uIMollierPoint));
            }

            DataGridView_MollierPoints.ItemsSource = dataGridViewElements;
        }

        private void regenerateDataGridView_Processes(List<UIMollierProcess> mollierProcesses, List<MollierGroup> mollierGroups)
        {
            if (mollierProcesses == null)
            {
                return;
            }

            mollierProcesses = mollierProcesses.SortByGroup().ConvertAll(x => (UIMollierProcess)x);
            string actualGroup = (string)GroupSelectionProcesses_ComboBox?.SelectedItem;
            List<DisplayUIMollierObject> dataGridViewElements = new List<DisplayUIMollierObject>();

            foreach (UIMollierProcess uIMollierProcess in mollierProcesses)
            {
                string name = Query.GroupName(uIMollierProcess, mollierGroups);
                if (actualGroup != defaultGroup && name != actualGroup)
                {
                    continue;
                }
                dataGridViewElements.Add(new DisplayUIMollierObject(uIMollierProcess, 0, airflow, airFlowUnit));
                dataGridViewElements.Add(new DisplayUIMollierObject(uIMollierProcess, 1, airflow, airFlowUnit));
            }

            DataGridView_MollierProcesses.ItemsSource = dataGridViewElements;
        }
        #endregion

        #region Air Flow Selection
        private void SupplyAirFlow_TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!Core.Query.TryConvert(SupplyAirFlow_TextBox.Text, out double supplyAirFlow))
            {
                return;
            }

            airflow = supplyAirFlow;
            regenerateDataGridViews();
        }

        private void SupplyAirFlow_CheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            //For now there is no exhaust airflow but there'll be imlemented switching between ariflows
            SupplyAirFlow_CheckBox.IsChecked = true;
            return;
        }

        private void SupplyAirflow_ComboBox_SelectedIndexChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SupplyAirFlow_CheckBox.IsChecked == true)
            {
                // Original WinForms read SupplyAirFlow_CheckBox.Text here (preserved verbatim).
                switch (SupplyAirFlow_CheckBox.Content as string)
                {
                    case "m3/s":
                        airFlowUnit = Units.UnitType.CubicMeterPerSecond;
                        break;
                    case "m3/h":
                        airFlowUnit = Units.UnitType.CubicMeterPerHour;
                        break;
                }
            }
        }
        #endregion

        #region Group Selection
        private void GroupSelectionPoints_ComboBox_SelectedValueChanged(object sender, SelectionChangedEventArgs e)
        {
            List<UIMollierPoint> uIMollierPoints = mollierModel?.GetMollierObjects<UIMollierPoint>();
            List<MollierGroup> mollierGroups = mollierModel?.GetMollierObjects<MollierGroup>();

            if (mollierModel == null || uIMollierPoints == null)
            {
                return;
            }
            string selectedGroup = GroupSelectionPoints_ComboBox.SelectedItem.ToString();

            // Change visibility of points from different groups
            foreach (UIMollierPoint uIMollierPoint in uIMollierPoints)
            {
                string groupName = Query.GroupName(uIMollierPoint, mollierGroups);
                if (selectedGroup == defaultGroup || selectedGroup == groupName)
                {
                    uIMollierPoint.UIMollierAppearance.Visible = true;
                }
                else
                {
                    uIMollierPoint.UIMollierAppearance.Visible = false;
                }
            }
            editObject();

            regenerateDataGridView_Points(uIMollierPoints, mollierGroups);
        }

        private void GroupSelectionProcesses_ComboBox_SelectedValueChanged(object sender, SelectionChangedEventArgs e)
        {
            List<UIMollierProcess> uIMollierProcesses = mollierModel?.GetMollierObjects<UIMollierProcess>();
            List<MollierGroup> mollierGroups = mollierModel?.GetMollierObjects<MollierGroup>();

            if (mollierModel == null || uIMollierProcesses == null)
            {
                return;
            }
            string selectedGroup = GroupSelectionProcesses_ComboBox.SelectedItem.ToString();

            // Change visibility of processes from different groups
            foreach (UIMollierProcess uIMollierProcess in uIMollierProcesses)
            {
                string groupName = Query.GroupName(uIMollierProcess, mollierGroups);
                if (selectedGroup == defaultGroup || selectedGroup == groupName)
                {
                    uIMollierProcess.UIMollierAppearance.Visible = true;
                }
                else
                {
                    uIMollierProcess.UIMollierAppearance.Visible = false;
                }
            }

            editObject();
            regenerateDataGridView_Processes(uIMollierProcesses, mollierGroups);
        }
        #endregion

        #region Object Edited
        private void ToolStripMenuItem_Edit_Click(object sender, RoutedEventArgs e)
        {
            DisplayUIMollierObject displayUIMollierObject = SelectedDisplayObject();
            if (displayUIMollierObject == null)
            {
                return;
            }

            displayUIMollierObject.UIMollierObject.Update();
            editObject();
        }

        private void editObject()
        {
            MollierModelEditedEventArgs mollierModelEditedEventArgs = new MollierModelEditedEventArgs(mollierModel);
            MollierModelEdited.Invoke(this, mollierModelEditedEventArgs);
            regenerateDataGridViews();
        }
        #endregion

        #region Object Removed
        private void ToolStripMenuItem_Remove_Click(object sender, RoutedEventArgs e)
        {
            DisplayUIMollierObject displayUIMollierObject = SelectedDisplayObject();
            if (displayUIMollierObject == null)
            {
                return;
            }

            removeObject(displayUIMollierObject.UIMollierObject);
        }

        private void removeObject(IUIMollierObject mollierObject)
        {
            if (mollierObject == null)
            {
                return;
            }

            MessageBoxResult confirmResult = MessageBox.Show("Are you sure to delete this item ?", "Delete Confirmation",
                                     MessageBoxButton.YesNo);
            if (confirmResult == MessageBoxResult.No)
            {
                return;
            }

            mollierModel.Remove(mollierObject);
            MollierModelEditedEventArgs mollierModelEditedEventArgs = new MollierModelEditedEventArgs(mollierModel);
            MollierModelEdited.Invoke(this, mollierModelEditedEventArgs);
            regenerateDataGridViews();
        }
        #endregion

        #region Cells Selection
        private void VisibleCheckBox_Click(object sender, RoutedEventArgs e)
        {
            DisplayUIMollierObject displayUIMollierObject = (sender as FrameworkElement)?.DataContext as DisplayUIMollierObject;
            if (displayUIMollierObject?.UIMollierObject == null)
            {
                return;
            }

            displayUIMollierObject.UIMollierObject.UIMollierAppearance.Visible = !displayUIMollierObject.UIMollierObject.UIMollierAppearance.Visible;
            editObject();
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            DisplayUIMollierObject displayUIMollierObject = (sender as FrameworkElement)?.DataContext as DisplayUIMollierObject;
            if (displayUIMollierObject?.UIMollierObject == null)
            {
                return;
            }

            displayUIMollierObject.UIMollierObject.Update();
            editObject();
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            DisplayUIMollierObject displayUIMollierObject = (sender as FrameworkElement)?.DataContext as DisplayUIMollierObject;
            if (displayUIMollierObject?.UIMollierObject == null)
            {
                return;
            }

            removeObject(displayUIMollierObject.UIMollierObject);
        }

        private void DataGridView_MollierPoints_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DataGridView_SelectionChanged(DataGridView_MollierPoints);
        }

        private void DataGridView_MollierProcesses_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DataGridView_SelectionChanged(DataGridView_MollierProcesses);
        }

        private void DataGridView_SelectionChanged(DataGrid dataGrid)
        {
            DisplayUIMollierObject displayUIMollierObject = dataGrid?.SelectedItem as DisplayUIMollierObject;
            if (displayUIMollierObject?.UIMollierObject == null)
            {
                return;
            }

            MollierObjectSelectedArgs mollierObjectSelectedArgs = new MollierObjectSelectedArgs(displayUIMollierObject.UIMollierObject);
            MollierObjectSelected?.Invoke(this, mollierObjectSelectedArgs);
        }
        #endregion

        private DisplayUIMollierObject SelectedDisplayObject()
        {
            DataGrid dataGrid = customizeMollierObjectsTabControl.SelectedIndex == 0
                ? DataGridView_MollierPoints
                : DataGridView_MollierProcesses;
            return dataGrid?.SelectedItem as DisplayUIMollierObject;
        }
    }
}
