"""R03 actual multiple recipients, same/different repeated villain choices.
Opening fixture only; actual paid casts/damage/end-step/native results. Additional
native AI seats answer legal choices, never assign outcomes or represent clients.
"""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/villain-multi-1009';FIX=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['RoyalStateFixture.java','VillainMultiFixture.java']];DECK=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
def wr(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def cards(o,z):return {c['id']:c for p in o['players'] for c in p['zones'][z]}
def run():
 case='davros-repeat';f=OUT/'native'/case/str(time.time_ns());f.mkdir(parents=True);audit=f/'audit';audit.mkdir();classes=f/'classes';classes.mkdir();checks=0;p=None
 for x in FIX+[Path(__file__)]: (f/x.name).write_bytes(x.read_bytes())
 result=dict(case=case,folder=f.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driver=sha(Path(__file__)),fixtures={x.name:sha(x) for x in FIX},producerClassesLib={x.relative_to(RT).as_posix():sha(x) for d in ['classes','lib'] for x in sorted((RT/d).rglob('*')) if x.is_file()},status='RUNNING');wr(f/'result.json',result)
 def ck(ok,msg):
  nonlocal checks
  assert ok,msg;checks+=1
 try:
  with (f/'compile.log').open('w',encoding='utf8') as log:subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(f/x.name) for x in FIX]],check=True,stdout=log,stderr=log)
  with tempfile.TemporaryDirectory(prefix='mtgb-villain-multi-') as temp,(f/'stderr.log').open('w',encoding='utf8') as log,(f/'trace.jsonl').open('w',encoding='utf8') as trace:
   pos=[dict(Battlefield=[],Hand=[],Graveyard=[],Library=['Forest','Island','Plains','Mountain','Swamp']) for s in [1,2,3,4]];pos[0]['Hand']=['Davros, Dalek Creator','The Valeyard','Lightning Bolt','Lightning Bolt'];pos[1]['Hand']=['Runeclaw Bear','Shock','Forest'];pos[2]['Hand']=['Sol Ring','Shock','Forest'];pos[3]['Hand']=['Giant Growth','Forest']
   cfg=dict(session=case,seed=4,startingLife=30,firstPlayerSeat=1,nativeSeats=4,decks=[DECK,DECK],positions=pos,auditDir=str(audit),extraChoiceInputs={'3':['Draw','Draw'],'4':[]});wr(f/'start.json',cfg)
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.VillainMultiFixture',str(RT/CFG['assets']),str(f/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start()
   def obs(r):
    path=audit/(str(r['requestId'])+'.json');end=time.monotonic()+4
    while time.monotonic()<end:
     try:return json.loads(path.read_text())
     except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
    raise AssertionError('missing native observer')
   def save(m,label,o=None):wr(f/(label+'-wire.json'),m);wr(f/(label+'-native.json'),o or obs(m['request']))
   cast=0;seen=set();initial=None;base=None;prechoice=None;ballots=0;discarded=[];discard_steps=0;target_done=set();deadline=time.monotonic()+180
   for step in range(600):
    ck(time.monotonic()<deadline,'deadline');m=q.get(timeout=35)
    if isinstance(m,Exception):raise m
    ck(m is not None,'native exited');trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type'] not in ('fatal','ended','rejected'),m)
    if m['type']!='request':continue
    r=m['request'];opts=r['options'];ids=[x['id'] for x in opts];kind=r['kind'];v=m['views'][0];pick='ok'
    if r['requestId'] in seen:continue
    seen.add(r['requestId'])
    if 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif kind=='InputPassPriority' and r['seat']==1 and not v['stackItems']:
     o=obs(r)
     if cast<4 and o['phase']=='MAIN1':
      if cast==0:initial=o;save(m,'initial',o)
      name=['Davros, Dalek Creator','The Valeyard','Lightning Bolt','Lightning Bolt'][cast];c=next(c for c in o['players'][0]['zones']['Hand'] if c['name']==name);pick='card:'+str(c['id']);cast+=1
     elif cast==4 and base is None:
      base=o;save(m,'before-end',o);ck([p['life'] for p in o['players']]==[30,27,27,30],'only two opponents damaged3 by actual bolts');ck(sum(c['tapped'] for c in o['players'][0]['zones']['Battlefield'])==11,'actual4+5+R+R mana paid');ck([p['additionalVillainChoices'] for p in o['players']]==[0,1,1,1],'Valeyard actual repeat permission');ck(not any(c['name']=='Dalek Token' for c in cards(o,'Battlefield').values()),'end-step token not yet created')
     elif cast==4 and ballots==2 and discard_steps==2:
      save(m,'resolved',o);ck(o['turn']==1 and o['turnSeat']==1,'natural same end-step resolution');ck(r.get('votingPlayerSeat',0)==0 and not r.get('secretVote',False),'vote metadata absent in ordinary villain flow');ck(r['seat']==1,'next supported priority restored');ck([p['life'] for p in o['players']]==[30,27,27,30],'no extra life effects')
      ck(len(o['extraChoices'])==2 and all(c['actor']==3 and c['chosen']=='Draw' and c['num']==1 and c['options']==['Draw','Discard'] for c in o['extraChoices']),'native third twice same Draw; unaffected4 never asked');ck(o['extraChoices'][0]['hands']==o['extraChoices'][1]['hands'],'both same choices collected before third effects');ck(all(not c['remembered'] for c in o['extraChoices']),'previous original memory cleared before next opponent')
      for seat in [1,2,3,4]:
       old=base['players'][seat-1]['zones'];now=o['players'][seat-1]['zones'];top=[c['id'] for c in old['Library']];draw=3 if seat==1 else 0;ck([c['id'] for c in now['Library']]==top[draw:],'exact native original top draw seat'+str(seat));ck({c['id'] for c in now['Hand']}==({c['id'] for c in old['Hand']}|set(top[:draw]))-(set(discarded) if seat==2 else set()),'exact original native hand seat'+str(seat));ck({c['id'] for c in now['Graveyard']}=={c['id'] for c in old['Graveyard']}|(set(discarded) if seat==2 else set()),'only exact original discarded card grave')
      bf=cards(o,'Battlefield');previous=cards(base,'Battlefield');new=[c for id,c in bf.items() if id not in previous];ck(len(new)==1 and new[0]['power']==new[0]['toughness']==3 and new[0]['owner']==new[0]['controller']==1,'one exact new own3/3Dalek');ck(new[0]['name']=='Dalek Token','actual native Dalek name');
      for id,c in previous.items():ck(bf[id]==c,'all original battlefield objects unchanged')
      source=next(c for c in bf.values() if c['name']=='Davros, Dalek Creator');ck(not source['remembered'] and not source['imprinted'],'original remembered recipient and imprint clear');result.update(status='PASS_BOUNDED',checks=checks,requests=len(seen),paidOperations=4,note='Actual four casts / two opponents damaged / natural end-step; human2 different Draw/Discard, third legal same Draw twice, unaffected4, exact draws/private discard/Dalek/memory/resume. Extra native seats not production clients.');break
    elif 'InputSelectTargets' in kind:
     save(m,'target-'+str(step));ck(cast in [3,4],'only actual Bolt targets')
     if r.get('selectedCount'):ck(cast in target_done,'confirm actual already chosen Bolt target');pick='ok'
     else:
      ck(cast not in target_done,'one target selection per actual Bolt');want='Player '+str(cast-1);pick=next(x['id'] for x in opts if x['label']==want);target_done.add(cast)
    elif kind=='ChooseOptions':
     o=obs(r);save(m,'choice-'+str(step),o);ck(r['seat']==r['playerSeat']==2 and r.get('votingPlayerSeat',0)==0 and not r.get('secretVote',False),'eligible original2 chooses ordinary villain effect');ck(base is not None and ballots<2,'two actual native repeated human ballots');select=[x for x in opts if x['id'].startswith('select:')];ck(len(select)==2 and all(x.get('card',{}).get('name')=='Davros, Dalek Creator' or 'Davros' in x['label'] for x in select),'exact original two spell-ability options')
     if prechoice is None:prechoice=o
     for seat in [1,2,3,4]:ck(o['players'][seat-1]['zones']['Hand']==prechoice['players'][seat-1]['zones']['Hand'],'collect both human choices before any effect seat'+str(seat))
     if r.get('selectedCount'):pick='confirm-cards';ballots+=1
     else:want='You draw a card.' if ballots==0 else 'discards 1 card.';pick=next(x['id'] for x in select if want in x['label'])
    elif 'SelectEntities' in kind or 'SelectCards' in kind:
     o=obs(r);save(m,'discard-'+str(step),o);ck(ballots==2 and r['seat']==r['playerSeat']==2,'original2 own discard after both collected');choices=[x for x in opts if x.get('card')];expected={c['id'] for c in base['players'][1]['zones']['Hand']};ck({x['card']['id'] for x in choices}==expected,'all and only native original2 hand pool');source=next(c for c in cards(o,'Battlefield').values() if c['name']=='Davros, Dalek Creator');ck(source['remembered']==['Player 2'],'remembered only actual discard recipient');oldlib=[c['id'] for c in base['players'][0]['zones']['Library']];ck([c['id'] for c in o['players'][0]['zones']['Library']]==oldlib[1:],'first original2 Draw already resolved before discard')
     if r.get('selectedCount'):pick='confirm-cards' if 'confirm-cards' in ids else 'ok'
     else:target=next(x for x in choices if x['card']['name']=='Runeclaw Bear');pick=target['id'];discarded.append(target['card']['id'])
     discard_steps+=1
    elif 'PayMana' in kind:save(m,'payment-'+str(step));pick='ok'
    elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in ids else next(i for i in ids if i.startswith('order:'))
    elif kind=='RevealCards':pick='confirm-cards'
    elif kind in ('InputAttack','InputBlock','InputPassPriority'):pick='ok'
    elif 'ok' not in ids:save(m,'unhandled-'+str(step));raise AssertionError(('unhandled actual input',r))
    ck(pick in ids and not next(o for o in opts if o['id']==pick).get('disabled'),('legal original response',r,pick));print(case,step,kind,r['seat'],pick,flush=True);p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   else:raise AssertionError('step limit')
 except Exception:result.update(status='FAIL',checks=checks,error=traceback.format_exc())
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 wr(f/'result.json',result);wr(OUT/'native-result.json',result);print(result['status'],result.get('error',''),flush=True);return result
if __name__=='__main__':run()
