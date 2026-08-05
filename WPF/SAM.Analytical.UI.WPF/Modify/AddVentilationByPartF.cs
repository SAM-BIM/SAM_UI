// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.UI;
using System.Collections.Generic;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        public static void AddVentilationByPartF(this UIAnalyticalModel? uIAnalyticalModel, string? zoneCategoryName = null, IEnumerable<Space>? spaces = null)
        {
            AnalyticalModel? analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null)
            {
                return;
            }

            PartFCalculator partFCalculator = Analytical.Query.DefaultPartFCalculator();
            if (partFCalculator is null)
            {
                return;
            }

            AdjacencyCluster? adjacencyCluster = analyticalModel.AdjacencyCluster;
            if(adjacencyCluster is null)
            {
                return;
            }

            partFCalculator.AdjacencyCluster = adjacencyCluster;

            if (string.IsNullOrWhiteSpace(zoneCategoryName))
            {
                partFCalculator.Calculate();
                analyticalModel = new AnalyticalModel(analyticalModel, partFCalculator.AdjacencyCluster);
            }
            else
            {
                List<Zone>? zones = adjacencyCluster?.GetZones()?.FindAll(x => x.GetValue<string>(ZoneParameter.ZoneCategory) == zoneCategoryName);
                if (zones != null)
                {
                    foreach (Zone zone in zones)
                    {
                        List<Space>? spaces_Zone = adjacencyCluster?.GetRelatedObjects<Space>(zone);
                        if (spaces_Zone == null || spaces_Zone.Count == 0)
                        {
                            continue;
                        }

                        partFCalculator.Calculate(spaces_Zone);

                        adjacencyCluster = partFCalculator.AdjacencyCluster;
                    }
                }

                analyticalModel = new AnalyticalModel(analyticalModel, new AdjacencyCluster(adjacencyCluster, true));
            }

            uIAnalyticalModel?.SetJSAMObject(new AnalyticalModel(analyticalModel, adjacencyCluster), new FullModification());
        }
    }
}