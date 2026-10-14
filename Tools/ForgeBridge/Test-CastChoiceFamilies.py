"""Actual casting/payment/stack/result checks for additional native choice families."""
import datetime, hashlib, json, os, queue, subprocess, sys, tempfile, threading
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime'
CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'Tools/Validation/Reports/CastChoiceFamilies';OUT.mkdir(parents=True,exist_ok=True)
FIXTURE=ROOT/'Tools/ForgeBridge/tests/CastChoiceFamilyFixture.java'
CLASSES=ROOT/'.tools/cast-choice-families/classes';CLASSES.mkdir(parents=True,exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(FIXTURE)],check=True)
NAMES=dict(multikicker='Everflowing Chalice',buyback='Whispers of the Muse',bargain='Torch the Tower',gift='Parting Gust',overload='Mizzium Mortars',dash='Mardu Scout',escalate='Collective Defiance',conspire='Gleeful Sabotage',casualty='Rob the Archives',squad='Sicarian Infiltrator')
NAMES.update(confluence='Mystic Confluence',xconfluence='Doomsday Confluence')
NAMES.update(profit='Cut of the Profits')
PAID=dict(multikicker=('MULTIKICKER',2,4),buyback=('Buyback',1,6),bargain=('Bargain',1,1),gift=('PromiseGift',1,2),overload=('Overload',1,6),dash=('Dash',1,2),escalate=('',0,4),conspire=('CONSPIRE',1,2),casualty=('CASUALTY',1,2),squad=('SQUAD',2,7))
PAID.update(confluence=('',0,5),xconfluence=('',0,7))
PAID.update(profit=('CASUALTY',3,4))
BASE=dict(multikicker=0,buyback=1,bargain=1,gift=2,overload=2,dash=2,escalate=3,conspire=2,casualty=2,squad=3)
BASE.update(xconfluence=1)
BASE.update(profit=4)
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()

