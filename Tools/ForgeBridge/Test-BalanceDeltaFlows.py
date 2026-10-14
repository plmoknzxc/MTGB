"""Actual Balance/Act casts, strict per-owner choices, trigger and zone replacement results."""
from pathlib import Path
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback,sys
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'.tools/balance-fight-clash-1011/balance';RT=ROOT/'ForgeRuntime'
runtime=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def ref(p):return dict(path=p.relative_to(ROOT).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def cards(n):return list({c['id']:c for p in n['players']for z in p['zones'].values()for c in z}.values())
def find(n,name,seat=1,zone=None):return next(c for c in cards(n)if c['name']==name and c['owner']==seat and(zone is None or c['zone']==zone))
def run(case):
 plan=json.loads((BASE/'cases.json').read_text('utf8'))[case];folder=BASE/'native'/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir();classes=folder/'classes';classes.mkdir()
 fixtures=[ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java',ROOT/'Tools/ForgeBridge/tests/BalanceDeltaFixture.java'];sources=fixtures+[Path(__file__),BASE/'cases.json']
 for p in sources:(folder/p.name).write_bytes(p.read_bytes())
 checks=[];paid=[];selectors=[];blood=set();p=None;before=None;source=None;stage='start';sel=0;picked=set();effectFinished=False
 def check(ok,msg):assert ok,msg;checks.append(msg)
 def observe(rid):
  end=time.monotonic()+6
  while time.monotonic()<end:
   try:return json.loads((audit/(str(rid)+'.json')).read_text('utf8'))
   except(FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
  raise AssertionError('Missing native observation '+str(rid))
 def freeze(m,n,label):write(folder/(label+'-wire.json'),m);write(folder/(label+'-observation.json'),n)
 def finish(m,n):
  nonlocal effectFinished
  old={c['id']:c for c in cards(before)};now={c['id']:c for c in cards(n)}
  expected={find(before,name,x['seat'],x['zone'])['id']:plan['destination'] if x['seat']==1 else'Graveyard'for x in plan['choices']for name in x['names']}
  expected[source]=plan['destination'];check(sel==len(plan['choices']),'All and only excess selections completed')
  for cid,c in old.items():
   check(now[cid]['zone']==expected.get(cid,c['zone']),'Exact final original zone '+str(cid))
   check(now[cid]['owner']==c['owner']and now[cid]['controller']==c['controller'],'No Balance ownership or control changes '+str(cid))
   if cid not in expected:
    for key in ['power','toughness','damage','counters','timestamp']:check(now[cid][key]==c[key],'Unaffected original '+key+' '+str(cid))
  check([x['life']for x in n['players']]==plan['life'],'Exact actual death-trigger life totals')
  check(len(blood)==plan['bloodTriggers'],'Exact once-per-death triggers, absent under exile replacement')
  check(not n['stack'],'Empty stack and restored priority');check(m['request']['kind']=='InputPassPriority','Normal next input')
  freeze(m,n,'after');effectFinished=True
 try:
  compiler=Path(os.environ.get('MTGB_JAVAC',r'C:/Program Files/Java/jdk-17/bin/javac.exe'))
  with(folder/'compile.log').open('w',encoding='utf8')as log:subprocess.run([str(compiler),'-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*map(str,fixtures)],check=True,stdout=log,stderr=subprocess.STDOUT,timeout=60)
  with tempfile.TemporaryDirectory(prefix='profile-',dir=folder,ignore_cleanup_errors=True)as temp,(folder/'stderr.log').open('w',encoding='utf8')as err,(folder/'trace.jsonl').open('w',encoding='utf8')as trace:
   deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[]);cfg=dict(session='balance-'+case,seed=4,startingLife=30,firstPlayerSeat=1,decks=[deck,deck],positions=plan['positions'],auditDir=str(audit));write(folder/'start.json',cfg)
   p=subprocess.Popen([str(RT/runtime['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.BalanceDeltaFixture',str(RT/runtime['assets']),str(folder/'start.json')],cwd=RT/runtime['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
   q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start();seen=set();deadline=time.monotonic()+180
   for step in range(400):
    check(time.monotonic()<deadline,'Bounded request loop');m=q.get(timeout=40)
    if isinstance(m,Exception):raise m
    check(m is not None,'Live native process');trace.write(json.dumps(m)+'\n');trace.flush();check(m['type']not in ['fatal','ended','rejected'],str(m))
    if m['type']!='request':continue
    r=m['request'];rid=r['requestId']
    if rid in seen:continue
    seen.add(rid);opts=r['options'];enabled=[o['id']for o in opts if not o.get('disabled')];kind=r['kind'];v=m['views'][0];n=None if'Mulligan'in kind else observe(rid);pick='ok'
    if n is not None and stage!='start':
     for si in n['stack']:
      if si['sourceName']=='Blood Artist':blood.add(si['id']);check(sel==len(plan['choices']),'Death triggers queued only after full parent resolution')
    if kind=='InputPassPriority':
     if stage=='result'and not n['stack']:finish(m,n);break
     if stage=='start'and r['seat']==1 and v['phase']=='MAIN1'and not n['stack']:
      before=n;source=find(n,plan['cast'],1,'Hand')['id'];freeze(m,n,'before');pick='card:'+str(source);stage='cast'
    elif'Mulligan'in kind:pick='keep'if'keep'in enabled else'ok'
    elif kind in ['InputAttack','InputBlock']:pick='ok'
    elif'PayMana'in kind:pick='ok';stage='paying'
    elif kind in ['InputSelectCardsFromList','SelectCards','InputSelectEntitiesFromList']:
     check(sel<len(plan['choices']),'No chooser for zero excess');choice=plan['choices'][sel];check(r['seat']==r['playerSeat']==choice['seat'],'Exact owner performs own discard/sacrifice');check(r['minCount']==r['maxCount']==choice['count'],'Exact fixed excess count')
     freeze(m,n,'select-'+str(sel)+'-'+str(rid));desired=[find(before,name,choice['seat'],choice['zone'])['id']for name in choice['names']];todo=[x for x in desired if x not in picked]
     if todo:
      o=next(o for o in opts if o['id']in enabled and o.get('card',{}).get('id')==todo[0]);pick=o['id'];picked.add(todo[0])
     else:
      pick='confirm-cards'if'confirm-cards'in enabled else'ok';selectors.append(dict(seat=choice['seat'],zone=choice['zone'],ids=desired,count=choice['count'],requestId=rid));write(folder/'selectors.json',selectors);sel+=1;picked=set()
    elif kind=='InputSelectTargets':
     freeze(m,n,'trigger-target-'+str(rid));check(sel==len(plan['choices']),'No death target interrupts parent chain');check(r['seat']==r['playerSeat']==1,'Original dead Blood Artist controller chooses')
     pick='ok'if r.get('selectedCount')==1 and'ok'in enabled else next(o['id']for o in opts if o['id']in enabled and o.get('label')=='Player 2')
    elif kind=='OrderAbilities':pick='confirm-order'if'confirm-order'in enabled else next(x for x in enabled if x.startswith('order:'))
    elif kind=='RevealCards':pick='confirm-cards'
    else:raise AssertionError(('Unhandled actual request',r))
    if stage in ['cast','paying']and any(x['source']==source and x['spell']for x in n['stack']):
     a=before['players'][0];b=n['players'][0];previous={c['id']:c for c in a['zones']['Battlefield']};used=[c for c in b['zones']['Battlefield']if c['tapped']and not previous[c['id']]['tapped']];produced=sum(2 if c['name']=='Sol Ring'else 1 for c in used)
     check(all(c['name']in ['Plains','Island','Swamp','Mountain','Forest','Sol Ring']for c in used),'Only known real mana sources');check(produced==plan['mana']+b['pool']-a['pool'],'Actual committed cost');paid.append(dict(name=plan['cast'],id=source,mana=plan['mana'],produced=produced,sourceIds=[c['id']for c in used],requestId=rid));freeze(m,n,'paid');stage='result'
    check(pick in enabled,('Enabled native response',kind,pick,enabled));response=dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=rid,option=pick);trace.write(json.dumps(response)+'\n');trace.flush();p.stdin.write(json.dumps(response)+'\n');p.stdin.flush();print(case,step,kind,pick,flush=True)
   check(effectFinished,'Complete paid effect endpoint')
   if p.poll()is None:p.kill();p.wait()
  status='PASS_BOUNDED'
 except Exception:status='FAIL';error=traceback.format_exc();print(error,flush=True)
 finally:
  if p is not None and p.poll()is None:p.kill();p.wait()
 result=dict(status=status,case=case,folder=folder.relative_to(ROOT).as_posix(),checks=len(checks),paid=paid,selectors=selectors,bloodTriggers=sorted(blood),completedEndpoints=int(effectFinished),runtimeHash=runtime['runtimeHash'],sources=[ref(x)for x in sources])
 if status=='FAIL':result['error']=error
 write(folder/'result.json',result);write(BASE/(case+'-latest.json'),result);print(status,case,folder,flush=True);return result
if __name__=='__main__':
 for case in sys.argv[1:]:run(case)
