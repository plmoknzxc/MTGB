"""Search Gatherer by canonical name, then exhaust its Japanese printing pages.

Does not assume local set codes/numbers equal Gatherer's. Only exact English
identity and matching Oracle rules can contribute published local text.
"""
import concurrent.futures
import hashlib
import importlib.util
import json
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REPORTS = ROOT / 'Tools/ForgeBridge/Reports'
WORK = ROOT / '.tools/gatherer-ja/lookup'
spec = importlib.util.spec_from_file_location('gatherer_import', Path(__file__).with_name('Import-GathererJapaneseSupplements.py'))
base = importlib.util.module_from_spec(spec)
spec.loader.exec_module(base)
CATALOG={r['key']:r for line in (ROOT/'ForgeRuntime/catalog.jsonl').read_text(encoding='utf-8').splitlines() if (r:=json.loads(line))}
ALIASES=json.loads(Path(__file__).with_name('LocalizedArtAliases.json').read_text(encoding='utf-8'))

def parse(html):
    chunks = []
    for match in re.finditer(r'self\.__next_f\.push\((.*?)\)</script>', html):
        try:
            value = json.loads(match[1])
            if len(value)>1 and isinstance(value[1],str): chunks.append(value[1])
        except ValueError: pass
    text = ''.join(chunks)
    cards = {}
    for match in re.finditer(r'\{(?="resourceId":)', text):
        try:
            value, _ = json.JSONDecoder().raw_decode(text[match.start():])
            if value.get('kind')=='CardData': cards[value['id']] = value
        except (ValueError,KeyError): pass
    pages = []
    for match in re.finditer(r'"cards":(?=\{)', text):
        try:
            value, _ = json.JSONDecoder().raw_decode(text[match.end():])
            if value.get('kind')=='CardData': pages.append(dict(total=value['totalItems'],pages=value['totalPages']))
        except (ValueError,KeyError): pass
    return list(cards.values()), pages

def get(url):
    path=WORK/(hashlib.sha256(url.encode()).hexdigest()[:24]+'.json')
    if path.exists():return json.loads(path.read_text(encoding='utf-8'))
    result=dict(url=url)
    try:
        request=urllib.request.Request(url,headers={'User-Agent':'MTGB-Localization-Audit/1.0'})
        with urllib.request.urlopen(request,timeout=20) as response:
            raw=response.read();cards,pages=parse(raw.decode('utf-8'))
            result.update(httpStatus=response.status,cards=cards,pages=pages,sourceSha256=hashlib.sha256(raw).hexdigest())
    except urllib.error.HTTPError as error:result.update(httpStatus=error.code,error=str(error))
    except Exception as error:result.update(httpStatus=0,error=str(error))
    path.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    time.sleep(.15)
    return result

def review(gap):
    name,key=gap['name'],gap['key']
    query=urllib.parse.urlencode(dict(cardName=name.replace(' ','_').replace(',','|',1),extraCards='true'))
    search=get('https://gatherer.wizards.com/search?'+query)
    result=dict(key=key,name=name,search=search['url'],searchStatus=search['httpStatus'],printPages=[],japaneseRecords=[])
    if search['httpStatus']!=200:
        result['status']='search-request-failed';return result
    exact=[c for c in search.get('cards',[]) if c.get('oracleName')==name]
    if not exact:
        alias=next((a for a in ALIASES if a['localName']==name and a['evidenceKey']==key),None)
        local=CATALOG.get(key,{})
        parent=local.get('name') if any(f['name']==name for f in local.get('faces',[])) and local.get('splitType')=='Adventure' else None
        lookup=alias['sourceName'] if alias else parent
        if lookup and lookup!=name:
            fallback=review(dict(key=key,name=lookup))
            fallback.update(name=name,initialSearch=result['search'],lookupName=lookup,
                            identityRoute='reviewed-same-printing-alias' if alias else 'local-Adventure-parent-and-matched-composite-face')
            if alias:
                fallback['aliasEvidence']=alias
                code,number=key.split('/',1)
                fallback['japaneseRecords']=[r for r in fallback['japaneseRecords'] if r['card']['setCode']==code and r['card']['cardNumberVariant']==number]
                for r in fallback['japaneseRecords']:
                    c=r['card'];face=local['faces'][0]
                    assert c['oraclePower']==face['power'] and c['oracleToughness']==face['toughness']
                    assert base.clean('{'+c['oracleManaText']+'}')==face['manaCost']
            return fallback
        result['status']='canonical-name-not-found-in-Gatherer-search';return result
    slugs=sorted(set(c['nameKebab'] for c in exact if c.get('nameKebab')))
    unresolved=False;seen={}
    for slug in slugs:
        url='https://gatherer.wizards.com/prints/'+urllib.parse.quote(slug,safe='')+'/ja-jp'
        first=get(url);result['printPages'].append({k:first.get(k) for k in ('url','httpStatus','pages','sourceSha256')})
        if first['httpStatus']!=200:
            unresolved|=first['httpStatus']!=404
            continue
        count=max((p['pages'] for p in first.get('pages',[])),default=1)
        for page in range(1,count+1):
            data=first if page==1 else get(url+'?page='+str(page))
            if page>1: result['printPages'].append({k:data.get(k) for k in ('url','httpStatus','pages','sourceSha256')})
            if data['httpStatus']!=200:unresolved=True;continue
            for card in data.get('cards',[]):
                if card.get('oracleName')==name and card.get('languageCode')=='ja':
                    seen[card['id']]=dict(card=card,page=data['url'],sourceSha256=data['sourceSha256'])
    result['japaneseRecords']=list(seen.values())
    if unresolved:result['status']='Japanese-print-search-incomplete'
    elif not seen:result['status']='no-Japanese-record-returned-by-Gatherer-print-index'
    else:result['status']='Japanese-print-records-found'
    return result

