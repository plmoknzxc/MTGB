"""Import UI set/type labels from Academy Ruins and Scryfall responses cached locally.
Only label resources are written; card libraries, scripts and images are untouched.
"""
import collections
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / '.tools/search-ui'
catalog_path = ROOT / 'Assets/Resources/SetBrowser/Catalog.json'
catalog = json.loads(catalog_path.read_text(encoding='utf8'))
academy_sets = json.loads((WORK / 'academy-sets.txt').read_text(encoding='utf8'))
sets = {e['code'].upper(): e for e in academy_sets}
for entry in catalog['entries']:
    name = sets.get(entry['code'].upper(), {}).get('translated_name')
    if name: entry['zh'] = name
catalog_path.write_text(json.dumps(catalog, ensure_ascii=False, separators=(',', ':'))+'\n', encoding='utf8')

path = ROOT / 'Assets/Resources/SearchTypeLabels.json'
labels = json.loads(path.read_text(encoding='utf8'))
allowed=set((WORK/'type-options.txt').read_text(encoding='utf8').splitlines())
allowed.update(json.loads((ROOT/'Tools/ForgeBridge/Reports/JapaneseTypeVocabulary.json').read_text(encoding='utf8'))['terms'])
indexed = {e['en']:e for e in labels['entries'] if e['en'] in allowed}
existing = json.loads((ROOT / 'Assets/Resources/TokenTypeAliases.json').read_text(encoding='utf8'))['entries']
known = {e['en']:e for e in existing}
candidates = {'zh':collections.defaultdict(set), 'ja':collections.defaultdict(set)}
evidence = {}
def collect(english, translated, locale, url):
    if not english or not translated: return
    en = re.split(r'\s+—\s+', english, maxsplit=1)
    local = re.split(r'\s*[—～]\s*',translated,maxsplit=1)
    if len(en)!=len(local): return
    pairs = []
    if len(en[0].split())==1: pairs.append((en[0].strip(),local[0].strip()))
    if len(en)==2:
        # Preserve multiword plane/dungeon subtype names as one label, too.
        pairs.append((en[1].strip(),local[1].strip()))
        words = en[1].replace('Time Lord','Time_Lord').split()
        terms = re.split(r'[・／/]',local[1].strip())
        if len(words)==len(terms): pairs.extend(zip((w.replace('_',' ') for w in words),terms))
    for key, value in pairs:
        if not value or not re.search(r'[\u3040-\u30ff\u3400-\u9fff]',value): continue
        candidates[locale][key].add(value)
        evidence[(locale,key,value)] = url

for source in sorted((ROOT / '.tools/japanese-rule-supplements').glob('page-*.json'))+list(WORK.glob('scryfall-types-*.json')):
    for card in json.loads(source.read_text(encoding='utf8')).get('data',[]):
        for face in card.get('card_faces') or [card]:
            collect(face.get('type_line'),face.get('printed_type_line'),'ja',card.get('scryfall_uri'))
def academy(card):
    collect(card.get('type_line'),card.get('zhs_type_line') or card.get('atomic_translated_type'),'zh',
            'https://mtgch.com/api/v1/card/'+str(card.get('set'))+'/'+str(card.get('collector_number'))+'/')
    for face in card.get('other_faces') or []: academy(face)
for source in list((ROOT / '.tools/chinese-rule-supplements').glob('*.json'))+list(WORK.glob('academy-types-*.json')):
    data=json.loads(source.read_text(encoding='utf8'))
    for card in data.get('items',[]): academy(card)
added=[]
for locale, terms in candidates.items():
    for key,values in terms.items():
        if key not in allowed or len(values)!=1: continue  # Ambiguous printed variants require review.
        entry=indexed.setdefault(key,{'en':key})
        if entry.get(locale) or known.get(key,{}).get(locale): continue
        entry[locale]=next(iter(values))
        added.append(dict(en=key,locale=locale,text=entry[locale],source=evidence[(locale,key,entry[locale])]))
labels['entries']=list(indexed.values())
path.write_text(json.dumps(labels,ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf8')
(WORK/'type-import-evidence.json').write_text(json.dumps(added,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print('Set labels:',{locale:sum(bool(e.get(locale)) for e in catalog['entries']) for locale in ('zh','ja')})
print('New type labels:',collections.Counter(e['locale'] for e in added))
