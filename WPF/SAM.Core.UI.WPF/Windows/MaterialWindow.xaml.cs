// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using SAM.Core.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace SAM.Core.UI.WPF
{
    /// <summary>
    /// WPF replacement for the WinForms SAM.Core.Windows.Forms.MaterialForm (+ MaterialControl).
    /// Edits an <see cref="IMaterial"/>: identity/thermal fields plus a category-grouped
    /// custom-parameter grid (<see cref="ParametersControl"/>). Mirrors the original public
    /// API (constructor, the read-only Material property).
    /// </summary>
    public partial class MaterialWindow : Window
    {
        private IMaterial material;
        private HashSet<Enum> enums;

        public MaterialWindow()
        {
            InitializeComponent();

            LoadMaterialTypes();
        }

        public MaterialWindow(IMaterial material, IEnumerable<Enum> enums = null)
        {
            InitializeComponent();

            LoadMaterialTypes();

            this.material = material;

            if (enums != null)
            {
                this.enums = new HashSet<Enum>(enums);
            }

            LoadMaterial();
        }

        private void LoadMaterialTypes()
        {
            foreach (MaterialType materialType in Enum.GetValues(typeof(MaterialType)))
            {
                ComboBox_MaterialType.Items.Add(Core.Query.Description(materialType));
            }
        }

        private void LoadMaterial()
        {
            TextBox_Name.Text = material?.Name;

            Material material_Temp = material as Material;
            if (material_Temp != null)
            {
                TextBox_DisplayName.Text = material_Temp.DisplayName;
                TextBox_Description.Text = material_Temp.Description;

                TextBox_ThermalConductivity.Text = double.IsNaN(material_Temp.ThermalConductivity) ? null : material_Temp.ThermalConductivity.ToString();
                TextBox_SpecificHeatCapacity.Text = double.IsNaN(material_Temp.SpecificHeatCapacity) ? null : material_Temp.SpecificHeatCapacity.ToString();
                TextBox_Density.Text = double.IsNaN(material_Temp.Density) ? null : material_Temp.Density.ToString();

                ComboBox_MaterialType.Text = Core.Query.Description(Core.Query.MaterialType(material_Temp));
            }

            LoadParameters();
        }

        private void LoadParameters()
        {
            ParametersControl_Main.CustomParameters = null;

            Material material_Temp = material as Material;
            if (material_Temp == null)
            {
                return;
            }

            CustomParameters customParameters = SAM.Core.UI.Create.CustomParameters(material_Temp, enums?.ToArray());

            if (material_Temp is FluidMaterial fluidMaterial)
            {
                CustomParameter customParameter = new CustomParameter("Dynamic Viscosity", "Dynamic Viscosity of Fluid [kg/(m*s)]", AccessType.ReadWrite, new DoubleParameterValue(0), typeof(FluidMaterial).Assembly.Name(), fluidMaterial.DynamicViscosity);
                customParameters?.Add(customParameter);
            }

            ParametersControl_Main.CustomParameters = customParameters;
        }

        public IMaterial Material
        {
            get
            {
                if (material == null)
                {
                    return null;
                }

                string name = TextBox_Name.Text;
                string displayName = TextBox_DisplayName.Text;
                string description = TextBox_Description.Text;

                if (!Core.Query.TryConvert(TextBox_ThermalConductivity.Text, out double thermalConductivity))
                {
                    thermalConductivity = double.NaN;
                }

                if (!Core.Query.TryConvert(TextBox_SpecificHeatCapacity.Text, out double specificHeatCapacity))
                {
                    specificHeatCapacity = double.NaN;
                }

                // NOTE: mirrors the original SAM_Windows MaterialControl, which read Density from
                // the SpecificHeatCapacity text box (pre-existing bug, preserved deliberately).
                if (!Core.Query.TryConvert(TextBox_SpecificHeatCapacity.Text, out double density))
                {
                    density = double.NaN;
                }

                CustomParameters customParameters = ParametersControl_Main.CustomParameters;

                IMaterial result = null;
                switch (material.MaterialType())
                {
                    case MaterialType.Gas:

                        double dynamicViscosity = double.NaN;

                        CustomParameter customParameter = customParameters?.Cast<CustomParameter>().ToList().Find(x => x?.Name == "Dynamic Viscosity");
                        if (customParameter != null)
                        {
                            if (!Core.Query.TryConvert(customParameter.Value, out dynamicViscosity))
                            {
                                dynamicViscosity = double.NaN;
                            }
                        }

                        result = new GasMaterial(material.Guid, name, displayName, description, thermalConductivity, density, specificHeatCapacity, dynamicViscosity);
                        break;

                    case MaterialType.Opaque:
                        Material opaqueMaterial = (OpaqueMaterial)material;
                        result = new OpaqueMaterial(opaqueMaterial.Guid, name, displayName, description, thermalConductivity, density, specificHeatCapacity);
                        break;

                    case MaterialType.Transparent:
                        Material transparentMaterial = (TransparentMaterial)material;
                        result = new OpaqueMaterial(transparentMaterial.Guid, name, displayName, description, thermalConductivity, density, specificHeatCapacity);
                        break;

                    default:
                        return null;
                }

                if (result is SAMObject sAMObject)
                {
                    SAM.Core.UI.Modify.SetValues(sAMObject, customParameters);
                }

                return result;
            }
        }

        private void NumberOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = System.Text.RegularExpressions.Regex.IsMatch(e.Text, "[^0-9.-]+");
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
    }
}
