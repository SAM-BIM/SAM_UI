// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

// Read-only TSD dump (opened with openReadOnly on a COPY): every tsdZoneArray of every zone and every tsdBuildingArray,
// annual hourly series, annual sum (W-h -> kWh for load/gain arrays), max and its 0-based index; hourly CSV.
public static class TsdDump
{
    static List<double> ToList(object o)
    {
        List<double> r = new List<double>();
        IEnumerable e = o as IEnumerable; if (e == null) return r;
        foreach (object x in e) r.Add(x == null ? double.NaN : System.Convert.ToDouble(x, CultureInfo.InvariantCulture));
        return r;
    }

    public static string Run(string tsdPath, string csvPath)
    {
        CultureInfo ci = CultureInfo.InvariantCulture;
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        TSD.TSDDocument doc = new TSD.TSDDocument();
        Dictionary<string, List<double>> cols = new Dictionary<string, List<double>>();
        try
        {
            doc.openReadOnly(tsdPath);
            TSD.BuildingData b = doc.SimulationData.GetBuildingData();
            sb.AppendLine("building name=" + b.name);
            foreach (TSD.tsdBuildingArray a in Enum.GetValues(typeof(TSD.tsdBuildingArray)))
            {
                List<double> s; try { s = ToList(b.GetAnnualBuildingResult((int)a)); } catch (Exception ex) { sb.AppendLine("B." + a + " ERR " + ex.Message); continue; }
                cols["B." + a] = s;
            }
            int z = 1;
            while (true)
            {
                TSD.ZoneData zd = null; try { zd = b.GetZoneData(z); } catch { }
                if (zd == null) break;
                sb.AppendLine("zone " + z + " name=" + zd.name + " floorArea=" + zd.floorArea.ToString(ci) + " volume=" + zd.volume.ToString(ci));
                foreach (TSD.tsdZoneArray a in Enum.GetValues(typeof(TSD.tsdZoneArray)))
                {
                    List<double> s; try { s = ToList(zd.GetAnnualZoneResult((int)a)); } catch (Exception ex) { sb.AppendLine("Z" + z + "." + a + " ERR " + ex.Message); continue; }
                    cols["Z" + z + "." + a] = s;
                }
                z++;
            }
        }
        finally { try { doc.close(); } catch { } }

        foreach (KeyValuePair<string, List<double>> kv in cols)
        {
            List<double> s = kv.Value.Where(v => !double.IsNaN(v)).ToList(); if (s.Count == 0) { sb.AppendLine(kv.Key + " empty"); continue; }
            double max = s.Max(), min = s.Min(), sum = s.Sum();
            sb.AppendLine(string.Format(ci, "{0} | n={1} | sum/1000={2:F4} | mean={3:F4} | min={4:F3} @h{5} | max={6:F3} @h{7}", kv.Key, kv.Value.Count, sum / 1000, sum / s.Count, min, kv.Value.IndexOf(min), max, kv.Value.IndexOf(max)));
        }
        if (!string.IsNullOrEmpty(csvPath))
        {
            List<string> keys = cols.Keys.ToList();
            int n = keys.Max(k => cols[k].Count);
            using (StreamWriter w = new StreamWriter(csvPath))
            {
                w.WriteLine("hour0," + string.Join(",", keys));
                for (int h = 0; h < n; h++) w.WriteLine(h.ToString(ci) + "," + string.Join(",", keys.Select(k => h < cols[k].Count ? cols[k][h].ToString("R", ci) : "")));
            }
        }
        return sb.ToString();
    }
}
