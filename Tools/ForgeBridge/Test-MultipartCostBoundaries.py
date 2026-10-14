"""Actual multi-part costs: committed earlier resource, cancel later picker, retry."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'.tools/multipart-cost-1007/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURE=ROOT/'Tools/ForgeBridge/tests/CostInteractionFixture.java';SHA=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES={'discard-before-sacrifice':dict(source='Dreamscape Artist',hand=['Grizzly Bears'],field=['Dreamscape Artist']+['Island']*3+['Forest'],enemy=[],grave=[],cancelAt='sacrifice',paidName='Grizzly Bears',paidZone='graveyardCards')}
CASES['life-discard-forced-completion']=dict(source='Bloodsoaked Altar',hand=['Grizzly Bears'],field=['Bloodsoaked Altar','Llanowar Elves'],enemy=[],grave=[],cancelAt='sacrifice',paidName='Grizzly Bears',paidZone='graveyardCards',sacrifice='Llanowar Elves',effect='demon',lifePaid=2,mandatoryCompletion=True)
CASES['spell-discard-before-sacrifice']=dict(source='Ruthless Disposal',sourceZone='hand',hand=['Ruthless Disposal','Grizzly Bears'],field=['Swamp']*5+['Llanowar Elves'],enemy=['Grizzly Bears','Hill Giant'],grave=[],cancelAt='sacrifice',paidName='Grizzly Bears',paidZone='graveyardCards',sacrifice='Llanowar Elves',effect='disposal')
CASES['replacement-discard-before-sacrifice']=dict(CASES['discard-before-sacrifice'],enemy=['Rest in Peace'],paidZone='exileCards')
CASES['exile-before-mana']=dict(source='Allosaurus Rider',sourceZone='hand',hand=['Allosaurus Rider','Grizzly Bears','Llanowar Elves'],field=['Island']*3,enemy=['Sphere of Resistance'],grave=[],chooseCostOrder=True,cancelAt='mana',paidName='Grizzly Bears',paidNames=['Grizzly Bears','Llanowar Elves'],paidZone='exileCards',payText='exile',choice='exile two green',effect='rider')
CASES['sacrifice-before-mana']=dict(source='Fireblast',sourceZone='hand',hand=['Fireblast'],field=['Mountain']*2+['Island'],enemy=['Sphere of Resistance'],grave=[],chooseCostOrder=True,cancelAt='mana',paidName='Mountain',paidNames=['Mountain','Mountain'],payZoneFrom='battlefield',paidZone='graveyardCards',payText='sacrifice',choice='sacrifice two Mountains',effect='fireblast')
CASES['return-before-mana']=dict(source='Gush',sourceZone='hand',hand=['Gush'],field=['Island']*3,enemy=['Sphere of Resistance'],grave=[],chooseCostOrder=True,cancelAt='mana',paidName='Island',paidNames=['Island','Island'],payZoneFrom='battlefield',paidZone='hand',payText='return',choice='return two Islands',effect='gush')
CASES['library-information-barrier']=dict(CASES['discard-before-sacrifice'],field=['Dreamscape Artist']+['Island']*4+['Forest','Treasonous Ogre','Ashiok, Wicked Manipulator'],cancelAt='mana',expectedLibraryBarrier=True,initialLoyalty=5)
CASES['existing-stack-cancel']=dict(CASES['discard-before-sacrifice'],hand=['Shock','Grizzly Bears'],field=['Dreamscape Artist']+['Island']*3+['Forest','Mountain'],existingStack=True)
CASES['existing-delayed-cancel']=dict(CASES['discard-before-sacrifice'],hand=['Twinflame','Grizzly Bears'],field=['Dreamscape Artist','Grizzly Bears']+['Island']*3+['Forest','Mountain','Mountain'],existingStack=True,existingDelayed=True)
CASES['sacrifice-counter-cancel']=dict(CASES['sacrifice-before-mana'],counterCard='Mountain',counterType='CHARGE',counterAmount=3)

def run(case):
 spec=CASES[case];folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir();seat=spec.get('seat',1)
 meta=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driverSha256=SHA(Path(__file__)),fixtureSha256=SHA(FIXTURE),spec=spec)
 (folder/'driver.py').write_bytes(Path(__file__).read_bytes());(folder/FIXTURE.name).write_bytes(FIXTURE.read_bytes())
 def finish(status,why):
  result=dict(meta,status=status,result=status+' '+case+': '+why)
  for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return result
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
  start=Path(temp)/'start.json';config=dict(session=case,spec=spec,seed=4,firstPlayerSeat=seat,startingLife=20,decks=[DECK,DECK],auditDir=str(audit));start.write_text(json.dumps(config),encoding='utf8');(folder/'start.json').write_text(json.dumps(config,indent=2),encoding='utf8')
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.CostInteractionFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');messages=queue.Queue()
  def reader():
   try:
    for line in proc.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,name):
   (folder/(name+'-envelope.json')).write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf8')
   v=dict(m['views'][seat-1],request=m['request'],awaitingInput=True);(folder/(name+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
  source=None;before=None;paid=None;cancelled=False;retry=False;stackSeen=False;searchSelected=0;enemyBefore=None;sphereActivated=False;drawnIds=None;answeredRequests=set();existingStage=0;existingItems=None;existingSource=None;existingToken=None;delayedTriggerIds=set()
  try:
   for step in range(100):
    m=messages.get(timeout=35);assert m is not None,'Native exited';trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush();assert m['type'] not in ['fatal','rejected','ended'],m
    if m['type']!='request':continue
    q=m['request'];v=m['views'][seat-1];own=v['players'][seat-1];other=v['players'][2-seat];kind=q['kind'];opts=q['options'];ids=[o['id'] for o in opts];pick='ok';message=q['message'].lower();snap(m,'step-'+str(step))
    if q['requestId'] in answeredRequests:continue
    for viewer in (1,2):assert not m['views'][viewer-1]['players'][2-viewer]['hand'],'Private hand leaked'
    if spec.get('existingDelayed') and existingStage==3 and v['phase']=='END_OF_TURN' and v['stackItems']:
     assert len(v['stackItems'])==1;entry=v['stackItems'][0]
     assert entry['kind']=='trigger' and entry['source']['id']==existingSource and ('Grizzly Bears ('+str(existingToken)+')') in entry['description'] and entry['description'].startswith('Exile ')
     delayedTriggerIds.add(entry['id'])
    if spec.get('existingDelayed') and existingStage==3 and kind=='InputPassPriority' and q['seat']==seat and v['phase']=='END_OF_TURN' and not v['stackItems']:
     snap(m,'existing-delayed-resolved');assert existingToken not in [c['id'] for c in own['battlefield']]
     assert len(delayedTriggerIds)==1
     # Exiled tokens cease to exist before the next priority (CR704.5d).
     assert all(existingToken not in [c['id'] for c in own[z]] for z in ['hand','graveyardCards','exileCards'])
     assert existingSource in [c['id'] for c in own['graveyardCards']] and paid in [c['id'] for c in own['hand']]
     assert not next(c for c in own['battlefield'] if c['id']==source)['tapped']
     return finish('PASS','Actual Twinflame resolves creating native haste copy and delayed next-end-step exile. During same turn Dreamscape commits discard then cancels later sacrifice: exact resources, existing token and empty stack restored. Actual end-step delayed trigger survives and exiles exact token once, source/hand intact and private priority recovers.')
    elif spec.get('existingStack') and kind=='InputPassPriority' and q['seat']==seat and v['phase']=='MAIN1':
     if existingStage==0:
      assert not v['stackItems'];existingSource=next(c['id'] for c in own['hand'] if c['name']==('Twinflame' if spec.get('existingDelayed') else 'Shock'));enemyBefore=json.loads(json.dumps(other));pick='card:'+str(existingSource);existingStage=1
     elif existingStage==1:
      if spec.get('existingDelayed') and v['stackItems']:pick='ok'
      else:
       if spec.get('existingDelayed'):
        tokens=[c for c in own['battlefield'] if c.get('token') and c['name']=='Grizzly Bears'];assert len(tokens)==1 and tokens[0]['power']==2 and tokens[0]['toughness']==2;existingToken=tokens[0]['id']
       else:assert len(v['stackItems'])==1 and v['stackItems'][0]['source']['id']==existingSource
       before=json.loads(json.dumps(own));existingItems=json.loads(json.dumps(v['stackItems']));source=next(c['id'] for c in own['battlefield'] if c['name']==spec['source']);paid=next(c['id'] for c in own['hand'] if c['name']=='Grizzly Bears');snap(m,'existing-stack-before');pick='card:'+str(source);existingStage=2
     elif existingStage==2:
      assert cancelled,'Outer ability unexpectedly succeeded';snap(m,'existing-stack-cancelled')
      assert v['stackItems']==existingItems,('Existing stack changed',existingItems,v['stackItems'])
      for zone in ['hand','battlefield','graveyardCards','exileCards']:assert [c['id'] for c in own[zone]]==[c['id'] for c in before[zone]],zone
      assert [(c['id'],c['tapped']) for c in own['battlefield']]==[(c['id'],c['tapped']) for c in before['battlefield']]
      assert own['manaPool']==before['manaPool'] and own['life']==before['life'] and own['libraryCount']==before['libraryCount'];existingStage=3
     elif existingStage==3 and not v['stackItems'] and not spec.get('existingDelayed'):
      snap(m,'existing-stack-resolved');assert other['life']==enemyBefore['life']-2
      assert existingSource in [c['id'] for c in own['graveyardCards']] and paid in [c['id'] for c in own['hand']]
      assert not next(c for c in own['battlefield'] if c['id']==source)['tapped']
      return finish('PASS','Actual Shock cast/paid and targeting opponent remains on stack before Dreamscape activation. Committed discard then cancel sacrifice restores exact resources and full original stack projection/IDs/target/text. Existing Shock resolves once for2damage, cancelled ability stays absent and private main priority recovers.')
    elif kind=='InputPassPriority' and q['seat']==seat and v['phase']=='MAIN1' and not v['stackItems']:
     if cancelled:
      snap(m,'cancelled')
      if spec.get('expectedLibraryBarrier'):
       assert own['libraryCount']==before['libraryCount']-3 and own['life']==before['life']
       assert [c['id'] for c in own['hand']]==[c['id'] for c in before['hand']]
       assert drawnIds and drawnIds=={c['id'] for c in own['exileCards']}
       assert any(c['name']=='Treasonous Ogre' for c in own['battlefield'])
       assert next(c for c in own['battlefield'] if c['id']==source)['tapped']==False
       return finish('PASS','Actual Treasonous Ogre life-cost mana activation during Dreamscape payment; Ashiok replaces 3 life with exiling exact three library cards. Outer cancel preserves exile identities/library decrease and unchanged life/hand; source tap refunded and main priority/privacy recovered. No full rollback claim after irreversible library movement.')
      for zone in ['hand','battlefield','graveyardCards','exileCards']:
       assert [c['id'] for c in own[zone]]==[c['id'] for c in before[zone]],('Cancellation failed to restore',zone,[c['name'] for c in own[zone]],[c['name'] for c in before[zone]])
      assert [(c['id'],c['tapped']) for c in own['battlefield']]==[(c['id'],c['tapped']) for c in before['battlefield']]
      if spec.get('counterCard'):
       assert any(c['counters'] for c in before['battlefield'] if c['name']==spec['counterCard'])
       assert [(c['id'],c['counters']) for c in own['battlefield']]==[(c['id'],c['counters']) for c in before['battlefield']],('Refund lost counters',[(c['id'],c['counters']) for c in own['battlefield']],[(c['id'],c['counters']) for c in before['battlefield']])
      assert own['life']==before['life'] and own['manaPool']==before['manaPool'] and own['libraryCount']==before['libraryCount']
      cancelled=False;retry=True;pick='card:'+str(source)
     elif retry:
      assert stackSeen,'Retry returned without putting the ability on stack';snap(m,'resolved')
      assert own['life']==before['life']-spec.get('lifePaid',0)
      if spec.get('payText')!='return':assert paid not in [c['id'] for c in own['hand']]
      effect=spec.get('effect','search')
      if effect=='search':
       assert own['libraryCount']==before['libraryCount']-2
       assert sum(c['name']=='Mountain' for c in own['battlefield'])==2
       assert len(own['battlefield'])==len(before['battlefield'])+1
       assert sum(c['tapped'] for c in own['battlefield'])==4
      elif effect=='demon':
       assert any(c['name']=='Demon Token' and c['power']==5 and c['toughness']==5 for c in own['battlefield'])
       assert not any(c['name']=='Llanowar Elves' for c in own['battlefield'])
      elif effect=='disposal':
       assert not any(c['creature'] for c in other['battlefield'])
       assert {c['id'] for c in other['graveyardCards']}=={c['id'] for c in enemyBefore['battlefield']}
       assert source in [c['id'] for c in own['graveyardCards']]
      elif effect=='rider':
       assert any(c['id']==source and c['power']==4 and c['toughness']==4 for c in own['battlefield'])
      elif effect=='fireblast':
       assert other['life']==enemyBefore['life']-4 and source in [c['id'] for c in own['graveyardCards']]
      elif effect=='gush':
       assert own['libraryCount']==before['libraryCount']-2 and len(own['hand'])==len(before['hand'])+3
      assert paid in [c['id'] for c in own[spec['paidZone']]]
      return finish('PASS',('Native life payment keeps later choices mandatory; discard/sacrifice committed once and actual effect resolved.' if spec.get('mandatoryCompletion') else 'Earlier nonmana resource actually moved before later cancellation; exact original hand/board/grave/exile order and tapped/mana/life/library restored. Same ability retried, costs committed exactly once and actual effect resolved.')+' Private views and main priority recovered.')
     else:
      assert before is None,'Unexpected unsuccessful activation';before=json.loads(json.dumps(own));enemyBefore=json.loads(json.dumps(other));source=next(c['id'] for c in own[spec.get('sourceZone','battlefield')] if c['name']==spec['source']);paid=next(c['id'] for c in own[spec.get('payZoneFrom','hand')] if c['name']==spec['paidName']);pick='card:'+str(source);snap(m,'before')
      if spec.get('mandatoryCompletion'):retry=True
    elif kind=='InputPassPriority':
     if retry and v['stackItems']:stackSeen=True;snap(m,'retry-stack')
    elif 'PayMana' in kind:
     if spec.get('expectedLibraryBarrier'):
      if not sphereActivated:
       sphere=next(c['id'] for c in own['battlefield'] if c['name']=='Treasonous Ogre')
       pick='card:'+str(sphere);assert pick in ids,('Life mana unavailable',ids);sphereActivated=True
      elif own['libraryCount']<before['libraryCount']:
       drawnIds={c['id'] for c in own['exileCards']};assert len(drawnIds)==3;snap(m,'library-changed-before-cancel');pick='cancel';cancelled=True
      else:continue # A nested replacement dialog will supersede this transient outer payment view.
     elif spec['cancelAt']=='mana' and not retry:
      expected=spec['paidNames'];actual=[c['name'] for c in own[spec['paidZone']]]
      assert all(actual.count(n)>=expected.count(n) for n in expected),(expected,actual)
      snap(m,'committed-before-cancel');assert 'cancel' in ids;pick='cancel';cancelled=True
     else:pick='ok'
    elif kind=='dialog':
     if q['message']=='getAbilityToPlay':pick=next(o['id'] for o in opts if spec.get('choice','').lower() in o['label'].lower() and o['label'].startswith(spec['source']+' -'))
     elif spec.get('expectedLibraryBarrier'):pick=next(o['id'] for o in opts if o['label']=='Blue')
     else:raise AssertionError(('Unexpected dialog',q))
    elif kind.startswith('InputSelectCards') or kind=='InputSelectEntitiesFromList':
     if spec.get('expectedLibraryBarrier') and own['libraryCount']<before['libraryCount']:
      drawnIds={c['id'] for c in own['exileCards']};assert len(drawnIds)==3;snap(m,'library-changed-before-cancel');pick='cancel';cancelled=True
     elif spec['cancelAt'] in message.splitlines()[-1] and not retry:
      assert paid in [c['id'] for c in own[spec['paidZone']]],('Earlier resource not yet paid',kind,message)
      snap(m,'committed-before-cancel');assert 'cancel' in ids;pick='cancel';cancelled=True
     elif 'discard' in message.splitlines()[-1]:
      pick='ok' if q.get('selectedCount') else next(o['id'] for o in opts if o.get('card',{}).get('id')==paid)
     elif retry and 'sacrifice' in message.splitlines()[-1] and not spec.get('payText'):
      if spec.get('mandatoryCompletion'):
       assert 'cancel' not in ids and own['life']==before['life']-spec['lifePaid']
       assert paid in [c['id'] for c in own['graveyardCards']];snap(m,'mandatory-after-life')
      pick='ok' if q.get('selectedCount') else next(o['id'] for o in opts if o.get('card',{}).get('name')==spec.get('sacrifice','Forest'))
     elif retry and 'library' in message:
      pick='ok' if q.get('selectedCount',0)==2 else next(o['id'] for o in opts if o.get('card') and not o.get('selected'))
     elif spec.get('payText') in message.splitlines()[-1] if spec.get('payText') else False:
      pick='ok' if q.get('selectedCount',0)==len(spec['paidNames']) else next(o['id'] for o in opts if o.get('card',{}).get('name') in spec['paidNames'] and not o.get('selected'))
     else:raise AssertionError(('Unknown picker',q))
    elif kind=='InputSelectTargets':
     if spec.get('existingDelayed') and existingStage==1:
      pick='ok' if q.get('selectedCount') else next(o['id'] for o in opts if o.get('card',{}).get('name')=='Grizzly Bears' and not o.get('selected'))
     else:pick='ok' if 'ok' in ids else next(o['id'] for o in opts if (o.get('card',{}).get('ownerSeat')==3-seat or o['id']=='player:'+str(other['targetId'])) and not o.get('selected'))
    elif kind=='OrderCards':
     candidates=[o for o in opts if o['id'].startswith('select:') and not o.get('selected')]
     pick='confirm-cards' if not candidates else sorted(candidates,key=lambda o:o['label'].startswith('{'))[0]['id']
    elif kind in ['InputConfirmMulligan','InputConfirm','InputAttack','InputBlock']:pick='ok'
    elif kind=='InputMulligan':pick='keep'
    else:raise AssertionError(('Unhandled',q))
    assert pick in ids,(kind,pick,q);print(case,step,kind,pick,flush=True);response=dict(type='respond',session=case,seat=q['seat'],requestId=q['requestId'],option=pick);trace.write(json.dumps(dict(response=response))+'\n');trace.flush();answeredRequests.add(q['requestId']);proc.stdin.write(json.dumps(response)+'\n');proc.stdin.flush()
   raise AssertionError('Step limit')
  except Exception as error:finish('FAIL',repr(error));raise
  finally:
   if proc.poll() is None:proc.kill();proc.wait()

if __name__=='__main__':
 selected=sys.argv[1:] or list(CASES);assert all(x in CASES for x in selected);results=[];summary=OUT/'run-summary.json';summary.write_text(json.dumps(dict(status='RUNNING',selected=selected,results=[])),encoding='utf8')
 try:
  subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(FIXTURE)],check=True)
  for case in selected:results.append(run(case));print(results[-1]['result'],flush=True)
 except BaseException:summary.write_text(json.dumps(dict(status='FAIL',selected=selected,results=results)),encoding='utf8');raise
 summary.write_text(json.dumps(dict(status='PASS',selected=selected,results=results),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
