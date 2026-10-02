"""Import existing published Chinese text for missing local faces; no runtime network.

Preserves Forge translations. Stores exact source responses and per-face provenance.
Does not call the site's automatic translation endpoints or change engine rules.
"""
import base64, hashlib, json, re, time
from pathlib import Path
import requests

ROOT=Path(__file__).resolve().parents[2]
WORK=ROOT/'.tools/chinese-rule-supplements'
WORK.mkdir(parents=True,exist_ok=True)
DEST=ROOT/'Assets/StreamingAssets/CardTranslations/zh-CN.supplement.txt'
PROVENANCE=ROOT/'Tools/ForgeBridge/Reports/ChineseRuleSupplements.json'
API='https://mtgch.com/api/v1/result'
HAN=re.compile(r'[\u3400-\u9fff]')

def read_rows(path):
    if not path.exists():return {}
    return {r[0]:r for line in path.read_text(encoding='utf-8-sig').splitlines()
            if len(r:=line.split('|',3))==4}

def clean(text):
    return (text or '').replace('\\\\n','\n').replace('\\n','\n').replace('\r','').strip()

def field(text):
    return clean(text).replace('\n','\\n').replace('|','｜')

def chinese(text):return bool(HAN.search(text or ''))

def needs_translation(text):
    return not chinese(text) or bool(re.search(r"[A-Za-z][A-Za-z'’-]*(?: +[A-Za-z][A-Za-z'’-]*){3,}",text or ''))

local={}
for line in (ROOT/'.tools/keyword-glossary/catalog-rules.tsv').read_text(encoding='utf-8').splitlines():
    name,rules=[base64.b64decode(x).decode('utf-8') for x in line.split('\t')]
    local[name]=rules
base=read_rows(ROOT/'Assets/StreamingAssets/CardTranslations/zh-CN.txt')
rows=read_rows(DEST)
previous=json.loads(PROVENANCE.read_text(encoding='utf-8')) if PROVENANCE.exists() else {'entries':[]}
evidence={x['name']:x for x in previous['entries']}
# Known Forge spelling/spacing differences refer to the same translated face.
for name,target in {"A-Tyr's Blesing":"A-Tyr's Blessing", "Take a Trip to . . .":"Take a Trip to..."}.items():
    if name in local and target in base:
        row=base[target]
        rows[name]=[name,*row[1:]]
        evidence[name]=dict(name=name,kind='existing-face-name-alias',face=target,
            textSha256=hashlib.sha256(row[3].encode('utf-8')).hexdigest())
# Manually verified upstream translation refers to an obsolete test design,
# whereas the current local catalog contains only MKM/142.
CORRECTIONS={'Red Herring'}
missing=[n for n,r in local.items() if r and needs_translation(base.get(n,['','','',''])[3]) and needs_translation(rows.get(n,['','','',''])[3])]
missing += [n for n in CORRECTIONS if n not in rows]
session=requests.Session()
session.headers['User-Agent']='MTGB-local-Chinese-resource-import/1.0'

def import_face(card,response_path):
    name=card.get('face_name') or card.get('name')
    if name in local and local[name] and (name in CORRECTIONS or needs_translation(base.get(name,['','','',''])[3])):
        # Printed Simplified Chinese is preferred; otherwise use the site's existing
        # attributed translation, never generate text from the English rule ourselves.
        printed=chinese(card.get('zhs_text'))
        text=card.get('zhs_text') if printed else card.get('atomic_translated_text')
        if chinese(text):
            cn=card.get('atomic_official_name') or card.get('zhs_face_name') or card.get('zhs_name') or card.get('atomic_translated_name') or ''
            if ' // ' in cn and card.get('face_name'):cn=card.get('atomic_translated_name') or ''
            typ=card.get('zhs_type_line') if printed else card.get('atomic_translated_type')
            candidate=[name,field(cn),field(typ),field(text)]
            if name not in rows or printed and evidence.get(name,{}).get('kind')!='printed-zhs':
                rows[name]=candidate
                evidence[name]=dict(name=name,kind='printed-zhs' if printed else 'published-translation',
                    translationSource='printed-zhs' if printed else card.get('atomic_text_translated_from'),
                    url='https://mtgch.com/api/v1/card/'+card['set']+'/'+card['collector_number']+'/',
                    oracleID=card.get('oracle_id'),faceIndex=card.get('face_index'),
                    englishText=card.get('oracle_text'),response=str(response_path.relative_to(ROOT)),
                    textSha256=hashlib.sha256(candidate[3].encode('utf-8')).hexdigest())
                if name in CORRECTIONS:
                    evidence[name]['replacesPrimary']=base[name][3]
                    evidence[name]['reason']='MKM/142 rules differ from obsolete upstream test-card translation'
    for other in card.get('other_faces') or []:import_face(other,response_path)

