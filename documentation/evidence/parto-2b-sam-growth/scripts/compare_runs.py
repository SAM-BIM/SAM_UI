# Before/after table for two Part O 2B run folders: .sam size, JSON size, DesignDay and ZoneSimulationResult
# counts, TM59 report equality and per-space airflow equality, per round.
# usage: python compare_runs.py [-v]   (folders below)
import zipfile,json,os,sys,collections,difflib
A=r'C:\TasOut\parto-2b-journey-2026-09-26\run'; B=r'C:\TasOut\parto-2b-sam-growth-2026-09-26\run'
pre='000000_SAM_AnalyticalModel-It1a-futureZ1'
def load(p):
    z=zipfile.ZipFile(p); n=[i for i in z.namelist() if i!='_ZipArchiveInfo'][0]; return json.loads(z.read(n)), z.getinfo(n).file_size
def grp(o,t):
    g=[g for g in o['AdjacencyCluster']['Objects'] if g['Key'].endswith('.'+t)]
    return [x['Value'] for x in g[0]['Value']] if g else []
def params(v):
    return {p['Name']:p['Value'] for ps in v.get('ParameterSets',[]) for p in ps.get('Parameters',[])}
def design(o):
    r={}
    for s in grp(o,'Space'):
        p=params(s); r[s['Name']]=tuple((k,p.get(k)) for k in sorted(p) if 'Air Flow' in k or 'Ventilation' in k)
    return r
rounds=['']+['-Opt%02d'%i for i in range(1,11)]+['-OptMax']
print('| Round | .sam before | .sam after | JSON before | JSON after | DesignDay b/a | ZoneResult b/a | TM59 report same | design airflows same |')
print('|---|--:|--:|--:|--:|--:|--:|:-:|:-:|')
for r in rounds:
    pa=os.path.join(A,pre+r+'.sam'); pb=os.path.join(B,pre+r+'.sam')
    if not os.path.exists(pb): print('|',r or 'baseline','| missing after |'); continue
    oa,ja=load(pa); ob,jb=load(pb)
    ta=open(os.path.join(A,pre+r+'-TM59.txt'),encoding='utf-8',errors='replace').read(); tb=open(os.path.join(B,pre+r+'-TM59.txt'),encoding='utf-8',errors='replace').read()
    same=ta==tb
    if not same and '-v' in sys.argv: print(''.join(list(difflib.unified_diff(ta.splitlines(1),tb.splitlines(1)))[:30]))
    print(f"| {r[1:] or 'baseline'} | {os.path.getsize(pa)/1024:,.0f} KB | {os.path.getsize(pb)/1024:,.0f} KB | {ja/1e6:,.2f} MB | {jb/1e6:,.2f} MB | {len(grp(oa,'DesignDay'))} / {len(grp(ob,'DesignDay'))} | {len(grp(oa,'ZoneSimulationResult'))} / {len(grp(ob,'ZoneSimulationResult'))} | {'yes' if same else 'NO'} | {'yes' if design(oa)==design(ob) else 'NO'} |")
