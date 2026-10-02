"""Import set labels from cached official product pages; does not touch card data or images.

Sources: magic.wizards.com/zh-Hans/products, www.cnwizards.com/products,
mtg-jp.com/products/card-gallery/. Save their HTML under .tools/search-ui first.
"""
import html
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
path = ROOT / 'Assets/Resources/SetBrowser/Catalog.json'
data = json.loads(path.read_text(encoding='utf8'))
entries = data['entries']
by_code = {e['code'].upper(): e for e in entries}
def normalized(text):
    return re.sub(r'[^a-z0-9]', '', text.lower())
by_name = {normalized(e['name']): e for e in entries}
aliases = {'2010coreset':'M10','2011coreset':'M11','2012coreset':'M12','2013coreset':'M13',
           '2014coreset':'M14','2015coreset':'M15','coreseteighthedition':'8ED',
           'coresetninthedition':'9ED','coresettenthedition':'10E','coresetseventhedition':'7ED',
           'classicsixthedition':'6ED','judgmentcardsetarchive':'JUD','brotherswar':'BRO',
           'alphabetaandunlimited':None}
source = (ROOT / '.tools/search-ui/products-zh.html').read_text(encoding='utf8')
for match in re.finditer(r'"(Product \| [^"\n]+)","([^"\n]*[\u3400-\u9fff][^"\n]*)"', source):
    heading, title = match.groups()
    segments = [p.strip() for p in heading.split('|')[1:]]
    entry = next((by_code[p.upper()] for p in segments if p.upper() in by_code), None)
    if entry is None:
        for segment in reversed(segments):
            key = normalized(segment)
            entry = by_name.get(key) or by_code.get(aliases.get(key,''))
            if entry: break
    if entry:
        entry['zh'] = html.unescape(title).replace('- 产品全览','').strip()
# The mainland product index supplies full localized titles for current releases.
current = {'FRA':'现实裂界','HOB':'万智牌 | 霍比特人','MSH':'万智牌丨漫威超级英雄',
           'SOS':'斯翠海文的秘密','TMT':'万智牌 | 忍者神龟','TLA':'万智牌丨降世神通：最后的气宗',
           'INR':'依尼翠：重制版','MH3':'摩登新篇3'}
cn = (ROOT / '.tools/search-ui/sets-zh.html').read_text(encoding='utf8')
for code, title in current.items():
    if title in cn and code in by_code: by_code[code]['zh'] = title
jp = (ROOT / '.tools/search-ui/sets-ja.html').read_text(encoding='utf8')
for block in re.findall(r'<a\b[^>]*href="/products/card-gallery/[^>]*>(.*?)</a>',jp,re.S):
    thumb = re.search(r'class="_thumb"[^>]*>\s*<img[^>]*src="[^"]*/([A-Za-z0-9]+)_',block)
    name = re.search(r'class="_name">(.*?)</div>',block,re.S)
    if thumb and name:
        code=thumb[1].upper()
        # Official thumbnails sometimes append a revision number (ECL2, etc.).
        if code not in by_code and re.fullmatch(r'[A-Z]{3}\d+',code):
            code=re.sub(r'\d+$','',code)
        if code in by_code:
            by_code[code]['ja'] = html.unescape(re.sub('<[^>]+>','',name[1])).strip()
# Translate only unambiguous ancillary suffixes of an already localized parent.
for _ in range(3):
    for entry in entries:
        parent = by_code.get(entry.get('parent','').upper())
        if not parent: continue
        prefix=parent['name']
        starts=entry['name'].casefold().startswith(prefix.casefold())
        suffix = entry['name'][len(prefix):].strip() if starts else ''
        translations = {'Tokens':('衍生物','トークン'),'Commander':('指挥官','統率者'),
                        'Promos':('推广牌','プロモ'),'Art Series':('美术系列','アート・シリーズ')}
        if suffix not in translations or not starts: continue
        for locale, tail in zip(('zh','ja'),translations[suffix]):
            if not entry.get(locale) and parent.get(locale): entry[locale] = parent[locale]+' '+tail
# Reviewed official title: magic.wizards.com/ja/news/mtg-arena/alchemy-lorwyn-spellbooks
if 'YECL' in by_code: by_code['YECL']['ja']='アルケミー：ローウィン'
path.write_text(json.dumps(data,ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf8')
print({locale:sum(bool(e.get(locale)) for e in entries) for locale in ('zh','ja')})
