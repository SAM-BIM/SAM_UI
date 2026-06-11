// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using OxyPlot;
using SystemColor = System.Drawing.Color;

namespace SAM.Core.Mollier.UI
{
    public static partial class Convert
    {
        /// <summary>
        /// Maps a System.Drawing.Color (the colour type used throughout the SAM domain/UI layer)
        /// to an OxyPlot OxyColor. System.Drawing.Color.Empty maps to OxyColors.Undefined so that
        /// OxyPlot falls back to its automatic colour (the WinForms behaviour for an unset series colour).
        /// </summary>
        public static OxyColor ToOxyColor(this SystemColor color)
        {
            if (color == SystemColor.Empty)
            {
                return OxyColors.Undefined;
            }

            return OxyColor.FromArgb(color.A, color.R, color.G, color.B);
        }

        /// <summary>As <see cref="ToOxyColor(SystemColor)"/> but returns <paramref name="fallback"/> when the colour is Empty.</summary>
        public static OxyColor ToOxyColor(this SystemColor color, OxyColor fallback)
        {
            return color == SystemColor.Empty ? fallback : OxyColor.FromArgb(color.A, color.R, color.G, color.B);
        }
    }
}
