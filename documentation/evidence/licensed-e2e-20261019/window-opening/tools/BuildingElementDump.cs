// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System; using System.Globalization;
public static class D { public static void Main(string[] a){
 var doc=new TBD.TBDDocument(); doc.openReadOnly(a[0]); var b=doc.Building;
 for(int i=0;;i++){ TBD.buildingElement e=null; try{e=b.GetBuildingElement(i);}catch{} if(e==null)break;
  var ats=new System.Collections.Generic.List<string>(); for(int k=0;k<4;k++){ TBD.ApertureType t=null; try{t=e.GetApertureType(k);}catch{} if(t==null)break; string fn="-"; try{fn=t.GetProfile().function;}catch{} ats.Add(t.name+" ["+fn+"]"); }
  if(e.BEType!=12 && ats.Count==0) continue;
  var zs=new System.Collections.Generic.List<string>(); for(int k=0;;k++){ TBD.zoneSurface s=null; try{s=e.zoneSurfaces(k);}catch{} if(s==null)break; string zn="?"; try{zn=s.zone.name;}catch{} zs.Add(zn+":"+s.area.ToString("F2",CultureInfo.InvariantCulture)); }
  Console.WriteLine("BE["+i+"] "+e.name+" type="+e.BEType+" ATs="+string.Join(" ; ",ats)+" surfaces="+string.Join(", ",zs)); }
 doc.close(); } }
