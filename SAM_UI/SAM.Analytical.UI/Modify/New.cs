// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

namespace SAM.Analytical.UI
{
    public static partial class Modify
    {
        public static bool New(this UIAnalyticalModel uIAnalyticalModel, System.Windows.Forms.IWin32Window owner = null)
        {
            if (uIAnalyticalModel == null)
            {
                uIAnalyticalModel = new UIAnalyticalModel();
            }

            if (uIAnalyticalModel.JSAMObject != null)
            {
                if (!uIAnalyticalModel.Close())
                {
                    return false;
                }
            }

            NewAnalyticalModelWindow newAnalyticalModelWindow = new NewAnalyticalModelWindow("New Project");

            // Bridge the WinForms IWin32Window owner to the WPF window's native owner handle.
            if (owner != null)
            {
                new System.Windows.Interop.WindowInteropHelper(newAnalyticalModelWindow).Owner = owner.Handle;
            }

            if (newAnalyticalModelWindow.ShowDialog() != true)
            {
                return false;
            }

            AnalyticalModel analyticalModel = newAnalyticalModelWindow.GetAnalyticalModel();
            if (analyticalModel == null)
            {
                return false;
            }

            uIAnalyticalModel.JSAMObject = analyticalModel;
            return true;
        }
    }
}
