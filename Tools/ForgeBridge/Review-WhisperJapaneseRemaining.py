"""Offline bilingual cross-check of every remaining Japanese rule-source gap.

Wisdom Guild publishes both Japanese text and English Oracle. It also carries
community translations for cards without official Japanese publication; record
that distinction rather than labeling every record official. No runtime fetch.
"""
import collections,concurrent.futures,hashlib,importlib.util,json,re,sys,time,unicodedata,urllib.parse,urllib.request,urllib.error,threading
from pathlib import Path
from bs4 import BeautifulSoup

ROOT=Path(__file__).resolve().parents[2]
REPORTS=ROOT/'Tools/ForgeBridge/Reports'
WORK=ROOT/'.tools/whisper-ja-review'
spec=importlib.util.spec_from_file_location('gatherer',Path(__file__).with_name('Import-GathererJapaneseSupplements.py'))
base=importlib.util.module_from_spec(spec);spec.loader.exec_module(base)
CATALOG={r['key']:r for l in (ROOT/'ForgeRuntime/catalog.jsonl').read_text(encoding='utf-8').splitlines() if (r:=json.loads(l))}
CONTEXT={(x['gap']['key'],x['gap']['name']):x['englishRules'] for x in json.loads((ROOT/'.tools/presentation-audit/review-contexts.json').read_text(encoding='utf-8'))}
BLOCKED=threading.Event()

def fetch(name):
    url='https://whisper.wisdom-guild.net/card/'+urllib.parse.quote_plus(name)+'/'
    path=WORK/(hashlib.sha256(url.encode()).hexdigest()[:24]+'.json')
    if path.exists():return json.loads(path.read_text(encoding='utf-8'))
    if '--offline' in sys.argv or BLOCKED.is_set():return dict(url=url,httpStatus=0,error='Not requested: source returned 403 or offline review selected.')
    result=dict(url=url)
    try:
        with urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'MTGB-Offline-Localization-Review/1.0'}),timeout=20) as response:
            raw=response.read();html=raw.decode('utf-8')
            result.update(httpStatus=response.status,sha256=hashlib.sha256(raw).hexdigest())
        path.with_suffix('.html').write_text(html,encoding='utf-8')
        soup=BeautifulSoup(html,'html.parser')
        detail=soup.select_one('.wg-whisper-card-detail')
        fields={}
        if detail:
            for tr in detail.find_all('tr'):
                th=tr.find('th');td=tr.find('td')
                if not th or not td:continue
                key=th.get_text(strip=True)
                if key=='カード名':fields[key]=td.find('b').get_text(strip=True)
                elif key in ('テキスト','オラクル'):
                    paragraphs=td.find_all('p')
                    fields[key]='\n'.join(p.get_text('',strip=True) for p in paragraphs) if paragraphs else td.get_text('',strip=True)
                else:fields[key]=td.get_text('',strip=True)
        result['fields']=fields
    except urllib.error.HTTPError as e:
        result.update(httpStatus=e.code,error=str(e))
        if e.code==403:BLOCKED.set()
    except Exception as e:result.update(httpStatus=0,error=str(e))
    path.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    time.sleep(.25)
    return result

COLORS={'白':'W','青':'U','黒':'B','赤':'R','緑':'G','無':'C','氷':'S'}
def mana(text):
    def convert(m):
        body=unicodedata.normalize('NFKC',m[1])
        body='/'.join(COLORS.get(x,x) for x in body.split('/'))
        return '{'+body+'}' if re.fullmatch(r'\d+|[WUBRGCSTXYZ]|[2WUBRG]/[WUBRGP]',body) else m[0]
    return re.sub(r'[（(]([^（）()]+)[）)]',convert,text)

def comparable(text,name):
    text=base.reviewed_english(unicodedata.normalize('NFKC',text),name)
    text=re.sub(r'\s+-{2,3}\s+', ' — ',text)
    text=text.replace(name,'this permanent')
    # Remove complete reminder parentheses, including nested mana explanations.
    while re.search(r'\([^()]*\)',text):text=re.sub(r'\([^()]*\)','',text)
    if ',' in name:text=re.sub(r'\b'+re.escape(name.split(',')[0])+r'\b','this permanent',text)
    return base.comparable(text,name).replace('’',"'")

