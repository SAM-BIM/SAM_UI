// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System;
using System.Globalization;
using System.Text;

// Read-only TBD dump (copy, openReadOnly): internal conditions as Tas received them.
public static class TbdDump
{
    static string P(TBD.profile p)
    {
        if (p == null) return "null";
        return string.Format(CultureInfo.InvariantCulture, "type={0} value={1} factor={2} setback={3} schedule={4}", p.type, p.value, p.factor, p.setbackValue, p.schedule == null ? "-" : p.schedule.name);
    }

    public static string Run(string tbdPath)
    {
        StringBuilder sb = new StringBuilder();
        TBD.TBDDocument doc = new TBD.TBDDocument();
        try
        {
            doc.openReadOnly(tbdPath);
            TBD.Building b = doc.Building;
            sb.AppendLine("building=" + b.name + " latitude=" + b.latitude + " longitude=" + b.longitude + " timeZone=" + b.timeZone);
            for (int i = 0; ; i++)
            {
                TBD.InternalCondition ic = null; try { ic = b.GetIC(i); } catch { }
                if (ic == null) break;
                sb.AppendLine("IC[" + i + "] " + ic.name + " includeSolarInMRT=" + ic.includeSolarInMRT);
                TBD.InternalGain g = ic.GetInternalGain();
                if (g != null)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  gain {0}: lightingRadProp={1} occupantRadProp={2} equipmentRadProp={3} freshAirRate={4} personGain={5} targetIlluminance={6}", g.name, g.lightingRadProp, g.occupantRadProp, g.equipmentRadProp, g.freshAirRate, g.personGain, g.targetIlluminance));
                    foreach (TBD.Profiles pr in Enum.GetValues(typeof(TBD.Profiles)))
                    {
                        TBD.profile p = null; try { p = g.GetProfile((int)pr); } catch { }
                        if (p != null) sb.AppendLine("    " + pr + ": " + P(p));
                    }
                }
                TBD.Emitter he = ic.GetHeatingEmitter(); if (he != null) sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  heatingEmitter {0} radiantProportion={1} viewCoefficient={2}", he.name, he.radiantProportion, he.viewCoefficient));
                TBD.Emitter ce = ic.GetCoolingEmitter(); if (ce != null) sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  coolingEmitter {0} radiantProportion={1} viewCoefficient={2}", ce.name, ce.radiantProportion, ce.viewCoefficient));
                TBD.Thermostat t = ic.GetThermostat();
                if (t != null)
                {
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "  thermostat {0} radiantProportion={1} controlRange={2} proportionalControl={3}", t.name, t.radiantProportion, t.controlRange, t.proportionalControl));
                    foreach (TBD.Profiles pr in Enum.GetValues(typeof(TBD.Profiles)))
                    {
                        TBD.profile p = null; try { p = t.GetProfile((int)pr); } catch { }
                        if (p != null) sb.AppendLine("    " + pr + ": " + P(p));
                    }
                }
            }
        }
        finally { try { doc.close(); } catch { } }
        return sb.ToString();
    }
}
