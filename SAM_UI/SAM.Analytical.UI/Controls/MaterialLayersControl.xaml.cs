// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Architectural;
using SAM.Core;
using SAM.Core.UI.WPF;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace SAM.Analytical.UI
{
    /// <summary>
    /// WPF replacement for the WinForms SAM.Architectural.Windows.MaterialLayersControl: a layer
    /// editor (material name + thickness grid with add / remove / move up / move down). Materials
    /// are picked from the supplied library via the WPF SelectMaterialWindow. Mirrors the original
    /// public API (MaterialLayers and MaterialLibrary properties and the Enabled setter).
    /// </summary>
    public partial class MaterialLayersControl : UserControl
    {
        private MaterialLibrary materialLibrary;
        private bool editable = true;

        private readonly ObservableCollection<LayerRow> rows = new ObservableCollection<LayerRow>();

        public MaterialLayersControl()
        {
            InitializeComponent();
            DataGrid_Layers.ItemsSource = rows;
        }

        public MaterialLayersControl(MaterialLibrary materialLibrary)
            : this()
        {
            this.materialLibrary = materialLibrary;
            Button_Add.IsEnabled = materialLibrary != null;
        }

        private void Button_Up_Click(object sender, RoutedEventArgs e)
        {
            Move(true);
        }

        private void Button_Down_Click(object sender, RoutedEventArgs e)
        {
            Move(false);
        }

        private void Button_Add_Click(object sender, RoutedEventArgs e)
        {
            IMaterial material = materialLibrary.Material(null, System.Windows.Window.GetWindow(this));
            if (material == null)
            {
                return;
            }

            if (!material.TryGetValue(Core.MaterialParameter.DefaultThickness, out double thickness) || double.IsNaN(thickness) || thickness <= 0)
            {
                thickness = 0.1;
            }

            Add(material.Name, thickness);
        }

        private void Button_Remove_Click(object sender, RoutedEventArgs e)
        {
            foreach (LayerRow layerRow in DataGrid_Layers.SelectedItems.Cast<LayerRow>().ToList())
            {
                rows.Remove(layerRow);
            }
        }

        private void DataGrid_Layers_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!editable)
            {
                return;
            }

            if (DataGrid_Layers.CurrentColumn != Column_MaterialName)
            {
                return;
            }

            LayerRow layerRow = DataGrid_Layers.CurrentItem as LayerRow;
            if (layerRow == null)
            {
                return;
            }

            IMaterial material = materialLibrary.Material(layerRow.Name, System.Windows.Window.GetWindow(this));
            if (material != null)
            {
                layerRow.Name = material.Name;
            }
        }

        private void Move(bool up = true)
        {
            List<LayerRow> selected = DataGrid_Layers.SelectedItems.Cast<LayerRow>().ToList();
            if (selected.Count == 0)
            {
                return;
            }

            // Process from the edge inwards so moves do not collide.
            selected.Sort((x, y) => up ? rows.IndexOf(x).CompareTo(rows.IndexOf(y)) : rows.IndexOf(y).CompareTo(rows.IndexOf(x)));

            foreach (LayerRow layerRow in selected)
            {
                int index = rows.IndexOf(layerRow);

                if (up && index == 0)
                {
                    continue;
                }

                if (!up && index == rows.Count - 1)
                {
                    continue;
                }

                rows.Move(index, up ? index - 1 : index + 1);
            }

            DataGrid_Layers.SelectedItems.Clear();
            foreach (LayerRow layerRow in selected)
            {
                DataGrid_Layers.SelectedItems.Add(layerRow);
            }
        }

        private bool Add(string name, double thickness)
        {
            if (name == null)
            {
                return false;
            }

            rows.Add(new LayerRow { Name = name, Thickness = thickness });
            return true;
        }

        public bool Enabled
        {
            set
            {
                editable = value;

                Button_Add.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                Button_Remove.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                Button_Up.IsEnabled = value;
                Button_Down.IsEnabled = value;
                DataGrid_Layers.IsReadOnly = !value;
            }
        }

        public List<MaterialLayer> MaterialLayers
        {
            get
            {
                List<MaterialLayer> result = new List<MaterialLayer>();
                foreach (LayerRow layerRow in rows)
                {
                    if (!Core.Query.TryConvert(layerRow.Thickness, out double thickness))
                    {
                        continue;
                    }

                    MaterialLayer materialLayer = new MaterialLayer(layerRow.Name, thickness);
                    result.Add(materialLayer);
                }

                return result;
            }

            set
            {
                rows.Clear();

                if (value != null)
                {
                    foreach (MaterialLayer materialLayer in value)
                    {
                        Add(materialLayer.Name, materialLayer.Thickness);
                    }
                }
            }
        }

        public MaterialLibrary MaterialLibrary
        {
            get
            {
                return materialLibrary;
            }

            set
            {
                materialLibrary = value;
                Button_Add.IsEnabled = materialLibrary != null;
            }
        }

        private sealed class LayerRow : INotifyPropertyChanged
        {
            private string name;
            private double thickness;

            public event PropertyChangedEventHandler PropertyChanged;

            public string Name
            {
                get
                {
                    return name;
                }

                set
                {
                    if (name == value)
                    {
                        return;
                    }

                    name = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                }
            }

            public double Thickness
            {
                get
                {
                    return thickness;
                }

                set
                {
                    if (thickness == value)
                    {
                        return;
                    }

                    thickness = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thickness)));
                }
            }
        }
    }
}
