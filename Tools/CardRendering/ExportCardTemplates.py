"""Export blank UI frames from the user's PSD pack; never modify the sources."""
from pathlib import Path
import sys,json,hashlib,io,argparse
root=Path.cwd();sys.path.insert(0,str(root/'.tools/psd-runtime'))
from psd_tools import PSDImage
from PIL import ImageDraw
parser=argparse.ArgumentParser();parser.add_argument('--source',default='D:/BaiduNetdiskDownload/咏蛤桌游共享-万智/模板');parser.add_argument('--only',nargs='*');args=parser.parse_args()
source=Path(args.source);output=root/'Assets/Resources/UI/CardTemplates';audit=[]
colors=set('WUBRGMCLA')
def render(filename,prefix,variants,artbox):
    path=source/filename;psd=PSDImage.open(path)
    baseline={id(l):l.visible for l in psd.descendants()}
    loyalty=next((l for l in psd.descendants() if l.name=='Loyalty Badge'),None)
    if loyalty is not None:loyalty.composite().save(output/'Loyalty.png')
    def walk(group,color,parent=''):
        siblings={l.name.strip() for l in group};is_colors=len(siblings&colors)>=4
        for l in group:
            l.visible=baseline[id(l)];name=l.name.strip();p=parent+'/'+name
            selected='C' if color=='L' and parent.startswith('/UI/Textboxes') else color
            if is_colors and name in colors|{'W2','U2','C 70%','V'}:l.visible=name==selected or name=='C 70%' and selected=='C'
            if prefix=='Basic' and p=='/UI/Textboxes/Basic':l.visible=color!='L'
            if prefix=='Basic' and p=='/UI/Textboxes/Land':l.visible=color=='L'
            if p in ['/Art','/Text','/Foil','/Extra','/Art Out of Frame','/UI/Watermark']:l.visible=False
            if p=='/UI/Frames/Non-LE/Stamp':l.visible=False
            if p=='/UI/Frames/Non-LE/No Stamp':l.visible=True
            if p=='/UI/Stamp':l.visible=False
            # Loyalty/chapter rows are laid out from the actual card, not PSD examples.
            if prefix.startswith('Planeswalker') and name in ['PW3','PW4']:l.visible=name==('PW4' if prefix=='Planeswalker4' else 'PW3')
            if prefix.startswith('Planeswalker') and name=='Grey' and parent.startswith('/UI/Textboxes'):l.visible=False
            if l.is_group():walk(l,color,p)
    for color in variants:
        if args.only and prefix+color not in args.only:continue
        walk(psd,color)
        image=psd.composite(force=True,ignore_preview=True,alpha=0)
        ImageDraw.Draw(image).rectangle(artbox,fill=(0,0,0,0))
        name=prefix+color;image.save(output/(name+'.png'))
        if prefix in ['Basic','Adventure']:
            plate=next(l for l in psd.descendants() if l.name=='PT Basic')
            embedded=PSDImage.open(io.BytesIO(plate.smart_object.data))
            for layer in embedded.descendants():
                if layer.name.startswith('Text'):layer.visible=False
                if not layer.is_group() and layer.parent.name in ['Box','Normal Box','Double Box']:layer.visible=layer.name.split()[0]==color
            box=next(l for l in embedded if l.name.startswith('Box'))
            if box.bbox[2]>box.bbox[0] and box.bbox[3]>box.bbox[1]:
                embedded.composite(force=True,ignore_preview=True,alpha=0).crop(box.bbox).save(output/(name+'-pt.png'))
        audit.append(dict(source=str(path),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),output=name,size=image.size,color=color))
        print(name,flush=True)
render('001 Basic 21.12.18.psd','Basic','WUBMCLA',(47,94,550,463))
render('011 Adventure 20.4.21.psd','Adventure','WUBGM',(47,94,550,463))
render('002 Planeswalker 21.12.18.psd','Planeswalker','WUBRGMC',(47,94,550,463))
render('002 Planeswalker 21.12.18.psd','Planeswalker4','WUBRGMC',(47,94,550,409))
render('007 Saga 21.12.18.psd','Saga','WUBRGM',(298,94,550,697))
def export_symbols():
    path=source/'002 Planeswalker 21.12.18.psd'
    psd=PSDImage.open(path)
    layer=next(l for l in psd.descendants() if l.name=='Loyalty Symbol1')
    embedded=PSDImage.open(io.BytesIO(layer.smart_object.data))
    for suffix,shape in [('Plus','+'),('Minus','-'),('Zero','=')]:
        name='Loyalty'+suffix
        if args.only and name not in args.only:continue
        for l in embedded.descendants():
            l.visible=l.name=='Box' or l.parent.name=='Box' and l.name==shape
        embedded.composite(force=True,ignore_preview=True,alpha=0).crop((19,9,94,69)).save(output/(name+'.png'))
        audit.append(dict(source=str(path),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),output=name,size=(75,60)))
    path=source/'007 Saga 21.12.18.psd';psd=PSDImage.open(path)
    for chapter in ['I','II','III','IV']:
        name='Chapter'+chapter
        if args.only and name not in args.only:continue
        layer=next(l for l in psd.descendants() if l.name==chapter and l.parent.name=='Chapters')
        layer.composite().save(output/(name+'.png'))
        audit.append(dict(source=str(path),sha256=hashlib.sha256(path.read_bytes()).hexdigest(),output=name,size=layer.composite().size))
export_symbols()
out=root/'.tools/template-rollout-1008';out.mkdir(exist_ok=True,parents=True)
if args.only and (out/'export.json').exists():
    changed={a['output'] for a in audit};audit=[a for a in json.loads((out/'export.json').read_text(encoding='utf-8')) if a['output'] not in changed]+audit
(out/'export.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2),encoding='utf-8')
