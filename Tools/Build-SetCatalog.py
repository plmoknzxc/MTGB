"""Build the offline set browser from Scryfall's set API; requires resvg-py."""
import json
import time
import urllib.request
from pathlib import Path
import resvg_py

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Assets/Resources/SetBrowser'
OUT.mkdir(parents=True, exist_ok=True)
def fetch(url):
    request = urllib.request.Request(url, headers={'User-Agent': 'MTGB-SetCatalog/1.0', 'Accept': 'application/json,image/svg+xml'})
    with urllib.request.urlopen(request, timeout=40) as response:
        return response.read()

sets = json.loads(fetch('https://api.scryfall.com/sets'))['data']
entries = []
for item in sets:
    icon = item['icon_svg_uri'].split('/')[-1].split('?')[0].removesuffix('.svg')
    # CON is a Windows device name, even with a .png/.meta extension.
    if icon.lower() == 'con':
        icon = 'set-con'
    path = OUT / (icon + '.png')
    if not path.exists():
        svg = fetch(item['icon_svg_uri']).decode()
        path.write_bytes(resvg_py.svg_to_bytes(svg_string=svg, width=64, style_sheet='path { fill: white; }'))
        time.sleep(.1)
    entries.append(dict(code=item['code'].upper(), name=item['name'], parent=item.get('parent_set_code', '').upper(), date=item.get('released_at',''), icon=icon))
(OUT / 'Catalog.json').write_text(json.dumps({'entries': entries}, ensure_ascii=False), encoding='utf-8')
print(f'Wrote {len(entries)} sets and {len(list(OUT.glob("*.png")))} local symbols')
