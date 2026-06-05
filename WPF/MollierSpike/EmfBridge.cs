// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using OxyPlot;

namespace MollierSpike
{
    /// <summary>
    /// EMF export compatibility path. OxyPlot has no native EMF exporter, so we render the
    /// PlotModel through OxyPlot.WindowsForms.GraphicsRenderContext (System.Drawing.Graphics-based)
    /// into a System.Drawing.Imaging.Metafile. This is the ~30 LOC bridge Stage 2d will lift
    /// into SAM.Core.Mollier.UI.WPF — the spike proves it compiles and runs with
    /// UseWindowsForms=false (only System.Drawing.Common + OxyPlot.WindowsForms referenced).
    /// </summary>
    internal static class EmfBridge
    {
        public static void Export(PlotModel model, string path, int width, int height)
        {
            ((IPlotModel)model).Update(true);

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
                        OxyPlot.WindowsForms.GraphicsRenderContext rc = new OxyPlot.WindowsForms.GraphicsRenderContext();
                        rc.SetGraphicsTarget(metafileGraphics);
                        ((IPlotModel)model).Render(rc, new OxyRect(0, 0, width, height));
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
