"""Real Karn exile/ultimate and same-game reset. Initial fixture only; no result writes."""
from pathlib import Path
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback,sys
R=Path(__file__).resolve().parents[2];RT=R/'ForgeRuntime';B=R/'.tools/restart-series-1011'
def wr(p,x):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n','utf8')
def ref(p):return dict(path=p.relative_to(R).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def cards(n):return [c for p in n['players'] for z in p['zones'].values() for c in z]
def find(n,name,owner,zone):return next(c for c in cards(n) if c['name']==name and c['owner']==owner and c['zone']==zone)
def run(case,staged=False):
 assert case in ['mixed','opposite','mutable']
 runtime=json.loads((RT/'runtime.json').read_text('utf-8-sig'));folder=B/'native'/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir()
 cklist=[];paid=[];p=None;initial=None;stage=0;selected=set();identity=None;exiled={};last_turn=0;copy_id=None;ultimate_rid=0;latest=None
 def ck(ok,label):
  cklist.append(label)
  if not ok:raise AssertionError(label)
 def observe(rid):
  end=time.monotonic()+8
  while time.monotonic()<end:
   try:return json.loads((audit/(str(rid)+'.json')).read_text('utf8'))
   except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
  raise AssertionError('Missing observation '+str(rid))
 def freeze(m,n,label):wr(folder/(label+'-wire.json'),m);wr(folder/(label+'-observation.json'),n)
 actor=2 if case=='opposite' else 1
 sourcefiles=[Path(__file__),*[R/'Tools/ForgeBridge/tests'/x for x in ['RoyalStateFixture.java','NamePredicateFixture.java','RestartStateFixture.java']],*[R/'Tools/ForgeBridge/java/mtgb/bridge'/x for x in ['ForgeBridge.java','BridgeLibraryKnowledge.java','BridgeRestartNotices.java']],RT/'runtime.json',RT/'assets/res/cardsfolder/k/karn_liberated.txt',RT/'assets/res/cardsfolder/k/kiki_jiki_mirror_breaker.txt',RT/'assets/res/cardsfolder/t/turn_to_frog.txt']
 frozen=[]
 for i,s in enumerate(sourcefiles):
  out=folder/'frozen'/str(i)/s.name;out.parent.mkdir(parents=True,exist_ok=True);out.write_bytes(s.read_bytes());frozen.append(dict(original=ref(s),frozen=ref(out)))
 wr(folder/'sources.json',frozen)
 try:
  ck(ref(RT/'files.json')['sha256']==runtime['runtimeHash'],'Exact published runtime manifest')
  guards=[x for x in json.loads((RT/'files.json').read_text('utf8'))['files'] if x['path'].startswith(('classes/mtgb/bridge/','classes/forge/game/GameAction','classes/forge/game/ability/effects/RestartGameEffect','classes/forge/game/phase/PhaseHandler'))]
  for x in guards:ck(ref(RT/x['path'])['sha256']==x['sha256'],'Published dependency '+x['path'])
  wr(folder/'runtime-preflight.json',[ref(RT/'files.json'),*[ref(RT/x['path']) for x in guards]])
  classes=folder/'classes';classes.mkdir()
  with (folder/'compile.log').open('w',encoding='utf8') as log:subprocess.run(['C:/Program Files/Java/jdk-17/bin/javac.exe','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(R/'Tools/ForgeBridge/tests'/x) for x in ['RoyalStateFixture.java','NamePredicateFixture.java','RestartStateFixture.java']]],check=True,stdout=log,stderr=subprocess.STDOUT,timeout=60)
  wr(folder/'fixture-classes.json',[ref(x) for x in sorted(classes.rglob('*.class'))])
  deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
  own=dict(Hand=(['Peek']+(['Turn to Frog'] if case=='mutable' else [])) if case!='opposite' else [],Battlefield=['Karn Liberated',*(['Kiki-Jiki, Mirror Breaker','Silvercoat Lion'] if case!='opposite' else []),*(['Island']*6)])
  other=dict(Hand=['Pacifism','Lightning Bolt'] if case!='opposite' else [],Battlefield=['Runeclaw Bear','Forest'])
  positions=[own,other] if actor==1 else [other,own]
  cfg=dict(session='restart-'+case,seed=4,startingLife=40,firstPlayerSeat=1,decks=[deck,deck],positions=positions,actor=actor,loyalty=14 if case=='opposite' else 23,auditDir=str(audit));wr(folder/'start.json',cfg)
  with tempfile.TemporaryDirectory(prefix='profile-',dir=folder,ignore_cleanup_errors=True) as tmp,(folder/'stderr.log').open('w',encoding='utf8') as err,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
   p=subprocess.Popen([str(RT/runtime['java']),'-Xmx512m','-XX:ActiveProcessorCount=3','-Djava.awt.headless=true','-Djava.io.tmpdir='+tmp,'-cp',';'.join(map(str,[classes,*([B/'bridge-classes'] if staged else []),RT/'classes',RT/'lib/*'])),'mtgb.bridge.RestartStateFixture',str(RT/'assets'),str(folder/'start.json')],cwd=RT/'assets',env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
   q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start();seen=set();deadline=time.monotonic()+420
   actions=(['peek','permanent','aura','spell']+(['frog'] if case=='mutable' else [])+['copy','restart']) if case!='opposite' else ['restart']
   for step in range(900):
    ck(time.monotonic()<deadline,'Bounded native deadline');m=q.get(timeout=45)
    if isinstance(m,Exception):raise m
    ck(m is not None,'Native output');trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type'] not in ['fatal','ended','rejected'],'Native envelope '+m['type'])
    if m['type']!='request':continue
    r=m['request'];rid=r['requestId']
    if rid in seen:continue
    seen.add(rid);opts=r['options'];enabled=[o['id'] for o in opts if not o.get('disabled')];kind=r['kind'];pick='ok'
    n=None if initial is None and 'Mulligan' in kind else observe(rid)
    if n:
     latest=n;freeze(m,n,'request-'+str(rid));ck(n['gameIdentity']==identity if identity else True,'Same native Game identity')
     if initial is None:
      initial=n;identity=n['gameIdentity'];karn=find(n,'Karn Liberated',actor,'Battlefield')['id'];ids={c['name']:c['id'] for c in cards(n) if c['name'] in ['Peek','Turn to Frog','Runeclaw Bear','Pacifism','Lightning Bolt','Silvercoat Lion','Kiki-Jiki, Mirror Breaker']};freeze(m,n,'initial')
     for a in n['stack']:
      key=(a['abilityId'],a['source'],a['api'])
      if not any(tuple(z['key'])==key for z in paid) and a['api'] in ['RestartGame','ChangeZone','CopyPermanent','RevealHand','Animate']:
       paid.append(dict(key=key,source=a['source'],api=a['api'],mana=a['payingMana'],requestId=rid,turn=n['turn']));wr(folder/'paid.json',paid);freeze(m,n,'paid-'+str(a['abilityId']))
     restarted=[e for e in n['events'] if e['event']=='restarted']
     if restarted and kind=='InputPassPriority' and n['setupCalls']>=2:
      freeze(m,n,'final');ck(len(restarted)==1 and restarted[0]['actor']==actor,'Exactly one real restart by activator')
      ck(n['turnSeat']==actor and n['turn']==1,'Activator starts new first turn');ck(n['setupCalls']==2,'Repeated opening hook skipped initial arrangement')
      ck(n['age']=='Play' and not n['stack'],'New opening completed and ordinary priority restored');ck(rid>ultimate_rid,'Fresh monotonically increasing request')
      ck(all(x['life']==40 and not x['counters'] and not x['blessing'] and x['ring']==0 and x['ringBearer']==0 for x in n['players']),'Life counters blessing and Ring reset for both players')
      ck(n['monarch']==0 and n['day']=='null','Monarch and day reset');ck(all(v in [0,'null','{}'] for v in n['delayed'].values()),'Old delayed triggers cleared')
      flat=cards(n);ck(not any(c.get('token') for c in flat),'Temporary copy ceased rather than reintroduced');ck(not any(c['zone']=='Command' for c in flat),'Old command effects absent')
      ck(not any(e['event']=='finished' for e in n['events']),'No scored Match finish on same-game restart')
      ck(any(z['api']=='RestartGame' for z in paid),'Actual ultimate committed with native loyalty payment')
      if case!='opposite':
       bear=next(c for c in flat if c['id']==ids['Runeclaw Bear']);ck(bear['zone']=='Battlefield' and bear['owner']==2 and bear['controller']==1,'Exact nonAura exiled original returns owned by opponent controlled by activator')
       ck(all(next(c for c in flat if c['id']==ids[name])['zone'] in ['Library','Hand'] for name in ['Pacifism','Lightning Bolt']),'Aura and instant return to shuffled owner zones')
       ck(copy_id is not None and not any(c['id']==copy_id for c in flat),'Actual Kiki copy ID no longer present')
       ck(len(exiled)==3,'Three actual Karn exile classes observed')
      if case=='mutable':
       lion=next(c for c in flat if c['id']==ids['Silvercoat Lion']);ck(lion['printedPower']==lion['power']==2 and lion['printedToughness']==lion['toughness']==2 and lion['colors']==lion['printedColors'],'Real ongoing animate characteristics reset on original object')
       ck(any(z['api']=='Animate' and len(z['mana'])==2 for z in paid),'Actual Turn to Frog paid two mana')
      for v in m['views']:
       ck(not v.get('stack'),'Both viewers see empty postrestart stack')
       ck(v.get('restartRevision')==1,'Actual native restart revision both recipients')
       ck(len(v['notices'])==1 and v['notices'][0]['kind']=='restart' and v['notices'][0]['actorSeat']==actor and v['notices'][0]['publicNotice'],'Actual public restart feedback both recipients')
       for pl in v['players']:ck(len(pl['hand'])==7 if pl['seat']==v['seat'] else not pl['hand'],'Opening hands actual viewer privacy')
      result='PASS_BOUNDED';break
    action=actions[stage] if stage<len(actions) else None
    if 'Mulligan' in kind:pick='keep' if 'keep' in enabled else 'ok'
    elif kind=='InputPassPriority':
     advanced=False
     if action in ['permanent','aura','spell'] and n and not n['stack']:
      name={'permanent':'Runeclaw Bear','aura':'Pacifism','spell':'Lightning Bolt'}[action]
      found=[c for c in cards(n) if c['name']==name and c['zone']=='Exile']
      if found:
       ck(found[0]['exiledWith']==karn,'ExiledWith exact Karn '+name);exiled[name]=found[0]['id'];last_turn=n['turn'];freeze(m,n,'after-'+action);stage+=1;advanced=True
     if action=='peek' and any(a['api']=='RevealHand' for a in paid) and not n['stack']:
      freeze(m,n,'after-peek');stage+=1;advanced=True
     if action=='copy' and not n['stack']:
      tokens=[c for c in cards(n) if c.get('token') and c['name']=='Silvercoat Lion']
      if tokens:copy_id=tokens[0]['id'];ck(any(isinstance(z,int) and z>0 for z in n['delayed'].values()),'Real copied token has native delayed sacrifice');freeze(m,n,'after-copy');stage+=1;advanced=True
     if action=='frog' and any(a['api']=='Animate' for a in paid) and not n['stack']:
      lion=next(c for c in cards(n) if c['id']==ids['Silvercoat Lion']);ck(lion['power']==lion['toughness']==1 and lion['colors']==2 and 'Frog' in lion['type'],'Actual changed base stats color type before restart');freeze(m,n,'after-frog');stage+=1;advanced=True
     if advanced:action=actions[stage] if stage<len(actions) else None
     if action and r['seat']==actor and n['turnSeat']==actor and n['phase'] in ['MAIN1','MAIN2'] and not n['stack']:
      if action in ['aura','spell','copy','frog'] and n['turn']<=last_turn:pick='ok'
      else:
       name='Peek' if action=='peek' else 'Turn to Frog' if action=='frog' else 'Kiki-Jiki, Mirror Breaker' if action=='copy' else 'Karn Liberated';source=find(n,name,actor,'Hand' if action in ['peek','frog'] else 'Battlefield')['id'];pick='card:'+str(source);ck(pick in enabled,'Actual source available '+action);selected.clear();freeze(m,n,'before-'+action)
    elif kind=='ChooseAbility' or (kind=='dialog' and r.get('message')=='getAbilityToPlay'):
     needle='Exile target permanent' if action=='permanent' else 'Target player exiles' if action in ['aura','spell'] else 'Restart the game'
     pick=next(o['id'] for o in opts if o['id'] in enabled and needle.lower() in o.get('label','').lower())
     if action=='restart':ultimate_rid=rid
    elif kind=='InputSelectTargets':
     target='player:'+str(2-actor) if action in ['peek','aura','spell'] else 'card:'+str(ids['Runeclaw Bear'] if action=='permanent' else ids['Silvercoat Lion'])
     pick='ok' if target in selected else target
     if target not in selected:selected.add(target)
    elif kind in ['SelectCards','InputSelectCardsFromList','InputSelectEntitiesFromList','GenericChoice']:
     if action in ['aura','spell']:
      name='Pacifism' if action=='aura' else 'Lightning Bolt';wanted=ids[name]
      option=next((o['id'] for o in opts if o['id'] in enabled and (o.get('card',{}).get('id')==wanted or name.lower() in o.get('label','').lower())),None)
      pick='confirm-cards' if 'confirm-cards' in enabled and wanted in selected else 'ok' if 'ok' in enabled and wanted in selected else option;selected.add(wanted)
     else:raise AssertionError(('Unexpected selection',action,kind,opts))
    elif 'PayMana' in kind or kind in ['InputAttack','InputBlock','InputConfirm','RevealCards']:pick='confirm-cards' if 'confirm-cards' in enabled else 'ok'
    else:raise AssertionError(('Unhandled request',action,kind,r.get('message'),opts))
    ck(pick in enabled,'Enabled response '+kind+' '+str(pick));reply=dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=rid,option=pick);trace.write(json.dumps(reply)+'\n');trace.flush();p.stdin.write(json.dumps(reply)+'\n');p.stdin.flush()
   else:raise AssertionError('Step limit')
 except Exception:result='FAIL';error=traceback.format_exc();print(error,flush=True)
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 out=dict(status=result,case=case,folder=folder.relative_to(R).as_posix(),checks=len(cklist),assertions=cklist,paid=paid,stage=stage,exiled=exiled,copyId=copy_id,runtimeHash=runtime['runtimeHash'],staged=staged,frozenSources=frozen)
 if result=='FAIL':out['error']=error
 wr(folder/'result.json',out);wr(B/(case+'-latest.json'),out);print(result,case,folder,flush=True);return out
if __name__=='__main__':
 for case in [x for x in sys.argv[1:] if x!='--staged']:
  if run(case,'--staged' in sys.argv)['status']!='PASS_BOUNDED':raise SystemExit(1)
