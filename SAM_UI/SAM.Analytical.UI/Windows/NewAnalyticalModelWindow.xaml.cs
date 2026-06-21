// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using System;

namespace SAM.Analytical.UI
{
    // In this namespace the bare name "Window" resolves to SAM.Analytical.Window (a building
    // element), so alias it (at namespace scope) to the WPF Window used for dialogs.
    using Window = System.Windows.Window;

    /// <summary>
    /// WPF replacement for the WinForms SAM.Analytical.Windows.Forms.NewAnalyticalModelForm: prompts
    /// for a project name and an optional template, and builds the AnalyticalModel (importing the
    /// template via the merging Query.Import when one is chosen).
    /// </summary>
    public partial class NewAnalyticalModelWindow : Window
    {
        private class TemplateItem
        {
            public string Name { get; set; }
            public string Path { get; set; }
        }

        private readonly string templatesDirectory;

        public NewAnalyticalModelWindow(string analyticalModelName = null, string templatesDirectory = null)
        {
            InitializeComponent();

            TextBox_ProjectName.Text = analyticalModelName;
            this.templatesDirectory = string.IsNullOrEmpty(templatesDirectory) ? Core.Query.TemplatesDirectory<AnalyticalModel>() : templatesDirectory;

            Loaded += NewAnalyticalModelWindow_Loaded;
        }

        private void NewAnalyticalModelWindow_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            ComboBox_Template.Items.Add(new TemplateItem { Name = "<none>", Path = null });
            ComboBox_Template.SelectedIndex = 0;

            if (!string.IsNullOrWhiteSpace(templatesDirectory) && System.IO.Directory.Exists(templatesDirectory))
            {
                string[] paths = System.IO.Directory.GetFiles(templatesDirectory);
                if (paths != null)
                {
                    foreach (string path in paths)
                    {
                        ComboBox_Template.Items.Add(new TemplateItem { Name = System.IO.Path.GetFileNameWithoutExtension(path), Path = path });
                    }
                }
            }
        }

        public AnalyticalModel GetAnalyticalModel()
        {
            AnalyticalModel result = new AnalyticalModel(Guid.NewGuid(), TextBox_ProjectName.Text);

            string path = (ComboBox_Template.SelectedItem as TemplateItem)?.Path;
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            {
                return result;
            }

            return result.Import<IJSAMObject>(path, (Func<IJSAMObject, bool>)null, new ImportOptions(), this);
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
