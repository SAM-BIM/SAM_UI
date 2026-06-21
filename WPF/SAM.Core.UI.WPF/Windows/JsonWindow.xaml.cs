// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace SAM.Core.UI.WPF
{
    /// <summary>
    /// WPF replacement for the WinForms SAM.Core.Windows.Forms.JsonForm: a read-out of the JSON
    /// representation of one or more IJSAMObjects (hosted in a JsonControl), with Save / Copy /
    /// Close. Used as the F12 inspector via the Query.JsonForm bridge.
    /// </summary>
    public partial class JsonWindow : Window
    {
        private readonly List<IJSAMObject> jSAMObjects;

        public JsonWindow()
        {
            InitializeComponent();
        }

        public JsonWindow(IEnumerable<IJSAMObject> jSAMObjects)
        {
            InitializeComponent();

            if (jSAMObjects != null)
            {
                this.jSAMObjects = jSAMObjects.Where(x => x != null).ToList();
            }

            JsonControl_Main.Text = Core.Convert.ToString(this.jSAMObjects) ?? string.Empty;
        }

        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            if (jSAMObjects == null)
            {
                return;
            }

            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "json files (*.json)|*.json|All files (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true
            };

            if (saveFileDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(saveFileDialog.FileName))
            {
                return;
            }

            Core.Convert.ToFile(jSAMObjects, saveFileDialog.FileName);
        }

        private void Button_Copy_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(JsonControl_Main.Text ?? string.Empty);
        }

        private void Button_Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
