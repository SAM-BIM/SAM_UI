// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        public static void EditInternalConditions(this UIAnalyticalModel uIAnalyticalModel, IEnumerable<Space> spaces)
        {
            AnalyticalModel analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null)
            {
                return;
            }

            InternalConditionWithSpacesWindow internalConditionWindow = new InternalConditionWithSpacesWindow(uIAnalyticalModel, spaces);
            bool? dialogResult = internalConditionWindow.ShowDialog();
            if(dialogResult == null || !dialogResult.HasValue || !dialogResult.Value)
            {
                return;
            }
        }

        // Moved here from SAM.Analytical.UI (the InternalCondition library browser is now the WPF
        // InternalConditionLibraryWindow, which lives in this assembly).
        public static void EditInternalConditions(this UIAnalyticalModel uIAnalyticalModel, System.Windows.Forms.IWin32Window owner = null)
        {
            AnalyticalModel analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null)
            {
                return;
            }

            AdjacencyCluster adjacencyCluster = analyticalModel.AdjacencyCluster;
            if (adjacencyCluster == null)
            {
                adjacencyCluster = new AdjacencyCluster();
            }

            ProfileLibrary profileLibrary = analyticalModel.ProfileLibrary;

            List<InternalCondition> internalConditions = adjacencyCluster.GetInternalConditions(false, true)?.ToList();
            InternalConditionLibrary internalConditionLibrary = new InternalConditionLibrary(analyticalModel.Name);
            internalConditions?.ForEach(x => internalConditionLibrary.Add(x));

            InternalConditionLibraryWindow internalConditionLibraryWindow = new InternalConditionLibraryWindow(internalConditionLibrary, profileLibrary, adjacencyCluster)
            {
                Title = "Internal Conditions",
                MultiSelect = true
            };

            // Bridge the WinForms IWin32Window owner to the WPF window's native owner handle.
            if (owner != null)
            {
                new System.Windows.Interop.WindowInteropHelper(internalConditionLibraryWindow).Owner = owner.Handle;
            }

            if (internalConditionLibraryWindow.ShowDialog() != true)
            {
                return;
            }

            internalConditionLibrary = internalConditionLibraryWindow.InternalConditionLibrary;
            profileLibrary = internalConditionLibraryWindow.ProfileLibrary;
            adjacencyCluster = internalConditionLibraryWindow.AdjacencyCluster;

            internalConditions = internalConditionLibrary?.GetInternalConditions();
            if (internalConditions == null || internalConditions.Count == 0)
            {
                adjacencyCluster.RemoveAll<InternalCondition>();
            }
            else
            {
                IEnumerable<InternalCondition> internalConditions_AdjacencyCluster = adjacencyCluster.GetInternalConditions(false, true);
                if (internalConditions_AdjacencyCluster != null && internalConditions_AdjacencyCluster.Count() != 0)
                {
                    foreach (InternalCondition internalCondition_AdjacencyCluster in internalConditions_AdjacencyCluster)
                    {
                        if (internalConditions.Find(x => x.Guid == internalCondition_AdjacencyCluster.Guid) == null)
                        {
                            adjacencyCluster.RemoveObject<InternalCondition>(internalCondition_AdjacencyCluster.Guid);
                        }
                    }
                }

                internalConditions.ForEach(x => adjacencyCluster.AddObject(x));
            }

            uIAnalyticalModel.JSAMObject = new AnalyticalModel(analyticalModel, adjacencyCluster, analyticalModel.MaterialLibrary, profileLibrary);
        }
    }
}
