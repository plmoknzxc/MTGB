"""Real-engine acceptance for blocks, divided combat damage, storm, ninjutsu and battle back faces."""
import json, os, queue, subprocess, tempfile, threading, sys
from pathlib import Path
root=Path(__file__).resolve().parents[2];runtime=root/'ForgeRuntime';cfg=json.loads((runtime/'runtime.json').read_text(encoding='utf-8-sig'))
out=root/'Tools/Validation/Reports/AdvancedInteractions';out.mkdir(parents=True,exist_ok=True)
classes=root/'.tools/advanced-interactions/classes';classes.mkdir(parents=True,exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(runtime/'classes')+';'+str(runtime/'lib/*'),'-d',str(classes),str(root/'Tools/ForgeBridge/tests/AdvancedInteractionFixture.java')],check=True)
deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
def run(case):
 with tempfile.TemporaryDirectory(prefix='mtgb-advanced-') as temp,(out/(case+'.log')).open('w',encoding='utf-8') as log,(out/(case+'.jsonl')).open('w',encoding='utf-8') as trace:
  start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,fixture=case,seed=4,startingLife=20,firstPlayerSeat=1,decks=[deck,deck])),encoding='utf-8')
  p=subprocess.Popen([str(runtime/cfg['java']),'-Djava.awt.headless=true','-cp',str(classes)+';'+str(runtime/'classes')+';'+str(runtime/'lib/*'),'mtgb.bridge.AdvancedInteractionFixture',str(runtime/cfg['assets']),str(start)],cwd=runtime/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
  q=queue.Queue();threading.Thread(target=lambda:[q.put(json.loads(line)) for line in p.stdout],daemon=True).start()
  def req():
   while True:
    m=q.get(timeout=40);trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ('fatal','ended','rejected'),m
    if m['type']=='request':return m
  def snap(m,label):
   v=dict(m['views'][m['request']['seat']-1]);v.update(request=m['request'],awaitingInput=True);(out/(case+'-'+label+'.json')).write_text(json.dumps(v),encoding='utf-8')
  try:
   casts=0;seen_damage=False;seen_copies=False;seen_ninja=False;seen_ninja_stack=False;reset=False;blocked=set();removed=False;declared=False;evidence_step=0;x_step=0
   for step in range(260):
    m=req();r=m['request'];v=m['views'][r['seat']-1];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];pick='ok'
    one,two=m['views'][0]['players'];field=one['battlefield'];stack=v['stackItems']
    if case=='battle' and len(stack)==1:
     if stack[0]['kind']=='trigger':snap(m,'front-trigger')
     elif stack[0]['source'].get('backFace'):snap(m,'back-stack')
    print(case,step,kind,v['phase'],ids,flush=True)
    if kind=='InputPassPriority':
     if case=='crew':
      vehicle=next(c for c in field if c['name']=="Kylox's Voltstrider");bear=next(c for c in field if c['name']=='Grizzly Bears')
      if vehicle['creature'] and not stack:
       assert bear['tapped'];snap(m,'resolved');return 'PASS crew: choose crew ability, select and tap creature cost, vehicle becomes creature on resolution'
      if r['seat']==1 and v['phase']=='MAIN1' and casts==0:pick='card:'+str(vehicle['id']);casts=1
     if case=='statuses':
      creature=next(c for c in field if c['name']=='Vampire Nighthawk');counters={c['kind']:c['count'] for c in creature['counters']}
      assert counters['P1P1']==2 and counters['STUN']==1 and creature['power']==4 and creature['toughness']==5
      assert {'FLYING','DEATHTOUCH','LIFELINK'}<=set(creature['keywords']);snap(m,'field');return 'PASS statuses: engine counters, stun, current combat keywords and modified 4/5 stats'
     if case=='graveyards':
      if casts and not stack and 'Grizzly Bears' not in two['graveyard']:
       assert 'Grizzly Bears' in one['graveyard'];snap(m,'resolved');return 'PASS graveyards: same-name cards in both graveyards, distinct owners and IDs, opponent card exiled, own copy retained'
      if r['seat']==1 and v['phase']=='MAIN1' and casts==0:
       pick='card:'+str(next(c for c in field if c['name']=="Agatha's Soul Cauldron")['id']);casts=1
     if case=='rooms':
      room=next(c for c in field if c.get('room'))
      assert room['leftRoom']=='Dollmaker\'s Shop' and room['rightRoom']=='Porcelain Gallery'
      if room['rightRoomUnlocked'] and not stack:
       assert room['leftRoomUnlocked'];snap(m,'unlocked');return 'PASS rooms: separate door names/state, real unlock cost and both doors remain unlocked'
      if r['seat']==1 and v['phase']=='MAIN1' and casts==0:
       assert room['leftRoomUnlocked'] and not room['rightRoomUnlocked'];snap(m,'locked');pick='card:'+str(room['id']);casts=1
     if case=='x':
      ballista=next((c for c in field if c['name']=='Walking Ballista'),None)
      if ballista:
       assert ballista['power']==2 and ballista['toughness']==2
       assert sum(c['tapped'] for c in field if c['land'])==4;snap(m,'resolved');return 'PASS X: numeric choice X=2, four mana paid, Walking Ballista enters as 2/2'
      if r['seat']==1 and v['phase']=='MAIN1' and casts==0:
       pick='card:'+str(next(c for c in one['hand'] if c['name']=='Walking Ballista')['id']);casts=1
     if case=='evidence':
      vehicle=next(c for c in field if c['name']=="Kylox's Voltstrider")
      if evidence_step==10 and not stack:
       assert vehicle['creature'] and 'Grizzly Bears' not in one['graveyard'] and 'Hill Giant' not in one['graveyard']
       assert 'Forest' in one['graveyard'] and 'Colossal Dreadmaw' in one['graveyard'];snap(m,'resolved')
       return 'PASS evidence: Forge mana-value tally, zero-value land, under-threshold gate, deselection, cancellation without exile, exact threshold and real exile/animation'
      if r['seat']==1 and v['phase']=='MAIN1' and not stack:
       assert 'Grizzly Bears' in one['graveyard'];pick='card:'+str(vehicle['id'])
     if case=='abilities':
      grist=next(c for c in field if c['name']=='Grist, the Hunger Tide')
      if stack and casts==1:
       assert stack[0]['kind']=='ability' and grist['loyalty']==6;snap(m,'activated');return 'PASS abilities: Grist opens three loyalty options directly; cancellation preserves loyalty; reopening and +1 activation enters stack'
      if r['seat']==1 and v['phase']=='MAIN1' and casts==0:
       assert grist['loyalty']==5;pick='card:'+str(grist['id']);casts=1
     if case=='damage' and seen_damage and v['phase']=='MAIN2':
      assert two['life']==18 and not any(c['name']=='Grizzly Bears' for c in two['battlefield']);snap(m,'resolved');return 'PASS damage: two blockers, lethal constraints, reset, trample remainder and actual deaths/life'
     if case=='storm':
      seen_copies |= any(e.get('copied') for e in stack)
      if any(e.get('copied') for e in stack):snap(m,'copies')
      if casts==3 and seen_copies and not stack:
       assert two['life']==17,two;snap(m,'resolved');return 'PASS storm: two prior spells, separate copied objects, retarget and 3 damage'
      if r['seat']==1 and v['phase']=='MAIN1' and not stack and casts<3:
       name='Ornithopter' if casts<2 else 'Grapeshot';card=next(c for c in one['hand'] if c['name']==name);pick='card:'+str(card['id']);casts+=1
     if case=='ninjutsu':
      seen_ninja_stack |= any(e['kind']=='ability' and e['source']['name']=='Moon-Circuit Hacker' for e in stack)
      ninja=next((c for c in field if c['name']=='Moon-Circuit Hacker'),None)
      if ninja:
       if not seen_ninja:assert seen_ninja_stack and ninja['tapped'] and ninja['attacking']
       seen_ninja=True;assert any(c['name']=='Ornithopter' for c in one['hand']);snap(m,'entered')
      if seen_ninja and v['phase']=='MAIN2':
       assert two['life']==18;snap(m,'resolved');return 'PASS ninjutsu: legal unblocked return cost, ability stack, tapped attacking entry and combat damage'
      if r['seat']==1 and declared and not seen_ninja:
       card=next((c for c in one['hand'] if c['name']=='Moon-Circuit Hacker' and 'card:'+str(c['id']) in ids),None)
       if card and v['phase'] in ('COMBAT_DECLARE_BLOCKERS','COMBAT_FIRST_STRIKE_DAMAGE','COMBAT_DAMAGE','COMBAT_END') and casts==0:pick='card:'+str(card['id']);casts=1
     if case=='battle' and any(c.get('backFace') for c in field):
      card=next(c for c in field if c.get('backFace'));assert card['creature'] and not card['battle'];snap(m,'backface');return 'PASS battle: defeat trigger, transformed casting and correct back-face battlefield state'
    elif kind=='InputAttack':
     if not declared:
      candidates=[o for o in opts if o['id'].startswith('attack:') and o['defenderKind']==('card' if case=='battle' else 'player')]
      if candidates:pick=candidates[0]['id'];declared=True
    elif kind=='InputBlock':
     if case=='damage':
      choices=[o for o in opts if o['id'].startswith('block:') and not o.get('selected')]
      if choices:pick=choices[0]['id'];blocked.add(choices[0]['defenderId']);snap(m,'blocking')
      elif not removed:
       pick=next(i for i in ids if i.startswith('block-remove:'));removed=True
      else:assert all(c.get('blockedAttackerIds') for c in two['battlefield'] if c['creature'])
    elif kind=='ChooseNumber':
     assert case=='x' and r['minValue']==0 and r['maxValue']>=2
     if r['selectedValue']==0:assert 'number-minus' not in ids
     if not reset:
      pick='cancel';reset=True;casts=0
      assert not any(c['tapped'] for c in field if c['land'])
     else:
      assert r['selectedValue']==[0,5,0,1,2][x_step]
      if x_step==4:snap(m,'number')
      pick=['number-plus-five','number-minus-five','number-plus','number-plus','confirm-number'][x_step];x_step+=1
    elif kind=='AssignAmount':
     seen_damage=True;amounts=r['allocations'];snap(m,'assign')
     assert len(amounts)==3
     if not reset and sum(a['amount'] for a in amounts)>0:pick='reset-amount';reset=True
     else:
      target=next((a for a in amounts if a['amount']<2),None);pick='confirm-amount' if target is None else 'amount-add:'+str(target['id'])
      if amounts[0]['amount']<2 or amounts[1]['amount']<2:assert 'amount-add:2' not in ids
    elif kind.startswith('InputPayMana'):
     desired='U' if case=='ninjutsu' else 'W' if case=='rooms' else 'R';pick='mana:'+desired
     if pick not in ids:
      card=next(c for c in field if c['name']==('Island' if desired=='U' else 'Plains' if desired=='W' else 'Mountain') and 'card:'+str(c['id']) in ids);pick='card:'+str(card['id'])
    elif kind=='InputSelectTargets':
     if case=='graveyards':
      cards=[o for o in opts if o.get('card',{}).get('zone')=='Graveyard']
      if cards:
       assert {o['card']['ownerSeat'] for o in cards}=={1,2} and len({o['card']['id'] for o in cards})==2
       snap(m,'targets');pick='ok' if 'ok' in ids and r.get('selectedCount') else next(o['id'] for o in cards if o['card']['ownerSeat']==2)
      else:pick='ok' if 'ok' in ids and r.get('selectedCount') else next(o['id'] for o in opts if o.get('card',{}).get('name')=='Grizzly Bears')
     else:pick='ok' if 'ok' in ids and r.get('selectedCount') else 'player:'+str(two['targetId'])
    elif case=='evidence' and r.get('selectionPurpose')=='collectEvidence':
     assert r['requiredValue']==6
     expected=[0,0,2,0,2,0,2,6,0,2,6][evidence_step]
     assert r['selectedValue']==expected,(evidence_step,r)
     assert ('ok' in ids)==(expected>=6)
     snap(m,'selection-'+str(evidence_step))
     sequence=['Forest','Grizzly Bears','Grizzly Bears','Grizzly Bears','cancel','Grizzly Bears','Hill Giant','reset-cards','Grizzly Bears','Hill Giant','ok']
     action=sequence[evidence_step]
     pick=action if action in ('cancel','ok','reset-cards') else next(o['id'] for o in opts if o.get('card',{}).get('name')==action)
     if evidence_step==10:snap(m,'confirmed')
     else:evidence_step+=1
    elif kind.startswith('InputSelect') or kind=='SelectCards':
     if case=='crew' and not r.get('selectedCount'):pick=next(o['id'] for o in opts if o.get('card',{}).get('name')=='Grizzly Bears')
     else:pick='ok' if 'ok' in ids else 'confirm-cards' if 'confirm-cards' in ids else next(o['id'] for o in opts if o['id'].startswith(('card:','select:')) and not o.get('selected'))
    elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in ids else next(i for i in ids if i.startswith('order:'))
    elif kind=='dialog':
     snap(m,'dialog');pick=next((o['id'] for o in opts if 'ninjutsu' in o['label'].lower()),ids[0])
     if case=='crew':pick=next(o['id'] for o in opts if 'crew' in o['label'].lower())
     if case=='x':pick=next(o['id'] for o in opts if o['label']=='2')
     if case=='abilities':
      assert len([i for i in ids if i.startswith('choice:')])==3 and 'cancel' in ids and all(o['label'] not in ('Yes','No') for o in opts)
      if not reset:pick='cancel';reset=True;casts=0
      else:pick=next(o['id'] for o in opts if o['label'].startswith('+1'))
    elif kind=='RevealCards':pick='confirm-cards'
    assert pick in ids,(kind,pick,opts)
    p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   raise AssertionError('No completion after 260 requests')
  finally:
   if p.poll() is None:
    p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
    try:p.wait(timeout=3)
    except subprocess.TimeoutExpired:p.kill();p.wait()
if __name__=='__main__':
 results=[]
 for case in sys.argv[1:] or ['damage','storm','ninjutsu','battle']:
  try:results.append(run(case))
  except Exception as e:results.append('FAIL '+case+': '+repr(e))
 report='result-'+ '-'.join(sys.argv[1:])+'.txt' if sys.argv[1:] else 'result.txt'
 (out/report).write_text('\n'.join(results),encoding='utf-8');print('\n'.join(results));sys.exit(any(s.startswith('FAIL') for s in results))
