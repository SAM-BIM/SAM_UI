// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System; using System.Globalization; using System.Collections.Generic;
public static class I { public static void Main(string[] a){
 var ci=CultureInfo.InvariantCulture; var doc=new TBD.TBDDocument(); doc.openReadOnly(a[0]); var b=doc.Building;
 Console.WriteLine("zone,surface,BE,BEType,area_m2,aperture_types,function,Cd,sheltered,schedule,BE_shared_by_zones");
 var uses=new Dictionary<string,HashSet<string>>(); var rows=new List<string[]>();
 for(int i=0;;i++){ TBD.zone z=null; try{z=b.GetZone(i);}catch{} if(z==null)break;
  int miss=0; for(int k=0;k<300;k++){ TBD.zoneSurface s=null; try{s=z.GetSurface(k);}catch{} if(s==null){ if(++miss>5) break; continue;} miss=0; TBD.buildingElement e=null; try{e=s.buildingElement;}catch{} if(e==null)continue;
   if(e.BEType!=12 && e.BEType!=15) continue; if(s.type.ToString()!="tbdExposed") continue;
   string at="-",fn="-",cd="-",sh="-",sc="-"; try{ var t=e.GetApertureType(0); if(t!=null){ at=t.name; var p=t.GetProfile(); fn=p.function; cd=t.dischargeCoefficient.ToString("F3",ci); sh=t.sheltered.ToString(); sc=p.schedule==null?"none":p.schedule.name; } }catch{}
   if(!uses.ContainsKey(e.name)) uses[e.name]=new HashSet<string>(); if(at!="-") uses[e.name].Add(z.name);
   rows.Add(new string[]{z.name,k.ToString(),e.name,e.BEType.ToString(),s.area.ToString("F2",ci),at,fn,cd,sh,sc,e.name}); } }
 foreach(var r in rows){ r[10]=uses[r[2]].Count>0? string.Join("|",uses[r[2]]):"n/a"; Console.WriteLine("\""+string.Join("\",\"",r)+"\""); }
 doc.close(); } }