def face_text(card,name):
    title=card.get('instanceName') or '';typ=card.get('instanceTypeLine') or ''
    rules=base.clean(card.get('instanceText'));oracle=base.clean(card.get('oracleText'))
    if '//ADV//' in rules:
        composite=card.get('compositeCard') or {}
        assert composite.get('compositeType')=='Adventure'
        jp=rules.split('//ADV//');en=oracle.split('//ADV//')
        assert len(jp)==len(en)==2
        if name==card['oracleName']:return title,typ,jp[0].strip(),en[0].strip()
        assert name==composite['oracleName']
        jl=[s.strip() for s in jp[1].splitlines() if s.strip()]
        el=[s.strip() for s in en[1].splitlines() if s.strip()]
        assert el[0]==name and base.clean(el[1])==base.clean(jl[1])
        assert 'Adventure' in el[2] and '出来事' in jl[2]
        return re.sub(r'（[ぁ-ゖァ-ヺー]+）','',jl[0]),jl[2],'\n'.join(jl[3:]),'\n'.join(el[3:])
    pairs=(('Gallifrey Falls','No More'),('Coward','Killer'))
    pair=next((p for p in pairs if name in p),None)
    if pair is None:return title,typ,rules,oracle
    assert card.get('compositeCard',{}).get('compositeType')=='Split'
    jp=rules.split('\n//\n');en=oracle.split('\n//\n')
    assert len(jp)==2 and len(en) in (2,3)
    lines=[s.strip() for s in jp[1].splitlines() if s.strip()]
    assert len(lines)>=4 and re.fullmatch(r'(?:o(?:\d+|[WUBRG]))+',lines[1])
    assert lines[2] in ('インスタント','ソーサリー')
    names=[s.strip() for s in title.split('//')]
    assert len(names)==2
    common_en=en[2] if len(en)==3 else ''
    common_jp=''
    if common_en:
        assert common_en.startswith('Fuse') and lines[-1].startswith('融合')
        common_jp=lines.pop()
    index=pair.index(name)
    body=jp[0].strip() if index==0 else '\n'.join(lines[3:])
    body_en=en[index].strip()
    if common_jp:body+='\n'+common_jp;body_en+='\n'+common_en
    return names[index], lines[2] if index==1 else '',body,body_en

def equivalent_english(text,name):
    if name=='Mind Flayer, the Shadow':text=text.replace('Arvinox, the Mind Flail','this permanent').replace('Arvinox','this permanent')
    if name=='Unleash the Flux':text=text.replace('a nonland permanent of their choice','a nonland permanent')
    return base.reviewed_english(text,name)

