"""R29: actual native band actions, membership, blockers and damage control."""
import hashlib,json,os,queue,shutil,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime'
CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'.tools/attack-bands-1008/native'
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
HERO='Benalish Hero';BEAR='Grizzly Bears';ELF='Llanowar Elves'
CASES=['join','recall','limit','bandswith','ordinary-repeat','join-seat2']
def write(p,v):Path(p).write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def run(case):
 folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True)
 fixtures=['StateBranchFixture.java','CombatKeywordFixture.java']
 for name in fixtures:shutil.copyfile(ROOT/'Tools/ForgeBridge/tests'/name,folder/name)
 shutil.copyfile(__file__,folder/'driver.py')
 identity=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],revision=CFG['revision'],driverSha256=sha(folder/'driver.py'),fixtures={n:sha(folder/n) for n in fixtures},assertions=[])
 classes=folder/'classes';classes.mkdir();subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(folder/n) for n in fixtures]],check=True)
 seat=2 if case=='join-seat2' else 1
 actors=[BEAR] if case=='ordinary-repeat' else ['Adventurers\' Guildhouse','Isao, Enlightened Bushi','Silvos, Rogue Elemental',BEAR] if case=='bandswith' else [HERO,BEAR]+([ELF] if case=='limit' else [])
 defenders=['Wall of Air'] if case=='ordinary-repeat' else ['Colossal Dreadmaw']
 # Expected native state after each wire action. No fixture creates groups.
 if case in ('join','join-seat2'):
  plan=[('attack',HERO,''),('attack',BEAR,''),('band',HERO,'active-one'),('band',BEAR,'group')]
 elif case=='recall':
  plan=[('attack',HERO,''),('band',HERO,'active-one'),('attack',BEAR,'group'),('band',HERO,'inactive'),('band',BEAR,'active-group'),('remove',HERO,'recalled'),('attack',HERO,'separate'),('band',HERO,'active-one'),('band',BEAR,'group')]
 elif case=='limit':
  plan=[('attack',HERO,''),('band',HERO,'active-one'),('attack',BEAR,'group'),('attack',ELF,'invalid'),('attack',ELF,'separate-third')]
 elif case=='bandswith':
  plan=[('attack','Isao, Enlightened Bushi',''),('band','Isao, Enlightened Bushi','active-one'),('attack','Silvos, Rogue Elemental','group'),('attack',BEAR,'invalid'),('attack',BEAR,'separate-third')]
 else:plan=[('attack',BEAR,''),('attack',BEAR,'repeat')]
 p=None;checks=0
 def check(ok,why):
  nonlocal checks
  assert ok,why;checks+=1
 try:
  with tempfile.TemporaryDirectory(prefix='mtgb-attack-bands-') as temp,(folder/'stderr.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
   audit=folder/'audit';audit.mkdir();own={'Hand':['Negate'],'Battlefield':actors};other={'Hand':['Negate'],'Battlefield':defenders}
   start=dict(session=case,auditDir=str(audit),seed=4,startingLife=20,firstPlayerSeat=seat,decks=[DECK,DECK],own=own if seat==1 else other,other=other if seat==1 else own);write(folder/'start.json',start)
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
    check(data['requestId']==r['requestId'],'observer version');return data
   def snapshot(m,label):
    r=m['request'];v=dict(m['views'][r['seat']-1]);v.update(request=r,awaitingInput=True);write(folder/(label+'.json'),v)
   initial=None;before=None;hands=None;seen=set();pos=0;previous=None;blocked=False;contexts=set();deadline=time.monotonic()+180
   for step in range(300):
    assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=35)
    if isinstance(m,Exception):raise m
    assert m is not None,'Bridge exited';trace.write(json.dumps(m)+'\n');trace.flush();check(m['type'] not in ('fatal','ended','rejected'),m)
    if m['type']!='request':continue
    r=m['request']
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);v=m['views'][seat-1];one=v['players'][seat-1];two=v['players'][2-seat];kind=r['kind'];ids=[o['id'] for o in r['options']];pick='ok'
    check(not m['views'][0]['players'][1]['hand'] and not m['views'][1]['players'][0]['hand'],'private hands concealed')
    if kind=='InputPassPriority':
     if initial is None and r['seat']==seat and v['phase']=='MAIN1':
      initial=v['players'];before=proof(r);hands=[{c['id'] for c in m['views'][s]['players'][s]['hand']} for s in range(2)];snapshot(m,'before')
     if initial is not None and pos==len(plan) and v['phase']=='MAIN2' and not v['stackItems']:
      after=proof(r);snapshot(m,'resolved');check([x['library'] for x in before['players']]==[x['library'] for x in after['players']],'exact library order')
      for s,pv in enumerate(v['players']):
       check({c['id'] for c in m['views'][s]['players'][s]['hand']}==hands[s],'exact private hands');check(not pv['exileCards'] and sum(pv['manaPool'])==0,'exile/mana unchanged')
      if case=='bandswith':check(two['life']==8 and one['life']==20,'legendary band plus separate Bear deal twelve unblocked damage');check(not contexts and not blocked,'no damage allocation without blockers')
      elif case=='ordinary-repeat':check(two['life']==20 and one['life']==20,'ordinary repeat stays assigned');check(one['battlefield'][0]['damage']==1 and two['battlefield'][0]['damage']==2,'ordinary blocked damage preserved')
      else:
       check(two['life']==(19 if case=='limit' else 20) and one['life']==20,'band blocks together; only independent Elf hits player')
       check({c['name'] for c in one['graveyardCards']}=={HERO},'attacking decider assigns all six to Hero; Bear survives')
       check(next(c for c in one['battlefield'] if c['name']==BEAR)['damage']==0,'Bear takes zero damage')
       check(two['battlefield'][0]['damage']==3 and len(contexts)==1,'Dreadmaw takes three and one defender damage context')
      identity.update(status='PASS',result='PASS '+case,checks=checks,requests=len(seen),assertions=['actual native declarations, explicit band select/join/deselect/recall and legality results','exact native membership and both public projections','same-band blocking and attacking-player allocation, or independent legendary/ordinary branch as named','exact private hands/library order and MAIN2 priority restoration']);break
    elif kind=='InputAttack':
     check(initial is not None and r['seat']==seat,'attack operator');native=proof(r);snapshot(m,'attack-'+str(pos))
     members={b['attacker']:set(b['members']) for b in native['bands']};cards={c['name']:c for c in one['battlefield']};active={o['attackerId'] for o in r['options'] if o['id'].startswith('attack-band:') and o.get('selected')}
     for view in m['views']:
      for c in view['players'][seat-1]['battlefield']:check(set(c.get('attackingBandIds',[]))==members.get(c['id'],set()),'both receivers exact public native band')
     if previous:
      action,name,expected=previous;target=cards[name]['id'];pair={cards[n]['id'] for n in ([HERO,BEAR] if case!='bandswith' else ['Isao, Enlightened Bushi','Silvos, Rogue Elemental'])} if case!='ordinary-repeat' else {target}
      if expected=='active-one':check(active=={target},'native band selected')
      if expected in ('group','active-group'):check(all(members.get(i)==pair for i in pair) and active==pair,'joined/selected exact native group')
      if expected=='inactive':check(not active and all(members.get(i)==pair for i in pair),'deselect preserves group')
      if expected=='recalled':check(target not in members and not active and members[cards[BEAR]['id']]=={cards[BEAR]['id']},'recall removes exact member and clears selection')
      if expected=='separate':check(not active and all(members.get(i)=={i} for i in pair),'redeclaration after recall remains independent')
      if expected=='invalid':check(target not in members and not active and all(members.get(i)==pair for i in pair),'illegal second nonbander/nonlegend cannot join; input reopens')
      if expected=='separate-third':check(members.get(target)=={target} and all(members.get(i)==pair for i in pair),'third creature can attack independently')
      if expected=='repeat':check(members.get(target)=={target} and not any(o['id'].startswith('attack-band:') for o in r['options']),'ordinary repeated assignment stays idempotent')
     if pos<len(plan):
      previous=plan[pos];action,name,_=previous;target=cards[name]['id'];prefix={'attack':'attack','band':'attack-band','remove':'attack-remove'}[action];pick=prefix+':'+str(target)+((':player:'+str(two['targetId'])) if action=='attack' else '');pos+=1
     else:previous=None
    elif kind=='InputBlock':
     snapshot(m,'block-'+str(step));native=proof(r)
     if case!='bandswith' and not blocked:
      attacker=next(c for c in one['battlefield'] if c['name']==(BEAR if case=='ordinary-repeat' else HERO));blocker=two['battlefield'][0]
      pick=next(o['id'] for o in r['options'] if o['id'].startswith('block:') and o.get('attackerId')==attacker['id'] and o.get('defenderId')==blocker['id']);blocked=True
     elif blocked:
      block=two['battlefield'][0];expected={c['id'] for c in one['battlefield'] if c['name'] in ({BEAR} if case=='ordinary-repeat' else {HERO,BEAR})};check(set(block['blockedAttackerIds'])==expected,'one blocker blocks all native band members')
    elif kind=='AssignAmount':
     check(r['seat']==seat and r['selectionPurpose']=='combatDamage','attacking player controls blocker damage allocation');check(r['sourceCard']['name']=='Colossal Dreadmaw' and r['maxCount']==6,'exact six blocker damage')
     contexts.add(r['contextId']);rows=r['allocations'];check({a.get('card',{}).get('name') for a in rows}=={HERO,BEAR},'allocation contains exact group and no player/independent creature');hero=next(a for a in rows if a['card']['name']==HERO)
     snapshot(m,'allocation-'+str(step));pick='amount-add:'+str(hero['id']) if hero['amount']<6 else 'confirm-amount'
    elif kind=='RevealCards':pick='confirm-cards'
    elif kind=='InputChooseStartingHand' or 'Mulligan' in kind:pass
    else:raise AssertionError(('Unexpected',kind,ids,r['message']))
    check(pick in ids,(kind,pick,ids));p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   else:raise AssertionError('No final priority')
   check('status' in identity,'No result')
 except Exception as e:
  identity.update(status='FAIL',result='FAIL '+case+': '+repr(e));(folder/'failure.txt').write_text(traceback.format_exc(),encoding='utf8')
 finally:
  if p is not None and p.poll() is None:
   try:p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush();p.wait(timeout=3)
   except (OSError,subprocess.TimeoutExpired):p.kill();p.wait()
 write(folder/'result.json',identity);write(OUT/(case+'-result.json'),identity);print(identity['result'],flush=True);return identity
if __name__=='__main__':
 OUT.mkdir(parents=True,exist_ok=True);results=[run(c) for c in sys.argv[1:] or CASES];write(OUT/'run-summary.json',dict(status='PASS' if all(r['status']=='PASS' for r in results) else 'PARTIAL',results=results));sys.exit(any(r['status']!='PASS' for r in results))
