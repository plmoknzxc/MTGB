"""Supplement Japanese Alchemy scans from reviewed official gallery responses.

Only explicit jp/ja image URLs and exact canonical English filename identities
are accepted; a Japanese page containing English scans is not sufficient.
"""
import concurrent.futures as futures
import hashlib
import io
import json
import re
from pathlib import Path
from urllib.parse import unquote, urlparse

import requests
from PIL import Image

ROOT=Path(__file__).resolve().parents[2]
WORK=ROOT/'.tools/arena-localization/galleries'
DEST=ROOT/'CardImages/ja-JP'


def normalized(value):
    return re.sub('[^a-z0-9]','',value.casefold())


def download(job):
    try:
        path=DEST/(job['key']+'.jpg')
        for ext in ('.jpg','.jpeg','.png'):
            if path.with_suffix(ext).exists():
                with Image.open(path.with_suffix(ext)) as image:image.load()
                return dict(**job,status='existing')
        r=requests.get(job['url'],headers={'User-Agent':'MTGB-local-Japanese-card-art-import/1.0'},timeout=(15,45))
        r.raise_for_status()
        with Image.open(io.BytesIO(r.content)) as image:
            image.load()
            if min(image.size)<100:raise ValueError('Not a card scan')
            buffer=io.BytesIO();image.convert('RGB').save(buffer,'JPEG',quality=95,subsampling=0)
        data=buffer.getvalue();path.parent.mkdir(parents=True,exist_ok=True)
        temporary=path.with_suffix('.download');temporary.write_bytes(data);temporary.replace(path)
        return dict(**job,status='downloaded',sha256=hashlib.sha256(data).hexdigest())
    except Exception as error:return dict(**job,status='failed',error=str(error))


def main():
    cards=[json.loads(line) for line in (ROOT/'ForgeRuntime/catalog.jsonl').read_text(encoding='utf-8').splitlines()]
    cards=[card for card in cards if re.fullmatch(r'Y[A-Z]{3}',card['key'].split('/')[0])]
    jobs={}
    report_path=ROOT/'Tools/ForgeBridge/Reports/JapaneseAlchemyImages.json'
    current={card['key']:card['name'] for card in cards}
    # The report preserves reviewed source identities, so a clean checkout can
    # repeat the import without depending on the original temporary HTML cache.
    if report_path.exists():
        for entry in json.loads(report_path.read_text(encoding='utf-8'))['entries']:
            if current.get(entry['key'])==entry['name']:
                jobs[entry['key']]={key:entry[key] for key in ('key','name','url','gallery')}
    for path in WORK.glob('*.json'):
        gallery=json.loads(path.read_text(encoding='utf-8'))
        for image in gallery['images']:
            url=image['url'];parsed=urlparse(url)
            if parsed.hostname not in ('media.wizards.com','images.ctfassets.net'):continue
            if not re.search(r'(?:jp_|ja_|/jp/|/ja/)',parsed.path,re.I):continue
            filename=normalized(Path(unquote(parsed.path)).stem)
            for card in cards:
                if filename.endswith(normalized(card['name'])):
                    jobs.setdefault(card['key'],dict(key=card['key'],name=card['name'],url=url,gallery=gallery['url']))
    results=[]
    with futures.ThreadPoolExecutor(max_workers=4) as pool:
        for result in pool.map(download,jobs.values()):results.append(result)
    stats={status:sum(result['status']==status for result in results) for status in ('downloaded','existing','failed')}
    report=dict(availableJapaneseAlchemyImages=len(jobs),stats=stats,entries=results)
    report_path.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(stats),f'of {len(jobs)} matched official Japanese Alchemy images',flush=True)
    if stats['failed']:raise SystemExit(1)


if __name__=='__main__':main()
