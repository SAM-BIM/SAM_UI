// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.UI;
using SAM.Core.UI.WPF;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        public static void EditSpaces(this UIAnalyticalModel uIAnalyticalModel, IWin32Window owner = null)
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

            ProfileLibrary profileLibrary = analyticalModel.ProfileLibrary;

            SpacesWindow spacesWindow = new SpacesWindow(adjacencyCluster.GetSpaces(), analyticalModel);
            bool? dialogResult = owner == null ? spacesWindow.ShowDialog() : spacesWindow.ShowDialog(owner);
            if (dialogResult != true)
            {
                return;
            }

            IEnumerable<Space> spaces = spacesWindow.Spaces;
            if (spaces != null && spaces.Count() != 0)
            {
                foreach (Space space in spaces)
                {
                    adjacencyCluster.AddObject(space);
                }
            }

            uIAnalyticalModel.JSAMObject = new AnalyticalModel(analyticalModel, adjacencyCluster, analyticalModel.MaterialLibrary, profileLibrary);
        }
    }
}
