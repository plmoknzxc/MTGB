"""Import exact-printing Scryfall card faces into Unity's loose image directory.

Run again to resume. Existing images are preserved. Metadata uses one bulk download;
image workers only access Scryfall's image CDN, not its API.
"""
import argparse
import collections
import concurrent.futures
import gzip
import hashlib
import io
import json
from pathlib import Path
import shutil
import threading
import time
import unicodedata

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


def normal(value):
    return unicodedata.normalize('NFKC', value or '').replace('’', "'").replace('꞉', ':').casefold().strip()


def safe(value):
    return value not in ('', '.', '..') and not any(c in value for c in '<>:"/\\|?*') and not any(ord(c) < 32 for c in value)


def bulk(refresh=False):
    manifest = CACHE / 'bulk-metadata.json'
    archive = CACHE / 'default-cards.jsonl.gz'
    if refresh or not archive.exists():
        response = session().get('https://api.scryfall.com/bulk-data/default_cards', timeout=40)
        response.raise_for_status()
        info = response.json()
        manifest.write_text(json.dumps(info, indent=2), encoding='utf8')
        url = info.get('jsonl_download_uri')
        if not url:
            raise RuntimeError('Bulk endpoint did not provide jsonl_download_uri; review API format.')
        print('Downloading bulk metadata', info.get('compressed_size'), flush=True)
        with session().get(url, stream=True, timeout=(20, 90)) as response:
            response.raise_for_status()
            with archive.with_suffix('.part').open('wb') as output:
                for block in response.iter_content(1024 * 1024):
                    output.write(block)
        archive.with_suffix('.part').replace(archive)
    return archive


def plan(archive):
    catalog = collections.defaultdict(list)
    for line in (ROOT / 'ForgeRuntime/catalog.jsonl').read_text(encoding='utf8').splitlines():
        card = json.loads(line)
        catalog[card['key']].append(card)
    index = collections.defaultdict(list)
    with gzip.open(archive, 'rt', encoding='utf8') as source:
        for line in source:
            card = json.loads(line)
            key = card.get('set', '').upper() + '/' + card.get('collector_number', '')
            if key in catalog:
                index[key].append(card)
    jobs, missing = [], []
    for key, candidates in catalog.items():
        if len(candidates) != 1 or candidates[0].get('ambiguous'):
            missing.append({'key': key, 'reason': 'ambiguous Forge printing'})
            continue
        card = candidates[0]
        code, number = key.split('/', 1)
        if not safe(code) or not safe(number):
            missing.append({'key': key, 'reason': 'unsafe filename'})
            continue
        matches = index.get(key, [])
        # Never substitute a different printing or an unrelated card with the same number.
        matches = [c for c in matches if normal(card['name']) in
                   {normal(part.get(field, '')) for part in [c, *c.get('card_faces', [])]
                    for field in ('name', 'printed_name', 'flavor_name')}]
        if len(matches) != 1:
            missing.append({'key': key, 'name': card['name'], 'reason': 'no unique exact printing/name match'})
            continue
        match = matches[0]
        if match.get('image_status') == 'placeholder':
            missing.append({'key': key, 'reason': 'source image is only a placeholder'})
            continue
        faces = [match] if match.get('image_uris') else match.get('card_faces', [])
        for face_index, face in enumerate(faces):
            url = face.get('image_uris', {}).get('normal')
            if not url:
                missing.append({'key': key, 'face': face_index, 'reason': 'source has no image'})
                continue
            if not url.startswith('https://cards.scryfall.io/'):
                raise RuntimeError('Unexpected image host: ' + url)
            suffix = '' if face_index == 0 else '_' + str(face_index + 1)
            jobs.append({'key': key, 'face': face_index, 'id': match['id'], 'url': url,
                         'path': f'{code}/{number}{suffix}.jpg', 'quality': match.get('image_status')})
        if not faces:
            missing.append({'key': key, 'reason': 'source has no faces'})
    priority = set()
    for deck in (ROOT / 'Decks').glob('*.mdk'):
        for line in deck.read_text(encoding='utf-8-sig').splitlines():
            parts = line.split()
            if len(parts) == 2 and parts[0].isdigit():
                priority.add(parts[1])
    jobs.sort(key=lambda j: (j['key'] not in priority, j['key'], j['face']))
    (CACHE / 'plan.json').write_text(json.dumps(jobs, ensure_ascii=False), encoding='utf8')
    (CACHE / 'unmatched.json').write_text(json.dumps(missing, ensure_ascii=False, indent=2), encoding='utf8')
    return jobs, missing


def download(job):
    path = DEST / job['path']
    for ext in ('.png', '.jpg', '.jpeg'):
        existing = path.with_suffix(ext)
        if existing.exists():
            try:
                with Image.open(existing) as image:
                    image.verify()
                return {**job, 'status': 'existing'}
            except Exception:
                return {**job, 'status': 'failed', 'error': 'Existing image is invalid; preserved for review'}
    if shutil.disk_usage(DEST).free < 4 * 1024**3:
        return {**job, 'status': 'failed', 'error': 'Disk reserve reached (4 GiB)'}
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
                    raise ValueError('Invalid card image dimensions/format')
                image.verify()
            path.parent.mkdir(parents=True, exist_ok=True)
            temporary = path.with_suffix('.download')
            temporary.write_bytes(data)
            temporary.replace(path)
            return {**job, 'status': 'downloaded', 'bytes': len(data), 'width': width, 'height': height,
                    'sha256': hashlib.sha256(data).hexdigest()}
        except Exception as error:
            if attempt == 3:
                return {**job, 'status': 'failed', 'error': str(error)}
            time.sleep(2 ** attempt)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--workers', type=int, default=12)
    parser.add_argument('--limit', type=int, default=0)
    parser.add_argument('--refresh-metadata', action='store_true', help='Fetch a fresh Scryfall bulk snapshot')
    args = parser.parse_args()
    CACHE.mkdir(parents=True, exist_ok=True)
    DEST.mkdir(parents=True, exist_ok=True)
    if not 1 <= args.workers <= 128:
        parser.error('--workers must be between 1 and 128')
    jobs, missing = plan(bulk(args.refresh_metadata))
    if args.limit:
        jobs = jobs[:args.limit]
    print('Planned faces:', len(jobs), 'unmatched entries:', len(missing), flush=True)
    stats = collections.Counter()
    started = last = time.monotonic()
    with (CACHE / 'results.jsonl').open('w', encoding='utf8') as journal:
        with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
            pending = {pool.submit(download, j) for j in jobs}
            for future in concurrent.futures.as_completed(pending):
                result = future.result()
                stats[result['status']] += 1
                journal.write(json.dumps(result, ensure_ascii=False) + '\n')
                if time.monotonic() - last > 10:
                    journal.flush()
                    print(dict(stats), 'of', len(jobs), 'seconds', round(time.monotonic() - started), flush=True)
                    last = time.monotonic()
    summary = {'planned_faces': len(jobs), 'unmatched': len(missing), **stats,
               'elapsed_seconds': round(time.monotonic() - started)}
    (CACHE / 'summary.json').write_text(json.dumps(summary, indent=2), encoding='utf8')
    print(summary, flush=True)
    return 1 if stats['failed'] else 0


if __name__ == '__main__':
    raise SystemExit(main())
