// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Mollier.UI.Controls;
using System.Windows.Controls;

namespace SAM.Core.Mollier.UI
{
    public static partial class Query
    {
        /// <summary>Reads a ParameterControl's value from a WPF panel by process-parameter type (WinForms FlowLayoutPanel port).</summary>
        public static T ParameterValue<T>(Panel panel, ProcessParameterType processParameterType)
        {
            if (panel == null)
            {
                return default(T);
            }

            foreach (object child in panel.Children)
            {
                if (typeof(T) == typeof(double))
                {
                    ParameterControl parameterControl = child as ParameterControl;
                    if (parameterControl == null)
                    {
                        continue;
                    }

                    if (processParameterType == parameterControl.ProcessParameterType)
                    {
                        return (T)(object)parameterControl.Value;
                    }
                }
            }

            return default(T);
        }
    }
}
