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
        public static void EditPanel(this UIAnalyticalModel uIAnalyticalModel, Panel panel, IWin32Window owner = null)
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

            ConstructionLibrary constructionLibrary = null;

            List<Construction> constructions = adjacencyCluster?.GetConstructions();
            if (constructions != null)
            {
                constructionLibrary = new ConstructionLibrary(analyticalModel.Name);
                constructions.ForEach(x => constructionLibrary.Add(x));
            }

            PanelWindow panelWindow = new PanelWindow(panel, materialLibrary, constructionLibrary, Core.Query.Enums(typeof(Panel)));
            if (panelWindow.ShowDialog(owner) != true)
            {
                return;
            }

            panel = panelWindow.Panel;
            constructionLibrary = panelWindow.ConstructionLibrary;

            adjacencyCluster.AddObject(panel);

            adjacencyCluster.ReplaceConstructions(constructionLibrary);

            uIAnalyticalModel.JSAMObject = new AnalyticalModel(analyticalModel, adjacencyCluster);
        }
    }
}
