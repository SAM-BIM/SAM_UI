// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.UI
{
    public static partial class Modify
    {
        public static void ReplaceApertureConstructions(this AdjacencyCluster adjacencyCluster, ApertureConstructionLibrary apertureConstructionLibrary)
        {
            if(adjacencyCluster == null)
            {
                return;
            }

            List<ApertureConstruction> apertureConstructions_Temp = adjacencyCluster.GetObjects<ApertureConstruction>();
            adjacencyCluster.Remove(apertureConstructions_Temp);

            UpdateApertureConstructions(adjacencyCluster, apertureConstructionLibrary);
        }

        public static void ReplaceApertureConstructions(this UIAnalyticalModel uIAnalyticalModel, ApertureConstructionLibrary apertureConstructionLibrary)
        {
            AdjacencyCluster adjacencyCluster = uIAnalyticalModel?.JSAMObject?.AdjacencyCluster;
            if (adjacencyCluster == null)
            {
                return;
            }

            adjacencyCluster.ReplaceApertureConstructions(apertureConstructionLibrary);

            uIAnalyticalModel.JSAMObject = new AnalyticalModel(uIAnalyticalModel.JSAMObject, adjacencyCluster);
        }

        // Ported from the retired SAM.Analytical.Windows.Modify.UpdateApertureConstructions: re-points
        // apertures that reference a library aperture-construction (by Guid) and adds any remaining
        // ones to the cluster.
        private static void UpdateApertureConstructions(AdjacencyCluster adjacencyCluster, ApertureConstructionLibrary apertureConstructionLibrary)
        {
            List<ApertureConstruction> apertureConstructions = apertureConstructionLibrary?.GetApertureConstructions();
            if (adjacencyCluster == null || apertureConstructions == null || apertureConstructions.Count == 0)
            {
                return;
            }

            List<ApertureConstruction> apertureConstructions_Temp = new List<ApertureConstruction>(apertureConstructions);

            List<Panel> panels = adjacencyCluster.GetPanels();
            if (panels != null)
            {
                for (int i = apertureConstructions_Temp.Count - 1; i >= 0; i--)
                {
                    bool exists = false;
                    foreach (Panel panel in panels)
                    {
                        List<Aperture> apertures = panel.Apertures;
                        if (apertures == null || apertures.Count == 0)
                        {
                            continue;
                        }

                        foreach (Aperture aperture in apertures)
                        {
                            ApertureConstruction apertureConstruction = aperture?.ApertureConstruction;
                            if (apertureConstruction == null)
                            {
                                continue;
                            }

                            if (apertureConstruction.Guid == apertureConstructions_Temp[i].Guid)
                            {
                                panel.RemoveAperture(aperture.Guid);
                                panel.AddAperture(new Aperture(aperture, apertureConstructions_Temp[i]));
                                adjacencyCluster.AddObject(panel);
                                exists = true;
                            }
                        }
                    }

                    if (exists)
                    {
                        apertureConstructions_Temp.RemoveAt(i);
                    }
                }
            }

            foreach (ApertureConstruction apertureConstruction_Temp in apertureConstructions_Temp)
            {
                adjacencyCluster.AddObject(apertureConstruction_Temp);
            }
        }
    }
}
