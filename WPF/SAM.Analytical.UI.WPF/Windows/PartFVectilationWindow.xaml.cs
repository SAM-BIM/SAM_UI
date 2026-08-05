// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.Windows;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// Interaction logic for PartFVectilationWindow.xaml
    /// </summary>
    public partial class PartFVectilationWindow : System.Windows.Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PartFVectilationWindow"/> class.
        /// </summary>
        public PartFVectilationWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets the selected zone category.
        /// </summary>
        public string? SelectedZoneCategory
        {
            get
            {
                return partFVectilationControl.SelectedZoneCategory;
            }
        }

        /// <summary>
        /// Gets or sets the list of zone categories.
        /// </summary>
        public List<string>? ZoneCategories
        {
            get
            {
                return partFVectilationControl.ZoneCategories;
            }
            set
            {
                partFVectilationControl.ZoneCategories = value;
            }
        }

        private void button_OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
