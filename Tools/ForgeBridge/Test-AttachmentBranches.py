"""R41 real attachment costs, retarget, detach, control, copy and player identities."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/'.tools/attachments-1007/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURES=[ROOT/'Tools/ForgeBridge/tests/AttachmentFixture.java',ROOT/'Tools/ForgeBridge/tests/CostInteractionFixture.java']
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES={
 'equip-retarget':dict(hand=['Naturalize'],field=['Bonesplitter','Grizzly Bears','Grizzly Bears']+['Forest']*4,actions=[('equip','weapon','bear1'),('equip','weapon','bear2'),('cast','Naturalize','weapon')]),
 'aura-detach':dict(hand=['Rancor','Unsummon'],field=['Grizzly Bears','Forest','Island'],actions=[('cast','Rancor','bear1'),('cast','Unsummon','bear1')]),
 'equipment-detach':dict(hand=['Unsummon'],field=['Bonesplitter','Grizzly Bears','Forest','Island'],actions=[('equip','weapon','bear1'),('cast','Unsummon','bear1')]),
 'control-host':dict(hand=['Donate'],field=['Bonesplitter','Grizzly Bears','Grizzly Bears']+['Island']*5,actions=[('equip','weapon','bear1'),('cast','Donate','donate'),('equip','weapon','bear2')]),
 'copy-equipment':dict(hand=['Sculpting Steel'],field=['Bonesplitter','Grizzly Bears','Grizzly Bears']+['Island']*5,actions=[('equip','weapon','bear1'),('cast','Sculpting Steel','weapon'),('equip','copy','bear2')]),
 'aura-player':dict(hand=['Curse of Opulence'],field=['Mountain'],actions=[('cast','Curse of Opulence','player')]),
 'aura-player-self':dict(hand=['Curse of Opulence'],field=['Mountain'],actions=[('cast','Curse of Opulence','self-player')]),
 'copy-aura':dict(hand=['Rancor','Copy Enchantment'],field=['Grizzly Bears','Grizzly Bears','Forest']+['Island']*3,actions=[('cast','Rancor','bear1'),('cast','Copy Enchantment','Rancor')]),
}
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def run(case):
 spec=CASES[case];folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir();relations=folder/'relations';relations.mkdir()
 meta=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driverSha256=sha(__file__),fixtures=[dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p)) for p in FIXTURES])
 (folder/'driver.py').write_bytes(Path(__file__).read_bytes())
 for p in FIXTURES:(folder/p.name).write_bytes(p.read_bytes())
 def finish(status,why):
  r=dict(meta,status=status,result=status+' '+case+': '+why)
  for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return r
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as tmp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
  start=Path(tmp)/'start.json';start.write_text(json.dumps(dict(session=case,spec=dict(hand=spec['hand'],field=spec['field'],enemy=[],grave=[]),relationsDir=str(relations),auditDir=str(audit),seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK])),encoding='utf8');(folder/'start.json').write_bytes(start.read_bytes())
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+tmp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.AttachmentFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
  messages=queue.Queue()
  def reader():
   try:
    for line in proc.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,label):
   for seat,v in enumerate(m['views'],1):(folder/(label+('-other' if seat==2 else '')+'.json')).write_text(json.dumps(dict(v,request=m['request'],awaitingInput=True),ensure_ascii=False,indent=2),encoding='utf8')
  def observation(r):
   until=time.monotonic()+5
   while time.monotonic()<until:
    try:return json.loads((relations/(str(r['requestId'])+'.json')).read_text(encoding='utf8'))
    except (OSError,json.JSONDecodeError):time.sleep(.01)
   raise AssertionError('Missing native attachment observation '+str(r['requestId']))
  refs={};stage=0;active=False;responded=set();selected_targets=0
  try:
   for step in range(230):
    m=messages.get(timeout=35);assert m is not None,'Native exited';trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
    if m['type']!='request':continue
    r=m['request'];v=m['views'][0];own=v['players'][0];other=v['players'][1];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts];pick='ok'
    if r['requestId'] in responded:continue
    assert not other['hand'] and not m['views'][1]['players'][0]['hand'],'Private hand leak'
    all_cards=own['battlefield']+other['battlefield'];byid={c['id']:c for c in all_cards}
    if kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
     if not refs:
      bears=[c for c in own['battlefield'] if c['name']=='Grizzly Bears'];refs.update({f'bear{i+1}':c['id'] for i,c in enumerate(bears)})
      refs.update({c['name']:c['id'] for c in own['hand']})
      if any(c['name']=='Bonesplitter' for c in all_cards):refs['weapon']=next(c['id'] for c in all_cards if c['name']=='Bonesplitter')
      refs['player']=other['targetId'];snap(m,'before')
     if active:
      native=observation(r);nb={c['id']:c for c in native};snap(m,'after-'+str(stage))
      # This comparison intentionally detects a missing bridge projection.
      for c in all_cards:
       expected=nb[c['id']]
       for field in ['attachedToKind','attachedToId','attachedByIds']:assert c.get(field)==expected[field],('Missing/wrong attachment projection',c['id'],field,c.get(field),expected[field])
      if case.startswith('aura-player'):assert nb[refs['Curse of Opulence']]['attachedToKind']=='player' and nb[refs['Curse of Opulence']]['attachedToId']==(own if case.endswith('-self') else other)['targetId']
      elif stage==0:
       source=refs.get('weapon',refs.get('Rancor'));assert nb[source]['attachedToId']==refs['bear1'];assert source in nb[refs['bear1']]['attachedByIds'];assert byid[refs['bear1']]['power']==4
      elif case=='equip-retarget':
       if stage==1:assert nb[refs['weapon']]['attachedToId']==refs['bear2'] and byid[refs['bear1']]['power']==2 and byid[refs['bear2']]['power']==4
       else:assert refs['weapon'] not in byid and any(c['id']==refs['weapon'] for c in own['graveyardCards']) and byid[refs['bear2']]['power']==2 and not nb[refs['bear2']]['attachedByIds']
      elif case in ['aura-detach','equipment-detach']:
       assert refs['bear1'] not in byid and any(c['id']==refs['bear1'] for c in own['hand'])
       if case=='aura-detach':assert any(c['id']==refs['Rancor'] for c in own['hand'])
       else:assert refs['weapon'] in byid and nb[refs['weapon']]['attachedToId']==0
      elif case=='control-host':
       assert nb[refs['bear1']]['controllerSeat']==2 and nb[refs['bear1']]['ownerSeat']==1 and nb[refs['weapon']]['controllerSeat']==1
       assert nb[refs['weapon']]['attachedToId']==refs['bear1' if stage==1 else 'bear2']
       if stage==2:assert byid[refs['bear1']]['power']==2 and byid[refs['bear2']]['power']==4
      elif case=='copy-equipment':
       copies=[c for c in all_cards if c.get('copy')];assert len(copies)==1;refs['copy']=copies[0]['id'];assert refs['copy']==refs['Sculpting Steel']
       assert nb[refs['weapon']]['attachedToId']==refs['bear1'] and nb[refs['copy']]['attachedToId']==(0 if stage==1 else refs['bear2'])
       if stage==2:assert byid[refs['bear1']]['power']==4 and byid[refs['bear2']]['power']==4
      elif case=='copy-aura':
       copy=byid[refs['Copy Enchantment']];assert copy['copy'] and copy['name']=='Rancor'
       assert nb[refs['Rancor']]['attachedToId']==refs['bear1'] and nb[copy['id']]['attachedToId']==refs['bear2']
       assert byid[refs['bear1']]['power']==4 and byid[refs['bear2']]['power']==4
      for pv in m['views']:
       for p in pv['players']:
        for c in p['battlefield']:assert all(c.get(f)==nb[c['id']][f] for f in ['attachedToKind','attachedToId','attachedByIds'])
      stage+=1;active=False
      if stage==len(spec['actions']):
       assert own['life']==20 and other['life']==20 and not any(own['manaPool'])
       snap(m,'resolved');return finish('PASS','actual costs/targets and native attachment identity through '+case+', source/host IDs and power/control/zone consequences, both projections/private hands and main priority recover')
     mode,source,target=spec['actions'][stage];source=refs[source];pick='card:'+str(source);assert pick in ids,(case,stage,r);active=True;selected_targets=0;snap(m,'action-'+str(stage))
    elif kind=='InputSelectTargets':
     target=spec['actions'][stage][2]
     if r.get('selectedCount',0)>=r.get('minCount',1):pick='ok'
     elif target in ['player','self-player','donate'] and not any(o['id'].startswith('card:') for o in opts):pick='player:'+str((own if target=='self-player' else other)['targetId'])
     else:pick='card:'+str(refs['bear1' if target=='donate' else target])
     snap(m,'targets-'+str(stage)+'-'+str(r.get('selectedCount',0)))
    elif kind.startswith('InputPayMana'):
     pick='ok';snap(m,'payment-'+str(stage))
    elif kind in ['SelectCards','dialog','InputSelectEntitiesFromList']:
     target=spec['actions'][stage][2];matching=[o for o in opts if o.get('card',{}).get('id')==refs.get(target) or o['label'].startswith('Equip') or o['label'].startswith('Bonesplitter')]
     if case=='copy-aura' and stage==1 and any(o.get('card',{}).get('id')==refs['bear2'] or o['id']=='card:'+str(refs['bear2']) for o in opts):
      matching=[o for o in opts if o.get('card',{}).get('id')==refs['bear2'] or o['id']=='card:'+str(refs['bear2'])]
      if r.get('selectedCount',0)>0:matching=[o for o in opts if o['id']=='ok']
     assert matching,(case,stage,r);pick=matching[0]['id'];snap(m,'choice-'+str(stage))
    elif kind in ['InputConfirmMulligan','InputConfirm','InputPassPriority']:pick='ok'
    else:raise AssertionError(('Unhandled',case,stage,r))
    assert pick in ids,(case,stage,kind,pick,r);responded.add(r['requestId']);print(case,step,stage,kind,pick,flush=True)
    proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');proc.stdin.flush()
   raise AssertionError('No completion')
  except BaseException:finish('FAIL',traceback.format_exc());raise
  finally:
   if proc.poll() is None:proc.kill();proc.wait()
if __name__=='__main__':
 results=[]
 def summary(status,error=None):(OUT/'run-summary.json').write_text(json.dumps(dict(status=status,results=results,error=error),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
 summary('RUNNING')
 try:
  subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*map(str,FIXTURES)],check=True)
  for case in sys.argv[1:] or CASES:results.append(run(case));print(results[-1]['result'],flush=True)
  summary('PASS')
 except BaseException:summary('FAIL',traceback.format_exc());raise
