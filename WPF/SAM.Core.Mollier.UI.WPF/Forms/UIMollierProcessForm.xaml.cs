// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Windows;
using SAM.Core.Mollier.UI.Controls;
using SAM.Geometry.Mollier;

namespace SAM.Core.Mollier.UI
{
    /// <summary>WPF port of the WinForms UIMollierProcessForm (process edit dialog; shown modeless by MollierControl.Edit).</summary>
    public partial class UIMollierProcessForm : Window
    {
        /// <summary>True when closed via OK (modeless-safe replacement for WPF DialogResult).</summary>
        public bool DialogOk { get; private set; }

        public UIMollierProcessForm()
        {
            InitializeComponent();
        }

        public UIMollierProcessForm(UIMollierProcess uIMollierProcess)
        {
            InitializeComponent();

            UIMollierProcess = uIMollierProcess;
        }

        public UIMollierProcess UIMollierProcess
        {
            get
            {
                if (UIMollierProcessControl_Main == null)
                {
                    return null;
                }

                return UIMollierProcessControl_Main.UIMollierProcess;
            }
            set
            {
                if (UIMollierProcessControl_Main != null)
                {
                    UIMollierProcessControl_Main.UIMollierProcess = value;
                }
            }
        }

        public Controls.MollierControl MollierControl
        {
            get { return UIMollierProcessControl_Main.MollierControl; }
            set
            {
                UIMollierProcessControl_Main.MollierControl = value;
                Button_Apply.Visibility = value != null ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void Cancel_Button_Click(object sender, RoutedEventArgs e)
        {
            DialogOk = false;
            Close();
        }

        private void OK_Button_Click(object sender, RoutedEventArgs e)
        {
            DialogOk = true;
            Close();
        }

        private void UIMollierProcessForm_Load(object sender, RoutedEventArgs e)
        {
            UIMollierProcessControl_Main.MollierPointSelected += UIMollierProcessControl_Main_MollierPointSelected;
            UIMollierProcessControl_Main.MollierPointSelecting += UIMollierProcessControl_Main_MollierPointSelecting;
        }

        private void UIMollierProcessControl_Main_MollierPointSelecting(object sender, EventArgs e)
        {
            Hide();
        }

        private void UIMollierProcessControl_Main_MollierPointSelected(object sender, MollierPointSelectedEventArgs e)
        {
            Show();
        }

        private void Button_Apply_Click(object sender, RoutedEventArgs e)
        {
            UIMollierProcessControl_Main.MollierControl.Apply(this);
        }
    }
}
