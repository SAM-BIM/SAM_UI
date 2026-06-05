// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using SAM.Core.Mollier.UI.Controls;
using System.Collections.Generic;

namespace SAM.Core.Mollier.UI
{
    public static partial class Create
    {
        /// <summary>Builds a ParameterControl per process-parameter type (OxyPlot/WPF port; returns WPF controls).</summary>
        public static List<ParameterControl> Controls(this IEnumerable<ProcessParameterType> processParameterTypes)
        {
            if (processParameterTypes == null)
            {
                return null;
            }

            List<ParameterControl> result = new List<ParameterControl>();
            foreach (ProcessParameterType processParameterType in processParameterTypes)
            {
                result.Add(new ParameterControl(processParameterType));
            }

            return result;
        }
    }
}
