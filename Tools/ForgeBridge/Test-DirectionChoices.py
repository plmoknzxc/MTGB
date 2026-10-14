"""R03 direction result increment. Native seats 3/4 use observed ordinary AI.
Opening setup only; every outcome comes from actual paid casts/loyalty activation.
Does not assert production four-player UI/transport support.
"""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime'
CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/direction-choice-1009'
FIX=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['RoyalStateFixture.java','DirectionChoiceFixture.java']]
DECK=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
def write(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def run(case):
 direction='Right' if 'right' in case else 'Left';amin=case.startswith('amin');empty='empty' in case
 source='Aminatou, the Fateshifter' if amin else 'Order of Succession'
 f=OUT/'native'/case/str(time.time_ns());f.mkdir(parents=True);audit=f/'audit';audit.mkdir();classes=f/'classes';classes.mkdir()
 for p in FIX+[Path(__file__)]: (f/p.name).write_bytes(p.read_bytes())
 producer={p.relative_to(RT).as_posix():sha(p) for d in ['classes','lib'] for p in sorted((RT/d).rglob('*')) if p.is_file()}
 result=dict(case=case,folder=f.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driver=sha(Path(__file__)),fixtures={p.name:sha(p) for p in FIX},producerClassesLib=producer,status='RUNNING');write(f/'result.json',result)
 checks=0;p=None
 def check(ok,msg):
  nonlocal checks
  assert ok,msg;checks+=1
 try:
  subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(f/p.name) for p in FIX]],check=True)
  positions=[]
  for seat in range(1,5):
   field=[] if empty and seat==3 else ['Grizzly Bears','Runeclaw Bear']
   field+=['Island']
   if amin:field+=['Sol Ring','Glorious Anthem']
   if seat==1 and amin:field+=[source]
   positions.append(dict(Battlefield=field,Hand=[] if seat!=1 or amin else [source],Library=['Island','Plains','Forest','Mountain']))
  with tempfile.TemporaryDirectory(prefix='mtgb-direction-') as temp,(f/'stderr.log').open('w',encoding='utf8') as log,(f/'trace.jsonl').open('w',encoding='utf8') as trace:
   cfg=dict(session=case,seed=4,startingLife=30,firstPlayerSeat=1,decks=[DECK,DECK],positions=positions,auditDir=str(audit));write(f/'start.json',cfg)
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.DirectionChoiceFixture',str(RT/CFG['assets']),str(f/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
   q=queue.Queue()
   def reader():
    try:
     for line in p.stdout:q.put(json.loads(line))
    except Exception as e:q.put(e)
    finally:q.put(None)
   threading.Thread(target=reader,daemon=True).start()
   def observed(r):
    path=audit/(str(r['requestId'])+'.json');end=time.monotonic()+4
    while time.monotonic()<end:
     try:return json.loads(path.read_text())
     except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
    raise AssertionError('Missing observation '+str(r['requestId']))
   def save(m,name):write(f/(name+'-wire.json'),m)
   initial=None;cast=False;chose=False;human=[];seen=set();source_id=0;deadline=time.monotonic()+180
   for step in range(350):
    check(time.monotonic()<deadline,'deadline');m=q.get(timeout=35)
    if isinstance(m,Exception):raise m
    check(m is not None,'process exited');trace.write(json.dumps(m)+'\n');trace.flush();check(m['type'] not in ('fatal','ended','rejected'),m)
    if m['type']!='request':continue
    r=m['request']
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);v=m['views'][0];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];pick='ok'
    if kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
     obs=observed(r)
     if not cast:
      initial=obs;save(m,'initial');src=next(c for c in v['players'][0]['battlefield' if amin else 'hand'] if c['name']==source);source_id=src['id'];pick='card:'+str(source_id);cast=True
     else:
      check(chose,'real direction chooser missing');save(m,'resolved');write(f/'final-observation.json',obs)
      before=[c for pv in initial['players'] for c in pv['zones']['Battlefield']];after=[c for pv in obs['players'] for c in pv['zones']['Battlefield']];index={c['id']:c for c in after};old={c['id']:c for c in before};shift=1 if direction=='Left' else -1
      nxt=lambda s:(s-1+shift)%4+1
      prev=lambda s:(s-1-shift)%4+1
      check([pv['life'] for pv in obs['players']]==[30]*4,'no life changes');check({c['id'] for c in after}=={c['id'] for c in before},'exact field identities')
      check([pv['seat'] for pv in obs['players']]==[1,2,3,4] and v['turnSeat']==1 and v['turn']==1,'turn order/turn unchanged')
      if amin:
       check(not human and not obs['aiChoices'],'simultaneous all transfer must not ask per-player selector')
       for c in before:
        expected=c['controller'] if c['name'] in ['Plains','Island','Swamp','Mountain','Forest'] or c['id']==source_id else prev(c['controller'])
        check(index[c['id']]['controller']==expected,('exact original next-player transfer',c,expected,index[c['id']]))
       check(index[source_id]['counters']=='[LOYALTY x 2]' and index[source_id]['direction']==direction,'real minus6 and retained chosen direction')
       check(all(not c['tapped'] for c in after),'loyalty activation did not pay mana')
      else:
       events=sorted(human+obs['aiChoices'],key=lambda e:e['time']);order=[1]
       for _ in range(3):order.append(nxt(order[-1]))
       chosen={};actual=[]
       for e in events:
        s=e['seat'];expected={c['id'] for c in before if c['controller']==nxt(s) and c['name'] in ['Grizzly Bears','Runeclaw Bear']}
        check(set(e['options'])==expected,('exact next-controller candidates',s,expected,e))
        check(all(c['controller']==old[c['id']]['controller'] for c in e['fieldBeforeTransfer']),'selection caused premature transfer')
        if expected:check(e['chosen'] in expected,'chosen original eligible identity');chosen[e['chosen']]=s
        else:check(e['chosen']==0,'empty next player caused phantom choice')
        actual.append(s)
       # Human empty choice is automatically skipped by native helper, AI empty calls recorded if invoked.
       expected_order=[s for s in order if any(c['controller']==nxt(s) and c['name'] in ['Grizzly Bears','Runeclaw Bear'] for c in before)]
       check([s for s in actual if any(e['seat']==s and e['options'] for e in events)]==expected_order,('exact direction chooser order',expected_order,actual))
       for c in before:check(index[c['id']]['controller']==chosen.get(c['id'],c['controller']),('only selected IDs transfer',c,index[c['id']]))
       check(sum(c['tapped'] for c in after)==4,'actual3U paid by four original lands')
       grave=obs['players'][0]['zones']['Graveyard'];check(any(c['id']==source_id for c in grave),'actual spell final grave identity')
      check(r['seat']==r['playerSeat']==1,'next original actor restored');result.update(status='PASS_BOUNDED',checks=checks,requests=len(seen),direction=direction,humanChoices=len(human),aiChoices=len(obs['aiChoices']),note='Actual paid operation, exact four-native-seat direction/result/IDs and next actor. AI seats3/4 are read-only observed, not human clients or Commander UI.')
      break
    elif kind=='InputConfirm' and cast and not chose:
     check(r['seat']==r['playerSeat']==1,'only activator chooses direction');check(set(o['label'] for o in opts)=={'Left','Right'},('actual Left/Right labels',opts));pick=next(o['id'] for o in opts if o['label']==direction);save(m,'direction');observed(r);chose=True
    elif ('SelectEntities' in kind or 'SelectCards' in kind) and chose:
     obs=observed(r);save(m,'choice-'+str(step));candidates=[o for o in opts if o.get('card')]
     if r.get('selectedCount'):pick='ok'
     else:
      check(not amin and candidates,'unexpected selector');pick=candidates[0]['id'];human.append(dict(time=obs['time'],seat=r['playerSeat'],options=[o['card']['id'] for o in candidates],chosen=candidates[0]['card']['id'],fieldBeforeTransfer=[c for pv in obs['players'] for c in pv['zones']['Battlefield']]))
    elif amin and kind=='dialog':pick=next(o['id'] for o in opts if '[-6]' in o['label'] or '-6:' in o['label'] or 'Choose left or right' in o['label']);save(m,'ultimate')
    elif 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif 'PayMana' in kind:pick='ok';save(m,'payment')
    elif kind=='RevealCards':pick='confirm-cards'
    elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in ids else next(i for i in ids if i.startswith('order:'))
    elif kind=='dialog':pick=ids[0]
    elif 'ok' not in ids:raise AssertionError(('unhandled',r))
    check(pick in ids and not next(o for o in opts if o['id']==pick).get('disabled'),('legal actual choice',r,pick));print(case,step,kind,r['seat'],pick,flush=True)
    p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   else:raise AssertionError('step limit')
 except Exception:result.update(status='FAIL',checks=checks,error=traceback.format_exc())
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 write(f/'result.json',result);write(OUT/(case+'-result.json'),result);print(result['status'],case,result.get('error',''),flush=True);return result
if __name__=='__main__':
 for case in sys.argv[1:] or ['order-left']:run(case)
