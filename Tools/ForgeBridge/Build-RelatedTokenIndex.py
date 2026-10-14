"""Build display-only token links from the existing LOCAL bulk metadata. No network."""
import gzip
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

def apply_project_links(entries):
    """Preserve exact-source project token art when rebuilding the display index."""
    supplement = ROOT / 'Tools/ForgeBridge/ProjectTokenArt.json'
    if not supplement.exists():
        return entries
    catalog = {}
    for line in (ROOT / 'ForgeRuntime/catalog.jsonl').read_text(encoding='utf8').splitlines():
        row = json.loads(line)
        catalog[row['key']] = row
    for link in json.loads(supplement.read_text(encoding='utf8'))['entries']:
        source = catalog.get(link['key'])
        if source is None or source.get('name', source.get('cardName')) != link['name']:
            raise ValueError('Project token source identity changed: ' + link['key'])
        existing = [e for e in entries if e['key'] == link['key']]
        if len(existing) > 1 or existing and existing[0]['name'] != link['name']:
            raise ValueError('Ambiguous project token source: ' + link['key'])
        target = existing[0] if existing else dict(key=link['key'], name=link['name'], tokens=[])
        if not existing:
            entries.append(target)
        for token in link['tokens']:
            prior = [t for t in target['tokens'] if t['key'] == token['key']]
            if prior and prior != [token]:
                raise ValueError('Conflicting project token: ' + token['key'])
            if not prior:
                target['tokens'].append(token.copy())
    return entries

def build():
    sources = {}
    for line in (ROOT / 'Assets/StreamingAssets/CardArtSources.jsonl').read_text(encoding='utf8').splitlines():
        row = json.loads(line)
        sources[row['key']] = row['name']
    tokens, parents = {}, []
    with gzip.open(ROOT / '.tools/card-images/default-cards.jsonl.gz', 'rt', encoding='utf8') as stream:
        for line in stream:
            row = json.loads(line)
            key = row.get('set', '').upper() + '/' + row.get('collector_number', '')
            tokens[row['id']] = dict(key=key, name=row['name'], rules=row.get('oracle_text', ''))
            if key in sources and sources[key] in [row['name']] + [f['name'] for f in row.get('card_faces', [])]:
                links = [p for p in row.get('all_parts', []) if p['component'] == 'token'
                         or p.get('type_line', '').split('—', 1)[0].strip() == 'Emblem']
                if links:
                    parents.append((key, sources[key], links))
    # A few exact token printings referenced by all_parts are omitted from Scryfall's
    # default-cards bulk file. Keep their IDs and printings locally so builds stay offline.
    tokens.update({
        'a7d41725-95ed-455f-9e5e-d10f7e2f9c29': dict(key='SLD/2065', name='Treasure', rules='{T}, Sacrifice this token: Add one mana of any color.'),
        '9195ca39-64af-4c7f-8d05-5106f7645a33': dict(key='DDQ/79', name='Spirit', rules='Flying'),
        '3a3cda49-3e96-47da-89ad-b961a889b8b5': dict(key='PL24/3', name='Dragon', rules='Flying'),
    })
    entries, unresolved = [], []
    for key, name, links in parents:
        related = []
        for link in links:
            token = tokens.get(link['id'])
            if token is None:
                unresolved.append(link['id'])
                token = dict(key='', name=link['name'], rules='')
            if token not in related:
                related.append(token)
        entries.append(dict(key=key, name=name, tokens=related))
    entries = apply_project_links(entries)
    dest = ROOT / 'Assets/Resources/RelatedTokenArt.json'
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(json.dumps(dict(entries=entries), ensure_ascii=False, separators=(',', ':')), encoding='utf8')
    print(f'{len(entries)} source printings; {len(set(unresolved))} unresolved token IDs (display missing); {dest.stat().st_size} bytes')

if __name__ == '__main__':
    build()
    subprocess.run([sys.executable, str(Path(__file__).with_name('Build-TokenTypeAliases.py'))], check=True)
