// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System; using System.IO; using System.Globalization;
public static class R { public static int Main(string[] a){
 string tbd=a[0], tsd=a[1], mode=a[2];
 var doc=new TBD.TBDDocument(); doc.open(tbd); var b=doc.Building;
 if(mode=="close"){
  TBD.zone z=null; for(int i=1;;i++){ TBD.zone t=null; try{t=b.GetZone(i);}catch{} if(t==null)break; if(t.name=="GF_08 Bathroom"){z=t;break;} }
  if(z==null){Console.WriteLine("GF_08 not found");return 2;}
  TBD.zoneSurface target=null; for(int k=0;;k++){ TBD.zoneSurface s=null; try{s=z.GetSurface(k);}catch{} if(s==null)break; string n="-"; try{n=s.buildingElement.name;}catch{} if(n.StartsWith("Windows: SIM_EXT_GLZ_57232B2B -pane")){target=s;break;} }
  if(target==null){Console.WriteLine("target pane not found");return 3;}
  var old=target.buildingElement; var nb=b.AddBuildingElement();
  nb.name=old.name+" [GF_08 closed]"; nb.BEType=old.BEType; nb.colour=old.colour; nb.width=old.width; nb.ghost=old.ghost; nb.ground=old.ground; nb.description=old.description;
  nb.AssignConstruction(old.GetConstruction());
  target.buildingElement=nb;
  Console.WriteLine("reassigned GF_08 pane from '"+old.name+"' to '"+nb.name+"' (area "+target.area.ToString("F3",CultureInfo.InvariantCulture)+"), aperture types on new BE: "+(Try(()=>nb.GetApertureType(0))==null?"none":"PRESENT"));
  doc.save(); }
 if(mode=="closeall"){
  int removed=0; for(int i=0;i<b.GetBECount();i++){ TBD.buildingElement e=null; try{e=b.GetBuildingElement(i);}catch{} if(e==null)continue;
   while(true){ TBD.ApertureType t=null; try{t=e.GetApertureType(0);}catch{} if(t==null)break; Console.WriteLine("removing aperture type '"+t.name+"' from BE '"+e.name+"'"); e.RemoveApertureType(0); removed++; if(removed>50)break; } }
  Console.WriteLine("aperture types detached: "+removed); doc.save(); } if(File.Exists(tsd)) File.Delete(tsd);
 var sw=System.Diagnostics.Stopwatch.StartNew();
 doc.simulate(1,365,0,1,0,0,tsd,1,0);
 Console.WriteLine("simulate returned after "+sw.Elapsed.TotalSeconds.ToString("F1")+" s");
 doc.save(); doc.close(); return 0; }
 static object Try(Func<object> f){ try{return f();}catch{return null;} } }