for start in range(0,len(missing),10):
    names=missing[start:start+10]
    query=' or '.join('name:'+json.dumps(n,ensure_ascii=False) for n in names)
    page=1
    while True:
        key=hashlib.sha256((query+'|'+str(page)).encode()).hexdigest()[:20]
        path=WORK/(key+'.json')
        if path.exists():data=json.loads(path.read_text(encoding='utf-8'))
        else:
            for attempt in range(4):
                response=session.get(API,params=dict(q=query,page=page,page_size=100,unique='oracle_id',include_fav='false'),timeout=35)
                if response.status_code==429:
                    time.sleep(min(30,2**attempt*3));continue
                response.raise_for_status();data=response.json();break
            else:raise RuntimeError('Rate limit persisted; preserved responses and stopped.')
            path.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
            time.sleep(.35)
        for card in data.get('items',[]):import_face(card,path)
        if page>=data.get('total_pages',1):break
        page+=1
    print(f'Queried {min(start+10,len(missing))}/{len(missing)}; local supplements {len(rows)}',flush=True)

# Forge stores some composite identities as well as individual face names. Join
# existing translated faces rather than leave the composite's summary in English.
for name in missing:
    if name in rows or ' // ' not in name:continue
    faces=[base.get(n) or rows.get(n) for n in name.split(' // ')]
    if not all(f and chinese(f[3]) for f in faces):continue
    cn=' // '.join(f[1] for f in faces)
    rules='\\n\\n'.join(f[1]+'\\n'+f[3] for f in faces)
    rows[name]=[name,cn,' // '.join(f[2] for f in faces),rules]
    evidence[name]=dict(name=name,kind='composed-existing-faces',faces=name.split(' // '),
                       textSha256=hashlib.sha256(rules.encode('utf-8')).hexdigest())

for n,r in rows.items():
    if 'CARDNAME' in r[3] and chinese(r[1]):
        r[3]=r[3].replace('CARDNAME',r[1])
        evidence[n]['normalization']='Expanded CARDNAME placeholder using published Chinese name'
        evidence[n]['textSha256']=hashlib.sha256(r[3].encode('utf-8')).hexdigest()
    assert len(r)==4 and r[0]==n and chinese(r[3]),'Invalid supplement '+n
    assert not any('\n' in x or '\r' in x or '|' in x for x in r),'Invalid fields '+n
DEST.write_text('\n'.join('|'.join(rows[n]) for n in sorted(rows))+'\n',encoding='utf-8')
corrections=[dict(name=n,expectedRules=evidence[n]['replacesPrimary'].replace('\\n','\n'))
             for n in sorted(rows) if evidence[n].get('replacesPrimary')]
(DEST.parent/'zh-CN.corrections.json').write_text(json.dumps(dict(entries=corrections),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
remaining=[n for n,r in local.items() if r and needs_translation(base.get(n,['','','',''])[3]) and needs_translation(rows.get(n,['','','',''])[3])]
PROVENANCE.parent.mkdir(parents=True,exist_ok=True)
PROVENANCE.write_text(json.dumps(dict(source=API,entries=[evidence[n] for n in sorted(rows)],remaining=remaining,
    initialMissing=previous.get('initialMissing',len(missing)),supplements=len(rows)),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'PASS: {len(rows)} local supplements; {len(remaining)} remaining without a published Chinese effect text.')
