// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Analytical.UI;
using SAM.Core.UI.WPF;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        public static MechanicalSystem CreateMechanicalSystem(this UIAnalyticalModel uIAnalyticalModel, MechanicalSystemType mechanicalSystemType = null, IWin32Window owner = null)
        {
            AnalyticalModel analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null)
            {
                return null;
            }

            AdjacencyCluster adjacencyCluster = analyticalModel.AdjacencyCluster;
            if (adjacencyCluster == null)
            {
                adjacencyCluster = new AdjacencyCluster();
            }

            if (mechanicalSystemType == null)
            {
                List<MechanicalSystemType> mechanicalSystemTypes = adjacencyCluster.GetMechanicalSystemTypes<MechanicalSystemType>();
                if (mechanicalSystemTypes == null || mechanicalSystemTypes.Count == 0)
                {
                    mechanicalSystemTypes = Analytical.Query.DefaultSystemTypeLibrary().GetSystemTypes<MechanicalSystemType>();
                }

                if (mechanicalSystemTypes == null || mechanicalSystemTypes.Count == 0)
                {
                    return null;
                }

                ComboBoxWindow<MechanicalSystemType> comboBoxWindow = new ComboBoxWindow<MechanicalSystemType>("Mechanical System Type", mechanicalSystemTypes, (MechanicalSystemType x) => x?.Name);
                bool? comboBoxResult = owner == null ? comboBoxWindow.ShowDialog() : comboBoxWindow.ShowDialog(owner);
                if (comboBoxResult != true)
                {
                    return null;
                }

                mechanicalSystemType = comboBoxWindow.SelectedItem;
            }

            if (mechanicalSystemType == null)
            {
                return null;
            }

            string id = Analytical.Create.Id(adjacencyCluster, mechanicalSystemType);

            MechanicalSystem mechanicalSystem = Analytical.Create.MechanicalSystem(mechanicalSystemType, null, id);

            MechanicalSystemWindow mechanicalSystemWindow = new MechanicalSystemWindow(mechanicalSystem, adjacencyCluster);
            bool? dialogResult = owner == null ? mechanicalSystemWindow.ShowDialog() : mechanicalSystemWindow.ShowDialog(owner);
            if (dialogResult != true)
            {
                return null;
            }

            adjacencyCluster = mechanicalSystemWindow.AdjacencyCluster;
            mechanicalSystem = mechanicalSystemWindow.MechanicalSystem;

            uIAnalyticalModel.JSAMObject = new AnalyticalModel(analyticalModel, adjacencyCluster);

            return mechanicalSystem;
        }
    }
}
