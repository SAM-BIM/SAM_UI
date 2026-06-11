// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using OxyPlot;
using OxyPlot.Series;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SAM.Core.Mollier.UI
{
    public static partial class Modify
    {
        /// <summary>
        /// Creates series for all the chart base lines (OxyPlot port).
        /// </summary>
        /// <param name="plotModel">Mollier plot model</param>
        /// <param name="mollierControlSettings">Mollier control settings</param>
        /// <returns>List of created series</returns>
        public static List<Series> AddLinesSeries(this PlotModel plotModel, MollierControlSettings mollierControlSettings)
        {
            if (plotModel == null || mollierControlSettings == null)
            {
                return null;
            }

            ChartDataType[] chartDataTypes = new ChartDataType[] { ChartDataType.DryBulbTemperature, ChartDataType.RelativeHumidity, ChartDataType.Density, ChartDataType.Enthalpy, ChartDataType.SpecificVolume, ChartDataType.WetBulbTemperature };
            List<List<ConstantValueCurve>> constantValueCurvesList = Enumerable.Repeat<List<ConstantValueCurve>>(null, chartDataTypes.Length).ToList();

            // Only the (pure) curve generation is parallelised; series are added to the model serially below.
            Parallel.For(0, chartDataTypes.Length, (int i) =>
            {
                constantValueCurvesList[i] = Query.ConstantValueCurves(chartDataTypes[i], mollierControlSettings);
            });

            List<Series> result = new List<Series>();
            foreach (List<ConstantValueCurve> constantValueCurves in constantValueCurvesList)
            {
                if (constantValueCurves == null)
                {
                    continue;
                }

                List<Series> series = Convert.ToChart(constantValueCurves, plotModel, mollierControlSettings);
                if (series == null)
                {
                    continue;
                }

                result.AddRange(series);
            }

            return result;
        }
    }
}
