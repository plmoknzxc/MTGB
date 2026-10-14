"""R31 native attack requirements/taxes and two distinct extra-combat consumers.

Arrange only starting positions. Every declaration, payment, optional exert and
phase insertion goes through Forge. Freeze producers separately for each attempt.
"""
import hashlib,json,os,queue,shutil,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime'
CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
OUT=ROOT/'.tools/combat-phase-boundaries-1008/native'
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['relentless-main2','celebrant-exert','celebrant-decline','attack-tax-pay','attack-tax-auto','attack-tax-cancel','attack-tax-partial-cancel','block-tax-pay','block-tax-cancel','must-attack-wall']
BEAR='Grizzly Bears';CELEBRANT='Combat Celebrant'

def write(path,value):Path(path).write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def run(case):
 assert case in CASES,case
 folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True)
 fixtures=['StateBranchFixture.java','CombatKeywordFixture.java']
 for name in fixtures:shutil.copyfile(ROOT/'Tools/ForgeBridge/tests'/name,folder/name)
 shutil.copyfile(__file__,folder/'driver.py')
 identity=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],revision=CFG['revision'],driverSha256=sha(folder/'driver.py'),fixtures={n:sha(folder/n) for n in fixtures},status='RUNNING')
 write(folder/'result.json',identity)
 celebrant=case.startswith('celebrant');tax=case.startswith('attack-tax');blocktax=case.startswith('block-tax');relentless=case=='relentless-main2';forced=case=='must-attack-wall'
 declined=case.endswith('cancel')
 own={'Hand':['Relentless Assault'] if relentless else ['Negate'],'Battlefield':([CELEBRANT,BEAR] if celebrant else ['Juggernaut'] if forced else ['Hill Giant'] if blocktax else [BEAR])+(['Mountain']*(4 if relentless else 2) if relentless or tax else [])}
 other={'Hand':['Negate'],'Battlefield':['Ghostly Prison'] if tax else ['Hipparion','Forest'] if blocktax else ['Wall of Wood','Wall of Air','Grizzly Bears'] if forced else []}
 p=None;checks=0
 def check(ok,why):
  nonlocal checks
  assert ok,why;checks+=1
 try:
  classes=folder/'classes';classes.mkdir()
  subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(folder/n) for n in fixtures]],check=True)
  with tempfile.TemporaryDirectory(prefix='mtgb-combat-phase-') as temp,(folder/'stderr.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
   audit=folder/'audit';audit.mkdir();write(folder/'start.json',dict(session=case,auditDir=str(audit),seed=4,startingLife=20,firstPlayerSeat=1,decks=[DECK,DECK],own=own,other=other))
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.CombatKeywordFixture',str(RT/CFG['assets']),str(folder/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
   messages=queue.Queue()
   def reader():
    try:
     for line in p.stdout:messages.put(json.loads(line))
    except Exception as e:messages.put(e)
    finally:messages.put(None)
   threading.Thread(target=reader,daemon=True).start()
   def observed(r):
    path=audit/(str(r['requestId'])+'.json');end=time.monotonic()+5
    while time.monotonic()<end:
     try:value=json.loads(path.read_text());break
     except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
    else:raise AssertionError('Missing complete observer '+str(r['requestId']))
    check(value['requestId']==r['requestId'],'exact observer version');return value
   def snapshot(m,label):
    r=m['request']
    for seat,v in enumerate(m['views'],1):write(folder/(label+('-other' if seat==2 else '')+'.json'),dict(v,request=r,awaitingInput=True))
   seen=set();initial=None;before=None;hands=None;creatures=[];spell=None;cast=False;paid=0;tax_decision=None;exert_decision=None;phases=[];rounds=[];declared={};attempted_empty=False;forced_retry=False;cancel_recovered=False;blocked=False;deadline=time.monotonic()+180
   for step in range(400):
    check(time.monotonic()<deadline,'Deadline');m=messages.get(timeout=35)
    if isinstance(m,Exception):raise m
    check(m is not None,'Bridge exited');trace.write(json.dumps(m)+'\n');trace.flush();check(m['type'] not in ('fatal','ended','rejected'),m)
    if m['type']!='request':continue
    r=m['request']
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);v=m['views'][0];one,two=v['players'];kind=r['kind'];ids=[o['id'] for o in r['options']];pick='ok'
    check(not m['views'][0]['players'][1]['hand'] and not m['views'][1]['players'][0]['hand'],'opponent private hand concealed')
    if not phases or phases[-1]!=v['phase']:phases.append(v['phase'])
    if initial is None and kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1':
     initial=v['players'];before=observed(r);hands=[{c['id'] for c in m['views'][s]['players'][s]['hand']} for s in range(2)];creatures=[c['id'] for c in one['battlefield'] if c['creature']];snapshot(m,'before')
    if initial is not None:check(v['turn']==1 and v['turnSeat']==1,'same player and turn throughout inserted combats')
    if kind=='InputPassPriority':
     if initial is not None and relentless and rounds and v['phase']=='MAIN2' and not cast and not v['stackItems'] and r['seat']==1:
      check(two['life']==18 and all(c['tapped'] for c in one['battlefield'] if c['id'] in creatures),'first combat complete before actual Assault cast');spell=next(c['id'] for c in one['hand'] if c['name']=='Relentless Assault');pick='card:'+str(spell);cast=True;snapshot(m,'cast-main2')
     elif initial is not None and rounds and v['phase']=='MAIN2' and not v['stackItems'] and (not relentless or len(rounds)==2):
      after=observed(r);snapshot(m,'resolved')
      expected_life=16 if relentless else 12 if case=='celebrant-exert' else 14 if celebrant else 18 if tax and not declined else 20 if tax or case=='block-tax-pay' else 17 if blocktax else 15
      check((one['life'],two['life'])==(20,expected_life),('exact life',one['life'],two['life'],expected_life))
      check(len(rounds)==(2 if relentless or case=='celebrant-exert' else 1),('exact combat declarations',rounds))
      check(phases.count('MAIN2')==(2 if relentless else 1),('main phases',phases))
      check([x['library'] for x in before['players']]==[x['library'] for x in after['players']],'exact library order unchanged')
      for s,pv in enumerate(v['players']):
       deaths={c['id'] for c in initial[1]['battlefield'] if c['name']=='Hipparion'} if s==1 and case=='block-tax-pay' else set()
       check({c['id'] for c in pv['battlefield']}=={c['id'] for c in initial[s]['battlefield']}-deaths,'exact battlefield IDs')
       check({c['id'] for c in pv['graveyardCards']}==deaths|({spell} if s==0 and relentless else set()),'exact graveyard identities')
       check({c['id'] for c in m['views'][s]['players'][s]['hand']}==hands[s]-({spell} if s==0 and relentless else set()),'exact private hand identity')
       check(not pv['exileCards'] and sum(pv['manaPool'])==0,'empty exile and mana')
      check(paid==(4 if relentless else 2 if tax and not declined else 1 if case in ('block-tax-pay','attack-tax-partial-cancel') else 0),('exact payment actions/resources',paid))
      check(sum(c['tapped'] for c in (two if blocktax else one)['battlefield'] if c['land'])==(0 if declined else paid),'exact mana sources including cancellation refund')
      check(all(c['tapped']==(not (tax and declined)) for c in one['battlefield'] if c['id'] in creatures),'exact attacker tap state')
      if celebrant:
       check(exert_decision==(case=='celebrant-exert'),'optional exert decision observed')
       c=next(c for c in one['battlefield'] if c['name']==CELEBRANT);check(c['exertedThisTurn']==(1 if exert_decision else 0) and c['exertedBySeats']==([1] if exert_decision else []),'exact exert projection/lifecycle')
      if tax or blocktax:check(tax_decision==(not declined),'combat tax explicit pay/decline')
      if tax and declined:
       check(not any(c['attacking'] for c in one['battlefield'] if c['id'] in creatures),'declined attack absent at restored MAIN2 priority')
      if blocktax:check(next(c for c in after['cards'] if c['id']==creatures[0])['damage']==(1 if not declined else 0),'exact blocker damage')
      if forced:check(attempted_empty and forced_retry,'must-attack invalid declaration reopens native input')
      identity.update(status='PASS',result='PASS '+case,checks=checks,requests=len(seen),phases=phases,attackRounds=rounds,assertions=['real attack/block declarations; extra phases or tax/requirement branch as named','same turn/player, exact combat and MAIN2 counts, native payment and optional decision','exact lives, attacker tap/exert state, full libraries/private hands/zone IDs, empty mana and MAIN2 restoration']);break
    elif kind=='InputAttack':
     check(initial is not None and r['seat']==1,'native attack operator');segment=len(phases)
     if segment not in declared:
      declared[segment]=set();rounds.append({'segment':segment,'requestId':r['requestId'],'contextId':r['contextId']});snapshot(m,'attack-'+str(len(rounds)))
      if len(rounds)==2:
       check(all(not c['tapped'] for c in one['battlefield'] if c['name']==BEAR),'other attacker really untapped for second combat')
       if celebrant:check(all(o.get('attackerId')!=next(c['id'] for c in one['battlefield'] if c['name']==CELEBRANT) for o in r['options'] if o['id'].startswith('attack:')),'exerted Celebrant cannot attack again')
     if forced and not attempted_empty:attempted_empty=True;pick='ok';snapshot(m,'invalid-empty')
     elif tax and declined and tax_decision is False:
      cancel_recovered=True;check(not any(c['attacking'] for c in one['battlefield'] if c['creature']) and not any(c['tapped'] for c in one['battlefield'] if c['name']=='Mountain'),'declined tax clears exact attacker and preserves lands');pick='ok';snapshot(m,'cancel-recovered')
     else:
      if forced and attempted_empty and not declared[segment]:forced_retry=True
      candidate=next((o for o in r['options'] if o['id'].startswith('attack:') and o['attackerId'] in creatures and o['attackerId'] not in declared[segment] and o['defenderKind']=='player' and o['defenderId']==two['targetId']),None)
      if candidate:pick=candidate['id'];declared[segment].add(candidate['attackerId'])
    elif kind=='InputBlock':
     check(r['seat']==2,'native block operator')
     if blocktax and not blocked:
      horse=next(c['id'] for c in two['battlefield'] if c['name']=='Hipparion');candidate=next(o for o in r['options'] if o['id'].startswith('block:') and o['attackerId']==creatures[0] and o['defenderId']==horse);pick=candidate['id'];blocked=True;snapshot(m,'block-tax-declare')
     if forced:
      walls={c['id'] for c in two['battlefield'] if 'Wall' in c['name']};bear=next(c['id'] for c in two['battlefield'] if c['name']==BEAR)
      offered={o['defenderId'] for o in r['options'] if o['id'].startswith('block:')};check(bear in offered and offered.isdisjoint(walls),'Walls illegal while ordinary creature can block');snapshot(m,'block-restriction')
    elif kind=='OrderCards' and r.get('selectionPurpose')=='exertAttack':
     check(celebrant and r['minCount']==0 and r['maxCount']==1,'optional single Celebrant exert');snapshot(m,'exert-'+str(r['selectedCount']));exert_decision=case=='celebrant-exert'
     if exert_decision and r['selectedCount']==0:pick=next(o['id'] for o in r['options'] if o.get('card',{}).get('name')==CELEBRANT)
     else:pick='confirm-cards'
    elif kind=='InputConfirm':
     check(tax,'only attack tax confirmation expected');tax_decision=case=='attack-tax-pay';pick='ok' if tax_decision else 'cancel';snapshot(m,'tax-decision')
    elif kind.startswith('InputPayMana'):
     check(relentless or tax or blocktax,'only native mana payment expected');check(r['seat']==(2 if blocktax else 1),'exact payer');snapshot(m,'payment-'+str(paid))
     if (tax or blocktax) and declined and (case!='attack-tax-partial-cancel' or paid==1):pick='cancel';tax_decision=False
     elif case=='attack-tax-auto':check('ok' in ids,'native auto payment available');pick='ok';paid=2;tax_decision=True
     elif 'mana:R' in ids:pick='mana:R'
     elif 'mana:G' in ids:pick='mana:G'
     else:
      payer=two if blocktax else one;pick='card:'+str(next(c['id'] for c in payer['battlefield'] if c['land'] and 'card:'+str(c['id']) in ids));paid+=1
      if tax or blocktax:tax_decision=True
    elif kind=='RevealCards':pick='confirm-cards'
    elif kind=='OrderAbilities':pick='confirm-order'
    elif kind=='InputChooseStartingHand' or 'Mulligan' in kind:pass
    else:raise AssertionError(('Unexpected',kind,ids,r['message']))
    check(pick in ids,(kind,pick,ids));p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   else:raise AssertionError('Step limit')
   check(identity['status']=='PASS','No final priority reached')
 except Exception as e:
  identity.update(status='FAIL',result='FAIL '+case+': '+repr(e));(folder/'failure.txt').write_text(traceback.format_exc(),encoding='utf8')
 finally:
  if p is not None and p.poll() is None:
   try:p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush();p.wait(timeout=3)
   except (OSError,subprocess.TimeoutExpired):p.kill();p.wait()
 write(folder/'result.json',identity);write(OUT/(case+'-result.json'),identity);print(identity.get('result',identity['status']),flush=True);return identity

if __name__=='__main__':
 OUT.mkdir(parents=True,exist_ok=True);cases=sys.argv[1:] or CASES;results=[]
 write(OUT/'run-summary.json',dict(status='RUNNING',cases=cases,startedAt=time.time()))
 try:
  for c in cases:results.append(run(c));write(OUT/'run-summary.json',dict(status='RUNNING',cases=cases,results=results))
  write(OUT/'run-summary.json',dict(status='PASS' if all(r['status']=='PASS' for r in results) else 'FAIL',cases=cases,results=results,finishedAt=time.time()))
 except Exception as e:
  write(OUT/'run-summary.json',dict(status='FAIL',cases=cases,results=results,error=repr(e)));raise
 sys.exit(any(r['status']!='PASS' for r in results))
