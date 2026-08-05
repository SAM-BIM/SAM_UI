// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.Windows.Controls;

namespace SAM.Analytical.UI.WPF
{
    /// <summary>
    /// Interaction logic for PartFVectilationControl.xaml
    /// </summary>
    public partial class PartFVectilationControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PartFVectilationControl"/> class.
        /// </summary>
        public PartFVectilationControl()
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
                return comboBox_ZoneCategory.SelectedItem as string;
            }
        }

        /// <summary>
        /// Gets or sets the list of zone categories.
        /// </summary>
        public List<string>? ZoneCategories
        {
            get
            {
                List<string> result = [];
                foreach (object @object in comboBox_ZoneCategory.Items)
                {
                    if (@object is string string_Item)
                    {
                        result.Add(string_Item);
                    }
                }

                return result;
            }
            set
            {
                string? value_Temp = comboBox_ZoneCategory.SelectedItem as string;
                comboBox_ZoneCategory.Items.Clear();
                if (value != null)
                {
                    foreach (string value_New in value)
                    {
                        comboBox_ZoneCategory.Items.Add(value_New);
                    }
                }

                if (!string.IsNullOrWhiteSpace(value_Temp))
                {
                    comboBox_ZoneCategory.SelectedItem = value_Temp;
                }
                else if (comboBox_ZoneCategory.Items.Count != 0)
                {
                    comboBox_ZoneCategory.SelectedItem = comboBox_ZoneCategory.Items[0];
                }
            }
        }
    }
}
