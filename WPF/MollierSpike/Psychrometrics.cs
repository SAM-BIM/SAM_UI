// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;

namespace MollierSpike
{
    /// <summary>
    /// Self-contained psychrometric formulas, deliberately NOT depending on SAM.Core.Mollier.
    /// The spike validates OxyPlot capabilities, not domain correctness, so it keeps the app
    /// throwaway and dependency-free. The diagram axes match the real psychrometric chart:
    /// X = dry-bulb temperature (degC), Y = humidity ratio (g water / kg dry air).
    /// </summary>
    internal static class Psychrometrics
    {
        public const double AtmosphericPressure = 101325.0; // Pa

        // Magnus saturation vapour pressure over water (Pa), T in degC.
        public static double SaturationPressure(double dryBulbTemperature)
        {
            return 610.94 * Math.Exp(17.625 * dryBulbTemperature / (243.04 + dryBulbTemperature));
        }

        // Humidity ratio (kg/kg) at a given temperature and relative humidity [0..1].
        public static double HumidityRatio(double dryBulbTemperature, double relativeHumidity, double pressure = AtmosphericPressure)
        {
            double pw = relativeHumidity * SaturationPressure(dryBulbTemperature);
            if (pw >= pressure)
            {
                return double.NaN;
            }
            return 0.621945 * pw / (pressure - pw);
        }

        public sealed class Curve
        {
            public string Name;
            public List<(double T, double W)> Points = new List<(double, double)>();
        }

        /// <summary>Constant-relative-humidity curve (RH in percent) sampled across the temperature range, in g/kg.</summary>
        public static Curve ConstantRelativeHumidityCurve(double relativeHumidityPercent, double tMin, double tMax, double step = 1.0)
        {
            Curve curve = new Curve { Name = relativeHumidityPercent + "% RH" };
            for (double t = tMin; t <= tMax + 1e-9; t += step)
            {
                double w = HumidityRatio(t, relativeHumidityPercent / 100.0);
                if (!double.IsNaN(w))
                {
                    curve.Points.Add((t, w * 1000.0));
                }
            }
            return curve;
        }

        /// <summary>Constant-enthalpy line (h in kJ/kg) clipped to the W>=0 region, in g/kg.</summary>
        public static Curve ConstantEnthalpyCurve(double enthalpyKJ, double tMin, double tMax, double step = 1.0)
        {
            Curve curve = new Curve { Name = enthalpyKJ + " kJ/kg" };
            for (double t = tMin; t <= tMax + 1e-9; t += step)
            {
                // h = 1.006*T + W*(2501 + 1.86*T)  ->  W = (h - 1.006*T) / (2501 + 1.86*T)
                double w = (enthalpyKJ - 1.006 * t) / (2501.0 + 1.86 * t);
                if (w >= 0)
                {
                    curve.Points.Add((t, w * 1000.0));
                }
            }
            return curve;
        }
    }
}
