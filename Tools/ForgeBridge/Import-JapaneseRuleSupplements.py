"""Import existing official Japanese printed text to local supplements, no runtime fetch."""
import base64, hashlib, json, re, time
from pathlib import Path
import requests
from JapanesePrintedText import face_rules
ROOT=Path(__file__).resolve().parents[2]
WORK=ROOT/'.tools/japanese-rule-supplements';WORK.mkdir(parents=True,exist_ok=True)
DEST=ROOT/'Assets/StreamingAssets/CardTranslations/ja-JP.supplement.txt'
REPORT=ROOT/'Tools/ForgeBridge/Reports/JapaneseRuleSupplements.json'
JA=re.compile('[\u3040-\u30ff\u3400-\u9fff]')
def rows(p):return {r[0]:r for l in p.read_text(encoding='utf-8-sig').splitlines() if len(r:=l.split('|',3))==4}
def field(t):return (t or '').replace('\\n','\n').replace('\r','').replace('\n','\\n').replace('|','｜').strip()
local={base64.b64decode(a).decode():base64.b64decode(b).decode() for a,b in (l.split('\t') for l in (ROOT/'.tools/keyword-glossary/catalog-rules.tsv').read_text().splitlines())}
base=rows(ROOT/'Assets/StreamingAssets/CardTranslations/ja-JP.txt')
supp={};evidence={}
def name_key(n):return re.sub(r'[（(][^）)]*[）)]','',n or '').strip()
name_aliases={name_key(r[1]):n for n,r in base.items() if n in local and JA.search(r[1])}
headers={'User-Agent':'MTGB-local-translation-import/1.0','Accept':'application/json'}
session=requests.Session();session.headers.update(headers)
url='https://api.scryfall.com/cards/search?q=lang%3Aja&unique=cards&order=released&dir=desc&include_extras=true';page=0
while url:
    page+=1;path=WORK/f'page-{page:03d}.json'
    if path.exists():data=json.loads(path.read_text(encoding='utf-8'))
    else:
        for attempt in range(6):
            response=session.get(url,timeout=45)
            if response.status_code in (429,502,503):time.sleep(2**attempt);continue
            response.raise_for_status()
            try:data=response.json()
            except (MemoryError,requests.exceptions.JSONDecodeError):
                response.close();time.sleep(2**min(attempt,4));continue
            response.close();break
        else:raise RuntimeError('API unavailable; cached prior pages retained')
        path.write_text(json.dumps(data,ensure_ascii=False),encoding='utf-8');time.sleep(.12)
    for card in data['data']:
        # Latest Japanese printing from the released-desc query is the first match.
        for face in card.get('card_faces') or [card]:
            name=face['name'];text=face_rules(card,face)
            if name in local and JA.search(face.get('printed_name','')):name_aliases[name_key(face['printed_name'])]=name
            if name not in local or name in supp or JA.search(base.get(name,['','','',''])[3]) or not JA.search(text):continue
            supp[name]=[name,field(face.get('printed_name') or name),field(face.get('printed_type_line')),field(text)]
            evidence[name]=dict(name=name,kind='official-japanese-printed-text',url=card['scryfall_uri'],
                apiURL=card['uri'],printingID=card['id'],oracleID=card.get('oracle_id'),set=card['set'],
                collectorNumber=card['collector_number'],released=card['released_at'],
                englishText=face.get('oracle_text',card.get('oracle_text','')),
                textSha256=hashlib.sha256(supp[name][3].encode()).hexdigest())
    url=data.get('next_page')
    if page%10==0 or not url:print(f'Pages {page}; official local Japanese supplements {len(supp)}',flush=True)

# Independent existing foreign-language data fills Japanese records absent from
# Scryfall's selected print. Do not treat Latin-only text as Japanese.
foreign=ROOT/'.tools/keyword-glossary/mtgjson-foreign-texts.json'
if foreign.exists():
    for name,languages in json.loads(foreign.read_text(encoding='utf-8')).items():
        if name not in local:continue
        for f in languages.get('Japanese',[]):
            if JA.search(f.get('name','')):name_aliases[name_key(f['name'])]=name
            if name in supp or JA.search(base.get(name,['','','',''])[3]) or not JA.search(f.get('text','')):continue
            supp[name]=[name,field(f['name']),field(f.get('type')),field(f['text'])]
            evidence[name]=dict(name=name,kind='published-japanese-foreign-data',url='https://mtgjson.com/api/v5/AtomicCards.json.xz',
                textSha256=hashlib.sha256(supp[name][3].encode()).hexdigest())

