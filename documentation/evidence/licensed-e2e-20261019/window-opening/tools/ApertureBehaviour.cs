// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System; using System.Collections; using System.Collections.Generic; using System.Globalization; using System.Linq;
public static class N {
 static double[] L(object o){ var r=new List<double>(); foreach(object x in (IEnumerable)o) r.Add(x==null?double.NaN:Convert.ToDouble(x,CultureInfo.InvariantCulture)); return r.ToArray(); }
 public static void Main(string[] a){ var ci=CultureInfo.InvariantCulture; var doc=new TSD.TSDDocument(); doc.openReadOnly(a[0]); var b=doc.SimulationData.GetBuildingData();
  var T=L(b.GetAnnualBuildingResult((int)TSD.tsdBuildingArray.externalTemperature));
  Console.WriteLine("zone,apertureSurfaces,paneArea_m2,hoursOpenGt0,hoursFullyOpen,meanOpenFraction,meanFlowIn_m3s,meanACH,maxACH,nightsTzGt23_at_idx22,nightsFullyOpen_00_07,nightsFullyOpen_when_not_triggered,flowDT_kWh");
  for(int z=1;;z++){ TSD.ZoneData zd=null; try{zd=b.GetZoneData(z);}catch{} if(zd==null)break; var Tz=L(zd.GetAnnualZoneResult((int)TSD.tsdZoneArray.dryBulbTemp));
   int ns=0; double area=0; double[] open=null; double[] flow=new double[8760];
   for(int s=1;;s++){ TSD.SurfaceData sd=null; try{sd=zd.GetSurfaceData(s);}catch{} if(sd==null)break; var o=L(sd.GetAnnualSurfaceResult((int)TSD.tsdSurfaceArray.apertureOpening)); var f=L(sd.GetAnnualSurfaceResult((int)TSD.tsdSurfaceArray.apertureFlowIn)); if(o.Any(v=>v>1e-9)||f.Any(v=>v>1e-9)){ ns++; area+=sd.area; if(open==null) open=o; for(int h=0;h<8760;h++) flow[h]+=f[h]; } }
   if(ns==0) continue;
   int trig=0,trigFull=0,nontrigFull=0; for(int d=0;d<364;d++){ double o=0; for(int j=24;j<=31;j++) o+=open[d*24+j]; bool full=o>=7.99; if(Tz[d*24+22]>23){trig++; if(full)trigFull++;} else if(full) nontrigFull++; }
   double dt=0; for(int h=0;h<8760;h++) dt+=flow[h]*1.2*1005*(Tz[h]-T[h]);
   Console.WriteLine(string.Join(",",new string[]{zd.name,ns.ToString(),area.ToString("F2",ci),open.Count(v=>v>0.001).ToString(),open.Count(v=>v>=0.999).ToString(),open.Average().ToString("F3",ci),flow.Average().ToString("F4",ci),(flow.Average()*3600/zd.volume).ToString("F2",ci),(flow.Max()*3600/zd.volume).ToString("F0",ci),trig.ToString(),trigFull.ToString(),nontrigFull.ToString(),(dt/1000).ToString("F0",ci)})); }
  doc.close(); } }
