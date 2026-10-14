"""Three shared native keyword flows. Setup only, original enabled input responses, raw failures retained."""
from pathlib import Path
from collections import Counter
import argparse,hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback,sys
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'.tools/characteristics-keywords-payments-1011/keywords';RT=ROOT/'ForgeRuntime'
CASES={
 'suspend-cancel-retry':dict(inputId='suspend-special-action-last-counter',positions=[{'Hand':['Aeon Chronicler'],'Battlefield':['Island']*8,'Library':['Island']*8},{'Hand':[],'Battlefield':[]}]),
 'suspend-cancel-original':dict(inputId='suspend-special-action-last-counter',positions=[{'Hand':['Aeon Chronicler'],'Battlefield':['Island']*8,'Library':['Island']*8},{'Hand':[],'Battlefield':[]}]),
 'variable-creature':dict(inputId='suspend-special-action-last-counter',positions=[{'Hand':['Aeon Chronicler','Ancestral Vision','Donate'],'Battlefield':['Island']*8+['Sphere of Resistance'],'Library':['Island']*12},{'Hand':[],'Battlefield':[]}]),
 'target-impossible':dict(inputId='suspend-special-action-last-counter',positions=[{'Hand':['Ancestral Vision','Ancestral Vision','Unsummon'],'Battlefield':['Island']*8+['Sphere of Resistance','True Believer'],'Library':['Island']*12},{'Hand':[],'Battlefield':['True Believer']}]),
 'self-live-power':dict(inputId='firebending-own-attacks-and-live-amount',positions=[{'Hand':['Environmental Sciences','Giant Growth'],'Battlefield':['Firebending Student','Fire Nation Cadets','Merfolk Looter','Grizzly Bears','Forest','Forest'],'Library':['Island']*10},{'Hand':[],'Battlefield':['Grizzly Bears','Scavenging Ooze','Forest']}]),
}
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def ref(p):return dict(path=p.relative_to(ROOT).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def cards(n):return list({c['id']:c for p in n['players']for z in p['zones'].values()for c in z}.values())
def exact(n,id):return next(c for c in cards(n)if c['id']==id)
def named(n,name,owner=1,zone=None):return next(c for c in cards(n)if c['name']==name and c['owner']==owner and(zone is None or c['zone']==zone))
def pool(n,seat=1):return n['players'][seat-1]['mana']
def ms(values):return Counter(json.dumps(x,sort_keys=True)for x in values)
def time_counters(c):return sum(v for k,v in c['counters'].items()if k.upper()=='TIME')
def fire(c):return [k for k in c['keywords']if k.startswith('Firebending')]
def run(case):
 BASE.mkdir(parents=True,exist_ok=True);runtime=json.loads((RT/'runtime.json').read_text('utf-8-sig'));spec=CASES[case]
 folder=BASE/'native'/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir();classes=folder/'classes';classes.mkdir()
 sources=[Path(__file__),ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java',ROOT/'Tools/ForgeBridge/tests/SuspendFirebendingTailFixture.java',RT/'runtime.json']
 frozen=[]
 for path in sources:
  target=folder/path.name;target.write_bytes(path.read_bytes());frozen.append(ref(target))
 write(folder/'case-plan.json',spec);checks=[];failures=[];paid=[];endpoints=[];representatives=[];requests=[];proc=None;state={'active':None,'staticCompleted':0,'cancelled':False,'cancelRequested':False,'attackChosen':{},'freeAttempts':{},'paidStackIds':[],'captured':[]};ids={};deadline=time.monotonic()+1000
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
  write(folder/(label+'-wire.json'),m);write(folder/(label+'-observation.json'),n)
  representatives.append(dict(label=label,requestId=m['request']['requestId'],kind=m['request']['kind'],wire=ref(folder/(label+'-wire.json')),observation=ref(folder/(label+'-observation.json'))))
 def once(m,n,label):
  if label not in state['captured']:freeze(m,n,label);state['captured'].append(label)
 def pause(error,m,n):
  record=dict(index=len(failures)+1,error=error,requestId=m['request']['requestId'],state=state.copy(),paid=len(paid),endpoints=len(endpoints));failures.append(record);write(folder/('raw-failure-'+str(len(failures))+'.json'),record);freeze(m,n,'pause-'+str(len(failures)));write(folder/'pause.json',record);print('PAUSED',folder,error,flush=True)
  until=time.monotonic()+240
  while proc.poll()is None and time.monotonic()<until:
   p=folder/'control.json'
   if p.exists():
    value=json.loads(p.read_text('utf8'));p.replace(folder/('control-used-'+str(len(failures))+'.json'));state.update(value.get('state',{}));return value['option']
   time.sleep(.2)
  raise AssertionError('Bounded native pending input correction expired')
 def start_action(m,n,name,kind='cast',seat=1,zone='Hand',**extras):
  card=named(n,name,seat,zone);active=dict(name=name,source=card['id'],kind=kind,seat=seat,beforeRequest=m['request']['requestId'],**extras);state['active']=active;freeze(m,n,'before-'+name.replace(' ','-')+'-'+str(len(endpoints)));return 'card:'+str(card['id'])
 def finish_action(m,n):
  action=state['active'];c=exact(n,action['source'])
  if action['kind']=='suspend':
   check(c['zone']=='Exile'and time_counters(c)==action['time'],'Original suspended card and exact initial TIME '+action['name']);check(c['suspended'],'Actual TIME-positive suspend state')
   check(not any(a['spell']or a['static']for a in n['stack']),'Suspend special action never put spell/static action on stack at completed endpoint');check(n['players'][0]['castsGame']==0,'Actual Suspend does not increment cast history')
   lands=[x for x in n['players'][0]['zones']['Battlefield']if x['name']=='Island'];spent=sum(x['tapped']for x in lands)-action['beforeTapped'];check(spent==action['mana'],'Exact actual suspended mana-source tap count '+action['name'])
   check(not pool(n),'No leftover suspend payment mana');paid.append(dict(action='suspend',source=c['id'],name=c['name'],mana=spent,requestId=m['request']['requestId'],kind='static-original-resource-delta'))
   state['staticCompleted']+=1;ids.setdefault('suspends',[]).append(c['id']);once(m,n,'suspended-'+str(state['staticCompleted']))
  elif action['name']=='Donate':check(exact(n,ids['Aeon Chronicler'])['controller']==2 and not exact(n,ids['Aeon Chronicler'])['haste'],'Real control change removes native suspend haste');once(m,n,'donated-haste-ended');state['donated']=True
  elif action['name']=='Unsummon':check(exact(n,ids['ownBeliever'])['zone']=='Hand','Original shroud source actually returned to hand');once(m,n,'own-believer-returned');state['bounced']=True
  elif action['name']=='Merfolk Looter':check(exact(n,ids['lesson'])['zone']=='Graveyard'and fire(exact(n,ids['Fire Nation Cadets'])),'Real discard changes native conditional Firebending entry');once(m,n,'cadets-condition-gained');state['looted']=True
  elif action['name']=='Giant Growth':check(exact(n,ids['Firebending Student'])['power']==5,'Native Giant Growth plus native Prowess changes live power1 to5 before Firebending resolves');once(m,n,'live-power-five');state['grown']=True
  elif action['name']=='Fire Nation Cadets':check(exact(n,ids['Fire Nation Cadets'])['power']==2,'Generated native R actually pays Cadets2 and changes its power');check(len(pool(n))==5 and all(x['color']=='R'and x['combat']for x in pool(n)),'Exactly five original combat R remain after actual two-unit payment');once(m,n,'combat-mana-spent');state['spent']=True
  elif action['name']=='Scavenging Ooze':check(exact(n,ids['lesson'])['zone']=='Exile'and not fire(exact(n,ids['Fire Nation Cadets'])),'Actual opposite actor exiles original Lesson and native conditional keyword is lost');check(n['players'][1]['life']==action['beforeLife'],'Noncreature Lesson gives no Ooze creature life bonus');once(m,n,'cadets-condition-lost');state['removed']=True
  endpoints.append(dict(action=action['kind'],name=action['name'],source=action['source'],requestId=m['request']['requestId'],turn=n['turn'],phase=n['phase']));write(folder/'endpoints.json',endpoints);write(folder/'paid.json',paid);state['active']=None
 def monitor(m,n):
  r=m['request'];v=m['views'][0]
  active=state['active']
  if active and active['name']=='Unsummon' and exact(n,ids['ownBeliever'])['zone']=='Hand' and not any(a['source']==active['source'] for a in n['stack']):finish_action(m,n);active=None
  if active and active['name']=='Giant Growth' and exact(n,active['source'])['zone']=='Graveyard' and exact(n,ids['Firebending Student'])['power']==5:
   check(any(a['api']=='Mana' and a['source']==ids['Firebending Student'] for a in n['stack']),'Live power changed while original Firebending trigger still awaits resolution');finish_action(m,n)
  if not ids:
   for name in ['Aeon Chronicler','Ancestral Vision','Donate','Unsummon','Firebending Student','Fire Nation Cadets','Merfolk Looter']:
    found=next((c for c in cards(n)if c['name']==name and c['owner']==1),None)
    if found:ids[name]=found['id']
   ids['lesson']=next((c['id']for c in cards(n)if c['name']=='Environmental Sciences'),0);ids['ownBeliever']=next((c['id']for c in cards(n)if c['name']=='True Believer'and c['owner']==1),0)
  for sa in n['stack']:
   if sa['spell']and sa['stackId']not in state['paidStackIds']:
    state['paidStackIds'].append(sa['stackId']);paid.append(dict(action='spell',name=sa['name'],source=sa['source'],mana=len(sa['payingMana']),objects=sa['payingMana'],requestId=r['requestId'],stackId=sa['stackId'],targets=sa['targetCards'],targetPlayers=sa['targetPlayers']));freeze(m,n,'paid-stack-'+str(sa['stackId']))
   elif not sa['spell']and not sa['trigger']and sa['stackId']not in state['paidStackIds']:
    state['paidStackIds'].append(sa['stackId']);paid.append(dict(action='activation',name=sa['name'],source=sa['source'],mana=len(sa['payingMana']),objects=sa['payingMana'],requestId=r['requestId'],stackId=sa['stackId'],targets=sa['targetCards']));freeze(m,n,'paid-stack-'+str(sa['stackId']))
  if case=='suspend-cancel-original':
   return bool(state['cancelled'])
  if case=='suspend-cancel-retry':
   return bool(state['cancelled'] and state['staticCompleted']==1)
  if case=='self-live-power':
   ownStudent=exact(n,ids['Firebending Student']);cadets=exact(n,ids['Fire Nation Cadets'])
   for sa in n['stack']:
    if sa['trigger']and sa['api']=='Mana'and sa['source']in[ownStudent['id'],cadets['id']]:
     check(n['turn']==3 and ownStudent['attacking']and cadets['attacking'],'Exact keyword sources only trigger on their own declared attacks');check(sa['actor']==1 and sa['params'].get('Produced')=='R'and sa['params'].get('CombatMana')=='True','Actual Firebending recipient/color/combat marker');once(m,n,'native-fire-trigger-'+str(sa['source']))
   if n['turn']==1 and n['phase'] in['COMBAT_DECLARE_ATTACKERS','COMBAT_DECLARE_BLOCKERS','COMBAT_FIRST_STRIKE_DAMAGE','COMBAT_DAMAGE','COMBAT_END']:
    check(not any(a['api']=='Mana'and a['source']==ownStudent['id']for a in n['stack'])and not pool(n),'Other own attacker does not trigger this native Student');once(m,n,'other-own-attacker-no-fire')
   if n['turn']==2 and n['phase'].startswith('COMBAT'):
    check(not any(a['api']=='Mana'and a['source']==ownStudent['id']for a in n['stack'])and not pool(n),'Other-player attack does not trigger this original Student');once(m,n,'opponent-attacker-no-fire')
   if state.get('grown')and not state.get('spent')and not state['active']and r['kind']=='InputPassPriority'and not n['stack']and n['turn']==3:
    check(len(pool(n))==7 and all(x['color']=='R'and x['combat']and x['actor']==1 for x in pool(n)),'Actual seven R: dynamic Student5 plus conditional Cadets2');check(sum(x['source']==ownStudent['id']for x in pool(n))==5 and sum(x['source']==cadets['id']for x in pool(n))==2,'Generated native Mana retains exact two source identities');once(m,n,'generated-five-plus-two')
   if state.get('removed')and n['turn']==3 and n['phase']=='MAIN2':check(not pool(n),'Original remaining combat mana expires at main2');once(m,n,'combat-expired')
   if state.get('removed')and n['turn']==5 and n['phase']=='MAIN2':check(not pool(n)and not fire(cadets),'Cadets later actual attack while condition absent creates no Firebending mana');once(m,n,'later-cadets-no-fire');return True
   return False
  suspended=[exact(n,id)for id in ids.get('suspends',[])]
  if len(suspended)==2 and n['turn']==2 and n['phase']=='UPKEEP':
   check([time_counters(c)for c in suspended]==([1,4]if case=='variable-creature'else[4,4]),'Other player upkeep does not remove these own TIME counters');once(m,n,'opponent-upkeep-unchanged')
  if case=='variable-creature'and len(suspended)==2:
   aeon=exact(n,ids['Aeon Chronicler']);vision=exact(n,ids['Ancestral Vision'])
   if aeon['zone']=='Battlefield'and aeon['controller']==1:
    check(time_counters(aeon)==0 and aeon['haste'],'Actual free creature has suspend haste after last counter');once(m,n,'aeon-free-entered')
    if n['turn']>=5:once(m,n,'haste-survived-cleanup')
   if n['turn']>=9 and vision['zone']=='Exile'and time_counters(vision)==0 and not any(a['source']==vision['id']for a in n['stack']):
    check(not vision['suspended'],'TIME0 exile is not advertised as suspended');once(m,n,'vision-declined-zero')
   if state.get('donated')and n['turn']>=11 and n['phase']=='UPKEEP'and not n['stack']:
    check(vision['zone']=='Exile'and time_counters(vision)==0 and not vision['suspended'],'Declined zero-TIME source remains exiled across a later own upkeep');check(n['players'][0]['castsGame']==2,'Only free Aeon and actual Donate were cast, Suspend and declined Vision were not');once(m,n,'later-zero-no-retrigger');return True
  if case=='target-impossible'and len(suspended)==2:
   for sid,attempt in list(state['freeAttempts'].items()):
    c=exact(n,int(sid))
    if attempt=='accepted'and c['zone']=='Exile'and time_counters(c)==0 and r['kind']=='InputPassPriority'and not any(a['source']==c['id']for a in n['stack']):
     if not state.get('bounced'):
      state['impossible']=c['id'];once(m,n,'impossible-exile-zero')
   succeeded=next((c for c in suspended if c['zone']=='Graveyard'),None)
   if succeeded and r['kind']=='InputPassPriority'and n['phase']=='UPKEEP'and not n['stack']:
    check(state.get('bounced')and state.get('impossible')and succeeded['id']!=state['impossible'],'Actual other original suspended card freely cast after shroud source removed');once(m,n,'vision-target-draw-resolved');state['targetResolved']=succeeded['id']
   if state.get('targetResolved')and n['turn']>=11 and n['phase']=='UPKEEP'and not n['stack']:
    impossible=exact(n,state['impossible']);check(impossible['zone']=='Exile'and time_counters(impossible)==0 and not impossible['suspended'],'Uncastable last-counter source stays TIME0 across later upkeep');once(m,n,'impossible-later-no-retrigger');return True
  return False
 def choose(m,n):
  r=m['request'];opts=[o for o in r['options']if not o.get('disabled')];enabled=[o['id']for o in opts];kind=r['kind'];v=m['views'][0];active=state['active'];pick='ok'
  if 'Mulligan'in kind:return 'keep'if'keep'in enabled else'ok'
  if kind=='InputPassPriority':
   if active and not n['stack']:
    if state['cancelRequested']:
     c=exact(n,active['source']);check(c['zone']=='Hand'and time_counters(c)==0 and not pool(n),'Cancelled original special action stayed in hand without counters/mana');check(sum(c['tapped']for c in n['players'][0]['zones']['Battlefield']if c['name']=='Island')==active['beforeTapped'],'Original cancelled suspend tap/mana refunded');once(m,n,'cancelled-suspend-refunded');endpoints.append(dict(action='cancel-suspend',name=c['name'],source=c['id'],requestId=r['requestId'],turn=n['turn'],phase=n['phase']));state['active']=None;state['cancelRequested']=False;state['cancelled']=True;active=None
    else:finish_action(m,n);active=None
   if case!='self-live-power':
    if case=='suspend-cancel-original'and state['cancelled']:return'ok'
    if case=='suspend-cancel-retry'and state['staticCompleted']==1:return'ok'
    if state['staticCompleted']<2 and n['turn']==1 and n['phase']=='MAIN1'and r['seat']==1 and not n['stack']:
     name='Aeon Chronicler'if case in['variable-creature','suspend-cancel-original','suspend-cancel-retry']and state['staticCompleted']==0 else'Ancestral Vision';card=named(n,name,1,'Hand');mana=5 if name=='Aeon Chronicler'else 1;counter=1 if name=='Aeon Chronicler'else 4
     return start_action(m,n,name,'suspend',time=counter,mana=mana,beforeTapped=sum(c['tapped']for c in n['players'][0]['zones']['Battlefield']if c['name']=='Island'),cancel=(name=='Aeon Chronicler'and not state['cancelled']))
    if case=='variable-creature'and not state.get('donated')and n['turn']>=5 and n['phase']=='MAIN1'and r['seat']==1 and not n['stack']:
     check(exact(n,ids['Aeon Chronicler'])['haste'],'Suspend haste persists beyond initial casting turn before actual control change');return start_action(m,n,'Donate',targets=[('player',2),('card',ids['Aeon Chronicler'])])
    if case=='target-impossible'and state.get('impossible')and not state.get('bounced')and not active and r['seat']==1:
     check('card:'+str(ids['Unsummon'])in enabled,'Actual instant available during remaining last-counter stack');return start_action(m,n,'Unsummon',targets=[('card',ids['ownBeliever'])])
   else:
    if n['turn']==3 and n['phase']=='MAIN1'and r['seat']==1 and not state.get('looted')and not n['stack']:return start_action(m,n,'Merfolk Looter','activate',zone='Battlefield')
    if n['turn']==3 and r['seat']==1 and not active and not state.get('grown')and any(a['api']=='Mana'and a['source']==ids['Firebending Student']for a in n['stack']):return start_action(m,n,'Giant Growth',targets=[('card',ids['Firebending Student'])])
    if state.get('grown')and not state.get('spent')and r['seat']==1 and not n['stack']:return start_action(m,n,'Fire Nation Cadets','activate',zone='Battlefield')
    if state.get('spent')and not state.get('removed')and r['seat']==2 and not n['stack']:return start_action(m,n,'Scavenging Ooze','activate',seat=2,zone='Battlefield',targets=[('card',ids['lesson'])],beforeLife=n['players'][1]['life'])
   return 'ok'
  if kind=='ChooseNumber':
   check(active and active['name']=='Aeon Chronicler','Actual variable Suspend number source');check(r['minValue']==1 and r['selectedValue']>=1,'Native XMin1 rejects zero through actual number contract');once(m,n,'suspend-number-min-one');return 'number-minus'if r['selectedValue']>1 else'confirm-number'
  if kind=='dialog':
   if r['message']=='getAbilityToPlay':
    if active and active['kind']=='suspend':return next(o['id']for o in opts if o['label'].startswith('Suspend '))
    if active and active['name']=='Merfolk Looter':return next(o['id']for o in opts if'Draw a card'in o['label'])
    return opts[0]['id']
   # Actual optional Play prompt uses native YES/NO candidates.
   if any(o['label'].lower()in['yes','是']for o in opts)and any(o['label'].lower()in['no','否']for o in opts):
    source=r.get('sourceCardId')or next((a['source']for a in n['stack']if a['api']=='Play'),0);c=exact(n,source);decline=case=='variable-creature'and c['name']=='Ancestral Vision';wanted='no'if decline else'yes';pick=next(o['id']for o in opts if o['label'].lower()in([wanted,'否']if decline else[wanted,'是']));state['freeAttempts'][str(source)]='declined'if decline else'accepted';once(m,n,'free-optional-'+str(source));return pick
   raise AssertionError(('Unknown actual keyword dialog',r))
  if 'PayMana'in kind:
   if active and active.get('cancel')and not state['cancelRequested']:
    taps=sum(c['tapped']for c in n['players'][0]['zones']['Battlefield']if c['name']=='Island')
    if taps>active['beforeTapped']:check('cancel'in enabled,'Actual suspend payment cancellation enabled');state['cancelRequested']=True;once(m,n,'partly-paid-suspend-before-cancel');return 'cancel'
   # Commit only through exact currently available native mana/card options.
   mana=next((o['id']for o in opts if o['id'].startswith('mana:')),None)
   if mana:return mana
   landIds={c['id'] for c in n['players'][r['seat']-1]['zones']['Battlefield'] if c['name'] in['Island','Forest'] and not c['tapped']}
   lands=[o for o in opts if o['id'].startswith('card:')and int(o['id'].split(':')[1])in landIds]
   if lands:return lands[0]['id']
   return 'ok'
  if kind=='InputSelectTargets':
   targets=active.get('targets',[])if active else[];candidates=[]
   for typ,id in targets:
    if typ=='player':key='player:'+str(v['players'][id-1]['targetId'])
    else:key='card:'+str(id)
    if key in enabled and not next(o for o in opts if o['id']==key).get('selected'):candidates.append(key)
   if not active:
    candidates=[o['id']for o in opts if o['id']=='player:'+str(v['players'][0]['targetId'])and not o.get('selected')]
   if candidates:return candidates[0]
   if'ok'in enabled:return'ok'
   if not active and not any(o['id'].startswith(('card:','player:'))for o in opts):once(m,n,'free-no-legal-target-candidates');return'cancel'
   raise AssertionError(('Actual target selection unavailable',r,active))
  if kind=='InputAttack':
   names=[]
   if case=='self-live-power':names=['Grizzly Bears']if n['turn']in[1,2]else['Firebending Student','Fire Nation Cadets']if n['turn']==3 else['Fire Nation Cadets']if n['turn']==5 else[]
   intended=[c['id']for c in cards(n)if c['controller']==r['seat']and c['zone']=='Battlefield'and c['name']in names]
   already=state['attackChosen'].setdefault(str(n['turn']),[])
   for sid in intended:
    option=next((o['id']for o in opts if o['id'].startswith('attack:'+str(sid)+':')),None)
    if option and sid not in already:already.append(sid);return option
   if case=='variable-creature'and n['turn']==3:check(any(o['id'].startswith('attack:'+str(ids['Aeon Chronicler'])+':')for o in opts),'Actual free creature can attack same turn with suspend haste');once(m,n,'same-turn-haste-attack-option')
   return'ok'
  if kind=='OrderAbilities':
   remaining=[o for o in opts if o['id'].startswith('order:')]
   return remaining[0]['id']if remaining else'confirm-order'
  if kind=='InputConfirm':
   play=next((a for a in n['stack']if a['api']=='Play'),None)
   if play:
    source=play['source'];decline=case=='variable-creature'and exact(n,source)['name']=='Ancestral Vision';state['freeAttempts'][str(source)]='declined'if decline else'accepted';once(m,n,'free-optional-'+str(source));return'cancel'if decline else'ok'
   return'ok'
  if kind=='RevealCards':return'confirm-cards'
  if kind=='InputBlock':return'ok'
  if kind.startswith('InputSelect')or kind in['SelectCards','ChooseEntity','SelectEntity']:
   if active and active['name']=='Merfolk Looter':
    option=next((o['id']for o in opts if(o.get('card')or{}).get('id')==ids['lesson']and not o.get('selected')),None)
    return option or('confirm-cards'if'confirm-cards'in enabled else'ok')
   if 'Cleanup' in r['message']:
    if'ok'in enabled:return'ok'
    return next(o['id']for o in opts if(o.get('card')or{}).get('name')=='Island'and not o.get('selected'))
   raise AssertionError(('Unknown actual keyword selection',r,active))
  raise AssertionError(('Unhandled actual keyword input',r))
 try:
  compiler=Path(os.environ.get('MTGB_JAVAC',r'C:/Program Files/Java/jdk-17/bin/javac.exe'))
  with(folder/'compile.log').open('w',encoding='utf8')as log:subprocess.run([str(compiler),'-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*map(str,sources[1:3])],check=True,stdout=log,stderr=subprocess.STDOUT,timeout=60)
  with tempfile.TemporaryDirectory(prefix='profile-',dir=folder,ignore_cleanup_errors=True)as profile,(folder/'stderr.log').open('w',encoding='utf8')as err,(folder/'trace.jsonl').open('w',encoding='utf8')as trace:
   deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[]);config=dict(session=case,seed=4,startingLife=40,firstPlayerSeat=1,decks=[deck,deck],positions=spec['positions'],auditDir=str(audit));write(folder/'start.json',config)
   proc=subprocess.Popen([str(RT/runtime['java']),'-Xmx512m','-XX:ActiveProcessorCount=3','-Djava.awt.headless=true','-Djava.io.tmpdir='+profile,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.SuspendFirebendingTailFixture',str(RT/runtime['assets']),str(folder/'start.json')],cwd=RT/runtime['assets'],env=dict(os.environ,APPDATA=profile,LOCALAPPDATA=profile,TEMP=profile,TMP=profile),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
   q=queue.Queue()
   def reader():
    try:
     for line in proc.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start();seen=set()
   for step in range(2400):
    check(time.monotonic()<deadline,'Bounded actual shared native flow');m=q.get(timeout=45);check(m is not None,'Native bridge remained live')
    if isinstance(m,Exception):raise m
    trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush();check(m['type']not in['fatal','ended','rejected'],str(m))
    if m['type']!='request':continue
    r=m['request'];rid=r['requestId']
    if rid in seen:continue
    seen.add(rid);n=None if'Mulligan'in r['kind']else observe(rid)
    if n:write(folder/('request-'+str(rid)+'-wire.json'),m);write(folder/('request-'+str(rid)+'-observation.json'),n)
    requests.append(dict(id=rid,kind=r['kind'],seat=r['seat'],message=r['message']));write(folder/'requests.json',requests)
    try:
     if n and monitor(m,n):break
     pick=choose(m,n);check(pick in[o['id']for o in r['options']if not o.get('disabled')],('Original enabled native input',pick,r))
    except Exception:pick=pause(traceback.format_exc(),m,n);check(pick in[o['id']for o in r['options']if not o.get('disabled')],'Driver correction only submits original enabled response')
    write(folder/'state.json',state);write(folder/'paid.json',paid);response=dict(type='respond',session=case,seat=r['seat'],requestId=rid,option=pick);trace.write(json.dumps(response)+'\n');trace.flush();proc.stdin.write(json.dumps(response)+'\n');proc.stdin.flush();print(case,step,n['turn']if n else 0,n['phase']if n else'',r['kind'],pick,flush=True)
   else:raise AssertionError('Actual keyword request-step cap')
  status='COMPLETE_WITH_RAW_FAILURES'if failures else'PASS_BOUNDED'
 except Exception:status='FAIL';error=traceback.format_exc();print(error,flush=True)
 finally:
  if proc is not None and proc.poll()is None:proc.kill();proc.wait()
 result=dict(status=status,case=case,inputId=spec['inputId'],runtimeHash=runtime['runtimeHash'],folder=folder.relative_to(ROOT).as_posix(),checks=len(checks),assertions=checks,paid=paid,endpoints=endpoints,representatives=representatives,requests=requests,state=state,ids=ids,frozenSources=frozen,failures=failures,limitations=['Actual native two-human case only; opening positions bypass entering initial permanents.','Static Suspend payment uses exact actual land tap/resource delta and native counter/zone/zero-cast history; no artificial stack spell.','Read-only observer records native abilities/mana identities/counter/keyword state; no outcome/phase/counter mutation.','UI, localization and remote delivery are root-owned separately.'])
 if status=='FAIL':result['error']=error
 write(folder/'result.json',result);write(BASE/(case+'-latest.json'),result);print(status,case,folder,flush=True);return result
if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('case',choices=CASES);args=parser.parse_args();result=run(args.case);sys.exit(0 if result['status']=='PASS_BOUNDED'else 1)
