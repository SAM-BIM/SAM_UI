import json, pathlib, re, hashlib, collections
root=pathlib.Path(r'C:\TasOut\pr237-g1-20261018')
def objects(path):
    text=re.sub(r'!.*$', '', path.read_text(encoding='utf-8-sig'),flags=re.M)
    return [[x.strip() for x in obj.split(',')] for obj in text.split(';') if obj.strip()]
def canonical_idf(path):
    rows=objects(path); mapping={}
    for row in rows:
        if row[0].lower()=='windowproperty:frameanddivider':
            mapping[row[1]]='FRAME_'+hashlib.sha256(json.dumps(row[2:]).encode()).hexdigest()
    rows=[[mapping.get(x,x) for x in row] for row in rows]
    for row in rows:
        if row[0].lower()=='fenestrationsurface:detailed':
            mapping[row[1]]='OPENING_'+hashlib.sha256(json.dumps(row[2:]).encode()).hexdigest()
    rows=sorted(tuple(mapping.get(x,x) for x in row) for row in rows)
    return rows
files=[p for p in (root/'explicit').rglob('in.idf')]
ref=next(p for p in files if 'benchmark-work' in str(p))
base=canonical_idf(ref)
results=[]
for path in files:
    normalized=canonical_idf(path)
    diff=collections.Counter(normalized)-collections.Counter(base)
    results.append({'path':str(path),'objects':len(normalized),'normalizedSha256':hashlib.sha256(json.dumps(normalized).encode()).hexdigest(),'sameEngineeringFields':normalized==base,'differentObjects':list(diff)[:10]})
(root/'explicit'/'idf-semantic-audit.json').write_text(json.dumps(results,indent=2))
print(json.dumps(results,indent=2)[:6000])