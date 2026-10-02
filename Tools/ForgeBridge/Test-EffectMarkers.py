"""Cast Ephemerate through Forge, verify its rebound marker and disappearance at upkeep."""
import importlib.util,json,os,queue,subprocess,tempfile,threading
from pathlib import Path
spec=importlib.util.spec_from_file_location('base',Path(__file__).with_name('Test-AdvancedInteractions.py'))
b=importlib.util.module_from_spec(spec);spec.loader.exec_module(b)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(b.runtime/'classes')+';'+str(b.runtime/'lib/*'),'-d',str(b.classes),str(b.root/'Tools/ForgeBridge/tests/EffectMarkersFixture.java')],check=True)
out=b.root/'Tools/Validation/Reports/EffectMarkers';out.mkdir(parents=True,exist_ok=True)
with tempfile.TemporaryDirectory() as temp,(out/'engine.log').open('w',encoding='utf8') as log:
 cfg=Path(temp)/'start.json';cfg.write_text(json.dumps(dict(session='markers',seed=4,startingLife=20,firstPlayerSeat=1,decks=[b.deck,b.deck])),encoding='utf8')
 p=subprocess.Popen([str(b.runtime/b.cfg['java']),'-Djava.awt.headless=true','-cp',str(b.classes)+';'+str(b.runtime/'classes')+';'+str(b.runtime/'lib/*'),'mtgb.bridge.EffectMarkersFixture',str(b.runtime/b.cfg['assets']),str(cfg)],cwd=b.runtime/b.cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
 q=queue.Queue();threading.Thread(target=lambda:[q.put(json.loads(line)) for line in p.stdout],daemon=True).start()
 cast=False;seen=False
 try:
  for step in range(180):
   m=q.get(timeout=45);assert m['type'] not in ('fatal','ended','rejected'),m
   if m['type']!='request':continue
   r=m['request'];v=m['views'][0];one=v['players'][0];opts=r['options'];ids=[o['id'] for o in opts];pick='ok'
   markers=one.get('effectMarkers',[])
   print(step,r['kind'],v['turn'],v['phase'],len(markers),flush=True)
   if markers and not seen:
    assert markers[0]['name']=='Ephemerate' and markers[0]['markerKind']=='delayed'
    assert 'upkeep' in markers[0]['currentRules'].lower() and markers[0]['printingKey']
    assert m['views'][1]['players'][0]['effectMarkers']==markers
    saved=dict(v);saved.update(request=r,awaitingInput=r['seat']==1)
    (out/'rebound.json').write_text(json.dumps(saved),encoding='utf8');seen=True
   if seen and not markers and v['turn']>=3:
    (out/'result.json').write_text('{"status":"PASS"}',encoding='utf8');print('PASS: actual cast, both viewers, rebound marker registered and removed at upkeep');break
   if r['kind']=='InputPassPriority' and r['seat']==1 and not cast and v['phase']=='MAIN1':
    pick='card:'+str(next(c['id'] for c in one['hand'] if c['name']=='Ephemerate'));cast=True
   elif r['kind']=='InputSelectCardsFromList':
    pick=next(o['id'] for o in opts if o['id'].startswith('card:') and not o.get('selected',False)) if r.get('remainingCount',0)>0 else 'ok'
   elif r['kind'].startswith('InputSelect'):
    pick='ok' if 'ok' in ids else next(i for i in ids if i.startswith('card:'))
   elif r['kind'].startswith('InputPayMana'):
    pick='ok' if 'ok' in ids else next((i for i in ids if i.startswith('mana:')),None) or next(o['id'] for o in opts if o['label'].startswith('Plains ['))
   elif pick not in ids:pick=ids[0]
   assert pick in ids,(r,pick)
   p.stdin.write(json.dumps(dict(type='respond',session='markers',seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
  else:raise AssertionError('Step limit')
 finally:
  if p.poll() is None:p.kill();p.wait()