def run(case):
 family=case.split('-')[0];extra=not case.endswith('-base');name=NAMES[family]
 with tempfile.TemporaryDirectory(prefix='mtgb-cast-family-') as temp,(OUT/(case+'.log')).open('w',encoding='utf8') as log,(OUT/(case+'.jsonl')).open('w',encoding='utf8') as trace:
  start=Path(temp)/'start.json';deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
  start.write_text(json.dumps(dict(session=case,seed=4,startingLife=20,firstPlayerSeat=1,decks=[deck,deck])),encoding='utf8')
  p=subprocess.Popen([str(RT/CFG['java']),'-Djava.awt.headless=true','-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.CastChoiceFamilyFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
  q=queue.Queue();threading.Thread(target=lambda:[q.put(json.loads(line)) for line in p.stdout],daemon=True).start()
  def snap(m,label):
   for i,v in enumerate(m['views']):
    public=dict(v)
    if m['request']['seat']==i+1:public.update(request=m['request'],awaitingInput=True)
    (OUT/(case+'-'+label+'-seat'+str(i+1)+'.json')).write_text(json.dumps(public),encoding='utf8')
  cast=False;seen=False;copied=False;resolved=False;last=None
  try:
   for step in range(170):
    while True:
     m=q.get(timeout=40);trace.write(json.dumps(m)+'\n');trace.flush()
     assert m['type'] not in ('fatal','ended','rejected'),m
     if m['type']=='request':break
    r=m['request'];last=r;opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];v=m['views'][0];one,two=v['players'];stack=v['stackItems'];pick='ok'
    log.write('STEP '+str(step)+' '+kind+' '+json.dumps(opts,ensure_ascii=False)+'\n');log.flush()
    for entry in stack:
     if entry['kind']!='spell' or entry['source']['name']!=name:continue
     costs=entry.get('castingChoices',[]);modes=entry.get('chosenModes',[])
     if family=='escalate':assert sorted(a['order'] for a in modes)==([2,3] if extra else [3]),modes
     elif family in ('confluence','xconfluence'):
      expected=[3,3,3] if family=='confluence' else [2,2,2] if extra else []
      assert [a['order'] for a in modes]==expected,modes
      if family=='xconfluence':assert entry.get('hasXValue') and entry['xValue']==(3 if extra else 0)
     else:
      required,count,_=PAID[family]
      if extra:assert any(c['kind']==required and c['count']==count for c in costs),(family,costs)
      else:assert not costs,costs
     other=next(e for e in m['views'][1]['stackItems'] if e['id']==entry['id'])
     assert other.get('castingChoices',[])==costs and other.get('chosenModes',[])==modes
     copied |= entry.get('copied',False)
     if not seen:snap(m,'stack');seen=True
    if kind=='InputConfirmMulligan':pass
    elif kind=='InputPassPriority':
     field=one['battlefield'];opponent=two['battlefield']
     done=cast and seen and not stack and (name in one['graveyard'] or family=='buyback' and one['libraryCount']==52 or family in ('multikicker','dash','squad') and any(c['name']==name for c in field))
     if done and not resolved:
      spent=sum(c['land'] and c['tapped'] for c in field);assert spent==(PAID[family][2] if extra else BASE[family]),(spent,family)
      if family=='multikicker':
       card=next(c for c in field if c['name']==name);assert next((c['count'] for c in card['counters'] if c['kind']=='CHARGE'),0)==(2 if extra else 0)
      elif family=='buyback':assert one['handCount']==(9 if extra else 8) and any(c['name']==name for c in one['hand'])==extra and (name in one['graveyard'])!=extra
      elif family=='bargain':
       assert ("Tormod's Crypt" in one['graveyard'])==extra
       assert any(c['name']=='Hill Giant' for c in two['exileCards'])==extra
       if not extra:assert next(c for c in opponent if c['name']=='Hill Giant')['damage']==2
      elif family=='gift':
       assert any(c['name']=='Hill Giant' for c in two['exileCards'])
       fish=[c for c in opponent if c.get('token')];assert len(fish)==int(extra)
       if extra:assert fish[0]['tapped'] and fish[0]['power']==fish[0]['toughness']==1
      elif family=='overload':assert 'Hill Giant' in two['graveyard'] and ('Grizzly Bears' in two['graveyard'])==extra
      elif family=='dash':assert ('HASTE' in next(c for c in field if c['name']==name)['keywords'])==extra
      elif family=='escalate':assert two['life']==17 and ('Hill Giant' in two['graveyard'])==extra
      elif family=='conspire':
       assert "Tormod's Crypt" in two['graveyard'] and ('Sol Ring' in two['graveyard'])==extra and copied==extra
       assert sum(c['tapped'] for c in field if c['creature'])==(2 if extra else 0)
      elif family=='profit':
       assert ('Hill Giant' in one['graveyard'])==extra and copied==extra and one['libraryCount']==(49 if extra else 51) and one['handCount']==(11 if extra else 9) and one['life']==(16 if extra else 18)
      elif family=='casualty':
       assert ('Grizzly Bears' in one['graveyard'])==extra and len(one['exileCards'])==(4 if extra else 2) and one['libraryCount']==(49 if extra else 51) and copied==extra
      elif family=='confluence':assert one['handCount']==10 and one['libraryCount']==50
      elif family=='xconfluence':
       tokens=[c for c in field if c.get('token')];assert len(tokens)==(3 if extra else 0) and all(c['power']==3 and c['toughness']==3 and 'MENACE' in c['keywords'] for c in tokens)
      elif family=='squad':
       bodies=[c for c in field if c['name']==name];assert len(bodies)==(3 if extra else 1) and sum(bool(c.get('token')) for c in bodies)==(2 if extra else 0)
       assert all(c['power']==1 and c['toughness']==2 for c in bodies) and one['handCount']==(10 if extra else 8) and one['libraryCount']==(50 if extra else 52)
      snap(m,'resolved');resolved=True
      if family not in ('dash','gift'):return 'PASS '+case+' native choice/payment/two-seat stack/effects/priority'
     if resolved and family in ('dash','gift') and v['phase']=='END_OF_TURN' and not stack:
      if family=='dash':assert any(c['name']==name for c in one['hand'])==extra and any(c['name']==name for c in field)!=extra
      else:
       giant=next((c for c in opponent if c['name']=='Hill Giant'),None);assert (giant is None)==extra
       if not extra:assert giant['power']==4 and giant['toughness']==4
      snap(m,'endstep');return 'PASS '+case+' native choice/payment/two-seat stack/effects/endstep/priority'
     if not cast and r['seat']==1 and v['phase']=='MAIN1' and not stack:
      pick=next(o['id'] for o in opts if o.get('card',{}).get('name')==name or o['label'].startswith(name+' ['));cast=True
    elif kind=='ChooseOptions':
     assert family in ('buyback','bargain','gift');snap(m,'optional');pick='select:0' if extra and not r['selectedCount'] else 'confirm-cards'
    elif kind=='ChooseModes':
     assert family=='escalate';snap(m,'modes');want=[2,3] if extra else [3]
     pick=next((o['id'] for o in opts if o['id'].startswith('mode:') and o.get('order') in want and not o.get('selected')),'confirm-modes')
    elif kind=='ChooseNumber':
     assert family in ('multikicker','squad','xconfluence','profit');snap(m,'number');pick='number-plus' if r['selectedValue']<(2 if family=='profit' else (3 if family=='xconfluence' else 2) if extra else 0) else 'confirm-number'
    elif kind=='InputConfirm':
     if family in ('conspire','casualty','profit'):pick='ok' if extra else 'cancel'
     else:pick='ok'
    elif kind.startswith('InputPayMana'):
     pick=next((i for i in ids if i.startswith('mana:')),None) or next(o['id'] for n in ['Island','Mountain','Forest','Plains','Swamp'] for o in opts if o['label'].startswith(n+' ['))
    elif kind=='InputSelectTargets':
     if r['selectedCount'] and 'ok' in ids:pick='ok'
     else:
      target='Sol Ring' if family=='conspire' and seen else "Tormod's Crypt" if family=='conspire' else 'Hill Giant'
      pick=next((o['id'] for o in opts if o.get('card',{}).get('name')==target),None) or 'player:'+str(two['targetId'])
    elif kind.startswith('InputSelect') or kind=='ChooseCards':
     pick='ok' if 'ok' in ids and r.get('selectedCount',0)>=r.get('minCount',1) else next(o['id'] for o in opts if o.get('card',{}).get('name') in ["Tormod's Crypt",'Grizzly Bears','Llanowar Elves','Hill Giant'] and not o.get('selected'))
    elif kind=='Scry':pick=next((i for i in ids if i.startswith('scry-top:')),None) or next(i for i in ids if i.startswith('confirm'))
    elif kind=='OrderAbilities':pick=next((i for i in ids if i.startswith('order:')),'confirm-order')
    elif kind=='dialog':
     choices=[o for o in opts if o['id']!='cancel'];snap(m,'dialog')
     if family in ('confluence','xconfluence'):
      needle='Draw a card' if family=='confluence' else 'Create a 3/3'
      pick=next(o['id'] for o in choices if needle in o['label'])
     elif family in ('overload','dash'):
      token=family.capitalize();pick=next(o['id'] for o in choices if (token.lower() in o['label'].lower())==extra)
     elif family=='gift':pick=choices[0]['id']
     else:pick=choices[0]['id']
    elif kind in ('InputAttack','InputBlock'):pick='ok'
    else:raise AssertionError((kind,opts))
    assert pick in ids,(kind,pick,ids)
    p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   raise AssertionError('No completion')
  except Exception as e:raise AssertionError((repr(e),last)) from e
  finally:
   if p.poll() is None:
    p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
    try:p.wait(timeout=3)
    except subprocess.TimeoutExpired:p.kill();p.wait()

results=[]
for case in sys.argv[1:] or [f+s for f in NAMES for s in (('',) if f=='confluence' else ('','-base'))]:
 try:result=run(case)
 except Exception as e:result='FAIL '+case+': '+repr(e)
 results.append(result)
 (OUT/(case+'-result.json')).write_text(json.dumps(dict(case=case,result=result,timeUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),testSha256=sha(Path(__file__)),fixtureSha256=sha(FIXTURE),runtimeManifest=CFG['runtimeHash'],traceSha256=sha(OUT/(case+'.jsonl'))),indent=2),encoding='utf8')
 print(result,flush=True)
(OUT/'result.txt').write_text('\n'.join(results),encoding='utf8')
sys.exit(any(r.startswith('FAIL') for r in results))
