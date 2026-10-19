// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System; using System.Collections; using System.Collections.Generic; using System.Globalization; using System.IO; using System.Text; using System.Linq;
public static class S {
 static double[] L(object o){ var r=new List<double>(); if(o==null) return r.ToArray(); foreach(object x in (IEnumerable)o) r.Add(x==null?double.NaN:Convert.ToDouble(x,CultureInfo.InvariantCulture)); return r.ToArray(); }
 public static void Main(string[] a){
  var doc=new TSD.TSDDocument(); doc.openReadOnly(a[0]); var b=doc.SimulationData.GetBuildingData();
  string csv = a.Length>1? a[1]:null; int zsel = a.Length>2? int.Parse(a[2]):-1;
  for(int z=1;;z++){ TSD.ZoneData zd=null; try{zd=b.GetZoneData(z);}catch{} if(zd==null)break;
   for(int s=1;;s++){ TSD.SurfaceData sd=null; try{sd=zd.GetSurfaceData(s);}catch{} if(sd==null)break;
    var o=L(sd.GetAnnualSurfaceResult((int)TSD.tsdSurfaceArray.apertureOpening)); var fi=L(sd.GetAnnualSurfaceResult((int)TSD.tsdSurfaceArray.apertureFlowIn)); var fo=L(sd.GetAnnualSurfaceResult((int)TSD.tsdSurfaceArray.apertureFlowOut));
    bool any = o.Any(v=>Math.Abs(v)>1e-9&&v!=-1) || fi.Any(v=>Math.Abs(v)>1e-9&&v!=-1) || fo.Any(v=>Math.Abs(v)>1e-9&&v!=-1);
    if(sd.BEType==12 || any) Console.WriteLine(string.Format(CultureInfo.InvariantCulture,"Z{0} {1} surf{2} BE={3} type={4} area={5:F2} openN={6} openMax={7:F3} openMean={8:F4} flowInMax={9:F4} flowInSum={10:F1} flowOutMax={11:F4} anyNonzero={12}",z,zd.name,s,sd.BEName,sd.BEType,sd.area,o.Length,o.Length>0?o.Max():double.NaN,o.Length>0?o.Average():double.NaN,fi.Length>0?fi.Max():double.NaN,fi.Sum(),fo.Length>0?fo.Max():double.NaN,any));
    if(csv!=null && z==zsel && any){ var sb=new StringBuilder("h,open,flowIn,flowOut\n"); for(int h=0;h<o.Length;h++) sb.Append(h+","+o[h].ToString("R",CultureInfo.InvariantCulture)+","+fi[h].ToString("R",CultureInfo.InvariantCulture)+","+fo[h].ToString("R",CultureInfo.InvariantCulture)+"\n"); File.WriteAllText(csv.Replace(".csv","_s"+s+".csv"),sb.ToString()); } } }
  doc.close(); } }

