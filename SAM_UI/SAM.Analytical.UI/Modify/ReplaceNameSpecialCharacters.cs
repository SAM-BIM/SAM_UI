// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core;
using SAM.Core.UI.WPF;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SAM.Analytical.UI
{
    public static partial class Modify
    {
        public static void ReplaceNameSpecialCharacters(this UIAnalyticalModel uIAnalyticalModel, IWin32Window owner = null)
        {
            AnalyticalModel analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null)
            {
                return;
            }

            AdjacencyCluster adjacencyCluster = analyticalModel.AdjacencyCluster;

            List<string> names = ActiveManager.GetSpecialCharacterMapNames();
            if (names == null || names.Count == 0)
            {
                return;
            }

            string name = null;

            ComboBoxWindow<string> comboBoxWindow = new ComboBoxWindow<string>("Select language", names);
            comboBoxWindow.SelectedItem = names.Find(x => x == "ISO");

            if (comboBoxWindow.ShowDialog(owner) != true)
            {
                return;
            }

            name = comboBoxWindow.SelectedItem;

            if(string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            adjacencyCluster.ReplaceNameSpecialCharacters(name);

            analyticalModel = new AnalyticalModel(analyticalModel, adjacencyCluster);

            uIAnalyticalModel.JSAMObject = analyticalModel;
        }
    }
}