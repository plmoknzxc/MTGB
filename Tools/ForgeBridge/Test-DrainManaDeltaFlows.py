"""Real DrainMana producer deltas; setup is initial-only, all observations read native state."""
from pathlib import Path
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback,sys
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'.tools/dig-drain-flip-1011/drain';RT=ROOT/'ForgeRuntime'
runtime=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def ref(p):return dict(path=p.relative_to(ROOT).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def cards(n):return list({c['id']:c for p in n['players']for z in p['zones'].values()for c in z}.values())
def find(n,name,owner=1,zone=None,untapped=False):return next(c for c in cards(n)if c['name']==name and c['owner']==owner and(zone is None or c['zone']==zone)and(not untapped or not c['tapped']))
def mana(n,seat=1):return n['players'][seat-1]['mana']
def ids(ms):return {m['identity'] for m in ms}
def run(case):
 plan=json.loads((BASE/'cases.json').read_text('utf8'))[case];folder=BASE/'native'/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir();classes=folder/'classes';classes.mkdir()
 fixtures=[ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java',ROOT/'Tools/ForgeBridge/tests/DrainManaDeltaFixture.java'];sources=fixtures+[Path(__file__),BASE/'cases.json',RT/'runtime.json']
 for s in sources:(folder/s.name).write_bytes(s.read_bytes())
 checks=[];paid=[];requests=[];endpoints=[];p=None;index=0;stage='next';before=None;source=None;targeted=[];paidSeen=False;manual=[];attacked=False
 def check(ok,msg):assert ok,msg;checks.append(msg)
 def observe(rid):
  end=time.monotonic()+6
  while time.monotonic()<end:
   try:return json.loads((audit/(str(rid)+'.json')).read_text('utf8'))
   except(FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
  raise AssertionError('Missing pending read-only observation '+str(rid))
 def freeze(m,n,label):write(folder/(label+'-wire.json'),m);write(folder/(label+'-observation.json'),n)
 def endpoint(m,n,op):
  nonlocal index,stage
  freeze(m,n,'after-'+str(index));check(not n['stack'],'Real empty-stack endpoint '+str(index));check(m['request']['seat']==m['request']['playerSeat'],'Real restored physical/logical operator '+str(index))
  events=n['events'][len(before['events']):];result=op['result']
  if op.get('action','cast')=='cast':check(paidSeen,'Actual original committed cast '+str(index))
  if result=='same-object-transfer':
   produced=[x for c in before['players'][1]['zones']['Battlefield'] for x in find(n,c['name'],2,'Battlefield')['lastProduced']]
   check(len(produced)==3,'Three real opponent land-produced mana');check(not mana(n,2),'Source pool actually emptied');check(ids(mana(n))==ids(produced),'Transferred exact same Mana object identities')
   check(sorted(mana(n),key=lambda x:x['identity'])==sorted(produced,key=lambda x:x['identity']),'Transfer preserves every source/restriction/snow/player/ability field')
   check(sum(x['snow']for x in mana(n))==1 and sum(x['restricted']for x in mana(n))==1,'Exact one snow and one restricted transferred unit')
  elif result=='snow-transfer-spent':
   check(len(mana(before))==3 and len(mana(n))==2,'One transferred snow mana spent only');check(not any(x['snow']for x in mana(n)),'Original snow object is actually consumed')
   check(ids(mana(n))=={x['identity']for x in mana(before)if not x['snow']},'Other original mana objects remain');check(find(n,"Arcum's Astrolabe",1,'Battlefield'),'Actual Astrolabe enters')
   check(find(n,before['players'][0]['zones']['Library'][0]['name'],1,'Hand')['id']==before['players'][0]['zones']['Library'][0]['id'],'Exact original top drawn')
  elif result=='restricted-transfer-spent':
   check(len(mana(before))==2 and any(x['restricted']for x in mana(before)),'Restricted original mana available before legal creature payment');check(not mana(n),'Both original transferred objects spent');check(find(n,'Grizzly Bears',1,'Battlefield'),'Real creature enters under paying actor')
  elif result=='retained-native-production':check(any(x['persistent']for x in mana(n)),'Actual Birgi persistent mana produced')
  elif result=='float':check(len(mana(n,op['seat']))==len(mana(before,op['seat']))+1,'Real manual land activation adds one original mana')
  elif result=='combat-native-production':check(sum(x['combat']for x in mana(n))==6,'Actual Roku attack creates six combat-only mana')
  elif result=='clear-retained':
   check(any(x['combat']for x in mana(before))and any(x['persistent']for x in mana(before))and any(not x['combat']and not x['persistent']for x in mana(before)),'Ordinary/static, persistent and combat units genuinely coexist before clear')
   check(not mana(n),'Active drain clears all per-mana/static retained units');check(all(c['tapped']for c in n['players'][0]['zones']['Battlefield']if c['name']in ['Island','Mountain']),'Mana Short taps every own original land')
   check(any(e['event']=='manaPool'and e['player']==1 and e['mode']=='Cleared'for e in events),'Actual affected native mana-clear event')
  elif result=='clear-empty':
   check(not mana(before,2)and not mana(n,2),'Actual initially empty target pool remains empty')
   check(all(c['tapped']for c in n['players'][1]['zones']['Battlefield']if c['name']in ['Snow-Covered Island','Mountain']),'Mana Short still taps each original target land')
   check([x['life']for x in before['players']]==[x['life']for x in n['players']],'Empty drain preserves actual life totals')
  elif result=='clear-all-players':
   check(mana(before,1)and mana(before,2),'Both players genuinely hold retained mana before all-player source');check(not mana(n,1)and not mana(n,2),'Worldpurge actually empties both pools')
   check(not n['players'][0]['zones']['Battlefield']and not n['players'][1]['zones']['Battlefield'],'Worldpurge returns all original permanents');check({e['player']for e in events if e['event']=='manaPool'and e['mode']=='Cleared'}=={1,2},'Both affected native pool-clear events')
  elif result=='horizon-convert':
   old=mana(before,2);now=mana(n,2);check(len(old)==len(now)==2,'Horizon replacement retains exact quantity instead of draining')
   check(all(x['color']=='C'for x in now),'Colored unit converted to actual colorless');oldc=next(x for x in old if x['color']=='C');check(oldc in now,'Existing colorless original object retained')
   colored=next(x for x in old if x['color']!='C');new=next(x for x in now if x['source']==colored['source']);check(new['identity']!=colored['identity']and all(new[k]==colored[k]for k in colored if k not in ['identity','color']),'Conversion preserves source/snow/ability/player metadata with new converted Mana object')
  elif result=='hippo-cast':check(find(n,'Pygmy Hippo',1,'Battlefield'),'Real Hippo cast enters under original actor')
  elif result.startswith('hippo-')and result!='hippo-credit-spent':
   hippo=find(n,'Pygmy Hippo',1,'Battlefield');damage=[e for e in events if e['event']=='damage'and e['source']==hippo['id']]
   check(not hippo['remembered'],'Original Hippo remembered list cleaned');check(not any(t['source']==hippo['id']for t in n['delayed']),'Exactly one delayed trigger consumed or absent at main2')
   if result=='hippo-replaced-zero':check(not damage and len(mana(n,2))==5 and not mana(n,1),'Replacement loses zero, suppresses damage and credits zero C')
   elif result=='hippo-actual-ten':check(not damage and not mana(n,2)and len(mana(n,1))==10 and all(x['color']=='C'and x['source']==hippo['id']for x in mana(n)),'Actual ten drained units yield ten postcombat Hippo C units, no damage')
   else:check(len(damage)==1 and damage[0]['combat']and damage[0]['amount']==2,'Decline keeps exactly normal two combat damage');check(mana(n,2)==mana(before,2),'Decline does not drain existing defending pool');check(mana(n,1)==mana(before,1),'Decline creates no fresh delayed C')
  elif result=='hippo-credit-spent':check(len(mana(before))==10 and len(mana(n))==5,'Actual five delayed C units spent from ten original pool units');check(find(n,'Stone Golem',1,'Battlefield'),'Actual colorless-funded creature resolves')
  elif result=='replacement-source-removed':check(find(n,'Horizon Stone',2,'Graveyard'),'Real Disenchant removes replacement source')
  else:raise AssertionError(('Unknown endpoint meaning',result))
  endpoints.append(dict(operation=index,result=result,requestId=m['request']['requestId']));write(folder/'endpoints.json',endpoints);index+=1;stage='next'
 try:
  compiler=Path(os.environ.get('MTGB_JAVAC',r'C:/Program Files/Java/jdk-17/bin/javac.exe'))
  with(folder/'compile.log').open('w',encoding='utf8')as log:subprocess.run([str(compiler),'-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*map(str,fixtures)],check=True,stdout=log,stderr=subprocess.STDOUT,timeout=60)
  with tempfile.TemporaryDirectory(prefix='profile-',dir=folder,ignore_cleanup_errors=True)as temp,(folder/'stderr.log').open('w',encoding='utf8')as err,(folder/'trace.jsonl').open('w',encoding='utf8')as trace:
   deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[]);cfg=dict(session='drain-'+case,seed=4,startingLife=40,firstPlayerSeat=1,decks=[deck,deck],positions=plan['positions'],auditDir=str(audit));write(folder/'start.json',cfg)
   p=subprocess.Popen([str(RT/runtime['java']),'-Xmx512m','-XX:ActiveProcessorCount=3','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.DrainManaDeltaFixture',str(RT/runtime['assets']),str(folder/'start.json')],cwd=RT/runtime['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
   q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start();seen=set();deadline=time.monotonic()+420
   for step in range(1100):
    check(time.monotonic()<deadline,'Bounded native deadline');m=q.get(timeout=40)
    if isinstance(m,Exception):raise m
    check(m is not None,'Native process live');trace.write(json.dumps(m)+'\n');trace.flush();check(m['type']not in ['fatal','ended','rejected'],str(m))
    if m['type']!='request':continue
    r=m['request'];rid=r['requestId']
    if rid in seen:continue
    seen.add(rid);opts=r['options'];enabled=[o['id']for o in opts if not o.get('disabled')];kind=r['kind'];view=m['views'][0];n=None if 'Mulligan'in kind else observe(rid);pick='ok'
    requests.append(dict(id=rid,kind=kind,seat=r['seat'],playerSeat=r['playerSeat'],stage=stage,operation=index));op=plan['operations'][index]
    if n:freeze(m,n,'request-'+str(rid))
    if stage=='resolve'and not paidSeen and op.get('action','cast')=='cast':
     spell=next((x for x in n['stack']if x['source']==source and x['spell']),None)
     if spell:
      check(len(spell['payingMana'])==op['mana'],'Actual original committed mana objects '+str(index));check(spell['actor']==1,'Actual original paying actor');paid.append(dict(operation=index,name=op['source'],sourceId=source,mana=op['mana'],objects=spell['payingMana'],requestId=rid));write(folder/'paid.json',paid);freeze(m,n,'paid-'+str(index));paidSeen=True
    if kind=='InputPassPriority':
     if stage=='resolve'and not n['stack']:
      endpoint(m,n,op)
      if index==len(plan['operations']):break
      op=plan['operations'][index]
     if stage=='combat'and attacked and not n['stack']and view['turnSeat']==1 and view['phase']==op.get('endPhase','MAIN2'):
      endpoint(m,n,op)
      if index==len(plan['operations']):break
      op=plan['operations'][index]
     if stage=='next'and not n['stack']and view['turnSeat']==op.get('turnSeat',1)and n['turn']>=op.get('minTurn',1)and r['seat']==op.get('seat',1)and(view['phase']==op.get('phase','MAIN1')or op.get('anyCombat')and view['phase'].startswith('COMBAT')):
      before=n;source=find(n,op['source'],op.get('seat',1),'Battlefield'if op.get('action')in ['float','combat']else'Hand',op.get('action')=='float')['id'];targeted=[];paidSeen=False;attacked=False;manual=[];freeze(m,n,'before-'+str(index))
      if op.get('action')=='combat':stage='combat'
      else:
       pick='card:'+str(source);stage='resolve'
       for name in op.get('manualSources',[]):
        chosen=next(c for c in n['players'][0]['zones']['Battlefield']if c['name']==name and not c['tapped']and c['id']not in manual);manual.append(chosen['id'])
    elif 'Mulligan'in kind:pick='keep'if'keep'in enabled else'ok'
    elif kind=='InputAttack':
     if stage=='combat'and r['seat']==1 and n['turn']>=op.get('minTurn',1):
      token=next((x for x in enabled if x.startswith('attack:'+str(source)+':')),None)
      if not attacked:check(token is not None,'Actual original attacker offered');pick=token;attacked=True
     else:pick='ok'
    elif kind=='InputBlock':pick='ok'
    elif 'PayMana'in kind:
     remaining=[id for id in manual if not next(c for c in cards(n)if c['id']==id)['tapped']]
     pick='card:'+str(remaining[0])if remaining else'ok'
    elif kind=='InputSelectTargets':
     options=op.get('targets',[]);todo=[spec for spec in options if text_target(spec,n)not in targeted]
     if todo:pick=text_target(todo[0],n);targeted.append(pick)
     else:pick='ok'
    elif kind=='InputConfirm':
     if stage=='combat'and op.get('hippoAccept')is not None:
      check(r['seat']==r['playerSeat']==1,'Original Hippo controller receives optional decision');pick='ok'if op['hippoAccept']else'cancel'
     else:pick='ok'
    elif kind=='dialog':pick=enabled[0]
    elif kind=='ChooseColor':pick=next((o['id']for o in opts if o['id']in enabled and 'green'in o['label'].lower()),enabled[0])
    elif kind in ['SelectCards','InputSelectCardsFromList','InputSelectEntitiesFromList','ChooseEntity','SelectEntity']:
     check(op['source']=='Worldpurge','Expected original all-player choose-up-to-seven');pick='confirm-cards'if'confirm-cards'in enabled else'ok'
    elif kind=='OrderAbilities':pick='confirm-order'if'confirm-order'in enabled else next(x for x in enabled if x.startswith('order:'))
    elif kind=='RevealCards':pick='confirm-cards'
    else:raise AssertionError(('Unhandled native Drain request',r))
    check(pick in enabled,('Original enabled response',kind,pick,enabled));response=dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=rid,option=pick);trace.write(json.dumps(response)+'\n');trace.flush();p.stdin.write(json.dumps(response)+'\n');p.stdin.flush();print(case,index,kind,pick,flush=True)
   else:raise AssertionError('Native request-step bound')
  status='PASS_BOUNDED'
 except Exception:status='FAIL';error=traceback.format_exc();print(error,flush=True)
 finally:
  if p is not None and p.poll()is None:p.kill();p.wait()
 result=dict(status=status,case=case,folder=folder.relative_to(ROOT).as_posix(),checks=len(checks),assertions=checks,paid=paid,endpoints=endpoints,requests=requests,runtimeHash=runtime['runtimeHash'],sources=[ref(x)for x in sources])
 if status=='FAIL':result['error']=error
 write(folder/'result.json',result);write(BASE/(case+'-latest.json'),result);print(status,case,folder,flush=True);return result
def text_target(spec,n):
 if spec[0]=='player':return 'player:'+str(spec[1]-1)
 return 'card:'+str(find(n,spec[1],spec[0])['id'])
if __name__=='__main__':
 for case in sys.argv[1:]:run(case)
