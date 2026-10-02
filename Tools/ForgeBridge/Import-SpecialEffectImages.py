"""Provision local helper cards only; never fetch images during gameplay."""
import gzip,json,importlib.util,requests,io
from PIL import Image
from pathlib import Path
root=Path(__file__).resolve().parents[2]
spec=importlib.util.spec_from_file_location('importer',Path(__file__).with_name('Import-RelatedTokenImages.py'));imp=importlib.util.module_from_spec(spec);spec.loader.exec_module(imp)
wanted={'The Monarch','The Initiative',"City's Blessing",'Radiation','The Ring','The Ring Tempts You','Day','Night','Start Your Engines!','Max Speed','Undercity','Dungeon of the Mad Mage','Lost Mine of Phandelver','Tomb of Annihilation','Energy Reserve','Experience','Poison Counter'}
rows={name:[] for name in wanted}
with gzip.open(root/'.tools/card-images/default-cards.jsonl.gz','rt',encoding='utf8') as f:
 for line in f:
  c=json.loads(line)
  if c.get('type_line') not in {'Card','Dungeon'} and not c['set'].startswith('t'):continue
  faces=[c] if c.get('image_uris') else c.get('card_faces',[])
  for i,face in enumerate(faces):
   if face.get('name') in rows and face.get('image_uris',{}).get('normal'):
    key=c['set'].upper()+'/'+c['collector_number'];suffix='' if i==0 else '_2'
    file=f"{c['set'].upper()}/{c['collector_number']}{suffix}.jpg"
    rows[face['name']].append(dict(name=face['name'],key=key,face=i,path=file,url=face['image_uris']['normal'],source=c['scryfall_uri']))
entries=[];results=[]
for name,candidates in sorted(rows.items()):
 if not candidates:raise RuntimeError('Missing helper metadata: '+name)
 candidates.sort(key=lambda c:(not (root/'CardImages'/c['path']).exists(),not c['key'].startswith('T'),c['key']))
 job=candidates[0];result=imp.download(job);results.append(result)
 if result['status']=='failed':raise RuntimeError(result)
 entries.append({k:job[k] for k in ['name','key','face','source']})
story=root/'CardImages/THOB/14.png'
story_url='https://cdn11.bigcommerce.com/s-b4ioc4fed9/images/stencil/960w/products/587123/4104261/vJwdfAfXWig9hGza69GMFIuZo__43254.1789459577.png?c=1'
if not story.exists():
 response=requests.get(story_url,timeout=30);response.raise_for_status()
 with Image.open(io.BytesIO(response.content)) as image:
  assert image.format=='PNG' and min(image.size)>=500;image.verify()
 story.parent.mkdir(parents=True,exist_ok=True);story.write_bytes(response.content)
with Image.open(story) as image:image.verify()
entries.append(dict(name='An Enduring Story',key='THOB/14',face=0,source='https://magicmadhouse.co.uk/magic-the-gathering-the-hobbit-the-hobbit-enduring-story-token'))
index_path=root/'Assets/Resources/SpecialEffectArt.json'
if index_path.exists():
 previous={e['name']:e for e in json.loads(index_path.read_text(encoding='utf8'))['entries']}
 for entry in entries:
  if previous.get(entry['name'],{}).get('localized'):entry['localized']=previous[entry['name']]['localized']
index_path.write_text(json.dumps(dict(entries=entries),ensure_ascii=False,indent=2),encoding='utf8')
out=root/'Tools/Validation/Reports/SpecialEffectAssets';out.mkdir(parents=True,exist_ok=True)
(out/'import.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf8')
print('PASS: '+str(len(entries))+' exact helper faces provisioned; '+str(sum(r['status']=='downloaded' for r in results))+' downloaded')
