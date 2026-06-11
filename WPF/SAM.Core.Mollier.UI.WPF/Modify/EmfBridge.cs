// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using OxyPlot;

namespace SAM.Core.Mollier.UI
{
    /// <summary>
    /// EMF export compatibility path. OxyPlot has no native EMF exporter, so the PlotModel is
    /// rendered through <see cref="OxyPlot.WindowsForms.GraphicsRenderContext"/> (a
    /// System.Drawing.Graphics-based render context) into a <see cref="Metafile"/>. This needs
    /// only the OxyPlot.WindowsForms + System.Drawing.Common packages — it does NOT require
    /// UseWindowsForms. Validated in the Stage 2a OxyPlot spike. SVG is the preferred vector
    /// export; EMF is kept for backward compatibility with the WinForms tool.
    /// </summary>
    public static partial class Modify
    {
        public static void SaveEmf(this PlotModel plotModel, string path, int width = 1000, int height = 700)
        {
            if (plotModel == null || string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            ((IPlotModel)plotModel).Update(true);

            // A Metafile needs a reference HDC to anchor its device context.
            using (Bitmap reference = new Bitmap(1, 1))
            using (Graphics referenceGraphics = Graphics.FromImage(reference))
            {
                IntPtr hdc = referenceGraphics.GetHdc();
                try
                {
                    using (FileStream stream = File.Create(path))
                    using (Metafile metafile = new Metafile(stream, hdc,
                        new RectangleF(0, 0, width, height), MetafileFrameUnit.Pixel, EmfType.EmfPlusDual))
                    using (Graphics metafileGraphics = Graphics.FromImage(metafile))
                    {
                        OxyPlot.WindowsForms.GraphicsRenderContext renderContext = new OxyPlot.WindowsForms.GraphicsRenderContext();
                        renderContext.SetGraphicsTarget(metafileGraphics);
                        ((IPlotModel)plotModel).Render(renderContext, new OxyRect(0, 0, width, height));
                    }
                }
                finally
                {
                    referenceGraphics.ReleaseHdc(hdc);
                }
            }
        }
    }
}
