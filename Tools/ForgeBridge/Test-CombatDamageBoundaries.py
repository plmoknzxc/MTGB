"""R30: native prevention/replacement and shared combat assignment state.

Only initial positions are arranged. Native cast/payment, declarations, optional
choices and AssignAmount responses produce all final results. Each attempt freezes
its producer; callers can rerun one failed case without replacing earlier proof.
"""
import hashlib,json,os,queue,shutil,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime'
CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'.tools/combat-damage-boundaries-1008/native'
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
DREAD='Colossal Dreadmaw';GIANT='Two-Headed Giant of Foriys';BAT='Deadly Recluse';ORGG='Butcher Orgg'
CASES=['fog','furnace','skip-shared','shared-deathtouch','divide']

def write(p,v):Path(p).write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()

def run(case):
 assert case in CASES,case
 folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True)
 fixtures=['StateBranchFixture.java','CombatKeywordFixture.java']
 for name in fixtures:shutil.copyfile(ROOT/'Tools/ForgeBridge/tests'/name,folder/name)
 shutil.copyfile(__file__,folder/'driver.py')
 identity=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],revision=CFG['revision'],driverSha256=sha(folder/'driver.py'),fixtures={n:sha(folder/n) for n in fixtures},status='RUNNING',assertions=[])
 write(folder/'result.json',identity)
 attackers=[DREAD,DREAD] if case=='skip-shared' else [BAT,DREAD] if case=='shared-deathtouch' else [ORGG] if case=='divide' else [DREAD]
 blockers=['Grizzly Bears','Wall of Air'] if case=='divide' else [GIANT]
 own={'Hand':['Fog'] if case=='fog' else ['Negate'],'Battlefield':attackers+(['Forest'] if case=='fog' else [])+(['Furnace of Rath'] if case=='furnace' else [])}
 other={'Hand':['Negate'],'Battlefield':blockers}
 p=None;checks=0
 def check(ok,why):
  nonlocal checks
  assert ok,why;checks+=1
 try:
  classes=folder/'classes';classes.mkdir()
  subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(folder/n) for n in fixtures]],check=True)
  with tempfile.TemporaryDirectory(prefix='mtgb-combat-boundaries-') as temp,(folder/'stderr.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
   audit=folder/'audit';audit.mkdir();write(folder/'start.json',dict(session=case,auditDir=str(audit),seed=4,startingLife=20,firstPlayerSeat=1,decks=[DECK,DECK],own=own,other=other))
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.CombatKeywordFixture',str(RT/CFG['assets']),str(folder/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
   messages=queue.Queue()
   def reader():
    try:
     for line in p.stdout:messages.put(json.loads(line))
    except Exception as e:messages.put(e)
    finally:messages.put(None)
   threading.Thread(target=reader,daemon=True).start()
   def proof(r):
    path=audit/(str(r['requestId'])+'.json');end=time.monotonic()+5
    while time.monotonic()<end:
     try:data=json.loads(path.read_text());break
     except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
    else:raise AssertionError('Missing complete observer '+str(r['requestId']))
    check(data['requestId']==r['requestId'],'observer exact version');return data
   def snapshot(m,label):
    r=m['request'];v=dict(m['views'][r['seat']-1]);v.update(request=r,awaitingInput=True);write(folder/(label+'.json'),v)
   before=None;initial=None;hands=None;source_ids=[];defender_ids=[];seen=set();attacked=set();pairs=set();contexts={};completed=[];skipped=None;optional=0;cast=False;paid=0;spell_id=None;deadline=time.monotonic()+180
   for step in range(400):
    check(time.monotonic()<deadline,'Deadline');m=messages.get(timeout=35)
    if isinstance(m,Exception):raise m
    check(m is not None,'Bridge exited');trace.write(json.dumps(m)+'\n');trace.flush();check(m['type'] not in ('fatal','ended','rejected'),m)
    if m['type']!='request':continue
    r=m['request']
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);v=m['views'][0];one,two=v['players'];kind=r['kind'];ids=[o['id'] for o in r['options']];pick='ok'
    check(not m['views'][0]['players'][1]['hand'] and not m['views'][1]['players'][0]['hand'],'private hand concealed')
    if kind=='InputPassPriority':
     if initial is None and r['seat']==1 and v['phase']=='MAIN1':
      initial=v['players'];before=proof(r);hands=[{c['id'] for c in m['views'][s]['players'][s]['hand']} for s in range(2)];source_ids=[c['id'] for c in one['battlefield'] if c['creature']];defender_ids=[c['id'] for c in two['battlefield'] if c['creature']];snapshot(m,'before')
     if initial is not None and case=='fog' and not cast and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
      spell_id=next(c['id'] for c in one['hand'] if c['name']=='Fog');pick='card:'+str(spell_id);cast=True
     if initial is not None and attacked and v['phase']=='MAIN2' and not v['stackItems']:
      after=proof(r);snapshot(m,'resolved');check([x['library'] for x in before['players']]==[x['library'] for x in after['players']],'exact library order unchanged')
      dead_own={source_ids[0]} if case in ('furnace','shared-deathtouch') else set()
      dead_other=set() if case=='fog' else {defender_ids[0]} if case in ('furnace','skip-shared','shared-deathtouch') else {next(c['id'] for c in initial[1]['battlefield'] if c['name']=='Grizzly Bears')}
      expected_life={'fog':(20,20),'furnace':(20,16),'skip-shared':(20,12),'shared-deathtouch':(20,14),'divide':(20,16)}[case]
      check((one['life'],two['life'])==expected_life,('exact life',one['life'],two['life'],expected_life))
      for s,pv in enumerate(v['players']):
       deaths=dead_own if s==0 else dead_other
       check({c['id'] for c in pv['battlefield']}=={c['id'] for c in initial[s]['battlefield']}-deaths,'exact battlefield IDs')
       check({c['id'] for c in pv['graveyardCards']}==deaths|({spell_id} if s==0 and case=='fog' else set()),'exact graveyard IDs')
       check({c['id'] for c in m['views'][s]['players'][s]['hand']}==hands[s]-({spell_id} if s==0 and case=='fog' else set()),'exact private hand IDs')
       check(not pv['exileCards'] and sum(pv['manaPool'])==0,'exile and mana unchanged')
      if case=='divide':expected_damage={source_ids[0]:2,next(c['id'] for c in initial[1]['battlefield'] if c['name']=='Wall of Air'):0}
      elif case=='fog':expected_damage={source_ids[0]:0,defender_ids[0]:0}
      elif case=='skip-shared':expected_damage={source_ids[0]:4,source_ids[1]:0}
      elif case=='shared-deathtouch':expected_damage={source_ids[1]:0}
      else:expected_damage={}
      for card_id,damage in expected_damage.items():check(next(c for c in after['cards'] if c['id']==card_id)['damage']==damage,('marked damage',card_id,damage))
      check(paid==(1 if case=='fog' else 0),'exact native payment count')
      if case=='fog':check(next(c for c in one['battlefield'] if c['name']=='Forest')['tapped'],'Fog spent native green resource')
      check(len(contexts)==({'skip-shared':4,'shared-deathtouch':2}.get(case,1)),('exact allocation contexts',list(contexts)))
      check(optional==(1 if case=='divide' else 0),'exact optional choice count')
      if case=='skip-shared':check(skipped==source_ids[0] and completed[:2]==[source_ids[1],source_ids[0]],'skip reorders and resumes exact source')
      identity.update(status='PASS',result='PASS '+case,checks=checks,requests=len(seen),contexts=list(contexts.values()),assertions=['native cast/payment where required; exact attack/block declarations','prevention/replacement applied after assignment, or shared assigned lethal/deathtouch and skip source lifecycle, or unrestricted divide choice as named','exact final lives, marked damage, zone IDs, private hands/full library order, empty mana and MAIN2 priority restoration']);break
    elif kind=='InputAttack':
     check(initial is not None and r['seat']==1,'attack operator')
     candidate=next((o for o in r['options'] if o['id'].startswith('attack:') and o['attackerId'] in source_ids and o['attackerId'] not in attacked and o['defenderKind']=='player' and o['defenderId']==two['targetId']),None)
     if candidate:pick=candidate['id'];attacked.add(candidate['attackerId'])
     else:check(attacked==set(source_ids),'all exact attackers declared')
    elif kind=='InputBlock':
     desired={(a,defender_ids[0]) for a in source_ids}
     candidate=next((o for o in r['options'] if o['id'].startswith('block:') and (o['attackerId'],o['defenderId']) in desired-pairs),None)
     if candidate:pick=candidate['id'];pairs.add((candidate['attackerId'],candidate['defenderId']))
     else:check(pairs==desired,'exact shared blocker declarations')
    elif kind=='InputConfirm':
     check(case=='divide' and r['seat']==1,'only divided-as-choose optional');optional+=1;snapshot(m,'divide-choice');pick='ok'
    elif kind=='AssignAmount':
     check(r['selectionPurpose']=='combatDamage','typed combat allocation');rows=r['allocations'];source=r['sourceCardId'];ctx=r['contextId'];first=ctx not in contexts
     check(source==r['sourceCard']['id'],'exact allocation source');check(sum(a['amount'] for a in rows)==r['selectedCount'] and r['selectedCount']+r['remainingCount']==r['maxCount'],'exact assignment totals');check(('confirm-amount' in ids)==(r['remainingCount']==0),'confirm gate')
     if first:contexts[ctx]=dict(contextId=ctx,source=source,max=r['maxCount'],lethal=[a.get('lethal') for a in rows]);snapshot(m,'context-'+str(len(contexts)))
     if case=='skip-shared' and skipped is None and source==source_ids[0]:
      check('skip-amount' in ids and r['maxCount']==6 and rows[0]['lethal']==4,'skip initial shared assignment');skipped=source;pick='skip-amount'
     else:
      if source in source_ids:
       check(r['seat']==1 and r['maxCount']==6,'attacker assignment amount/operator')
       if case=='divide':
        goal=[2 if a.get('card',{}).get('name')=='Grizzly Bears' else 0 if a.get('card') else 4 for a in rows]
        check({a.get('card',{}).get('id') for a in rows if a.get('card')}==set(defender_ids),'divide includes unblocked creature')
        check(any(not a.get('card') for a in rows),'divide includes defending player')
        if first:check(all('amount-add:'+str(a['id']) in ids for a in rows),'divide bypasses lethal gates')
       else:
        lethal=0 if case=='shared-deathtouch' or case=='skip-shared' and source==skipped else 4
        check(len(rows)==2 and rows[0]['targetId']==defender_ids[0] and rows[0]['lethal']==lethal and rows[0]['hasLethal'],'shared native lethal threshold')
        check(rows[1]['targetKind']=='player' and rows[1]['targetId']==two['targetId'] and rows[1]['playerSeat']==2,'defending player identity')
        check(('amount-add:'+str(rows[1]['id']) in ids)==(r['remainingCount']>0 and rows[0]['amount']>=lethal),'prevention/replacement/shared lethal trample gate')
        goal=[lethal,6-lethal]
        if case=='skip-shared' and source==skipped:check(ctx!=next(iter(contexts)) and 'skip-amount' not in ids,'resumed last attacker fresh context cannot skip')
      else:
       check(case in ('skip-shared','shared-deathtouch') and r['seat']==2 and source==defender_ids[0] and r['maxCount']==4,'shared blocker exact four damage/operator')
       check({a['targetId'] for a in rows}==set(source_ids),'shared blocker exact targets');goal=[4 if a['targetId']==source_ids[0] else 0 for a in rows]
      target=next((i for i,a in enumerate(rows) if a['amount']<goal[i]),None)
      if target is None:pick='confirm-amount';completed.append(source);snapshot(m,'confirmed-'+str(len(completed)))
      else:pick='amount-add:'+str(rows[target]['id'])
    elif kind.startswith('InputPayMana'):
     check(case=='fog' and r['seat']==1,'Fog payment operator')
     if 'mana:G' in ids:pick='mana:G'
     else:pick='card:'+str(next(c['id'] for c in one['battlefield'] if c['name']=='Forest' and 'card:'+str(c['id']) in ids));paid+=1
    elif kind=='RevealCards':pick='confirm-cards'
    elif kind=='InputChooseStartingHand' or 'Mulligan' in kind:pass
    else:raise AssertionError(('Unexpected',kind,ids,r['message']))
    check(pick in ids,(kind,pick,ids));p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   else:raise AssertionError('No final priority')
   check(identity['status']=='PASS','No completed result')
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
