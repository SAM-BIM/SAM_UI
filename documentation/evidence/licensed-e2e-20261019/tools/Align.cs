// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

// Hourly alignment checks between two CSVs (column by header substring): Pearson correlation and RMS difference at
// lags -2..+2 (b shifted by lag hours), annual sums, monthly sums.
public static class Align
{
    static List<double> Col(string csv, string headerContains)
    {
        string[] lines = File.ReadAllLines(csv);
        string[] h = lines[0].Split(',');
        int i = Array.FindIndex(h, x => x.Contains(headerContains));
        if (i < 0) throw new Exception("column not found: " + headerContains + " in " + csv);
        return lines.Skip(1).Select(l => double.Parse(l.Split(',')[i], CultureInfo.InvariantCulture)).ToList();
    }

    public static string Run(string csvA, string colA, double scaleA, string csvB, string colB, double scaleB)
    {
        CultureInfo ci = CultureInfo.InvariantCulture;
        List<double> a = Col(csvA, colA).Select(x => x * scaleA).ToList();
        List<double> b = Col(csvB, colB).Select(x => x * scaleB).ToList();
        int n = Math.Min(a.Count, b.Count);
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Format(ci, "A={0} sum={1:F3}  B={2} sum={3:F3}  n={4}", colA, a.Take(n).Sum(), colB, b.Take(n).Sum(), n));
        for (int lag = -2; lag <= 2; lag++)
        {
            List<double> x = new List<double>(), y = new List<double>();
            for (int t = 0; t < n; t++) { int u = t + lag; if (u < 0 || u >= n) continue; x.Add(a[t]); y.Add(b[u]); }
            double mx = x.Average(), my = y.Average();
            double sxy = 0, sxx = 0, syy = 0, se = 0;
            for (int k = 0; k < x.Count; k++) { sxy += (x[k] - mx) * (y[k] - my); sxx += (x[k] - mx) * (x[k] - mx); syy += (y[k] - my) * (y[k] - my); se += (x[k] - y[k]) * (x[k] - y[k]); }
            sb.AppendLine(string.Format(ci, "  lag {0,2}: r={1:F5} rms={2:F4}", lag, sxy / Math.Sqrt(sxx * syy), Math.Sqrt(se / x.Count)));
        }
        int[] days = { 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 }; int s = 0;
        sb.Append("  monthly A/B:");
        for (int m = 0; m < 12; m++) { int e = s + days[m] * 24; sb.Append(string.Format(ci, " {0:F0}/{1:F0}", a.Skip(s).Take(e - s).Sum(), b.Skip(s).Take(e - s).Sum())); s = e; }
        sb.AppendLine();
        return sb.ToString();
    }
}
