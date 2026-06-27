// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Mollier.UI.Controls;
using System.Windows.Controls;

namespace SAM.Core.Mollier.UI
{
    public static partial class Modify
    {
        /// <summary>Sets a ParameterControl's value within a WPF panel by process-parameter type (WinForms FlowLayoutPanel port).</summary>
        public static bool SetParameterValue(Panel panel, ProcessParameterType processParameterType, double value)
        {
            if (panel == null)
            {
                return false;
            }

            foreach (object child in panel.Children)
            {
                ParameterControl parameterControl = child as ParameterControl;
                if (parameterControl == null)
                {
                    continue;
                }

                if (processParameterType == parameterControl.ProcessParameterType)
                {
                    parameterControl.Value = value;
                    return true;
                }
            }

            return false;
        }
    }
}
