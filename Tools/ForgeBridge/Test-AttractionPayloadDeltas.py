"""Actual attraction opening/visit/prize histories. No post-start state writes."""
from pathlib import Path
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback,sys,zipfile
R=Path(__file__).resolve().parents[2];B=R/'.tools/payload-tail-1011';RT=R/'ForgeRuntime'
runtime=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
def wr(p,x):p.write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n','utf8')
def ref(p):return dict(path=p.relative_to(R).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def cards(n):return list({c['id']:c for p in n['players'] for z in p['zones'].values() for c in z}.values())
def find(n,name,owner=1,zone=None):return next(c for c in cards(n) if c['name']==name and c['owner']==owner and (zone is None or c['zone']==zone))
def run(case):
 plan=json.loads((B/'cases.json').read_text('utf8'))[case]
 folder=B/'native'/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir();classes=folder/'classes';classes.mkdir()
 fixtures=[R/'Tools/ForgeBridge/tests'/x for x in ['RoyalStateFixture.java','NamePredicateFixture.java','AttractionPayloadFixture.java']]
 upstream=R/'.tools/forge-proof/upstream/forge-game/src/main/java/forge/game'
 sources=fixtures+[Path(__file__),B/'cases.json',RT/'runtime.json',R/'Tools/ForgeBridge/java/mtgb/bridge/ForgeBridge.java']+[upstream/'ability/effects'/x for x in ['OpenAttractionEffect.java','ClaimThePrizeEffect.java','RollDiceEffect.java']]+[upstream/'trigger/TriggerClaimPrize.java',upstream/'card/CardFactoryUtil.java']+[RT/'assets/res/cardsfolder'/x for x in plan['scripts']]
 frozen=[]
 for i,p in enumerate(sources):
  out=folder/'frozen'/str(i)/p.name;out.parent.mkdir(parents=True,exist_ok=True);out.write_bytes(p.read_bytes());frozen.append(dict(original=ref(p),frozen=ref(out)))
 wr(folder/'sources.json',frozen)
 checks=[];violations=[];endpoints=[];payments=[];requests=[];inputs=[];p=None;index=0;stage='next';source=None;before=None;paid=False;initial=None;targets=[];completedPrize=False
 def ck(ok,label):
  checks.append(label)
  if not ok:violations.append(label)
 def require(ok,label):
  ck(ok,label)
  if not ok:raise AssertionError(label)
 def observe(rid):
  until=time.monotonic()+8
  while time.monotonic()<until:
   try:return json.loads((audit/(str(rid)+'.json')).read_text('utf8'))
   except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
  raise AssertionError('Missing original request observation '+str(rid))
 def freeze(m,n,label):wr(folder/(label+'-wire.json'),m);wr(folder/(label+'-observation.json'),n)
 def amount(n,name):return len([c for c in cards(n) if c['name'] in [name,name+' Token'] and c['zone']=='Battlefield'])
 def count(c,typ):return next((x['amount'] for x in c['counters'] if x['type'].upper()==typ.upper()),0)
 def endpoint(m,n,op):
  nonlocal stage,index,completedPrize
  freeze(m,n,'after-'+str(index));ck(not n['stack'],'Actual empty stack at endpoint '+str(index));ck(m['request']['seat']==m['request']['playerSeat'],'Original actor restored '+str(index))
  if op['action']=='cast':ck(paid,'Actual cast and committed payment '+str(index))
  native=cards(n);old=cards(before)
  if op['result']=='opened':
   olddeck=before['players'][0]['zones']['AttractionDeck'];newdeck=n['players'][0]['zones']['AttractionDeck'];opened=olddeck[:min(2,len(olddeck))]
   ck([c['id'] for c in newdeck]==[c['id'] for c in olddeck[len(opened):]],'Exact remaining auxiliary order')
   ck(all(next(c for c in native if c['id']==x['id'])['zone']=='Battlefield' for x in opened),'Actual top attraction objects enter Battlefield')
   ck(all(next(c for c in native if c['id']==x['id'])['variant']==x['variant'] and next(c for c in native if c['id']==x['id'])['lights']==x['lights'] for x in opened),'Original functional variants and lights preserved')
   ck(count(find(n,'The Most Dangerous Gamer'),'P1P1')==count(find(before,'The Most Dangerous Gamer'),'P1P1')+len(opened),'Actual Gamer receives exactly one counter per opened attraction')
  elif op['result']=='visit':
   beeble=find(before,'Pick-a-Beeble');now=next(c for c in native if c['id']==beeble['id']);delta=amount(n,'Treasure')-amount(before,'Treasure')
   if now['zone']=='Junkyard':
    completedPrize=True
    ck(delta==3,'Actual crossed threshold makes one normal and two prize Treasures')
    ck(find(n,'Grizzly Bears',2)['zone']=='Graveyard','Actual Gamer ClaimPrize target destroyed')
    ck(any(x['kind']=='InputSelectTargets' and x.get('sourceName')=='The Most Dangerous Gamer' and x.get('triggerCard')==beeble['id'] and x.get('triggerPlayer')==1 for x in requests),'Actual ClaimPrize preserves original Card and activating Player into Gamer target')
    olddeck=before['players'][0]['zones']['AttractionDeck'];newdeck=n['players'][0]['zones']['AttractionDeck']
    ck(len(newdeck)==max(0,len(olddeck)-1),'Prize opens exactly one replacement or empty no-op')
    if olddeck:ck(next(c for c in native if c['id']==olddeck[0]['id'])['zone']=='Battlefield','Prize replacement is exact prior top native object')
    ck(count(find(n,'The Most Dangerous Gamer'),'P1P1')==count(find(before,'The Most Dangerous Gamer'),'P1P1')+int(bool(olddeck)),'Opening after Prize queues exact Gamer counter once')
    ck(any(e['event']=='zone' and e['id']==beeble['id'] and e['zone']=='Battlefield' and e['mode']=='Removed' for e in n['events']),'Original prize source actually leaves Battlefield')
   else:
    ck(now['zone']=='Battlefield','Below-threshold/miss preserves attraction source')
    luckDelta=count(now,'Luck')-count(beeble,'Luck')
    ck(0<=count(now,'Luck')<6,'Actual visit remains below Prize threshold')
    ck(delta==int(luckDelta>0),'Each actual matching visit makes one normal Treasure; misses none')
    ck(find(n,'Grizzly Bears',2)['zone']=='Battlefield','Below threshold has no Gamer prize trigger')
   ck(next(c for c in native if c['id']==source)['zone']=='Battlefield','Actual original paid Line Cutter enters')
  elif op['result']=='natural':
   ck(n['turn']>=3 and m['views'][0]['turnSeat']==1,'Natural later own Main phase reached')
   ck(any(x.get('message','').find('visit')>=0 for x in inputs),'Actual natural/effect roll notification captured')
  else:raise AssertionError('Undefined endpoint '+op['result'])
  endpoints.append(dict(operation=index,result=op['result'],requestId=m['request']['requestId'],wire=ref(folder/('after-'+str(index)+'-wire.json')),observation=ref(folder/('after-'+str(index)+'-observation.json'))));wr(folder/'endpoints.json',endpoints)
  index+=1;stage='next'
 try:
  with (folder/'compile.log').open('w',encoding='utf8') as log:subprocess.run(['C:/Program Files/Java/jdk-17/bin/javac.exe','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*map(str,fixtures)],check=True,stdout=log,stderr=subprocess.STDOUT,timeout=60)
  wr(folder/'fixture-classes.json',[ref(x) for x in sorted(classes.rglob('*.class'))])
  with tempfile.TemporaryDirectory(prefix='profile-',dir=folder,ignore_cleanup_errors=True) as tmp,(folder/'stderr.log').open('w',encoding='utf8') as err,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
   deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
   cfg=dict(session='attraction-'+case,seed=plan['seed'],startingLife=30,firstPlayerSeat=1,decks=[deck,deck],positions=plan['positions'],Attractions=plan['Attractions'],auditDir=str(audit))
   if 'initialAttractionLuck' in plan:cfg['initialAttractionLuck']=plan['initialAttractionLuck']
   wr(folder/'start.json',cfg)
   p=subprocess.Popen([str(RT/runtime['java']),'-Xmx512m','-XX:ActiveProcessorCount=3','-Djava.awt.headless=true','-Djava.io.tmpdir='+tmp,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.AttractionPayloadFixture',str(RT/'assets'),str(folder/'start.json')],cwd=RT/'assets',env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
   q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start();seen=set();deadline=time.monotonic()+480
   for step in range(1200):
    require(time.monotonic()<deadline,'Native bounded deadline');m=q.get(timeout=45)
    if isinstance(m,Exception):raise m
    require(m is not None,'Native process has live output');trace.write(json.dumps(m)+'\n');trace.flush();require(m['type'] not in ['fatal','ended','rejected'],'Native envelope '+m['type'])
    if m['type']!='request':
     if m['type']=='notify':inputs.append(m)
     continue
    r=m['request'];rid=r['requestId']
    if rid in seen:continue
    seen.add(rid);kind=r['kind'];opts=r['options'];enabled=[o['id'] for o in opts if not o.get('disabled')];op=plan['operations'][index];pick='ok';n=None if 'Mulligan' in kind else observe(rid)
    info=dict(id=rid,kind=kind,operation=index,stage=stage,seat=r['seat'],message=r.get('message',''))
    if n:
     freeze(m,n,'request-'+str(rid));trigger=next((a for a in n['stack'] if a['name']=='The Most Dangerous Gamer' and a['trigger']),None)
     if trigger:info.update(sourceName=trigger['name'],triggerCard=trigger['triggering'].get('Card',{}).get('cardId'),triggerPlayer=trigger['triggering'].get('Player',{}).get('playerSeat'))
     pending=n.get('pendingAbility',{})
     if pending:info.update(sourceName=pending.get('sourceName'),triggerCard=pending.get('Card',{}).get('cardId'),triggerPlayer=pending.get('Player',{}).get('playerSeat'))
    requests.append(info);inputs.append(info)
    if stage=='resolve' and not paid:
     a=next((x for x in n['stack'] if x['source']==source and x['spell']),None)
     if a:
      ck(len(a['payingMana'])==op['mana'],'Original cast actual mana count '+str(index));payments.append(dict(operation=index,source=source,mana=len(a['payingMana']),objects=a['payingMana'],requestId=rid));wr(folder/'paid.json',payments);paid=True
    if kind=='InputPassPriority':
     if stage=='resolve' and not n['stack'] and r['seat']==1:
      endpoint(m,n,op)
      if completedPrize and plan.get('stopAtPrize'):break
      if index==len(plan['operations']):break
      op=plan['operations'][index]
     if stage=='wait' and not n['stack'] and r['seat']==1 and n['turn']>=3 and m['views'][0]['turnSeat']==1 and n['phase']=='MAIN2':
      endpoint(m,n,op)
      if index==len(plan['operations']):break
      op=plan['operations'][index]
     if stage=='next' and not n['stack'] and r['seat']==1 and m['views'][0]['turnSeat']==1 and n['phase']=='MAIN1':
      before=n;paid=False;targets=[];freeze(m,n,'before-'+str(index))
      if initial is None:initial=n;wr(folder/'initial.json',initial)
      if op['action']=='wait':stage='wait'
      else:source=find(n,op['source'],1,'Hand')['id'];pick='card:'+str(source);stage='resolve'
    elif 'Mulligan' in kind:pick='keep' if 'keep' in enabled else 'ok'
    elif 'PayMana' in kind:pick='ok'
    elif kind=='InputSelectTargets':
     name='Silvercoat Lion' if 'phased out' in r.get('message','') else 'Grizzly Bears';owner=1 if name=='Silvercoat Lion' else 2;ident=find(n,name,owner,'Battlefield')['id'];option='card:'+str(ident)
     pick='ok' if option in targets else option
     if pick!='ok':targets.append(pick)
    elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in enabled else next(x for x in enabled if x.startswith('order:'))
    elif kind in ['InputAttack','InputBlock','InputConfirm']:pick='ok'
    elif kind=='dialog':
     match=[o['id'] for o in opts if o['id'] in enabled and op.get('ability','').lower() in o['label'].lower()]
     require(bool(op.get('ability')) and len(match)==1,'Explicit original ability choice');pick=match[0]
    elif kind=='RevealCards':pick='confirm-cards'
    else:raise AssertionError(('Unhandled original native request',kind,r.get('message'),enabled))
    require(pick in enabled,'Enabled original response '+kind+' '+pick);reply=dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=rid,option=pick);trace.write(json.dumps(reply)+'\n');trace.flush();p.stdin.write(json.dumps(reply)+'\n');p.stdin.flush();print(case,index,kind,pick,flush=True)
   else:raise AssertionError('Bounded step limit')
  if plan.get('stopAtPrize'):ck(completedPrize,'Actual same-source threshold crossing completed')
  status='PASS_BOUNDED' if not violations else 'FAIL_BOUNDED_AUDIT_REQUIRED'
 except Exception:status='FAIL';error=traceback.format_exc();print(error,flush=True)
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 result=dict(status=status,case=case,folder=folder.relative_to(R).as_posix(),checks=len(checks),assertions=checks,violations=violations,endpoints=endpoints,paid=payments,requests=requests,inputs=inputs,runtimeHash=runtime['runtimeHash'],frozenSources=frozen)
 if status=='FAIL':result['error']=error
 wr(folder/'result.json',result);wr(B/(case+'-latest.json'),result);print(status,case,folder,flush=True);return result
if __name__=='__main__':
 for case in sys.argv[1:]:
  result=run(case)
  if result['status']!='PASS_BOUNDED':raise SystemExit(1)
