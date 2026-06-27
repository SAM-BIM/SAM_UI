// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using System;
using System.Collections.Generic;
using System.Windows;

namespace SAM.Core.UI.WPF
{
    public static partial class Query
    {
        /// <summary>
        /// WPF replacement for SAM.Core.Windows.Query.Material: shows a modal
        /// <see cref="SelectMaterialWindow"/> over the given material library and returns the
        /// chosen material (null if cancelled). <paramref name="name"/> pre-fills the search box.
        /// </summary>
        public static IMaterial Material(this MaterialLibrary materialLibrary, string name = null, Window owner = null)
        {
            if (materialLibrary == null)
            {
                return null;
            }

            List<IMaterial> materials = materialLibrary.GetMaterials();

            SelectMaterialWindow selectMaterialWindow = new SelectMaterialWindow(materials, Core.Query.Enums(typeof(IMaterial)))
            {
                SearchText = name
            };

            if (owner != null)
            {
                selectMaterialWindow.Owner = owner;
            }

            if (selectMaterialWindow.ShowDialog() != true)
            {
                return null;
            }

            return selectMaterialWindow.Material;
        }
    }
}
