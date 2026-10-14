"""Import full Meld results from frozen, exact Scryfall all_parts relations.

No name-only matching or inferred collector-number suffixes. Existing images
are preserved by the shared importer; unknown printings remain in missing.json.
"""
import argparse,hashlib,importlib.util,json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--metadata',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--install',action='store_true')
    args=parser.parse_args()
    rows={}
    for path in sorted(args.metadata.glob('*.json')):
        row=json.loads(path.read_text('utf8'))
        if not isinstance(row,dict) or row.get('object')!='card':continue
        def identity(card):return (card['id'],card['set'],card['collector_number'],card['name'],card.get('layout'),
            sorted((p['id'],p['component'],p['name']) for p in card.get('all_parts',[])),card.get('image_uris',{}).get('normal','').split('?')[0])
        if row['id'] in rows and identity(rows[row['id']])!=identity(row):raise ValueError('Conflicting exact source identity')
        rows[row['id']]=row
    by_key={}
    for row in rows.values():
        key=row['set'].upper()+'/'+row['collector_number']
        if key in by_key:raise ValueError('Ambiguous exact source printing '+key)
        by_key[key]=row
    entries,missing,jobs=[],[],{}
    for line in (ROOT/'ForgeRuntime/catalog.jsonl').open(encoding='utf8'):
        printing=json.loads(line)
        if printing.get('splitType')!='Meld' or len(printing['faces'])!=2:continue
        source=by_key.get(printing['key'])
        if printing.get('ambiguous') or source is None or source['name']!=printing['name']:
            missing.append({'key':printing['key'],'reason':'No exact original metadata'});continue
        candidates=[part for part in source.get('all_parts',[]) if part['component']=='meld_result' and part['name']==printing['faces'][1]['name']]
        result=rows.get(candidates[0]['id']) if len(candidates)==1 else None
        if result is None or result['name']!=printing['faces'][1]['name'] or result.get('image_status')=='placeholder':
            missing.append({'key':printing['key'],'reason':'No exact linked Meld result'});continue
        url=result.get('image_uris',{}).get('normal')
        if not url or not url.startswith('https://cards.scryfall.io/'):raise ValueError('No approved full image for '+printing['key'])
        art_key=result['set'].upper()+'/'+result['collector_number']
        # Some alternate printings explicitly link a shared result whose reverse
        # metadata lists another printing. The exact original's outbound link is
        # authoritative; retain the reverse-printing difference for review.
        entries.append({'key':printing['key'],'state':'Meld','name':result['name'],'artKey':art_key,'sourceId':source['id'],'resultId':result['id'],'source':result['scryfall_uri'],
            'reversePrintingMatched':any(p['id']==source['id'] and p['component']=='meld_part' for p in result.get('all_parts',[]))})
        jobs[art_key]={'key':art_key,'face':0,'url':url,'path':art_key+'.jpg','id':result['id'],'quality':result['image_status']}
    args.output.mkdir(parents=True,exist_ok=True)
    def write(name,data):(args.output/name).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n','utf8')
    write('index.json',{'entries':entries});write('missing.json',missing);write('plan.json',list(jobs.values()))
    spec=importlib.util.spec_from_file_location('images',ROOT/'Tools/ForgeBridge/Import-CardImages.py')
    importer=importlib.util.module_from_spec(spec);spec.loader.exec_module(importer)
    results=[importer.download(job) for job in jobs.values()];write('results.json',results)
    if any(r['status']=='failed' for r in results):raise RuntimeError('Provisioning failed; index not installed')
    write('source-guards.json',[{'path':str(p),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(args.metadata.glob('*.json'))])
    if args.install:(ROOT/'Assets/Resources/MeldCardArt.json').write_bytes((args.output/'index.json').read_bytes())
    print(json.dumps({'entries':len(entries),'faces':len(jobs),'missing':len(missing),'installed':args.install}))

if __name__=='__main__':main()
