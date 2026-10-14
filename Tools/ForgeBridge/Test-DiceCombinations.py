"""Real five dice differences: cost/choices/native results; no injected outcome."""
import hashlib,json,os,queue,re,subprocess,sys,tempfile,threading,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'.tools/dice-combinations-1007/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURES=[ROOT/'Tools/ForgeBridge/tests'/n for n in ['LibraryCostFixture.java','RandomMechanicFixture.java','DiceCombinationFixture.java']]
SHA=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
CASES=['fixed-negative','choose-low','choose-high','multiple-modifiers','reroll-both']
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
def run(case):
 f=OUT/case/str(time.time_ns());f.mkdir(parents=True);audit=f/'audit';audit.mkdir()
 meta=dict(case=case,folder=f.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driverSha256=SHA(Path(__file__)),fixtures=[dict(path=p.relative_to(ROOT).as_posix(),sha256=SHA(p)) for p in FIXTURES]);(f/'driver.py').write_bytes(Path(__file__).read_bytes())
 for p in FIXTURES:(f/p.name).write_bytes(p.read_bytes())
 def finish(status,message):
  r=dict(meta,status=status,result=status+' '+case+': '+message);(f/'result.json').write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf8');return r
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(f/'engine.log').open('w',encoding='utf8') as log,(f/'trace.jsonl').open('w',encoding='utf8') as trace:
  start=dict(session=case,fixture=case,seed=5 if case=='fixed-negative' else 4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK],auditDir=str(audit));(f/'start.json').write_text(json.dumps(start,indent=2)+'\n',encoding='utf8');sp=Path(temp)/'start.json';sp.write_text(json.dumps(start),encoding='utf8')
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.DiceCombinationFixture',str(RT/CFG['assets']),str(sp)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');q=queue.Queue()
  def reader():
   try:
    for line in proc.stdout:q.put(json.loads(line))
   finally:q.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,label):
   for seat,v in enumerate(m['views'],1):(f/(label+'-seat'+str(seat)+'.json')).write_text(json.dumps(dict(v,request=m['request'],awaitingInput=True),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  def observe(r):
   p=audit/(str(r['requestId'])+'.json');until=time.monotonic()+3
   while time.monotonic()<until:
    try:return json.loads(p.read_text())['players']
    except (OSError,json.JSONDecodeError):time.sleep(.01)
   raise AssertionError('Read-only audit missing')
  initial=None;source=None;bear=None;paid=0;stage=0;chosen=None;offered=[];natural=None;modifications=0;modsources=set();rerollconfirmed=False;scry=0;deadline=time.monotonic()+120
  try:
   for step in range(240):
    assert time.monotonic()<deadline,'Deadline';m=q.get(timeout=35);assert m is not None,'Native exited';trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
    if m['type']!='request':continue
    r=m['request'];v=m['views'][0];one,two=v['players'];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts];pick='ok';snap(m,'request-'+str(step))
    assert not m['views'][1]['players'][0]['hand'] and not m['views'][0]['players'][1]['hand'],'Private hand leak'
    if kind=='InputPassPriority' and not v['stackItems']:
     if stage==0 and r['seat']==1 and v['phase']=='MAIN1':
      initial=observe(r);source=next(c['id'] for c in (one['battlefield'] if case=='fixed-negative' else one['hand']) if c['name']==('The Deck of Many Things' if case=='fixed-negative' else 'Contact Other Plane' if case=='multiple-modifiers' else 'Grave Endeavor'));bear=next((c['id'] for c in one['graveyardCards'] if c['name']=='Grizzly Bears'),None);snap(m,'before');pick='card:'+str(source);stage=1
     elif stage==1 and (case=='fixed-negative' and next(c for c in one['battlefield'] if c['id']==source)['tapped'] or any(c['id']==source for c in one['graveyardCards'])):
      a=observe(r);rolls=a[0]['rolls'];assert a[1]['library']==initial[1]['library'] and one['manaPool']==[0]*6
      notes=[n for n in v.get('notices',[]) if n['kind']=='dice'];assert notes and [(n['value'],n['actorSeat']) for n in notes]==[(n['value'],n['actorSeat']) for n in m['views'][1].get('notices',[]) if n['kind']=='dice']
      if case=='fixed-negative':
       assert paid==2 and a[0]['rolled']==1 and rolls==[1] and not one['hand'];assert sorted(c['id'] for c in one['graveyardCards'])==sorted(c['id'] for c in json.loads((f/'before-seat1.json').read_text())['players'][0]['hand']);assert a[0]['library']==initial[0]['library'] and one['life']==two['life']==20
       message='Natural die1 notification/stored roll preserved; actual -2 hand modifier yields -1 and discards two exact hand IDs after native Deck 2T payment; full libraries/life/pool/priority exact.'
      elif case=='multiple-modifiers':
       assert paid==4 and modifications==2 and len(modsources)==2 and a[0]['rolled']==1 and rolls==[natural+2]
       x=next(c for c in one['battlefield'] if c['name']=='Xenosquirrels');assert x['power']==x['toughness']==1 and sum(c['count'] for c in x['counters'] if c['kind']=='P1P1')==1 and one['life']==19 and two['life']==20
       draw=3 if rolls[0]==20 else 2 if 1<=rolls[0]<=19 else 0;assert a[0]['library']==initial[0]['library'][draw:] and {c['id'] for c in one['hand']}==set(initial[0]['library'][:draw]);assert scry==(3 if rolls[0]==20 else 2 if 10<=rolls[0]<=19 else 0)
       message='Both actual modifier source IDs applied to same natural die, +1 counter and one life paid separately; final+2 branch, exact card draw/library/resources and priority.'
      else:
       assert paid==(8 if case=='reroll-both' else 7) and a[0]['rolled']==(4 if case=='reroll-both' else 2) and len(rolls)==2 and sorted(rolls)==sorted(offered) and chosen in rolls
       other=next((x for x in rolls if x!=chosen),rolls[0]);b=next(c for c in one['battlefield'] if c['id']==bear)
       assert b['power']==b['toughness']==2+chosen and sum(c['count'] for c in b['counters'] if c['kind']=='P1P1')==chosen and one['life']==20+other and two['life']==20-other
       assert not one['hand'] and not any(c['id']==bear for c in one['graveyardCards']) and a[0]['library']==initial[0]['library']
       if case=='reroll-both':assert rerollconfirmed
       else:assert len(set(rolls))==2 and chosen==(min(rolls) if case=='choose-low' else max(rolls))
       message='Actual Grave Endeavor 5BB paid; chosen d10 result gives exact Bear ID that many counters, other result loses/gains life, full libraries and priority. '+('Two independent native dice selections/confirmation, one1 payment and two physical rerolls.' if case=='reroll-both' else 'Distinct low/high chosen via native result callback.')
      snap(m,'resolved');return finish('PASS',message)
    elif kind=='InputPassPriority':pick='ok'
    elif 'PayMana' in kind:
     colors=re.findall(r'\{([WUBRG])\}',r.get('remainingManaCost',''));allowed={'B':'Swamp','U':'Island'}
     pick=next((i for i in ids if i.startswith('mana:') and (not colors or i.split(':')[1] in colors)),None)
     if pick is None:pick=next('card:'+str(c['id']) for c in one['battlefield'] if 'card:'+str(c['id']) in ids and (not colors or c['name'] in [allowed[x] for x in colors]));paid+=1
    elif kind=='ChooseOptions' and 'reroll' in r['message'].lower():
     assert case=='reroll-both' and r['minCount']==0 and r['maxCount']==2
     snap(m,'reroll-selected-'+str(r.get('selectedCount',0)))
     if r.get('selectedCount',0)==2:pick='confirm-cards';rerollconfirmed=True
     else:pick=next(o['id'] for o in opts if o['id'].startswith('select:') and not o.get('selected'))
    elif kind=='dialog':
     text=r['message'].lower()
     if 'choose a roll to modify' in text:
      snap(m,'modify-choice');pick=ids[0] if modifications==0 else next(o['id'] for o in opts if o['label'].lower()=='none');natural=int(opts[0]['label']) if modifications==0 else natural
     elif 'choose a result' in text:
      offered=[int(o['label']) for o in opts];chosen=min(offered) if case=='choose-low' else max(offered);pick=next(o['id'] for o in opts if int(o['label'])==chosen);snap(m,'chosen-result')
     elif 'getabilitytoplay' in text:pick=next(o['id'] for o in opts if 'roll a d20' in o['label'].lower())
     else:raise AssertionError(('Unhandled dialog',r))
    elif kind=='InputConfirm' and 'increase or decrease' in r['message'].lower():modifications+=1;pick='ok';snap(m,'increment-'+str(modifications))
    elif kind=='InputConfirm':
     assert case=='multiple-modifiers' and ('counter(s) from Xenosquirrels' in r['message'] or 'pay 1 life' in r['message']);pick='ok';snap(m,'modifier-cost-'+str(modifications));modsources.add(next(c['id'] for c in one['battlefield'] if c['name']==('Xenosquirrels' if 'Xenosquirrels' in r['message'] else 'Night Shift of the Living Dead')))
    elif kind in ['InputSelectCardsFromList','InputSelectEntitiesFromList','SelectCards']:
     if r.get('selectedCount') and ('ok' in ids or 'confirm-cards' in ids):pick='confirm-cards' if 'confirm-cards' in ids else 'ok'
     else:
      pick=next(o['id'] for o in opts if o['id'].startswith(('card:','select:')) and not o.get('selected'));modsources.add(int(pick.split(':')[-1])) if case=='multiple-modifiers' else None
     snap(m,'source-choice-'+str(len(modsources)) if case=='multiple-modifiers' else 'grave-choice')
    elif kind=='Scry':
     scry=max(scry,r.get('selectedCount',0)+r.get('remainingCount',0));pick='confirm-scry' if 'confirm-scry' in ids else next(i for i in ids if i.startswith('scry-top:'))
    elif kind=='InputMulligan':pick='keep'
    elif kind=='RevealCards':pick='confirm-cards'
    elif kind=='OrderAbilities':pick='confirm-order'
    elif kind in ['InputConfirmMulligan','InputAttack','InputBlock']:pick='ok'
    else:raise AssertionError(('Unhandled',kind,r))
    assert pick in ids,(case,step,kind,pick,r);print(case,step,kind,pick,flush=True);proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');proc.stdin.flush()
   raise AssertionError('Step limit')
  except Exception as e:finish('FAIL',repr(e));raise
  finally:
   if proc.poll() is None:proc.kill();proc.wait()
if __name__=='__main__':
 selected=list(sys.argv[1:] or CASES);assert all(k in CASES for k in selected);results=[];summary=OUT/'run-summary.json'
 def save(status):summary.write_text(json.dumps(dict(status=status,selected=selected,results=results),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
 save('RUNNING')
 try:
  subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*[str(p) for p in FIXTURES]],check=True)
  for c in selected:results.append(run(c));print(results[-1]['result'],flush=True)
 except BaseException:save('FAIL');raise
 save('PASS')