def main():
    WORK.mkdir(parents=True,exist_ok=True)
    report=json.loads((REPORTS/'LocalizedPresentationReviewed.json').read_text(encoding='utf-8'))
    gaps=[g for g in report['entries'] if g['language']=='ja-JP' and g['field']=='rules' and g['status']=='translation-unavailable-in-checked-sources']
    previous_path=REPORTS/'GathererJapaneseAllPrints.json'
    if previous_path.exists():
        previous=json.loads(previous_path.read_text(encoding='utf-8'))['entries']
        identities={(g['key'],g['name']) for g in gaps}
        gaps.extend(dict(key=g['key'],name=g['name']) for g in previous if (g['key'],g['name']) not in identities)
    contexts=json.loads((ROOT/'.tools/presentation-audit/review-contexts.json').read_text(encoding='utf-8'))
    english={(c['gap']['key'],c['gap']['name']):c['englishRules'] for c in contexts}
    rows={}
    dest=ROOT/'Assets/StreamingAssets/CardTranslations/ja-JP.gatherer-prints.txt'
    if dest.exists():
        rows={r[0]:r for line in dest.read_text(encoding='utf-8').splitlines() if len(r:=line.split('|',3))==4}
    entries=[]
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        for index,result in enumerate(pool.map(review,gaps),1):
            name,key=result['name'],result['key'];usable=[];mismatches=[]
            for record in result['japaneseRecords']:
                card=record['card'];title,typ,rules,oracle=face_text(card,name)
                if not base.JA.search(rules):continue
                if '//ADV//' in rules:
                    mismatches.append(dict(reason='compound-area-requires-review',record=record));continue
                if base.comparable(equivalent_english(english[(key,name)],name),name)!=base.comparable(equivalent_english(oracle,name),name):
                    mismatches.append(dict(reason='English-Oracle-version-differs',record=record));continue
                usable.append(record)
            if usable:
                chosen=max(usable,key=lambda r:r['card'].get('setReleaseDate',''));card=chosen['card'];title,typ,rules,oracle=face_text(card,name)
                rows[name]=[name,title if base.JA.search(title) else '',typ if base.JA.search(typ) else '',rules.replace('\n','\\n')]
                result.update(status='published-Japanese-rules-imported-from-print-index',selectedSource=chosen['page'],printedName=title,printedRules=rules)
                if result.get('identityRoute')=='local-Adventure-parent-and-matched-composite-face':
                    parent=CATALOG[key]['name'];front=face_text(card,parent)[2]
                    assert base.comparable(face_text(card,parent)[3],parent)==base.comparable(CATALOG[key]['faces'][0]['text'].replace('\\n','\n'),parent)
                    corrections_path=ROOT/'Assets/StreamingAssets/CardTranslations/ja-JP.corrections.json'
                    corrections=json.loads(corrections_path.read_text(encoding='utf-8'))
                    for file in (ROOT/'Assets/StreamingAssets/CardTranslations').glob('ja-JP*.txt'):
                        for line in file.read_text(encoding='utf-8-sig').splitlines():
                            old=line.split('|',3)
                            if len(old)==4 and old[0]==parent and '//ADV//' in old[3] and not any(x['name']==parent for x in corrections['entries']):
                                corrections['entries'].append(dict(name=parent,expectedRules=old[3].replace('\\n','\n')))
                    corrections_path.write_text(json.dumps(corrections,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
                    rows[parent]=[parent,'','',front.replace('\n','\\n')]
                    result['parentRuleRepair']='Separates the exact matched official Adventure parent text from its auxiliary area; preserves independent existing name/type.'
            elif mismatches:result.update(status='published-Japanese-record-needs-version-review',versionReviews=mismatches)
            elif result['japaneseRecords']:result['status']='Japanese-records-without-Japanese-rule-text'
            if name.startswith('Snow-Covered '):
                plain=name[len('Snow-Covered '):]
                symbols=dict(Plains='W',Island='U',Swamp='B',Mountain='R',Forest='G')
                if plain in symbols and english[(key,name)].strip('()')=='{T}: Add {'+symbols[plain]+'}.':
                    basic=next(line.split('|',3) for line in (ROOT/'Assets/StreamingAssets/CardTranslations/ja-JP.txt').read_text(encoding='utf-8-sig').splitlines() if line.startswith(plain+'|'))
                    assert basic[3]=='{'+symbols[plain]+'}'
                    rows[name]=[name,'','',basic[3]]
                    result.update(status='intrinsic-basic-land-mana-symbol-display',displayRules=basic[3],displayEvidence='Uses existing Japanese '+plain+' mana-symbol presentation for the same intrinsic basic-land mana ability; not an invented printed translation.')
            image_review=REPORTS/'GathererJapaneseImageTranscriptions.json'
            if image_review.exists():
                image_rows=json.loads(image_review.read_text(encoding='utf-8'))['entries']
                manual=next((r for r in image_rows if r['key']==key and r['name']==name),None)
                if manual:
                    assert base.comparable(base.clean(manual['englishRules']),name)==base.comparable(base.clean(english[(key,name)]),name)
                    asset=ROOT/manual['path']
                    assert hashlib.sha256(asset.read_bytes()).hexdigest()==manual['sha256']
                    rows[name]=[name,manual['printedName'],manual['printedType'],manual['printedRules'].replace('\n','\\n')]
                    result.update(status='published-Japanese-rules-transcribed-from-official-image',imageReview=manual)
            entries.append(result)
            if index%100==0 or index==len(gaps):print(f'{index}/{len(gaps)}; new rule records={sum(r["status"]=="published-Japanese-rules-imported-from-print-index" for r in entries)}',flush=True)
    dest.write_text('\n'.join('|'.join(s.replace('|','｜').replace('\r','').replace('\n','\\n') for s in row) for _,row in sorted(rows.items()))+'\n',encoding='utf-8')
    output=dict(complete=not any(r['status'].endswith('failed') or r['status'].endswith('incomplete') for r in entries),reviewed=len(entries),entries=entries,
                limitation='Exact canonical-name search followed by every page of the indexed Japanese prints. No result means the current Gatherer index returned none, not that no Japanese publication exists. Published text with different Oracle wording remains for explicit review.')
    previous_path.write_text(json.dumps(output,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('All-print index review finished',flush=True)

if __name__=='__main__':
    sys.stdout.reconfigure(encoding='utf-8');main()
