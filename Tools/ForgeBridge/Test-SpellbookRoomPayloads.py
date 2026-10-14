"""Original Giant Secrets random-book boundaries and Room effect payloads; real native operations."""
from pathlib import Path
import argparse,hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback,sys
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';BASE=ROOT/'.tools/layers-spellbook-reconcile-1011/digital-room'
ROOM="Dollmaker's Shop // Porcelain Gallery"
CASES={
 'spellbook-boundaries':{'inputIds':['makecard-random-spellbook-exhaustion'],'positions':[{'Hand':['Stormkeld Curator']*2,'Battlefield':['Island']*24,'Library':['Island']*10},{'Hand':[],'Battlefield':[]}]},
 'spellbook-overflow-tail':{'inputIds':['makecard-random-spellbook-exhaustion'],'positions':[{'Hand':['Stormkeld Curator'],'Battlefield':['Island']*20,'Library':['Island']*10},{'Hand':[],'Battlefield':[]}]},
 'rooms-loop':{'inputIds':['room-effect-face-selection'],'positions':[{'Hand':[ROOM,'Ghostly Dancers'],'Battlefield':['Plains']*24+[ROOM,'Marina Vendrell']+['Keys to the House']*4+['Ghostly Dancers']+['Ghostly Keybearer']*2,'Library':['Island']*10},{'Hand':[],'Battlefield':[]}]},
}
BOOK={'Aether Tunnel','All That Glitters','Celestial Mantle',"Curator's Ward",'Curiosity','Ethereal Armor','Face of Divinity','Knightly Valor','Metamorphic Alteration',"On Serra's Wings",'Rousing Read','Staggering Insight','Take Flight'}
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def ref(p):return {'path':p.relative_to(ROOT).as_posix(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
def cards(n):return list({c['id']:c for p in n['players']for z in p['zones'].values()for c in z}.values())
def exact(n,sid):return next(c for c in cards(n)if c['id']==sid)
def original(c):return c.get('originalName',c['name'])
def spirit_count(n):return sum(c['token']and c['name']=='Spirit Token'for c in n['players'][0]['zones']['Battlefield'])
def validate_terminal(case,state,endpoints,paid):
 count=7 if case=='rooms-loop'else 2 if case=='spellbook-boundaries'else 1
 if len(endpoints)!=count or len(paid)!=count:return False
 if any(e.get('source')!=p.get('source')for e,p in zip(endpoints,paid)):return False
 if case=='rooms-loop':
  expected=[['LeftSplit'],[],['RightSplit'],['LeftSplit','RightSplit'],['LeftSplit'],['RightSplit'],['LeftSplit','RightSplit']]
  return state.get('targetQueries')==2 and len(set(state.get('attackers',[])))==2 and 'combat-ended' in state['captured'] and all(e.get('unlocked')==u and e.get('spirits')==s for e,u,s in zip(endpoints,expected,[1,1,1,2,2,2,4])) and [p.get('mana')for p in paid]==[2,0,3,3,3,5,3]
 return (endpoints[-1].get('x')==14 and len(endpoints[-1].get('generated',[]))==14)and(case!='spellbook-boundaries'or endpoints[0].get('x')==0 and not endpoints[0].get('generated'))
def run(case,native_override=None):
 if native_override:native_override=native_override.resolve()
 BASE.mkdir(parents=True,exist_ok=True);folder=BASE/'native'/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir();classes=folder/'classes';classes.mkdir()
 runtime=json.loads((RT/'runtime.json').read_text('utf-8-sig'));spec=CASES[case];sources=[Path(__file__),ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java',ROOT/'Tools/ForgeBridge/tests/SpellbookRoomPayloadFixture.java',RT/'runtime.json'];frozen=[]
 for p in sources:
  dst=folder/p.name;dst.write_bytes(p.read_bytes());frozen.append(ref(dst))
 write(folder/'case-plan.json',spec);checks=[];failures=[];paid=[];endpoints=[];representatives=[];requests=[];proc=None;state={'active':None,'actions':0,'captured':[],'paidStackIds':[],'attackers':[],'targetQueries':0};ids={};deadline=time.monotonic()+900
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
  write(folder/(label+'-wire.json'),m);write(folder/(label+'-observation.json'),n);representatives.append({'label':label,'wire':ref(folder/(label+'-wire.json')),'observation':ref(folder/(label+'-observation.json'))})
 def once(m,n,label):
  if label not in state['captured']:freeze(m,n,label);state['captured'].append(label)
 def pause(error,m,n):
  record={'index':len(failures)+1,'error':error,'requestId':m['request']['requestId'],'state':state.copy(),'paid':len(paid),'endpoints':len(endpoints)};failures.append(record);write(folder/('raw-failure-'+str(len(failures))+'.json'),record);freeze(m,n,'pause-'+str(len(failures)));print('PAUSED',folder,error,flush=True)
  until=time.monotonic()+180
  while proc.poll()is None and time.monotonic()<until:
   p=folder/'control.json'
   if p.exists():
    try:data=json.loads(p.read_text('utf-8-sig'))
    except(PermissionError,json.JSONDecodeError):time.sleep(.1);continue
    p.replace(folder/('control-used-'+str(len(failures))+'.json'));state.update(data.get('state',{}));return data['option']
   time.sleep(.1)
  raise AssertionError('Bounded original input correction expired')
 def start(m,n,sid,name,kind='cast',**extra):
  state['active']={'source':sid,'name':name,'kind':kind,'beforeHand':[c['id']for c in n['players'][0]['zones']['Hand']],'beforeSpirits':spirit_count(n),**extra};once(m,n,'before-action-'+str(state['actions']));return'card:'+str(sid)
 def finish(m,n):
  a=state['active'];index=state['actions'];c=exact(n,a['source']);label='endpoint-'+str(index);endpoint={'action':a['kind'],'name':a['name'],'source':a['source'],'requestId':m['request']['requestId'],'label':label,'turn':n['turn'],'phase':n['phase']}
  if case.startswith('spellbook'):
   generated=[c for c in n['players'][0]['zones']['Hand']if c['id']not in a['beforeHand']];x=a['x']
   check(c['zone']=='Exile','Actual Adventure source resolves to Exile');check(len(generated)==x and len({c['id']for c in generated})==x,'Exact generated X card count and distinct original IDs');check(all(c['name']in BOOK and c['owner']==1 and c['controller']==1 and not c['token']for c in generated),'Actual spellbook members are non-token actor-owned hand cards')
   check(not m['views'][1]['players'][0]['hand'],'Conjured cards remain private from other seat')
   if x>13:check(len({c['name']for c in generated})<x,'Over-length random spellbook permits repeated names while preserving distinct IDs')
   endpoint.update(x=x,generated=[{'id':c['id'],'name':c['name']}for c in generated])
  else:
   expected=[['LeftSplit'],[],['RightSplit'],['LeftSplit','RightSplit'],['LeftSplit'],['RightSplit'],['LeftSplit','RightSplit']][index];target=ids['roomA']if index<=4 else ids['roomB'];room=exact(n,target)
   check(room['unlockedRooms']==expected,'Exact original target Room key state after action'+str(index));check(not room['remembered'],'Room consumer leaves no remembered residue')
   if index in[1,2,3,4,6]:
    face=['','LeftSplit','RightSplit','LeftSplit','RightSplit','','LeftSplit'][index];unlock=index in[2,3,6];check(any(e.get('event')=='door'and e['source']==target and e['state']==face and e['unlock']==unlock and e['seat']==1 for e in n['events']),'Actual native lock/unlock event has exact original source/face/actor')
   if index in[2,3,4,6]:check(c['zone']=='Graveyard','Actual Keys resource sacrifice moves exact source to graveyard')
   if index==1:check(c['tapped'],'Actual Marina resource tap retained')
   if index==5:check(c['zone']=='Battlefield'and len(ids['rooms'])==2,'Actual Dancers enters and chooses other original Room')
   spirits=spirit_count(n);delta=1 if index in[0,3]else 2 if index==6 else 0;check(spirits==a['beforeSpirits']+delta,'Exact enchantment-enter/full-unlock Spirit trigger result'+str(index));endpoint.update(target=target,unlocked=room['unlockedRooms'],spirits=spirits)
  check(not n['stack'],'Actual terminal empty native stack');once(m,n,label);endpoints.append(endpoint);state['actions']+=1;state['active']=None;write(folder/'endpoints.json',endpoints)
 def monitor(m,n):
  r=m['request'];kind=r['kind']
  if not ids:
   allc=cards(n);ids['curators']=[c['id']for c in allc if original(c)=='Stormkeld Curator'];rooms=[c for c in allc if c.get('room')];ids['rooms']=[c['id']for c in rooms]
   ids['roomA']=next((c['id']for c in rooms if c['zone']=='Hand'),0);ids['roomB']=next((c['id']for c in rooms if c['zone']=='Battlefield'),0)
   ids['marina']=next((c['id']for c in allc if c['name']=='Marina Vendrell'),0);ids['keys']=[c['id']for c in allc if c['name']=='Keys to the House'];ids['dancersHand']=next((c['id']for c in allc if c['name']=='Ghostly Dancers'and c['zone']=='Hand'),0);ids['keybearers']=[c['id']for c in allc if c['name']=='Ghostly Keybearer'];ids['initial']=[c['id']for c in allc]
  if n['turn']==1 and n['phase']=='MAIN1'and kind=='InputPassPriority'and not state.get('opening'):state['opening']=True;once(m,n,'opening-before-paid')
  for a in n['stack']:
   if not a['trigger']and not a['static']and a['stackId']not in state['paidStackIds']:
    state['paidStackIds'].append(a['stackId']);paid.append({'action':'spell'if a['spell']else'activation','name':a['name'],'source':a['source'],'actor':a['actor'],'cardState':a['cardState'],'mana':len(a['payingMana']),'objects':a['payingMana'],'requestId':r['requestId'],'stackId':a['stackId']});once(m,n,'paid-stack-'+str(a['stackId']))
  if state['active']and kind=='InputPassPriority'and not n['stack']:finish(m,n)
  expected=7 if case=='rooms-loop'else 2 if case=='spellbook-boundaries'else 1
  if state['actions']==expected:
   if case!='rooms-loop':return True
   if n['phase']=='COMBAT_DAMAGE':once(m,n,'damage-stage')
   if n['phase']=='MAIN2'and not n['stack']:
    check(state['targetQueries']==2 and len(state['attackers'])==2,'Actual Keybearer two combat triggers complete no-target and fully-unlocked empty-state branches');check(exact(n,ids['roomB'])['unlockedRooms']==['LeftSplit','RightSplit'],'Empty-state unlock target preserves fully unlocked exact Room');check(n['players'][1]['life']<100,'Actual unblocked Keybearer damage occurs');once(m,n,'combat-ended');return True
  return False
 def choose(m,n):
  r=m['request'];kind=r['kind'];opts=[o for o in r['options']if not o.get('disabled')];enabled=[o['id']for o in opts];a=state['active']
  if'Mulligan'in kind:return'keep'if'keep'in enabled else'ok'
  if kind=='InputPassPriority':
   if not a and not n['stack']and r['seat']==1 and n['turn']==1 and n['phase']=='MAIN1':
    i=state['actions']
    if case.startswith('spellbook') and i<(2 if case=='spellbook-boundaries'else 1):return start(m,n,ids['curators'][i],'Giant Secrets',x=0 if case=='spellbook-boundaries'and i==0 else 14)
    if case=='rooms-loop':
     if i==0:return start(m,n,ids['roomA'],ROOM,face='LeftSplit')
     if i==1:return start(m,n,ids['marina'],'Marina Vendrell','activation',target=ids['roomA'],face='LeftSplit')
     if i in[2,3,4,6]:return start(m,n,ids['keys'][{2:0,3:1,4:2,6:3}[i]],'Keys to the House','activation',target=ids['roomB']if i==6 else ids['roomA'],face='RightSplit'if i in[2,4]else'LeftSplit')
     if i==5:return start(m,n,ids['dancersHand'],'Ghostly Dancers',target=ids['roomB'],face='RightSplit')
   return'ok'
  if kind=='ChooseNumber':
   check(a and'x'in a,'Actual Giant Secrets X number source');x=a['x'];v=r['selectedValue'];return'number-plus-five'if x-v>=5 and'number-plus-five'in enabled else'number-plus'if v<x else'number-minus'if v>x else'confirm-number'
  if kind=='dialog':
   if r['message']=='getAbilityToPlay':
    if a['name']=='Giant Secrets':return next(o['id']for o in opts if'Giant Secrets'in o['label'])
    if a['name']=='Keys to the House':return next(o['id']for o in opts if'Lock or unlock'in o['label'])
    if a.get('face'):return next(o['id']for o in opts if("Dollmaker's Shop"if a['face']=='LeftSplit'else'Porcelain Gallery')in o['label'])
    return opts[0]['id']
   if a and a.get('face') and'Room'in r['message']:
    wanted="Dollmaker's Shop"if a['face']=='LeftSplit'else'Porcelain Gallery';once(m,n,'room-face-'+str(state['actions']));return next(o['id']for o in opts if wanted in o['label'])
   raise AssertionError(('Unknown exact digital/Room dialog',r,a))
  if kind=='ChooseOptions':
   if'confirm-cards'in enabled:return'confirm-cards'
   if a and a['name']=='Ghostly Dancers':once(m,n,'dancers-mode');return next(o['id']for o in opts if'Unlock a locked door'in o['label'])
   raise AssertionError(('Unknown exact ChooseOptions',r,a))
  if'PayMana'in kind:
   mana=next((o['id']for o in opts if o['id'].startswith('mana:')),None)
   if mana:return mana
   lands={c['id']for c in n['players'][r['seat']-1]['zones']['Battlefield']if c['name']in['Island','Plains']and not c['tapped']};return next(o['id']for o in opts if o['id'].startswith('card:')and int(o['id'].split(':')[1])in lands)
  if kind=='InputSelectTargets':
   if a:
    target='card:'+str(a['target']);return target if target in enabled and not next(o for o in opts if o['id']==target).get('selected')else'ok'
   check(case=='rooms-loop'and state['actions']==7,'Actual Keybearer target request after combat damage')
   if state['targetQueries']==0:once(m,n,'keybearer-no-target');check('ok'in enabled,'Native optional zero target confirm is enabled');state['targetQueries']+=1;return'ok'
   target='card:'+str(ids['roomB'])
   if target in enabled and not next(o for o in opts if o['id']==target).get('selected'):once(m,n,'keybearer-fully-unlocked-target');return target
   check('ok'in enabled,'Actual fully-unlocked Room target can confirm');state['targetQueries']+=1;return'ok'
  if kind.startswith('InputSelect')or kind in['SelectCards','ChooseEntity','SelectEntity']:
   if a and a['name']=='Ghostly Dancers':
    target='card:'+str(a['target']);once(m,n,'dancers-room-choice');return target if target in enabled and not next(o for o in opts if o['id']==target).get('selected')else'confirm-cards'if'confirm-cards'in enabled else'ok'
   raise AssertionError(('Unknown exact Room selection',r,a))
  if kind=='InputAttack':
   for sid in ids['keybearers']:
    option=next((o['id']for o in opts if o['id'].startswith('attack:'+str(sid)+':')),None)
    if option and sid not in state['attackers']:state['attackers'].append(sid);return option
   return'ok'
  if kind=='InputBlock':return'ok'
  if kind=='OrderAbilities':return'confirm-order'if'confirm-order'in enabled else next(o['id']for o in opts if o['id'].startswith('order:'))
  if kind=='RevealCards':return'confirm-cards'
  if kind=='InputConfirm':
   if a and a['name']=='Keys to the House':return'ok'
   raise AssertionError(('Unknown actual confirmation',r,a))
  raise AssertionError(('Unhandled digital/Room native input',r,a))
 try:
  compiler=Path(os.environ.get('MTGB_JAVAC',r'C:/Program Files/Java/jdk-17/bin/javac.exe'))
  with(folder/'compile.log').open('w',encoding='utf8')as log:subprocess.run([str(compiler),'-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*map(str,sources[1:3])],check=True,stdout=log,stderr=subprocess.STDOUT,timeout=60)
  frozen+=list(map(ref,sorted(classes.rglob('*.class'))));tested=[]
  if native_override:tested=list(map(ref,sorted(native_override.rglob('*.class'))));frozen+=tested;write(folder/'tested-native-overrides.json',tested)
  with tempfile.TemporaryDirectory(prefix='profile-',dir=folder,ignore_cleanup_errors=True)as profile,(folder/'stderr.log').open('w',encoding='utf8')as err,(folder/'trace.jsonl').open('w',encoding='utf8')as trace:
   deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]};config={'session':case,'seed':4,'startingLife':100,'firstPlayerSeat':1,'decks':[deck,deck],'positions':spec['positions'],'auditDir':str(audit)};write(folder/'start.json',config)
   cp=([native_override]if native_override else[])+[classes,RT/'classes',RT/'lib/*'];proc=subprocess.Popen([str(RT/runtime['java']),'-Xmx512m','-XX:ActiveProcessorCount=3','-Djava.awt.headless=true','-Djava.io.tmpdir='+profile,'-cp',';'.join(map(str,cp)),'mtgb.bridge.SpellbookRoomPayloadFixture',str(RT/runtime['assets']),str(folder/'start.json')],cwd=RT/runtime['assets'],env=dict(os.environ,APPDATA=profile,LOCALAPPDATA=profile,TEMP=profile,TMP=profile),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
   q=queue.Queue()
   def reader():
    try:
     for line in proc.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start();seen=set()
   for step in range(700):
    check(time.monotonic()<deadline,'Bounded actual original digital/Room flow');m=q.get(timeout=45);check(m is not None,'Native bridge remains live')
    if isinstance(m,Exception):raise m
    trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush();check(m['type']not in['fatal','ended','rejected'],str({k:v for k,v in m.items()if k!='views'}))
    if m['type']!='request':continue
    r=m['request'];rid=r['requestId']
    if rid in seen:continue
    seen.add(rid);n=None if'Mulligan'in r['kind']else observe(rid)
    if n:write(folder/('request-'+str(rid)+'-wire.json'),m);write(folder/('request-'+str(rid)+'-observation.json'),n)
    requests.append({'id':rid,'kind':r['kind'],'seat':r['seat'],'message':r['message']});write(folder/'requests.json',requests)
    try:
     if n and monitor(m,n):break
     pick=choose(m,n);check(pick in[o['id']for o in r['options']if not o.get('disabled')],'Actual original enabled native response')
    except Exception:pick=pause(traceback.format_exc(),m,n);check(pick in[o['id']for o in r['options']if not o.get('disabled')],'Correction uses original enabled native input')
    write(folder/'state.json',state);write(folder/'paid.json',paid);response={'type':'respond','session':case,'seat':r['seat'],'requestId':rid,'option':pick};trace.write(json.dumps(response)+'\n');trace.flush();proc.stdin.write(json.dumps(response)+'\n');proc.stdin.flush();print(case,step,n['turn']if n else 0,n['phase']if n else'',r['kind'],pick,flush=True)
   else:raise AssertionError('Native request step cap')
  check(validate_terminal(case,state,endpoints,paid),'All original planned digital/Room terminal endpoints complete')
  status='COMPLETE_WITH_RAW_FAILURES'if failures else'PASS_BOUNDED'
 except Exception:status='FAIL';error=traceback.format_exc();print(error,flush=True)
 finally:
  if proc is not None and proc.poll()is None:proc.kill();proc.wait()
 result={'status':status,'case':case,'inputIds':spec['inputIds'],'runtimeHash':runtime['runtimeHash'],'folder':folder.relative_to(ROOT).as_posix(),'checks':len(checks),'assertions':checks,'paid':paid,'endpoints':endpoints,'representatives':representatives,'requests':requests,'state':state,'ids':ids,'frozenSources':frozen,'failures':failures,'limitations':['Initial positioning only; no lock/RNG/outcome fields injected after setup.','Exact original selected consumers/minima only; conditional/generated/custom payloads not exhaustive.','UI and production publication remain root-owned; isolated overrides keep separate tested class identity.']}
 if status=='FAIL':result['error']=error
 write(folder/'result.json',result);write(BASE/(case+'-latest.json'),result);print(status,case,folder,flush=True);return result
if __name__=='__main__':
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('case',choices=CASES);parser.add_argument('--native-override',type=Path);args=parser.parse_args();result=run(args.case,args.native_override);sys.exit(0 if result['status']=='PASS_BOUNDED'else 1)
