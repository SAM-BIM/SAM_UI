// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors

using System; using System.Collections.Generic; using System.Globalization; using System.IO; using System.Linq; using System.Text; using System.Text.RegularExpressions;
// ESO -> CSV. args: eso, outCsv, keyVarRegex, freq (Hourly|Timestep). Weather-file run period only (last environment).
public static class EsoCsv { public static void Main(string[] a){
 var ci=CultureInfo.InvariantCulture; var dict=new Dictionary<int,string>(); var sel=new Dictionary<int,int>(); var names=new List<string>(); var rx=new Regex(a[2]); string freq=a[3];
 var rows=new List<double[]>(); double[] cur=null; int env=0; bool inData=false; int lastEnvIdx=0; var stamps=new List<string>(); int nsel=0; double[] curRow=null; bool wantStamp=false; var envRows=new Dictionary<int,List<double[]>>(); var envStamps=new Dictionary<int,List<string>>();
 foreach(var line in File.ReadLines(a[0])){
  if(!inData){ if(line.StartsWith("End of Data Dictionary")){ inData=true; continue; } var p=line.Split(','); int id; if(p.Length>=4 && int.TryParse(p[0],out id) && id>=7){ var rest=string.Join(",",p.Skip(2)); var m=Regex.Match(rest,@"^(.*?)\s*!(\w+)"); if(m.Success){ string kv=m.Groups[1].Value; string f=m.Groups[2].Value; if(f.Equals(freq,StringComparison.OrdinalIgnoreCase) && rx.IsMatch(kv)){ sel[id]=names.Count; names.Add(kv.Replace(",",";")); } } } continue; }
  if(line.StartsWith("End of Data")) break;
  var q=line.Split(','); int rid; if(!int.TryParse(q[0],out rid)) continue;
  if(rid==1){ env++; envRows[env]=new List<double[]>(); envStamps[env]=new List<string>(); curRow=null; continue; }
  if(rid==2){ // time stamp
    double sm=double.Parse(q[6],ci), em=double.Parse(q[7],ci); bool ok = freq.Equals("Hourly",StringComparison.OrdinalIgnoreCase)? (em-sm==60) : (em-sm!=60 && em-sm>0);
    if(freq.Equals("Timestep",StringComparison.OrdinalIgnoreCase)) ok = true;
    if(ok){ curRow=Enumerable.Repeat(double.NaN,names.Count).ToArray(); if(!envRows.ContainsKey(env)){envRows[env]=new List<double[]>();envStamps[env]=new List<string>();} envRows[env].Add(curRow); envStamps[env].Add(string.Join("-",q[2],q[3],q[5],q[6])); } else curRow=null; continue; }
  if(rid>=7 && curRow!=null && sel.ContainsKey(rid)){ curRow[sel[rid]]=double.Parse(q[1],ci); } }
 int use=envRows.Keys.Max(); if(a.Length>4 && a[4].StartsWith("env=")) use=int.Parse(a[4].Substring(4)); var sb=new StringBuilder("stamp,"+string.Join(",",names)+"\n");
 for(int i=0;i<envRows[use].Count;i++) sb.AppendLine(envStamps[use][i]+","+string.Join(",",envRows[use][i].Select(v=>double.IsNaN(v)?"":v.ToString("R",ci))));
 File.WriteAllText(a[1],sb.ToString()); Console.WriteLine("env "+use+" rows "+envRows[use].Count+" cols "+names.Count); } }


