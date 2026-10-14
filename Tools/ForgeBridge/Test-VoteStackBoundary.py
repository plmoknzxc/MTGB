"""R03 actual vote target inheritance and AllVotes trigger; tie-copy then majority-counter."""
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/r03-payload-1010';FIX=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['RoyalStateFixture.java','VoteChoiceFixture.java']];DECK=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
def rd(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def wr(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def run():
 majorityOnly='--majority-only' in sys.argv
 case='vote-counter-allvotes' if majorityOnly else 'vote-stack-allvotes'
 f=OUT/'native'/case/str(time.time_ns());f.mkdir(parents=True);audit=f/'audit';audit.mkdir();classes=f/'classes';classes.mkdir();checks=0;p=None
 for x in FIX+[Path(__file__)]: (f/x.name).write_bytes(x.read_bytes())
 result=dict(folder=f.relative_to(ROOT).as_posix(),case='vote-stack-allvotes',runtimeHash=CFG['runtimeHash'],driver=sha(Path(__file__)),fixtures={x.name:sha(x) for x in FIX},producerClassesLib={x.relative_to(RT).as_posix():sha(x) for d in ['classes','lib'] for x in (RT/d).rglob('*') if x.is_file()},status='RUNNING');wr(f/'result.json',result)
 def ck(ok,msg):
  nonlocal checks
  assert ok,msg;checks+=1
 try:
  with (f/'compile.log').open('w') as log:subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(f/x.name) for x in FIX]],check=True,stdout=log,stderr=subprocess.STDOUT)
  with tempfile.TemporaryDirectory(prefix='mtgb-vote-stack-') as temp,(f/'trace.jsonl').open('w',encoding='utf8') as trace,(f/'stderr.log').open('w') as log:
   positions=[dict(Battlefield=['Grudge Keeper','Ballot Broker'],Hand=['Reach Through Mists','Reach Through Mists','Split Decision','Split Decision'],Library=['Island','Plains','Forest','Mountain']),dict(Battlefield=['Grizzly Bears'],Hand=['Negate'],Library=['Island','Plains','Forest','Mountain'])]
   cfg=dict(session='vote-stack-allvotes',seed=4,startingLife=30,firstPlayerSeat=1,nativeSeats=2,decks=[DECK,DECK],positions=positions,auditDir=str(audit),extraVoteInputs={});wr(f/'start.json',cfg)
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.VoteChoiceFixture',str(RT/CFG['assets']),str(f/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start()
   def obs(r):
    end=time.monotonic()+4
    while time.monotonic()<end:
     try:return rd(audit/(str(r['requestId'])+'.json'))
     except (FileNotFoundError,json.JSONDecodeError,PermissionError):time.sleep(.01)
    raise AssertionError('native read-only observer missing')
   def save(m,o,n):wr(f/(n+'-wire.json'),m);wr(f/(n+'-native.json'),o)
   initial=None;stage=2 if majorityOnly else 0;cast='';seen=set();spellIDs=[];votes={1:[0,0],2:[0,0]};copied=False;triggerStages=set();deadline=time.monotonic()+180
   for step in range(500):
    ck(time.monotonic()<deadline,'deadline');m=q.get(timeout=35)
    if isinstance(m,Exception):raise m
    ck(m is not None,'process exited');trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type'] not in ['fatal','ended','rejected'],m)
    if m['type']!='request':continue
    r=m['request'];kind=r['kind'];opts=r['options'];ids=[x['id'] for x in opts];pick='ok';v=m['views'][0]
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);o=None if 'Mulligan' in kind else obs(r)
    ck(not m['views'][0]['players'][1]['hand'] and not m['views'][1]['players'][0]['hand'],'private hands concealed')
    if 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif kind=='InputPassPriority':
     if initial is None and r['seat']==1 and v['phase']=='MAIN1':initial=o;save(m,o,'initial')
     for s in v['stackItems']:
      if s.get('copied') and s.get('source',{}).get('name')=='Reach Through Mists':copied=True;save(m,o,'copied-stack')
      if s.get('source',{}).get('name')=='Grudge Keeper':triggerStages.add(stage);save(m,o,'keeper-stack-'+str(stage))
     if initial is not None and r['seat']==1 and v['phase']=='MAIN1':
      if stage in [0,2] and not v['stackItems']:
       if stage==2 and not majorityOnly:
        ck(votes=={1:[1,0],2:[1,0]} and copied and triggerStages=={2},'first actual tie/copied original spell and AllVotes trigger');ck([pv['life'] for pv in o['players']]==[30,28],'first Grudge actual different voter loses2');save(m,o,'first-resolved')
        top=initial['players'][0]['zones']['Library'];ck([c['id'] for c in o['players'][0]['zones']['Library']]==[c['id'] for c in top[2:]],'actual copy and original each draw exact top card');ck(len(o['voteLogs'])==1 and len(o['voteLogs'][0].splitlines())==2,'first exact two credited ballots')
       cast='Reach Through Mists';c=next(c for c in o['players'][0]['zones']['Hand'] if c['name']==cast);spellIDs.append(c['id']);pick='card:'+str(c['id']);stage+=1
      elif stage in [1,3] and v['stackItems']:
       ck(len(v['stackItems'])==1 and v['stackItems'][0]['source']['id']==spellIDs[-1],'original spell pending before actual vote response');save(m,o,'original-stack-'+str(stage));cast='Split Decision';c=next(c for c in o['players'][0]['zones']['Hand'] if c['name']==cast);spellIDs.append(c['id']);pick='card:'+str(c['id']);stage+=1
      elif stage==4 and not v['stackItems']:
       save(m,o,'resolved');ck(votes==({1:[0,2],2:[0,1]} if majorityOnly else {1:[1,2],2:[1,1]}),'exact own0/1 extra ballots and original actor credits');ck(triggerStages==({4} if majorityOnly else {2,4}),'exact native AllVotes trigger continuations');ck([pv['life'] for pv in o['players']]==[30,28 if majorityOnly else 26],'only each different opponent gets two life loss')
       ck(len(o['voteLogs'])==(1 if majorityOnly else 2) and [len(x.splitlines()) for x in o['voteLogs']]==([3] if majorityOnly else [2,3]),'exact majority/tie log count')
       for text,n in zip(o['voteLogs'],[2] if majorityOnly else [1,2]):ck(sum(x.startswith('Player 1 ') and 'Denial' in x for x in text.splitlines())==n and sum(x.startswith('Player 2 ') and 'Duplication' in x for x in text.splitlines())==1,'exact original voter and choice counts')
       for seat in [1,2]:
        old=initial['players'][seat-1]['zones'];now=o['players'][seat-1]['zones'];draw=2 if seat==1 and not majorityOnly else 0;top=[c['id'] for c in old['Library']];ck([c['id'] for c in now['Library']]==top[draw:],'only first copy/original draw; majority original countered');ck({c['id'] for c in now['Hand']}==({c['id'] for c in old['Hand']}-set(spellIDs))|set(top[:draw]),'exact original remaining private hand/drawn IDs');ck({c['id'] for c in now['Graveyard']}==(set(spellIDs) if seat==1 else set()),'all paid original spell IDs grave, no copied card created in zones');ck(now['Exile']==old['Exile'],'exile unchanged')
        oldfield={c['id']:c for c in old['Battlefield']};newfield={c['id']:c for c in now['Battlefield']};ck(set(oldfield)==set(newfield),'exact original battlefield unchanged')
        for id,c in newfield.items():
         for key in ['name','owner','controller','timestamp','counters','remembered']:ck(c[key]==oldfield[id][key],'unchanged field '+key)
        for view in m['views']:ck(sum(view['players'][seat-1]['manaPool'])==sum(view['players'][seat-1]['snowManaPool'])==0,'zero mana pools')
       ck(sum(c['tapped'] for c in o['players'][0]['zones']['Battlefield'])==(3 if majorityOnly else 6),'actual paid U/1U');ck(r['seat']==r['playerSeat']==1 and v['turn']==1,'restored original actor/turn');result.update(status='PASS_BOUNDED',checks=checks,requests=len(seen),spellIDs=spellIDs,votes=votes,case=case,scope='Actual paid spells and inherited original stack target; majority Counter/no draw and native Grudge Keeper AllVotes life loss, optional ballots/credits/IDs/resources/restored actor. Tie-copy is separate saved native proof when majority-only. No global child payload/locale/four-client PASS.');break
    elif kind in ['InputSelectTargets','SelectStackTarget']:
     save(m,o,'targets-'+str(step));rows=[x for x in opts if x['id'] not in ['ok','cancel']]
     if r.get('selectedCount'):pick='ok'
     else:ck(len(rows)==1 and 'Reach Through Mists' in rows[0]['label'],'exact sole original legal stack spell target');pick=rows[0]['id']
    elif kind=='ChooseOptions':
     save(m,o,'vote-'+str(step));ix=0 if stage==2 else 1;seat=r['playerSeat'];ck(seat==r['seat'] and seat in [1,2],'actual original voter authority')
     if r.get('selectedCount'):pick='confirm-cards'
     else:desired='Denial' if seat==1 else 'Duplication';pick=next(x['id'] for x in opts if x['label']==desired or x['label'].endswith(desired));votes[seat][ix]+=1
    elif kind=='dialog' and r['message']=='one: How many additional votes do you want?':
     save(m,o,'optional-'+str(step));ck(r['seat']==r['playerSeat']==1,'only real optional-vote owner chooses');desired='0' if stage==2 else '1';pick=next(x['id'] for x in opts if x['label']==desired)
    elif 'PayMana' in kind:save(m,o,'payment-'+str(step));pick='ok'
    elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in ids else next(x for x in ids if x.startswith('order:'))
    elif kind=='RevealCards':pick='confirm-cards'
    elif 'ok' not in ids:save(m,o,'unhandled-'+str(step));raise AssertionError(('unhandled',r))
    ck(pick in ids and not next(x for x in opts if x['id']==pick).get('disabled'),('original legal option',r,pick));print(step,kind,r['seat'],pick,flush=True);p.stdin.write(json.dumps(dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   else:raise AssertionError('step limit')
 except Exception:result.update(status='FAIL',checks=checks,error=traceback.format_exc())
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 wr(f/'result.json',result);wr(OUT/('vote-counter-result.json' if majorityOnly else 'vote-stack-result.json'),result);print(result['status'],result.get('error',''),flush=True);return result
if __name__=='__main__':raise SystemExit(run()['status']!='PASS_BOUNDED')
