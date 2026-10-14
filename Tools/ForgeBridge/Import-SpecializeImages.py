"""Provision exact linked Specialize faces; preserve existing local artwork.

--metadata points to a directory of frozen Scryfall HBG search source-*.json
responses (include_extras=true, unique=prints). No name-only or inferred-number
substitution: each face must be linked by the exact original printing's all_parts.
"""
import argparse
import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--metadata', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    rows = []
    for path in sorted(args.metadata.glob('source-*.json')):
        rows.extend(json.loads(path.read_text(encoding='utf8'))['data'])
    by_key = {}
    for card in rows:
        key = card['set'].upper() + '/' + card['collector_number']
        if key in by_key:
            raise ValueError('Ambiguous source printing: ' + key)
        by_key[key] = card
    catalog = [json.loads(line) for line in (ROOT / 'ForgeRuntime/catalog.jsonl').read_text(encoding='utf8').splitlines()]
    definitions = {}
    for path in (ROOT / 'ForgeRuntime/assets/res/cardsfolder').rglob('*.txt'):
        text = path.read_text(encoding='utf8')
        if 'AlternateMode:Specialize' not in text:
            continue
        parts = text.split('\nSPECIALIZE:')
        original = next(line[5:] for line in parts[0].splitlines() if line.startswith('Name:'))
        faces = {}
        for part in parts[1:]:
            color = part.splitlines()[0].strip()
            state = {'WHITE': 'W', 'BLUE': 'U', 'BLACK': 'B', 'RED': 'R', 'GREEN': 'G'}[color]
            faces['Specialize' + state] = next(line[5:] for line in part.splitlines() if line.startswith('Name:'))
        definitions[original] = faces
    entries, missing, jobs = [], [], {}
    for printing in catalog:
        if printing['name'] not in definitions:
            continue
        key = printing['key']
        original = by_key.get(key)
        if printing.get('ambiguous') or not original or original['name'] != printing['name']:
            missing.append({'key': key, 'name': printing['name'], 'reason': 'No exact original printing/name'})
            continue
        linked = {part['id']: part for part in original.get('all_parts', [])}
        for state, name in definitions[printing['name']].items():
            candidates = [row for row in rows if row['name'] == name and row['id'] in linked]
            if len(candidates) != 1 or not candidates[0].get('image_uris', {}).get('normal') or candidates[0].get('image_status') == 'placeholder':
                missing.append({'key': key, 'state': state, 'name': name, 'reason': 'No unique linked full-image face'})
                continue
            face = candidates[0]
            art_key = face['set'].upper() + '/' + face['collector_number']
            url = face['image_uris']['normal']
            if not url.startswith('https://cards.scryfall.io/'):
                raise ValueError('Unexpected image host: ' + url)
            entries.append({'key': key, 'state': state, 'name': name, 'artKey': art_key, 'source': face['scryfall_uri']})
            jobs[art_key] = {'key': art_key, 'face': 0, 'url': url, 'path': art_key + '.jpg', 'id': face['id'], 'quality': face['image_status']}
    args.output.mkdir(parents=True, exist_ok=True)
    def write(name, data):
        (args.output / name).write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf8')
    write('index.json', {'entries': entries})
    write('missing.json', missing)
    write('plan.json', list(jobs.values()))
    spec = importlib.util.spec_from_file_location('images', ROOT / 'Tools/ForgeBridge/Import-CardImages.py')
    importer = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(importer)
    import concurrent.futures
    with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
        results = list(pool.map(importer.download, jobs.values()))
    write('results.json', results)
    failed = [result for result in results if result['status'] == 'failed']
    if failed:
        raise RuntimeError('Image provisioning failed; index not installed')
    if args.install:
        # Missing exact matches remain explicit; never substitute an unlinked face.
        target = ROOT / 'Assets/Resources/SpecializeCardArt.json'
        target.write_bytes((args.output / 'index.json').read_bytes())
    print(json.dumps({'entries': len(entries), 'faces': len(jobs), 'missing': len(missing), 'failed': len(failed), 'installed': args.install}))


if __name__ == '__main__':
    main()
