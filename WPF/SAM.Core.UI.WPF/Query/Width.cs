// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System.Drawing;
using System.Windows.Forms;

namespace SAM.Core.UI.WPF
{
    public static partial class Query
    {
        /// <summary>
        /// Width of <paramref name="text"/> rendered in <paramref name="font"/>, scaled so the
        /// glyph height equals <paramref name="height"/>. Ported from SAM.Core.Windows.Query.Width
        /// (part of retiring the SAM_Windows dependency); uses GDI text measurement.
        /// </summary>
        public static double Width(this string text, Font font, double height)
        {
            if (text == null || font == null || double.IsNaN(height) || height <= 0)
            {
                return double.NaN;
            }

            Size size = TextRenderer.MeasureText(text, font);
            if (size.Height <= 0)
            {
                return double.NaN;
            }

            double factor = height / size.Height;

            return size.Width * factor;
        }
    }
}
