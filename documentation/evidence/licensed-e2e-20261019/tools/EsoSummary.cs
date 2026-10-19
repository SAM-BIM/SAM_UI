// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

// Independent EnergyPlus ESO reader: hourly variables only. Annual sums (J -> kWh), maxima (J/h -> W) and the
// 0-based hour-of-year of the maximum; optional hourly CSV.
public static class EsoSummary
{
    public static string Run(string esoPath, string csvPath)
    {
        CultureInfo ci = CultureInfo.InvariantCulture;
        Dictionary<int, string> names = new Dictionary<int, string>();
        List<int> order = new List<int>();
        Dictionary<int, List<double>> series = new Dictionary<int, List<double>>();
        bool data = false; int hours = 0; int environments = 0;
        foreach (string line in File.ReadLines(esoPath))
        {
            if (!data)
            {
                if (line == "End of Data Dictionary") { data = true; continue; }
                int bang = line.IndexOf("!Hourly", StringComparison.Ordinal);
                if (bang > 0)
                {
                    string[] p = line.Substring(0, bang).Split(new[] { ',' }, 3);
                    int id = int.Parse(p[0], ci);
                    names[id] = p[2].Trim(); order.Add(id); series[id] = new List<double>(8760);
                }
                continue;
            }
            if (line == "End of Data") break;
            int c = line.IndexOf(',');
            int key = int.Parse(line.Substring(0, c), ci);
            if (key == 1) { environments++; continue; }
            if (key == 2) { hours++; continue; }
            List<double> s;
            if (series.TryGetValue(key, out s)) s.Add(double.Parse(line.Substring(c + 1), ci));
        }
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("environments=" + environments + " hourlyStamps=" + hours);
        foreach (int id in order)
        {
            List<double> s = series[id]; if (s.Count == 0) continue;
            double max = s.Max(); int imax = s.IndexOf(max); double sum = s.Sum();
            if (names[id].Contains("[J]"))
                sb.AppendLine(string.Format(ci, "{0} | n={1} | sum={2:F4} kWh | max={3:F2} W @h{4}", names[id], s.Count, sum / 3.6e6, max / 3600, imax));
            else
                sb.AppendLine(string.Format(ci, "{0} | n={1} | mean={2:F3} | min={5:F3} | max={3:F3} @h{4}", names[id], s.Count, sum / s.Count, max, imax, s.Min()));
        }
        if (!string.IsNullOrEmpty(csvPath))
        {
            List<int> ids = order.Where(i => series[i].Count > 0).ToList();
            using (StreamWriter w = new StreamWriter(csvPath))
            {
                w.WriteLine("hour0," + string.Join(",", ids.Select(i => names[i].Replace(",", ":"))));
                int n = ids.Max(i => series[i].Count);
                for (int h = 0; h < n; h++)
                    w.WriteLine(h.ToString(ci) + "," + string.Join(",", ids.Select(i => h < series[i].Count ? series[i][h].ToString("R", ci) : "")));
            }
        }
        return sb.ToString();
    }
}
