// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Windows;
using SAM.Core.Mollier.UI.Controls;
using SAM.Geometry.Mollier;

namespace SAM.Core.Mollier.UI.Forms
{
    /// <summary>WPF port of the WinForms UIMollierPointForm (point edit dialog; shown modeless by MollierControl.Edit).</summary>
    public partial class UIMollierPointForm : Window
    {
        /// <summary>True when closed via OK (modeless-safe replacement for WPF DialogResult).</summary>
        public bool DialogOk { get; private set; }

        public UIMollierPointForm()
        {
            InitializeComponent();
        }

        public UIMollierPointForm(UIMollierPoint uIMollierPoint)
        {
            InitializeComponent();

            if (uIMollierPoint == null)
            {
                return;
            }
            CustomizePointControl_Main.UIMollierPoint = uIMollierPoint;
        }

        public UIMollierPoint UIMollierPoint
        {
            get { return CustomizePointControl_Main.UIMollierPoint; }
            set { CustomizePointControl_Main.UIMollierPoint = value; }
        }

        public MollierControl MollierControl
        {
            get { return CustomizePointControl_Main?.MollierControl; }
            set
            {
                CustomizePointControl_Main.MollierControl = value;
                Button_Apply.Visibility = value != null ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void Cancel_Button_Click(object sender, RoutedEventArgs e)
        {
            DialogOk = false;
            Close();
        }

        private void CustomizePointForm_Load(object sender, RoutedEventArgs e)
        {
            CustomizePointControl_Main.MollierPointSelected += CustomizePointControl_Main_MollierPointSelected;
            CustomizePointControl_Main.MollierPointSelecting += CustomizePointControl_Main_MollierPointSelecting;
        }

        private void CustomizePointControl_Main_MollierPointSelecting(object sender, EventArgs e)
        {
            Hide();
        }

        private void CustomizePointControl_Main_MollierPointSelected(object sender, MollierPointSelectedEventArgs e)
        {
            Show();
        }

        private void OK_Button_Click(object sender, RoutedEventArgs e)
        {
            DialogOk = true;
            Close();
        }

        private void Button_Apply_Click(object sender, RoutedEventArgs e)
        {
            CustomizePointControl_Main.MollierControl.Apply(this);
        }
    }
}
