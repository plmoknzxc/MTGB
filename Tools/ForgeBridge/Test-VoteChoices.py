"""R03 actual vote count and zone consumers. No after-opening outcome setters.
Extra native seats provide validated fixture vote inputs, not production clients.
"""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/vote-choice-1009'
FIX=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['RoyalStateFixture.java','VoteChoiceFixture.java']];DECK=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
def wr(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def scenario(case):
 seats=4 if case.endswith('tie') else 3
 source='Plea for Power' if case.startswith('plea') else 'Tivit, Seller of Secrets' if case.startswith('tivit') else "Council's Judgment" if case.startswith('judgment') else 'Custodi Squire'
 positions=[dict(Battlefield=[],Hand=[source] if s==1 else [],Graveyard=[],Library=['Island','Plains','Forest','Mountain','Swamp']) for s in range(1,seats+1)]
 if case=='plea-majority':votes={1:['Time'],2:['Time'],3:['Knowledge']};counts={'Time':2,'Knowledge':1}
 elif case=='plea-tie':votes={1:['Time'],2:['Knowledge'],3:['Time'],4:['Knowledge']};counts={'Time':2,'Knowledge':2}
 elif case.startswith('tivit'):
  positions[0]['Battlefield']=["Brago's Representative"];positions[1]['Battlefield']=['Ballot Broker']
  if case=='tivit-accept':votes={1:['Evidence','Evidence','Bribery'],2:['Evidence','Bribery'],3:['Bribery']};counts={'Evidence':3,'Bribery':3}
  elif case=='tivit-decline':votes={1:['Evidence','Evidence'],2:['Bribery'],3:['Bribery']};counts={'Evidence':2,'Bribery':2}
  else:votes={1:['Bribery']*3,2:['Bribery']*2,3:['Bribery']};counts={'Evidence':0,'Bribery':6}
 elif case=='judgment-empty':votes={1:[],2:[],3:[]};counts={}
 elif case=='judgment-tie':
  positions[0]['Battlefield']=['Llanowar Elves'];positions[1]['Battlefield']=['Grizzly Bears','Runeclaw Bear','Wall of Roots'];votes={1:['Grizzly Bears'],2:['Runeclaw Bear'],3:['Grizzly Bears'],4:['Runeclaw Bear']};counts={'Grizzly Bears':2,'Runeclaw Bear':2}
 elif case.startswith('squire'):
  positions[0]['Graveyard']=['Grizzly Bears','Runeclaw Bear','Sol Ring','Lightning Bolt'];positions[1]['Graveyard']=['Grizzly Bears']
  votes={1:['Grizzly Bears'],2:['Runeclaw Bear' if case.endswith('tie') else 'Grizzly Bears'],3:['Grizzly Bears' if case.endswith('tie') else 'Runeclaw Bear']};counts={'Grizzly Bears':2,'Runeclaw Bear':1}
  if seats==4:votes[4]=['Runeclaw Bear'];counts={'Grizzly Bears':2,'Runeclaw Bear':2}
 else:raise ValueError(case)
 return source,seats,positions,votes,counts
def run(case):
 source,seats,positions,votes,counts=scenario(case);f=OUT/'native'/case/str(time.time_ns());f.mkdir(parents=True);audit=f/'audit';audit.mkdir();classes=f/'classes';classes.mkdir();checks=0;p=None
 for x in FIX+[Path(__file__)]: (f/x.name).write_bytes(x.read_bytes())
 producer={x.relative_to(RT).as_posix():sha(x) for d in ['classes','lib'] for x in sorted((RT/d).rglob('*')) if x.is_file()}
 result=dict(case=case,folder=f.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driver=sha(Path(__file__)),fixtures={x.name:sha(x) for x in FIX},producerClassesLib=producer,status='RUNNING');wr(f/'result.json',result)
 def ck(ok,msg):
  nonlocal checks
  assert ok,msg;checks+=1
 try:
  with (f/'compile.log').open('w',encoding='utf8') as compile_log:
   subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(f/x.name) for x in FIX]],check=True,stdout=compile_log,stderr=compile_log)
  with tempfile.TemporaryDirectory(prefix='mtgb-vote-') as temp,(f/'stderr.log').open('w',encoding='utf8') as log,(f/'trace.jsonl').open('w',encoding='utf8') as trace:
   cfg=dict(session=case,seed=4,startingLife=30,firstPlayerSeat=1,nativeSeats=seats,decks=[DECK,DECK],positions=positions,auditDir=str(audit),extraVoteInputs={str(s):votes[s] for s in range(3,seats+1)});wr(f/'start.json',cfg)
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.VoteChoiceFixture',str(RT/CFG['assets']),str(f/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
   q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start()
   def observe(r):
    path=audit/(str(r['requestId'])+'.json');end=time.monotonic()+4
    while time.monotonic()<end:
     try:return json.loads(path.read_text())
     except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
    raise AssertionError('missing actual native observation')
   def save(m,label,o=None):wr(f/(label+'-wire.json'),m);wr(f/(label+'-native.json'),o or observe(m['request']))
   initial=None;cast=False;done=False;source_id=0;human={1:0,2:0};vote_seats=[];seen=set();deadline=time.monotonic()+150
   for step in range(500):
    ck(time.monotonic()<deadline,'deadline');m=q.get(timeout=35)
    if isinstance(m,Exception):raise m
    ck(m is not None,'native exited');trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type'] not in ('fatal','ended','rejected'),m)
    if m['type']!='request':continue
    r=m['request']
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];v=m['views'][0];pick='ok'
    if 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif done and case.startswith('plea') and kind=='InputPassPriority' and observe(r)['turn']>1:
     obs=observe(r);ck(obs['turnSeat']==(1 if case=='plea-majority' else 2),'actual next turn follows extra-turn/tie result');save(m,'next-turn',obs);result.update(status='PASS_BOUNDED',checks=checks,requests=len(seen),nativeSeats=seats,expectedVotes=counts,note='Actual paid vote, exact credits/majority/tie/draw or token/zone result and next operator; extra native seats scripted only legal inputs, not production clients.');break
    elif kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
     obs=observe(r)
     if not cast:
      initial=obs;save(m,'initial',obs);src=next(c for c in obs['players'][0]['zones']['Hand'] if c['name']==source);source_id=src['id'];pick='card:'+str(source_id);cast=True
     elif not done:
      save(m,'resolved',obs);ck([p['life'] for p in obs['players']]==[30]*seats,'no accidental life changes');ck(human=={s:len(votes[s]) for s in [1,2]},'exact human ballot count');ck(len(obs['extraVotes'])==sum(len(votes[s]) for s in range(3,seats+1)),'exact extra native input count')
      ck(sum(c['tapped'] for c in obs['players'][0]['zones']['Battlefield'])==(6 if case.startswith('tivit') else 5 if case.startswith('squire') else 3 if case.startswith('judgment') else 4),'actual printed mana payment')
      logs=obs['voteLogs'];ck(len(logs)==(0 if case=='judgment-empty' else 1),'single vote-result event or empty-pool early return')
      if counts:
       lines=logs[0].splitlines();ck(len(lines)==sum(counts.values()),('exact vote event ballots',counts,lines))
       for s in range(1,seats+1):ck(sum(x.startswith('Player '+str(s)+' ') for x in lines)==len(votes[s]),'exact credited original voter'+str(s))
       for label,num in counts.items():ck(sum(label in line for line in lines)==num,('exact credited vote count',label,num,lines))
      if case.startswith('plea'):
       top=[c['id'] for c in initial['players'][0]['zones']['Library']];afterlib=[c['id'] for c in obs['players'][0]['zones']['Library']];hand_before={c['id'] for c in initial['players'][0]['zones']['Hand']};hand_after={c['id'] for c in obs['players'][0]['zones']['Hand']};draw=0 if case=='plea-majority' else 3;ck(afterlib==top[draw:] and hand_after==(hand_before-{source_id})|set(top[:draw]),'exact draw3 original top IDs or no draw');ck(obs['nextTurnSeat']==(1 if case=='plea-majority' else 2),'registered extra-turn only on Time majority')
      elif case.startswith('tivit'):
       old={c['id'] for p in initial['players'] for z in p['zones'].values() for c in z};tokens=[c for c in obs['players'][0]['zones']['Battlefield'] if c['id'] not in old];ck(len(tokens)==sum(counts.values()) and len({c['id'] for c in tokens})==len(tokens),'exact distinct generated token IDs');ck({n:sum(c['name']==n+' Token' for c in tokens) for n in ['Clue','Treasure']}=={'Clue':counts['Evidence'],'Treasure':counts['Bribery']},'StoreVoteNum exact tokens including zero branch');ck(all(c['owner']==c['controller']==1 for c in tokens),'exact token recipient')
      elif case.startswith('judgment'):
       expected={c['id'] for c in initial['players'][1]['zones']['Battlefield'] if c['name'] in counts};ck({c['id'] for p in obs['players'] for c in p['zones']['Exile']}==expected,'all and only tied most original IDs exiled');ck(all(c['id'] not in expected for p in obs['players'] for c in p['zones']['Battlefield']),'exiled targets left battlefield')
      else:
       winners={'Grizzly Bears'} if case=='squire-majority' else {'Grizzly Bears','Runeclaw Bear'};expected={c['id'] for c in initial['players'][0]['zones']['Graveyard'] if c['name'] in winners};ck({c['id'] for c in obs['players'][0]['zones']['Hand']}=={c['id'] for c in initial['players'][0]['zones']['Hand']}-{source_id}|expected,'exact majority/tied grave IDs in hand');ck({c['id'] for c in obs['players'][0]['zones']['Graveyard']}=={c['id'] for c in initial['players'][0]['zones']['Graveyard']}-expected,'exact remaining grave IDs including nonwinners/ineligible')
      dest='Battlefield' if case.startswith(('tivit','squire')) else 'Graveyard';actual=next(c for c in obs['players'][0]['zones'][dest] if c['id']==source_id);ck(not actual['remembered'],'no leaked voted-object memory');ck(r['seat']==r['playerSeat']==1,'next actual operator restored');done=True
      if not case.startswith('plea'):result.update(status='PASS_BOUNDED',checks=checks,requests=len(seen),nativeSeats=seats,expectedVotes=counts,note='Actual paid vote source and exact credited ballots/StoreVoteNum/zone IDs/memory cleanup/operator. Native extra seats only fixture inputs; no four-client UI/transport.');break
    elif kind=='ChooseOptions' and cast:
     seat=r['playerSeat'];ck(seat==r['seat'] and seat in [1,2],'vote chooser correct original human seat');save(m,'vote-'+str(step))
     if r.get('selectedCount'):pick='confirm-cards'
     else:
      desired=votes[seat][human[seat]];choice=next(o for o in opts if o['label'].endswith(desired) or o['label']==desired);pick=choice['id'];human[seat]+=1;vote_seats.append(seat)
    elif ('SelectEntities' in kind or 'SelectCards' in kind) and cast:
     seat=r['playerSeat'];ck(seat==r['seat'] and seat in [1,2],'card vote exact chooser');cards=[o for o in opts if o.get('card')];save(m,'vote-'+str(step));obs=observe(r)
     expected={c['id'] for c in initial['players'][1]['zones']['Battlefield'] if c['name'] in ['Grizzly Bears','Runeclaw Bear','Wall of Roots']} if case.startswith('judgment') else {c['id'] for c in initial['players'][0]['zones']['Graveyard'] if c['name'] in ['Grizzly Bears','Runeclaw Bear','Sol Ring']};ck({o['card']['id'] for o in cards}==expected,'full eligible card pool not actor/self/land/instant or other-owner grave')
     if r.get('selectedCount'):pick='ok'
     else:desired=votes[seat][human[seat]];pick=next(o['id'] for o in cards if o['card']['name']==desired);human[seat]+=1;vote_seats.append(seat)
    elif (kind=='ChooseNumber' or kind=='dialog' and r['message']=='one: How many additional votes do you want?') and case.startswith('tivit'):
     ck(r['seat']==r['playerSeat'] and r['playerSeat'] in [1,2],'optional extra belongs to original voter');desired='0' if case=='tivit-decline' else '1';pick=next(o['id'] for o in opts if o['label']==desired);save(m,'extra-number-'+str(step))
    elif 'PayMana' in kind:pick='ok';save(m,'payment')
    elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in ids else next(i for i in ids if i.startswith('order:'))
    elif kind=='RevealCards':pick='confirm-cards'
    elif kind in ('InputAttack','InputBlock','InputPassPriority'):pick='ok'
    elif 'ok' not in ids:raise AssertionError(('unhandled actual input',r))
    ck(pick in ids and not next(o for o in opts if o['id']==pick).get('disabled'),('legal original response',r,pick));print(case,step,kind,r['seat'],pick,flush=True)
    p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   else:raise AssertionError('step limit')
 except Exception:result.update(status='FAIL',checks=checks,error=traceback.format_exc())
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 wr(f/'result.json',result);wr(OUT/(case+'-result.json'),result);print(result['status'],case,result.get('error',''),flush=True);return result
if __name__=='__main__':
 for case in sys.argv[1:] or ['plea-majority']:run(case)
