// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Windows;

namespace SAM.Core.Mollier.UI.Forms
{
    /// <summary>WPF port of the WinForms OpenJSONForm (merge/replace chooser dialog).</summary>
    public partial class OpenJSONForm : Window
    {
        public enum Action { Undefined, Replace, Merge }

        private Action action = Action.Undefined;

        /// <summary>True when closed via Merge or Replace. Replaces WinForms DialogResult.</summary>
        public bool DialogOk { get; private set; }

        public Action GetAction()
        {
            return action;
        }

        public OpenJSONForm()
        {
            InitializeComponent();
        }

        private void MergeButton_Click(object sender, RoutedEventArgs e)
        {
            DialogOk = true;
            action = Action.Merge;
            Close();
        }

        private void ReplaceButton_Click(object sender, RoutedEventArgs e)
        {
            DialogOk = true;
            action = Action.Replace;
            Close();
        }
    }
}
