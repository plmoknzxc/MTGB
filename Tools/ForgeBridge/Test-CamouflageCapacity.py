"""R03 finite additional-blocking capacity: real declarations, paid spell and outcomes.
Only opening positions and deterministic standard RNG belong to the fixture.
Every answer is an original legal wire option; immutable attempts retain failures.
"""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/camouflage-boundary-1010';FIX=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['RoyalStateFixture.java','CombatChoiceFixture.java']];DECK=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
SPECS={
 'jarkeld-legal':(['Giant Spider','Centaur Courser'],['Wall of Roots','Mothrider Samurai','General Jarkeld']),
 'jarkeld-illegal':(['Wind Drake','Centaur Courser'],['Wall of Air','Wall of Roots','General Jarkeld']),
 'jarkeld-one-gone':(['Wind Drake','Centaur Courser'],['Wall of Air','Wall of Roots','General Jarkeld']),
 'sorrow-legal':(['Giant Spider','Centaur Courser',"Sorrow's Path"],['Wall of Roots','Mothrider Samurai']),
 'sorrow-capacity':(['Giant Spider','Horned Turtle','Centaur Courser',"Sorrow's Path"],['Wall of Glare','Wall of Roots']),
 'camouflage':(['Boggart Brute','Charging Rhino','Wind Drake','Centaur Courser'],['Wall of Glare','Wall of Roots','Wall of Air','Wall of Omens','Grizzly Bears','Vampire Interloper','Llanowar Elves']),
 'camouflage-empty':(['Wind Drake','Centaur Courser'],['Vampire Interloper','Llanowar Elves']),
}
SPECS={'camouflage-additional':(['Llanowar Elves','Elvish Mystic','Arbor Elf'],['Wall of Glare','Wall of Roots','Wall of Omens','Brave the Sands'])}
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
  with tempfile.TemporaryDirectory(prefix='mtgb-combat-choice-') as temp,(f/'stderr.log').open('w',encoding='utf8') as log,(f/'trace.jsonl').open('w',encoding='utf8') as trace:
   a,b=SPECS[case];cam=case.startswith('camouflage');own=dict(Battlefield=a+['Forest','Mountain'],Hand=['Camouflage'] if cam else ['Lightning Bolt'] if case.endswith('one-gone') else ['Negate']);other=dict(Battlefield=b,Hand=['Negate'])
   if cam:other['tapped']=[]
   cfg=dict(session=case,seed=4,fixtureSeed=int(os.environ.get('COMBAT_CHOICE_SEED','3' if case.endswith('limits') else '4')),startingLife=30,firstPlayerSeat=1,decks=[DECK,DECK],own=own,other=other,auditDir=str(audit));wr(f/'start.json',cfg)
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
   initial=None;before=None;switched=None;seen=set();attacked=set();pairs=set();activated=False;bolt=False;cast=False;target_clicks=set();pool_history=[];pile_done=set();single=False;allocs={};deadline=time.monotonic()+180
   for step in range(550):
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
     if initial is not None and cam and attacked and not cast and o['phase']=='COMBAT_DECLARE_ATTACKERS' and r['seat']==1:
      pick='card:'+str(card(o,1,'Camouflage','Hand')['id']);cast=True;save(m,'cast-camouflage',o)
     elif initial is not None and not cam and pairs and o['phase']=='COMBAT_DECLARE_BLOCKERS':
      if before is None:before=o;save(m,'before-switch',o)
      if not activated and r['seat']==(2 if case.startswith('jarkeld') else 1):
       pick='card:'+str(card(o,r['seat'],'General Jarkeld' if case.startswith('jarkeld') else "Sorrow's Path")['id']);activated=True;save(m,'activate-switch',o)
      elif case.endswith('one-gone') and activated and target_clicks and not bolt and r['seat']==1 and v['stackItems']:
       pick='card:'+str(card(o,1,'Lightning Bolt','Hand')['id']);bolt=True;save(m,'cast-removal',o)
      elif activated and not v['stackItems'] and (not case.endswith('one-gone') or bolt):
       if switched is None:switched=o;save(m,'after-switch',o)
     if cam and cast and o['phase']=='COMBAT_DECLARE_BLOCKERS' and not v['stackItems'] and (len(pile_done)==len(attacked) or case.endswith('empty')):
      if switched is None:switched=o;save(m,'after-piles',o)
     if initial is not None and attacked and o['phase']=='MAIN2' and not v['stackItems']:
      save(m,'resolved',o);ck(cast if cam else activated,'actual paid operation');ck(switched is not None,'native post-resolution relation captured');actual=relation(switched);I=lambda seat,name:card(initial,seat,name)['id']
      if not cam:
       old=relation(before);ck(len(old)==len(attacked) and len(pairs)==(3 if case.endswith('capacity') else 2),'real declarations original pair counts')
       if case=='jarkeld-legal' or case=='sorrow-legal':
        ck(actual=={I(1,'Giant Spider'):{I(2,'Mothrider Samurai')},I(1,'Centaur Courser'):{I(2,'Wall of Roots')}},'exact swapped original IDs');s=card(switched,2,'Mothrider Samurai');ck(s['power']==s['toughness']==(4 if case.startswith('sorrow') else 3),'only flagged remove/reblock triggers second Bushido')
       elif case.endswith('one-gone'):ck(actual=={I(1,'Centaur Courser'):{I(2,'Wall of Roots')}},'one gone prevents reassignment, surviving old pair intact')
       else:ck(actual==old,'illegal/capacity whole relation unchanged')
       ck([p['life'] for p in o['players']]==([28,30] if case.startswith('sorrow') else [30,30]),'exact life only Sorrow tap trigger')
       dead={'Giant Spider'} if case=='sorrow-legal' else {'Wind Drake'} if case.endswith('one-gone') else set()
       dmg={'jarkeld-legal':({'Giant Spider':3,'Centaur Courser':0},{'Wall of Roots':3,'Mothrider Samurai':2,'General Jarkeld':0}), 'jarkeld-illegal':({'Wind Drake':1,'Centaur Courser':0},{'Wall of Air':2,'Wall of Roots':3,'General Jarkeld':0}), 'jarkeld-one-gone':({'Centaur Courser':0},{'Wall of Air':0,'Wall of Roots':3,'General Jarkeld':0}), 'sorrow-legal':({'Centaur Courser':2},{'Wall of Roots':3,'Mothrider Samurai':2}), 'sorrow-capacity':({'Giant Spider':2,'Horned Turtle':2,'Centaur Courser':2},{'Wall of Glare':3,'Wall of Roots':3})}[case]
       for seat in [1,2]:
        ck({c['id'] for c in o['players'][seat-1]['zones']['Battlefield']}=={c['id'] for c in initial['players'][seat-1]['zones']['Battlefield'] if not(seat==1 and c['name'] in dead)},'exact remaining battlefield IDs')
        for name,amount in dmg[seat-1].items():ck(card(o,seat,name)['damage']==amount,'native correct combat/trigger damage '+name)
       ck({c['id'] for c in o['players'][0]['zones']['Graveyard']}=={I(1,n) for n in dead}|({card(initial,1,'Lightning Bolt','Hand')['id']} if bolt else set()),'exact dead/removal spell native grave IDs');ck(not o['players'][1]['zones']['Graveyard'],'all blockers survive')
       ck(card(o,2,'General Jarkeld')['tapped'] if case.startswith('jarkeld') else card(o,1,"Sorrow's Path")['tapped'],'source native T paid')
      else:
       ck(card(initial,1,'Camouflage','Hand')['id'] in [c['id'] for c in o['players'][0]['zones']['Graveyard']],'original paid spell grave');ck(card(o,1,'Forest')['tapped'] and not card(o,1,'Mountain')['tapped'],'exact G payment');ck(len(switched['randomCalls'])==(0 if case.endswith('empty') else len(attacked)-1),'actual shuffle or zero-eligible phase fast path');order=[x['attacker'] for x in (before or switched)['matrix']];mapping=order[:]
       for draw in switched['randomCalls']:
        n=draw['bound'];j=draw['value'];ck(0<=j<n and n<=len(mapping),'real bounded RNG output');mapping[n-1],mapping[j]=mapping[j],mapping[n-1]
       chosen={};legalByAttacker={};nameById={c['id']:c['name'] for c in initial['players'][0]['zones']['Battlefield']};blockName={c['id']:c['name'] for c in initial['players'][1]['zones']['Battlefield']}
       piles=[['Wall of Glare','Wall of Roots','Wall of Omens'],['Wall of Glare','Wall of Roots','Wall of Omens'],['Wall of Glare']]
       for ix,attacker in enumerate(mapping):
        legal={I(2,n) for n in piles[ix] if nameById[attacker]!='Wind Drake' or n=='Wall of Air'}
        if nameById[attacker]=='Boggart Brute' and len(legal)<2:legal=set()
        if nameById[attacker]=='Charging Rhino' and len(legal)>1:ck(single and actual[attacker]=={chosen_single},'actual exact-one native fallback chosen ID');legal={chosen_single}
        ck(actual[attacker]==legal,'RNG permutation + legality/min/max gives exact original relation');legalByAttacker[attacker]=list(legal)
       ck(len(pile_done)==len(attacked) if not case.endswith('empty') else len(pile_done)==0,'dynamic pile requests or no-candidate fast path')
       result['randomPermutation']=mapping;result['actualRelations']={str(k):sorted(v) for k,v in actual.items()};result['exactOneFallback']=single;result['minimumFiltered']=any(nameById[a]=='Boggart Brute' and not actual[a] for a in actual);result['illegalFlyingFiltered']=any(nameById[a]=='Wind Drake' and any(n!='Wall of Air' for n in piles[mapping.index(a)]) for a in actual)
       if case.endswith('limits'):ck(single and result['minimumFiltered'],'dedicated max-one/min-two branch must actually be reached')
       # Zero-power multi-blocker avoids damage-split ambiguity; exact one-card
       # remaining blockers independently account for the native combat result.
       powers={c['id']:c['power'] for c in switched['players'][0]['zones']['Battlefield']};bp={c['id']:c['power'] for c in switched['players'][1]['zones']['Battlefield']};ck([p['life'] for p in o['players']]==[30,30-sum(powers[a] for a,bs in actual.items() if not bs)],'exact unblocked damage from actual random relation')
       for c in o['players'][0]['zones']['Battlefield']:
        if c['id'] in actual:ck(c['damage']==sum(bp[x] for x in actual[c['id']]),'exact incoming blocker damage')
       if case.endswith('empty'):ck(all(not bs for bs in actual.values()) and not o['players'][1]['zones']['Graveyard'],'no candidates no phantom block')
      if case=='camouflage-additional':
       ck(len(pool_history)==3 and [p['candidates'] for p in pool_history]==[['Wall of Glare','Wall of Omens','Wall of Roots']]*2+[['Wall of Glare']],'finite two piles removes only after second; Any stays through third')
       for seat in [1,2]:
        old=initial['players'][seat-1]['zones']['Battlefield'];now=o['players'][seat-1]['zones']['Battlefield']
        ck({c['id'] for c in old}=={c['id'] for c in now},'all original battlefield cards survive low-power actual combat')
        for c in now:
         oldc=next(x for x in old if x['id']==c['id'])
         ck(all(c[k]==oldc[k] for k in ['name','owner','controller','timestamp','power','toughness','counters']),'card identity/characteristics unchanged')
         ck(c['tapped']==(c['id'] in attacked or c['name']=='Forest'),'only real attackers and G land tapped')
         automatic=sum(powers[a] for a,bs in actual.items() if len(bs)==1 and c['id'] in bs)
         expected=0 if seat==1 else automatic+sum(amount for rows in allocs.values() for target,amount in rows if target==c['id'])
         ck(c['damage']==expected,'exact real recorded combat assignment damage')
       ck(not o['players'][1]['zones']['Graveyard'],'no defender deaths')
       ck(len(o['players'][0]['zones']['Graveyard'])==1,'only actual Camouflage grave')
      for seat in [1,2]:
       old=initial['players'][seat-1]['zones'];now=o['players'][seat-1]['zones'];ck([c['id'] for c in now['Library']]==[c['id'] for c in old['Library']],'exact library unchanged');ck(not now['Exile'],'no extra exile')
       expected={c['id'] for c in old['Hand']} - ({card(initial,1,'Camouflage','Hand')['id']} if cam and seat==1 else {card(initial,1,'Lightning Bolt','Hand')['id']} if bolt and seat==1 else set());ck({c['id'] for c in now['Hand']}==expected,'exact remaining private hand')
       ck(sum(v['players'][seat-1]['manaPool'])==sum(v['players'][seat-1]['snowManaPool'])==0,'no residual pools')
      result.update(status='PASS_BOUNDED',checks=checks,requests=len(seen),scope='Original R03 exact paid combat-switch/pile outcomes; no all-payload/legacy-band/four-client/full-localization claim');break
    elif kind=='InputAttack':
     ck(initial is not None and r['seat']==1,'native own declaration');sources={c['id'] for c in o['players'][0]['zones']['Battlefield'] if c['name'] in a and c['name']!="Sorrow's Path"};op=next((x for x in opts if x['id'].startswith('attack:') and x['attackerId'] in sources-attacked and x['defenderKind']=='player'),None)
     if op:pick=op['id'];attacked.add(op['attackerId'])
     else:ck(attacked==sources,'all intended attacks declared');save(m,'attack-confirm',o)
    elif kind=='InputBlock':
     ck(not cam,'Camouflage actually replaces ordinary blockers');wanted=[('Giant Spider','Wall of Glare'),('Horned Turtle','Wall of Glare'),('Centaur Courser','Wall of Roots')] if case.endswith('capacity') else [('Wind Drake','Wall of Air'),('Centaur Courser','Wall of Roots')] if 'illegal' in case or 'one-gone' in case else [('Giant Spider','Wall of Roots'),('Centaur Courser','Mothrider Samurai')];target={(card(o,1,an)['id'],card(o,2,bn)['id']) for an,bn in wanted};op=next((x for x in opts if x['id'].startswith('block:') and (x['attackerId'],x['defenderId']) in target-pairs),None)
     if op:pick=op['id'];pairs.add((op['attackerId'],op['defenderId']))
     else:ck(pairs==target,'all exact original declarations');save(m,'block-confirm',o)
    elif 'InputSelectTargets' in kind:
     save(m,'targets-'+str(step),o);boltTarget=bolt and r['seat']==1;want=[card(o,1,'Wind Drake')['id']] if boltTarget else [card(o,2,n)['id'] for n in ['Wall of Glare','Wall of Roots']] if case.endswith('capacity') else [card(o,2,n)['id'] for n in ['Wall of Roots','Mothrider Samurai']] if case.startswith('sorrow') else [card(o,1,n)['id'] for n in ['Giant Spider','Centaur Courser']] if case=='jarkeld-legal' else [card(o,1,n)['id'] for n in ['Wind Drake','Centaur Courser']]
     op=next((x for x in opts if x.get('card',{}).get('id') in want and (boltTarget,x['id']) not in target_clicks and x['id']!='ok'),None)
     if op:pick=op['id'];target_clicks.add((boltTarget,pick))
     else:ck('ok' in ids and r.get('selectedCount')==len(want),'exact native pair/single confirmation');pick='ok'
    elif cam and ('SelectCards' in kind or 'SelectEntities' in kind):
     save(m,'pile-'+str(step),o);message=r['message'];rows=[x for x in opts if x.get('card',{}).get('id',0)>0];names={x['card']['name'] for x in rows};match=re.search(r'pile (\d+)',message,re.I)
     if before is None:before=o;save(m,'before-piles',o)
     if match:
      ix=int(match.group(1));ck(r['seat']==2 and r.get('minCount')==0,'defender chooses optional pile');ck('Vampire Interloper' not in names and 'Llanowar Elves' not in names,'cannot block/tapped excluded');wanted=[['Wall of Glare','Wall of Roots','Wall of Omens'],['Wall of Glare','Wall of Roots','Wall of Omens'],['Wall of Glare']][ix-1];poolExpected={'Wall of Glare','Wall of Roots','Wall of Omens'} if ix<=2 else {'Wall of Glare'};ck(names==poolExpected,'exact dynamic original pool including reusable unlimited blocker');op=next((x for x in rows if x['card']['name'] in wanted and not x.get('selected',False)),None)
      if op:pick=op['id']
      else:ck(r.get('selectedCount')==len(wanted),'exact selected pile');pick='confirm-cards' if 'confirm-cards' in ids else 'ok';pile_done.add(ix);pool_history.append(dict(pile=ix,candidates=sorted(names),selected=wanted))
     else:
      ck('Charging Rhino' in message and r.get('minCount')==r.get('maxCount')==1,'native one blocker max fallback');ck(names<= {'Wall of Glare','Wall of Roots','Wall of Air','Wall of Omens','Grizzly Bears'},'only surviving legal original fallback pool');single=True;op=next((x for x in rows if x['card']['name']=='Wall of Glare'),rows[0]);chosen_single=op['card']['id'];pick=('confirm-cards' if 'confirm-cards' in ids else 'ok') if r.get('selectedCount') else op['id']
    elif 'PayMana' in kind:save(m,'payment-'+str(step),o);pick='ok'
    elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in ids else next(i for i in ids if i.startswith('order:'));save(m,'order-'+str(step),o)
    elif kind=='AssignAmount':
     save(m,'damage-'+str(step),o);rows=r['allocations'];remaining=r['remainingCount'];row=next((x for x in rows if x.get('card') and x.get('hasLethal') and x['amount']<x['lethal']),rows[0]);pick='amount-add:'+str(row['id']) if remaining else 'confirm-amount';allocs[r['contextId']]=[(x['targetId'],x['amount']) for x in rows]
    elif kind=='RevealCards':pick='confirm-cards'
    elif kind not in ('InputPassPriority','InputChooseStartingHand'):save(m,'unhandled-'+str(step),o);raise AssertionError(('unhandled actual input',r))
    ck(pick in ids and not next(x for x in opts if x['id']==pick).get('disabled'),('legal original response',r,pick));print(case,step,kind,r['seat'],pick,flush=True);p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   else:raise AssertionError('step limit')
   ck(result['status']=='PASS_BOUNDED','final result');result.update(checks=checks,piles=pool_history,allocations=allocs)
 except Exception:result.update(status='FAIL',checks=checks,error=traceback.format_exc())
 finally:
  if p is not None and p.poll() is None:p.kill();p.wait()
 wr(f/'result.json',result);wr(OUT/(case+'-result.json'),result);print(result['status'],result.get('error',''),flush=True);return result
if __name__=='__main__':
 results=[run(c) for c in sys.argv[1:] or SPECS];wr(OUT/'native-summary.json',dict(results=results));sys.exit(any(r['status']=='FAIL' for r in results))
