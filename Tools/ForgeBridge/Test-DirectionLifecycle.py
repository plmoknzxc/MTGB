"""R03.02 actual direction restriction lifecycle. Native4 seats, supported UI2.
Only opening state is arranged; casting, loyalty, choices, upkeep and expiry are native.
"""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/direction-lifecycle-1009'
FIX=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['RoyalStateFixture.java','DirectionChoiceFixture.java','DirectionLifecycleFixture.java']]
DECK=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
def wr(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def run(case):
 source={'barrier':'Mystic Barrier','pramikon':'Pramikon, Sky Rampart','teyo':'Teyo, Geometric Tactician'}[case]
 f=OUT/'native'/case/str(time.time_ns());f.mkdir(parents=True);audit=f/'audit';audit.mkdir();classes=f/'classes';classes.mkdir();checks=0;p=None
 for x in FIX+[Path(__file__)]: (f/x.name).write_bytes(x.read_bytes())
 producer={x.relative_to(RT).as_posix():sha(x) for d in ['classes','lib'] for x in sorted((RT/d).rglob('*')) if x.is_file()}
 result=dict(case=case,folder=f.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driver=sha(Path(__file__)),fixtures={x.name:sha(x) for x in FIX},producerClassesLib=producer,status='RUNNING');wr(f/'result.json',result)
 def ck(ok,msg):
  nonlocal checks
  assert ok,msg;checks+=1
 def matrix(obs,direction):
  rows=obs['defenders'];ck(len({x['seat'] for x in rows})==4,'all four attackers evaluated against actual native defenders')
  for row in rows:
   expected=True if direction is None or row['kind']=='battle' else row['defenderSeat']==(row['seat']-1+(1 if direction=='Left' else -1))%4+1
   ck(row['directionAllowed']==expected,('direction restriction exact defender/planeswalker/battle',direction,row))
   ck(bool(row['ruleSources'])==(direction is not None),'actual restriction source exists/expired')
   for src in row['ruleSources']:ck(src['direction']==direction,'helper/source copied chosen direction')
  ck(any(r['kind']=='battle' for r in rows) and any(r['kind']=='planeswalker' for r in rows),'actual distinct card-defender exceptions included')
 try:
  subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(f/x.name) for x in FIX]],check=True)
  positions=[]
  for seat in range(1,5):
   field=['Grizzly Bears' if seat<=2 else 'Wall of Roots','Jace Beleren','Invasion of Zendikar','Island']
   if seat==1 and case=='teyo':field+=[source]
   positions.append(dict(Battlefield=field,Hand=[source] if seat==1 and case!='teyo' else [],Library=['Island','Island','Island','Island']))
  with tempfile.TemporaryDirectory(prefix='mtgb-direction-life-') as temp,(f/'stderr.log').open('w',encoding='utf8') as log,(f/'trace.jsonl').open('w',encoding='utf8') as trace:
   cfg=dict(session=case,seed=4,startingLife=30,firstPlayerSeat=1,decks=[DECK,DECK],positions=positions,auditDir=str(audit));wr(f/'start.json',cfg)
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.DirectionLifecycleFixture',str(RT/CFG['assets']),str(f/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
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
    raise AssertionError('missing actual observation')
   def save(m,label,obs=None):wr(f/(label+'-wire.json'),m);wr(f/(label+'-native.json'),obs or observed(m['request']))
   initial=None;working=False;choices=0;source_id=0;restricted=False;attack=False;start_attack_id=0;seen=set();deadline=time.monotonic()+180;turn_seats=[]
   for step in range(600):
    ck(time.monotonic()<deadline,'deadline');m=q.get(timeout=35)
    if isinstance(m,Exception):raise m
    ck(m is not None,'native exited');trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type'] not in ('fatal','ended','rejected'),m)
    if m['type']!='request':continue
    r=m['request']
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];v=m['views'][0]
    # Match.startGame invokes the opening fixture after mulligans; no observer exists yet.
    if 'Mulligan' in kind:
     pick='keep' if 'keep' in ids else 'ok';ck(pick in ids,'actual opening keep')
     p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush();continue
    obs=observed(r);pick='ok'
    if not turn_seats or turn_seats[-1]!=obs['turnSeat']:turn_seats.append(obs['turnSeat'])
    if kind=='InputPassPriority' and r['seat']==1 and obs['turnSeat']==1 and obs['phase']=='MAIN1' and not v['stackItems']:
     if not working:
      initial=obs;matrix(obs,None);save(m,'initial',obs);src=next(c for c in obs['players'][0]['zones']['Battlefield' if case=='teyo' else 'Hand'] if c['name']==source);source_id=src['id'];start_attack_id=next(c['id'] for c in obs['players'][0]['zones']['Battlefield'] if c['name']=='Grizzly Bears');pick='card:'+str(source_id);working=True
     elif not restricted:
      ck(choices==1,'exact initial direction choice');matrix(obs,'Left' if case=='barrier' else 'Right');save(m,'restricted',obs);restricted=True
      src=next(c for c in obs['players'][0]['zones']['Battlefield'] if c['id']==source_id);ck(src['direction']==('Left' if case=='barrier' else 'Right'),'original source stores direction')
      ck(sum(c['tapped'] for c in obs['players'][0]['zones']['Battlefield'])==({'barrier':5,'pramikon':3,'teyo':0}[case]),'actual native spell mana or loyalty-only cost')
      if case=='teyo':ck(src['loyalty']==1,'actual typed loyalty minus2 from3');ck(len(obs['players'][0]['zones']['Command'])==1,'single real copied-direction helper')
     elif obs['turn']>1:
      ck(turn_seats==[1,2,3,4,1],('ordinary native turn wheel unchanged',turn_seats));ck(attack,'actual attack input checked before expiry/rechoose');matrix(obs,'Right' if case=='barrier' else None);save(m,'final',obs)
      ck(choices==(2 if case=='barrier' else 1),'exact rechoice/expiry callback count')
      if case=='teyo':ck(not obs['players'][0]['zones']['Command'],'native until-next-turn helper removed');ck(next(c for c in obs['players'][0]['zones']['Battlefield'] if c['id']==source_id)['direction']=='Right','source direction retained but no longer restricts')
      result.update(status='PASS_BOUNDED',checks=checks,requests=len(seen),turnSeats=turn_seats,choices=choices,actualAttackChecked=attack,note='Actual paid source/loyalty activation and native direction lifecycle/defender legality, ordinary four-seat native wheel; AI3/4 not human clients, no transport claim.');break
    elif kind=='InputConfirm' and r['message']=='Choose a direction':
     ck(working and r['seat']==r['playerSeat']==1,'only source controller chooses');ck([(o['id'],o['label']) for o in opts]==[('ok','Left'),('cancel','Right')],'original Left/Right callback IDs')
     direction='Left' if case=='barrier' and choices==0 else 'Right';pick=next(o['id'] for o in opts if o['label']==direction);save(m,'direction-'+str(choices),obs)
     if case=='barrier' and choices:ck(obs['turn']>1 and obs['phase']=='UPKEEP','rechoose only actual next upkeep')
     if case=='pramikon':ck(not any(c['id']==source_id for c in obs['players'][0]['zones']['Battlefield']),'ETB replacement chooses before source actually enters')
     choices+=1
    elif kind=='InputAttack' and r['seat']==1 and restricted and not attack:
     matrix(obs,'Left' if case=='barrier' else 'Right');expected={row['target'] for row in obs['defenders'] if row['attacker']==start_attack_id and row['actualLegal']};actual={o['defenderId'] for o in opts if o.get('attackerId')==start_attack_id and o['id'].startswith('attack:')};ck(actual==expected and actual,'exact production native attack options follow real legality');save(m,'attack',obs);attack=True
     if case=='pramikon':result.update(status='PASS_BOUNDED',checks=checks,requests=len(seen),choices=choices,actualAttackChecked=True,note='Real URW paid Pramikon ETB replacement and native attack option legal exact defender/planeswalker/battle result; original source/next actor valid. Native4 withAI3/4, not four clients.');break
    elif kind=='dialog' and case=='teyo' and working:pick=next(o['id'] for o in opts if 'Choose left or right' in o['label']);save(m,'loyalty',obs)
    elif 'PayMana' in kind:pick='ok';save(m,'payment',obs)
    elif 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in ids else next(i for i in ids if i.startswith('order:'))
    elif kind=='RevealCards':pick='confirm-cards'
    elif kind in ('InputAttack','InputBlock','InputPassPriority'):pick='ok'
    elif 'SelectCards' in kind or 'SelectEntities' in kind:
     pick='ok' if 'ok' in ids else next(o['id'] for o in opts if o.get('card'));save(m,'native-choice-'+str(step),obs)
    elif 'ok' not in ids:raise AssertionError(('unexpected native choice',r))
    ck(pick in ids and not next(o for o in opts if o['id']==pick).get('disabled'),('legal original input',r,pick));print(case,step,kind,r['seat'],obs['turnSeat'],obs['phase'],pick,flush=True)
    p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   else:raise AssertionError('step limit')
 except Exception:result.update(status='FAIL',checks=checks,error=traceback.format_exc())
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 wr(f/'result.json',result);wr(OUT/(case+'-result.json'),result);print(result['status'],case,result.get('error',''),flush=True);return result
if __name__=='__main__':
 for case in sys.argv[1:] or ['barrier']:run(case)
