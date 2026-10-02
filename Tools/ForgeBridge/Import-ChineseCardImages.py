"""Install published Academy Ruins Chinese art for exact local printings.

Resumable metadata cache and per-image journal; runtime never uses the network.
WebP sources are decoded to JPEG because the Unity local loader does not accept WebP.
Existing local Chinese images are preserved. No English source is relabeled Chinese.
"""
import concurrent.futures as futures
import argparse
import hashlib
import io
import json
import re
import shutil
import threading
import time
from pathlib import Path
from urllib.parse import urlparse

import requests
from PIL import Image
from LocalizedArtIdentity import matches

ROOT = Path(__file__).resolve().parents[2]
WORK = ROOT / '.tools/chinese-card-images'
DEST = ROOT / 'CardImages/zh-CN'
REPORT = ROOT / 'Tools/ForgeBridge/Reports/ChineseCardImages.json'
WORK.mkdir(parents=True, exist_ok=True)
LOCAL = threading.local()


def session():
    if not hasattr(LOCAL, 'session'):
        LOCAL.session = requests.Session()
        LOCAL.session.headers.update({'User-Agent': 'MTGB-local-Chinese-art-import/1.0', 'Accept': 'application/json'})
    return LOCAL.session


def request(url):
    for attempt in range(4):
        try:
            response = session().get(url, timeout=(12, 40))
            response.raise_for_status()
            return response
        except requests.RequestException:
            if attempt == 3:
                raise
            time.sleep(2 ** attempt)


def metadata(code):
    path = WORK / ('set-' + code + '.json')
    if path.exists():
        return json.loads(path.read_text(encoding='utf-8'))
    response = request('https://mtgch.com/api/v1/set/' + code.lower() + '/cards/')
    response.encoding = 'utf-8'
    data = response.json()
    if data.get('total_pages', 1) > 1 or len(data.get('items', [])) != data.get('count'):
        raise ValueError('Incomplete set response: ' + code)
    temporary = path.with_suffix('.part')
    temporary.write_text(json.dumps(data, ensure_ascii=False), encoding='utf-8')
    temporary.replace(path)
    time.sleep(.15)
    return data


def normalized(name):
    return (name or '').replace('’', "'").replace('꞉', ':').casefold().strip()


