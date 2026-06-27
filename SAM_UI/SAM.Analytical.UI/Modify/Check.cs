// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020–2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.UI.WPF;
using System.Windows.Forms;

namespace SAM.Analytical.UI
{
    public static partial class Modify
    {
        public static void Check(this UIAnalyticalModel uIAnalyticalModel, IWin32Window owner = null)
        {
            AnalyticalModel analyticalModel = uIAnalyticalModel?.JSAMObject;
            if (analyticalModel == null)
            {
                return;
            }

            Core.Log log = null;

            System.Action action = new (() => 
            {
                log = analyticalModel.Log();
            });

            SAM.Core.UI.WPF.ProgressBarWindow.Show("Loading Data", action, owner);
            if(log == null)
            {
                return;
            }

            log.Sort();

            SAM.Core.UI.WPF.LogWindow logWindow = new SAM.Core.UI.WPF.LogWindow(log);
            if (owner == null)
            {
                logWindow.ShowDialog();
            }
            else
            {
                logWindow.ShowDialog(owner);
            }
        }
    }
}