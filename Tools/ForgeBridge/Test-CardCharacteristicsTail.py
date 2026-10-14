"""Actual characteristic tails; setup initial-only, native observations read-only."""
from pathlib import Path
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback,sys
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'.tools/characteristics-keywords-payments-1011/characteristics';RT=ROOT/'ForgeRuntime'
runtime=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def ref(p):return dict(path=p.relative_to(ROOT).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def cards(n):return list({c['id']:c for p in n['players']for z in p['zones'].values()for c in z}.values())
def find(n,name,owner=1,zone=None,untapped=False):return next(c for c in cards(n)if c['name']==name and c['owner']==owner and(zone is None or c['zone']==zone)and(not untapped or not c['tapped']))
def mana(n,seat=1):return n['players'][seat-1]['mana']
def ids(ms):return {m['identity'] for m in ms}
def run(case):
 plan=json.loads((BASE/'cases.json').read_text('utf8'))[case];folder=BASE/'native'/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir();classes=folder/'classes';classes.mkdir()
 fixtures=[ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java',ROOT/'Tools/ForgeBridge/tests/CardCharacteristicsTailFixture.java'];sources=fixtures+[Path(__file__),BASE/'cases.json',RT/'runtime.json'];overrides=[];classpath=[classes,RT/'classes',RT/'lib/*']
 if plan.get('isolatedOverride'):
  stagePath=ROOT/plan['isolatedOverride'];nativeStage=json.loads(stagePath.read_text('utf8'));assert nativeStage['baseRuntimeHash']==runtime['runtimeHash'];isolated=folder/'isolated-native';tested=[]
  for item in nativeStage['compiledClasses']:
   origin=ROOT/item['path'];assert ref(origin)==item;relative=origin.relative_to(Path(nativeStage['classpath']));dest=isolated/relative;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(origin.read_bytes());assert ref(dest)['sha256']==item['sha256'];tested.append(dict(original=item,tested=ref(dest)))
  sources.extend([stagePath,ROOT/nativeStage['frozenSource']['path']]);assert ref(ROOT/nativeStage['frozenSource']['path'])==nativeStage['frozenSource'];classpath.insert(0,isolated);overrides.append(dict(stage=ref(stagePath),source=nativeStage['source'],frozenSource=nativeStage['frozenSource'],compiledClasses=tested,classpath=isolated.relative_to(ROOT).as_posix(),baseRuntimeHash=nativeStage['baseRuntimeHash']))
 for s in sources:(folder/s.name).write_bytes(s.read_bytes())
 checks=[];paid=[];requests=[];endpoints=[];p=None;index=0;stage='next';before=None;source=None;targeted=[];paidSeen=False;manual=[];poolPicks=[];attacked=False;blocked=False;declared=set();original={}
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
  freeze(m,n,'after-'+str(index));check(not n['stack'],'Actual empty stack endpoint '+str(index));check(m['request']['seat']==m['request']['playerSeat'],'Restored physical/logical operator '+str(index))
  result=op['result'];expect=op.get('expect',{})
  if op.get('action','cast') in ['cast','activate']:check(paidSeen,'Actual committed original operation '+str(index))
  for name,pt in expect.get('pt',{}).items():check([find(n,name,1,'Battlefield')['power'],find(n,name,1,'Battlefield')['toughness']]==pt,('Exact actual PT',index,name,pt))
  if 'life'in expect:check([p['life']for p in n['players']]==expect['life'],('Actual life',index,expect['life']))
  for owner,name,zone in expect.get('zone',[]):check(find(n,name,owner,zone),('Original named source zone',index,name,zone))
  for name,color in expect.get('colors',{}).items():check(find(n,name)['colors']==color,('Actual native colors',index,name,color))
  for name,allTypes in expect.get('allTypes',{}).items():check(find(n,name)['allCreatureTypes']==allTypes,('Actual all creature types',index,name,allTypes))
  for name,legal in expect.get('block',{}).items():check(find(n,name,2,'Battlefield')['canBlock']==legal,('Actual native blocker legality',index,name,legal))
  if case=='cda-pt':
   for name in ['Tarmogoyf','Adeline, Resplendent Cathar','Daxos, Blessed by the Sun']:
    c=find(n,name,1,'Battlefield');check(c['id']==original[name],'Same native permanent ID '+name)
    cda=c['ptCda'];base=[c['printedPower'],c['printedToughness']]
    for row in cda:
     for dim,k in enumerate(['power','toughness']):
      if k in row:base[dim]=row[k]
    w=wirecard(m,c['id']);check(w is not None,'Original visible projection '+name)
    check([w['basePower'],w['baseToughness']]==base,('Native CDA baseline matches wire',index,name,base))
    check([w['effectiveBasePower'],w['effectiveBaseToughness']]==[c['currentPower'],c['currentToughness']],('Native effective matches wire',index,name))
    check([w['power'],w['toughness']]==[c['power'],c['toughness']],('Native final matches wire',index,name))
    check([w['powerBaseChanged'],w['toughnessBaseChanged']]==[c['currentPower']!=base[0],c['currentToughness']!=base[1]],('Native external set flags',index,name))
    if name.startswith('Adeline'):check(len(cda)==1 and 'power'in cda[0]and 'toughness'not in cda[0]and c['currentToughness']==4,'Actual power-only CDA preserves toughness 4')
    elif name.startswith('Daxos'):check(len(cda)==1 and 'toughness'in cda[0]and 'power'not in cda[0]and c['currentPower']==2,'Actual toughness-only CDA preserves power 2')
    elif result=='cda-removed-external-set':check(not cda,'Real remove-ability clears native CDA table')
    else:check(len(cda)==1,'Actual native both-dimension CDA table')
   if result.startswith('grist-')or result=='initial-cda':
    c=find(n,'Grist, the Hunger Tide');check(c['id']==original[c['name']],'Same Grist physical ID')
    excluded=c['zone']=='Battlefield';check(c['creature']!=excluded and c['planeswalker'],'Actual Grist zone exclusion type')
    check((not c['ptCda'])if excluded else(c['power']==c['toughness']==1 and len(c['ptCda'])==1),'Actual Grist zone exclusion PT table')
  if case.startswith('cda-nonpt'):
   for name in ['Mistform Ultimus','Transguild Courier','Fallaji Wayfarer','Universal Automaton','Eldrazi Skyspawner','Ghostfire']:
    c=find(n,name);check(c['id']==original[name],'Same original non-PT object '+name);w=wirecard(m,c['id'])
    if w:check(w['currentColors']==c['colors'] and w['currentType']==c['type'],('Exact native current type/color wire',index,name))
   c=find(n,'Fallaji Wayfarer');check(any(x['cda']and x['params'].get('CharacteristicDefining')=='False'for x in c['statics']),'Actual presence-based CDA native flag despite False text')
   if result=='noncreature-warp-entry':check(not find(n,'Eusocial Engineering')['creature'] and any(t['params'].get('Destination')=='Exile'or 'Exile'in str(t)for t in n['delayed']),'Actual inherited noncreature Warp delayed exile')
  endpoints.append(dict(operation=index,result=result,requestId=m['request']['requestId']));write(folder/'endpoints.json',endpoints);index+=1;stage='next'

 try:
  compiler=Path(os.environ.get('MTGB_JAVAC',r'C:/Program Files/Java/jdk-17/bin/javac.exe'))
  with(folder/'compile.log').open('w',encoding='utf8')as log:subprocess.run([str(compiler),'-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*map(str,fixtures)],check=True,stdout=log,stderr=subprocess.STDOUT,timeout=60)
  with tempfile.TemporaryDirectory(prefix='profile-',dir=folder,ignore_cleanup_errors=True)as temp,(folder/'stderr.log').open('w',encoding='utf8')as err,(folder/'trace.jsonl').open('w',encoding='utf8')as trace:
   deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[]);cfg=dict(session='characteristics-'+case,seed=4,startingLife=40,firstPlayerSeat=1,decks=[deck,deck],positions=plan['positions'],auditDir=str(audit));write(folder/'start.json',cfg)
   p=subprocess.Popen([str(RT/runtime['java']),'-Xmx512m','-XX:ActiveProcessorCount=3','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,classpath)),'mtgb.bridge.CardCharacteristicsTailFixture',str(RT/runtime['assets']),str(folder/'start.json')],cwd=RT/runtime['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
   q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start();seen=set();deadline=time.monotonic()+550
   for step in range(1400):
    check(time.monotonic()<deadline,'Bounded lifecycle native deadline');m=q.get(timeout=40)
    if isinstance(m,Exception):raise m
    check(m is not None,'Native process live');trace.write(json.dumps(m)+'\n');trace.flush();check(m['type']not in ['fatal','ended','rejected'],str(m))
    if m['type']!='request':continue
    r=m['request'];rid=r['requestId']
    if rid in seen:continue
    seen.add(rid);opts=r['options'];enabled=[o['id']for o in opts if not o.get('disabled')];kind=r['kind'];view=m['views'][0];n=None if 'Mulligan'in kind else observe(rid);pick='ok'
    requests.append(dict(id=rid,kind=kind,seat=r['seat'],playerSeat=r['playerSeat'],stage=stage,operation=index));op=plan['operations'][index]
    if n:freeze(m,n,'request-'+str(rid))
    if stage=='resolve'and not paidSeen and op.get('action','cast')in ['cast','activate']:
     spell=next((x for x in n['stack']if x['source']==source and not x['trigger']),None)
     if spell:
      check(len(spell['payingMana'])==op['mana'],'Actual original committed mana objects '+str(index));check(spell['actor']==1,'Actual original paying actor');paid.append(dict(operation=index,name=op['source'],sourceId=source,mana=op['mana'],objects=spell['payingMana'],requestId=rid,api=spell['api'],spell=spell['spell'],warp=spell['warp']));write(folder/'paid.json',paid);freeze(m,n,'paid-'+str(index));paidSeen=True
    if kind=='InputPassPriority':
     if stage=='resolve'and not n['stack']:
      endpoint(m,n,op)
      if index==len(plan['operations']):break
      op=plan['operations'][index]
     if stage=='wait'and not n['stack']and view['phase']==op['endPhase']and n['turn']>=op.get('minTurn',1)and('endTurnSeat'not in op or view['turnSeat']==op['endTurnSeat']):
      endpoint(m,n,op)
      if index==len(plan['operations']):break
      op=plan['operations'][index]
     if stage=='combat'and attacked and not n['stack']and view['turnSeat']==1 and view['phase']==op.get('endPhase','MAIN2'):
      endpoint(m,n,op)
      if index==len(plan['operations']):break
      op=plan['operations'][index]
     if stage=='next'and not n['stack']and view['turnSeat']==op.get('turnSeat',1)and (op.get('action')=='wait'or n['turn']>=op.get('minTurn',1))and r['seat']==op.get('seat',1)and(op.get('action')=='wait'or view['phase']==op.get('phase','MAIN1')or op.get('anyCombat')and view['phase'].startswith('COMBAT')):
      before=n;source=None if op.get('action')in ['wait','observe']else find(n,op['source'],op.get('seat',1),'Battlefield'if op.get('action')in ['float','combat','activate']else'Hand',op.get('action')=='float')['id'];targeted=[];paidSeen=False;attacked=False;blocked=False;declared=set();manual=[];poolPicks=list(op.get('poolPayments',[]));freeze(m,n,'before-'+str(index))
      if not original:original.update({c['name']:c['id']for c in cards(n)if c['name'] not in ['Plains','Island','Swamp','Mountain','Forest']})
      if op.get('action')=='observe':
       endpoint(m,n,op)
       if index==len(plan['operations']):break
      elif op.get('action')=='combat':stage='combat'
      elif op.get('action')=='wait':stage='wait'
      else:
       pick='card:'+str(source);stage='resolve'
       for name in op.get('manualSources',[]):
        chosen=next(c for c in n['players'][0]['zones']['Battlefield']if c['name']==name and not c['tapped']and c['id']not in manual);manual.append(chosen['id'])
    elif 'Mulligan'in kind:pick='keep'if'keep'in enabled else'ok'
    elif kind=='InputAttack':
     if stage=='combat'and r['seat']==1 and n['turn']>=op.get('minTurn',1):
      attackers={find(n,name,1,'Battlefield')['id']for name in op.get('attackers',[op['source']])}
      token=next((o['id']for o in opts if o['id']in enabled and o['id'].startswith('attack:')and o['attackerId']in attackers and o['attackerId']not in declared),None)
      if token:pick=token;declared.add(next(o['attackerId']for o in opts if o['id']==token));attacked=True
      else:check(declared==attackers,'All genuine original attackers declared');pick='ok'
     else:pick='ok'
    elif kind=='InputBlock':
     legal=[o for o in opts if o['id']in enabled and o['id'].startswith('block:')]
     if op.get('blocker'):
      bidder=find(n,op['blocker'],2,'Battlefield')['id'];token=next((o['id']for o in legal if o.get('defenderId')==bidder),None)
      if token and not blocked:pick=token;blocked=True
      else:pick='ok'
     else:
      if stage=='combat'and attacked:check(not legal,'Actual disabled blocker options after restrictions')
      pick='ok'
    elif 'PayMana'in kind:
     remaining=[id for id in manual if not next(c for c in cards(n)if c['id']==id)['tapped']]
     if remaining:pick='card:'+str(remaining[0])
     elif poolPicks:
      spec=poolPicks.pop(0);pick=next(x for x in enabled if(x.startswith('mana:S:')if spec=='snow'else ':restricted:'in x if spec=='restricted'else x=='mana:'+spec))
     else:pick='ok'
    elif kind=='InputSelectTargets':
     options=op.get('targets',[]);todo=[spec for spec in options if text_target(spec,n)not in targeted]
     if todo and text_target(todo[0],n)in enabled:pick=text_target(todo[0],n);targeted.append(pick)
     else:pick='ok'
    elif kind=='OrderCards'and r.get('selectionPurpose')=='exertAttack':
     check(stage=='combat'and op.get('exert')and r['minCount']==0 and r['maxCount']==1,'Actual optional Celebrant exert contract')
     pick=next(o['id']for o in opts if o.get('card',{}).get('name')=='Combat Celebrant')if r['selectedCount']==0 else'confirm-cards'
    elif kind=='InputConfirm':
     if stage=='combat'and op.get('hippoAccept')is not None:
      check(r['seat']==r['playerSeat']==1,'Original Hippo controller receives optional decision');pick='ok'if op['hippoAccept']else'cancel'
     else:pick='ok'
    elif kind=='dialog':pick=next((o['id']for o in opts if o['id']in enabled and op.get('choice','').lower()in o['label'].lower()),enabled[0])
    elif kind=='ChooseColor':pick=next((o['id']for o in opts if o['id']in enabled and 'green'in o['label'].lower()),enabled[0])
    elif kind in ['SelectCards','InputSelectCardsFromList','InputSelectEntitiesFromList','ChooseEntity','SelectEntity']:raise AssertionError(('Unexpected lifecycle selection',r))
    elif kind=='OrderAbilities':pick='confirm-order'if'confirm-order'in enabled else next(x for x in enabled if x.startswith('order:'))
    elif kind=='RevealCards':pick='confirm-cards'
    elif kind=='Scry':pick=next((x for x in enabled if x.startswith('scry-top:')),'confirm-scry')
    else:raise AssertionError(('Unhandled native Drain request',r))
    check(pick in enabled,('Original enabled response',kind,pick,enabled));response=dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=rid,option=pick);trace.write(json.dumps(response)+'\n');trace.flush();p.stdin.write(json.dumps(response)+'\n');p.stdin.flush();print(case,index,kind,pick,flush=True)
   else:raise AssertionError('Native request-step bound')
  status='PASS_BOUNDED'
 except Exception:status='FAIL';error=traceback.format_exc();print(error,flush=True)
 finally:
  if p is not None and p.poll()is None:p.kill();p.wait()
 result=dict(status=status,case=case,folder=folder.relative_to(ROOT).as_posix(),checks=len(checks),assertions=checks,paid=paid,endpoints=endpoints,requests=requests,runtimeHash=runtime['runtimeHash'],originalIds=original,sources=[ref(x)for x in sources],testedNativeOverrides=overrides)
 if status=='FAIL':result['error']=error
 write(folder/'result.json',result);write(BASE/(case+'-latest.json'),result);print(status,case,folder,flush=True);return result
def wirecard(m,id):
 found=[]
 def walk(v):
  if isinstance(v,dict):
   if v.get('id')==id and 'basePower'in v:found.append(v)
   for x in v.values():walk(x)
  elif isinstance(v,list):
   for x in v:walk(x)
 walk(m['views']);return found[0]if found else None

def text_target(spec,n):
 if spec[0]=='player':return 'player:'+str(spec[1]-1)
 return 'card:'+str(find(n,spec[1],spec[0])['id'])
if __name__=='__main__':
 for case in sys.argv[1:]:
  result=run(case)
  if result['status']!='PASS_BOUNDED' or result.get('violations') or result.get('failures'):raise SystemExit(1)
