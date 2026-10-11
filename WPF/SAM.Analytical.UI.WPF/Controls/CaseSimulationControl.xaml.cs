// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using Microsoft.Win32;
using SAM.Analytical.Tas;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// Interaction logic for CaseSimulationControl.xaml
    /// </summary>
    public partial class CaseSimulationControl : System.Windows.Controls.UserControl
    {
        private string? directory = null;

        private WorkflowSettings workflowSettings = Query.DefaultWorkflowSettings();

        public CaseSimulationControl()
        {
            InitializeComponent();
        }

        public string? Directory
        {
            get
            {
                return directory;
            }
            set
            {
                directory = value;
                TextBox_Directory.Text = directory;

                //TextBox_Directory.Focus();
                //TextBox_Directory.CaretIndex = TextBox_Directory.Text.Length;
                //TextBox_Directory.ScrollToEnd();

            }
        }

        public bool Parallel
        {
            get
            {
                return checkBox_Parallel.IsChecked.HasValue && checkBox_Parallel.IsChecked.Value;
            }
            set
            {
                checkBox_Parallel.IsChecked = value;
            }
        }

        public WorkflowSettings WorkflowSettings
        {
            get
            {
                if(workflowSettings is null)
                {
                    workflowSettings = Query.DefaultWorkflowSettings();
                }

                workflowSettings.AddIZAMs = checkBox_AddIZAMs.IsChecked.Value;
                workflowSettings.Sizing = checkBox_Sizing.IsChecked.Value;
                workflowSettings.Simulate = checkBox_Simulate.IsChecked.Value;
                workflowSettings.UseWidths = checkBox_UseBEThickness.IsChecked.Value;
                workflowSettings.UnmetHours = checkBox_RunUnmetHours.IsChecked.Value;
                workflowSettings.RemoveExistingTBD = checkBox_RemoveTBD.IsChecked.Value;
                workflowSettings.T3DRoute = checkBox_DirectT3D.IsChecked == true ? T3DRoute.Direct : T3DRoute.GbXML;
               
                return workflowSettings;
            }

            set
            {
                if(value == null)
                {
                    return;
                }

                workflowSettings = value;
                if(workflowSettings is not null)
                {
                    checkBox_AddIZAMs.IsChecked = workflowSettings.AddIZAMs;
                    // Before the first read of the WorkflowSettings getter below: that getter writes every box back into
                    // workflowSettings, so a box not yet updated would overwrite the route just supplied.
                    checkBox_DirectT3D.IsChecked = workflowSettings.T3DRoute == T3DRoute.Direct;
                    checkBox_Sizing.IsChecked = WorkflowSettings.Sizing;
                    checkBox_Simulate.IsChecked = WorkflowSettings.Simulate;
                    checkBox_UseBEThickness.IsChecked = WorkflowSettings.UseWidths;
                    checkBox_RunUnmetHours.IsChecked = WorkflowSettings.UnmetHours;
                    checkBox_RemoveTBD.IsChecked = WorkflowSettings.RemoveExistingTBD;
                }
            }
        }

        private void button_Browse_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            OpenFolderDialog openFolderDialog = new();
            bool? dialogResult = openFolderDialog.ShowDialog();
            if (dialogResult == null || !dialogResult.HasValue || !dialogResult.Value)
            {
                return;
            }

            directory = openFolderDialog.FolderName;
            TextBox_Directory.Text = directory;

            //TextBox_Directory.Focus();
            //TextBox_Directory.CaretIndex = TextBox_Directory.Text.Length;
            //TextBox_Directory.ScrollToEnd();

        }
    }
}
