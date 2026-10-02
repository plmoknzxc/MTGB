"""Import exact-printing images referenced by the local related-token index."""
import argparse
import collections
import concurrent.futures
import gzip
import io
import json
import os
from pathlib import Path
import shutil
import threading
import time

import requests
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
CACHE = ROOT / '.tools/card-images'
DEST = ROOT / 'CardImages'
HEADERS = {'User-Agent': 'MTGB-CardArtImporter/1.0', 'Accept': 'application/json'}
local = threading.local()


def session():
    if not hasattr(local, 'http'):
        local.http = requests.Session()
        local.http.headers.update(HEADERS)
    return local.http


def safe(value):
    return value not in ('', '.', '..') and not any(c in value for c in '<>:"/\\|?*') and not any(ord(c) < 32 for c in value)


def plan(emblems_only=False):
    index = json.loads((ROOT / 'Assets/Resources/RelatedTokenArt.json').read_text(encoding='utf8'))
    keys = {token['key'] for entry in index['entries'] for token in entry['tokens'] if token.get('key')}
    rows = collections.defaultdict(list)
    archive = CACHE / 'default-cards.jsonl.gz'
    with gzip.open(archive, 'rt', encoding='utf8') as source:
        for line in source:
            card = json.loads(line)
            key = card.get('set', '').upper() + '/' + card.get('collector_number', '')
            if key in keys and (not emblems_only or card.get('type_line') == 'Emblem'):
                rows[key].append(card)
    if emblems_only:
        keys = set(rows)

    jobs, missing = [], []
    for key in sorted(keys):
        parts = key.split('/', 1)
        if len(parts) != 2 or not safe(parts[0]) or not safe(parts[1]):
            missing.append({'key': key, 'reason': 'unsafe filename'})
            continue
        matches = rows.get(key, [])
        if len(matches) != 1:
            missing.append({'key': key, 'reason': f'expected one exact bulk row, found {len(matches)}'})
            continue
        card = matches[0]
        faces = [card] if card.get('image_uris') else card.get('card_faces', [])
        if not faces:
            missing.append({'key': key, 'reason': 'source has no image faces'})
            continue
        for face_index, face in enumerate(faces):
            url = face.get('image_uris', {}).get('normal')
            if not url:
                missing.append({'key': key, 'face': face_index, 'reason': 'source has no normal image'})
                continue
            if not url.startswith('https://cards.scryfall.io/'):
                raise RuntimeError('Unexpected image host: ' + url)
            suffix = '' if face_index == 0 else '_2'
            jobs.append({'key': key, 'face': face_index, 'url': url,
                         'path': f'{parts[0]}/{parts[1]}{suffix}.jpg'})
    return jobs, missing


def valid_image(path):
    with Image.open(path) as image:
        image.verify()
        return image.format == 'JPEG' and min(image.size) >= 100


def download(job):
    path = DEST / job['path']
    resolved = path.resolve()
    root = str(DEST.resolve())
    if os.path.commonpath([root, str(resolved)]).casefold() != root.casefold():
        return {**job, 'status': 'failed', 'error': 'path escaped CardImages'}
    for ext in ('.png', '.jpg', '.jpeg'):
        existing = path.with_suffix(ext)
        if existing.exists():
            try:
                if not valid_image(existing):
                    raise ValueError('existing image is not a full-size JPEG')
                return {**job, 'status': 'existing'}
            except Exception:
                return {**job, 'status': 'failed', 'error': 'existing image is invalid; preserved'}
    if shutil.disk_usage(DEST).free < 4 * 1024**3:
        return {**job, 'status': 'failed', 'error': '4 GiB free-space reserve reached'}
    for attempt in range(4):
        try:
            response = session().get(job['url'], timeout=(15, 45), headers={'Accept': 'image/jpeg'})
            if response.status_code == 429:
                time.sleep(min(60, int(response.headers.get('Retry-After', '10'))))
            response.raise_for_status()
            data = response.content
            with Image.open(io.BytesIO(data)) as image:
                width, height = image.size
                if image.format != 'JPEG' or min(width, height) < 100:
                    raise ValueError('invalid card image format or dimensions')
                image.verify()
            path.parent.mkdir(parents=True, exist_ok=True)
            temporary = path.with_suffix('.download')
            temporary.write_bytes(data)
            temporary.replace(path)
            return {**job, 'status': 'downloaded', 'bytes': len(data), 'width': width, 'height': height}
        except Exception as error:
            if attempt == 3:
                return {**job, 'status': 'failed', 'error': str(error)}
            time.sleep(2 ** attempt)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--workers', type=int, default=12)
    parser.add_argument('--emblems-only', action='store_true', help='Provision only locally indexed emblem images')
    args = parser.parse_args()
    if not 1 <= args.workers <= 32:
        parser.error('--workers must be between 1 and 32')
    CACHE.mkdir(parents=True, exist_ok=True)
    DEST.mkdir(parents=True, exist_ok=True)
    jobs, missing = plan(args.emblems_only)
    print('Planned faces:', len(jobs), 'unmatched:', len(missing), flush=True)
    stats = collections.Counter()
    total_bytes = 0
    started = time.monotonic()
    prefix = 'emblem' if args.emblems_only else 'related-token'
    with (CACHE / (prefix + '-results.jsonl')).open('w', encoding='utf8') as journal:
        with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
            futures = [pool.submit(download, job) for job in jobs]
            for count, future in enumerate(concurrent.futures.as_completed(futures), 1):
                result = future.result()
                stats[result['status']] += 1
                total_bytes += result.get('bytes', 0)
                journal.write(json.dumps(result, ensure_ascii=False) + '\n')
                if count % 100 == 0 or count == len(futures):
                    journal.flush()
                    print(count, 'of', len(futures), dict(stats), flush=True)
    if missing:
        (CACHE / (prefix + '-unmatched.json')).write_text(
            json.dumps(missing, ensure_ascii=False, indent=2), encoding='utf8')
    summary = {'planned_faces': len(jobs), 'unmatched': len(missing), **stats,
               'downloaded_bytes': total_bytes, 'elapsed_seconds': round(time.monotonic() - started)}
    (CACHE / (prefix + '-summary.json')).write_text(json.dumps(summary, indent=2), encoding='utf8')
    print(summary, flush=True)
    return 1 if stats['failed'] or missing else 0


if __name__ == '__main__':
    raise SystemExit(main())
