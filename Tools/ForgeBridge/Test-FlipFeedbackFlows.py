"""Native casts/activations and seeded Random; no result mutations or direct effect invocation."""
from pathlib import Path
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback,sys
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'.tools/flip-feedback-1011/native';RT=ROOT/'ForgeRuntime'
runtime=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
PREFIX=None
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def ref(p):return dict(path=p.relative_to(ROOT).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def cards(n):return list({c['id']:c for p in n['players']for z in p['zones'].values()for c in z}.values())
def find(n,name,owner=1,zone=None):return next(c for c in cards(n)if c['name']==name and c['owner']==owner and(zone is None or c['zone']==zone))
def verify_feedback(case,wire,before,source):
 expected={'orb-real-token':['turned','hit'],'orb-noflip':['no-turn'],'star-miss':['turned','miss'],'empty-fixed':['empty']}[case]
 checks=[]
 def check(value,label):assert value,label;checks.append(label)
 notes=[v.get('notices',[]) for v in wire['views']]
 check(len(notes)==2 and all([n.get('flipOutcome') for n in seat]==expected for seat in notes),'Both actual recipients have exact flip outcomes')
 check(all(n.get('kind')=='flip' and n.get('publicNotice') and n.get('actorSeat')==1 and n.get('sourceCard',{}).get('id')==source for seat in notes for n in seat),'Public typed current caller/source identity')
 check([{k:v for k,v in n.items() if k!='id'} for n in notes[0]]==[{k:v for k,v in n.items() if k!='id'} for n in notes[1]],'Both recipients have identical public metadata')
 final=notes[0][-1];old={c['id']:c for c in cards(before)}
 check(final['flipCount']=={'orb-real-token':2,'orb-noflip':0,'star-miss':1,'empty-fixed':0}[case],'Actual turnover count matches original fixture branch')
 if case=='empty-fixed':check(not final.get('flipLocation') and not final['flipHits'],'No invented empty location or hit')
 else:
  chosen=find(before,'Air Elemental' if case=='star-miss' else 'Silvercoat Lion',2,'Battlefield')['id']
  check(final['flipLocation']['id']==chosen,'Exact original chosen location physical ID')
 if case=='orb-real-token':check({c['id'] for c in final['flipHits']}=={c['id'] for c in old.values() if c['owner']==2 and c['zone']=='Battlefield'},'Exact two physical hit IDs captured before child effect')
 else:check(not final['flipHits'],'No fabricated miss hits')
 return checks
def run(case):
 plan=json.loads((ROOT/'Tools/ForgeBridge/tests/FlipFeedbackCases.json').read_text('utf8'))[case];folder=BASE/'native'/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir();classes=folder/'classes';classes.mkdir()
 fixtures=[ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java',ROOT/'Tools/ForgeBridge/tests/FlipOntoDeltaFixture.java'];sources=fixtures+[Path(__file__),ROOT/'Tools/ForgeBridge/tests/FlipFeedbackCases.json',ROOT/'Tools/ForgeBridge/java/mtgb/bridge/ForgeBridge.java',RT/'runtime.json']
 patch=ROOT/'Tools/ForgeBridge/java/forge/game/ability/effects/FlipOntoBattlefieldEffect.java'
 sources.append(patch) # Existing published effect; no rule override compiled for this feedback increment.
 for p in sources:(folder/p.name).write_bytes(p.read_bytes())
 checks=[];paid=[];requests=[];p=None;stage='next';before=None;source=None;paidSeen=False;locationSeen=False;outcome=None
 def check(ok,msg):assert ok,msg;checks.append(msg)
 def observe(rid):
  end=time.monotonic()+6
  while time.monotonic()<end:
   try:return json.loads((audit/(str(rid)+'.json')).read_text('utf8'))
   except(FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
  raise AssertionError('Missing pending native snapshot '+str(rid))
 def freeze(m,n,label):write(folder/(label+'-wire.json'),m);write(folder/(label+'-observation.json'),n)
 try:
  with(folder/'compile.log').open('w',encoding='utf8')as log:subprocess.run([r'C:/Program Files/Java/jdk-17/bin/javac.exe','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*map(str,fixtures)],check=True,stdout=log,stderr=subprocess.STDOUT,timeout=60)
  with tempfile.TemporaryDirectory(prefix='profile-',dir=folder,ignore_cleanup_errors=True)as temp,(folder/'stderr.log').open('w',encoding='utf8')as err,(folder/'trace.jsonl').open('w',encoding='utf8')as trace:
   deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[]);cfg=dict(session='flip-'+case,seed=4,fixtureSeed=plan['seed'],startingLife=40,firstPlayerSeat=1,decks=[deck,deck],positions=plan['positions'],auditDir=str(audit));write(folder/'start.json',cfg)
   p=subprocess.Popen([str(RT/runtime['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,*([PREFIX] if PREFIX else []),RT/'classes',RT/'lib/*'])),'mtgb.bridge.FlipOntoDeltaFixture',str(RT/runtime['assets']),str(folder/'start.json')],cwd=RT/runtime['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
   q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start();seen=set();deadline=time.monotonic()+150
   for step in range(250):
    check(time.monotonic()<deadline,'Bounded deadline');m=q.get(timeout=35)
    if isinstance(m,Exception):raise m
    check(m is not None,'Native process live');trace.write(json.dumps(m)+'\n');trace.flush()
    if m['type']=='fatal'and plan.get('expectFatal'):
     check(paidSeen,'Real paid original cast before fatal');check('FlipOntoBattlefieldEffect.resolve' in (folder/'stderr.log').read_text('utf8'),'Actual empty getFirst diagnostics');outcome=dict(fatal=m);break
    check(m['type']not in ['fatal','ended','rejected'],str(m))
    if m['type']!='request':continue
    r=m['request'];rid=r['requestId']
    if rid in seen:continue
    seen.add(rid);opts=r['options'];enabled=[o['id']for o in opts if not o.get('disabled')];kind=r['kind'];v=m['views'][0];n=None if'Mulligan'in kind else observe(rid);pick='ok'
    requests.append(dict(id=rid,kind=kind,seat=r['seat'],playerSeat=r['playerSeat'],stage=stage))
    if n:freeze(m,n,'request-'+str(rid))
    if stage=='resolve'and not paidSeen:
     a=next((a for a in n['stack']if a['source']==source and not a['trigger']),None)
     if a:
      check(len(a['mana'])==plan['mana'],'Exact committed mana count');old={c['id']:c for c in cards(before)};check(all(x['source']in old for x in a['mana']),'Exact original payment source IDs');check(all(old[x['source']]['owner']==1 for x in a['mana']),'Payment original actor sources')
      paid.append(dict(sourceId=source,name=plan['source'],mana=plan['mana'],sources=a['mana'],requestId=rid));write(folder/'paid.json',paid);freeze(m,n,'paid');paidSeen=True
      if plan.get('action')=='activate':check(find(n,plan['source'],zone='Battlefield')['tapped'],'Native Orb tap payment')
    if kind=='InputPassPriority':
     if stage=='resolve'and not n['stack']:
      freeze(m,n,'after');check(paidSeen,'Paid action reaches full result');check(r['seat']==r['playerSeat'],'Next exact physical/logical actor');check(all(not x['remembered']for x in n['remembered']),'Current remembered cleanup (older frozen observer opening handles require source inference)');check([x['life']for x in n['players']]==[40,40],'No unintended life change');
      now={c['id']:c for c in cards(n)};old={c['id']:c for c in cards(before)};delta=n['events'][len(before['events']):];damage=[e for e in delta if e['event']=='damage'];expected=[]
      for owner,name,amount in plan.get('damage',[]):expected.append(dict(source=source,target=find(before,name,owner)['id'],amount=amount))
      check([{k:e[k]for k in ['source','target','amount']}for e in damage]==expected,'Exact original damage recipients and quantities')
      for owner,name,zone,tapped in plan.get('expected',[]):
       id=find(before,name,owner,'Battlefield')['id'];check(now[id]['zone']==zone,'Original '+name+' result zone');check(now[id]['tapped']==tapped,'Original '+name+' result tap')
      check(now[source]['zone']=='Graveyard','Original source final graveyard')
      if plan.get('location'):check(locationSeen,'Actual multi-location public selector')
      else:check(not locationSeen,'Mandatory zero/single location bypasses selector')
      checks.extend(verify_feedback(case,m,before,source))
      outcome=dict(requestId=rid,randomCalls=n['randomCalls'],abilities=n['abilities'],events=delta);write(folder/'outcome.json',outcome);break
     if stage=='next'and not n['stack']and r['seat']==1 and v['turnSeat']==1 and v['phase']in ['MAIN1','MAIN2']:
      before=n;source=find(n,plan['source'],1,'Battlefield'if plan.get('action')=='activate'else'Hand')['id'];freeze(m,n,'before');pick='card:'+str(source);stage='resolve'
    elif'Mulligan'in kind:pick='keep'if'keep'in enabled else'ok'
    elif kind in ['InputAttack','InputBlock']:pick='ok'
    elif'PayMana'in kind:pick='ok'
    elif kind=='InputConfirm':pick='cancel'if'Kor Firewalker'in r.get('message','')else'ok'
    elif kind=='dialog':
     if r.get('message')=='getAbilityToPlay':pick=enabled[0]
     else:pick=next((o['id']for o in opts if o['id']in enabled and('red'in o['label'].lower()or o['label']=='R')),enabled[0])
    elif kind in ['SelectCards','InputSelectCardsFromList','InputSelectEntitiesFromList','ChooseEntity','SelectEntity']:
     check(plan.get('location')is not None,'Planned multiple location request');check(r['seat']==r['playerSeat']==1,'Native flip source actor chooses location');check(r['minCount']==r['maxCount']==1,'Mandatory exactly one native location');check('cancel'not in enabled,'No voluntary native cancel');locationSeen=True
     owner,name=plan['location'];target=find(n,name,owner,'Battlefield')['id'];o=next(o for o in opts if o.get('card',{}).get('id')==target or o['id']=='card:'+str(target));pick='ok'if r['selectedCount']==1 else o['id'];write(folder/('location-selected-wire.json'if r['selectedCount']else'location-wire.json'),m)
    elif kind=='RevealCards':pick='confirm-cards'
    else:raise AssertionError(('Unhandled native flip request',r))
    check(pick in enabled,('Enabled original response',kind,pick,enabled));response=dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=rid,option=pick);trace.write(json.dumps(response)+'\n');trace.flush();p.stdin.write(json.dumps(response)+'\n');p.stdin.flush();print(case,step,kind,pick,flush=True)
   else:raise AssertionError('Request bound')
  status='PASS_BOUNDED_EMPTY_FATAL_REPRO'if plan.get('expectFatal')else'PASS_BOUNDED'
 except Exception:status='FAIL';error=traceback.format_exc();print(error,flush=True)
 finally:
  if p is not None and p.poll()is None:p.kill();p.wait()
 tested=classes/'forge/game/ability/effects/FlipOntoBattlefieldEffect.class'
 result=dict(status=status,case=case,folder=folder.relative_to(ROOT).as_posix(),checks=len(checks),assertions=checks,paid=paid,requests=requests,outcome=outcome,baseRuntimeHash=runtime['runtimeHash'],testedOverride=None,testedBridge=ref((PREFIX or RT/'classes')/'mtgb/bridge/ForgeBridge.class'),testedFlip=ref(RT/'classes/forge/game/ability/effects/FlipOntoBattlefieldEffect.class'),sources=[ref(x)for x in sources])
 if status=='FAIL':result['error']=error
 write(folder/'result.json',result);write(BASE/(case+'-latest.json'),result);print(status,case,folder,flush=True);return result
if __name__=='__main__':
 import argparse
 ap=argparse.ArgumentParser();ap.add_argument('--bridge-prefix',type=Path);ap.add_argument('cases',nargs='+');args=ap.parse_args();PREFIX=args.bridge_prefix.resolve() if args.bridge_prefix else None
 if PREFIX:assert (PREFIX/'mtgb/bridge/ForgeBridge.class').is_file()
 BASE.mkdir(parents=True,exist_ok=True)
 for case in args.cases:run(case)
