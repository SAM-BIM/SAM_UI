// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using OxyPlot;
using SAM.Geometry.Planar;

namespace SAM.Core.Mollier.UI
{
    public static partial class Query
    {
        /// <summary>
        /// OxyPlot port of the WinForms label-scale heuristic. The original derived a scale vector from
        /// screen DPI / physical resolution and the form client size; OxyPlot has no equivalent of that
        /// device context, so this reproduces the part that actually drives label sizing: the zoom
        /// proportion relative to the default ranges (≈1 at default zoom, growing/shrinking with zoom).
        /// The absolute cross-DPI fudge factor is dropped. NOTE: label spacing should be eyeballed in the
        /// live app by a domain user (per the migration plan's visual-fidelity risk callout).
        /// </summary>
        public static Vector2D ScaleVector2D(PlotModel plotModel, MollierControlSettings mollierControlSettings)
        {
            if (mollierControlSettings == null)
            {
                return new Vector2D(1, 1);
            }

            MollierControlSettings mollierControlSettings_Default = new MollierControlSettings();

            double humidityRatioProportion = (mollierControlSettings.HumidityRatio_Max - mollierControlSettings.HumidityRatio_Min) / (mollierControlSettings_Default.HumidityRatio_Max - mollierControlSettings_Default.HumidityRatio_Min);
            double temperatureProportion = (mollierControlSettings.Temperature_Max - mollierControlSettings.Temperature_Min) / (mollierControlSettings_Default.Temperature_Max - mollierControlSettings_Default.Temperature_Min);

            double xFactor = humidityRatioProportion;
            double yFactor = temperatureProportion;
            if (mollierControlSettings.ChartType == ChartType.Psychrometric)
            {
                double temp = xFactor;
                xFactor = yFactor;
                yFactor = temp;
            }

            return new Vector2D(xFactor, yFactor);
        }
    }
}
