// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.UI.WPF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// WPF replacement for the WinForms SAM.Analytical.Windows.Forms.SpaceForm (+ SpaceControl):
    /// edits a single Space (editable Name, read-only Guid, its InternalCondition with
    /// Modify/Remove, custom parameters, and Occupancy). Modify Internal Condition opens the WPF
    /// <see cref="InternalConditionWindow"/>; Occupancy opens the WPF <see cref="OccupancyWindow"/>.
    /// Needs the AnalyticalModel for the internal-condition editor's profile-library/adjacency
    /// look-ups. Mirrors the original surface (the Space get/set). F12 opens the JSON inspector.
    /// </summary>
    public partial class SpaceWindow : System.Windows.Window
    {
        private Space space;
        private readonly AnalyticalModel analyticalModel;
        private readonly HashSet<Enum> enums;

        public SpaceWindow()
        {
            InitializeComponent();
        }

        public SpaceWindow(Space space, AnalyticalModel analyticalModel, IEnumerable<Enum> enums = null)
        {
            InitializeComponent();

            this.analyticalModel = analyticalModel;

            if (enums != null)
            {
                this.enums = new HashSet<Enum>(enums);
            }

            SetSpace(space);
        }

        public Space Space
        {
            get
            {
                return GetSpace();
            }

            set
            {
                SetSpace(value);
            }
        }

        private void SetSpace(Space space)
        {
            this.space = space == null ? null : new Space(space);

            TextBox_Name.Text = this.space?.Name;
            TextBox_Guid.Text = this.space?.Guid.ToString();
            TextBox_InternalCondition.Text = this.space?.InternalCondition?.Name;

            ParametersControl_Main.CustomParameters = this.space == null
                ? null
                : SAM.Core.UI.Create.CustomParameters(this.space, enums?.ToArray());
        }

        private Space GetSpace()
        {
            if (space == null)
            {
                return null;
            }

            Space result = new Space(space, TextBox_Name.Text, space.Location);

            SAM.Core.UI.CustomParameters customParameters = ParametersControl_Main.CustomParameters;

            SAM.Core.UI.Modify.SetValues(result, customParameters);

            return result;
        }

        private void Button_ModifyInternalCondition_Click(object sender, RoutedEventArgs e)
        {
            Space space = GetSpace();
            if (space == null)
            {
                return;
            }

            InternalConditionWindow internalConditionWindow = new InternalConditionWindow(analyticalModel, space) { Owner = this };
            if (internalConditionWindow.ShowDialog() != true)
            {
                return;
            }

            Space = internalConditionWindow.Space;
        }

        private void Button_RemoveInternalCondition_Click(object sender, RoutedEventArgs e)
        {
            if (space == null)
            {
                return;
            }

            Space space_Temp = new Space(space);
            space_Temp.InternalCondition = null;
            Space = space_Temp;
        }

        private void Button_Occupancy_Click(object sender, RoutedEventArgs e)
        {
            Space space = GetSpace();
            if (space == null)
            {
                return;
            }

            OccupancyWindow occupancyWindow = new OccupancyWindow { Owner = this, Space = space };
            if (occupancyWindow.ShowDialog() != true)
            {
                return;
            }

            Space = occupancyWindow.Space;
        }

        private void Button_OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            Space.JsonForm(this, e);
        }
    }
}
