# Per-type object count / compact JSON size in a Part O run's saved .sam models, one column per round.
# usage: python cluster_counts.py <run folder> "" -Opt01 -Opt08 ...   ("" = the Iteration 2 baseline)
import zipfile,json,sys,collections,os
def load(f):
    z=zipfile.ZipFile(f); n=[i for i in z.namelist() if i!='_ZipArchiveInfo'][0]
    return json.loads(z.read(n))
def size(o): return len(json.dumps(o,separators=(',',':')))
pre='000000_SAM_AnalyticalModel-It1a-futureZ1'
rows={}
for s in sys.argv[2:]:
    o=load(os.path.join(sys.argv[1],pre+s+'.sam')); r={}
    for g in o['AdjacencyCluster']['Objects']:
        r[g['Key'].split('.')[-1]]=(len(g['Value']), sum(size(x) for x in g['Value']))
    r['#Relations']=(len(o['AdjacencyCluster']['Relations']),size(o['AdjacencyCluster']['Relations']))
    rows[s or 'base']=r
keys=sorted(set(k for r in rows.values() for k in r))
print('type'.ljust(34)+''.join(f'{s:>20}' for s in rows))
for k in keys: print(k.ljust(34)+''.join(f"{str(rows[s].get(k,('-','-'))[0])+'/'+str(rows[s].get(k,('-','-'))[1]):>20}" for s in rows))
