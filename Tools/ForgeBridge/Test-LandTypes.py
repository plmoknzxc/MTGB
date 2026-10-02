"""CR 305.6-8 / 613.1d / 613.7: audit shipped land-type effects, restoration and wire data."""
import json, os, queue, subprocess, tempfile, threading
from pathlib import Path
root=Path(__file__).resolve().parents[2]
runtime=root/'ForgeRuntime'
cfg=json.loads((runtime/'runtime.json').read_text(encoding='utf-8-sig'))
classes=root/'.tools/land-type-tests/classes'
out=root/'Tools/Validation/Reports/LandTypes';out.mkdir(parents=True,exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(runtime/'classes')+';'+str(runtime/'lib/*'),'-d',str(classes),str(root/'Tools/ForgeBridge/tests/LandTypeFixture.java')],check=True)
deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
cases=[('blood',['Blood Moon'],{'R'}),('blue',['Harbinger of the Seas'],{'U'}),('omen',['Prismatic Omen'],set('WUBRG')),('dryad',['Dryad of the Ilysian Grove'],set('WUBRG')),('blood-blue',['Blood Moon','Harbinger of the Seas'],{'U'}),('blue-blood',['Harbinger of the Seas','Blood Moon'],{'R'}),('blood-omen',['Blood Moon','Prismatic Omen'],set('WUBRG')),('omen-blood',['Prismatic Omen','Blood Moon'],{'R'})]
results=[]
for case,effects,expected in cases:
 with tempfile.TemporaryDirectory(prefix='mtgb-landtypes-') as temp,(out/(case+'.log')).open('w',encoding='utf-8') as log:
  start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,seed=4,startingLife=20,firstPlayerSeat=1,decks=[deck,deck],effects=effects)),encoding='utf-8')
  p=subprocess.Popen([str(runtime/cfg['java']),'-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,runtime/'classes',runtime/'lib/*'])),'mtgb.bridge.LandTypeFixture',str(runtime/cfg['assets']),str(start)],cwd=runtime/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
  q=queue.Queue();threading.Thread(target=lambda:[q.put(json.loads(line)) for line in p.stdout],daemon=True).start()
  captured=[]
  try:
   for _ in range(100):
    msg=q.get(timeout=30)
    assert msg['type']!='fatal',msg
    if msg['type'] in ('land-audit','land-restored'):
     captured.append(msg)
     if msg['type']=='land-restored':break
    elif msg['type']=='request':
     r=msg['request'];opts=r['options'];pick=next((o for o in opts if o['id']=='ok'),opts[0])
     p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick['id']))+'\n');p.stdin.flush()
   assert len(captured)==2,captured
   (out/(case+'.json')).write_text(json.dumps(captured,ensure_ascii=False,indent=2),encoding='utf-8')
   for snapshot in captured:
    suffix='active' if snapshot['type']=='land-audit' else 'restored'
    (out/(case+'-'+suffix+'-view.json')).write_text(json.dumps(snapshot['views'][0],ensure_ascii=False,indent=2),encoding='utf-8')
    for land in snapshot['lands']:
     wire=land['wire']
     assert wire['currentType']==land['type'],(case,land)
     assert wire['hasCurrentLandState'],(case,land)
     assert '<span' not in wire['currentRules'],(case,land)
   fetch,forest,arbor,saga,enemy=captured[0]['lands']
   assert fetch['wire']['landTypesChanged'],fetch
   assert ('Search your library' in fetch['wire']['currentRules'])==(case in ('omen','dryad')),fetch
   for symbol in expected: assert '{'+symbol+'}' in fetch['wire']['currentRules'],fetch
   assert set(fetch['mana'])==expected,(case,fetch)
   assert ('Sacrifice' in ' '.join(fetch['abilities']))==(case in ('omen','dryad')),(case,fetch)
   assert 'Basic' not in fetch['type'] and 'Basic' in forest['type'],(case,fetch,forest)
   assert 'Creature' in arbor['type'] and 'Dryad' in arbor['type'],(case,arbor)
   assert 'Enchantment' in saga['type'] and 'Saga' in saga['type'],(case,saga)
   assert set(forest['mana'])==(set('WUBRG') if any(x in effects for x in ('Prismatic Omen','Dryad of the Ilysian Grove')) else {'G'}),(case,forest)
   assert set(enemy['mana'])==(set() if case in ('omen','dryad') else {'U'} if effects[-1]=='Harbinger of the Seas' else {'R'}),(case,enemy)
   original=captured[1]['lands'][0]
   assert original['type']=='Land' and not original['mana'] and 'Sacrifice' in ' '.join(original['abilities']),original
   assert not original['wire']['landTypesChanged'] and 'Search your library' in original['wire']['currentRules'],original
   results.append('PASS '+case+'; engine types/mana/abilities, controller scope, supertypes, removal restoration')
   print(results[-1],flush=True)
  finally:
   if p.poll() is None:
    p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
    try:p.wait(timeout=3)
    except subprocess.TimeoutExpired:p.kill();p.wait()
(out/'result.txt').write_text('\n'.join(results),encoding='utf-8')
