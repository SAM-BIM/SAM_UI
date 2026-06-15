// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Architectural;
using System.Collections.Generic;

namespace SAM.Analytical.UI
{
    public static partial class Query
    {
        /// <summary>
        /// WPF equivalent of SAM.Analytical.Windows.Query.ConstructionLayers: converts the
        /// material layers held by a (WPF) <see cref="MaterialLayersControl"/> into
        /// <see cref="ConstructionLayer"/>s.
        /// </summary>
        public static List<ConstructionLayer> ConstructionLayers(this MaterialLayersControl materialLayersControl)
        {
            List<MaterialLayer> materialLayers = materialLayersControl?.MaterialLayers;
            if (materialLayers == null)
            {
                return null;
            }

            List<ConstructionLayer> result = new List<ConstructionLayer>();
            foreach (MaterialLayer materialLayer in materialLayers)
            {
                result.Add(new ConstructionLayer(materialLayer.Name, materialLayer.Thickness));
            }

            return result;
        }
    }
}
