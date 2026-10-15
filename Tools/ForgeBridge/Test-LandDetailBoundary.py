"""Native land display baseline and real subtype changes, separate from gameplay proof."""
import json,os,queue,subprocess,sys,tempfile,threading
from pathlib import Path
root=Path(__file__).resolve().parents[2];runtime=root/'ForgeRuntime'
cfg=json.loads((runtime/'runtime.json').read_text(encoding='utf-8-sig'))
out=Path(sys.argv[1]) if len(sys.argv)>1 else root/'.tools/ui-browsing-boundary-1014/land-native'
out.mkdir(parents=True,exist_ok=True);classes=out/'classes';classes.mkdir(exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(runtime/'classes')+';'+str(runtime/'lib/*'),'-d',str(classes),str(root/'Tools/ForgeBridge/tests/LandDetailBoundaryFixture.java')],check=True)
deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
with tempfile.TemporaryDirectory(prefix='mtgb-land-detail-') as temp,(out/'engine.log').open('w',encoding='utf-8') as log:
 start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session='land-detail',seed=4,startingLife=20,firstPlayerSeat=1,decks=[deck,deck])),encoding='utf-8')
 p=subprocess.Popen([str(runtime/cfg['java']),'-Djava.awt.headless=true','-cp',';'.join(map(str,[classes.resolve(),runtime/'classes',runtime/'lib/*'])),'mtgb.bridge.LandDetailBoundaryFixture',str(runtime/cfg['assets']),str(start)],cwd=runtime/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
 q=queue.Queue();threading.Thread(target=lambda:[q.put(json.loads(line)) for line in p.stdout],daemon=True).start()
 try:
  states=[]
  while len(states)<5:
   m=q.get(timeout=40);assert m['type'] not in ('fatal','rejected'),m
   if m['type']=='land-detail':
    state=m['state'];states.append(state)
    (out/(state+'.json')).write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf-8')
    v=m['views'][0];v['seat']=1;(out/(state+'-view.json')).write_text(json.dumps(v),encoding='utf-8')
    shrine,forest,reef,fetch=m['lands']
    if state in ('unchanged','restored','omen-restored'):
     assert all(not c['wire']['landTypesChanged'] and not c['wire'].get('hasAbilityChanges',False) for c in m['lands'])
     assert set(shrine['mana'])=={'W','B'} and set(forest['mana'])=={'G'}
     assert 'Add {W}' in shrine['wire']['currentRules'] and 'pay 2 life' in shrine['wire']['originalRules']
    elif state=='moon':
     assert set(shrine['mana'])=={'R'} and shrine['wire']['landTypesChanged']
     assert 'Search your library' not in fetch['wire']['currentRules']
    elif state=='omen':
     assert set(shrine['mana'])==set('WUBRG') and shrine['wire']['landTypesChanged']
     assert 'Search your library' in fetch['wire']['currentRules']
   elif m['type']=='request':
    r=m['request'];p.stdin.write(json.dumps(dict(type='respond',session='land-detail',seat=r['seat'],requestId=r['requestId'],option='ok'))+'\n');p.stdin.flush()
  text='PASS native unchanged shock/basic/pain/fetch, Blood Moon removal, Prismatic Omen addition, both restorations; five board projections'
  (out/'result.txt').write_text(text,encoding='utf-8');print(text)
 except Exception as e:(out/'result.txt').write_text('FAIL '+repr(e),encoding='utf-8');raise
 finally:
  if p.poll() is None:p.kill();p.wait()
