import json,pathlib,hashlib
root=pathlib.Path(r'C:\TasOut\pr237-g1-20261018')
def numeric(value,path=''):
    if isinstance(value,bool):return {}
    if isinstance(value,(int,float)):return {path:value}
    if isinstance(value,list):
        out={}
        for i,v in enumerate(value):out.update(numeric(v,path+'/'+str(i)))
        return out
    if isinstance(value,dict):
        out={}
        for k,v in value.items():
            if k not in ('Guid','Reference','DateTime'):out.update(numeric(v,path+'/'+k))
        return out
    return {}
def extract(folder):
    j=json.loads((root/folder/'000000_SAM_AnalyticalModel.json').read_text(encoding='utf-8-sig')); j=j[0] if isinstance(j,list) and len(j)==1 else j;out={};strings={};counts={}
    for group in j['AdjacencyCluster']['Objects']:
        if not group['Key'].endswith('SimulationResult'):continue
        counts[group['Key']]=len(group['Value'])
        for entry in group['Value']:
            obj=entry['Value'];params={}
            for ps in obj.get('ParameterSets',[]):
                for p in ps.get('Parameters',[]):
                    if p.get('Name') and p['Name'] not in ('Zone Guid','ZoneSurfaceReference'):params[p['Name']]=p.get('Value')
            key=group['Key']+'/'+obj.get('Name','')+'/'+str(params.get('Load Type'))
            if group['Key'].endswith('SurfaceSimulationResult'):key+='/'+str(obj.get('Reference'))
            if key in counts:raise Exception('duplicate result identity '+key)
            counts[key]=1
            out.update(numeric(params,key))
    return out,{k:v for k,v in counts.items() if '/' not in k}
a,ac=extract('tas-baseline');b,bc=extract('tas-candidate')
diffs=[{'path':k,'baseline':a.get(k),'candidate':b.get(k)} for k in sorted(a.keys()|b.keys()) if a.get(k)!=b.get(k)]
report={'criterion':'Exact equality of every numeric leaf of persisted simulation parameters, aligned by result type/name/load type and surface reference. No numerical tolerance. Run-generated result/zone identities and timestamps are metadata, not numerical outputs.','baselineCounts':ac,'candidateCounts':bc,'baselineNumbers':len(a),'candidateNumbers':len(b),'exactMatch':not diffs,'differences':diffs}
(root/'tas-comparison.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2)[:7000])