# Official release notes carry genuine Japanese where API "ja" rows still have
# English printed_text. Card blocks are separate from ruling bullet blocks.
pdf=ROOT/'.tools/keyword-glossary/JA_EOEReleaseNotes.pdf'
if pdf.exists():
    import fitz
    with fitz.open(pdf) as document:
        for page_index,page_data in enumerate(document):
            for block in page_data.get_text('blocks'):
                text=block[4].strip()
                header=re.match(r'^《([^》]+)》\s*\n',text)
                if not header:continue
                name=name_aliases.get(name_key(header[1]))
                if not name or name in supp or JA.search(base.get(name,['','','',''])[3]):continue
                lines=[l.strip() for l in text[header.end():].splitlines() if l.strip()]
                if lines and re.fullmatch(r'(?:\{[^}]+\})+',lines[0]):lines.pop(0)
                if not lines or not any(t in lines[0] for t in ('クリーチャー','アーティファクト','土地','インスタント','ソーサリー','エンチャント','プレインズウォーカー')):continue
                typ=lines.pop(0)
                lines=[l for l in lines if not re.fullmatch(r'[０-９0-9＊*]+[/／][０-９0-9＊*]+',l)]
                body='\n'.join(lines)
                if not body or '•' in body:continue
                supp[name]=[name,header[1],field(typ),field(body)]
                evidence[name]=dict(name=name,kind='official-japanese-release-notes',page=page_index+1,
                    url='https://media.wizards.com/2025/downloads/EOE_Release_Notes_R8Z03VaANE/JA_EOEReleaseNotes_20250718.pdf',
                    textSha256=hashlib.sha256(supp[name][3].encode()).hexdigest())
prepared=ROOT/'.tools/keyword-glossary/japanese-prepared-texts.json'
if prepared.exists():
    for record in json.loads(prepared.read_text(encoding='utf-8')):
        name=record['name'];text=record['text']
        if name not in local or not JA.search(text):continue
        if name in supp:
            if not JA.search(supp[name][2]):supp[name][2]=field(record['type'])
        elif not JA.search(base.get(name,['','','',''])[3]):
            supp[name]=[name,field(record['japaneseName']),field(record['type']),field(text)]
            evidence[name]=dict(name=name,kind='published-japanese-prepared-spell',url=record['url'],
                textSha256=hashlib.sha256(supp[name][3].encode()).hexdigest())

for name,target in {"A-Tyr's Blesing":"A-Tyr's Blessing",'Take a Trip to . . .':'Take a Trip to...'}.items():
    row=supp.get(target) or base.get(target)
    if name in local and row and JA.search(row[3]):
        supp[name]=[name,*row[1:]];evidence[name]=dict(name=name,kind='existing-face-name-alias',face=target)
for name in local:
    if name in supp or JA.search(base.get(name,['','','',''])[3]) or ' // ' not in name:continue
    parts=[supp.get(n) or base.get(n) for n in name.split(' // ')]
    if not all(p and JA.search(p[3]) for p in parts):continue
    supp[name]=[name,' // '.join(p[1] for p in parts),' // '.join(p[2] for p in parts),'\\n\\n'.join(p[1]+'\\n'+p[3] for p in parts)]
    evidence[name]=dict(name=name,kind='composed-existing-faces',faces=name.split(' // '))
missing=[n for n,t in local.items() if t.strip() and not JA.search((supp.get(n) or base.get(n) or ['','','',''])[3])]
DEST.write_text('\n'.join('|'.join(supp[n]) for n in sorted(supp))+'\n',encoding='utf-8')
REPORT.write_text(json.dumps(dict(source='https://api.scryfall.com/cards/search',pages=page,supplements=len(supp),
    entries=[evidence[n] for n in sorted(supp)],remaining=missing),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(f'PASS: {len(supp)} Japanese supplements; {len(missing)} remaining rule names',flush=True)
