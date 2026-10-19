// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System; using System.Globalization; using System.Text;
public static class P {
 static string Pr(TBD.profile p){ if(p==null) return "null"; return string.Format(CultureInfo.InvariantCulture,"type={0} value={1} factor={2} sb={3} sched={4}",p.type,p.value,p.factor,p.setbackValue,p.schedule==null?"-":p.schedule.name);}
 public static void Main(string[] a){
  TBD.TBDDocument doc=new TBD.TBDDocument(); doc.openReadOnly(a[0]); TBD.Building b=doc.Building;
  Console.WriteLine("== IZAMs");
  for(int i=0;;i++){ TBD.IZAM z=null; try{z=b.GetIZAM(i);}catch{} if(z==null)break;
   string s="?",t=""; try{s=z.GetSourceZone().name;}catch{} for(int q=0;;q++){ try{ var tz=z.GetTargetZone(q); if(tz==null)break; t+=(q==0?"":",")+tz.name; }catch{break;} }
   Console.WriteLine("IZAM["+i+"] "+z.name+" fromOutside="+z.fromOutside+" src="+s+" tgt="+t);
   { TBD.profile p=null; try{p=z.GetProfile();}catch{} if(p!=null) Console.WriteLine("   profile: "+Pr(p)); } }
  Console.WriteLine("== ICs");
  for(int i=0;;i++){ TBD.InternalCondition ic=null; try{ic=b.GetIC(i);}catch{} if(ic==null)break; Console.WriteLine("IC["+i+"] "+ic.name); TBD.InternalGain g=ic.GetInternalGain(); if(g!=null){ Console.WriteLine("  freshAirRate="+g.freshAirRate); foreach(TBD.Profiles pr in Enum.GetValues(typeof(TBD.Profiles))){TBD.profile p=null; try{p=g.GetProfile((int)pr);}catch{} if(p!=null&&(pr==TBD.Profiles.ticI||pr==TBD.Profiles.ticV)) Console.WriteLine("   "+pr+": "+Pr(p));}} TBD.Thermostat t=ic.GetThermostat(); if(t!=null){ foreach(TBD.Profiles pr in Enum.GetValues(typeof(TBD.Profiles))){TBD.profile p=null; try{p=t.GetProfile((int)pr);}catch{} if(p!=null) Console.WriteLine("   thermo "+pr+": "+Pr(p));}} }
  Console.WriteLine("== Zones");
  for(int i=1;;i++){ TBD.zone z=null; try{z=b.GetZone(i);}catch{} if(z==null)break; string icn="-"; try{icn=z.GetIC(0).name;}catch{try{icn=z.GetIC(1).name;}catch{}}
   Console.WriteLine("Z"+i+" "+z.name+" IC="+icn+" vol="+z.volume+" maxHeat="+z.maxHeatingLoad+" sizeHeat="+z.sizeHeating);
   for(int k=0;;k++){ TBD.zoneSurface s=null; try{s=z.GetSurface(k);}catch{} if(s==null)break; string link="-"; try{ if(s.linkSurface!=null) link=s.linkSurface.zone.name;}catch{}
    string be="-",bt="-"; try{be=s.buildingElement.name; bt=s.buildingElement.BEType.ToString();}catch{}
    Console.WriteLine(string.Format(CultureInfo.InvariantCulture,"   S{0} type={1} area={2:F2} BE={3} BEType={4} link={5}",k,s.type,s.area,be,bt,link)); } }
  doc.close(); } }

