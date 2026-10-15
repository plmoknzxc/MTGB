"""Package a release player with one Chinese-first scan per rules identity/face.

Original scans stay untouched. Exact helper keys are retained because token,
meld and specialize consumers do not use the catalog's printing fallback.
"""
import argparse
import collections
import concurrent.futures
import hashlib
import io
import json
import os
from pathlib import Path
import shutil
import time
import zipfile

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
IMAGES = ROOT / 'CardImages'
EXTENSIONS = ('.png', '.jpg', '.jpeg')


def read_json(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def inventory():
    result = {}
    def visit(folder):
        for entry in os.scandir(folder):
            if entry.is_dir(follow_symlinks=False):
                visit(entry.path)
            elif entry.is_file() and Path(entry.name).suffix.lower() in EXTENSIONS:
                path = Path(entry.path)
                relative = path.relative_to(IMAGES).as_posix()
                parts = relative.split('/')
                if len(parts) == 2 or len(parts) == 3 and parts[0] == 'zh-CN':
                    result[relative] = (path, entry.stat().st_size)
    visit(IMAGES)
    return result


def select_images():
    available = inventory()
    groups = collections.defaultdict(list)
    with (ROOT / 'ForgeRuntime/catalog.jsonl').open(encoding='utf-8-sig') as stream:
        for line in stream:
            card = json.loads(line)
            groups[card['name']].append(card)
    selected, missing = {}, []

    def find(key, face, language):
        suffix = '' if face == 0 else '_' + str(face + 1)
        prefix = 'zh-CN/' if language == 'zh-CN' else ''
        for extension in EXTENSIONS:
            relative = prefix + key + suffix + extension
            if relative in available:
                source, size = available[relative]
                return dict(source=relative, path=key + suffix + extension,
                            language=language, bytes=size, key=key, face=face)

    def add(record, reason):
        path = record['path']
        if path in selected:
            if selected[path]['language'] != record['language']:
                raise RuntimeError('Conflicting scan selection: ' + path)
            selected[path]['reasons'].append(reason)
        else:
            selected[path] = dict(record, reasons=[reason])

    for name, cards in sorted(groups.items()):
        usable = [card for card in cards if not card.get('ambiguous')]
        faces = 2 if any(card['splitType'] in ('Transform', 'Modal') for card in usable) else 1
        for face in range(faces):
            candidates = []
            for card in usable:
                if face and card['splitType'] not in ('Transform', 'Modal'):
                    continue
                for language in ('zh-CN', 'en'):
                    record = find(card['key'], face, language)
                    if record:
                        candidates.append(((language != 'zh-CN', card.get('artIndex') != 1, card['key']), record))
            if candidates:
                add(min(candidates, key=lambda row: row[0])[1], 'catalog:' + name)
            else:
                missing.append(dict(name=name, face=face, reason='No installed unambiguous Chinese or English scan'))

    helpers = set()
    for entry in read_json(ROOT / 'Assets/Resources/RelatedTokenArt.json')['entries']:
        helpers.update(token['key'] for token in entry['tokens'])
    for filename in ('MeldCardArt.json', 'SpecializeCardArt.json'):
        helpers.update(entry['artKey'] for entry in read_json(ROOT / 'Assets/Resources' / filename)['entries'])
    for entry in read_json(ROOT / 'Assets/Resources/SpecialEffectArt.json')['entries']:
        helpers.add(entry['key'])
        helpers.update(row['key'] for row in entry.get('localized', []) if row['language'] == 'zh-CN')
    for key in sorted(helpers):
        for face in (0, 1):
            record = find(key, face, 'zh-CN') or find(key, face, 'en')
            if record:
                add(record, 'helper:' + key)
            elif face == 0:
                missing.append(dict(key=key, face=face, reason='No installed exact helper scan'))
    images = sorted(selected.values(), key=lambda row: row['path'])
    report = dict(catalog_sha256=digest(ROOT / 'ForgeRuntime/catalog.jsonl'),
                  catalog_names=len(groups), image_faces=len(images),
                  chinese_faces=sum(row['language'] == 'zh-CN' for row in images),
                  english_fallback_faces=sum(row['language'] == 'en' for row in images),
                  image_bytes=sum(row['bytes'] for row in images), missing=missing,
                  selection='One face per canonical rules name; Chinese across all unambiguous printings first; English only if absent. Exact helper keys retained. Preferred scans use neutral CardImages paths for every interface language.',
                  images=images)
    return report


def player_files(player, decks=()):
    roots = {'MTGB.exe', 'UnityPlayer.dll', 'UnityCrashHandler64.exe', 'MonoBleedingEdge',
             'MTGB_Data', 'D3D12', 'DirectML.dll', 'WinPixEventRuntime.dll', 'ForgeRuntime'}
    files = []
    for item in sorted(player.iterdir()):
        if item.name not in roots:
            continue
        if item.is_symlink():
            raise RuntimeError('Player package must not contain links: ' + str(item))
        for path in sorted(item.rglob('*')) if item.is_dir() else [item]:
            if not path.is_file():
                continue
            relative = path.relative_to(player)
            if path.suffix.lower() in ('.pdb', '.mdb', '.meta', '.log'):
                continue
            if 'DoNotShip' in str(relative) or 'source' in relative.parts:
                continue
            files.append((path, relative.as_posix()))
    starter = ROOT / 'Decks/Starter.mdk'
    if starter.is_file():
        files.append((starter, 'Decks/Starter.mdk'))
    for deck in decks:
        deck = deck.resolve()
        if not deck.is_file() or deck.suffix.lower() != '.mdk':
            raise RuntimeError('Expected an existing .mdk deck: ' + str(deck))
        files.append((deck, 'Decks/' + deck.name))
    names = {name for _, name in files}
    if len(names) != len(files):
        raise RuntimeError('Duplicate player/deck package paths')
    if not {'MTGB.exe', 'UnityPlayer.dll', 'MTGB_Data/globalgamemanagers', 'ForgeRuntime/runtime.json'} <= names:
        raise RuntimeError('Incomplete Unity release player')
    manifest = read_json(player / 'ForgeRuntime/files.json')
    for entry in manifest['files']:
        name = 'ForgeRuntime/' + entry['path']
        if name not in names:
            raise RuntimeError('Missing runtime resource: ' + name)
    if digest(player / 'ForgeRuntime/files.json') != read_json(player / 'ForgeRuntime/runtime.json')['runtimeHash']:
        raise RuntimeError('Runtime manifest hash mismatch')
    if digest(player / 'ForgeRuntime/catalog.jsonl') != read_json(player / 'ForgeRuntime/runtime.json')['catalogHash']:
        raise RuntimeError('Catalog hash mismatch')
    return files, {'ForgeRuntime/' + entry['path']: entry['sha256'] for entry in manifest['files']}


def read_player_file(item):
    path, name = item
    data = path.read_bytes()
    return name, data, hashlib.sha256(data).hexdigest()


def read_image(record):
    data = (IMAGES / record['source']).read_bytes()
    if len(data) != record['bytes']:
        raise RuntimeError('Image changed after selection: ' + record['source'])
    with Image.open(io.BytesIO(data)) as image:
        if image.format not in ('JPEG', 'PNG') or not all(100 <= size <= 4096 for size in image.size):
            raise RuntimeError('Unsupported image format or size: ' + record['source'])
        record['size'] = list(image.size)
        image.load()
    record['sha256'] = hashlib.sha256(data).hexdigest()
    return record, data


def bounded_reads(function, items, workers):
    # Limit outstanding data buffers even on Python versions before map(buffersize).
    with concurrent.futures.ThreadPoolExecutor(max_workers=workers) as pool:
        pending = collections.deque()
        iterator = iter(items)
        for item in iterator:
            pending.append(pool.submit(function, item))
            if len(pending) >= workers * 2:
                yield pending.popleft().result()
        while pending:
            yield pending.popleft().result()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--player', type=Path)
    parser.add_argument('--deck', type=Path, action='append', default=[],
                        help='Additional .mdk deck to include; may be repeated')
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--plan-only', action='store_true')
    args = parser.parse_args()
    output = args.output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    report = select_images()
    summary = {key: value for key, value in report.items() if key not in ('images', 'missing')}
    print(json.dumps(dict(summary, missing_entries=len(report['missing'])), ensure_ascii=False), flush=True)
    report_path = output.with_suffix('.manifest.json')
    if args.plan_only:
        report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
        return
    if args.player is None:
        parser.error('--player is required when packaging')
    partial = output.with_suffix('.zip.partial')
    if output.exists() or partial.exists():
        parser.error('Output already exists; choose a fresh archive path')
    files, expected_runtime = player_files(args.player.resolve(), args.deck)
    required = report['image_bytes'] + sum(path.stat().st_size for path, _ in files) + 1024**3
    if shutil.disk_usage(output.parent).free < required:
        raise RuntimeError('Insufficient output disk space with 1 GiB reserve')
    last = time.monotonic()
    file_manifest = []
    with zipfile.ZipFile(partial, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6, allowZip64=True) as archive:
        print(f'Packing {len(files)} player/runtime files...', flush=True)
        for index, (name, data, sha256) in enumerate(bounded_reads(read_player_file, files, 8), 1):
            if name in expected_runtime and sha256 != expected_runtime[name]:
                raise RuntimeError('Changed runtime resource: ' + name)
            archive.writestr('MTGB/' + name, data)
            file_manifest.append(dict(path=name, sha256=sha256, bytes=len(data)))
            if time.monotonic() - last > 10:
                print(f'Checked and packed {index}/{len(files)} player/runtime files', flush=True)
                last = time.monotonic()
        for index, (record, data) in enumerate(bounded_reads(read_image, report['images'], 4), 1):
            archive.writestr('MTGB/CardImages/' + record['path'], data, compress_type=zipfile.ZIP_STORED)
            if time.monotonic() - last > 10:
                print(f'Checked and packed {index}/{len(report["images"])} images', flush=True)
                last = time.monotonic()
        report['player_files'] = file_manifest
        archive.writestr('MTGB/package-manifest.json', json.dumps(report, ensure_ascii=False, indent=2).encode('utf-8'))
        archive.writestr('MTGB/README.txt', 'MTGB Windows x64\n解压整个 MTGB 文件夹，运行 MTGB.exe。无需另装 Java。\n默认以 1280×720 普通窗口启动，可拖动边框调整大小。\n卡组放在 Decks 目录内（.mdk 文件）。\n卡图按规则牌名去重，中文优先，缺中文才用英文；不含日文卡图和完整异画库。\n卡图缺项及文件校验值见 package-manifest.json。\n'.encode('utf-8'))
    print('Verifying complete ZIP CRC...', flush=True)
    with zipfile.ZipFile(partial) as archive:
        bad = archive.testzip()
        if bad:
            raise RuntimeError('Archive CRC failure: ' + bad)
        if len(archive.namelist()) != len(set(archive.namelist())):
            raise RuntimeError('Duplicate archive paths')
    partial.rename(output)
    report.update(archive_bytes=output.stat().st_size, archive_sha256=digest(output), crc_verified=True)
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps({key: value for key, value in report.items() if key not in ('images', 'missing', 'player_files')}, ensure_ascii=False), flush=True)


if __name__ == '__main__':
    main()
