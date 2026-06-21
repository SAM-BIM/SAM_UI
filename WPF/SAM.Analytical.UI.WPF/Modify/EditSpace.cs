// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.UI;
using SAM.Core.UI.WPF;
using System.Windows.Forms;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        public static void EditSpace(this UIAnalyticalModel uIAnalyticalModel, Space space, IWin32Window owner = null)
        {
            AnalyticalModel analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null)
            {
                return;
            }

            AdjacencyCluster adjacencyCluster = analyticalModel.AdjacencyCluster;
            ProfileLibrary profileLibrary = analyticalModel.ProfileLibrary;

            SpaceWindow spaceWindow = new SpaceWindow(space, analyticalModel, Core.Query.Enums(typeof(Space)));
            bool? dialogResult = owner == null ? spaceWindow.ShowDialog() : spaceWindow.ShowDialog(owner);
            if (dialogResult != true)
            {
                return;
            }

            Space space_Temp = spaceWindow.Space;
            if (space_Temp != null)
            {
                adjacencyCluster?.AddObject(space_Temp);
            }

            uIAnalyticalModel.JSAMObject = new AnalyticalModel(analyticalModel, adjacencyCluster, analyticalModel.MaterialLibrary, profileLibrary);
        }
    }
}
