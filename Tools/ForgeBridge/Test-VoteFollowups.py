"""R03 actual secret/player/controlled votes. Opening-only fixture and read-only
native observation; scripted extra seats only provide legal input, not clients.
"""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/vote-followup-1009';FIX=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['RoyalStateFixture.java','VoteFollowupFixture.java']];DECK=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
def wr(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def scenario(case):
 source='Círdan the Shipwright' if case=='cirdan' else 'Mob Verdict'
 pos=[dict(Battlefield=[],Hand=[],Graveyard=[],Library=['Island','Plains','Forest','Mountain','Swamp']) for s in [1,2,3]];pos[0]['Hand']=[source];pos[0]['Battlefield']=['Llanowar Elves'];pos[1]['Battlefield']=['Grizzly Bears','Wall of Roots'];pos[2]['Battlefield']=['Wall of Roots']
 votes={1:['Player 1'],2:['Player 1'],3:['Player 3']} if case=='cirdan' else {1:['Player 2'],2:['Player 1'],3:['Player 2']}
 if case=='cirdan':pos[1]['Hand']=['Runeclaw Bear','Sol Ring','Lightning Bolt']
 if case.startswith('mob-controlled'):
  pos[0]['Hand'].insert(0,'Illusion of Choice');votes={1:['Player 2','Player 1','Player 1'],2:[],3:[]}
  if case=='mob-controlled-extra':pos[1]['Battlefield'].append('Ballot Broker');votes[1].append('Player 1')
 return source,pos,votes
def run(case):
 source,pos,votes=scenario(case);f=OUT/'native'/case/str(time.time_ns());f.mkdir(parents=True);audit=f/'audit';audit.mkdir();classes=f/'classes';classes.mkdir();checks=0;p=None
 for x in FIX+[Path(__file__)]: (f/x.name).write_bytes(x.read_bytes())
 producer={x.relative_to(RT).as_posix():sha(x) for d in ['classes','lib'] for x in sorted((RT/d).rglob('*')) if x.is_file()};result=dict(case=case,folder=f.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driver=sha(Path(__file__)),fixtures={x.name:sha(x) for x in FIX},producerClassesLib=producer,status='RUNNING');wr(f/'result.json',result)
 def ck(ok,msg):
  nonlocal checks
  assert ok,msg;checks+=1
 try:
  with (f/'compile.log').open('w',encoding='utf8') as log:subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(f/x.name) for x in FIX]],check=True,stdout=log,stderr=log)
  with tempfile.TemporaryDirectory(prefix='mtgb-vote-followup-') as temp,(f/'stderr.log').open('w',encoding='utf8') as log,(f/'trace.jsonl').open('w',encoding='utf8') as trace:
   cfg=dict(session=case,seed=4,startingLife=30,firstPlayerSeat=1,nativeSeats=3,decks=[DECK,DECK],positions=pos,auditDir=str(audit),extraVoteInputs={'3':votes[3]});wr(f/'start.json',cfg)
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.VoteFollowupFixture',str(RT/CFG['assets']),str(f/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
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
   initial=None;before_vote=None;cast=0;human={1:0,2:0};sid=0;seen=set();deadline=time.monotonic()+140;voter=0;followup=0;optional_extra=0
   for step in range(500):
    ck(time.monotonic()<deadline,'deadline');m=q.get(timeout=35)
    if isinstance(m,Exception):raise m
    ck(m is not None,'native exited');trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type'] not in ('fatal','ended','rejected'),m)
    if m['type']!='request':continue
    r=m['request'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];v=m['views'][0];pick='ok'
    if r['requestId'] in seen:continue
    seen.add(r['requestId'])
    if 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
     o=obs(r)
     if not cast:initial=o;save(m,'initial',o)
     if case.startswith('mob-controlled') and cast==1 and any(c['name']==source for c in o['players'][0]['zones']['Hand']):save(m,'controlled',o);before_vote=o
     if cast<(2 if case.startswith('mob-controlled') else 1):
      want='Illusion of Choice' if case.startswith('mob-controlled') and cast==0 else source;c=next(c for c in o['players'][0]['zones']['Hand'] if c['name']==want);pick='card:'+str(c['id']);cast+=1
      if want==source:sid=c['id']
     else:
      save(m,'resolved',o);ck(r.get('votingPlayerSeat',0)==0 and not r.get('secretVote',False),'vote metadata cleared after callback');ck(human=={s:len(votes[s]) for s in [1,2]},'exact human votes');ck(len(o['voteLogs'])==1,'one collective vote event');lines=o['voteLogs'][0].splitlines();extra=case=='mob-controlled-extra';ck(len(lines)==(4 if extra else 3),'all credited ballots');ck(all(sum(l.startswith('Player '+str(s)+' ') for l in lines)==(2 if extra and s==2 else 1) for s in [1,2,3]),'exact original credit per native voter even controlled');ck(optional_extra==(1 if extra else 0),'extra vote number chosen by original player separately');ck(r['seat']==r['playerSeat']==1,'next operator restored');ck(o['turnSeat']==1 and o['turn']==1,'turn unchanged through resolution')
      base=before_vote or initial;counts={1:2,2:0,3:1} if case=='cirdan' else {1:3 if extra else 2,2:1,3:0} if case.startswith('mob-controlled') else {1:1,2:2,3:0}
      for seat in [1,2,3]:
       expected=counts[seat] if case=='cirdan' or seat==1 else 0;lib=[c['id'] for c in base['players'][seat-1]['zones']['Library']];actual=[c['id'] for c in o['players'][seat-1]['zones']['Library']];ck(actual==lib[expected:],'exact original top draw IDs seat'+str(seat));hand={c['id'] for c in o['players'][seat-1]['zones']['Hand']};old={c['id'] for c in base['players'][seat-1]['zones']['Hand']};removed={sid} if seat==1 else {next(c['id'] for c in initial['players'][1]['zones']['Hand'] if c['name']=='Runeclaw Bear')} if case=='cirdan' and seat==2 else set();ck(hand==(old-removed)|set(lib[:expected]),'exact hand identities seat'+str(seat))
       ck(o['players'][seat-1]['life']==(30 if case=='cirdan' or seat==1 else 30-counts[seat]*2),'exact native player damage from votes seat'+str(seat))
      if case=='cirdan':
       ck(followup==2,'zero-vote optional hand picker selected/confirmed once');chosen=next(c for c in initial['players'][1]['zones']['Hand'] if c['name']=='Runeclaw Bear');ck(any(c['id']==chosen['id'] and c['owner']==c['controller']==2 for c in o['players'][1]['zones']['Battlefield']),'zero-vote original permanent enters for correct player')
      else:
       for seat in [1,2,3]:
        dmg=0 if seat==1 else counts[seat]*2;old=initial['players'][seat-1]['zones']['Battlefield'];final={c['id']:c for c in o['players'][seat-1]['zones']['Battlefield']};grave={c['id'] for c in o['players'][seat-1]['zones']['Graveyard']}
        for c in old:
         dies=c['name']=='Grizzly Bears' and dmg>=2
         if dies:ck(c['id'] in grave and c['id'] not in final,'lethal voted damage original creature dies')
         else:ck(c['id'] in final and final[c['id']]['damage']==(dmg if c['name'] in ['Wall of Roots','Llanowar Elves','Ballot Broker'] else 0),'exact surviving original creature damage/unaffected')
      dest='Battlefield' if case=='cirdan' else 'Graveyard';src=next(c for c in o['players'][0]['zones'][dest] if c['id']==sid);ck(not src['remembered'],'scoped memory cleanup');ck(sum(c['tapped'] for c in o['players'][0]['zones']['Battlefield'])==(5 if case=='cirdan' or case.startswith('mob-controlled') else 4),'actual printed mana paid')
      result.update(status='PASS_BOUNDED',checks=checks,requests=len(seen),note='Actual secret player votes/self/Other/control credits and original pools, exact native draw/damage/zero-vote followup/memory/resume. Extra native3 scripted legal input, not production client.');break
    elif cast and (kind=='ChooseOptions' or 'SelectEntities' in kind or 'SelectCards' in kind):
     o=obs(r);save(m,'choice-'+str(step),o);seat=r['seat'];playeropts=[x for x in opts if x.get('playerSeat') or x['label'].startswith('Player ')]
     if playeropts:
      credited=([1,2,2,3][voter] if case=='mob-controlled-extra' else voter+1) if case.startswith('mob-controlled') else seat;expected=set([1,2,3]) if case=='cirdan' else set([1,2,3])-{credited};actual={int(x['label'].split()[-1]) for x in playeropts};ck(actual==expected,('Other eligibility against original credited voter',credited,actual,expected,r));ck(r.get('votingPlayerSeat')==credited and r.get('secretVote') and r.get('sourceCardId')==sid,'typed actual original voter/secret/public source metadata');ck(not o['voteLogs'],'no collective reveal before all votes')
      for view in m['views']:ck(not any('Current Votes' in (n.get('message','')+n.get('value','')) or 'voted for' in (n.get('message','')+n.get('value','')) for n in view.get('notices',[])),'no early secret vote notification in supported views')
      if r.get('selectedCount'):pick='confirm-cards' if 'confirm-cards' in ids else 'ok';voter+=1
      else:want=votes[seat][human[seat]];pick=next(x['id'] for x in playeropts if x['label']==want);human[seat]+=1
     else:
      ck(case=='cirdan' and seat==r['playerSeat']==2 and len(o['voteLogs'])==1,'zero votes own hand followup after collective reveal');ck(r.get('votingPlayerSeat',0)==0 and not r.get('secretVote',False),'vote context not leaked into private followup');cards=[x for x in opts if x.get('card')];expected={c['id'] for c in initial['players'][1]['zones']['Hand'] if c['name']!='Lightning Bolt'};ck({x['card']['id'] for x in cards}==expected,'all and only original permanent hand options')
      pick='ok' if r.get('selectedCount') else next(x['id'] for x in cards if x['card']['name']=='Runeclaw Bear');followup+=1
    elif kind=='dialog' and case=='mob-controlled-extra':
     save(m,'optional-extra-'+str(step));ck(r['seat']==r['playerSeat']==2 and r.get('votingPlayerSeat',0)==0 and not r.get('secretVote',False),'original player alone chooses optional extra number before controlled ballots');ck({x['label'] for x in opts}=={'0','1'},'native optional0..1 pool');pick=next(x['id'] for x in opts if x['label']=='1');optional_extra+=1
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
 wr(f/'result.json',result);wr(OUT/(case+'-result.json'),result);print(result['status'],case,result.get('error',''),flush=True);return result
if __name__=='__main__':
 for case in sys.argv[1:] or ['cirdan']:run(case)
