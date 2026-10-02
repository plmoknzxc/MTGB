"""Audit local Chinese rule coverage without equating one Han character to completeness."""
import base64, collections, json, re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
HAN=re.compile(r'[\u3400-\u9fff]')
def rows(path):
    return {r[0]:r for l in path.read_text(encoding='utf-8-sig').splitlines() if len(r:=l.split('|',3))==4}
base=rows(ROOT/'Assets/StreamingAssets/CardTranslations/zh-CN.txt')
supp=rows(ROOT/'Assets/StreamingAssets/CardTranslations/zh-CN.supplement.txt')
merged={n:r.copy() for n,r in base.items()}
corrections=json.loads((ROOT/'Assets/StreamingAssets/CardTranslations/zh-CN.corrections.json').read_text(encoding='utf-8'))['entries']
corrected={c['name']:c['expectedRules'] for c in corrections}
for n,r in supp.items():
    if n not in merged:merged[n]=r.copy();continue
    for i in range(1,4):
        if n in corrected and base[n][3].replace('\\n','\n')==corrected[n] or not merged[n][i].strip() or not HAN.search(merged[n][i]) or i==3 and re.search(r"[A-Za-z][A-Za-z'’-]*(?: +[A-Za-z][A-Za-z'’-]*){3,}",merged[n][i]):merged[n][i]=r[i]
local={}
for l in (ROOT/'.tools/keyword-glossary/catalog-rules.tsv').read_text(encoding='utf-8').splitlines():
    n,t=[base64.b64decode(x).decode() for x in l.split('\t')];local[n]=t
faces={}; printings=0
for l in (ROOT/'ForgeRuntime/catalog.jsonl').read_text(encoding='utf-8').splitlines():
    c=json.loads(l);printings+=1
    for f in c['faces']:faces[f['name']]=f.get('text','')
def inspect(catalog,translations):
    out=collections.defaultdict(list)
    for n,en in catalog.items():
        if not en.strip():out['noEnglishRules'].append(n);continue
        r=translations.get(n)
        if not r:out['missingRecord'].append(n);continue
        text=r[3].replace('\\n','\n')
        if not HAN.search(text):out['noChineseRules'].append(n);continue
        residual=re.sub(r'<[^>]*>|\{[^}]*\}|//Level_\d+//','',text).replace(n,'').replace(r[1],'')
        fragments=[c.strip() for c in re.findall(r"[A-Za-z][A-Za-z' -]*",residual)
                   if len(c.strip())>2 and c.strip() not in ('III','VIII','VII','VERT','VERT X')]
        if fragments:out['residualLatinReview'].append(dict(name=n,fragments=fragments))
        # Candidate only: names, quotations and foreign keyword labels need review.
        chunks=re.findall(r"[A-Za-z][A-Za-z'’—– -]*(?:[.!?]|$)",re.sub(r'\{[^}]*\}','',text))
        chunks=[x.strip() for x in chunks if len(re.findall(r'[A-Za-z]+',x))>=4]
        if chunks:out['mixedLanguageCandidates'].append(dict(name=n,chunks=chunks,text=text,english=en))
        else:out['hasChineseRules'].append(n)
    return dict(counts={k:len(v) for k,v in out.items()},details=dict(out))
result=dict(uniqueExportNames=len(local),uniqueCatalogFaces=len(faces),printings=printings,
    before=inspect(local,base),after=inspect(local,merged),catalogFacesAfter=inspect(faces,merged))
dest=ROOT/'Tools/ForgeBridge/Reports/ChineseRuleCoverage.json';dest.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v if k not in ('before','after','catalogFacesAfter') else v['counts'] for k,v in result.items()},ensure_ascii=False,indent=2))
for r in result['after']['details'].get('mixedLanguageCandidates',[])[:35]:print(r['name']+' | '+' / '.join(r['chunks']))
