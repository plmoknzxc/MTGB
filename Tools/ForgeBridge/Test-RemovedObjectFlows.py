"""Actual AWOL exile/cease flow and response changing its only target. No result writes."""
from pathlib import Path
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback,sys
R=Path(__file__).resolve().parents[2];RT=R/'ForgeRuntime';B=R/'.tools/removed-object-1011'
runtime=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
def wr(p,x):p.write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n','utf8')
def ref(p):return dict(path=p.relative_to(R).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def cards(n):return list({c['id']:c for p in n['players'] for z in p['zones'].values() for c in z}.values())
def find(n,name,owner,zone):return next(c for c in cards(n) if c['name']==name and c['owner']==owner and c['zone']==zone)
def run(case,staged=False):
 assert case in ['cease','target-leaves']
 folder=B/'native'/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir();classes=folder/'classes';classes.mkdir()
 files=RT/'files.json';assert ref(files)['sha256']==runtime['runtimeHash']
 guards=[x for x in json.loads(files.read_text('utf8'))['files'] if x['path'].startswith(('classes/mtgb/bridge/ForgeBridge','classes/forge/game/GameAction','classes/forge/game/ability/effects/RemoveFromGameEffect'))]
 if not staged:assert any(x['path']=='classes/forge/game/ability/effects/RemoveFromGameEffect.class' for x in guards),'Published current-object override required'
 for x in guards:assert ref(RT/x['path'])['sha256']==x['sha256']
 wr(folder/'runtime-preflight.json',[ref(files),*[ref(RT/x['path']) for x in guards]])
 fixtures=[R/'Tools/ForgeBridge/tests'/x for x in ['RoyalStateFixture.java','NamePredicateFixture.java','MeldPayloadFixture.java','RemovedObjectFixture.java']]
 sourcefiles=[Path(__file__),*fixtures,R/'Tools/ForgeBridge/java/mtgb/bridge/ForgeBridge.java',RT/'runtime.json',RT/'assets/res/cardsfolder/a/awol.txt',RT/'assets/res/cardsfolder/u/unsummon.txt',R/'.tools/forge-proof/upstream/forge-game/src/main/java/forge/game/ability/effects/RemoveFromGameEffect.java']
 frozen=[]
 for i,p in enumerate(sourcefiles):
  out=folder/'frozen'/str(i)/p.name;out.parent.mkdir(parents=True,exist_ok=True);out.write_bytes(p.read_bytes());frozen.append(dict(original=ref(p),frozen=ref(out)))
 if staged:
  for p in [B/'fix/source/forge/game/ability/effects/RemoveFromGameEffect.java',*sorted((B/'fix/classes').rglob('*.class'))]:
   out=folder/'frozen'/str(len(frozen))/p.name;out.parent.mkdir(parents=True,exist_ok=True);out.write_bytes(p.read_bytes());frozen.append(dict(original=ref(p),frozen=ref(out)))
 wr(folder/'sources.json',frozen)
 checks=[];paid=[];p=None;stage='attack';target=source=response=None;selected=set();responded=False;before=None;result=None
 def ck(ok,label):
  checks.append(label)
  if not ok:raise AssertionError(label)
 def observe(rid):
  until=time.monotonic()+8
  while time.monotonic()<until:
   try:return json.loads((audit/(str(rid)+'.json')).read_text('utf8'))
   except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
  raise AssertionError('Missing observed original request '+str(rid))
 def freeze(m,n,label):wr(folder/(label+'-wire.json'),m);wr(folder/(label+'-observation.json'),n)
 try:
  with (folder/'compile.log').open('w',encoding='utf8') as log:subprocess.run(['C:/Program Files/Java/jdk-17/bin/javac.exe','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*map(str,fixtures)],check=True,stdout=log,stderr=subprocess.STDOUT,timeout=60)
  wr(folder/'fixture-classes.json',[ref(x) for x in sorted(classes.rglob('*.class'))])
  with tempfile.TemporaryDirectory(prefix='profile-',dir=folder,ignore_cleanup_errors=True) as tmp,(folder/'stderr.log').open('w',encoding='utf8') as err,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
   deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
   positions=[dict(Hand=['Unsummon'] if case=='target-leaves' else [],Battlefield=['Grizzly Bears','Elvish Mystic',*(['Island']*4)]),dict(Hand=['AWOL'],Battlefield=['Plains']*6)]
   cfg=dict(session='remove-'+case,seed=4,startingLife=30,firstPlayerSeat=1,decks=[deck,deck],positions=positions,auditDir=str(audit));wr(folder/'start.json',cfg)
   p=subprocess.Popen([str(RT/runtime['java']),'-Xmx512m','-XX:ActiveProcessorCount=3','-Djava.awt.headless=true','-Djava.io.tmpdir='+tmp,'-cp',';'.join(map(str,[classes,*([B/'fix/classes'] if staged else []),RT/'classes',RT/'lib/*'])),'mtgb.bridge.RemovedObjectFixture',str(RT/'assets'),str(folder/'start.json')],cwd=RT/'assets',env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
   q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start();seen=set();deadline=time.monotonic()+360
   for step in range(600):
    ck(time.monotonic()<deadline,'Bounded native deadline');m=q.get(timeout=45)
    if isinstance(m,Exception):raise m
    ck(m is not None,'Native process output');trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type'] not in ['fatal','ended','rejected'],'Native envelope '+m['type'])
    if m['type']!='request':continue
    r=m['request'];rid=r['requestId']
    if rid in seen:continue
    seen.add(rid);kind=r['kind'];opts=r['options'];enabled=[o['id'] for o in opts if not o.get('disabled')];n=None if 'Mulligan' in kind else observe(rid);pick='ok'
    if n:
     freeze(m,n,'request-'+str(rid))
     if before is None:
      before=n;target=find(n,'Grizzly Bears',1,'Battlefield')['id'];source=find(n,'AWOL',2,'Hand')['id'];response=find(n,'Unsummon',1,'Hand')['id'] if case=='target-leaves' else None;freeze(m,n,'initial')
     for a in n['stack']:
      if a['source'] in [source,response] and a['spell'] and not any(x['source']==a['source'] for x in paid):
       expected=3 if a['source']==source else 1;ck(len(a['payingMana'])==expected,'Original spell committed mana '+str(a['source']));paid.append(dict(source=a['source'],mana=expected,objects=a['payingMana'],requestId=rid));wr(folder/'paid.json',paid);freeze(m,n,'paid-'+str(a['source']))
    if 'Mulligan' in kind:pick='keep' if 'keep' in enabled else 'ok'
    elif kind=='InputAttack':
     if stage=='attack':
      c=next(x for x in cards(n) if x['id']==target)
      if not c['attacking']:pick=next(o['id'] for o in opts if o['id'] in enabled and o.get('attackerId')==target and o.get('defenderKind')=='player')
      else:stage='cast';pick='ok'
     else:pick='ok'
    elif kind=='InputPassPriority':
     if stage=='cast' and r['seat']==2 and any(c['id']==target and c['attacking'] for c in cards(n)):
      ck('card:'+str(source) in enabled,'AWOL enabled through actual ordinary Constructed combat');pick='card:'+str(source);stage='resolve'
     elif stage=='resolve' and case=='target-leaves' and r['seat']==1 and not responded and any(a['source']==source for a in n['stack']):
      pick='card:'+str(response);responded=True;selected.clear();stage='response'
     elif stage in ['resolve','response'] and not n['stack']:
      freeze(m,n,'final');flat=cards(n);c=next(c for c in n['tracked'] if c['id']==target)
      ck(any(c['id']==source and c['zone']=='Graveyard' for c in flat),'Original AWOL finishes in caster graveyard');ck(len(paid)==(1 if case=='cease' else 2),'All actual spell payments captured')
      if case=='cease':
       ck(not c['present'],'Exact original attacker no longer exists in native game');ck(not any(c['id']==target for c in flat),'Exact target absent from every native player zone');ck(not any(x['id']==target and x['attacking'] for x in flat),'No current removed original in combat')
       ze=[e for e in n['events'] if e.get('event')=='zone' and e.get('id')==target];ck(any(e['zone']=='Exile' and e['mode']=='Added' for e in ze),'Original target actually entered Exile before removal');ck(any(e['zone']=='Exile' and e['mode']=='Removed' for e in ze),'Original target actually removed from Exile')
      else:
       ck(c['zone']=='Hand' and any(x['id']==target and x['owner']==1 and x['zone']=='Hand' for x in flat),'Response returns exact target to original owner hand');ck(not any(e.get('event')=='zone' and e.get('id')==target and e.get('zone')=='Exile' for e in n['events']),'Illegal old target is neither exiled nor ceased');ck(any(x['id']==response and x['zone']=='Graveyard' for x in flat),'Original paid response resolves normally')
      other=find(before,'Elvish Mystic',1,'Battlefield');ck(any(x['id']==other['id'] and x['timestamp']==other['timestamp'] and x['zone']=='Battlefield' for x in flat),'Nonattacking permanent unchanged');ck([x['life'] for x in n['players']]==[30,30],'No phantom combat damage or life loss');ck(r['seat'] in [1,2] and r['seat']==r['playerSeat'],'Next original priority restored')
      for v in m['views']:
       public=[c for pl in v['players'] for z in ['battlefield','graveyardCards','exileCards'] for c in pl[z]];ck(not any(c['id']==target for c in public),'Both seats omit removed/returned target from public zones');ck(not v.get('stack'),'Both seats have empty resolved stack')
      result='PASS_BOUNDED';break
    elif kind=='InputSelectTargets':
     option='card:'+str(target)
     if option in selected:pick='ok'
     else:
      ck(option in enabled,'Exact attacking target enabled');pick=option;selected.add(option)
      if stage=='resolve':ck(not any(o.get('card',{}).get('id')==find(before,'Elvish Mystic',1,'Battlefield')['id'] for o in opts if o['id'] in enabled),'Nonattacking creature is not a legal AWOL target');freeze(m,n,'target-selection')
    elif 'PayMana' in kind or kind in ['InputBlock','InputConfirm']:pick='ok'
    else:raise AssertionError(('Unhandled actual native request',kind,r.get('message'),enabled))
    ck(pick in enabled,'Enabled original response '+kind+' '+pick);reply=dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=rid,option=pick);trace.write(json.dumps(reply)+'\n');trace.flush();p.stdin.write(json.dumps(reply)+'\n');p.stdin.flush()
   else:raise AssertionError('Native step limit')
 except Exception:result='FAIL';error=traceback.format_exc();print(error,flush=True)
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 out=dict(status=result,case=case,folder=folder.relative_to(R).as_posix(),checks=len(checks),assertions=checks,paid=paid,runtimeHash=runtime['runtimeHash'],staged=staged,frozenSources=frozen)
 if result=='FAIL':out['error']=error
 wr(folder/'result.json',out);wr(B/(case+'-latest.json'),out);print(result,case,folder,flush=True);return out
if __name__=='__main__':
 for case in [x for x in sys.argv[1:] if x!='--staged']:
  if run(case,'--staged' in sys.argv)['status']!='PASS_BOUNDED':raise SystemExit(1)
