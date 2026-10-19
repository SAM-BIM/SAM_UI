// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System; using System.Collections; using System.Collections.Generic; using System.Globalization; using System.IO; using System.Linq; using System.Text;
public static class E {
 static double[] L(object o){ var r=new List<double>(); foreach(object x in (IEnumerable)o) r.Add(x==null?double.NaN:Convert.ToDouble(x,CultureInfo.InvariantCulture)); return r.ToArray(); }
 public static void Main(string[] a){
  var ci=CultureInfo.InvariantCulture; var doc=new TSD.TSDDocument(); doc.openReadOnly(a[0]); var b=doc.SimulationData.GetBuildingData();
  var T=L(b.GetAnnualBuildingResult((int)TSD.tsdBuildingArray.externalTemperature));
  var bh=L(b.GetAnnualBuildingResult((int)TSD.tsdBuildingArray.heatingProfile));
  var sb=new StringBuilder("zone,name,heat_kWh,heatMax_W,hoursAtCap,infVent_kWh,bldgHT_kWh,solar_kWh,extCondOp_kWh,extCondGl_kWh,meanTz,hoursTzBelow23.9,equip_kWh,occ_kWh,light_kWh,apertureOpenMean,apertureFlowMean_m3s\n");
  var hourly=new StringBuilder("h,Text,Tz,heat,infVent,bHT,open,flowIn,flowOut\n");
  for(int z=1;;z++){ TSD.ZoneData zd=null; try{zd=b.GetZoneData(z);}catch{} if(zd==null)break;
   Func<TSD.tsdZoneArray,double[]> g=ar=>L(zd.GetAnnualZoneResult((int)ar));
   var H=g(TSD.tsdZoneArray.heatingLoad); var IV=g(TSD.tsdZoneArray.infVentGain); var BT=g(TSD.tsdZoneArray.buildingHeatTransfer); var So=g(TSD.tsdZoneArray.solarGain); var Co=g(TSD.tsdZoneArray.externalConductionOpaque); var Cg=g(TSD.tsdZoneArray.externalConductionGlazing); var Tz=g(TSD.tsdZoneArray.dryBulbTemp);
   var Eq=g(TSD.tsdZoneArray.equipmentSensibleGain); var Oc=g(TSD.tsdZoneArray.occupantSensibleGain); var Li=g(TSD.tsdZoneArray.lightingGain);
   double mx=H.Max(); int cap=H.Count(v=>v>mx-0.5 && mx>0);
   double om=0,fm=0; int ns=0; double[] oo=null,fi=null,fo=null;
   for(int s=1;;s++){ TSD.SurfaceData sd=null; try{sd=zd.GetSurfaceData(s);}catch{} if(sd==null)break; var o=L(sd.GetAnnualSurfaceResult((int)TSD.tsdSurfaceArray.apertureOpening)); var f1=L(sd.GetAnnualSurfaceResult((int)TSD.tsdSurfaceArray.apertureFlowIn)); if(o.Any(v=>v>1e-9) || f1.Any(v=>v>1e-9)){ ns++; om+=o.Average(); fm+=f1.Average(); if(oo==null){oo=o;fi=f1;fo=L(sd.GetAnnualSurfaceResult((int)TSD.tsdSurfaceArray.apertureFlowOut));} } }
   sb.AppendLine(string.Join(",",new string[]{z.ToString(),zd.name,(H.Sum()/1000).ToString("F3",ci),mx.ToString("F2",ci),cap.ToString(),(IV.Sum()/1000).ToString("F3",ci),(BT.Sum()/1000).ToString("F3",ci),(So.Sum()/1000).ToString("F3",ci),(Co.Sum()/1000).ToString("F3",ci),(Cg.Sum()/1000).ToString("F3",ci),Tz.Average().ToString("F3",ci),Tz.Count(v=>v<23.9).ToString(),(Eq.Sum()/1000).ToString("F3",ci),(Oc.Sum()/1000).ToString("F3",ci),(Li.Sum()/1000).ToString("F3",ci),ns>0?(om/ns).ToString("F4",ci):"",ns>0?fm.ToString("F5",ci):""}));
   if(z==4||z==11){ for(int h=0;h<8760;h++) hourly.AppendLine(string.Join(",",new string[]{ h.ToString(),T[h].ToString("R",ci),Tz[h].ToString("R",ci),H[h].ToString("R",ci),IV[h].ToString("R",ci),BT[h].ToString("R",ci), oo!=null?oo[h].ToString("R",ci):"", fi!=null?fi[h].ToString("R",ci):"", fo!=null?fo[h].ToString("R",ci):"" })); File.WriteAllText(a[1]+"_hourly_Z"+z+".csv",hourly.ToString()); hourly.Clear(); hourly.AppendLine("h,Text,Tz,heat,infVent,bHT,open,flowIn,flowOut"); } }
  File.WriteAllText(a[1]+"_zones.csv",sb.ToString());
  Console.WriteLine("building heatingProfile kWh = "+(bh.Sum()/1000).ToString("F3",ci));
  doc.close(); } }
