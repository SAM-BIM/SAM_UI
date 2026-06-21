// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.UI.WPF;
using System.Windows.Forms;

namespace SAM.Analytical.UI
{
    public static partial class Modify
    {
        public static void EditAddressAndLocation(this UIAnalyticalModel uIAnalyticalModel, IWin32Window owner = null)
        {
            AnalyticalModel analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null)
            {
                return;
            }

            AddressAndLocationWindow addressAndLocationWindow = new AddressAndLocationWindow(analyticalModel.Address, analyticalModel.Location);
            if (addressAndLocationWindow.ShowDialog(owner) != true)
            {
                return;
            }

            analyticalModel = new AnalyticalModel(analyticalModel.Name, analyticalModel.Description, addressAndLocationWindow.Location, addressAndLocationWindow.Address, analyticalModel.AdjacencyCluster);

            uIAnalyticalModel.JSAMObject = analyticalModel;
        }
    }
}