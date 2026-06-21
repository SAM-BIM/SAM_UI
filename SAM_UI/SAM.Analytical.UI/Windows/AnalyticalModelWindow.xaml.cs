// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.UI
{
    // In this namespace the bare name "Window" resolves to SAM.Analytical.Window (a building
    // element), so alias it (at namespace scope) to the WPF Window used for dialogs.
    using Window = System.Windows.Window;

    /// <summary>
    /// WPF replacement for the WinForms SAM.Analytical.Windows.Forms.AnalyticalModelForm: edits an
    /// AnalyticalModel's name/description plus a custom-parameter grid. Mirrors the legacy public
    /// surface (constructor + read-only AnalyticalModel getter).
    /// </summary>
    public partial class AnalyticalModelWindow : Window
    {
        private AnalyticalModel analyticalModel;
        private HashSet<Enum> enums;

        public AnalyticalModelWindow()
        {
            InitializeComponent();
        }

        public AnalyticalModelWindow(AnalyticalModel analyticalModel, IEnumerable<Enum> enums = null)
        {
            InitializeComponent();

            this.analyticalModel = analyticalModel;

            if (enums != null)
            {
                this.enums = new HashSet<Enum>(enums);
            }

            Load();
        }

        private void Load()
        {
            TextBox_Name.Text = analyticalModel?.Name;
            TextBox_Description.Text = analyticalModel?.Description;
            TextBox_Guid.Text = analyticalModel?.Guid.ToString();

            ParametersControl_Main.CustomParameters = analyticalModel == null ? null : SAM.Core.UI.Create.CustomParameters(analyticalModel, enums?.ToArray());
        }

        public AnalyticalModel AnalyticalModel
        {
            get
            {
                if (analyticalModel == null)
                {
                    return null;
                }

                AnalyticalModel result = new AnalyticalModel(TextBox_Name.Text, TextBox_Description.Text, analyticalModel.Location, analyticalModel.Address, analyticalModel.AdjacencyCluster, analyticalModel.MaterialLibrary, analyticalModel.ProfileLibrary);

                SAM.Core.UI.Modify.SetValues(result, ParametersControl_Main.CustomParameters);

                return result;
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
