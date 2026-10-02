"""Collect published Japanese prepared spell faces omitted by language APIs."""
import json,re,time
from pathlib import Path
import requests
from bs4 import BeautifulSoup
ROOT=Path(__file__).resolve().parents[2]
WORK=ROOT/'.tools/japanese-prepared-texts';WORK.mkdir(parents=True,exist_ok=True)
cards=[];seen=set()
for line in (ROOT/'ForgeRuntime/catalog.jsonl').read_text().splitlines():
    c=json.loads(line)
    if c['forgeSet']!='FRA' or c['splitType']!='Prepare' or len(c['faces'])<2 or c['faces'][1]['name'] in seen:continue
    seen.add(c['faces'][1]['name']);cards.append(c)
SYMBOLS={'白':'W','青':'U','黒':'B','赤':'R','緑':'G','◇':'C','Ｔ':'T','Ｑ':'Q','Ｘ':'X'}
def symbols(text):
    def sub(m):
        parts=m[1].split('/')
        if not all(p in SYMBOLS or p.isdigit() for p in parts):return m[0]
        return '{'+'/'.join(SYMBOLS.get(p,p.translate(str.maketrans('０１２３４５６７８９','0123456789'))) for p in parts)+'}'
    return re.sub(r'\(([^()]*)\)',sub,text)
entries=[]
for c in cards:
    key=c['key'].replace('/','');url='https://whisper.wisdom-guild.net/card/'+key+'/'
    path=WORK/(key+'.html')
    if not path.exists():
        r=requests.get(url,timeout=30);r.raise_for_status();path.write_text(r.text,encoding='utf-8');time.sleep(.35)
    soup=BeautifulSoup(path.read_text(encoding='utf-8'),'html.parser')
    for br in soup.find_all('br'):br.replace_with('\n')
    current=None
    for tr in soup.select('tr'):
        cells=tr.find_all(['th','td'],recursive=False)
        if len(cells)!=2:continue
        label=cells[0].get_text('',strip=True);value=cells[1].get_text('',strip=True)
        if label=='カード名' and '/' in value:
            cn,name=value.split('/',1)
            if name not in [f['name'] for f in c['faces']]:current=None;continue
            current=dict(name=name,japaneseName=cn,url=url,type='',text='');entries.append(current)
        elif current is not None and label=='タイプ':current['type']=value
        elif current is not None and label=='テキスト':current['text']=symbols(value)
    print(c['key'],len(entries),flush=True)
out=ROOT/'.tools/keyword-glossary/japanese-prepared-texts.json';out.write_text(json.dumps(entries,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('PASS:',len(entries),'published Japanese prepared face records')
