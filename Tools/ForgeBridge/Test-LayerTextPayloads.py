"""Actual characteristic tails; setup initial-only, native observations read-only."""
from pathlib import Path
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback,sys
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'.tools/layers-spellbook-reconcile-1011/layers';RT=ROOT/'ForgeRuntime'
runtime=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def ref(p):return dict(path=p.relative_to(ROOT).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def cards(n):return list({c['id']:c for p in n['players']for z in p['zones'].values()for c in z}.values())
def find(n,name,owner=1,zone=None,untapped=False):return next(c for c in cards(n)if c['name']==name and c['owner']==owner and(zone is None or c['zone']==zone)and(not untapped or not c['tapped']))
def mana(n,seat=1):return n['players'][seat-1]['mana']
def ids(ms):return {m['identity'] for m in ms}
def run(case):
 plan=json.loads((BASE/'cases.json').read_text('utf8'))[case];folder=BASE/'native'/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir();classes=folder/'classes';classes.mkdir()
 fixtures=[ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java',ROOT/'Tools/ForgeBridge/tests/LayerTextPayloadFixture.java'];sources=fixtures+[Path(__file__),BASE/'cases.json',BASE/'original-records.json',RT/'runtime.json',RT/'files.json'];overrides=[];classpath=[classes,RT/'classes',RT/'lib/*']
 if plan.get('isolatedOverride'):
  stagePath=ROOT/plan['isolatedOverride'];nativeStage=json.loads(stagePath.read_text('utf8'));published=all((RT/'classes'/Path(item['path']).relative_to(Path(nativeStage['classpath']).relative_to(ROOT))).exists() and ref(RT/'classes'/Path(item['path']).relative_to(Path(nativeStage['classpath']).relative_to(ROOT)))['sha256']==item['sha256'] for item in nativeStage['compiledClasses']);assert published or nativeStage['baseRuntimeHash']==runtime['runtimeHash'],'Current runtime must contain exact published tested classes or the authorized isolated base';assert ref(RT/'files.json')['sha256']==runtime['runtimeHash'],'Current runtime manifest identity';isolated=folder/'isolated-native';tested=[]
  for item in nativeStage['compiledClasses']:
   origin=ROOT/item['path'];assert ref(origin)==item;relative=origin.relative_to(Path(nativeStage['classpath']));dest=isolated/relative;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(origin.read_bytes());assert ref(dest)['sha256']==item['sha256'];tested.append(dict(original=item,tested=ref(dest)))
  sources.extend([stagePath,ROOT/nativeStage['frozenSource']['path'],ROOT/nativeStage['strictDefinition']['path']]);assert ref(ROOT/nativeStage['frozenSource']['path'])==nativeStage['frozenSource'];classpath.insert(0,isolated) if not published else None;overrides.append(dict(stage=ref(stagePath),source=nativeStage['source'],frozenSource=nativeStage['frozenSource'],compiledClasses=tested,classpath=isolated.relative_to(ROOT).as_posix() if not published else 'ForgeRuntime/classes',execution='published matching tested classes' if published else 'base plus isolated matching tested classes',baseRuntimeHash=nativeStage['baseRuntimeHash']))
 for s in sources:
  dest=folder/'frozen-source'/s.relative_to(ROOT);dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(s.read_bytes())
 checks=[];paid=[];requests=[];endpoints=[];p=None;index=0;stage='next';before=None;source=None;targeted=[];paidSeen=False;manual=[];poolPicks=[];attacked=False;blocked=False;declared=set();original={};wordIndex=0
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
  validate_layer(result,n,m,check,original,before,case)
  endpoints.append(dict(operation=index,result=result,requestId=m['request']['requestId']));write(folder/'endpoints.json',endpoints);index+=1;stage='next'

 try:
  compiler=Path(os.environ.get('MTGB_JAVAC',r'C:/Program Files/Java/jdk-17/bin/javac.exe'))
  with(folder/'compile.log').open('w',encoding='utf8')as log:subprocess.run([str(compiler),'-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*map(str,fixtures)],check=True,stdout=log,stderr=subprocess.STDOUT,timeout=60)
  with tempfile.TemporaryDirectory(prefix='profile-',dir=folder,ignore_cleanup_errors=True)as temp,(folder/'stderr.log').open('w',encoding='utf8')as err,(folder/'trace.jsonl').open('w',encoding='utf8')as trace:
   deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[]);cfg=dict(session='layers-'+case,seed=4,startingLife=40,firstPlayerSeat=1,decks=[deck,deck],positions=plan['positions'],auditDir=str(audit));write(folder/'start.json',cfg)
   p=subprocess.Popen([str(RT/runtime['java']),'-Xmx512m','-XX:ActiveProcessorCount=3','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,classpath)),'mtgb.bridge.LayerTextPayloadFixture',str(RT/runtime['assets']),str(folder/'start.json')],cwd=RT/runtime['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
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
     if stage=='resolve' and op.get('action')=='float' and r['seat']==1 and any(x['source']==source for x in n['players'][0]['mana']):
      freeze(m,n,'after-'+str(index));validate_layer(op['result'],n,m,check,original,before,case);endpoints.append(dict(operation=index,result=op['result'],requestId=rid));index+=1;stage='next';op=plan['operations'][index]
     if stage=='resolve'and op.get('action')!='float'and not n['stack']and paidSeen:
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
      before=n;source=None if op.get('action')in ['wait','observe']else find(n,op['source'],op.get('seat',1),'Battlefield'if op.get('action')in ['float','combat','activate']else'Hand',op.get('action')=='float')['id'];targeted=[];paidSeen=False;attacked=False;blocked=False;declared=set();manual=[];wordIndex=0;poolPicks=list(op.get('poolPayments',[]));freeze(m,n,'before-'+str(index))
      if not original:original.update({str(c['owner'])+':'+c['name']:c['id']for c in cards(n)if c['name'] not in ['Plains','Island','Swamp','Mountain','Forest']})
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
     for spec in op.get('rejectTargets',[]):check(text_target(spec,n) not in enabled,('Actual negative target predicate',index,spec))
     options=op.get('targets',[]);todo=[spec for spec in options if text_target(spec,n)not in targeted]
     if todo and text_target(todo[0],n)in enabled:pick=text_target(todo[0],n);targeted.append(pick)
     else:pick='ok'
    elif kind=='OrderCards'and r.get('selectionPurpose')=='exertAttack':
     check(stage=='combat'and op.get('exert')and r['minCount']==0 and r['maxCount']==1,'Actual optional Celebrant exert contract')
     pick=next(o['id']for o in opts if o.get('card',{}).get('name')=='Combat Celebrant')if r['selectedCount']==0 else'confirm-cards'
    elif r.get('selectionPurpose')=='optionalCosts' and kind in ['OrderCards','ChooseOptions','SelectCards']:
     check(r['minCount']==0 and r['selectedCount']==0,'Actual optional costs declined');pick='confirm-cards'
    elif kind=='InputConfirm':
     if op.get('source')=='Twiddle':
      check(op['choice']=='Untap' and 'cancel' in enabled,'Actual binary Tap=true Untap=false contract');freeze(m,n,'binary-untap');pick='cancel'
     elif stage=='combat'and op.get('hippoAccept')is not None:
      check(r['seat']==r['playerSeat']==1,'Original Hippo controller receives optional decision');pick='ok'if op['hippoAccept']else'cancel'
     else:pick='cancel' if 'buyback' in r.get('message','').lower() else 'ok'
    elif kind=='ChooseOptions':
     freeze(m,n,'mode-'+str(index)+'-'+str(r['selectedCount']));pick=pick_mode(r,op,check)
    elif kind in ['dialog','ChooseColor','ChooseType']:
     if r.get('message')=='getAbilityToPlay':
      label=op.get('ability','');matches=[o for o in opts if o['id']in enabled and o['id']not in ['cancel','ok'] and label.lower() in o['label'].lower()];check(len(matches)==1,('Exact ability picker',index,label,r));pick=matches[0]['id']
     elif op.get('targets') and any(o.get('card') for o in opts):
      todo=[spec for spec in op['targets'] if text_target(spec,n)not in targeted];check(todo,'No unsolicited card target choice');wanted=int(text_target(todo[0],n).split(':')[1]);matches=[o for o in opts if o['id']in enabled and o.get('card',{}).get('id')==wanted];check(len(matches)==1,('Exact original card ID in mixed native selector',index,wanted));pick=matches[0]['id'];targeted.append('card:'+str(wanted));freeze(m,n,'mixed-target-'+str(index))
     elif r.get('message')=='ManaColor' or kind in ['ChooseColor','ChooseType'] or ('choose' in r.get('message','').lower() and ('color' in r.get('message','').lower() or 'type' in r.get('message','').lower())):
      words=op.get('wordChoices',[]);check(wordIndex<len(words),('Unexpected word choice',index,r));label=words[wordIndex];matches=[o for o in opts if o['id']in enabled and o['label'].lower()==label.lower()];check(len(matches)==1,('Canonical original/new word',index,label,r));
      if wordIndex==1:check(all(o['label'].lower()!=words[0].lower() for o in opts),('New word excludes original',index))
      if op['source']=='Artificial Evolution' and wordIndex==1:check(all(o['label']!='Wall' for o in opts),'Actual forbidden Wall absent')
      freeze(m,n,'word-'+str(index)+'-'+str(wordIndex));wordIndex+=1;pick=matches[0]['id']
     else:
      label=op.get('choice','');matches=[o for o in opts if o['id']in enabled and label and label.lower() in o['label'].lower()];check(len(matches)==1,('Exact mode/operation choice',index,label,r));pick=matches[0]['id']
    elif kind in ['SelectCards','InputSelectCardsFromList','InputSelectEntitiesFromList','ChooseEntity','SelectEntity']:raise AssertionError(('Unexpected lifecycle selection',r))
    elif kind=='OrderCards' and r.get('selectionPurpose')=='optionalCosts':pick='confirm-cards'
    elif kind=='OrderAbilities':pick='confirm-order'if'confirm-order'in enabled else next(x for x in enabled if x.startswith('order:'))
    elif kind=='RevealCards':pick='confirm-cards'
    elif kind=='Scry':pick=next((x for x in enabled if x.startswith('scry-top:')),'confirm-scry')
    else:raise AssertionError(('Unhandled native Drain request',r))
    check(pick in enabled,('Original enabled response',kind,pick,enabled));response=dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=rid,option=pick);trace.write(json.dumps(response)+'\n');trace.flush();p.stdin.write(json.dumps(response)+'\n');p.stdin.flush();print(case,index,kind,pick,flush=True)
   else:raise AssertionError('Native request-step bound')
  check(index==len(plan['operations']) and len(endpoints)==len(plan['operations']),'Strict complete full plan suffix');check(len(paid)==sum(x.get('action') in ['cast','activate'] for x in plan['operations']),'Every actual paid operation committed');status='PASS_BOUNDED'
 except Exception:status='FAIL';error=traceback.format_exc();print(error.splitlines()[0]+' '+error.splitlines()[-1][:400],flush=True)
 finally:
  if p is not None and p.poll()is None:p.kill();p.wait()
 result=dict(status=status,case=case,folder=folder.relative_to(ROOT).as_posix(),checks=len(checks),assertions=checks,paid=paid,endpoints=endpoints,requests=requests,runtimeHash=runtime['runtimeHash'],originalIds=original,sources=[dict(source=ref(x),frozen=ref(folder/'frozen-source'/x.relative_to(ROOT)))for x in sources],testedNativeOverrides=overrides)
 if status=='FAIL':result['error']=error
 write(folder/'result.json',result);write(BASE/(case+'-latest.json'),result);print(status,case,folder,flush=True);return result
def pick_mode(r,op,check):
 label=op.get('choice','');matches=[o for o in r['options']if not o.get('disabled') and label and label.lower()in o['label'].lower()]
 check(r['minCount']==r['maxCount']==1 and len(matches)==1,('Exact native one-mode contract',label))
 pick='confirm-cards'if r['selectedCount']>=r['minCount']else matches[0]['id'];check(any(o['id']==pick and not o.get('disabled')for o in r['options']),'Exact enabled mode select/confirmation');return pick

def validate_layer(result,n,m,check,original,before,case):
 def c(name,owner=1,zone='Battlefield'):return find(n,name,owner,zone)
 def ab(name,api):return [x for x in c(name)['abilities'] if x['api']==api and not x['spell']]
 def inherited(name):return [x for x in ab(name,'Mana') if x['params'].get('Produced')=='Any']
 def destroy(word):
  rows=ab('Auriok Champion','Destroy');check(len(rows)==1,'One actual exchanged Destroy ability');a=rows[0]
  check(a['host']==c('Auriok Champion')['id'],'Current ability host is original receiver');check(a['params']['ValidTgts']=='Permanent.'+word,'Actual native changed target word '+word)
  check(word.lower()+' permanent' in a['description'].lower(),'Actual native copied/current ability description '+word)
  check(word.lower()+' permanent' in c('Auriok Champion')['abilityText'].lower(),'Actual native CardView ability text '+word)
 for name in ['Allosaurus Shepherd','Elvish Mystic','Northern Paladin','Auriok Champion']:
  check(c(name)['id']==original['1:'+name],'Original physical ID '+name)
  w=wirecard(m,c(name)['id']);check(w is not None,'Known public wire original ID '+name)
  check([w['power'],w['toughness']]==[c(name)['power'],c(name)['toughness']],'Wire/native PT '+name)
  check(w['currentType']==c(name)['type'],'Wire/native type '+name)
 if result=='song-inherited-group':
  own=[x for x in n['players'][0]['zones']['Battlefield'] if x['creature']]
  check(len(own)==5,'Five real initial controlled creatures receive grant')
  for x in own:check(any(a['params'].get('Produced')=='Any' and a['host']==x['id'] for a in x['abilities']),'Inherited mana points to its exact recipient '+x['name'])
  for x in n['players'][1]['zones']['Battlefield']:check(not any(a['params'].get('Produced')=='Any' for a in x['abilities']),'Opponent excluded from actual AnimateAll '+x['name'])
  check(not any(a['params'].get('Produced')=='Any' for a in c('Song of Freyalise')['abilities']),'Noncreature Saga excluded')
 elif result=='rancor-external':check(c('Auriok Champion')['power']==3 and 'Trample' in c('Auriok Champion')['keywords'],'Real external Rancor boost/keyword')
 elif result=='ordinary-boost':check([c('Elvish Mystic')['power'],c('Elvish Mystic')['toughness']]==[4,4],'Real ordinary Giant Growth before base layer')
 elif result in ['elf-filter-base','goblin-filter-base']:
  expectedBase=5 if case=='combined' or result=='elf-filter-base' else 1;expectedNet=8 if case=='combined' else expectedBase
  check([c('Elvish Mystic')['currentPower'],c('Elvish Mystic')['currentToughness']]==[expectedBase,expectedBase],'Actual retained base layer matches this original flow')
  check([c('Elvish Mystic')['power'],c('Elvish Mystic')['toughness']]==[expectedNet,expectedNet],'Actual ordinary boost or original unmodified nonrecipient matches this flow')
  check(c('Northern Paladin')['currentPower']==3 and c('Auriok Champion')['currentPower']==1,'Non-Elf and non-Goblin excluded from filtered base grants')
  check(c('Goblin Piker',2)['currentPower']==2 and c('Llanowar Elves',2)['currentPower']==1,'Opponent Elf/Goblin excluded from group')
  if result=='elf-filter-base':check(c('Goblin Piker')['currentPower']==2,'Own non-Elf Goblin excluded')
  else:check(c('Goblin Piker')['currentPower']==5 and 'Dinosaur' in c('Goblin Piker')['type'],'Actual changed creature-type word selects Goblin recipient')
 elif result=='inherited-mana':
  pool=n['players'][0]['mana'];check(len(pool)==1 and pool[0]['color']=='U' and pool[0]['source']==c('Elvish Mystic')['id'],'Actual inherited AnyMana activation produces U from original recipient')
 elif result=='type-words':
  a=ab('Allosaurus Shepherd','AnimateAll')[0];check('Creature.Goblin+YouCtrl'==a['params']['ValidCards'],'Actual original Elf becomes canonical Goblin in ability predicate')
  check(c('Allosaurus Shepherd')['typeWords'].get('Elf')=='Goblin','Actual persistent type-word map')
 elif result=='prior-color-words':
  a=ab('Northern Paladin','Destroy')[0];check(a['params']['ValidTgts']=='Permanent.Red','Actual initial Black-to-Red predicate');check(c('Northern Paladin')['colorWords'].get('Black')=='Red','Actual prior permanent color-word map')
 elif result=='text-exchanged':
  destroy('Red');pal=c('Northern Paladin');champ=c('Auriok Champion');check(len(pal['traitsByText'])==len(champ['traitsByText'])==1,'Each real original has one ByText exchange table')
  check(not ab('Northern Paladin','Destroy'),'Original Paladin loses own intrinsic Destroy')
  check(any(x['params'].get('Execute')=='TrigGainLife' and x['host']==pal['id'] for x in pal['triggers']),'Copied life trigger uses new actual host')
  check(any('Protection' in x for x in pal['keywords']) and not any('Protection' in x for x in champ['keywords']),'Intrinsic protection transferred only to opposite host')
  check(champ['power']==3 and 'Trample' in champ['keywords'],'Unexchanged external Rancor survives ByText swap')
  check(inherited('Northern Paladin') and inherited('Auriok Champion'),'Both independent Song grants survive exchange')
 elif result=='temporary-color-words':destroy('Green');check(c('Auriok Champion')['colorWords'].get('Red')=='Green','Actual temporary receiver word map')
 elif result=='changed-destroy-green':destroy('Green');check(find(n,'Grizzly Bears',2,'Graveyard')['id']==original['2:Grizzly Bears'],'Actual Green target destroyed with original ID');check(c('Goblin Piker',2)['id']==original['2:Goblin Piker'],'Wrong-color red target retained')
 elif result=='actual-untap':check(not c('Auriok Champion')['tapped'],'Twiddle actually untaps receiver')
 elif result=='swapped-trigger-host':
  check(n['players'][0]['life']==41,'Actual newly inherited optional ETB trigger gains1');check(any(x['params'].get('Execute')=='TrigGainLife' for x in c('Northern Paladin')['triggers']),'Triggered inherited life source retained')
 elif result=='eot-one-source-expiry':
  check(n['turn']==2 and n['phase']=='MAIN1','Natural next-opponent main1 after actual EOT')
  check([c('Elvish Mystic')['currentPower'],c('Elvish Mystic')['power'],c('Elvish Mystic')['toughness']]==[1,1,1],'Real EOT clears both base5 and ordinary boost')
  check(c('Goblin Piker')['currentPower']==2 and not c('Goblin Piker')['ptExternal'],'Real EOT clears second Goblin base source')
  check(inherited('Elvish Mystic') and inherited('Auriok Champion'),'Different-duration Song grants survive both base sources')
  destroy('Red');check(not c('Auriok Champion')['colorWords'],'Temporary Red-to-Green map expired; copied prior Red remains')
  if case in ['combined','tail']:check(c('Allosaurus Shepherd')['typeWords'].get('Elf')=='Goblin','Unrelated permanent creature-type word change survives EOT')
  else:check(not c('Allosaurus Shepherd')['typeWords'],'Unchanged original Shepherd in exchange-only tail')
 elif result=='restored-destroy-red':destroy('Red');check(find(n,'Goblin Piker',2,'Graveyard')['id']==original['2:Goblin Piker'],'Actual restored Red target destroyed with exact ID')
 elif result=='new-object-blink':
  pal=c('Northern Paladin');check(pal['timestamp']!=find_id(before,pal['id'])['timestamp'],'Cloudshift actual same-ID new game timestamp')
  check(not pal['traitsByText'] and not pal['colorWords'] and not inherited('Northern Paladin'),'New object has no old exchange/text/external grant')
  rows=ab('Northern Paladin','Destroy');check(len(rows)==1 and rows[0]['params']['ValidTgts']=='Permanent.Black','New object has true original intrinsic Black predicate')
  destroy('Red');check(c('Auriok Champion')['traitsByText'],'Other original remains exchanged before source removal')
 elif result=='exchange-source-expiry':
  pal=c('Northern Paladin');champ=c('Auriok Champion');check(not pal['traitsByText'] and not champ['traitsByText'],'Source departure cleans only original live exchange entries')
  check(pal['timestamp']==find_id(before,pal['id'])['timestamp'],'Old duration cleanup leaves blinked new object timestamp unchanged')
  check(not ab('Auriok Champion','Destroy') and any(x['params'].get('Execute')=='TrigGainLife' for x in champ['triggers']),'Original Champion intrinsic life trigger restored and copied Destroy removed')
  check(any('Protection' in x for x in champ['keywords']) and not any('Protection' in x for x in pal['keywords']),'Correct original intrinsic protection restored')
  check(champ['power']==3 and 'Trample' in champ['keywords'] and inherited('Auriok Champion'),'Rancor and distinct-duration Song survive source expiry')
  check(n['players'][0]['life']==41 and not n['stack'],'Accurate life and normal next priority after source cleanup')
 else:raise AssertionError('Undefined exact result '+result)
def find_id(n,cid):return next(c for c in cards(n)if c['id']==cid)

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
 if not sys.argv[1:]:raise SystemExit('Required exact caseArg: combined, tail, or exchange-tail')
 for case in sys.argv[1:]:
  result=run(case)
  if result['status']!='PASS_BOUNDED' or result.get('violations') or result.get('failures'):raise SystemExit(1)
