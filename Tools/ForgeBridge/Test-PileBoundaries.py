"""Real multi/predefined-pile casts. Shared opening fixture; exact native identities."""
import hashlib,json,os,queue,re,subprocess,sys,tempfile,threading,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'.tools/pile-boundaries-1007/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURES=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['LibraryCostFixture.java','SearchBoundaryFixture.java']]
SHA=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['whims-balanced','whims-empty','whims-replaced','hostile-left-hidden','hostile-right-visible','hostile-empty','hostile-cancel','abstract-hidden','abstract-visible','river-piles','toymaker-hidden']

def run(case):
 whims=case.startswith('whims');abstract=case.startswith('abstract');cancel=case.endswith('cancel');river=case.startswith('river');toy=case.startswith('toymaker')
 spell='The Celestial Toymaker' if toy else 'Raging River' if river else 'Whims of the Fates' if whims else 'Abstract Performance' if abstract else 'Hostile Negotiations'
 board=['Mountain']*6+['Grizzly Bears','Runeclaw Bear'] if whims else ['Island']*6 if abstract else ['Swamp']*4
 op=['Leyline of the Void'] if case=='whims-replaced' else ['Grizzly Bears','Runeclaw Bear','Llanowar Elves','Ornithopter'] if whims else []
 if river:board=['Mountain']*2+['Grizzly Bears','Runeclaw Bear'];op=['Grizzly Bears','Runeclaw Bear','Wind Drake']
 if toy:board=['Plains','Island','Swamp','Fervor']
 folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir()
 meta=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driverSha256=SHA(Path(__file__)),fixtures=[dict(path=p.relative_to(ROOT).as_posix(),sha256=SHA(p)) for p in FIXTURES]);(folder/'driver.py').write_bytes(Path(__file__).read_bytes())
 for f in FIXTURES:(folder/f.name).write_bytes(f.read_bytes())
 def finish(status,message):
  r=dict(meta,status=status,result=status+' '+case+': '+message)
  for f in [folder/'result.json',OUT/(case+'-result.json')]:f.write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return r
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
  start=dict(session=case,fixture=case,hand1=[spell],hand2=[],board1=board,board2=op,grave1=[],grave2=[],emptyLibrary=case=='hostile-empty',seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK],auditDir=str(audit));(folder/'start.json').write_text(json.dumps(start,indent=2)+'\n',encoding='utf8');f=Path(temp)/'start.json';f.write_text(json.dumps(start),encoding='utf8')
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.SearchBoundaryFixture',str(RT/CFG['assets']),str(f)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');messages=queue.Queue()
  def reader():
   try:
    for line in proc.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,label):
   for seat,v in enumerate(m['views'],1):(folder/(label+'-seat'+str(seat)+'.json')).write_text(json.dumps(dict(v,awaitingInput=m['type']=='request'),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
   (folder/(label+'-request.json')).write_text(json.dumps(m['request'],ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  def libs(q):
   f=audit/(str(q['requestId'])+'.json');until=time.monotonic()+3
   while not f.exists() and time.monotonic()<until:time.sleep(.01)
   assert f.exists(),'Missing library audit';return [p['library'] for p in json.loads(f.read_text())['players']]
  source=None;before=None;initial=None;cast=False;paid=False;cancelled=False;piles=[];split=[[],[]];split_ids=[set(),set()];flip=2 if case=='hostile-right-visible' else 1;chosen_index=flip if case.endswith('visible') else 3-flip;initial_exile=None;attackers=[];blockers=[];attacked=set();blocked=set();river_ready=False;block_seen=False
  try:
   for step in range(160):
    m=messages.get(timeout=35);assert m is not None,'Native exited';trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush();assert m['type'] not in ['fatal','rejected','ended'],m
    if m['type']!='request':continue
    q=m['request'];v=m['views'][0];one,two=v['players'];kind=q['kind'];opts=q['options'];ids=[o['id'] for o in opts];pick='ok'
    assert not m['views'][1]['players'][0]['hand'] and not m['views'][0]['players'][1]['hand'],'Private hand leaked'
    if kind=='InputPassPriority':
     if not cast and q['seat']==1 and v['turn']==1 and v['phase']=='MAIN1' and not v['stackItems']:
      source=next(c['id'] for c in one['hand'] if c['name']==spell);initial=libs(q);before=[{c['id'] for c in p['battlefield']} for p in v['players']];initial_exile={c['id'] for c in one['exileCards']};attackers=[c['id'] for c in one['battlefield'] if c['creature']];blockers=[c['id'] for c in two['battlefield'] if c['creature'] and c['name']!='Wind Drake'];pick='card:'+str(source);cast=True;snap(m,'before')
     elif toy and cast and not v['stackItems']:
      if v['phase']=='MAIN1' and source in {c['id'] for c in one['battlefield']}:assert paid and sum(c['tapped'] for c in one['battlefield'] if c['land'])==3;river_ready=True
      elif len(piles)==1:
       assert river_ready and len(split[0])==1 and split[0][0]==set(initial[0][:2]);assert {c['id'] for c in one['hand']}==set(initial[0][:2]) and {c['id'] for c in one['exileCards']}==set(initial[0][2:3]) and not one['graveyardCards'] and libs(q)==[initial[0][3:],initial[1]]
       assert all(p['life']==20 for p in v['players']) and sum(one['manaPool'])==0;snap(m,'resolved');return finish('PASS','Actual The Celestial Toymaker cast WUB/attack/top-three subset/defending-player predefined one-hidden pile: concealed payload has no IDs, chosen exact two Library-origin defined IDs move from exile to private hand, remainder exile and exact library order/priority restore.')
     elif river and cast and not v['stackItems']:
      if v['phase']=='MAIN1' and source in {c['id'] for c in one['battlefield']}:assert paid and sum(c['tapped'] for c in one['battlefield'] if c['land'])==2;river_ready=True
      elif v['phase']=='MAIN2':
       assert river_ready and block_seen and [len(s) for s in split]==[1,1] and len(blocked)==2 and one['life']==two['life']==20 and libs(q)==initial
       assert {c['id'] for c in one['graveyardCards']}==set(attackers) and {c['id'] for c in two['graveyardCards']}==set(blockers)
       assert all('pile' not in c.get('currentRules','').lower() for p in v['players'] for c in p['battlefield'] if c['creature']);assert all(not any('Raging River Left'==c['name'] or 'Raging River Right'==c['name'] for c in p['effectMarkers']) for p in v['players'])
       snap(m,'resolved');(folder/'choices.json').write_text(json.dumps(dict(attackers=attackers,blockers=blockers,blocked=[list(x) for x in blocked],initialLibraries=initial,finalLibraries=libs(q)),indent=2),encoding='utf8');return finish('PASS','Actual Raging River cast RR, attack, both native left/right separations, flying unrestricted/legal same-pile and absent cross-pile block options, exact combat deaths and end-combat restoration.')
     elif cast and not v['stackItems'] and (cancelled or source in [c['id'] for c in one['graveyardCards']+one['exileCards']]):
      current=libs(q);assert sum(one['manaPool'])+sum(two['manaPool'])==0
      if cancel:
       assert cancelled and not paid and not piles and not any(split) and [c['id'] for c in one['hand']]==[source] and not one['graveyardCards'];assert current==initial and all(not c['tapped'] for c in one['battlefield'])
      elif whims:
       assert paid and [len(s) for s in split]==[2,2],split
       for seat,p in enumerate(v['players']):
        groups=split[seat]+[before[seat]-set().union(*split[seat])];assert set().union(*groups)==before[seat] and not any(groups[i]&groups[j] for i in range(3) for j in range(i+1,3))
        remaining={c['id'] for c in p['battlefield']};lost=before[seat]-remaining;assert lost in groups,('Native sacrifice was not one exact pile',lost,groups)
        if case=='whims-replaced' and seat==0:assert {c['id'] for c in p['exileCards']}==lost|{source} and not p['graveyardCards'] and lost,'Sacrificed pile/source not exiled'
        else:assert {c['id'] for c in p['graveyardCards']}==lost|({source} if seat==0 else set());assert not p['exileCards']
        assert p['life']==20
       if case=='whims-replaced':assert any(c['name']=='Leyline of the Void' for c in two['battlefield']),'Replacement source did not survive chosen empty pile'
       assert current==initial and not piles
      else:
       size=4 if abstract else 3;pool=initial[0][:size*2];expected=[set(pool[:size]),set(pool[size:])];assert len(piles)==(1 if abstract else 2) and paid
       if abstract:chosen_index=1 if case.endswith('hidden') else 2
       chosen=expected[chosen_index-1];hand={c['id'] for c in one['hand']};grave={c['id'] for c in one['graveyardCards']}
       assert hand==(set(pool)-chosen if abstract else chosen) and grave==({source}|(chosen if abstract else set(pool)-chosen)),('Wrong predefined-pile destination',hand,grave,expected,chosen_index)
       assert {c['id'] for c in one['exileCards']}==initial_exile and current[0]==initial[0][size*2:] and current[1]==initial[1] and one['life']==(20 if abstract else 17) and two['life']==20
       assert sum(c['tapped'] for c in one['battlefield'])==(6 if abstract else 4)
      snap(m,'resolved');(folder/'choices.json').write_text(json.dumps(dict(source=source,initialLibraries=initial,finalLibraries=current,beforeBoard=[sorted(x) for x in before],split=[[sorted(x) for x in s] for s in split],pileRequests=piles,flip=flip,chosen=chosen_index),ensure_ascii=False,indent=2)+'\n',encoding='utf8');return finish('PASS','Actual '+spell+': exact native pile identities, payment/cancellation, destinations, private views and priority restored.')
    elif 'PayMana' in kind:
     pick='cancel' if cancel else 'ok';cancelled=cancel;paid=not cancel
    elif kind=='ChoosePile':
     if toy:
      assert q['seat']==2 and len(q['piles'])==2 and len(split[0])==1;left,right=q['piles'];assert left['concealed'] and left['cardCount']==2 and not left['cards'] and not right['concealed'] and {c['id'] for c in right['cards']}==set(initial[0][2:3]);pick='pile:1';piles.append(dict(requestId=q['requestId'],seat=2,option=pick));snap(m,'pile-1')
      assert pick in ids;print(case,step,v['phase'],kind,pick,flush=True);proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=q['seat'],requestId=q['requestId'],option=pick))+'\n');proc.stdin.flush();continue
     count=len(piles);assert q['seat']==(2 if abstract or count==1 else 1),('Wrong pile operator',q['seat'],count)
     size=4 if abstract else 3;expected=[set(initial[0][:size]),set(initial[0][size:size*2])];assert [o['id'] for o in opts]==['pile:1','pile:2'] and q.get('selectionPurpose')=='pile'
     assert len(q['piles'])==2
     for i,pile in enumerate(q['piles']):
      assert pile['pileId']==i+1 and pile['cardCount']==len(expected[i])
      assert {c['id'] for c in pile['cards']}==expected[i]
      if expected[i] and not abstract and count==0:
       assert all(c.get('knownFacePrintingKey') or c.get('printingKey') for c in pile['cards']),('Caster cannot see either face-down pile',pile)
      if expected[i] and (abstract or count==1):
       hidden=(i==0 if abstract else i+1!=flip)
       assert all(c.get('faceDown')==hidden for c in pile['cards'])
       if hidden:assert all(not c.get('knownFaceName') and not c.get('printingKey') for c in pile['cards']),'Opponent sees hidden pile face'
       else:assert all(c.get('printingKey') for c in pile['cards'])
     if q['seat']==1:pick='pile:'+str(flip)
     else:pick='pile:'+str(1 if abstract and case.endswith('hidden') else 2 if abstract else chosen_index)
     piles.append(dict(requestId=q['requestId'],seat=q['seat'],groups=[sorted(x) for x in expected],option=pick));snap(m,'pile-'+str(count+1))
    elif whims and (kind=='SelectCards' or kind.startswith('InputSelectCards')):
     seat=q['seat']-1;cards=[o for o in opts if o.get('card')];pool={o['card']['id'] for o in cards};assert pool==before[seat]-split_ids[seat] and q.get('minCount',0)==0
     n=len(split[seat]);assert n<2;count=0 if case=='whims-empty' or case=='whims-replaced' and seat==1 else len(before[seat])//3
     selected={o['card']['id'] for o in cards if o.get('selected')}
     if len(selected)<count:pick=next(o['id'] for o in cards if o['card']['id'] not in selected)
     else:split[seat].append(selected);split_ids[seat]|=selected;pick='confirm-cards' if 'confirm-cards' in ids else 'ok';snap(m,'split-'+str(seat+1)+'-'+str(n+1))
    elif toy and (kind=='SelectCards' or kind.startswith('InputSelectCards') or kind.startswith('InputSelectEntities')):
     assert q['seat']==1;cards=[o for o in opts if o.get('card')];assert {o['card']['id'] for o in cards}==set(initial[0][:3]);selected={o['card']['id'] for o in cards if o.get('selected')}
     if len(selected)<2:pick=next(o['id'] for o in cards if o['card']['id'] in initial[0][:2] and o['card']['id'] not in selected)
     else:split[0].append(selected);pick='confirm-cards' if 'confirm-cards' in ids else 'ok';snap(m,'toy-split')
    elif toy and kind=='InputAttack':
     assert river_ready;candidate=next((o for o in opts if o['id'].startswith('attack:') and o['attackerId']==source and o['attackerId'] not in attacked),None)
     if candidate:pick=candidate['id'];attacked.add(source)
    elif river and (kind=='SelectCards' or kind.startswith('InputSelectCards')):
     seat=q['seat']-1;cards=[o for o in opts if o.get('card')];expected=set(attackers if seat==0 else blockers);assert {o['card']['id'] for o in cards}==expected and 'left' in q['message'].lower()
     selected={o['card']['id'] for o in cards if o.get('selected')}
     if not selected:pick=next(o['id'] for o in cards if o['card']['id']==min(expected))
     else:assert selected=={min(expected)};split[seat].append(selected);pick='confirm-cards' if 'confirm-cards' in ids else 'ok';snap(m,'river-split-'+str(seat+1))
    elif river and kind=='InputAttack':
     assert river_ready;candidate=next((o for o in opts if o['id'].startswith('attack:') and o['attackerId'] in attackers and o['attackerId'] not in attacked and o['defenderKind']=='player'),None)
     if candidate:pick=candidate['id'];attacked.add(candidate['attackerId'])
     else:assert attacked==set(attackers)
    elif river and kind=='InputBlock':
     assert {c['name'] for c in two['effectMarkers']}=={'Raging River Left','Raging River Right'},'Missing native combat restriction helpers'
     assert [len(s) for s in split]==[1,1];native={(o['attackerId'],o['defenderId']) for o in opts if o['id'].startswith('block:')};left_a=min(attackers);left_b=min(blockers);right_a=max(attackers);right_b=max(blockers);fly=next(c['id'] for c in two['battlefield'] if c['name']=='Wind Drake')
     assert (left_a,left_b) in native and (right_a,right_b) in native and (left_a,right_b) not in native and (right_a,left_b) not in native and {(a,fly) for a in attackers}<=native,('Wrong native river block legality',native)
     candidate=next((o for o in opts if o['id'].startswith('block:') and (o['attackerId'],o['defenderId']) in {(left_a,left_b),(right_a,right_b)}-blocked),None)
     if candidate:pick=candidate['id'];blocked.add((candidate['attackerId'],candidate['defenderId']))
     else:block_seen=True;snap(m,'river-blocks')
    elif kind=='RevealCards':pick='confirm-cards'
    elif kind=='dialog':pick=ids[0]
    elif kind=='InputMulligan':pick='keep'
    elif kind=='OrderAbilities':pick='confirm-order'
    elif kind in ['InputConfirm','InputConfirmMulligan','InputAttack','InputBlock']:pick='ok'
    else:raise AssertionError(('Unhandled pile request',q))
    assert pick in ids,(case,step,kind,pick,q);print(case,step,v['phase'],kind,pick,flush=True);proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=q['seat'],requestId=q['requestId'],option=pick))+'\n');proc.stdin.flush()
   raise AssertionError('Step limit')
  except Exception as e:finish('FAIL',repr(e));raise
  finally:
   if proc.poll() is None:proc.kill();proc.wait()
if __name__=='__main__':
 selected=list(sys.argv[1:] or CASES);assert all(k in CASES for k in selected);results=[];f=OUT/'run-summary.json';f.write_text(json.dumps(dict(status='RUNNING',selected=selected,results=[])),encoding='utf8')
 try:
  subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*[str(p) for p in FIXTURES]],check=True)
  for case in selected:r=run(case);results.append(r);print(json.dumps(r),flush=True)
 except BaseException:f.write_text(json.dumps(dict(status='FAIL',selected=selected,results=results)),encoding='utf8');raise
 f.write_text(json.dumps(dict(status='PASS',selected=selected,results=results),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
