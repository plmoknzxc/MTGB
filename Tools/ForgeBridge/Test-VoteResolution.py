"""R03 actual object votes, optional abstention and EachVote native followups.
Opening-only fixture, legal input driver and independent read-only observations.
Extra native seats are not production human/network clients.
"""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/vote-resolution-1009';FIX=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['RoyalStateFixture.java','VoteResolutionFixture.java']];DECK=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
def wr(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def cards(o,z):return {c['id']:c for p in o['players'] for c in p['zones'][z]}
def scenario(case):
 assert case in ['trap','trap-controlled','vault-mixed','expropriate','elrond']
 controlled=case=='trap-controlled'
 if controlled:case='trap'
 source={'trap':'Trap the Trespassers','vault-mixed':"Vault 11: Voter's Dilemma",'expropriate':'Expropriate','elrond':'Elrond of the White Council'}[case]
 pos=[dict(Battlefield=[],Hand=[],Graveyard=[],Library=['Island','Plains','Forest','Mountain','Swamp']) for s in [1,2,3]];pos[0]['Hand']=[source];pos[0]['Battlefield']=['Llanowar Elves'];pos[1]['Battlefield']=['Grizzly Bears','Runeclaw Bear','Wall of Roots'];pos[2]['Battlefield']=['Wall of Roots']
 votes={'trap':{1:['Grizzly Bears'],2:['Grizzly Bears'],3:['Runeclaw Bear']},'vault-mixed':{1:['ABSTAIN','Grizzly Bears'],2:['ABSTAIN','Runeclaw Bear'],3:['ABSTAIN','ABSTAIN']},'expropriate':{1:['Time'],2:['Money'],3:['Money']},'elrond':{1:['Aid'],2:['Fellowship'],3:['Aid']}}[case]
 if case=='elrond':pos[1]['Battlefield'].append('Raging Goblin')
 borrowed=[dict(name='Sol Ring',owner=2,controller=3)] if case=='expropriate' else []
 if controlled:
  pos[0]['Hand'].insert(0,'Illusion of Choice');votes={1:['Grizzly Bears','Grizzly Bears','Runeclaw Bear'],2:[],3:[]}
 return source,pos,votes,borrowed
def run(case):
 source,pos,votes,borrowed=scenario(case);f=OUT/'native'/case/str(time.time_ns());f.mkdir(parents=True);audit=f/'audit';audit.mkdir();classes=f/'classes';classes.mkdir();checks=0;p=None
 for x in FIX+[Path(__file__)]: (f/x.name).write_bytes(x.read_bytes())
 result=dict(case=case,folder=f.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driver=sha(Path(__file__)),fixtures={x.name:sha(x) for x in FIX},producerClassesLib={x.relative_to(RT).as_posix():sha(x) for d in ['classes','lib'] for x in sorted((RT/d).rglob('*')) if x.is_file()},status='RUNNING');wr(f/'result.json',result)
 def ck(ok,msg):
  nonlocal checks
  assert ok,msg;checks+=1
 try:
  with (f/'compile.log').open('w',encoding='utf8') as log:subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(f/x.name) for x in FIX]],check=True,stdout=log,stderr=log)
  with tempfile.TemporaryDirectory(prefix='mtgb-vote-resolution-') as temp,(f/'stderr.log').open('w',encoding='utf8') as log,(f/'trace.jsonl').open('w',encoding='utf8') as trace:
   cfg=dict(session=case,seed=4,startingLife=30,firstPlayerSeat=1,nativeSeats=3,decks=[DECK,DECK],positions=pos,borrowed=borrowed,auditDir=str(audit),extraVoteInputs={'3':votes[3]});wr(f/'start.json',cfg)
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.VoteResolutionFixture',str(RT/CFG['assets']),str(f/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
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
   initial=None;before_vote={};cast=0;controlled=case=='trap-controlled';human={1:0,2:0};sid=0;seen=set();deadline=time.monotonic()+200;chosen=[];followup=0;vault_checked_zero=False;resolved=None
   for step in range(700):
    ck(time.monotonic()<deadline,'deadline');m=q.get(timeout=35)
    if isinstance(m,Exception):raise m
    ck(m is not None,'native exited');trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type'] not in ('fatal','ended','rejected'),m)
    if m['type']!='request':continue
    r=m['request'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];v=m['views'][0];pick='ok'
    if r['requestId'] in seen:continue
    seen.add(r['requestId'])
    if 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif kind=='InputPassPriority' and r['seat']==1 and v['turnSeat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
     o=obs(r)
     if cast<(2 if controlled else 1):
      if not cast:initial=o;save(m,'initial',o)
      want='Illusion of Choice' if controlled and not cast else source;c=next(c for c in o['players'][0]['zones']['Hand'] if c['name']==want);pick='card:'+str(c['id']);cast+=1
      if want==source:sid=c['id']
     elif case=='vault-mixed' and o['turn']==1:save(m,'chapter-one',o)
     elif case=='vault-mixed' and len(o['voteLogs'])==0:pass
     elif case=='vault-mixed' and len(o['voteLogs'])==1:
      if not vault_checked_zero:
       ck(len(o['voteLogs'])==1 and o['voteLogs'][0]=='','all abstained ballot event empty');base=before_vote[2]
       for s in [1,2,3]:
        old=base['players'][s-1]['zones'];now=o['players'][s-1]['zones'];top=[c['id'] for c in old['Library']];ck([c['id'] for c in now['Library']]==top[1:],'zero vote exact top draw seat'+str(s));ck({c['id'] for c in now['Hand']}=={c['id'] for c in old['Hand']}|set(top[:1]),'zero vote hand identities');ck(set(cards(base,'Battlefield'))==set(cards(o,'Battlefield')),'all abstain no destroy')
       ck(cards(o,'Battlefield')[sid]['lore']==2,'native chapterII not injected');save(m,'chapter-two',o);vault_checked_zero=True
     elif case=='expropriate' and resolved is not None and o['turn']==2:
      ck(o['turnSeat']==1,'actual voted extra turn entered');save(m,'extra-turn',o);result.update(status='PASS_BOUNDED',checks=checks,requests=len(seen),note='Actual EachVote-owned followup/control and extra turn');break
     elif case=='expropriate' and resolved is not None:pass
     else:
      save(m,'resolved',o);resolved=o;ck(r['seat']==r['playerSeat']==1,'next operator restored');ck(r.get('votingPlayerSeat',0)==0 and not r.get('secretVote',False),'vote metadata scope restored');ck(human=={s:len(votes[s]) for s in [1,2]},'all actual human ballots consumed');ck(all(p['life']==30 for p in o['players']),'unaffected life')
      if case in ['trap','trap-controlled']:
       ck(len(o['voteLogs'])==1 and len(o['voteLogs'][0].splitlines())==3,'all three exact ballots revealed once');bf=cards(o,'Battlefield');old=cards(initial,'Battlefield')
       for id,c in old.items():
        n=2 if c['name']=='Grizzly Bears' else 1 if c['name']=='Runeclaw Bear' else 0;ck(bf[id]['stun']==n and bf[id]['tapped']==(n>0 or c['name'] in ['Island','Plains','Mountain'] and bf[id]['tapped']),'voted-only exact stun and tap');ck(bf[id]['controller']==c['controller'] and bf[id]['owner']==c['owner'],'all owners/controllers unchanged')
       ck(set(bf)==set(old),'no native object generated/lost');ck(sum(c['tapped'] for c in o['players'][0]['zones']['Battlefield'])==(4 if controlled else 3),'actual2U plus controlled U paid');ck(not cards(o,'Graveyard')[sid]['remembered'],'remembered voted objects cleared')
       if controlled:ck(not o['extraVotes'] and all(sum(line.startswith('Player '+str(s)+' ') for line in o['voteLogs'][0].splitlines())==1 for s in [1,2,3]),'controlled chooser1 original credit1/2/3, no fixture AI ballot')
      elif case=='vault-mixed':
       ck(vault_checked_zero and len(o['voteLogs'])==2 and len(o['voteLogs'][1].splitlines())==2,'two natural chapter vote outcomes');base=before_vote[3];dead={c['id'] for c in cards(base,'Battlefield').values() if c['name'] in ['Grizzly Bears','Runeclaw Bear']}|{sid};ck(set(cards(o,'Battlefield'))==set(cards(base,'Battlefield'))-dead,'all and only tied highest creatures destroyed and sagaIII sacrificed');ck(dead<=set(cards(o,'Graveyard')),'exact native grave IDs');ck(not cards(o,'Graveyard')[sid]['remembered'],'Saga remembered highest cleanup')
       for s in [1,2,3]:ck([c['id'] for c in o['players'][s-1]['zones']['Library']]==[c['id'] for c in base['players'][s-1]['zones']['Library']],'no draw with votes seat'+str(s))
      elif case=='expropriate':
       ck(followup==2 and len(chosen)==1,'Money original voter2 full pool choice selected confirmed');old=cards(initial,'Battlefield');bf=cards(o,'Battlefield');automatic=[id for id,c in old.items() if c['owner']==3];ck(len(automatic)==1,'Money original voter3 mandatory sole owned permanent needs no input');chosen+=automatic
       for id,c in old.items():ck(bf[id]['owner']==c['owner'] and bf[id]['controller']==(1 if id in chosen else c['controller']),'only exact owned choices gain control')
       ck(cards(o,'Exile')[sid]['name']==source and not cards(o,'Exile')[sid]['remembered'],'source exiled memory cleanup');ck(o['nextTurnSeat']==1,'Time vote schedules extra turn');ck(sum(c['tapped'] for c in o['players'][0]['zones']['Battlefield'])==9,'actual7UUpaid')
      else:
       bf=cards(o,'Battlefield');goblin=next(c for c in cards(initial,'Battlefield').values() if c['name']=='Raging Goblin');ck(followup==2 and chosen==[goblin['id']],'original voter Fellowship own choice exact');ck(bf[goblin['id']]['owner']==2 and bf[goblin['id']]['controller']==1,'gain control retains original owner');ck(bf[goblin['id']]['canAttack']=={'2':False,'3':True},'real haste creature forbidden owner permitted other opponent');ck(all(c['p1p1']==(2 if c['controller']==1 and c['name'] in ['Llanowar Elves',source,'Raging Goblin'] else 0) for c in bf.values()),'two Aid votes all current own creatures only');src=bf[sid];ck(not src['remembered'] and not src['imprinted'] and src['aid']=='Number$0','all scoped memory/imprinted/Aid cleared');ck(sum(c['tapped'] for c in o['players'][0]['zones']['Battlefield'])==5,'actual3GUpaid')
      if case!='expropriate':result.update(status='PASS_BOUNDED',checks=checks,requests=len(seen),note='Actual paid/native object-count/optional abstain/EachVote results, original IDs and scoped memory/operator. Extra native3 legal fixture inputs only.');break
    elif cast and (kind=='ChooseOptions' or 'SelectEntities' in kind or 'SelectCards' in kind):
     o=obs(r);save(m,'choice-'+str(step),o);seat=r['seat'];is_vote=r.get('votingPlayerSeat',0)>0
     if is_vote:
      ck(seat==r['playerSeat']==(1 if controlled else r['votingPlayerSeat']),'correct actual chooser');ck(r['votingPlayerSeat']==human[1]+1 if controlled else r['votingPlayerSeat']==seat,'correct original human voter');ck(r.get('secretVote')==(case!='expropriate'),'typed secrecy exact');stage=cards(o,'Battlefield')[sid]['lore'] if case=='vault-mixed' else 1
      if stage not in before_vote:before_vote[stage]=o;save(m,'before-vote-'+str(stage),o)
      expected_logs=1 if case=='vault-mixed' and stage==3 else 0;ck(len(o['voteLogs'])==expected_logs,'no collective vote before all ballots');ck(not any(c['stun'] for c in cards(o,'Battlefield').values()),'no early stun effect')
      choices=[x for x in opts if x['id'].startswith('select:') or x.get('card')]
      if case in ['trap','trap-controlled','vault-mixed']:
       expected={id for id,c in cards(o,'Battlefield').items() if c['name'] in ['Grizzly Bears','Runeclaw Bear','Wall of Roots','Llanowar Elves','Human Soldier Token'] and (case not in ['trap','trap-controlled'] or c['controller']!=1)};ck({x['card']['id'] for x in choices}==expected,'full exact native creature pool')
      desired=votes[seat][human[seat]]
      if desired=='ABSTAIN':pick='cancel' if 'cancel' in ids else 'ok';human[seat]+=1;ck(case=='vault-mixed','only UpTo abstains')
      elif r.get('selectedCount'):pick='confirm-cards' if 'confirm-cards' in ids else 'ok';human[seat]+=1
      else:pick=next(x['id'] for x in choices if x.get('card',{}).get('name')==desired or x['label'].strip().endswith(desired))
     else:
      ck(case in ['expropriate','elrond'] and len(o['voteLogs'])==1,'each-vote followup only after collected all');ck(not r.get('secretVote',False),'vote identity cleared for ordinary followup');optscards=[x for x in opts if x.get('card')]
      if case=='expropriate':
       credited=2 if len(chosen)==0 else 3 if r.get('selectedCount',0)==0 else cards(initial,'Battlefield')[chosen[-1]]['owner'];ck(seat==r['playerSeat']==1,'caster chooses owned permanent for Money');expected={id for id,c in cards(o,'Battlefield').items() if c['owner']==credited};ck({x['card']['id'] for x in optscards}==expected,'owned voter pool, not controlled by voter');name='Sol Ring' if credited==2 else 'Wall of Roots';target=next(x for x in optscards if x['card']['name']==name)
      else:
       ck(seat==r['playerSeat']==2,'Fellowship original voter chooses');expected={id for id,c in cards(o,'Battlefield').items() if c['controller']==2};ck({x['card']['id'] for x in optscards}==expected,'own Fellowship creature pool');target=next(x for x in optscards if x['card']['name']=='Raging Goblin')
      if r.get('selectedCount'):pick='ok'
      else:pick=target['id'];chosen.append(target['card']['id'])
      followup+=1
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
 for case in sys.argv[1:] or ['trap']:run(case)
