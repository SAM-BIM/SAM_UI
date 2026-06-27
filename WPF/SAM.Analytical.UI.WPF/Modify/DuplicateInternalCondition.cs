// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Linq;

namespace SAM.Analytical.UI.WPF
{
    public static partial class Modify
    {
        // Moved here from SAM.Analytical.UI and merged with the retired
        // SAM.Analytical.Windows.Modify.Duplicate: the editor (InternalConditionWindow) lives in this
        // assembly. Name-dedups the duplicate, opens the editor, then adds it to the model.
        public static void DuplicateInternalCondition(this UIAnalyticalModel uIAnalyticalModel, InternalCondition internalCondition, System.Windows.Forms.IWin32Window owner = null)
        {
            AnalyticalModel analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null || internalCondition == null)
            {
                return;
            }

            AdjacencyCluster adjacencyCluster = analyticalModel.AdjacencyCluster;
            List<InternalCondition> internalConditions = adjacencyCluster.GetInternalConditions(false, true)?.ToList();

            string name = (string.IsNullOrWhiteSpace(internalCondition.Name) ? string.Empty : internalCondition.Name).Trim();
            string name_Temp = name;
            int index = 1;
            while (internalConditions?.Find(x => x.Name == name_Temp) != null)
            {
                name_Temp = string.Format("{0} {1}", name, index.ToString());
                index++;
            }

            internalCondition = new InternalCondition(name_Temp, Guid.NewGuid(), internalCondition);

            InternalConditionWindow internalConditionWindow = new InternalConditionWindow(analyticalModel, internalCondition);

            // Bridge the WinForms IWin32Window owner to the WPF window's native owner handle.
            if (owner != null)
            {
                new System.Windows.Interop.WindowInteropHelper(internalConditionWindow).Owner = owner.Handle;
            }

            if (internalConditionWindow.ShowDialog() != true)
            {
                return;
            }

            internalCondition = internalConditionWindow.InternalCondition;
            if (internalCondition == null)
            {
                return;
            }

            if (!analyticalModel.AddInternalCondition(internalCondition))
            {
                return;
            }

            uIAnalyticalModel.JSAMObject = new AnalyticalModel(analyticalModel);
        }
    }
}
