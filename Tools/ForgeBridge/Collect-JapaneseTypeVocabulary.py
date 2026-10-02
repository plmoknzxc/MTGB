import json,re,requests
from pathlib import Path
from bs4 import BeautifulSoup
root=Path(__file__).resolve().parents[2]
url='https://mtg-jp.com/gameplay/rules/docs/0006836/'
response=requests.get(url,timeout=30);response.raise_for_status();response.encoding='utf8'
text=BeautifulSoup(response.text,'html.parser').get_text('',strip=False)
terms={}
seen=set()
for paragraph in re.split(r'(?=205\.[234])',text):
    header=re.match(r'205\.(?:2a|3[g-n]|4a)\b',paragraph)
    if not header or header[0] in seen:continue
    seen.add(header[0])
    for ja,en in re.findall(r'「([^「」／]+)／([^「」]+)」',paragraph):
        terms[en.strip().replace('’',"'")]=re.sub(r'\s+','',ja)
# The official Japanese CR currently spells Symbiote's English half Synbiote.
if 'Synbiote' in terms:terms['Symbiote']=terms['Synbiote']
assert terms['Equipment']=='装備品' and terms['Town']=='街' and terms['Dellian']=='デリアン'
dest=root/'Tools/ForgeBridge/Reports/JapaneseTypeVocabulary.json'
dest.write_text(json.dumps(dict(source=url,version='20260807.0',terms=terms),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print('Collected',len(terms),'official Japanese type terms')
