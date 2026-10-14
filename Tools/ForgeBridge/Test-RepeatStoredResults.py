"""Five shared original Repeat/StoreSVar flows; native inputs and read-only observations only."""
from pathlib import Path
from collections import Counter
import argparse,hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback,sys
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';BASE=ROOT/'.tools/repeat-reveal-names-1011/repeats'
CASES={
 'chaos-strategy':dict(inputIds=['stored-minus-times-repeat-results'],positions=[{'Hand':['Game of Chaos','Game of Chaos','Strategy, Schmategy'],'Battlefield':['Mountain']*10+['Sol Ring','Grizzly Bears'],'Library':['Island']*10},{'Hand':['Island'],'Battlefield':['Forest','Grizzly Bears']}]),
 'spark-note-upkeep':dict(inputIds=['stored-minus-times-repeat-results'],positions=[{'Hand':['Spark Fiend'],'Battlefield':['Mountain']*6,'Library':['Island']*10},{'Hand':[],'Battlefield':[]}]),
 'round-decline-reset':dict(inputIds=['repeat-stop-and-reset-consumers'],positions=[{'Hand':['Eureka','Hypergenesis','Regrowth','Jace Beleren','Jace Beleren','Grizzly Bears'],'Battlefield':['Forest']*15,'Library':['Island']*10},{'Hand':['Mountain','Grizzly Bears'],'Battlefield':[],'Library':['Island']*10}]),
 'delina-high-low-reset':dict(inputIds=['repeat-stop-and-reset-consumers'],positions=[{'Hand':[],'Battlefield':['Delina, Wild Mage','Grizzly Bears'],'Library':['Island']*10},{'Hand':[],'Battlefield':[]}]),
 'helm-gates-reset':dict(inputIds=['repeat-stop-and-reset-consumers'],positions=[{'Hand':['Twiddle','Twiddle','Rest in Peace'],'Battlefield':['Island']*17+['Plains']*2+['Helm of Obedience']*2,'Library':['Island']*10},{'ExactLibrary':True,'Hand':['Reclaim'],'Battlefield':['Forest'],'Library':['Island','Forest','Grizzly Bears','Mountain','Plains']}]),
}
CASES['helm-gates-tail']=dict(inputIds=['repeat-stop-and-reset-consumers'],positions=[{'Hand':['Twiddle','Twiddle','Rest in Peace'],'Battlefield':['Island']*17+['Plains']*2+['Helm of Obedience']*2,'Library':['Island']*10},{'ExactLibrary':True,'Hand':['Reclaim'],'Battlefield':['Forest'],'Library':['Island','Grizzly Bears','Mountain','Plains']}])
CASES['helm-replacement-tail']=dict(inputIds=['repeat-stop-and-reset-consumers'],positions=[{'Hand':[],'Battlefield':['Island']*3+['Helm of Obedience','Rest in Peace']},{'ExactLibrary':True,'Hand':[],'Battlefield':[],'Library':['Island']}])
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def ref(p):return dict(path=p.relative_to(ROOT).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def cards(n):return list({c['id']:c for p in n['players']for z in p['zones'].values()for c in z}.values())
def exact(n,id):return next(c for c in cards(n)if c['id']==id)
def named(n,name,seat=1,zone=None):return next(c for c in cards(n)if c['name']==name and c['owner']==seat and(zone is None or c['zone']==zone))
def value(c,key):
 s=c['svars'].get(key,'');return int(s.split('$')[-1])if s and s.split('$')[-1].lstrip('-').isdigit()else None
def validate_terminal(case,state,endpoints,paid):
 """No PASS for an incomplete suffix, operator-corrected failure or missing endpoint."""
 counts={'chaos-strategy':3,'spark-note-upkeep':1,'round-decline-reset':5,'helm-gates-reset':8,'helm-gates-tail':8,'helm-replacement-tail':1,'delina-high-low-reset':0}
 if len(endpoints)!=counts[case]or len(paid)!=counts[case]:return False
 if case=='spark-note-upkeep':return all(x in state['captured']for x in['matched-upkeep-safe-zero','later-upkeep-no-roll'])
 if case=='delina-high-low-reset':return all(x in state['captured']for x in['first-attack-expired','second-attack-expired'])and len(state['tokens'].get('1',[]))==2 and len(state['tokens'].get('3',[]))==1
 if case=='round-decline-reset':return endpoints[0]['source']==endpoints[2]['source']and endpoints[4]['action']=='free-cast'
 if case.startswith('helm-gates'):return endpoints[0]['source']==endpoints[2]['source']and endpoints[3]['source']==endpoints[7]['source']
 return True
class JavaRandom:
 MASK=(1<<48)-1;MULT=0x5DEECE66D;ADD=11
 def __init__(self,seed):self.state=(seed^self.MULT)&self.MASK
 def next(self,n):self.state=(self.state*self.MULT+self.ADD)&self.MASK;return self.state>>(48-n)
 def integer(self,bound):
  if bound&(bound-1)==0:return (bound*self.next(31))>>31
  while True:
   bits=self.next(31);v=bits%bound
   if bits-v+(bound-1)<(1<<31):return v
 def boolean(self):return self.next(1)!=0
def plan_seed(case,opening):
 seed=opening['seed'];wanted=opening['rngState'];r=JavaRandom(seed);count=None
 for i in range(10000):
  if r.state==wanted:count=i;break
  r.next(1)
 if count is None:raise AssertionError('Read-only opening RNG state did not match initial Java Random progression')
 for seed in range(1,100000):
  r=JavaRandom(seed)
  for _ in range(count):r.next(1)
  initial=r.state
  if case=='chaos-strategy':
   flips=[r.boolean()for _ in range(4)];rolls=[r.integer(6)+1 for _ in range(3)];ok=rolls[0]==6 and rolls[1]!=6 and rolls[2]!=6 and 5 not in rolls[1:]
   outcomes=dict(flips=flips,rolls=rolls)
  elif case=='spark-note-upkeep':
   rolls=[r.integer(6)+1 for _ in range(4)];ok=sum(rolls[:2])in[4,5,6,8,9,10]and sum(rolls[:2])==sum(rolls[2:]);outcomes=dict(rolls=rolls)
  elif case=='delina-high-low-reset':
   rolls=[r.integer(20)+1 for _ in range(3)];ok=rolls[0]>=15 and rolls[1]<=14 and rolls[2]>=15;outcomes=dict(rolls=rolls)
  else:ok=True;outcomes={}
  if ok:return dict(seed=seed,startupCalls=count,openingState=initial,outcomes=outcomes,openingProbe=opening)
 raise AssertionError('No bounded initial seed found')
def run(case,probe=False,seed_plan=None,native_override=None):
 if native_override:native_override=native_override.resolve()
 if not probe and seed_plan is None and case in['chaos-strategy','spark-note-upkeep','delina-high-low-reset']:
  seed_plan=json.loads((BASE/(case+'-seed-plan.json')).read_text('utf8'))
 BASE.mkdir(parents=True,exist_ok=True);runtime=json.loads((RT/'runtime.json').read_text('utf-8-sig'));spec=CASES[case];mode='probe'if probe else'native';folder=BASE/mode/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir();classes=folder/'classes';classes.mkdir()
 sources=[Path(__file__),ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java',ROOT/'Tools/ForgeBridge/tests/RepeatStoredResultsFixture.java',RT/'runtime.json'];frozen=[]
 for path in sources:
  target=folder/path.name;target.write_bytes(path.read_bytes());frozen.append(ref(target))
 write(folder/'case-plan.json',spec);checks=[];failures=[];paid=[];endpoints=[];representatives=[];requests=[];proc=None;state={'active':None,'captured':[],'paidStackIds':[],'actions':0,'choiceCounts':{},'attackChosen':{},'flipRounds':{},'tokens':{}};ids={};deadline=time.monotonic()+1200
 def check(ok,label):
  if not ok:raise AssertionError(label)
  checks.append(label)
 def observe(rid):
  until=time.monotonic()+10
  while time.monotonic()<until:
   try:return json.loads((audit/(str(rid)+'.json')).read_text('utf8'))
   except(FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
  raise AssertionError('Read-only native observation missing '+str(rid))
 def freeze(m,n,label):
  write(folder/(label+'-wire.json'),m);write(folder/(label+'-observation.json'),n);representatives.append(dict(label=label,requestId=m['request']['requestId'],kind=m['request']['kind'],wire=ref(folder/(label+'-wire.json')),observation=ref(folder/(label+'-observation.json'))))
 def once(m,n,label):
  if label not in state['captured']:freeze(m,n,label);state['captured'].append(label)
 def pause(error,m,n):
  record=dict(index=len(failures)+1,error=error,requestId=m['request']['requestId'],state=state.copy(),paid=len(paid),endpoints=len(endpoints));failures.append(record);write(folder/('raw-failure-'+str(len(failures))+'.json'),record);freeze(m,n,'pause-'+str(len(failures)));write(folder/'pause.json',record);print('PAUSED',folder,error,flush=True)
  until=time.monotonic()+240
  while proc.poll()is None and time.monotonic()<until:
   p=folder/'control.json'
   if p.exists():
    try:data=json.loads(p.read_text('utf-8-sig'))
    except(PermissionError,json.JSONDecodeError):time.sleep(.1);continue
    p.replace(folder/('control-used-'+str(len(failures))+'.json'));state.update(data.get('state',{}));return data['option']
   time.sleep(.2)
  raise AssertionError('Bounded native pending input correction expired')
 def start_action(m,n,name,kind='cast',seat=1,source=None,**extras):
  c=exact(n,source)if source else named(n,name,seat,'Battlefield'if kind=='activation'else'Hand');a=dict(name=name,source=c['id'],kind=kind,seat=seat,beforeRequest=m['request']['requestId'],beforeLife=[p['life']for p in n['players']],beforeTapped=sum(x['tapped']for x in n['players'][seat-1]['zones']['Battlefield']),**extras);state['active']=a;freeze(m,n,'before-action-'+str(state['actions']));return'card:'+str(c['id'])
 def finish_action(m,n):
  a=state['active'];c=exact(n,a['source']);number=state['actions'];label='endpoint-'+str(number)
  if a['kind']=='suspend':
   check(c['zone']=='Exile'and c['counters'].get('TIME',0)==3,'Actual Hypergenesis suspend produces exact original three TIME counters');spent=sum(x['tapped']for x in n['players'][0]['zones']['Battlefield'])-a['beforeTapped'];check(spent==3 and n['players'][0]['castsGame']==3,'Actual suspend pays three mana and does not increment cast count');paid.append(dict(action='suspend',name=c['name'],source=c['id'],mana=spent,requestId=m['request']['requestId'],kind='static-original-resource-delta'))
  elif a['name']=='Game of Chaos':
   rounds=state['flipRounds'].get(str(a['source']),[]);check(len(rounds)==2,'Exactly two actual Chaos flips then optional stop');check(value(c,'NumLife')==1,'Actual original Chaos host NumLife restored to one after repeat');expected=a['beforeLife'][:]
   for i,x in enumerate(rounds):delta=(1 if x['winner']==1 else-1)*(2**i);expected[0]+=delta;expected[1]-=delta
   check([p['life']for p in n['players']]==expected,'Original first stake1 and second stake2 exact life transfer');check(c['zone']=='Graveyard','Actual paid Chaos resolved to graveyard')
  elif a['name']=='Strategy, Schmategy':
   rolls=n['players'][0]['rolls'];check(rolls==seed_plan['outcomes']['rolls']and len(rolls)==3,'Actual first six grants exactly two additional dice and finite stop');actual=next(x for x in n['paidAbilities']if x['source']==a['source']);check(value(actual,'Left')==0,'Actual original Strategy ability Left Plus2 Minus1 ends at zero');results=rolls[1:]
   if 2 in results:check(exact(n,ids['Sol Ring'])['zone']=='Graveyard','Actual Strategy artifact outcome destroys original Ring')
   if 3 in results:check(all(x['zone']=='Graveyard'for x in cards(n)if x['id']in ids['lands']),'Actual Strategy land outcome destroys every original land')
   if 4 in results:
    times=results.count(4);check([p['life']for p in n['players']]==[x-3*times for x in a['beforeLife']],'Actual Strategy damage outcome damages both players');check(all(exact(n,x)['zone']=='Graveyard'for x in ids['bears']),'Actual Strategy damage outcome kills both original bears')
  elif a['name']=='Spark Fiend':
   total=sum(n['players'][0]['rolls']);check(n['players'][0]['rolls']==seed_plan['outcomes']['rolls'][:2],'Actual Spark ETB two physical dice');check(value(c,'Noted')==total and value(c,'Safe')==1,'Actual Spark stores ETB total and leaves future upkeep enabled');check(c['zone']=='Battlefield','Actual paid Spark survives allowed ETB total')
  elif a['name']in['Eureka','Hypergenesis']:
   check(c['zone']=='Graveyard'and value(c,'NumPlayerGiveup')==0 and not c['remembered'],'Actual source full-round stop resets counter and memory '+a['name']);count=state['choiceCounts'].get(str(c['id'])+'-'+str(number),0);expected=6 if a['name']=='Eureka'and number==0 else 2 if a['name']=='Eureka'else 4;check(count==expected,'Exact actual complete-player rounds before stop '+a['name']);
   if a['name']=='Eureka'and number==0:
    check(exact(n,ids['jace'][0])['zone']=='Battlefield'and exact(n,ids['p2Mountain'])['zone']=='Battlefield'and exact(n,ids['p2Bear'])['zone']=='Battlefield','Eureka allows planeswalker and both-player legal permanent placements across two productive rounds')
   if a['name']=='Hypergenesis':check(exact(n,ids['p1Bear'])['zone']=='Battlefield'and exact(n,ids['jace'][1])['zone']=='Hand','Hypergenesis allowed creature while planeswalker stays in hand')
  elif a['name']=='Regrowth':check(exact(n,ids['Eureka'])['zone']=='Hand','Actual Regrowth returns same original Eureka for reset recast')
  elif a['name']=='Helm of Obedience':
   check(value(c,'AccumulatedMilled')==0 and not c['remembered'],'Actual Helm resets accumulated count and remembered original card after activation '+str(number));p2=n['players'][1]['zones']
   first=1 if case=='helm-gates-tail'else 2
   if case=='helm-replacement-tail':check(not p2['Library']and not p2['Graveyard']and exact(n,ids['p2Library'][0])['zone']=='Exile'and c['zone']=='Battlefield','Actual milled card replaced to exile does not count graveyard and empty library terminates repeat')
   elif number==0:check([x['id']for x in p2['Graveyard']]==ids['p2Library'][:first]and c['zone']=='Battlefield','Actual first X stop at exact noncreatures milled to graveyard, source remains')
   if number==2:check(c['zone']=='Graveyard'and exact(n,ids['p2Library'][first])['zone']=='Battlefield'and exact(n,ids['p2Library'][first])['controller']==1,'Actual next same-host creature stop sacrifices Helm and gains control of original Bear')
   if number==3:check(not p2['Library']and c['zone']=='Battlefield'and {x['id']for x in p2['Graveyard']}==set(ids['p2Library'][:first]+ids['p2Library'][first+1:]),'Actual X3 stops after remaining two lands exhaust library without sacrifice')
   if number==7:check(not p2['Library']and exact(n,ids['p2Library'][0])['zone']=='Exile'and c['zone']=='Battlefield','Actual replacement puts milled original card into exile and empty library ends loop without sacrifice')
  elif a['name']=='Twiddle':check(not exact(n,a['targets'][0][1])['tapped'],'Actual Twiddle untaps original Helm for next activation')
  elif a['name']=='Reclaim':check(n['players'][1]['zones']['Library'][0]['id']==ids['p2Library'][0],'Actual Reclaim restores original graveyard land to library top')
  elif a['name']=='Rest in Peace':check(c['zone']=='Battlefield'and not n['players'][0]['zones']['Graveyard']and not n['players'][1]['zones']['Graveyard'],'Actual paid Rest in Peace establishes exile replacement and exiles previous graveyards')
  once(m,n,label);endpoints.append(dict(action=a['kind'],name=a['name'],source=a['source'],requestId=m['request']['requestId'],turn=n['turn'],phase=n['phase'],label=label));state['actions']+=1;state['active']=None;write(folder/'endpoints.json',endpoints)
 def monitor(m,n):
  r=m['request'];kind=r['kind']
  if not ids:
   for name in ['Game of Chaos','Strategy, Schmategy','Sol Ring','Spark Fiend','Eureka','Hypergenesis','Delina, Wild Mage']:
    cs=[c for c in cards(n)if c['name']==name and c['owner']==1]
    if cs:ids[name]=cs[0]['id']
   ids['chaos']=[c['id']for c in cards(n)if c['name']=='Game of Chaos'];ids['lands']=[c['id']for c in cards(n)if c['zone']=='Battlefield'and c['name']in['Forest','Mountain']];ids['bears']=[c['id']for c in cards(n)if c['name']=='Grizzly Bears'and c['zone']=='Battlefield'];ids['jace']=[c['id']for c in cards(n)if c['name']=='Jace Beleren'];ids['helms']=[c['id']for c in cards(n)if c['name']=='Helm of Obedience']
   for key,name,seat in[('p2Mountain','Mountain',2),('p2Bear','Grizzly Bears',2),('p1Bear','Grizzly Bears',1)]:
    cs=[c for c in cards(n)if c['name']==name and c['owner']==seat and c['zone']=='Hand'];ids[key]=cs[0]['id']if cs else 0
   ids['p2Library']=[c['id']for c in n['players'][1]['zones']['Library']]
  if probe and n['turn']==1 and n['phase']=='MAIN1'and kind=='InputPassPriority'and r['seat']==1:
   write(folder/'opening-rng.json',dict(seed=4,rngState=n['rngState'],requestId=r['requestId'],paidOperations=0,wire=ref(folder/('request-'+str(r['requestId'])+'-wire.json'))));return True
  if not probe and n['turn']==1 and n['phase']=='MAIN1'and r['seat']==1 and not state.get('openingChecked'):
   if seed_plan:check(n['rngState']==seed_plan['openingState'],'Actual read-only opening RNG equals initial-seed plan before any paid action')
   state['openingChecked']=True;once(m,n,'opening-before-paid')
  for a in n['stack']:
   if not a['trigger']and not a['static']and a['stackId']not in state['paidStackIds']:
    state['paidStackIds'].append(a['stackId']);paid.append(dict(action='spell'if a['spell']else'activation',name=a['name'],source=a['source'],actor=a['actor'],mana=len(a['payingMana']),objects=a['payingMana'],requestId=r['requestId'],stackId=a['stackId'],targets=a['targetCards'],targetPlayers=a['targetPlayers']));once(m,n,'paid-stack-'+str(a['stackId']))
  if state['active']and kind=='InputPassPriority'and not n['stack']:finish_action(m,n)
  if probe:return False
  if case=='chaos-strategy':return state['actions']==3
  if case=='spark-note-upkeep'and state['actions']==1:
   c=exact(n,ids['Spark Fiend'])
   if n['turn']==2 and n['phase']=='UPKEEP'and not n['stack']:check(value(c,'Safe')==1 and n['players'][0]['rolls']==[],'Other player upkeep has no Spark roll or note mutation');once(m,n,'other-upkeep-no-roll')
   if n['turn']==3 and n['phase']=='UPKEEP'and not n['stack']:
    check(n['players'][0]['rolls']==seed_plan['outcomes']['rolls'][2:]and value(c,'Safe')==0,'Actual own upkeep matches noted two-die total and Minus.Noted stores zero');once(m,n,'matched-upkeep-safe-zero')
   if n['turn']==5 and n['phase']=='UPKEEP'and not n['stack']:
    check(n['players'][0]['rolls']==[]and value(c,'Safe')==0 and c['zone']=='Battlefield','Later own upkeep does not roll again after native zero gate');once(m,n,'later-upkeep-no-roll');return True
  if case=='round-decline-reset'and state['actions']==4:
   c=exact(n,ids['Hypergenesis'])
   if c['zone']=='Stack':state['active']=dict(name='Hypergenesis',source=c['id'],kind='free-cast',seat=1,beforeLife=[p['life']for p in n['players']])
   if n['turn']==2 and n['phase']=='UPKEEP':check(c['counters'].get('TIME',0)==3,'Other-player upkeep leaves actual Hypergenesis TIME unchanged');once(m,n,'hyper-opponent-upkeep')
  if case=='round-decline-reset':return state['actions']==5
  if case.startswith('helm-gates'):return state['actions']==8
  if case=='helm-replacement-tail':return state['actions']==1
  if case=='delina-high-low-reset':
   delina=exact(n,ids['Delina, Wild Mage']);tokens=[c for c in cards(n)if c['token']and c['zone']=='Battlefield'];turn=n['turn']
   if tokens:
    state['tokens'].setdefault(str(turn),[])
    for c in tokens:
     if c['id']not in state['tokens'][str(turn)]:
      check(c['owner']==1 and c['controller']==1 and c['name']=='Grizzly Bears'and c['tapped']and c['attacking']and not c['legendary']and(c['power'],c['toughness'])==(2,2),'Actual Delina token exact owner/copied state/tapped/attacking/nonlegendary');check(any(t['params'].get('Phase')=='EndCombat'for t in c['triggers']),'Actual copy owns native end-combat exile trigger');state['tokens'][str(turn)].append(c['id']);once(m,n,'token-'+str(c['id']))
   if turn==1 and n['phase']=='MAIN2'and not n['stack']:
    check(len(state['tokens'].get('1',[]))==2 and not tokens and value(delina,'RepeatCheck')==0,'Actual high-then-low creates two copies and stops, then both expire at combat end');check(n['players'][0]['rolls']==seed_plan['outcomes']['rolls'][:2],'Actual Delina first attack high then low physical dice');once(m,n,'first-attack-expired')
   if turn==3 and n['phase']=='MAIN2'and not n['stack']:
    check(len(state['tokens'].get('3',[]))==1 and not tokens and value(delina,'RepeatCheck')==1,'Actual next attack runs once despite previous zero, then optional stop and token expiry');check(n['players'][0]['rolls']==seed_plan['outcomes']['rolls'][2:],'Actual second attack high roll and decline');once(m,n,'second-attack-expired');return True
  return False
 def choose(m,n):
  r=m['request'];opts=[o for o in r['options']if not o.get('disabled')];enabled=[o['id']for o in opts];kind=r['kind'];a=state['active'];v=m['views'][r['seat']-1]
  if'Mulligan'in kind:return'keep'if'keep'in enabled else'ok'
  if kind=='InputPassPriority':
   if not a and not n['stack']:
    num=state['actions']
    if case=='chaos-strategy'and r['seat']==1 and n['turn']==1 and n['phase']=='MAIN1':return start_action(m,n,'Game of Chaos',source=ids['chaos'][num],targets=[('player',2)])if num<2 else start_action(m,n,'Strategy, Schmategy')
    if case=='spark-note-upkeep'and num==0 and r['seat']==1 and n['turn']==1 and n['phase']=='MAIN1':return start_action(m,n,'Spark Fiend')
    if case=='helm-replacement-tail'and num==0 and r['seat']==1 and n['turn']==1 and n['phase']=='MAIN1':return start_action(m,n,'Helm of Obedience','activation',source=ids['helms'][0],x=2,targets=[('player',2)])
    if case=='round-decline-reset'and r['seat']==1 and n['turn']==1 and n['phase']=='MAIN1':
     if num==0:return start_action(m,n,'Eureka')
     if num==1:return start_action(m,n,'Regrowth',targets=[('card',ids['Eureka'])])
     if num==2:return start_action(m,n,'Eureka',source=ids['Eureka'])
     if num==3:return start_action(m,n,'Hypergenesis','suspend')
    if case.startswith('helm-gates')and n['turn']==1 and n['phase']=='MAIN1':
     if num in[0,2,3,7]and r['seat']==1:return start_action(m,n,'Helm of Obedience','activation',source=ids['helms'][0 if num in[0,2]else 1],x=1 if case=='helm-gates-tail'and num==0 else 2 if num in[0,7]else 3,targets=[('player',2)])
     if num in[1,5]and r['seat']==1:return start_action(m,n,'Twiddle',targets=[('card',ids['helms'][0 if num==1 else 1])])
     if num==4 and r['seat']==2:return start_action(m,n,'Reclaim',seat=2,targets=[('card',ids['p2Library'][0])])
     if num==6 and r['seat']==1:return start_action(m,n,'Rest in Peace')
   return'ok'
  if kind=='ChooseNumber':
   check(a and a.get('x',0)>=1,'Actual original Helm XMin1 number choice');return'number-plus'if r['selectedValue']<a['x']else'number-minus'if r['selectedValue']>a['x']else'confirm-number'
  if kind=='ChooseOptions'and case=='round-decline-reset':
   if'confirm-cards'in enabled:return'confirm-cards'
   ckey=str(a['source'])+'-'+str(state['actions']);i=state['choiceCounts'].get(ckey,0);check(r['seat']==(1 if i%2==0 else 2),'Actual repeat player chooser order starts with original caster each round');put=(a['name']=='Eureka'and state['actions']==0 and i in[0,1,3])or(a['name']=='Hypergenesis'and i==0);state['choiceCounts'][ckey]=i+1;wanted='Choose 'if put else'Do not put';once(m,n,'repeat-choice-'+str(state['actions'])+'-'+str(i));return next(o['id']for o in opts if wanted in o['label'])
  if kind=='dialog':
   if r['message']=='getAbilityToPlay':
    if a and a['kind']=='suspend':return next(o['id']for o in opts if o['label'].startswith('Suspend '))
    return opts[0]['id']
   if case=='chaos-strategy'and a and a['name']=='Game of Chaos':
    heads=next((o['id']for o in opts if o['label'].lower()=='heads'),None)
    if heads:return heads
   yes=next((o['id']for o in opts if o['label'].lower()=='yes'),None)
   if yes:return yes
   raise AssertionError(('Unknown actual Repeat dialog',r,a))
  if'PayMana'in kind:
   mana=next((o['id']for o in opts if o['id'].startswith('mana:')),None)
   if mana:return mana
   landIds={c['id']:c['name']for c in n['players'][r['seat']-1]['zones']['Battlefield']if c['name']in['Island','Plains','Mountain','Forest']and not c['tapped']};lands=[o for o in opts if o['id'].startswith('card:')and int(o['id'].split(':')[1])in landIds]
   if a and a['name']=='Rest in Peace':lands.sort(key=lambda o:landIds[int(o['id'].split(':')[1])]!='Plains')
   if lands:return lands[0]['id']
   return'ok'
  if kind=='InputSelectTargets':
   targets=a.get('targets',[])if a else[('card',ids.get('p1Bear')or ids['bears'][0])]
   for typ,id in targets:
    key='player:'+str(v['players'][id-1]['targetId'])if typ=='player'else'card:'+str(id)
    if key in enabled and not next(o for o in opts if o['id']==key).get('selected'):return key
   if'ok'in enabled:return'ok'
   raise AssertionError(('Target contract',r,targets))
  if kind=='InputAttack':
   if case=='delina-high-low-reset'and r['seat']==1 and n['turn']in[1,3]:
    sid=ids['Delina, Wild Mage'];chosen=state['attackChosen'].setdefault(str(n['turn']),[]);option=next((o['id']for o in opts if o['id'].startswith('attack:'+str(sid)+':')),None)
    if option and sid not in chosen:chosen.append(sid);return option
   return'ok'
  if kind=='InputConfirm':
   if a and a['name']=='Twiddle':return next(o['id']for o in opts if o['label']=='Untap')
   if r['message'].endswith('Call coin flip'):return next(o['id']for o in opts if o['label'].lower()=='heads')
   if case=='chaos-strategy'and a and a['name']=='Game of Chaos':
    key=str(a['source']);rounds=state['flipRounds'].setdefault(key,[]);i=len(rounds);before=a['beforeLife'][0]if i==0 else rounds[-1]['life'][0];delta=n['players'][0]['life']-before;check(abs(delta)==2**i,'Actual Chaos sequential stake doubles from previous stored value');winner=1 if delta>0 else 2;check(r['seat']==winner,'Actual remembered Chaos winner chooses optional repeat');check(value(exact(n,a['source']),'NumLife')==2**(i+1),'Actual native stored next Chaos stake');rounds.append(dict(winner=winner,stake=abs(delta),life=[p['life']for p in n['players']],requestId=r['requestId']));once(m,n,'chaos-optional-'+str(state['actions'])+'-'+str(i));return'ok'if i==0 else'cancel'
   if case=='delina-high-low-reset':once(m,n,'delina-optional-'+str(n['turn']));return'ok'if n['turn']==1 else'cancel'
   return'ok'
  if kind=='OrderAbilities':return next((o['id']for o in opts if o['id'].startswith('order:')),'confirm-order')
  if kind=='InputBlock':return'ok'
  if kind=='RevealCards':return'confirm-cards'
  if kind.startswith('InputSelect')or kind in['SelectCards','ChooseEntity','SelectEntity']:
   if case=='round-decline-reset'and a and a['name']in['Eureka','Hypergenesis']:
    index=state['choiceCounts'][str(a['source'])+'-'+str(state['actions'])]-1;target=ids['jace'][0]if a['name']=='Eureka'and r['seat']==1 else ids['p2Mountain']if a['name']=='Eureka'and index==1 else ids['p2Bear']if a['name']=='Eureka'else ids['p1Bear']
    candidateIds=[int(o['id'].split(':')[-1])for o in opts if o['id'].startswith('card:')]
    if a['name']=='Eureka'and r['seat']==1:check(ids['jace'][0]in candidateIds,'Actual Eureka permanent selector includes planeswalker')
    if a['name']=='Hypergenesis':check(ids['jace'][1]not in candidateIds,'Actual Hypergenesis allowed-type selector excludes planeswalker')
    option=next((o['id']for o in opts if o['id']=='card:'+str(target)and not o.get('selected')),None);return option or('confirm-cards'if'confirm-cards'in enabled else'ok')
   if'Cleanup'in r['message']:
    if'ok'in enabled:return'ok'
    return next(o['id']for o in opts if o['id'].startswith('card:')and not o.get('selected'))
   if a and a['name']=='Twiddle':return next((o['id']for o in opts if o['label'].lower()=='untap'),opts[0]['id'])
   raise AssertionError(('Unknown actual Repeat selection',r,a))
  raise AssertionError(('Unhandled actual Repeat input',r,a))
 try:
  compiler=Path(os.environ.get('MTGB_JAVAC',r'C:/Program Files/Java/jdk-17/bin/javac.exe'))
  with(folder/'compile.log').open('w',encoding='utf8')as log:subprocess.run([str(compiler),'-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*map(str,sources[1:3])],check=True,stdout=log,stderr=subprocess.STDOUT,timeout=60)
  frozen+=list(map(ref,sorted(classes.rglob('*.class'))))
  with tempfile.TemporaryDirectory(prefix='profile-',dir=folder,ignore_cleanup_errors=True)as profile,(folder/'stderr.log').open('w',encoding='utf8')as err,(folder/'trace.jsonl').open('w',encoding='utf8')as trace:
   deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[]);config=dict(session=case,seed=4 if probe or not seed_plan else seed_plan['seed'],startingLife=40,firstPlayerSeat=1,decks=[deck,deck],positions=spec['positions'],auditDir=str(audit))
   if case=='spark-note-upkeep':config['numericAudit']=str(folder/'numeric-adapter.json')
   write(folder/'start.json',config);write(folder/'seed-plan.json',seed_plan)
   testedOverrides=[]
   if native_override:
    testedOverrides=list(map(ref,sorted(native_override.rglob('*.class'))));frozen+=testedOverrides;write(folder/'tested-native-overrides.json',testedOverrides)
   cp=([native_override]if native_override else[])+[classes,RT/'classes',RT/'lib/*']
   proc=subprocess.Popen([str(RT/runtime['java']),'-Xmx512m','-XX:ActiveProcessorCount=3','--add-opens=java.base/java.util=ALL-UNNAMED','-Djava.awt.headless=true','-Djava.io.tmpdir='+profile,'-cp',';'.join(map(str,cp)),'mtgb.bridge.RepeatStoredResultsFixture',str(RT/runtime['assets']),str(folder/'start.json')],cwd=RT/runtime['assets'],env=dict(os.environ,APPDATA=profile,LOCALAPPDATA=profile,TEMP=profile,TMP=profile),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
   q=queue.Queue()
   def reader():
    try:
     for line in proc.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start();seen=set()
   for step in range(1600):
    check(time.monotonic()<deadline,'Bounded actual shared native flow');m=q.get(timeout=45);check(m is not None,'Native bridge remained live')
    if isinstance(m,Exception):raise m
    trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush();check(m['type']not in['fatal','ended','rejected'],str({k:v for k,v in m.items()if k!='views'}))
    if m['type']!='request':continue
    r=m['request'];rid=r['requestId']
    if rid in seen:continue
    seen.add(rid);n=None if'Mulligan'in r['kind']else observe(rid)
    if n:write(folder/('request-'+str(rid)+'-wire.json'),m);write(folder/('request-'+str(rid)+'-observation.json'),n)
    requests.append(dict(id=rid,kind=r['kind'],seat=r['seat'],message=r['message']));write(folder/'requests.json',requests)
    try:
     if n and monitor(m,n):break
     pick=choose(m,n);check(pick in[o['id']for o in r['options']if not o.get('disabled')],('Original enabled native input',pick,r))
    except Exception:pick=pause(traceback.format_exc(),m,n);check(pick in[o['id']for o in r['options']if not o.get('disabled')],'Driver correction submits only original enabled input')
    write(folder/'state.json',state);write(folder/'paid.json',paid);response=dict(type='respond',session=case,seat=r['seat'],requestId=rid,option=pick);trace.write(json.dumps(response)+'\n');trace.flush();proc.stdin.write(json.dumps(response)+'\n');proc.stdin.flush();print(case,step,n['turn']if n else 0,n['phase']if n else'',r['kind'],pick,flush=True)
   else:raise AssertionError('Actual repeat request-step cap')
  if not probe:check(validate_terminal(case,state,endpoints,paid),'All planned original terminal endpoints and actual operations completed')
  status='OPENING_RNG_READ_ONLY'if probe else'COMPLETE_WITH_RAW_FAILURES'if failures else'PASS_BOUNDED'
 except Exception:status='FAIL';error=traceback.format_exc();print(error,flush=True)
 finally:
  if proc is not None and proc.poll()is None:proc.kill();proc.wait()
 result=dict(status=status,case=case,inputIds=spec['inputIds'],runtimeHash=runtime['runtimeHash'],folder=folder.relative_to(ROOT).as_posix(),checks=len(checks),assertions=checks,paid=paid,endpoints=endpoints,representatives=representatives,requests=requests,state=state,ids=ids,frozenSources=frozen,failures=failures,limitations=['Only five actual two-human original minimum flows; initial permanent placements bypass ETB.','All initial seed planning is offline; actual RNG state observation is read-only and no runtime outcome is injected.','Unavailable MaxRepeat0 and infinite-cap producers remain conditional source scope, not fabricated native results.','UI localization and remote delivery are root-owned; no production or canonical edits.'])
 if status=='FAIL':result['error']=error
 write(folder/'result.json',result);write(BASE/(case+('-probe'if probe else'')+'-latest.json'),result);print(status,case,folder,flush=True);return result
if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('case',choices=CASES);parser.add_argument('--probe',action='store_true');parser.add_argument('--seed-plan',type=Path);parser.add_argument('--native-override',type=Path);args=parser.parse_args();seed_plan=json.loads(args.seed_plan.read_text('utf8'))if args.seed_plan else None;result=run(args.case,args.probe,seed_plan,args.native_override);sys.exit(0 if result['status']in['PASS_BOUNDED','OPENING_RNG_READ_ONLY']else 1)
