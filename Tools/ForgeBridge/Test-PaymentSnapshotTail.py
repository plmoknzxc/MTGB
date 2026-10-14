"""Bounded real cancellation flows. No state writes after opening fixture setup."""
import hashlib,json,os,queue,shutil,subprocess,sys,tempfile,threading,time,traceback,zipfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/'.tools/characteristics-keywords-payments-1011/payments';OUT.mkdir(parents=True,exist_ok=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES={
 'pending-history-identity':dict(Hand=['Shock','Grizzly Bears'],Battlefield=['Dreamscape Artist','Mountain','Snow-Covered Island','Hydraulic Helper',"Mishra's Workshop",'Island','Forest']),
 'static-local':dict(Hand=['Grizzly Bears'],Battlefield=['Dreamscape Artist','Snow-Covered Island','Island','Forest','Overabundance',"Winter's Night"]),
 'static-fallback':dict(Hand=['Think Twice'],Battlefield=['Snow-Covered Island','Overabundance',"Winter's Night"]),
 'queued-fallback':dict(Hand=['Think Twice'],Battlefield=['Island','Island'])}
CASES['static-local-retry']=dict(CASES['static-local'])
FIXTURES=['RoyalStateFixture.java','DrainManaDeltaFixture.java','PaymentSnapshotTailFixture.java']
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def ref(p):return dict(path=p.relative_to(ROOT).as_posix(),sha256=sha(p))
def run(case):
 folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir();classes=folder/'classes';classes.mkdir()
 shutil.copyfile(__file__,folder/'driver.py')
 for n in FIXTURES:shutil.copyfile(ROOT/'Tools/ForgeBridge/tests'/n,folder/n)
 write(folder/'cases.json',CASES)
 shutil.copyfile(RT/'runtime.json',folder/'runtime.json')
 nativebase=[]
 for name in ['forge/game/cost/CostPayment.class','forge/game/GameSnapshot.class','forge/game/zone/MagicStack.class','forge/game/GameActionUtil.class','forge/game/mana/Mana.class','forge/game/mana/ManaRefundService.class','forge/game/player/Player.class','forge/game/trigger/TriggerHandler.class','forge/game/spellability/AbilityManaPart.class','mtgb/bridge/ForgeBridge.class']:
  dst=folder/'native-classbase'/name;dst.parent.mkdir(parents=True,exist_ok=True);live=RT/'classes'/name
  if live.exists():dst.write_bytes(live.read_bytes());origin=live.relative_to(ROOT).as_posix()
  else:
   matches=[]
   for jar in (RT/'lib').glob('*.jar'):
    with zipfile.ZipFile(jar) as z:
     if name in z.namelist():matches.append((jar,z.read(name)))
   assert len(matches)==1,(name,[str(x[0]) for x in matches]);dst.write_bytes(matches[0][1]);origin=matches[0][0].relative_to(ROOT).as_posix()+'!/'+name
  nativebase.append(dict(origin=origin,frozen=ref(dst)))
 write(folder/'native-classbase.json',nativebase)
 override=Path(os.environ['MTGB_PAYMENT_TAIL_OVERRIDE_DIR']).resolve() if os.environ.get('MTGB_PAYMENT_TAIL_OVERRIDE_DIR') else None
 tested=[]
 if override:
  assert override.is_relative_to(OUT.resolve()) and override.is_dir(),override
  for x in sorted(override.rglob('*.class')):
   dst=folder/'tested-overrides'/x.relative_to(override);dst.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(x,dst);tested.append(dict(classPath=x.relative_to(override).as_posix(),frozen=ref(dst)))
  for path in ['forge/game/cost/CostPayment.java','forge/game/card/Card.java']:
   src=RT/'source/native-patches'/path;dst=folder/'tested-source'/path;dst.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(src,dst)
  write(folder/'tested-overrides.json',tested)
 result=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],sources=[ref(folder/'driver.py'),*[ref(folder/n) for n in FIXTURES],ref(folder/'runtime.json'),ref(folder/'cases.json'),ref(folder/'native-classbase.json')],status='RUNNING')
 result['testedNativeOverrides']=tested;result['testedOverrideSources']=[ref(x) for x in sorted((folder/'tested-source').rglob('*.java'))] if override else []
 write(folder/'result.json',result);p=None;checks=0;violations=[];labels=[];paid=[];responded=set()
 def check(ok,why):
  nonlocal checks
  checks+=1
  if not ok:violations.append(why)
 def finish(status,why):
  result.update(status=status,result=why,checks=checks,violations=violations,labels=labels,paid=paid)
  result['frozenEvidence']=[ref(x) for x in sorted(folder.rglob('*')) if x.is_file() and x.name not in ('result.json','evidence.json')]
  write(folder/'result.json',result);write(OUT/(case+'-latest.json'),dict(case=case,ref=ref(folder/'result.json'),status=status));return result
 try:
  subprocess.run(['C:/Program Files/Java/jdk-17/bin/javac.exe','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(folder/n) for n in FIXTURES]],check=True)
  with tempfile.TemporaryDirectory(prefix='mtgb-payment-tail-') as temp,(folder/'stderr.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
   config=dict(session=case,seed=4,firstPlayerSeat=1,startingLife=30,decks=[DECK,DECK],auditDir=str(audit),own=CASES[case],other=dict(Hand=['Negate'],Battlefield=['Vorinclex, Voice of Hunger'] if case=='queued-fallback' else []))
   write(folder/'start.json',config)
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx640m','-XX:ActiveProcessorCount=3','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,*([override] if override else []),RT/'classes',RT/'lib/*'])),'mtgb.bridge.PaymentSnapshotTailFixture',str(RT/CFG['assets']),str(folder/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');messages=queue.Queue()
   def reader():
    try:
     for line in p.stdout:messages.put(json.loads(line))
    except Exception as e:messages.put(e)
    finally:messages.put(None)
   threading.Thread(target=reader,daemon=True).start()
   def observed(r):
    end=time.monotonic()+5
    while time.monotonic()<end:
     try:return json.loads((audit/(str(r['requestId'])+'.json')).read_text('utf8'))
     except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
    raise AssertionError('Missing exact observer '+str(r['requestId']))
   def snap(m,a,label):
    write(folder/(label+'-envelope.json'),m);write(folder/(label+'-native.json'),a)
    for seat,v in enumerate(m['views'],1):write(folder/(label+'-seat'+str(seat)+'.json'),dict(v,request=m['request'],awaitingInput=True))
    labels.append(dict(label=label,requestId=m['request']['requestId'],kind=m['request']['kind'],seat=m['request']['seat']))
   def card(v,name,zone='battlefield'):return next(c for c in v['players'][0][zone] if c['name']==name)
   stage='initial';floats=['Snow-Covered Island','Hydraulic Helper',"Mishra's Workshop"] if case=='pending-history-identity' else [];fi=0;manual=0;poolPaid=0;before=None;shock=None;source=None;cancelled=False;outer=False;stackSeen=False;deadline=time.monotonic()+230
   for step in range(650):
    assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=40)
    if isinstance(m,Exception):raise m
    assert m is not None,'native exited';trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush();assert m['type'] not in ('fatal','ended','rejected'),m
    if m['type']!='request':continue
    r=m['request'];rid=r['requestId'];v=m['views'][0];one,two=v['players'];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts];pick='ok';message=r['message'].lower()
    if rid in responded:continue
    a=observed(r) if v['phase'] not in ('null',None) else None
    if a is not None:check(a['requestId']==rid,'exact request observer');check(not m['views'][0]['players'][1]['hand'] and not m['views'][1]['players'][0]['hand'],'recipient hand privacy')
    if kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1':
     if stage=='initial':
      snap(m,a,'opening')
      if case=='pending-history-identity':shock=card(v,'Shock','hand')['id'];pick='card:'+str(shock);stage='shock-casting'
      elif case.startswith('static-local'):before=a;source=card(v,'Dreamscape Artist')['id'];snap(m,a,'before');pick='card:'+str(source);outer=True;stage='outer-paying'
      else:before=a;snap(m,a,'before');pick='card:'+str(card(v,'Think Twice','hand')['id']);outer=True;stage='outer-paying'
     elif stage=='shock-casting':
      assert len(a['stack'])==1 and a['stack'][0]['source']==shock,('Shock not pending',a['stack']);snap(m,a,'shock-stack');paid.append(dict(source=shock,name='Shock',mana=1,requestId=rid));stage='floating';pick='card:'+str(card(v,floats[fi])['id']);fi+=1
     elif stage=='floating':
      if fi<len(floats):pick='card:'+str(card(v,floats[fi])['id']);fi+=1
      else:before=a;source=card(v,'Dreamscape Artist')['id'];snap(m,a,'before');pick='card:'+str(source);outer=True;stage='outer-paying';manual=0;poolPaid=0
     elif stage=='cancel-submitted':
      snap(m,a,'after-cancel');check(not a['costs'],'cost stack cleared');check(not a['frozen'],'stack unfrozen');check(not a['frozenStack'] and not a['waiting'],'cancelled attempt queue empty');cancelled=True
      if case=='pending-history-identity':
       check(a['stack']==before['stack'],'exact original pending stack survives')
       check([(p['spellsCastThisTurn'],p['spellsCastThisGame']) for p in a['players']]==[(p['spellsCastThisTurn'],p['spellsCastThisGame']) for p in before['players']],'turn/game history preserved')
       if 'achievementSpellsCast' in a['players'][0]:check([(p['achievementSpellsCast'],p['achievementMaxStormCount']) for p in a['players']]==[(p['achievementSpellsCast'],p['achievementMaxStormCount']) for p in before['players']],'both player achievement counts restored exactly')
       check(sum(e['event']=='cast' and e.get('source')==shock and e.get('spell') for e in a['events'])==1,'pending Shock cast event exactly once');stage='await-shock'
      elif case.startswith('static-local'):
       check(a['players'][0]['life']==before['players'][0]['life'],'local cancelled manual damage restored')
       check(a['players'][0]['mana']==before['players'][0]['mana'],'local cancelled manual mana restored')
       check([(c['id'],c['tapped'],c['hidden']) for c in a['players'][0]['zones']['Battlefield']]==[(c['id'],c['tapped'],c['hidden']) for c in before['players'][0]['zones']['Battlefield']],'local mana action hidden/tap effects restore atomically')
       if case=='static-local-retry':stage='retry-paying';manual=0;poolPaid=0;pick='card:'+str(source)
       else:return finish('PASS_BOUNDED' if not violations else 'FAIL_BOUNDED_REPRO','Real snapshot gate, manual snow mana with Overabundance/Winter hidden writer, paid mana+discard+tap, later sacrifice cancel, original priority recovered.')
      else:
       stage='await-next-turn'
     elif stage=='await-shock' and not a['stack']:
      snap(m,a,'original-shock-resolved');check(two['life']==28,'original Shock resolves for exactly2damage');check(shock in [c['id'] for c in one['graveyardCards']],'original Shock exact ID in graveyard');check(a['players'][0]['spellsCastThisGame']==1,'game history still one after original Shock resolves')
      return finish('PASS_BOUNDED' if not violations else 'FAIL_BOUNDED_REPRO','Actual paid pending Shock, real manual prefloat snow/restricted identities, ordinary multi-cost gate paid mana and discard, later sacrifice cancel, original Shock result and priority.')
     elif stage=='await-next-turn' and a['turn']>=3:
      snap(m,a,'next-own-main');return finish('PASS_BOUNDED' if not violations else 'FAIL_BOUNDED_REPRO','Actual manual mana in single-mana-cost fallback, partial mana paid then outer cancellation. Retained resources/trigger/next own untap captured without synthetic queue changes.')
     elif stage=='retry-paying' and a['stack']:
      snap(m,a,'retry-stack');check(len(a['stack'])==1 and a['stack'][0]['source']==source and len(a['stack'][0]['payingMana'])==3,'actual retry activation pays3mana and enters stack');paid.append(dict(source=source,name='Dreamscape Artist',mana=3,requestId=rid));stage='retry-resolving'
     elif stage=='retry-resolving' and not a['stack']:
      snap(m,a,'retry-resolved');c=next(c for c in a['players'][0]['zones']['Battlefield'] if c['name']=='Snow-Covered Island');check(c['tapped'] and len(c['hidden'])==1,'new retry marker occurs exactly once');check(a['players'][0]['life']==29,'retry immediate damage occurs once');check(len(a['players'][0]['zones']['Library'])==len(before['players'][0]['zones']['Library'])-2,'actual retry searches two original library cards');stage='retry-next-untap'
     elif stage=='retry-next-untap' and a['turn']>=3:
      snap(m,a,'retry-first-own-untap');c=next(c for c in a['players'][0]['zones']['Battlefield'] if c['name']=='Snow-Covered Island');check(c['tapped'] and not c['hidden'],'one real next own untap skipped and marker consumed');stage='retry-following-untap'
     elif stage=='retry-following-untap' and a['turn']>=5:
      snap(m,a,'retry-second-own-untap');c=next(c for c in a['players'][0]['zones']['Battlefield'] if c['name']=='Snow-Covered Island');check(not c['tapped'] and not c['hidden'],'following own untap succeeds without duplicate skip');return finish('PASS_BOUNDED' if not violations else 'FAIL_BOUNDED_REPRO','Extra bounded paid retry: restored cancelled writer absent, real retry pays3 and searches2 original basic lands, one new marker skips one own untap and following own untap succeeds.')
    elif 'PayMana' in kind:
     if not outer:
      if 'card:'+str(card(v,'Mountain')['id']) in ids and not card(v,'Mountain')['tapped']:pick='card:'+str(card(v,'Mountain')['id'])
      else:pick=next((x for x in ids if x.startswith('mana:R')),'ok')
     elif case in ('static-fallback','queued-fallback'):
      if manual==0:
       snap(m,a,'gate-before-manual');pick='card:'+str(card(v,'Snow-Covered Island' if case=='static-fallback' else 'Island')['id']);manual=1
      elif poolPaid==0:
       snap(m,a,'manual-action');assert any(x.startswith('mana:U') for x in ids),r;pick=next(x for x in ids if x.startswith('mana:U'));poolPaid=1
      else:
       snap(m,a,'partial-costs');assert 'cancel' in ids,r;pick='cancel';stage='cancel-submitted'
     elif case.startswith('static-local'):
      if manual==0:snap(m,a,'gate-before-manual');pick='card:'+str(card(v,'Snow-Covered Island')['id']);manual=1
      else:
       if poolPaid==0:snap(m,a,'manual-action')
       options=[o['id'] for o in opts if o['id'].startswith(('mana:U','mana:S:U'))];assert options,('Missing native U option',r);pick=options[0];poolPaid+=1
     else:
      if manual==0:snap(m,a,'gate-before-manual');manual=1
      mana=[o for o in opts if o['id'].startswith(('mana:U','mana:S:U'))]
      if mana:pick=mana[0]['id'];poolPaid+=1
      else:pick='card:'+str(card(v,'Island')['id'])
    elif kind=='OrderCards':
     available=[o for o in opts if o['id'].startswith('select:') and not o.get('selected')]
     def rank(o):
      s=o['label'].lower();return 0 if s.startswith('{') else 1 if 'discard' in s else 2 if s in ('tap','{t}') or 'tap' in s else 3
     pick=min(available,key=rank)['id'] if available else 'confirm-cards'
    elif kind=='dialog':
     if r['message']=='getAbilityToPlay':
      options=[o for o in opts if ('mana' in o['label'].lower() or 'add ' in o['label'].lower())] if stage=='floating' else [o for o in opts if ('discard' in o['label'].lower() or 'search your library' in o['label'].lower())]
      if not options:options=[o for o in opts if o['id'] not in ('cancel','ok')]
      assert options,r;pick=options[0]['id']
     else:raise AssertionError(('Unknown dialog',r))
    elif kind.startswith('InputSelectCards') or kind=='InputSelectEntitiesFromList':
     cost_message=message.splitlines()[-1]
     if 'sacrifice' in cost_message:
      if stage=='retry-paying':pick='ok' if r.get('selectedCount') else next(o['id'] for o in opts if o.get('card',{}).get('name')=='Forest')
      else:snap(m,a,'partial-costs');assert any(c['name']=='Grizzly Bears' for c in a['players'][0]['zones']['Graveyard']),'Discard not committed';check(any(c['snapshotPresent'] for c in a['costs']),'ordinary local cancellation snapshot actually active');assert 'cancel' in ids,r;pick='cancel';stage='cancel-submitted'
     elif 'discard' in cost_message:pick='ok' if r.get('selectedCount') else next(o['id'] for o in opts if o.get('card',{}).get('name')=='Grizzly Bears')
     elif stage=='retry-resolving' and 'library' in message:pick='ok' if r.get('selectedCount',0)==2 else next(o['id'] for o in opts if o.get('card') and not o.get('selected'))
     else:raise AssertionError(('Unknown picker',r))
    elif kind=='InputSelectTargets':pick='ok' if 'ok' in ids else next(o['id'] for o in opts if o['id']=='player:'+str(two['targetId']))
    elif kind in ('InputConfirmMulligan','InputConfirm','InputAttack','InputBlock','InputPassPriority'):pick='ok'
    elif kind=='InputMulligan':pick='keep'
    else:raise AssertionError(('Unknown request',r))
    assert pick in ids,(stage,kind,pick,r);print(case,step,rid,kind,pick,flush=True);response=dict(type='respond',session=case,seat=r['seat'],requestId=rid,option=pick);trace.write(json.dumps(dict(response=response))+'\n');trace.flush();responded.add(rid);p.stdin.write(json.dumps(response)+'\n');p.stdin.flush()
   raise AssertionError('Step limit')
 except Exception as error:
  finish('FAIL',repr(error));raise
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
if __name__=='__main__':
 selected=sys.argv[1:] or list(CASES);assert all(x in CASES for x in selected)
 for case in selected:
  result=run(case);print(result['result'],flush=True)
  if result['status']!='PASS_BOUNDED' or result.get('violations') or result.get('failures'):raise SystemExit(1)
  print('PASS_BOUNDED '+case,flush=True)
