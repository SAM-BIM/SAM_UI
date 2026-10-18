import json,pathlib,hashlib
r=pathlib.Path(r'C:\TasOut\pr237-g1-20261018')
def model(path):
 j=json.loads(path.read_text(encoding='utf-8-sig'));return j[0] if isinstance(j,list) else j
def spaces(j):return {v['Value']['Guid'].replace('-',''):v['Value']['Name'] for g in j['AdjacencyCluster']['Objects'] if g['Key']=='SAM.Analytical.Space' for v in g['Value']}
run=next((r/'isolation').glob('*_OpenStudio_*'));m=json.loads((run/'openstudio-run.json').read_text())
a=spaces(model(run/'snapshot.json'));b=spaces(model(run/m['outputs']['resultModel']));c=spaces(model(r/'inputs'/'HungaryHouse.json'));keys=[k for k,v in a.items() if v=='GF_07 LivingRoom'];assert len(keys)==1;key=keys[0]
assert a[key]==b[key]=='GF_07 LivingRoom';assert c[key]=='G1_ISOLATION_EDIT'
h=hashlib.sha256((run/'snapshot.json').read_bytes()).hexdigest();assert h.upper()=='8247570EDFE72EBC801F97A048FFD7E53207F949BAA378C591090716D001491B'
report={'status':m['status'],'startedUtc':m['startedUtc'],'finishedUtc':m['finishedUtc'],'spaceGuid':key,'snapshotName':a[key],'resultName':b[key],'liveSavedName':c[key],'snapshotSha256':h,'passed':True}
(r/'isolation'/'isolation-assertions.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))