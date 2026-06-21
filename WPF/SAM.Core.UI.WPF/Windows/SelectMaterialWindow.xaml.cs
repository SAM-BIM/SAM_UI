// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace SAM.Core.UI.WPF
{
    /// <summary>
    /// WPF replacement for the WinForms SAM.Core.Windows.Forms.SelectMaterialForm: a searchable
    /// single-selection material picker (search box + read-only list, OK/Cancel or double-click).
    /// Mirrors the original public API (constructor, the Material property and the SearchText
    /// property). The WinForms form also showed a read-only material-property preview pane via
    /// MaterialControl; that preview is intentionally omitted - selection is by name.
    /// </summary>
    public partial class SelectMaterialWindow : Window
    {
        private readonly List<IMaterial> materials;
        private readonly ObservableCollection<Row> rows = new ObservableCollection<Row>();

        public SelectMaterialWindow()
        {
            InitializeComponent();
            DataGrid_Materials.ItemsSource = rows;
        }

        public SelectMaterialWindow(IEnumerable<IMaterial> materials, IEnumerable<Enum> enums = null)
        {
            InitializeComponent();
            DataGrid_Materials.ItemsSource = rows;

            this.materials = materials?.Where(x => x != null).ToList();
            this.materials?.Sort((x, y) => string.Compare(x.Name, y.Name, StringComparison.Ordinal));

            FillRows(null);
        }

        private void FillRows(string search)
        {
            rows.Clear();

            if (materials == null || materials.Count == 0)
            {
                return;
            }

            List<IMaterial> materials_Temp = materials;
            if (!string.IsNullOrWhiteSpace(search))
            {
                materials_Temp = materials.Search(search, (IMaterial material) => material?.Name);
            }

            if (materials_Temp == null)
            {
                return;
            }

            foreach (IMaterial material in materials_Temp)
            {
                if (material != null)
                {
                    rows.Add(new Row(material));
                }
            }
        }

        private void TextBox_Search_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            FillRows(TextBox_Search.Text);
        }

        public IMaterial Material
        {
            get
            {
                return (DataGrid_Materials.SelectedItem as Row)?.Material;
            }
        }

        public string SearchText
        {
            get
            {
                return TextBox_Search.Text;
            }

            set
            {
                TextBox_Search.Text = value;
            }
        }

        private void DataGrid_Materials_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (Material == null)
            {
                return;
            }

            DialogResult = true;
            Close();
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

        private sealed class Row
        {
            public Row(IMaterial material)
            {
                Material = material;
                Name = material?.Name;

                if (material is Material material_Temp)
                {
                    Description = material_Temp.Description;
                    MaterialType = material_Temp.MaterialType().ToString();
                }
            }

            public IMaterial Material { get; }

            public string Name { get; }

            public string Description { get; }

            public string MaterialType { get; }
        }
    }
}
