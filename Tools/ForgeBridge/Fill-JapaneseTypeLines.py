"""Fill missing Japanese type lines using local published data and known type terms."""
import collections, gzip, json, re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
FOLDER=ROOT/'Assets/StreamingAssets/CardTranslations'
JA=re.compile('[\u3040-\u30ff\u3400-\u9fff]')
def rows(path):
    return {f[0]:f for line in path.read_text(encoding='utf-8-sig').splitlines() if len(f:=line.split('|',3))==4}
base=rows(FOLDER/'ja-JP.txt'); supp=rows(FOLDER/'ja-JP.supplement.txt')
merged={n:list(f) for n,f in base.items()}
for n,f in supp.items():
    if n not in merged:merged[n]=list(f)
    else:
        for i in range(1,4):
            if f[i] and (not merged[n][i] or not JA.search(merged[n][i])):merged[n][i]=f[i]
faces={}
for line in (ROOT/'ForgeRuntime/catalog.jsonl').read_text(encoding='utf8').splitlines():
    for face in json.loads(line)['faces']:faces.setdefault(face['name'],face)
published={}
for path in sorted((ROOT/'.tools/japanese-rule-supplements').glob('page-*.json')):
    for card in json.loads(path.read_text(encoding='utf8')).get('data',[]):
        for face in card.get('card_faces') or [card]:
            if JA.search(face.get('printed_type_line','')):published.setdefault(face['name'],(face['printed_type_line'],card.get('scryfall_uri','')))
foreign=ROOT/'.tools/keyword-glossary/mtgjson-foreign-texts.json'
if foreign.exists():
    for n,languages in json.loads(foreign.read_text(encoding='utf8')).items():
        for f in languages.get('Japanese',[]):
            if JA.search(f.get('type','')):published.setdefault(n,(f['type'],'local-mtgjson-foreign-data'))
terms={e['en']:e['ja'] for e in json.loads((ROOT/'Assets/Resources/TokenTypeAliases.json').read_text(encoding='utf8'))['entries'] if e.get('ja')}
terms.update({'Artifact':'アーティファクト','Creature':'クリーチャー','Enchantment':'エンチャント','Instant':'インスタント','Sorcery':'ソーサリー','Land':'土地','Planeswalker':'プレインズウォーカー','Battle':'バトル','Kindred':'同族','Tribal':'部族','Legendary':'伝説の','Basic':'基本','Snow':'氷雪','World':'ワールド','Ongoing':'持続','Conspiracy':'策略','Plane':'次元','Phenomenon':'現象','Vanguard':'ヴァンガード','Scheme':'計略'})
vocabulary=ROOT/'Tools/ForgeBridge/Reports/JapaneseTypeVocabulary.json'
if vocabulary.exists():terms.update(json.loads(vocabulary.read_text(encoding='utf8'))['terms'])
# Some silver-border cards are not present in the engine catalog. Their local
# Oracle type line still supplies an unambiguous canonical type for the UI.
uncatalogued={n for n,f in merged.items() if JA.search(f[3]) and not JA.search(f[2]) and n not in faces}
if uncatalogued:
    with gzip.open(ROOT/'.tools/card-images/default-cards.jsonl.gz','rt',encoding='utf8') as stream:
        for line in stream:
            card=json.loads(line)
            for face in card.get('card_faces') or [card]:
                if face['name'] not in uncatalogued:continue
                typ=face.get('type_line','');parts=re.split(' [—-] ',typ,maxsplit=1)
                main=parts[0].split();subs=parts[1].split() if len(parts)>1 else []
                if all(t in terms for t in main+subs):
                    faces[face['name']]=dict(name=face['name'],types=[t for t in main if t not in ['Basic','Legendary','Snow','World','Ongoing']],supertypes=[t for t in main if t in ['Basic','Legendary','Snow','World','Ongoing']],subtypes=subs)
filled=[];missing=[]
for name,row in sorted(merged.items()):
    if not JA.search(row[3]) or JA.search(row[2]):continue
    face=faces.get(name)
    if not face:
        missing.append(dict(name=name,unknown=['nonstandard or uncatalogued type line']))
        continue
    if name in published:typ,source=published[name]
    else:
        names=face.get('supertypes',[])+face.get('types',[])+face.get('subtypes',[])
        unknown=[n for n in names if n not in terms]
        if unknown:missing.append(dict(name=name,unknown=unknown));continue
        typ=''.join(terms[n] for n in face.get('supertypes',[]))+'・'.join(terms[n] for n in face.get('types',[]))
        if face.get('subtypes'):typ+=' — '+'・'.join(terms[n] for n in face['subtypes'])
        source='local-known-type-terms'
    target=supp.setdefault(name,[name,'','',''])
    target[2]=typ
    filled.append(dict(name=name,type=typ,source=source))
(FOLDER/'ja-JP.supplement.txt').write_text('\n'.join('|'.join(f) for _,f in sorted(supp.items()))+'\n',encoding='utf8')
report_path=ROOT/'Tools/ForgeBridge/Reports/JapaneseTypeLines.json'
previous=json.loads(report_path.read_text(encoding='utf8')).get('entries',[]) if report_path.exists() else []
all_entries={e['name']:e for e in previous}
all_entries.update({e['name']:e for e in filled})
report=dict(filledThisRun=len(filled),recordedFills=len(all_entries),remaining=len(missing),unknownTerms=dict(collections.Counter(t for row in missing for t in row['unknown'])),entries=list(all_entries.values()),unresolved=missing)
(ROOT/'Tools/ForgeBridge/Reports/JapaneseTypeLines.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps({k:v for k,v in report.items() if k not in ['entries','unresolved']},ensure_ascii=True))
