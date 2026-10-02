import json,os,queue,subprocess,tempfile,threading,sys
from pathlib import Path
root=Path(__file__).resolve().parents[2];runtime=root/'ForgeRuntime'
cfg=json.loads((runtime/'runtime.json').read_text(encoding='utf-8-sig'))
out=root/'Tools/Validation/Reports/Interactions';out.mkdir(parents=True,exist_ok=True)
classes=root/'.tools/selection-tests/classes';bridge=Path(os.environ.get('MTGB_BRIDGE_CLASSES',str(root/'.tools/selection-tests/bridge')))
subprocess.run(['javac','-encoding','UTF-8','-cp',str(bridge)+';'+str(runtime/'lib/*'),'-d',str(classes),str(root/'Tools/ForgeBridge/tests/InteractionFixture.java')],check=True)
deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
def run(case):
 with tempfile.TemporaryDirectory(prefix='mtgb-interaction-') as temp,(out/(case+'.log')).open('w',encoding='utf-8') as log,(out/(case+'.jsonl')).open('w',encoding='utf-8') as trace:
  start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,seed=4,startingLife=20,firstPlayerSeat=1,decks=[deck,deck])),encoding='utf-8')
  p=subprocess.Popen([str(runtime/cfg['java']),'-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,bridge,runtime/'lib/*'])),'mtgb.bridge.InteractionFixture',str(runtime/cfg['assets']),str(start)],cwd=runtime/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
  q=queue.Queue();threading.Thread(target=lambda:[q.put(json.loads(line)) for line in p.stdout],daemon=True).start()
  stage=0;chosen=False;targets=[]
  try:
   for _ in range(65):
    m=q.get(timeout=25);trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush()
    assert m['type'] not in ('fatal','rejected','ended'),m
    if m['type']!='request':continue
    r=m['request'];v=m['views'][r['seat']-1];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];pick='ok'
    print(case,stage,kind,[(o['id'],o['label'][:65]) for o in opts],flush=True)
    if kind=='InputPassPriority':
     if case.startswith('evoke') and stage==1:
      assert any(c['name']=='Endurance' for c in v['players'][0]['hand']);assert v['stackCount']==0;return
     if case=='mana' and stage==1:
      assert not next(c for c in v['players'][0]['battlefield'] if c['name']=='Mox Opal')['tapped'];assert v['stackCount']==0;stage=2;chosen=False
     elif case=='mana' and stage==3:
      assert next(c for c in v['players'][0]['battlefield'] if c['name']=='Mox Opal')['tapped'];assert v['stackCount']==0;assert sum(v['players'][0]['manaPool'])==1;return
     elif case.startswith('trigger') and stage==4 and v['stackCount']==0:
      assert next(c for c in v['players'][0]['battlefield'] if c['name']=='Welding Jar')['tapped'];return
     if not chosen and r['seat']==1 and v['phase']=='MAIN1':
      name={'mana':'Mox Opal','evoke':'Endurance','evoke-menu':'Endurance','trigger':'Skateboard','trigger-auto':'Skateboard'}[case];c=next(c for c in v['players'][0]['battlefield' if case=='mana' else 'hand'] if c['name']==name);pick='card:'+str(c['id']);chosen=True
    elif kind=='dialog':
     if case=='mana':
      assert stage in (0,2), 'Duplicate mana color popup';assert r['message']=='ManaColor';assert 'cancel' in ids
      assert not next(c for c in v['players'][0]['battlefield'] if c['name']=='Mox Opal')['tapped']
      pick='cancel' if stage==0 else next(o['id'] for o in opts if o['label']=='BLUE');stage=1 if stage==0 else 3
     elif case=='evoke-menu':assert 'cancel' in ids;pick='cancel';stage=1
     elif case=='evoke':raise AssertionError('Only payable evoke should skip ability menu: '+str(r))
    elif 'PayMana' in kind:
     assert case.startswith('trigger');pick='ok' if case=='trigger-auto' else next(o['id'] for o in opts if 'Mountain' in o['label'])
    elif case=='evoke' and kind.startswith('InputSelect'):
     assert any(o.get('card',{}).get('name')=='Grizzly Bears' for o in opts);pick='cancel';stage=1
    elif kind=='InputSelectTargets' and case.startswith('trigger'):
     assert r['sourceCard']['name']=='Skateboard' and r['abilityKind']=='trigger' and r['abilityText']
     if stage==0:pick=next(o['id'] for o in opts if o.get('card',{}).get('name')=='Ornithopter');targets=[pick];stage=1
     elif stage==1:assert any(o['id']==targets[0] and o['selected'] for o in opts);pick='reset-targets';stage=2
     elif stage==2:assert r['selectedCount']==0;pick=next(o['id'] for o in opts if o.get('card',{}).get('name')=='Welding Jar');stage=3
     else:assert r['selectedCount']==1;pick='ok';stage=4
    p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   raise AssertionError('Unresolved '+case)
  finally:
   if p.poll() is None:
    p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
    try:p.wait(timeout=3)
    except subprocess.TimeoutExpired:p.kill();p.wait()
if __name__=='__main__':
 results=[]
 try:
  for case in sys.argv[1:] or ['mana','evoke','evoke-menu','trigger']:run(case);results.append('PASS '+case);print(results[-1],flush=True)
 finally:(out/'engine-result.txt').write_text('\n'.join(results),encoding='utf-8')
