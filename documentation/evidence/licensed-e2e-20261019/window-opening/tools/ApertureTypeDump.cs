// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System; using System.Globalization;
public static class A { public static void Main(string[] a){
 var doc=new TBD.TBDDocument(); doc.openReadOnly(a[0]); var b=doc.Building;
 for(int i=0;;i++){ TBD.ApertureType t=null; try{t=b.GetApertureType(i);}catch{} if(t==null)break;
  string pr="-"; try{ var p=t.GetProfile(); pr=p==null?"null":string.Format(CultureInfo.InvariantCulture,"type={0} value={1} factor={2} sb={3} sched={4}",p.type,p.value,p.factor,p.setbackValue,p.schedule==null?"-":p.schedule.name);}catch(Exception e){pr="ERR "+e.Message;}
  string fn="-"; try{ var pp=t.GetProfile(); fn=pp.function+" | desc="+pp.description; }catch{} Console.WriteLine("   FUNCTION: "+fn); Console.WriteLine("AT["+i+"] "+t.name+" Cd="+t.dischargeCoefficient+" sheltered="+t.sheltered+" profile: "+pr); }

 doc.close(); } }


