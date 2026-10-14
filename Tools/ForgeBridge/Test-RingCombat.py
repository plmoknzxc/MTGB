"""Actual monarch and initiative acquisition, upkeep/draw, source death and combat transfer."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';cfg=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/ring-combat-1006/native';OUT.mkdir(parents=True,exist_ok=True);classes=OUT/'classes';classes.mkdir(exist_ok=True)
fixture=ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java';subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),str(fixture)],check=True)
CASES=['ring-level1-block-restriction','ring-level2-loot','ring-level3-sacrifice','ring-level4-life-loss','ring-bearer-control','ring-bearer-leaves'];deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]};sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def run(case):
 change=case=='ring-bearer-change';level=int(case[10]) if case.startswith('ring-level') else 2 if change else 1;control=case.endswith('control');leave=case.endswith('leaves');combat=not(control or leave);own={'Hand':['Birthday Escape']*level+(['Donate'] if control else ['Murder'] if leave else []),'Battlefield':['Grizzly Bears','Elvish Mystic'],'Library':['Island']*10};other={'Battlefield':['Wall of Roots'] if level==3 else ['Hill Giant','Grizzly Bears']};folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);meta={'case':case,'folder':folder.relative_to(ROOT).as_posix(),'runtimeHash':cfg['runtimeHash'],'driverSha256':sha(Path(__file__)),'fixtures':[{'path':fixture.relative_to(ROOT).as_posix(),'sha256':sha(fixture)}]}
 def finish(status,message):
  d={**meta,'status':status,'result':status+' '+case+': '+message}
  for file in [folder/'result.json',OUT/(case+'-result.json')]:file.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return d
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'trace.jsonl').open('w',encoding='utf8') as trace,(folder/'engine.log').open('w',encoding='utf8') as log:
  audit=Path(temp)/'audit';audit.mkdir();start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,actor=1,own=own,other=other,auditDir=str(audit),seed=4,firstPlayerSeat=1,startingLife=30,decks=[deck,deck])),encoding='utf8');p=subprocess.Popen([str(RT/cfg['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.RoyalStateFixture',str(RT/cfg['assets']),str(start)],cwd=RT/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');messages=queue.Queue()
  def reader():
   try:
    for line in p.stdout:
     with (folder/'stdout.log').open('a',encoding='utf8') as raw:raw.write(line)
     messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,name):
   for seat,v in enumerate(m['views'],1):
    d=dict(v);d.update(request=m['request'],awaitingInput=True);(folder/(name+('' if seat==1 else '-other')+'.json')).write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf8')
  cast=0;original_bearer=None;bearer=None;blocker=None;ready=False;attacked=False;blocked=False;secondary=False;looted=False;discarded=None;beforehand=None;beforelibrary=None;deadline=time.monotonic()+220
  try:
   for step in range(400):
    assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=40);assert m is not None,'Native exit';trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
    if m['type']!='request':continue
    r=m['request'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];v=m['views'][0];pl,op=v['players'];stack=v['stackItems'];pick=None;snap(m,'step-'+str(step))
    if bearer is None and any(c['name']=='Grizzly Bears' for c in pl['battlefield']):bearer=next(c['id'] for c in pl['battlefield'] if c['name']=='Grizzly Bears');original_bearer=bearer;blocker=next(c['id'] for c in op['battlefield'] if c['creature'])
    for pv in v['players']:
     for c in pv['battlefield']:assert c.get('ringBearer',False)==(c['id']==pv['ringBearerId']),('Ring card identity mismatch',pv['seat'],c)
    for item in stack:
     c=item['source']
     if c['name']=='The Ring':assert c.get('helperArtName')=='The Ring' and c.get('effectMarker') and 'currentRules' in c,('Ring helper source fields missing',item)
    if kind=='InputPassPriority':
     if cast<level and not stack and r['seat']==1 and v['phase']=='MAIN1':
      if cast:assert pl['ringTemptedCount']==cast and pl['ringBearerId']==bearer
      pick='card:'+str(next(c['id'] for c in pl['hand'] if c['name']=='Birthday Escape'));cast+=1
     elif cast==level and pl['ringLevel']==level and not stack and not ready:
      assert pl['ringTemptedCount']==level and pl['ringBearerId']==bearer and 'Legendary' in next(c['currentType'] for c in pl['battlefield'] if c['id']==bearer);assert len([c for c in pl['effectMarkers'] if c['name']=='The Ring'])==1;ready=True;beforehand=pl['handCount'];beforelibrary=pl['libraryCount'];snap(m,'ready')
     if ready and change and not stack:
      assert bearer!=original_bearer and pl['ringBearerId']==bearer and next(c for c in pl['battlefield'] if c['id']==bearer)['ringBearer'] and not next(c for c in pl['battlefield'] if c['id']==original_bearer)['ringBearer'] and 'Legendary' not in next(c['currentType'] for c in pl['battlefield'] if c['id']==original_bearer)
      snap(m,'bearer-changed');return finish('PASS','actual two Birthday casts advance Ring1to2, exact first Bear designation changes to exact Mystic, former bearer loses ring flag and granted Legendary, Ring helper same identity, both native perspectives and next priority')
     if ready and not stack and (control or leave) and not secondary and r['seat']==1 and v['phase']=='MAIN1':pick='card:'+str(next(c['id'] for c in pl['hand'] if c['name']==('Donate' if control else 'Murder')));secondary=True
     elif ready and secondary and not stack and pl['ringBearerId']==0:
      assert pl['ringLevel']==1 and pl['ringTemptedCount']==1 and any(c['name']=='The Ring' for c in pl['effectMarkers'])
      if control:assert any(c['id']==bearer and 'Legendary' not in c['currentType'] for c in op['battlefield']) and op['ringBearerId']==0 and not any(c['id']==bearer for c in pl['battlefield'])
      else:assert any(c['id']==bearer for c in pl['graveyardCards']) and not any(c['id']==bearer for c in pl['battlefield'])
      snap(m,'bearer-cleared');return finish('PASS','actual Birthday Escape establishes exact legendary Bear as Ring-bearer; '+('Donate changes controller' if control else 'Murder kills bearer')+', native bearer clears to0 while original level1 Ring persists, original object identity and both projections, next priority')
     elif ready and attacked and not stack and v['phase']=='MAIN2':
      if level>=2:assert looted and discarded is not None and pl['handCount']==beforehand and pl['libraryCount']==beforelibrary-1 and any(c['id']==discarded for c in pl['graveyardCards'])
      else:assert not looted and pl['handCount']==beforehand and pl['libraryCount']==beforelibrary
      if level==3:assert blocked and any(c['id']==blocker for c in op['graveyardCards']) and pl['life']==op['life']==30
      else:assert not blocked and op['life']==(25 if level==4 else 28) and pl['life']==30 and any(c['id']==blocker for c in op['battlefield'])
      snap(m,'combat-resolved');return finish('PASS','actual '+str(level)+' Birthday casts establish Ring level'+str(level)+' and exact legendary Bear; '+('greater-power Hill Giant has no legal block, unblocked2 damage' if level==1 else 'attack draws exactly1 and discards exact native card; '+('surviving0/5 Wall blocks and is sacrificed by delayed trigger at endcombat, life30/30' if level==3 else 'unblocked2 damage'+(' plus Ring loss3, opponent25' if level==4 else ', opponent28')))+', both projections and next main2 priority')
     if pick is None:pick='ok'
    elif 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif 'PayMana' in kind:pick='ok' if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled') else next(o['id'] for o in opts if o['id'].startswith('card:'))
    elif kind=='InputAttack':
     if ready and combat and r['seat']==1 and not attacked:pick=next(o['id'] for o in opts if o['id'].startswith('attack:') and o['attackerId']==bearer and o['defenderKind']=='player');attacked=True;snap(m,'attack-choice')
     else:pick='ok'
    elif kind=='InputBlock':
     if ready and r['seat']==2 and level==3 and not blocked:pick=next(o['id'] for o in opts if o['id'].startswith('block:') and o['defenderId']==blocker and o['attackerId']==bearer);blocked=True;snap(m,'block-choice')
     else:
      if ready and r['seat']==2 and level!=3:assert not any(o['id'].startswith('block:') and o.get('defenderId')==blocker for o in opts),'Larger-power blocker legal';assert any(o['id'].startswith('block:') and o.get('defenderId')!=blocker for o in opts),'Equal-power blocker missing';snap(m,'block-forbidden')
      pick='ok'
    elif 'SelectTargets' in kind:
     if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled'):pick='ok'
     else:pick=next(o['id'] for o in opts if o.get('card',{}).get('id')==bearer or control and o['id']=='player:'+str(op['targetId']))
    elif 'SelectCards' in kind or 'SelectEntities' in kind:
     if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled'):pick='ok'
     elif change and pl['ringLevel']==2 and any(o.get('card',{}).get('name')=='Elvish Mystic' for o in opts):
      bearer=next(o['card']['id'] for o in opts if o.get('card',{}).get('name')=='Elvish Mystic');pick=next(o['id'] for o in opts if o.get('card',{}).get('id')==bearer);snap(m,'bearer-choice')
     elif any(o.get('card',{}).get('id')==bearer for o in opts):pick=next(o['id'] for o in opts if o.get('card',{}).get('id')==bearer);snap(m,'bearer-choice')
     else:
      selected=next(o for o in opts if o.get('card') and not o.get('selected') and not o.get('disabled'));pick=selected['id'];discarded=selected['card']['id'];looted=True;snap(m,'loot-choice')
    elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in ids else next(o['id'] for o in opts if o['id'].startswith('order:'))
    elif kind in ['dialog','Option']:pick=next(i for i in ids if i not in ['cancel','no'])
    elif kind=='RevealCards':pick='confirm-cards'
    elif 'ok' in ids:pick='ok'
    else:raise AssertionError(('Unhandled',kind,r['message'],ids))
    assert pick in ids,(kind,pick,ids);print(case,step,kind,r['seat'],pick,'casts',cast,'Ring',pl.get('ringLevel'),'phase',v['phase'],flush=True);p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   raise AssertionError('Step limit')
  except Exception:finish('FAIL',traceback.format_exc());raise
  finally:
   if p.poll() is None:p.kill();p.wait()
if __name__=='__main__':
 results=[]
 for case in sys.argv[1:] or CASES:
  try:d=run(case);print(d['result'],flush=True);results.append(d)
  except Exception as e:print('FAIL '+case+': '+repr(e),flush=True);results.append(json.loads((OUT/(case+'-result.json')).read_text()))
 (OUT/'results.json').write_text(json.dumps({'status':'PASS' if all(d['status']=='PASS' for d in results) else 'FAIL','results':results},ensure_ascii=False,indent=2)+'\n',encoding='utf8');sys.exit(any(d['status']!='PASS' for d in results))
