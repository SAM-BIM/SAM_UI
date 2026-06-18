// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;

namespace SAM.Analytical.UI
{
    public static partial class Modify
    {
        public static void ReplaceConstructions(this AdjacencyCluster adjacencyCluster, ConstructionLibrary constructionLibrary)
        {
            if(adjacencyCluster == null)
            {
                return;
            }

            List<Construction> constructions_Temp = adjacencyCluster.GetObjects<Construction>();
            adjacencyCluster.Remove(constructions_Temp);

            UpdateConstructions(adjacencyCluster, constructionLibrary);
        }

        public static void ReplaceConstructions(this UIAnalyticalModel uIAnalyticalModel, ConstructionLibrary constructionLibrary)
        {
            AdjacencyCluster adjacencyCluster = uIAnalyticalModel?.JSAMObject?.AdjacencyCluster;
            if (adjacencyCluster == null)
            {
                return;
            }

            List<Construction> constructions_Temp = adjacencyCluster.GetObjects<Construction>();
            adjacencyCluster.Remove(constructions_Temp);

            UpdateConstructions(adjacencyCluster, constructionLibrary);

            uIAnalyticalModel.JSAMObject = new AnalyticalModel(uIAnalyticalModel.JSAMObject, adjacencyCluster);
        }

        // Ported from the retired SAM.Analytical.Windows.Modify.UpdateConstructions: re-points panels
        // that reference a library construction (by Guid) and adds any remaining ones to the cluster.
        private static void UpdateConstructions(AdjacencyCluster adjacencyCluster, ConstructionLibrary constructionLibrary)
        {
            List<Construction> constructions = constructionLibrary?.GetConstructions();
            if (adjacencyCluster == null || constructions == null)
            {
                return;
            }

            List<Construction> constructions_Temp = new List<Construction>(constructions);

            List<Panel> panels = adjacencyCluster.GetPanels();
            if (panels != null)
            {
                for (int i = constructions_Temp.Count - 1; i >= 0; i--)
                {
                    bool exists = false;
                    foreach (Panel panel in panels)
                    {
                        Construction construction = panel?.Construction;
                        if (construction != null && construction.Guid == constructions_Temp[i].Guid)
                        {
                            adjacencyCluster.AddObject(Analytical.Create.Panel(panel, constructions_Temp[i]));
                            exists = true;
                        }
                    }

                    if (exists)
                    {
                        constructions_Temp.RemoveAt(i);
                    }
                }
            }

            foreach (Construction construction_Temp in constructions_Temp)
            {
                adjacencyCluster.AddObject(construction_Temp);
            }
        }
    }
}
