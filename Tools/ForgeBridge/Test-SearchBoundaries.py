"""Real native optional/restricted/prohibited and mixed-origin searches."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'.tools/search-boundaries-1007/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURES=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['LibraryCostFixture.java','SearchBoundaryFixture.java']];SHA=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES={
 'optional-accept':{},'optional-decline':{},'optional-empty':{'emptyLibrary':True},
 'restricted-select':{},'restricted-fail':{},'prohibited':{},
 'mixed-grave':{},'mixed-hand':{},'opponent-accept':{},'opponent-decline':{},
}
def run(case):
 spec=CASES[case];mixed=case.startswith('mixed');optional=case.startswith('optional');tempt=case.startswith('opponent');spell='Tempt with Discovery' if tempt else 'Ornithopter' if mixed else 'Farhaven Elf' if optional else 'Rampant Growth'
 hand=[spell]+(['Ornithopter']*2 if case=='mixed-hand' else []);board=['Forest']*4+(['Mishra, Artificer Prodigy'] if mixed else []);op=['Aven Mindcensor'] if case.startswith('restricted') else ['Mindlock Orb'] if case=='prohibited' or mixed else []
 folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);audit=folder/'audit';audit.mkdir()
 meta=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driverSha256=SHA(Path(__file__)),fixtures=[dict(path=p.relative_to(ROOT).as_posix(),sha256=SHA(p)) for p in FIXTURES]);(folder/'driver.py').write_bytes(Path(__file__).read_bytes())
 for p in FIXTURES:(folder/p.name).write_bytes(p.read_bytes())
 def finish(status,message):
  r=dict(meta,status=status,result=status+' '+case+': '+message)
  for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return r
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
  start=dict(session=case,fixture=case,hand1=hand,hand2=[],board1=board,board2=op,grave1=['Ornithopter']*2 if case=='mixed-grave' else [],grave2=[],emptyLibrary=spec.get('emptyLibrary',False),seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK],auditDir=str(audit));(folder/'start.json').write_text(json.dumps(start,indent=2)+'\n',encoding='utf8');p=Path(temp)/'start.json';p.write_text(json.dumps(start),encoding='utf8')
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.SearchBoundaryFixture',str(RT/CFG['assets']),str(p)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');messages=queue.Queue()
  def reader():
   try:
    for line in proc.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,label):
   for seat,v in enumerate(m['views'],1):(folder/(label+'-seat'+str(seat)+'.json')).write_text(json.dumps(dict(v,request=m.get('request'),awaitingInput=m['type']=='request'),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  def libraries(q):
   p=audit/(str(q['requestId'])+'.json');until=time.monotonic()+3
   while not p.exists() and time.monotonic()<until:time.sleep(.01)
   assert p.exists(),'Missing library audit';return [x['library'] for x in json.loads(p.read_text())['players']]
  cast=False;initial=None;initial_board=None;source=None;chosen=None;paid=False;confirms=[];picker=0;altid=None;altids=set();declined=False;chosen_seats=[[],[]]
  try:
   for step in range(140):
    m=messages.get(timeout=35);assert m is not None,'Native exited';trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush();assert m['type'] not in ['fatal','rejected','ended'],m
    if m['type']!='request':continue
    q=m['request'];v=m['views'][0];one,two=v['players'];kind=q['kind'];opts=q['options'];ids=[o['id'] for o in opts];pick='ok';snap(m,'request-'+str(step));hand1=one['hand']
    assert not m['views'][1]['players'][0]['hand'] and not m['views'][0]['players'][1]['hand'],'Private hand leak'
    if kind=='InputPassPriority':
     if not cast and q['seat']==1 and v['turn']==1 and v['phase']=='MAIN1' and not v['stackItems']:
      assert [c['name'] for c in two['battlefield']]==op,'Opening search restriction absent'
      initial=libraries(q);initial_board=[c['id'] for c in one['battlefield']];source=next(c['id'] for c in hand1 if c['name']==spell);altids={c['id'] for c in (one['graveyardCards'] if case=='mixed-grave' else hand1) if c['name']=='Ornithopter' and c['id']!=source};altid=min(altids) if altids else None;pick='card:'+str(source);cast=True;snap(m,'before')
     elif cast and not v['stackItems'] and (source in [c['id'] for c in one['battlefield']+one['graveyardCards']]):
      current=libraries(q);new=[c for c in one['battlefield'] if c['id'] not in initial_board];assert one['life']==two['life']==20 and sum(one['manaPool'])+sum(two['manaPool'])==0
      if not tempt:assert current[1]==initial[1]
      if tempt:
       accepted=case=='opponent-accept';assert paid and source in [c['id'] for c in one['graveyardCards']] and not hand1 and not two['hand']
       assert len(new)==(2 if accepted else 1) and {c['id'] for c in new}==set(chosen_seats[0]) and all(not c['tapped'] for c in new)
       assert len(two['battlefield'])==(1 if accepted else 0) and {c['id'] for c in two['battlefield']}==set(chosen_seats[1])
       assert len(chosen_seats[1])==(1 if accepted else 0) and any('Search your library?'==s for s in confirms)
       for seat in range(2):assert set(current[seat])==set(initial[seat])-set(chosen_seats[seat])
      elif mixed:
       assert len(new)==2 and {c['id'] for c in new}=={source,chosen} and chosen in altids and all(c['name']=='Ornithopter' for c in new);assert current[0]==initial[0] and picker>0
       assert {c['id'] for c in (one['graveyardCards'] if case=='mixed-grave' else hand1)}==altids-{chosen}
       assert not (hand1 if case=='mixed-grave' else one['graveyardCards'])
      elif optional:
       assert paid and not hand1 and source in [c['id'] for c in new]
       if case=='optional-accept':assert len(new)==2 and chosen in initial[0] and any(c['id']==chosen and c['tapped'] for c in new) and set(current[0])==set(initial[0])-{chosen} and picker>0
       else:assert len(new)==1 and current[0]==initial[0] and picker==0 and (declined if case=='optional-decline' else initial[0]==[])
      else:
       assert paid and source in [c['id'] for c in one['graveyardCards']] and not hand1
       if case=='restricted-select':assert picker>0 and len(new)==1 and new[0]['id']==chosen and new[0]['tapped'] and chosen in initial[0][:4] and set(current[0])==set(initial[0])-{chosen}
       else:assert not new and set(current[0])==set(initial[0]) and (picker>0 if case=='restricted-fail' else picker==0)
      snap(m,'resolved');(folder/'choices.json').write_text(json.dumps(dict(confirms=confirms,picker=picker,chosen=chosen,source=source,alternateId=altid,initialLibraries=initial,finalLibraries=current),ensure_ascii=False,indent=2)+'\n',encoding='utf8');return finish('PASS','Actual '+spell+' casting/payment/search: exact original IDs/zones, restricted or alternative permission, private hands, both library identities, resources and main priority restored.')
    elif 'PayMana' in kind:pick='ok';paid=True
    elif kind=='InputConfirm':
     text=q.get('message','');confirms.append(text);snap(m,'confirm-'+str(len(confirms)))
     if tempt:assert q['seat']==q['playerSeat']==2;pick='ok' if case=='opponent-accept' else 'cancel'
     elif optional and case=='optional-decline':pick='cancel';declined=True
     elif mixed and 'library' in text.lower():pick='cancel'
     elif mixed and 'graveyard' in text.lower():pick='ok' if case=='mixed-grave' else 'cancel'
     else:pick='ok'
    elif kind=='RevealCards':pick='confirm-cards'
    elif kind in ['SelectCards'] or kind.startswith('InputSelectEntities'):
     picker+=1;cards=[o for o in opts if o.get('card')];nativeids={o['card']['id'] for o in cards}
     if case.startswith('restricted'):assert nativeids<=set(initial[0][:4]) and len(nativeids)==4,(nativeids,initial[0][:4]);assert {c['id'] for c in q.get('previewCards',[])}<=set(initial[0][:4]),'Restricted preview exposes deeper library'
     if mixed:assert nativeids==altids,('Alternate source illegal candidate',q,altids)
     snap(m,'picker-'+str(picker))
     if case=='restricted-fail':pick='cancel' if 'cancel' in ids else 'confirm-cards' if 'confirm-cards' in ids else 'ok'
     elif q.get('selectedCount'):pick='confirm-cards' if 'confirm-cards' in ids else 'ok'
     else:
      if tempt:assert nativeids==set(initial[q['seat']-1])-set(chosen_seats[q['seat']-1]) and q['playerSeat']==q['seat']
      chosen=cards[0]['card']['id'];pick=cards[0]['id'];chosen_seats[q['seat']-1].append(chosen)
    elif kind=='InputMulligan':pick='keep'
    elif kind=='OrderAbilities':pick='confirm-order'
    elif kind in ['InputConfirmMulligan','InputAttack','InputBlock']:pick='ok'
    else:raise AssertionError(('Unhandled search request',q))
    assert pick in ids,(case,step,kind,pick,q);print(case,step,v['phase'],kind,pick,flush=True);proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=q['seat'],requestId=q['requestId'],option=pick))+'\n');proc.stdin.flush()
   raise AssertionError('Step limit')
  except Exception as e:finish('FAIL',repr(e));raise
  finally:
   if proc.poll() is None:proc.kill();proc.wait()
if __name__=='__main__':
 selected=list(sys.argv[1:] or CASES);assert all(k in CASES for k in selected);results=[];p=OUT/'run-summary.json';p.write_text(json.dumps(dict(status='RUNNING',selected=selected,results=[])),encoding='utf8')
 try:
  subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*[str(p) for p in FIXTURES]],check=True)
  for case in selected:r=run(case);results.append(r);print(json.dumps(r),flush=True)
 except BaseException:p.write_text(json.dumps(dict(status='FAIL',selected=selected,results=results)),encoding='utf8');raise
 p.write_text(json.dumps(dict(status='PASS',selected=selected,results=results),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
