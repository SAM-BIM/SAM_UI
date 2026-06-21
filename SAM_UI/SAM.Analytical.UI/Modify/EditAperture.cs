// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using SAM.Core.UI.WPF;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SAM.Analytical.UI
{
    public static partial class Modify
    {
        public static void EditAperture(this UIAnalyticalModel uIAnalyticalModel, Aperture aperture, IWin32Window owner = null)
        {
            AnalyticalModel analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null)
            {
                return;
            }

            AdjacencyCluster adjacencyCluster = analyticalModel.AdjacencyCluster;
            if (adjacencyCluster == null)
            {
                return;
            }

            MaterialLibrary materialLibrary = analyticalModel.MaterialLibrary;

            ApertureConstructionLibrary apertureConstructionLibrary = null;

            List<ApertureConstruction> apertureConstructions = adjacencyCluster?.GetApertureConstructions();
            if (apertureConstructions != null)
            {
                apertureConstructionLibrary = new ApertureConstructionLibrary(analyticalModel.Name);
                apertureConstructions.ForEach(x => apertureConstructionLibrary.Add(x));
            }

            ApertureWindow apertureWindow = new ApertureWindow(aperture, materialLibrary, apertureConstructionLibrary, Core.Query.Enums(typeof(Aperture)));
            if (apertureWindow.ShowDialog(owner) != true)
            {
                return;
            }

            aperture = apertureWindow.Aperture;
            apertureConstructionLibrary = apertureWindow.ApertureConstructionLibrary;

            Panel panel = adjacencyCluster.GetPanel(aperture);
            if (panel != null)
            {
                panel.RemoveAperture(aperture.Guid);
                panel.AddAperture(aperture);
                adjacencyCluster.AddObject(panel);
            }

            adjacencyCluster.ReplaceApertureConstructions(apertureConstructionLibrary);

            uIAnalyticalModel.JSAMObject = new AnalyticalModel(analyticalModel, adjacencyCluster);
        }
    }
}
