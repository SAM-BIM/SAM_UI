// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SAM.Core.UI.WPF
{
    /// <summary>
    /// WPF replacement for the WinForms SAM.Core.Windows.Forms.MaterialLibraryForm: a searchable
    /// material list manager (add / remove / duplicate / import / export, edit on double-click).
    /// Mirrors the original public API (constructor, the MaterialLibrary property, and the
    /// import/export events) so hosts can override import/export with their own logic.
    /// </summary>
    public partial class MaterialLibraryWindow : Window
    {
        public event EventHandler<MaterialLibraryExportingEventArgs> MaterialLibraryExporting;
        public event EventHandler<MaterialLibraryImportingEventArgs> MaterialLibraryImporting;

        private MaterialLibrary materialLibrary;
        private HashSet<Enum> enums;
        private IMaterial material_Selected;

        private readonly ObservableCollection<Row> rows = new ObservableCollection<Row>();

        public MaterialLibraryWindow()
        {
            InitializeComponent();
            DataGrid_Materials.ItemsSource = rows;
        }

        public MaterialLibraryWindow(MaterialLibrary materialLibrary, IEnumerable<Enum> enums = null, IMaterial material_Selected = null)
        {
            InitializeComponent();
            DataGrid_Materials.ItemsSource = rows;

            if (materialLibrary != null)
            {
                this.materialLibrary = new MaterialLibrary(materialLibrary);
            }

            this.material_Selected = material_Selected;
            if (enums != null)
            {
                this.enums = new HashSet<Enum>(enums);
            }

            SetMaterialLibrary(this.materialLibrary);
        }

        private void SetMaterialLibrary(MaterialLibrary materialLibrary)
        {
            this.materialLibrary = materialLibrary ?? new MaterialLibrary("Material Library");

            FillRows(null);

            if (material_Selected != null)
            {
                string uniqueId = this.materialLibrary.GetUniqueId(material_Selected);
                Row row = rows.FirstOrDefault(x => this.materialLibrary.GetUniqueId(x.Material) == uniqueId);
                if (row != null)
                {
                    DataGrid_Materials.SelectedItem = row;
                }
            }
        }

        private void FillRows(string search)
        {
            rows.Clear();

            List<IMaterial> materials = materialLibrary?.GetMaterials();
            if (materials == null || materials.Count == 0)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                materials = materials.Search(search, (IMaterial material) => material?.Name);
            }

            if (materials == null)
            {
                return;
            }

            foreach (IMaterial material in materials)
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

        public MaterialLibrary MaterialLibrary
        {
            get
            {
                return new MaterialLibrary(materialLibrary);
            }
        }

        private void DataGrid_Materials_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Row row = DataGrid_Materials.SelectedItem as Row;
            IMaterial material = row?.Material;
            if (material == null)
            {
                return;
            }

            MaterialWindow materialWindow = new MaterialWindow(material, enums?.ToList()) { Owner = this };
            if (materialWindow.ShowDialog() != true)
            {
                return;
            }

            material = materialWindow.Material;
            if (material == null)
            {
                return;
            }

            materialLibrary.Add(material);

            FillRows(TextBox_Search.Text);
        }

        private void Button_Add_Click(object sender, RoutedEventArgs e)
        {
            MaterialWindow materialWindow = new MaterialWindow(null, enums?.ToList()) { Owner = this };
            if (materialWindow.ShowDialog() != true)
            {
                return;
            }

            IMaterial material = materialWindow.Material;
            if (material == null)
            {
                return;
            }

            materialLibrary?.Add(material);
            rows.Add(new Row(material));
        }

        private void Button_Remove_Click(object sender, RoutedEventArgs e)
        {
            List<Row> selected = DataGrid_Materials.SelectedItems.Cast<Row>().ToList();
            foreach (Row row in selected)
            {
                materialLibrary.Remove(row.Material);
                rows.Remove(row);
            }
        }

        private void Button_Duplicate_Click(object sender, RoutedEventArgs e)
        {
            Row row = DataGrid_Materials.SelectedItem as Row;
            IMaterial material = row?.Material;
            if (material == null)
            {
                return;
            }

            material = materialLibrary.Duplicate(material, this, enums?.ToList());
            if (material != null)
            {
                rows.Add(new Row(material));
            }
        }

        private void Button_Export_Click(object sender, RoutedEventArgs e)
        {
            MaterialLibrary materialLibrary = this.materialLibrary == null ? new MaterialLibrary(string.Empty) : new MaterialLibrary(this.materialLibrary);

            if (MaterialLibraryExporting != null)
            {
                MaterialLibraryExportingEventArgs materialLibraryExportingEventArgs = new MaterialLibraryExportingEventArgs { MaterialLibrary = materialLibrary };
                MaterialLibraryExporting.Invoke(this, materialLibraryExportingEventArgs);
                if (materialLibraryExportingEventArgs.Handled)
                {
                    return;
                }
            }

            Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "json files (*.json)|*.json|All files (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true,
                FileName = "SAM_MaterialLibrary_CustomVer00.json"
            };

            if (saveFileDialog.ShowDialog() != true)
            {
                return;
            }

            bool result = Core.Convert.ToFile(materialLibrary, saveFileDialog.FileName);
            MessageBox.Show(result ? "Library exported successfully." : "Library could not be exported.");
        }

        private void Button_Import_Click(object sender, RoutedEventArgs e)
        {
            List<IMaterial> materials = null;
            bool handled = false;

            if (MaterialLibraryImporting != null)
            {
                MaterialLibraryImportingEventArgs materialLibraryImportingEventArgs = new MaterialLibraryImportingEventArgs();
                MaterialLibraryImporting.Invoke(this, materialLibraryImportingEventArgs);
                if (materialLibraryImportingEventArgs.Handled)
                {
                    handled = true;
                    materials = materialLibraryImportingEventArgs.MaterialLibrary?.GetMaterials();
                }
            }

            if (!handled)
            {
                Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "json files (*.json)|*.json|All files (*.*)|*.*",
                    FilterIndex = 2,
                    RestoreDirectory = true
                };

                string directory = Core.Query.ResourcesDirectory();
                if (System.IO.Directory.Exists(directory))
                {
                    openFileDialog.InitialDirectory = directory;
                }

                if (openFileDialog.ShowDialog() != true)
                {
                    return;
                }

                List<IJSAMObject> sAMObjects = Core.Convert.ToSAM<IJSAMObject>(openFileDialog.FileName);
                if (sAMObjects == null || sAMObjects.Count == 0)
                {
                    MessageBox.Show("No materials to import");
                    return;
                }

                materials = new List<IMaterial>();
                foreach (IJSAMObject jSAMObject in sAMObjects)
                {
                    if (jSAMObject is IMaterial material)
                    {
                        materials.Add(material);
                    }
                    else if (jSAMObject is MaterialLibrary materialLibrary_Temp)
                    {
                        List<IMaterial> materials_Temp = materialLibrary_Temp.GetMaterials();
                        if (materials_Temp != null && materials_Temp.Count != 0)
                        {
                            materials.AddRange(materials_Temp);
                        }
                    }
                }
            }

            if (materials == null || materials.Count == 0)
            {
                MessageBox.Show("No materials to import.");
                return;
            }

            if (materialLibrary == null)
            {
                materialLibrary = new MaterialLibrary("MaterialLibrary");
            }

            materials.ForEach(x => materialLibrary.Add(x));

            SetMaterialLibrary(materialLibrary);

            MessageBox.Show("Materials imported successfully.");
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

                if (material is Material material_Temp)
                {
                    DisplayName = material_Temp.DisplayName;
                    Description = material_Temp.Description;
                    MaterialType = material_Temp.MaterialType().ToString();
                }

                Name = material?.Name;
            }

            public IMaterial Material { get; }

            public string DisplayName { get; }

            public string Name { get; }

            public string Description { get; }

            public string MaterialType { get; }
        }
    }
}
