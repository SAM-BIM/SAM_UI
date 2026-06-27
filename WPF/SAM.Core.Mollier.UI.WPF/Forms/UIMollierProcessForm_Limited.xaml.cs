// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Windows;
using SAM.Geometry.Mollier;

namespace SAM.Core.Mollier.UI.Forms
{
    /// <summary>WPF port of the WinForms UIMollierProcessForm_Limited (process customise dialog).</summary>
    public partial class UIMollierProcessForm_Limited : Window
    {
        /// <summary>True when closed via OK (modeless-safe replacement for WPF DialogResult).</summary>
        public bool DialogOk { get; private set; }

        public UIMollierProcessForm_Limited()
        {
            InitializeComponent();
        }

        public UIMollierProcessForm_Limited(UIMollierProcess uIMollierProcess)
        {
            InitializeComponent();
            if (uIMollierProcess == null)
            {
                return;
            }
            customizeProcessControl.UIMollierProcess = uIMollierProcess;
        }

        public UIMollierProcess UIMollierProcess
        {
            get { return customizeProcessControl.UIMollierProcess; }
            set { customizeProcessControl.UIMollierProcess = value; }
        }

        private void OK_Button_Click(object sender, RoutedEventArgs e)
        {
            DialogOk = true;
            Close();
        }

        private void Cancel_Button_Click(object sender, RoutedEventArgs e)
        {
            DialogOk = false;
            Close();
        }
    }
}
