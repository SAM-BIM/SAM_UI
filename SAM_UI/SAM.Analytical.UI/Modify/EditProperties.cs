// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.UI
{
    public static partial class Modify
    {
        public static void EditProperties(this UIAnalyticalModel uIAnalyticalModel, System.Windows.Forms.IWin32Window owner = null)
        {
            AnalyticalModel analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null)
            {
                return;
            }

            AnalyticalModelWindow analyticalModelWindow = new AnalyticalModelWindow(analyticalModel, Core.Query.Enums(typeof(AnalyticalModel)));

            // Bridge the WinForms IWin32Window owner to the WPF window's native owner handle.
            if (owner != null)
            {
                new System.Windows.Interop.WindowInteropHelper(analyticalModelWindow).Owner = owner.Handle;
            }

            if (analyticalModelWindow.ShowDialog() != true)
            {
                return;
            }

            analyticalModel = analyticalModelWindow.AnalyticalModel;
            if (analyticalModel == null)
            {
                return;
            }

            uIAnalyticalModel.JSAMObject = analyticalModel;
        }
    }
}
