// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.UI;
using SAM.Core.UI.WPF;
using System.Windows.Forms;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        public static void EditInternalCondition(this UIAnalyticalModel uIAnalyticalModel, InternalCondition internalCondition, IWin32Window owner = null)
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

            InternalConditionWindow internalConditionWindow = new InternalConditionWindow(analyticalModel, internalCondition);
            bool? dialogResult = owner == null ? internalConditionWindow.ShowDialog() : internalConditionWindow.ShowDialog(owner);
            if (dialogResult != true)
            {
                return;
            }

            internalCondition = internalConditionWindow.InternalCondition;
            if (internalCondition == null)
            {
                return;
            }

            adjacencyCluster.AddObject(internalCondition);

            uIAnalyticalModel.JSAMObject = new AnalyticalModel(analyticalModel, adjacencyCluster, analyticalModel.MaterialLibrary, profileLibrary);
        }
    }
}
