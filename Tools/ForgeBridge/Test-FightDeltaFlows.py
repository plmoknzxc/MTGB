"""Original real Fight producers; each attempt freezes its driver, case and observer."""
from pathlib import Path
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback,sys
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'.tools/balance-fight-clash-1011/fight';RT=ROOT/'ForgeRuntime'
runtime=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def ref(p):return dict(path=p.relative_to(ROOT).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def cards(n):return list({c['id']:c for p in n['players']for z in p['zones'].values()for c in z}.values())
def find(n,name,owner=1,zone=None):return next(c for c in cards(n)if name in [c['name'],c['printedName'],c['currentName']]and c['owner']==owner and(zone is None or c['zone']==zone))
def run(case):
 plan=json.loads((BASE/'cases.json').read_text('utf8'))[case];folder=BASE/'native'/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir();classes=folder/'classes';classes.mkdir()
 fixtures=[ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java',ROOT/'Tools/ForgeBridge/tests/FightDeltaFixture.java'];sources=fixtures+[Path(__file__),BASE/'cases.json',RT/'runtime.json']
 for p in sources:(folder/p.name).write_bytes(p.read_bytes())
 checks=[];paid=[];requests=[];endpoints=[];p=None;index=0;stage='next';before=None;source=None;targeted=[];numberSelected=False;paidSeen=False;choiceIds=[]
 def check(ok,msg):assert ok,msg;checks.append(msg)
 def observe(rid):
  end=time.monotonic()+6
  while time.monotonic()<end:
   try:return json.loads((audit/(str(rid)+'.json')).read_text('utf8'))
   except(FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
  raise AssertionError('Missing read-only pending observation '+str(rid))
 def freeze(m,n,label):write(folder/(label+'-wire.json'),m);write(folder/(label+'-observation.json'),n)
 def endpoint(m,n,op):
  nonlocal index,stage
  freeze(m,n,'after-'+str(index));check(not n['stack'],'Actual empty-stack next priority '+str(index));check(m['request']['seat']==m['request']['playerSeat'],'Exact next physical/logical operator '+str(index))
  for spec in op.get('expected',[]):
   c=next(c for c in cards(n)if c['id']==source)if spec['name']==op.get('source')else find(n,spec['name'],spec.get('owner',1),spec.get('zone'))
   for k,v in spec.items():
    if k not in ['name','owner']:check(c[k]==v,'Exact native '+str(index)+' '+spec['name']+' '+k)
  old={c['id']:c for c in cards(before)};now={c['id']:c for c in cards(n)}
  if op.get('action','cast')!='wait':check(paidSeen,'Actual paid original stack action '+str(index))
  eventdelta=n['events'][len(before['events']):];damage=[e for e in eventdelta if e['event']=='damage']
  if 'damage' in op:
   expected=[dict(source=find(before,a,sa)['id'],target=find(before,b,sb)['id'],amount=amount)for sa,a,sb,b,amount in op['damage']]
   check(sorted([{k:e[k]for k in ['source','target','amount']}for e in damage],key=lambda e:(e['source'],e['target'],e['amount']))==sorted(expected,key=lambda e:(e['source'],e['target'],e['amount'])),'Exact original source/recipient damage events '+str(index));check(all(e['phase'].startswith('MAIN')for e in damage),'Actual damage occurs outside combat '+str(index))
  if op.get('sameSelf'):
   c=now[source];check(c['zone']=='Graveyard','Real self-fighting original dies to double power');check(len(damage)==1 and damage[0]['source']==damage[0]['target']==source and damage[0]['amount']==8,'Reachable self fight produces exactly8 self damage')
  if op.get('draw'):
   top=before['players'][0]['zones']['Library'][0]['id'];check(now[top]['zone']=='Hand','Actual FightOnce draws exact original top '+str(index));check([c['id']for c in n['players'][0]['zones']['Library']]==[c['id']for c in before['players'][0]['zones']['Library']][1:],'Only one original library card drawn '+str(index))
  if op.get('noDraw'):check([c['id']for c in n['players'][0]['zones']['Library']]==[c['id']for c in before['players'][0]['zones']['Library']],'Decline has no FightOnce draw')
  endpoints.append(dict(operation=index,result=op['result'],requestId=m['request']['requestId']));write(folder/'endpoints.json',endpoints);index+=1;stage='next'
 try:
  compiler=Path(os.environ.get('MTGB_JAVAC',r'C:/Program Files/Java/jdk-17/bin/javac.exe'))
  with(folder/'compile.log').open('w',encoding='utf8')as log:subprocess.run([str(compiler),'-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*map(str,fixtures)],check=True,stdout=log,stderr=subprocess.STDOUT,timeout=60)
  with tempfile.TemporaryDirectory(prefix='profile-',dir=folder,ignore_cleanup_errors=True)as temp,(folder/'stderr.log').open('w',encoding='utf8')as err,(folder/'trace.jsonl').open('w',encoding='utf8')as trace:
   deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[]);cfg=dict(session='fight-'+case,seed=4,startingLife=40,firstPlayerSeat=1,decks=[deck,deck],positions=plan['positions'],auditDir=str(audit));write(folder/'start.json',cfg)
   p=subprocess.Popen([str(RT/runtime['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.FightDeltaFixture',str(RT/runtime['assets']),str(folder/'start.json')],cwd=RT/runtime['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
   q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start();seen=set();deadline=time.monotonic()+260
   for step in range(650):
    check(time.monotonic()<deadline,'Bounded native deadline');m=q.get(timeout=40)
    if isinstance(m,Exception):raise m
    check(m is not None,'Native process remains live');trace.write(json.dumps(m)+'\n');trace.flush();check(m['type']not in ['fatal','ended','rejected'],str(m))
    if m['type']!='request':continue
    r=m['request'];rid=r['requestId']
    if rid in seen:continue
    seen.add(rid);opts=r['options'];enabled=[o['id']for o in opts if not o.get('disabled')];kind=r['kind'];v=m['views'][0];n=None if 'Mulligan'in kind else observe(rid);pick='ok'
    requests.append(dict(id=rid,kind=kind,seat=r['seat'],playerSeat=r['playerSeat'],stage=stage,operation=index));op=plan['operations'][index]if index<len(plan['operations'])else None
    if n:freeze(m,n,'request-'+str(rid))
    if stage=='resolve'and not paidSeen and any(x['source']==source and not x['trigger']for x in n['stack']):
     prev={c['id']:c for c in cards(before)};used=[c for c in n['players'][0]['zones']['Battlefield']if c['name']in ['Plains','Island','Swamp','Mountain','Forest']and c['tapped']and not prev[c['id']]['tapped']]
     check(len(used)==op['mana']+n['players'][0]['pool']-before['players'][0]['pool'],'Actual committed original mana sources '+str(index));paid.append(dict(operation=index,sourceId=source,name=op['source'],mana=op['mana'],lands=[c['id']for c in used],requestId=rid));write(folder/'paid.json',paid);freeze(m,n,'paid-'+str(index));paidSeen=True
     if op.get('tap'):check(find(n,op['source'])['tapped'],'Actual native source tap cost')
    if kind=='InputPassPriority':
     if stage=='resolve'and not n['stack']:
      endpoint(m,n,op)
      if index==len(plan['operations']):break
      op=plan['operations'][index]
     if stage=='wait'and not n['stack']and v['turnSeat']==op['turnSeat']and v['phase']==op['phase']and n['turn']>=op.get('minTurn',0):
      endpoint(m,n,op)
      if index==len(plan['operations']):break
      op=plan['operations'][index]
     if stage=='next'and not n['stack']:
      if op.get('action')=='wait':before=n;stage='wait';freeze(m,n,'before-'+str(index))
      elif r['seat']==1 and v['turnSeat']==1 and v['phase']in ['MAIN1','MAIN2']:
       before=n;source=find(n,op['source'],1,'Battlefield'if op.get('action')=='activate'else'Hand')['id'];pick='card:'+str(source);stage='resolve';targeted=[];numberSelected=False;paidSeen=False;choiceIds=[];freeze(m,n,'before-'+str(index))
    elif 'Mulligan'in kind:pick='keep'if'keep'in enabled else'ok'
    elif kind in ['InputAttack','InputBlock']:pick='ok'
    elif 'PayMana'in kind:pick='cancel'if stage=='wait'else'ok'
    elif kind=='InputSelectTargets':
     todo=[(seat,name)for seat,name in op.get('targets',[])if find(n,name,seat)['id']not in targeted]
     if stage=='wait':pick=next(x for x in enabled if x.startswith('card:'))
     elif todo:
      seat,name=todo[0];id=find(n,name,seat)['id'];pick='card:'+str(id);targeted.append(id)
     else:pick='ok'
    elif kind=='InputConfirm':
     if 'fight' in (r.get('message','')+' '+r.get('title','')).lower()and 'fightAccept'in op:
      check(r['seat']==r['playerSeat']==1,'Original fighter controller receives optional Fight');pick='ok'if op['fightAccept']else'cancel';write(folder/('fight-confirm-'+str(index)+'.json'),m)
     elif stage=='wait':pick='cancel'
     else:pick='ok'
    elif kind in ['ChooseNumber','dialog']and r.get('message')!='getAbilityToPlay'and 'x'in op:
     if 'confirm-number'in enabled:pick='confirm-number'if r['selectedValue']==op['x']else('number-plus'if r['selectedValue']<op['x']else'number-minus')
     else:pick=next(o['id']for o in opts if o['id']in enabled and o['label']==str(op['x']))
    elif kind=='dialog':
     needle=op.get('abilityContains')
     pick=next((o['id']for o in opts if o['id']in enabled and needle and needle.lower()in o['label'].lower()),enabled[0])
    elif kind in ['SelectCards','InputSelectCardsFromList','InputSelectEntitiesFromList','ChooseEntity','SelectEntity']:
     check('play'in op,'Actual expected Mask hand selector');wanted=find(n,op['play'],1,'Hand')['id'];o=next((o for o in opts if o.get('card',{}).get('id')==wanted or o.get('id')=='card:'+str(wanted)),None)
     if o and o['id']not in choiceIds:pick=o['id'];choiceIds.append(pick)
     else:pick='confirm-cards'if'confirm-cards'in enabled else'ok'
    elif kind=='OrderAbilities':pick='confirm-order'if'confirm-order'in enabled else next(x for x in enabled if x.startswith('order:'))
    elif kind=='RevealCards':pick='confirm-cards'
    else:raise AssertionError(('Unhandled native Fight request',r))
    check(pick in enabled,('Exact enabled response',kind,pick,enabled));response=dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=rid,option=pick);trace.write(json.dumps(response)+'\n');trace.flush();p.stdin.write(json.dumps(response)+'\n');p.stdin.flush();print(case,step,index,kind,pick,flush=True)
   else:raise AssertionError('Request step bound')
   if p.poll()is None:p.kill();p.wait()
  status='PASS_BOUNDED'
 except Exception:status='FAIL';error=traceback.format_exc();print(error,flush=True)
 finally:
  if p is not None and p.poll()is None:p.kill();p.wait()
 result=dict(status=status,case=case,folder=folder.relative_to(ROOT).as_posix(),checks=len(checks),assertions=checks,paid=paid,endpoints=endpoints,requests=requests,runtimeHash=runtime['runtimeHash'],sources=[ref(x)for x in sources])
 if status=='FAIL':result['error']=error
 write(folder/'result.json',result);write(BASE/(case+'-latest.json'),result);print(status,case,folder,flush=True);return result
if __name__=='__main__':
 for case in sys.argv[1:]:run(case)
