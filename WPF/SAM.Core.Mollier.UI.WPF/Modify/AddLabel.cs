// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using OxyPlot;
using OxyPlot.Annotations;
using SAM.Core.Mollier.UI.Classes;

namespace SAM.Core.Mollier.UI
{
    public static partial class Modify
    {
        // OxyPlot port: a WinForms point-series-with-Label becomes a TextAnnotation.
        public static TextAnnotation AddLabel(this PlotModel plotModel, ChartLabel chartLabel, MollierControlSettings mollierControlSettings)
        {
            if (plotModel == null || chartLabel == null)
            {
                return null;
            }

            TextAnnotation result = new TextAnnotation
            {
                Text = chartLabel.Text,
                TextPosition = new DataPoint(chartLabel.Position.X, chartLabel.Position.Y),
                TextRotation = System.Convert.ToInt32(chartLabel.Angle) % 90,
                TextColor = chartLabel.Color.ToOxyColor(OxyColors.Black),
                Stroke = OxyColors.Undefined,
                Background = OxyColors.Undefined,
                StrokeThickness = 0,
                Tag = chartLabel.Tag,
            };

            plotModel.Annotations.Add(result);
            return result;
        }
    }
}
