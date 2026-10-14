"""Actual Meld and split histories. No post-start state writes."""
from pathlib import Path
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback,sys,zipfile
R=Path(__file__).resolve().parents[2];B=R/'.tools/meld-tail-1011';RT=R/'ForgeRuntime'
CASE_FILE=R/'Tools/ForgeBridge/tests/MeldPayloadCases.json'
runtime=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
def wr(p,x):p.write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n','utf8')
def ref(p):return dict(path=p.relative_to(R).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def runtime_preflight():
 manifest=RT/'files.json';assert ref(manifest)['sha256']==runtime['runtimeHash']
 records=json.loads(manifest.read_text('utf8'))['files'];relevant=[x for x in records if x['path'].startswith(('classes/mtgb/bridge/ForgeBridge','classes/forge/game/GameAction'))]
 assert relevant and any(x['path']=='classes/forge/game/GameAction.class' for x in relevant)
 for x in relevant:assert ref(RT/x['path'])['sha256']==x['sha256']
 return [ref(manifest),*[ref(RT/x['path']) for x in relevant]]
def cards(n):return list({c['id']:c for p in n['players'] for z in p['zones'].values() for c in z}.values())
def find(n,name,owner=1,zone=None):return next(c for c in cards(n) if c['name']==name and c['owner']==owner and (zone is None or c['zone']==zone))
def run(case):
 plan=json.loads(CASE_FILE.read_text('utf8'))[case]
 primaryName=plan.get('primary','Hanweir Battlements');secondaryName=plan.get('secondary','Hanweir Garrison');meldName=plan.get('meldName','Hanweir, the Writhing Township')
 folder=B/'native'/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir();classes=folder/'classes';classes.mkdir()
 wr(folder/'runtime-preflight.json',runtime_preflight())
 fixtures=[R/'Tools/ForgeBridge/tests'/x for x in ['RoyalStateFixture.java','NamePredicateFixture.java','MeldPayloadFixture.java']]
 upstream=R/'.tools/forge-proof/upstream/forge-game/src/main/java/forge/game'
 sources=fixtures+[Path(__file__),CASE_FILE,RT/'runtime.json',R/'Tools/ForgeBridge/java/mtgb/bridge/ForgeBridge.java',upstream/'ability/effects/MeldEffect.java',upstream/'GameAction.java']+[RT/'assets/res/cardsfolder'/x for x in plan['scripts']]
 frozen=[]
 if plan.get('stagedOrderFix'):sources += [B/'fix/source/forge/game/GameAction.java',*sorted((B/'fix/classes').rglob('*.class'))]
 if plan.get('stagedChoiceFix'):sources += [B/'choice-fix/source/mtgb/bridge/ForgeBridge.java',*sorted((B/'choice-fix/classes').rglob('*.class'))]
 if plan.get('stagedComponentFix'):sources += [*sorted((B/'component-fix/classes').rglob('*.class'))]
 for i,p in enumerate(sources):
  out=folder/'frozen'/str(i)/p.name;out.parent.mkdir(parents=True,exist_ok=True);out.write_bytes(p.read_bytes());frozen.append(dict(original=ref(p),frozen=ref(out)))
 wr(folder/'sources.json',frozen)
 checks=[];violations=[];endpoints=[];payments=[];requests=[];inputs=[];p=None;index=0;stage='next';source=None;before=None;paid=False;initial=None;targets=[];completedPrize=False;selectedPartnerId=None
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
 def count(c,typ):return c['loyalty'] if typ.upper()=='LOYALTY' else next((x['amount'] for x in c['counters'] if x['type'].upper()==typ.upper()),0)
 def components(m,n):
  linked=next(c for c in cards(n) if c['meldedWith']);frontIds={linked['id'],linked['meldedWith']}
  for seatView in m['views']:
   visible=[c for player in seatView['players'] for c in player['battlefield']]
   primary=next(c for c in visible if c['id']==linked['id']);parts=primary.get('componentCards',[])
   require(len(parts)==2 and {c['id'] for c in parts}==frontIds,'Both seats receive exact original physical component IDs')
   require({c['name'] for c in parts}=={primaryName,secondaryName} and all(c['ownerSeat']==1 and c.get('printingKey') and c['stateName']=='Original' and not c['backFace'] and c['componentPreview'] and c['zone']=='MeldComponent' for c in parts),'Both seats receive printed fronts, owner, printing and read-only scope')
   require(not any(c['id']==linked['meldedWith'] for c in visible),'Hidden linked original is not a standalone Battlefield object')
 def endpoint(m,n,op):
  nonlocal stage,index,completedPrize,selectedPartnerId
  freeze(m,n,'after-'+str(index));ck(not n['stack'],'Actual empty stack at endpoint '+str(index));ck(m['request']['seat']==m['request']['playerSeat'],'Original actor restored '+str(index))
  if op['action']=='cast':ck(paid,'Actual cast and committed payment '+str(index))
  native=cards(n);old=cards(before)
  prim=next(c['id'] for c in cards(initial) if c['meldedWith']) if plan.get('initialMeld') else find(initial,primaryName,1)['id'];sec=next(c['meldedWith'] for c in cards(initial) if c['meldedWith']) if plan.get('initialMeld') else next((c['id'] for c in cards(initial) if c['name']==secondaryName),0)
  if selectedPartnerId is not None:sec=selectedPartnerId
  if op['result']=='meld':
   merged=next(c for c in native if c['id']==prim)
   ck(merged['name']==meldName and merged['state']=='Meld','Actual native Meld state and name')
   if plan.get('meldLoyalty') is not None:
    ck(merged['zone']=='Battlefield' and count(merged,'LOYALTY')==plan['meldLoyalty'],'Actual planeswalker enters with printed Meld loyalty')
    for v in m['views']:ck(next(c for p in v['players'] for c in p['battlefield'] if c['id']==prim)['loyalty']==plan['meldLoyalty'],'Both seats receive actual Meld loyalty')
   else:ck(merged['zone']=='Battlefield' and [merged['power'],merged['toughness']]==plan.get('meldPT',[7,4]),'Actual linked permanent has printed Meld body')
   ck(merged['meldedWith']==sec and merged['component']['id']==sec,'Two exact original physical component identities')
   ck(not any(c['id']==sec and c['zone']=='Battlefield' for c in native),'No phantom independent secondary Battlefield card')
   bfids=[c['id'] for c in m['views'][0]['players'][0]['battlefield']]
   ck(prim in bfids and sec not in bfids,'Production view contains primary only')
   if plan.get('checkComponents'):components(m,n)
   if op['action']=='attack':
    ck(merged['attacking'] and merged['defenderKind']=='player' and merged['defenderId']==m['views'][0]['players'][1]['targetId'],'New Meld object attacks exact original defender')
    ck(not any(c['id']==sec and c.get('attacking') for c in native),'Secondary has no independent combat assignment')
   if plan.get('partnerOrdinal') is not None:
    partners=[c for c in cards(initial) if c['name']=='Hanweir Garrison' and c['zone']=='Battlefield' and c['owner']==1]
    ck(sec==partners[plan['partnerOrdinal']]['id'],'Exact selected same-name partner melded')
    ck(all(any(c['id']==old['id'] and c['zone']=='Battlefield' and c['name']=='Hanweir Garrison' for c in native) for old in partners if old['id']!=sec),'Unselected same-name original remains a separate unchanged permanent')
  elif op['result']=='split':
   parts=[next(c for c in native if c['id']==ident) for ident in [prim,sec]]
   ck(all(c['zone']==op['zone'] and c['owner']==1 for c in parts),'Both distinct original cards in actual owner destination')
   ck({c['name'] for c in parts}=={primaryName,secondaryName},'Split restores original front names')
   ck(not any(c['id'] in [prim,sec] and c['zone']=='Battlefield' for c in native),'No stale melded Battlefield object')
   if plan.get('checkComponents'):
    ck(all(not c.get('componentCards') for v in m['views'] for p in v['players'] for zone in ['battlefield','graveyardCards','exileCards'] for c in p.get(zone,[])),'Split removes public linked preview in both seat snapshots')
   if op['zone']=='Library':
    library=n['players'][0]['zones']['Library'];top=plan.get('libraryTop',False);ck({c['id'] for c in (library[:2] if top else library[-2:])}=={prim,sec},'Both exact original parts at actual library '+('top' if top else 'bottom'))
    ck(any('order' in r['kind'].lower() or 'order' in r['message'].lower() for r in requests if r['operation']==(0 if plan.get('initialMeld') else 1)),'Actual owner chooses relative order of both library components')
    if plan.get('firstComponent'):ck(library[0 if top else -1]['id']==(prim if plan['firstComponent']=='primary' else sec),'Actual selected component is closest to library '+('top' if top else 'bottom'))
   if op.get('life') is not None:ck([p['life'] for p in n['players']]==op['life'],'Exactly one merged-creature death trigger and original actor life result')
   if plan.get('orderedGraveyard'):
    grave=n['players'][0]['zones']['Graveyard'];ids=[c['id'] for c in grave];ck(any(r['kind']=='OrderCards' for r in requests),'Actual native ordered-graveyard preference and owner choice gate')
    ck(ids.index(prim)<ids.index(sec) if plan['firstComponent']=='primary' else ids.index(sec)<ids.index(prim),'Selected first original is closer to graveyard bottom, unlike library indexing')
  elif op['result']=='clone':
   copied=next(c for c in native if c['id']==source)
   ck(paid and copied['name']==secondaryName and any(c['id']==source and c['name']=='Clone' and c['zone']=='Hand' for c in cards(before)) and not copied['token'],'Actual paid Clone copies real partner with original physical identity')
   selectedPartnerId=source
  elif op['result']=='no-meld':
   if op['action']=='activate':ck(paid,'Actual committed activation before negative Meld endpoint')
   else:ck(any(c['name']==secondaryName and c.get('attacking') for c in native),'Actual secondary remains attacking after insufficient exile aborts Meld')
   ck(not any(c['meldedWith'] or c['state']=='Meld' for c in native),'Invalid original partner does not create a linked Meld object')
   for name,zone in op.get('expectedZones',{}).items():ck(any(c['name']==name and c['zone']==zone for c in native),'Actual negative component zone '+name+zone)
   if plan.get('initialPartner') in ['missing','wrong-owner']:ck(any(c['id']==prim and c['zone']=='Battlefield' and c['name']==primaryName for c in native),'Missing or nonowned partner leaves primary unchanged')
   for v in m['views']:ck(all(not c.get('componentCards') for p in v['players'] for c in p['battlefield']),'No readonly linked preview produced by failed Meld')
  elif op['result']=='loyalty':
   merged=next(c for c in native if c['id']==prim)
   ck(paid and count(merged,'LOYALTY')==op['loyalty'] and n['players'][0]['life']==op['life'],'Actual paid loyalty activation changes counters and life')
   ck(merged['meldedWith']==sec and merged['state']=='Meld','Loyalty activation preserves exact linked originals')
   if plan.get('checkComponents'):components(m,n)
  else:raise AssertionError('Undefined meld endpoint')
  endpoints.append(dict(operation=index,result=op['result'],requestId=m['request']['requestId'],wire=ref(folder/('after-'+str(index)+'-wire.json')),observation=ref(folder/('after-'+str(index)+'-observation.json'))));wr(folder/'endpoints.json',endpoints)
  index+=1;stage='next'
 try:
  with (folder/'compile.log').open('w',encoding='utf8') as log:subprocess.run(['C:/Program Files/Java/jdk-17/bin/javac.exe','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*map(str,fixtures)],check=True,stdout=log,stderr=subprocess.STDOUT,timeout=60)
  wr(folder/'fixture-classes.json',[ref(x) for x in sorted(classes.rglob('*.class'))])
  with tempfile.TemporaryDirectory(prefix='profile-',dir=folder,ignore_cleanup_errors=True) as tmp,(folder/'stderr.log').open('w',encoding='utf8') as err,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
   deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
   cfg=dict(session='meld-'+case,seed=plan['seed'],startingLife=30,firstPlayerSeat=1,decks=[deck,deck],positions=plan['positions'],auditDir=str(audit))
   if plan.get('initialPartner'):cfg['initialPartner']=plan['initialPartner'];cfg['initialPartnerName']=secondaryName
   if plan.get('orderedGraveyard'):cfg['orderedGraveyard']=True
   if plan.get('initialMeld'):
    cfg['initialMeld']=True;cfg['initialMeldPrimary']=primaryName;cfg['initialMeldSecondary']=secondaryName
    if plan.get('meldLoyalty') is not None:cfg['initialMeldLoyalty']=plan['meldLoyalty']
   wr(folder/'start.json',cfg)
   p=subprocess.Popen([str(RT/runtime['java']),'-Xmx512m','-XX:ActiveProcessorCount=3','-Djava.awt.headless=true','-Djava.io.tmpdir='+tmp,'-cp',';'.join(map(str,[classes,*([B/'fix/classes'] if plan.get('stagedOrderFix') else []),*([B/'component-fix/classes'] if plan.get('stagedComponentFix') else []),*([B/'choice-fix/classes'] if plan.get('stagedChoiceFix') else []),RT/'classes',RT/'lib/*'])),'mtgb.bridge.MeldPayloadFixture',str(RT/'assets'),str(folder/'start.json')],cwd=RT/'assets',env=dict(os.environ,APPDATA=tmp,LOCALAPPDATA=tmp,TEMP=tmp,TMP=tmp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
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
    if stage=='resolve' and not paid and op['action'] in ['cast','activate']:
     a=next((x for x in n['stack'] if x['source']==source),None)
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
      if initial is None:
       initial=n;wr(folder/'initial.json',initial)
       if plan.get('checkComponents') and plan.get('initialMeld'):components(m,n)
      if op['action']=='wait':stage='wait'
      elif op['action']=='attack':source=find(n,primaryName,1,'Battlefield')['id'];stage='attack'
      else:source=find(n,op['source'],1,'Hand' if op['action']=='cast' else 'Battlefield')['id'];pick='card:'+str(source);stage='resolve'
    elif 'Mulligan' in kind:pick='keep' if 'keep' in enabled else 'ok'
    elif 'PayMana' in kind:pick='ok'
    elif kind=='InputSelectTargets':
     name=meldName;owner=1;ident=find(n,name,owner,'Battlefield')['id'];option='card:'+str(ident)
     pick='ok' if option in targets else option
     if pick!='ok':targets.append(pick)
    elif kind in ['one','InputSelectEntitiesFromList','InputSelectCardsFromList'] and plan.get('actualClone'):
     ident=selectedPartnerId if op['result']=='no-meld' else find(n,secondaryName,1,'Battlefield')['id'];selected=any(o.get('card',{}).get('id')==ident and o.get('selected') for o in opts)
     pick='ok' if selected and 'ok' in enabled else 'confirm-cards' if 'confirm-cards' in enabled else next(o['id'] for o in opts if o.get('card',{}).get('id')==ident and o['id'] in enabled)
    elif kind in ['one','InputSelectEntitiesFromList','InputSelectCardsFromList'] and plan.get('partnerOrdinal') is not None:
     partners=[c for c in cards(initial) if c['name']=='Hanweir Garrison' and c['zone']=='Battlefield' and c['owner']==1];selectedPartnerId=partners[plan['partnerOrdinal']]['id']
     selected=any(o.get('card',{}).get('id')==selectedPartnerId and o.get('selected') for o in opts)
     pick='ok' if selected and 'ok' in enabled else 'confirm-cards' if 'confirm-cards' in enabled else next(o['id'] for o in opts if o.get('card',{}).get('id')==selectedPartnerId and o['id'] in enabled)
    elif kind=='OrderCards':
     if plan.get('stagedChoiceFix'):
      candidates=[o['card'] for o in opts if o['id'].startswith('select:')]
      require(len(candidates)==2 and {c['name'] for c in candidates}=={'Hanweir Battlements','Hanweir Garrison'},'Exact two original front-face native ordering candidates')
      require(all(c.get('printingKey') and c.get('ownerSeat')==1 and not c.get('backFace') and c.get('stateName')=='Original' for c in candidates),'Both native components retain printing, owner and original front in ordering window')
     desired='Hanweir Battlements' if plan.get('firstComponent')=='primary' else 'Hanweir Garrison';unselected=[o for o in opts if o['id'] in enabled and o['id'].startswith('select:') and not o.get('selected')];pick='confirm-cards' if 'confirm-cards' in enabled else next((o['id'] for o in unselected if o.get('card',{}).get('name')==desired),unselected[0]['id'])
    elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in enabled else next(x for x in enabled if x.startswith('order:'))
    elif kind=='ChooseModes' and op.get('modes'):
     pick='confirm-modes' if 'confirm-modes' in enabled else next(o['id'] for o in opts if o['id'] in enabled and o['id'].startswith('mode:') and not o.get('selected') and any(word.lower() in o['label'].lower() for word in op['modes']))
    elif kind=='InputAttack' and stage=='attack':
     attackIds=[find(initial,name,1,'Battlefield')['id'] for name in [primaryName,secondaryName]]
     remaining=[ident for ident in attackIds if not any(c['id']==ident and c.get('attacking') for c in cards(n))]
     if remaining:pick=next(o['id'] for o in opts if o['id'] in enabled and o.get('attackerId')==remaining[0] and o.get('defenderKind')=='player')
     else:pick='ok';stage='resolve'
    elif kind in ['InputAttack','InputBlock','InputConfirm']:pick='ok'
    elif kind=='dialog':
     match=[o['id'] for o in opts if o['id'] in enabled and op.get('ability','').lower() in o['label'].lower()]
     require(bool(op.get('ability')) and len(match)==1,'Explicit original ability choice');pick=match[0]
    elif kind=='RevealCards':pick='confirm-cards'
    elif kind=='ConfirmAction' and plan.get('actualClone'):pick='yes'
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