def download(job):
    try:
        path = DEST / job['relative']
        for extension in ('.png', '.jpg', '.jpeg'):
            candidate = path.with_suffix(extension)
            if candidate.exists():
                with Image.open(candidate) as image:
                    image.verify()
                return dict(**job, status='existing')
        if shutil.disk_usage(ROOT).free < 4 * 1024 ** 3:
            raise OSError('Disk reserve reached (4 GiB)')
        data = request(job['url']).content
        with Image.open(io.BytesIO(data)) as image:
            image.load()
            if min(image.size) < 100:
                raise ValueError('Source is a thumbnail or placeholder')
            size = image.size
            buffer = io.BytesIO()
            image.convert('RGB').save(buffer, format='JPEG', quality=95, subsampling=0)
        output = buffer.getvalue()
        path.parent.mkdir(parents=True, exist_ok=True)
        temporary = path.with_suffix('.download')
        temporary.write_bytes(output)
        with Image.open(temporary) as image:
            image.verify()
        temporary.replace(path)
        return dict(**job, status='downloaded', size=size, bytes=len(output), sha256=hashlib.sha256(output).hexdigest())
    except Exception as error:
        return dict(**job, status='failed', error=str(error))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--workers', type=int, default=12, choices=range(1, 33))
    args = parser.parse_args()
    catalog = {}
    for line in (ROOT / 'ForgeRuntime/catalog.jsonl').read_text(encoding='utf-8').splitlines():
        card = json.loads(line)
        if not card.get('ambiguous'):
            catalog[card['key']] = card
    # Include helper/token printings used by the detail and hover views.
    token_index = ROOT / 'Assets/Resources/RelatedTokenArt.json'
    for entry in json.loads(token_index.read_text(encoding='utf-8'))['entries']:
        for token in entry['tokens']:
            catalog.setdefault(token['key'], dict(key=token['key'], name=token['name'], faces=[dict(name=token['name'])]))
    sets = sorted({key.split('/')[0] for key in catalog})
    jobs, excluded, metadata_failures, unavailable_sets = {}, [], [], []
    print(f'Checking {len(catalog)} local printings in {len(sets)} sets', flush=True)
    with futures.ThreadPoolExecutor(max_workers=3) as pool:
        pending = {pool.submit(metadata, code): code for code in sets}
        for index, done in enumerate(futures.as_completed(pending), 1):
            code = pending[done]
            try:
                data = done.result()
                if not data['items']:
                    unavailable_sets.append(code)
                for item in data['items']:
                    key = item['set'].upper() + '/' + item['collector_number']
                    card = catalog.get(key)
                    if not card:
                        continue
                    names = {normalized(card['name'])} | {normalized(face['name']) for face in card['faces']}
                    source_names={normalized(item['display_name']),normalized(item.get('display_flavor_name'))}
                    source_names.update(normalized(value) for value in item.get('former_names',[]) if isinstance(value,str))
                    if not matches(card, source_names, item.get('oracle_id')):
                        excluded.append(dict(key=key, reason='name mismatch', source=item['display_name']))
                        continue
                    faces = [item] + (item.get('other_faces') or [] if item.get('is_double_faced') else [])
                    for face_index, face in enumerate(faces):
                        url = face.get('image_url') or ''
                        parsed = urlparse(url)
                        if parsed.hostname != 'images.mtgch.com' or not parsed.path.startswith('/zhs/'):
                            continue
                        if not re.fullmatch(r'[A-Za-z0-9_.★†-]+', item['collector_number']):
                            excluded.append(dict(key=key, reason='unsafe collector number'))
                            continue
                        suffix = '' if face_index == 0 else '_' + str(face_index + 1)
                        relative = key + suffix + '.jpg'
                        jobs[relative] = dict(key=key, faceIndex=face_index, relative=relative, url=url, sourceID=item['id'])
            except requests.HTTPError as error:
                if error.response is not None and error.response.status_code == 404:
                    unavailable_sets.append(code)
                else:
                    metadata_failures.append(dict(set=code, error=str(error)))
            except Exception as error:
                metadata_failures.append(dict(set=code, error=str(error)))
            if index % 20 == 0 or index == len(sets):
                print(f'Metadata {index}/{len(sets)}; Chinese faces {len(jobs)}; failed sets {len(metadata_failures)}', flush=True)
    (WORK / 'plan.json').write_text(json.dumps(list(jobs.values()), ensure_ascii=False, indent=2), encoding='utf-8')
    stats, results, last = {}, [], time.monotonic()
    with (WORK / 'results.jsonl').open('a', encoding='utf-8') as journal, futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
        pending = [pool.submit(download, job) for job in jobs.values()]
        for index, done in enumerate(futures.as_completed(pending), 1):
            result = done.result()
            results.append(result)
            stats[result['status']] = stats.get(result['status'], 0) + 1
            journal.write(json.dumps(result, ensure_ascii=False) + '\n')
            journal.flush()
            if time.monotonic() - last > 10 or index == len(pending):
                print(f'Images {index}/{len(pending)}: {stats}', flush=True)
                last = time.monotonic()
    summary = dict(localPrintings=len(catalog), sets=len(sets), availableChineseFaces=len(jobs), stats=stats,
                   metadataFailures=metadata_failures, unavailableSets=unavailable_sets, excluded=excluded,
                   failures=[r for r in results if r['status'] == 'failed'],
                   source='https://mtgch.com', destination=str(DEST.relative_to(ROOT)))
    REPORT.write_text(json.dumps(summary, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(summary, ensure_ascii=True)[:2000], flush=True)
    if metadata_failures or stats.get('failed'):
        raise SystemExit(1)


if __name__ == '__main__':
    main()
