"""Install exact-printing Japanese scans from Scryfall's all-language bulk data.

Matching requires local set, collector number and English face identity. Images
remain local at runtime. Existing Japanese images are preserved and validated.
"""
import concurrent.futures as futures
import gzip
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

ROOT=Path(__file__).resolve().parents[2]
WORK=ROOT/'.tools/japanese-card-images'
DEST=ROOT/'CardImages/ja-JP'
REPORT=ROOT/'Tools/ForgeBridge/Reports/JapaneseCardImages.json'
LOCAL=threading.local()


def session():
    if not hasattr(LOCAL,'session'):
        LOCAL.session=requests.Session()
        LOCAL.session.headers.update({'User-Agent':'MTGB-local-Japanese-card-art-import/1.0','Accept':'application/json'})
    return LOCAL.session


def request(url,stream=False):
    for attempt in range(4):
        try:
            response=session().get(url,stream=stream,timeout=(15,60))
            response.raise_for_status()
            return response
        except requests.RequestException:
            if attempt==3:raise
            time.sleep(2**attempt)


def normalized(name):
    return (name or '').replace('’',"'").replace('꞉',':').casefold().strip()


def plan():
    catalog={}
    for line in (ROOT/'ForgeRuntime/catalog.jsonl').read_text(encoding='utf-8').splitlines():
        card=json.loads(line)
        if not card.get('ambiguous'):catalog[card['key']]=card
    for entry in json.loads((ROOT/'Assets/Resources/RelatedTokenArt.json').read_text(encoding='utf-8'))['entries']:
        for token in entry['tokens']:
            catalog.setdefault(token['key'],dict(key=token['key'],name=token['name'],faces=[dict(name=token['name'])]))
    path=WORK/'all-cards.jsonl.gz'
    metadata=request('https://api.scryfall.com/bulk-data/all_cards').json()
    if not path.exists():
        url=metadata['jsonl_download_uri']
        temporary=path.with_suffix('.part')
        downloaded,last=0,time.monotonic()
        with request(url,stream=True) as response,temporary.open('wb') as output:
            for chunk in response.iter_content(1024*1024):
                output.write(chunk);downloaded+=len(chunk)
                if time.monotonic()-last>10:
                    print(f'Bulk metadata {downloaded//1024//1024} MiB',flush=True);last=time.monotonic()
        if downloaded!=metadata['compressed_size']:raise ValueError('Bulk download length mismatch')
        temporary.replace(path)
        (WORK/'bulk-source.json').write_text(json.dumps(metadata,indent=2),encoding='utf-8')
    jobs,excluded={},[]
    scanned=0
    with gzip.open(path,'rt',encoding='utf-8') as source:
        for line in source:
            card=json.loads(line);scanned+=1
            if card.get('lang')!='ja' or card.get('image_status')=='placeholder':continue
            key=card['set'].upper()+'/'+card['collector_number']
            local=catalog.get(key)
            if not local:continue
            names={normalized(local['name'])}|{normalized(face['name']) for face in local['faces']}
            source_names={normalized(card['name'])}|{normalized(face['name']) for face in card.get('card_faces',[])}
            source_names.add(normalized(card.get('flavor_name')))
            source_names.update(normalized(face.get('flavor_name')) for face in card.get('card_faces', []))
            if not matches(local, source_names, card.get('oracle_id')):
                excluded.append(dict(key=key,reason='name mismatch',source=card['name']));continue
            if not re.fullmatch(r'[A-Za-z0-9_.★†-]+',card['collector_number']):continue
            faces=[card] if card.get('image_uris') else card.get('card_faces',[])
            for index,face in enumerate(faces):
                url=(face.get('image_uris') or {}).get('normal','')
                if urlparse(url).hostname!='cards.scryfall.io':continue
                suffix='' if index==0 else '_'+str(index+1)
                relative=key+suffix+'.jpg'
                jobs[relative]=dict(key=key,faceIndex=index,relative=relative,url=url,sourceID=card['id'])
    (WORK/'plan.json').write_text(json.dumps(list(jobs.values()),indent=2),encoding='utf-8')
    print(f'Matched {len(jobs)} Japanese faces across {len(catalog)} local printings; scanned {scanned} source cards',flush=True)
    return list(jobs.values()),excluded


def download(job):
    try:
        path=DEST/job['relative']
        for extension in ('.png','.jpg','.jpeg'):
            existing=path.with_suffix(extension)
            if existing.exists():
                with Image.open(existing) as image:image.load()
                return dict(**job,status='existing')
        if shutil.disk_usage(ROOT).free<4*1024**3:raise OSError('4 GiB disk reserve reached')
        data=request(job['url']).content
        with Image.open(io.BytesIO(data)) as image:
            image.load();size=image.size
            if image.format!='JPEG' or min(size)<100:raise ValueError('Unexpected image format or placeholder')
        path.parent.mkdir(parents=True,exist_ok=True)
        temporary=path.with_suffix('.download');temporary.write_bytes(data);temporary.replace(path)
        return dict(**job,status='downloaded',size=size,bytes=len(data),sha256=hashlib.sha256(data).hexdigest())
    except Exception as error:return dict(**job,status='failed',error=str(error))


def main():
    WORK.mkdir(parents=True,exist_ok=True)
    jobs,excluded=plan();stats,results,last={},[],time.monotonic()
    with futures.ThreadPoolExecutor(max_workers=16) as pool,(WORK/'results.jsonl').open('a',encoding='utf-8') as journal:
        pending=[pool.submit(download,job) for job in jobs]
        for index,done in enumerate(futures.as_completed(pending),1):
            result=done.result();results.append(result);stats[result['status']]=stats.get(result['status'],0)+1
            journal.write(json.dumps(result,ensure_ascii=False)+'\n');journal.flush()
            if time.monotonic()-last>10 or index==len(jobs):
                print(f'Japanese images {index}/{len(jobs)}: {stats}',flush=True);last=time.monotonic()
    summary=dict(availableJapaneseFaces=len(jobs),stats=stats,excluded=excluded,failures=[r for r in results if r['status']=='failed'],
                 source='https://api.scryfall.com/bulk-data/all_cards',destination=str(DEST.relative_to(ROOT)))
    REPORT.write_text(json.dumps(summary,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(summary,ensure_ascii=True)[:1800],flush=True)
    if stats.get('failed'):raise SystemExit(1)


if __name__=='__main__':main()
