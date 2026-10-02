"""Build a portable one-printing-per-Forge-rules-name image ZIP without changing originals."""
import argparse
import collections
import hashlib
import io
import json
from pathlib import Path
import shutil
import time
import zipfile
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, default=ROOT / 'Builds/CardImages-Basic.zip')
    args = parser.parse_args()
    output = args.output.resolve()
    if output.exists() or output.with_suffix('.zip.partial').exists():
        parser.error('Output already exists; choose a new output path.')
    cache = ROOT / '.tools/card-images'
    inventory = {row['path']: row for row in map(json.loads, (cache / 'inventory.jsonl').read_text(encoding='utf8').splitlines())}
    jobs = collections.defaultdict(list)
    for job in json.loads((cache / 'plan.json').read_text(encoding='utf8')):
        if job['path'] in inventory:
            jobs[job['key']].append(job)
    groups = collections.defaultdict(list)
    catalog = (ROOT / 'ForgeRuntime/catalog.jsonl').read_bytes()
    for line in catalog.decode('utf8').splitlines():
        card = json.loads(line)
        groups[card['name']].append(card)
    selected, missing = [], []
    for name, cards in sorted(groups.items()):
        candidates = []
        for card in cards:
            faces = sorted(jobs.get(card['key'], []), key=lambda j: j['face'])
            expected = set(range(len(card['faces']))) if card['splitType'] in ('Transform', 'Modal', 'Meld') else {0}
            if card.get('ambiguous') or not expected.issubset({j['face'] for j in faces}):
                continue
            rank = (any(j.get('quality') != 'highres_scan' for j in faces), card.get('artIndex') != 1, card['key'])
            candidates.append((rank, card, faces))
        if not candidates:
            missing.append({'name': name, 'keys': sorted({c['key'] for c in cards}), 'reason': 'No unambiguous printing with all required image faces'})
            continue
        _, card, faces = min(candidates, key=lambda c: c[0])
        selected.append({'name': name, 'key': card['key'], 'faces': [dict(inventory[j['path']], face=j['face'], quality=j.get('quality')) for j in faces]})
    paths = [face for card in selected for face in card['faces']]
    if len({p['path'] for p in paths}) != len(paths):
        raise RuntimeError('Selected printing paths collide across rules names.')
    total = sum(p['bytes'] for p in paths)
    output.parent.mkdir(parents=True, exist_ok=True)
    if shutil.disk_usage(output.parent).free < total + 1024**3:
        raise RuntimeError('Insufficient disk space including 1 GiB reserve.')
    report = {'catalog_sha256': hashlib.sha256(catalog).hexdigest(), 'catalog_names': len(groups),
              'selected_names': len(selected), 'faces': len(paths), 'image_bytes': total,
              'image_GiB': round(total / 1024**3, 3), 'source_faces': len(inventory),
              'excluded_faces': len(inventory) - len(paths), 'missing_names': len(missing),
              'selection': 'Complete faces first; highres_scan preferred; artIndex=1 preferred; ordinal printing key tie-break.',
              'missing': missing, 'cards': selected}
    print(json.dumps({k: v for k, v in report.items() if k not in ('cards', 'missing')}, ensure_ascii=False), flush=True)
    temporary = output.with_suffix('.zip.partial')
    started = last = time.monotonic()
    with zipfile.ZipFile(temporary, 'w', compression=zipfile.ZIP_STORED, allowZip64=True) as archive:
        for i, face in enumerate(paths, 1):
            source = (ROOT / 'CardImages' / face['path']).resolve()
            if not source.is_relative_to((ROOT / 'CardImages').resolve()):
                raise RuntimeError('Image path escapes source root.')
            data = source.read_bytes()
            if len(data) != face['bytes'] or hashlib.sha256(data).hexdigest() != face['sha256']:
                raise RuntimeError('Image changed since inventory: ' + face['path'])
            with Image.open(io.BytesIO(data)) as image:
                image.verify()
            archive.writestr('CardImages/' + face['path'], data)
            if time.monotonic() - last > 10:
                print(f'Verified and packed {i}/{len(paths)} faces', flush=True)
                last = time.monotonic()
        archive.writestr('basic-card-images-manifest.json', json.dumps(report, ensure_ascii=False, indent=2).encode('utf8'))
        archive.writestr('README.txt', ('MTGB basic card images\nExtract next to the game executable. Requires the current client with same-card image fallback.\n'
                          'One printing per Forge canonical rules name, with all available faces. No image re-encoding.\n'
                          'Full source image library remains separate for optional art downloads.\n'
                          'See basic-card-images-manifest.json for selected printings and missing names.\n').encode('utf8'))
    print('Checking complete archive CRC...', flush=True)
    with zipfile.ZipFile(temporary) as archive:
        bad = archive.testzip()
        if bad:
            raise RuntimeError('ZIP CRC failed: ' + bad)
    temporary.rename(output)
    report['archive_bytes'] = output.stat().st_size
    report['archive_GiB'] = round(report['archive_bytes'] / 1024**3, 3)
    report['elapsed_seconds'] = round(time.monotonic() - started)
    output.with_suffix('.manifest.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf8')
    print(json.dumps({k: v for k, v in report.items() if k not in ('cards', 'missing')}, ensure_ascii=False), flush=True)


if __name__ == '__main__':
    main()
