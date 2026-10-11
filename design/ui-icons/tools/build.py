"""Generate UI command icons with the unchanged SAM#179 glyph/badge renderer.

manifest.json is authoritative. Canonical SVGs use the approved 24x24 canvas;
PNG sizes preserve existing UI asset dimensions or the existing 32px command slot.
The vendor files are byte copies of SAM#179 and the frozen SAM-BIM extension.
"""
import concurrent.futures, hashlib, json, os, pathlib, sys, tempfile
from PIL import Image
HERE=pathlib.Path(__file__).resolve().parent
DESIGN=HERE.parent
REPO=DESIGN.parent.parent
sys.path.insert(0,str(HERE/'vendor'))
import icons, icons_ext, render

def main():
    rows=json.loads((DESIGN/'manifest.json').read_text(encoding='utf-8'))
    specs={r['icon_id']:r for r in rows}
    svgdir=DESIGN/'svg';svgdir.mkdir(exist_ok=True)
    for iid,r in specs.items():
        svg=icons.icon_svg(r['object'],r['op'],plural=r.get('plural',False),container=r.get('container'),comment='SAM UI icon '+iid)
        (svgdir/(iid+'.svg')).write_text(svg,encoding='utf-8',newline='\n')
    jobs={}
    for r in rows:jobs.setdefault((r['icon_id'],tuple(r['size'])),[]).append(REPO/r['output'])
    def one(job):
        (iid,(w,h)),outputs=job
        with tempfile.TemporaryDirectory(prefix='sam-ui-icon-') as tmp:
            page=pathlib.Path(tmp)/'icon.html';shot=pathlib.Path(tmp)/'icon.png'
            url=(svgdir/(iid+'.svg')).as_uri()
            page.write_text(f'<!DOCTYPE html><body style="margin:0;background:transparent"><img src="{url}" width="{w}" height="{h}" style="position:absolute;left:0;top:0"></body>',encoding='utf-8')
            render._shot(str(page),str(shot),max(w,64),max(h,64))
            im=Image.open(shot).convert('RGBA').crop((0,0,w,h))
            for output in outputs:
                output.parent.mkdir(parents=True,exist_ok=True)
                im.save(output,optimize=True)
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:list(pool.map(one,jobs.items()))
    for r in rows:
        im=Image.open(REPO/r['output']);assert im.size==tuple(r['size']) and im.mode=='RGBA'
    # Native-size contact sheet, preserving alpha on light, orange and dark backgrounds.
    sample=[]
    for iid in sorted(specs):
        r=next(x for x in rows if x['icon_id']==iid)
        sample.append((iid,Image.open(REPO/r['output']).convert('RGBA')))
    from PIL import ImageDraw
    cols=4;cellw=300;cellh=84
    sheet=Image.new('RGB',(cols*cellw,((len(sample)+cols-1)//cols)*cellh),'white');draw=ImageDraw.Draw(sheet)
    for i,(iid,im) in enumerate(sample):
        x=(i%cols)*cellw;y=(i//cols)*cellh
        draw.text((x+5,y+5),iid,fill='#111111')
        for j,bg in enumerate(['#d8d8d8','#f5a644','#353535']):
            xx=x+6+j*72;yy=y+24
            draw.rectangle((xx,yy,xx+60,yy+55),fill=bg)
            sheet.paste(im,(xx+(60-im.width)//2,yy+(55-im.height)//2),im)
    review=DESIGN/'review';review.mkdir(exist_ok=True);sheet.save(review/'contact_sheet.png')
    print(f'{len(rows)} UI asset references, {len(specs)} distinct approved icons generated')
if __name__=='__main__':main()
