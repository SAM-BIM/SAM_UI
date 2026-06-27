// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using System;
using System.Collections.Generic;
using System.Windows;

namespace SAM.Core.UI.WPF
{
    public static partial class Modify
    {
        /// <summary>
        /// Duplicates a material in a library (uniquifying its name) and opens the WPF
        /// MaterialWindow to edit the copy. Ported from SAM.Core.Windows.Modify.Duplicate.
        /// </summary>
        public static IMaterial Duplicate(this MaterialLibrary materialLibrary, IMaterial material, Window owner = null, IEnumerable<Enum> enums = null)
        {
            if (materialLibrary == null || material == null)
            {
                return null;
            }

            string name = (string.IsNullOrWhiteSpace(material.Name) ? string.Empty : material.Name).Trim();
            string name_Temp = name;
            int index = 1;
            while (materialLibrary?.GetMaterials()?.Find(x => x.Name == name_Temp) != null)
            {
                name_Temp = string.Format("{0} {1}", name, index.ToString());
                index++;
            }
            name = name_Temp;

            material = Core.Create.Material(material as Material, name, name, null);
            if (material == null)
            {
                MessageBox.Show("Material cannot be duplicated");
                return null;
            }

            MaterialWindow materialWindow = new MaterialWindow(material, enums);
            if (owner != null)
            {
                materialWindow.Owner = owner;
            }

            if (materialWindow.ShowDialog() != true)
            {
                return null;
            }

            material = materialWindow.Material;
            if (material == null)
            {
                return null;
            }

            materialLibrary?.Add(material);

            return material;
        }
    }
}
