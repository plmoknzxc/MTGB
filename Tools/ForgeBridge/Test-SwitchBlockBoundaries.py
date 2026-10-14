"""R03 original SwitchBlock boundaries, actual actions only; old proofs retained."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/switchblock-boundary-1010';FIX=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['RoyalStateFixture.java','CombatChoiceFixture.java']];DECK=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
SPECS={
 'jarkeld-band':(['Benalish Hero','Grizzly Bears'],['Wall of Roots','General Jarkeld']),
 'jarkeld-multiple':(['Giant Spider','Centaur Courser'],['Wall of Roots','Wall of Air','Grizzly Bears','General Jarkeld']),
 'sorrow-one-gone':(['Giant Spider','Centaur Courser',"Sorrow's Path"],['Grizzly Bears','Wall of Roots']),
 'sorrow-illegal':(['Air Elemental','Centaur Courser',"Sorrow's Path"],['Wall of Air','Wall of Roots']),
 'sorrow-multiple':(['Giant Spider','Horned Turtle','Centaur Courser',"Sorrow's Path"],['Wall of Glare','Palace Guard']),
 'sorrow-additional':(['Giant Spider','Horned Turtle','Centaur Courser',"Sorrow's Path"],['Wall of Glare','Wall of Roots','Brave the Sands'])}
def wr(p,v):p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def card(o,seat,name,zone='Battlefield'):return next(c for c in o['players'][seat-1]['zones'][zone] if c['name']==name)
def relation(o):return {x['attacker']:set(x['blockers']) for x in o['matrix']}
def run(case):
 f=OUT/'native'/case/str(time.time_ns());f.mkdir(parents=True);audit=f/'audit';audit.mkdir();classes=f/'classes';classes.mkdir();checks=0;p=None
 for x in FIX+[Path(__file__)]: (f/x.name).write_bytes(x.read_bytes())
 result=dict(case=case,folder=f.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driver=sha(Path(__file__)),fixtures={x.name:sha(x) for x in FIX},producerClassesLib={x.relative_to(RT).as_posix():sha(x) for d in ['classes','lib'] for x in sorted((RT/d).rglob('*')) if x.is_file()},status='RUNNING');wr(f/'result.json',result)
 def ck(ok,msg):
  nonlocal checks
  assert ok,msg;checks+=1
 try:
  with (f/'compile.log').open('w',encoding='utf8') as log:subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(f/x.name) for x in FIX]],check=True,stdout=log,stderr=log)
  with tempfile.TemporaryDirectory(prefix='mtgb-switch-boundary-') as temp,(f/'stderr.log').open('w',encoding='utf8') as log,(f/'trace.jsonl').open('w',encoding='utf8') as trace:
   a,b=SPECS[case];jarkeld=case.startswith('jarkeld');gone=case.endswith('one-gone');band=case.endswith('band');own=dict(Battlefield=a+['Mountain'],Hand=['Lightning Bolt'] if gone else ['Negate']);other=dict(Battlefield=b,Hand=['Negate']);cfg=dict(session=case,seed=4,fixtureSeed=4,startingLife=30,firstPlayerSeat=1,decks=[DECK,DECK],own=own,other=other,auditDir=str(audit));wr(f/'start.json',cfg)
   p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.CombatChoiceFixture',str(RT/CFG['assets']),str(f/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
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
   initial=None;before=None;switched=None;seen=set();attacked=set();pairs=set();activated=False;bolt=False;targets=set();bandsteps=0;allocations=[];deadline=time.monotonic()+180
   recipient='Wall of Roots' if case=='sorrow-additional' else 'Palace Guard'
   wanted=[('Benalish Hero','Wall of Roots')] if band else [('Giant Spider','Wall of Roots'),('Giant Spider','Wall of Air'),('Centaur Courser','Grizzly Bears')] if case=='jarkeld-multiple' else [('Giant Spider','Grizzly Bears'),('Centaur Courser','Wall of Roots')] if gone else [('Air Elemental','Wall of Air'),('Centaur Courser','Wall of Roots')] if case=='sorrow-illegal' else [('Giant Spider','Wall of Glare'),('Horned Turtle','Wall of Glare'),('Centaur Courser',recipient)]
   for step in range(500):
    ck(time.monotonic()<deadline,'deadline');m=q.get(timeout=35)
    if isinstance(m,Exception):raise m
    ck(m is not None,'native exited');trace.write(json.dumps(m)+'\n');trace.flush();ck(m['type'] not in ('fatal','ended','rejected'),m)
    if m['type']!='request':continue
    r=m['request'];opts=r['options'];ids=[x['id'] for x in opts];kind=r['kind'];v=m['views'][0];pick='ok'
    if r['requestId'] in seen:continue
    seen.add(r['requestId']);o=None if 'Mulligan' in kind or kind=='InputChooseStartingHand' else obs(r)
    ck(not m['views'][0]['players'][1]['hand'] and not m['views'][1]['players'][0]['hand'],'private hands concealed')
    if 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif kind=='InputPassPriority':
     if initial is None and o['phase']=='MAIN1' and r['seat']==1:initial=o;save(m,'initial',o)
     if initial is not None and pairs and o['phase']=='COMBAT_DECLARE_BLOCKERS':
      if before is None:before=o;save(m,'before-switch',o)
      if not activated and r['seat']==(2 if jarkeld else 1):pick='card:'+str(card(o,r['seat'],'General Jarkeld' if jarkeld else "Sorrow's Path")['id']);activated=True;save(m,'activate-switch',o)
      elif gone and activated and targets and not bolt and r['seat']==1 and v['stackItems']:pick='card:'+str(card(o,1,'Lightning Bolt','Hand')['id']);bolt=True;save(m,'cast-removal',o)
      elif activated and not v['stackItems'] and (not gone or bolt):
       if switched is None:switched=o;save(m,'after-switch',o)
     if initial is not None and attacked and o['phase']=='MAIN2' and not v['stackItems']:
      save(m,'resolved',o);ck(activated and switched is not None,'actual paid native switch resolved');I=lambda seat,name:card(initial,seat,name)['id'];old=relation(before);actual=relation(switched)
      if band:
       members={I(1,'Benalish Hero'),I(1,'Grizzly Bears')};ck(all(set(x['band'])==members for x in before['matrix']),'actual declared same native band');ck(old==actual=={i:{I(2,'Wall of Roots')} for i in members},'same-band target switch leaves whole original relation intact')
      elif gone:ck(actual=={I(1,'Giant Spider'):set(),I(1,'Centaur Courser'):{I(2,'Wall of Roots')}},'one original blocker gone cannot half-swap')
      elif case=='sorrow-illegal':ck(actual==old,'flying legality prevents whole swap')
      elif jarkeld:ck(actual=={I(1,'Giant Spider'):{I(2,'Grizzly Bears')},I(1,'Centaur Courser'):{I(2,'Wall of Roots'),I(2,'Wall of Air')}},'whole two-versus-one original blockers switch')
      else:ck(actual=={I(1,'Giant Spider'):{I(2,recipient)},I(1,'Horned Turtle'):{I(2,recipient)},I(1,'Centaur Courser'):{I(2,'Wall of Glare')}},'actual legal additional-capacity whole switch')
      ck([pv['life'] for pv in o['players']]==([30,30] if jarkeld else [28,30]),'only actual Sorrow two life loss');dead={'Grizzly Bears'} if case in ['jarkeld-multiple','sorrow-one-gone'] else set()
      dmg={'jarkeld-band':({'Benalish Hero':0,'Grizzly Bears':0},{'Wall of Roots':3,'General Jarkeld':0}),'jarkeld-multiple':({'Giant Spider':2,'Centaur Courser':1},{'Wall of Roots':3,'Wall of Air':0,'General Jarkeld':0}),'sorrow-one-gone':({'Giant Spider':2,'Centaur Courser':2},{'Wall of Roots':3}),'sorrow-illegal':({'Air Elemental':3,'Centaur Courser':2},{'Wall of Air':4,'Wall of Roots':3}),'sorrow-multiple':({'Giant Spider':3,'Horned Turtle':2,'Centaur Courser':2},{'Wall of Glare':3,'Palace Guard':3})}.get(case)
      if case=='sorrow-additional':dmg=({'Giant Spider':2,'Horned Turtle':2,'Centaur Courser':2},{'Wall of Glare':3,'Wall of Roots':3,'Brave the Sands':0})
      oldall={c['id']:c for pv in initial['players'] for c in pv['zones']['Battlefield']}
      for seat in [1,2]:
       oldzones=initial['players'][seat-1]['zones'];now=o['players'][seat-1]['zones'];ck({c['id'] for c in now['Battlefield']}=={c['id'] for c in oldzones['Battlefield'] if not(seat==2 and c['name'] in dead)},'exact original surviving battlefield IDs')
       for c in now['Battlefield']:
        for k in ['owner','controller','timestamp','power','toughness','counters']:ck(c[k]==oldall[c['id']][k],('unchanged native field',k,c))
       for name,amount in dmg[seat-1].items():ck(card(o,seat,name)['damage']==amount,('accurate actual marked damage',name,amount))
       ck([c['id'] for c in now['Library']]==[c['id'] for c in oldzones['Library']],'original library unchanged');ck(not now['Exile'],'no extra exile');expected={c['id'] for c in oldzones['Hand']} - ({card(initial,1,'Lightning Bolt','Hand')['id']} if gone and seat==1 else set());ck({c['id'] for c in now['Hand']}==expected,'exact remaining private hand');ck(sum(v['players'][seat-1]['manaPool'])==sum(v['players'][seat-1]['snowManaPool'])==0,'no residual mana pools')
       ck({c['id'] for c in now['Graveyard']}==({I(2,'Grizzly Bears')} if seat==2 and dead else {card(initial,1,'Lightning Bolt','Hand')['id']} if seat==1 and gone else set()),'exact combat/removal grave IDs')
      ck(card(o,2,'General Jarkeld')['tapped'] if jarkeld else card(o,1,"Sorrow's Path")['tapped'],'actual native source T paid');ck(card(o,1,'Mountain')['tapped']==gone,'only actual Bolt mana paid');ck(r['seat']==r['playerSeat']==1,'natural MAIN2 actor1 restored');ck(not any(x['kind']=='OrderCombatants' for x in (json.loads(line)['request'] for line in (f/'trace.jsonl').read_text().splitlines() if json.loads(line)['type']=='request')),'current nonlegacy game has no invented combat order selector');result.update(status='PASS_BOUNDED',checks=checks,requests=len(seen),allocations=allocations,scope='Actual original SwitchBlock same-band/one-blocker-gone/illegal-flying/multiple-blockers/capacity-success boundaries; production rules legacy ordering stays disabled, no mode support claim');break
    elif kind=='InputAttack':
     ck(initial is not None and r['seat']==1,'original native declaration');sources={card(o,1,n)['id'] for n in a if n!="Sorrow's Path"};op=next((x for x in opts if x['id'].startswith('attack:') and x['attackerId'] in sources-attacked and x['defenderKind']=='player'),None)
     if op:pick=op['id'];attacked.add(op['attackerId'])
     elif band and bandsteps<2:pick='attack-band:'+str(card(o,1,'Benalish Hero' if bandsteps==0 else 'Grizzly Bears')['id']);bandsteps+=1
     else:ck(attacked==sources,'all intended attackers');save(m,'attack-confirm',o)
    elif kind=='InputBlock':
     target={(card(o,1,an)['id'],card(o,2,bn)['id']) for an,bn in wanted};op=next((x for x in opts if x['id'].startswith('block:') and (x['attackerId'],x['defenderId']) in target-pairs),None)
     if op:pick=op['id'];pairs.add((op['attackerId'],op['defenderId']))
     else:ck(pairs==target,'exact original block declarations');save(m,'block-confirm',o)
    elif kind=='InputSelectTargets':
     save(m,'targets-'+str(step),o);bolt_target=bolt and r.get('sourceCard',{}).get('name')=='Lightning Bolt';want=[card(o,2,'Grizzly Bears')['id']] if bolt_target else [card(o,1,n)['id'] for n in a if n!="Sorrow's Path"] if jarkeld else [card(o,2,n)['id'] for n in b[:2]];op=next((x for x in opts if x.get('card',{}).get('id') in want and (bolt_target,x['id']) not in targets and x['id']!='ok'),None)
     if op:pick=op['id'];targets.add((bolt_target,pick))
     else:ck('ok' in ids and r.get('selectedCount')==len(want),'exact original pair/single confirmation')
    elif kind=='AssignAmount':
     save(m,'damage-'+str(step),o);ck(case in ['jarkeld-band','jarkeld-multiple','sorrow-multiple','sorrow-additional'] and r['selectionPurpose']=='combatDamage','actual multiple relation allocation');rows=r['allocations'];desired='Benalish Hero' if band else 'Wall of Roots' if jarkeld else 'Giant Spider';row=next(x for x in rows if x.get('card',{}).get('name')==desired);pick='amount-add:'+str(row['id']) if r['remainingCount'] else 'confirm-amount';allocations.append(dict(requestId=r['requestId'],source=r['sourceCard']['id'],actor=r['seat'],rows=[(x['targetId'],x['amount']) for x in rows]));ck(r['seat']==(1 if jarkeld else 2),'actual allocation controller');ck(r['maxCount']==0 if band or case=='sorrow-additional' else r['maxCount']>0,'actual zero/nonzero allocation amount')
    elif 'PayMana' in kind:save(m,'payment-'+str(step),o)
    elif kind=='RevealCards':pick='confirm-cards'
    elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in ids else next(i for i in ids if i.startswith('order:'))
    elif kind!='InputChooseStartingHand':save(m,'unhandled-'+str(step),o);raise AssertionError(('unhandled actual input',r))
    ck(pick in ids and not next(x for x in opts if x['id']==pick).get('disabled'),('legal actual response',r,pick));print(case,step,kind,r['seat'],pick,flush=True);p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   else:raise AssertionError('step limit')
 except Exception:result.update(status='FAIL',checks=checks,error=traceback.format_exc())
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 wr(f/'result.json',result);wr(OUT/(case+'-result.json'),result);print(result['status'],result.get('error',''),flush=True);return result
if __name__=='__main__':
 results=[run(c) for c in sys.argv[1:] or SPECS];wr(OUT/'native-summary.json',dict(results=results));sys.exit(any(r['status']=='FAIL' for r in results))