def review(gap):
    name,key=gap['name'],gap['key'];source=fetch(name)
    r=dict(key=key,name=name,source=source['url'],httpStatus=source['httpStatus'],sourceSha256=source.get('sha256'))
    if source['httpStatus']!=200:
        r['status']='source-request-failed' if source['httpStatus'] not in (404,410) else 'no-card-page-returned';return r
    fields=source.get('fields',{});title=fields.get('カード名','')
    if not title.endswith('/'+name):r['status']='exact-bilingual-identity-not-found';return r
    japanese=title[:-len(name)-1];rules=mana(fields.get('テキスト',''));oracle=fields.get('オラクル','')
    r.update(publishedName=japanese,publishedRules=rules,publishedEnglish=oracle,publishedFields=fields)
    if not base.JA.search(rules):r['status']='no-Japanese-rule-text-returned';return r
    local=CATALOG[key];face=next((f for f in local['faces'] if f['name']==name),None)
    if face is None:r['status']='composite-name-requires-face-review';return r
    if mana(fields.get('マナコスト',''))!=face.get('manaCost',''):
        r['status']='mana-cost-version-review';return r
    stats=unicodedata.normalize('NFKC',fields.get('Ｐ／Ｔ',''))
    if face.get('power') and stats!=face['power']+'/'+face['toughness']:
        r['status']='power-toughness-version-review';return r
    if comparable(oracle,name)!=comparable(CONTEXT[(key,name)],name):
        r['status']='English-Oracle-version-review';r['localEnglish']=CONTEXT[(key,name)];return r
    r.update(status='bilingual-published-Japanese-rules-verified',printedType=re.sub(r'\([A-Za-z -]+\)','',fields.get('タイプ','')),
             validation='Exact English identity; matching mana cost and creature stats; same English Oracle after self-name/reminder formatting normalization. Japanese database text retained, not machine translated.',
             sourceQualification='Published Wisdom Guild Japanese text; site credits Wizards card text and Wisdom Guild translations for untranslated cards. This record alone does not prove an official Japanese printing.')
    return r

def main():
    WORK.mkdir(parents=True,exist_ok=True)
    current=json.loads((REPORTS/'LocalizedPresentationReviewed.json').read_text(encoding='utf-8'))
    gaps=[r for r in current['entries'] if r['language']=='ja-JP' and r['field']=='rules' and r['status']=='translation-unavailable-in-checked-sources']
    report_path=REPORTS/'WhisperJapaneseRemaining.json'
    if report_path.exists():
        seen={(g['key'],g['name']) for g in gaps}
        gaps.extend(dict(key=r['key'],name=r['name']) for r in json.loads(report_path.read_text(encoding='utf-8'))['entries'] if (r['key'],r['name']) not in seen)
    if '--pilot' in sys.argv:gaps=gaps[:20]
    entries=[]
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        for i,r in enumerate(pool.map(review,gaps),1):
            entries.append(r)
            if i%50==0 or i==len(gaps):
                report_path.write_text(json.dumps(dict(complete=i==len(gaps),reviewed=i,total=len(gaps),allSourcesChecked=not any(x['status']=='source-request-failed' for x in entries),entries=entries,
                  limitation='Every candidate classified; 403 and unrequested pages are unresolved source access, not evidence of absent Japanese text.'),ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
                print(f'{i}/{len(gaps)}: '+str(dict(collections.Counter(x['status'] for x in entries))),flush=True)
    dest=ROOT/'Assets/StreamingAssets/CardTranslations/ja-JP.whisper.txt'
    rows={r[0]:r for line in dest.read_text(encoding='utf-8').splitlines() if len(r:=line.split('|',3))==4} if dest.exists() else {}
    for r in entries:
        if r['status']=='bilingual-published-Japanese-rules-verified':
            rows[r['name']]=[r['name'],r['publishedName'] if base.JA.search(r['publishedName']) else '',r['printedType'] if base.JA.search(r['printedType']) else '',r['publishedRules'].replace('\n','\\n')]
    dest.write_text('\n'.join('|'.join(s.replace('|','｜') for s in row) for _,row in sorted(rows.items()))+'\n',encoding='utf-8')

if __name__=='__main__':sys.stdout.reconfigure(encoding='utf-8');main()
