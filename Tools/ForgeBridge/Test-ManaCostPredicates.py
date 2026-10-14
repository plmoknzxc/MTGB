"""Actual activated/resolving payment predicates; initial positions only injected."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'.tools/mana-predicates-1006/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURE=ROOT/'Tools/ForgeBridge/tests/RestrictedManaFixture.java';SHA=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(FIXTURE)],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES={
 'activated-X':dict(board=['Rosheen Meanderer','Banshee'],hand=['Myr Retriever'],source='Banshee',x=3),
 'resolving-X':dict(board=['Rosheen Meanderer','Mountain'],hand=['Shock'],counter='Condescend',x=3,other=['Island','Wastes','Wastes','Wastes']),
 'resolving-twice-X':dict(board=['Rosheen Meanderer','Mountain'],hand=['Shock'],counter="Thassa's Intervention",x=2,other=['Island','Island','Wastes','Wastes']),
 'fixed-tax-forbidden':dict(board=['Rosheen Meanderer','Mountain'],hand=['Shock'],counter='Mana Leak',x=0,other=['Island','Wastes']),
 'resolving-C':dict(board=['Cultivator Drone','Cultivator Drone','Swamp','Wastes'],hand=['Bearer of Silence'],other=['Grizzly Bears'],x=0),
}
def run(case):
 spec=CASES[case];folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True)
 meta=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driverSha256=SHA(Path(__file__)),fixtures=[dict(path=FIXTURE.relative_to(ROOT).as_posix(),sha256=SHA(FIXTURE))])
 (folder/'driver.py').write_bytes(Path(__file__).read_bytes());(folder/'RestrictedManaFixture.java').write_bytes(FIXTURE.read_bytes())
 def finish(status,message):
  result=dict(meta,status=status,result=status+' '+case+': '+message)
  for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return result
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
  start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,fixture=case,hand=spec['hand'],lands=spec['board'],opponentHand=[spec['counter']] if 'counter' in spec else [],opponentBoard=spec.get('other',[]),seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK])),encoding='utf8')
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.RestrictedManaFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
  messages=queue.Queue()
  def reader():
   try:
    for line in proc.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,label):
   for seat,v in enumerate(m['views'],1):(folder/(label+'-seat'+str(seat)+'.json')).write_text(json.dumps(dict(v,request=m['request'],awaitingInput=True),ensure_ascii=False,indent=2),encoding='utf8')
  floated=set();cast=False;counter=False;stack_seen=False;effect_paid=0;negative_seen=False;scry=False;last_payment=None
  try:
   for step in range(180):
    m=messages.get(timeout=35);assert m is not None,'Bridge exited';trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush();assert m['type'] not in {'fatal','rejected','ended'},m
    if m['type']!='request':continue
    r=m['request'];v=m['views'][r['seat']-1];one=v['players'][0];two=v['players'][1];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts];pick='ok'
    snap(m,'request-'+str(step))
    if v['stackItems']:stack_seen=True
    if kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
     if cast and stack_seen:
      if case=='activated-X':
       assert effect_paid==3 and one['manaPool']==[0,0,0,0,0,1] and one['life']==18 and two['life']==19
       assert next(c for c in one['battlefield'] if c['name']=='Banshee')['tapped']
      elif case=='resolving-C':
       assert effect_paid==2 and sum(one['manaPool'])==0 and any(c['name']=='Bearer of Silence' and c['power']==2 and c['toughness']==1 for c in one['battlefield'])
       assert 'Grizzly Bears' in two['graveyard'] and all(c['tapped'] for c in one['battlefield'] if c['name'] in {'Swamp','Wastes','Cultivator Drone'})
      else:
       assert counter and 'Shock' in one['graveyard'] and spec['counter'] in two['graveyard'] and sum(two['manaPool'])==0
       if case=='fixed-tax-forbidden':assert negative_seen and effect_paid==0 and one['manaPool']==[0,0,0,0,0,4] and two['life']==20
       else:assert effect_paid==(3 if case=='resolving-X' else 4) and one['manaPool'][5]==(1 if case=='resolving-X' else 0) and two['life']==18
       if case=='resolving-X':assert scry
      return finish('PASS','Actual production, exact activated/resolving cost payment or native rejection, resource/zone/life assertions and next priority.')
     source=next((c for c in one['battlefield'] if c['name'] in {'Rosheen Meanderer','Cultivator Drone'} and c['id'] not in floated),None)
     if source:pick='card:'+str(source['id']);floated.add(source['id'])
     elif not cast:
      if case=='activated-X':
       assert one['manaPool']==[0,0,0,0,0,4] and 'card:'+str(one['hand'][0]['id']) not in ids
       pick='card:'+str(next(c for c in one['battlefield'] if c['name']=='Banshee')['id'])
      else:pick='card:'+str(one['hand'][0]['id'])
      cast=True
    elif kind=='InputPassPriority' and r['seat']==2 and cast and 'counter' in spec and not counter and v['stackItems']:
     pick='card:'+str(next(c for c in two['hand'] if c['name']==spec['counter'])['id']);counter=True
    elif kind=='ChooseNumber':pick='number-plus' if r['selectedValue']<spec['x'] else 'confirm-number'
    elif kind=='ChooseModes':pick='confirm-modes' if 'confirm-modes' in ids else next(o['id'] for o in opts if o['label'].startswith('Counter target spell'))
    elif kind=='dialog':
     if case=='resolving-twice-X':pick=next(o['id'] for o in opts if o['label'].startswith('Counter target spell'))
     else:raise AssertionError(('Unexpected dialog',r))
    elif 'PayMana' in kind:
     if last_payment is not None:
      assert last_payment!=(one['manaPool'],r.get('remainingManaCost')),('Payment made no resource/cost progress',r)
      last_payment=None
     if r['seat']==2:pick='ok'
     elif case=='activated-X':pick='mana:C';effect_paid+=1
     elif case=='resolving-C':
      if one['manaPool'][5]==2 and not any(c['name']=='Bearer of Silence' for c in one['battlefield']) and not any(s['kind']=='spell' and s['source']['name']=='Bearer of Silence' for s in v['stackItems']):
       land=next(c for c in one['battlefield'] if c['name'] in {'Swamp','Wastes'} and not c['tapped'] and 'card:'+str(c['id']) in ids);pick='card:'+str(land['id'])
      else:pick='mana:C';effect_paid+=1
     elif counter:
      if case=='fixed-tax-forbidden':
       assert one['manaPool']==[0,0,0,0,0,4] and r['remainingManaCost']=='{3}'
       pick='cancel' if negative_seen else 'mana:C';negative_seen=True
      else:assert 'mana:C' in ids,('Restricted resolving X mana unavailable',r);pick='mana:C';effect_paid+=1;last_payment=(one['manaPool'],r.get('remainingManaCost'))
     else:pick='card:'+str(next(c for c in one['battlefield'] if c['name']=='Mountain' and not c['tapped'])['id'])
    elif kind=='SelectStackTarget':pick='ok' if 'ok' in ids else next(i for i in ids if i.startswith('stack:'))
    elif kind=='InputSelectTargets':pick='ok' if 'ok' in ids else 'player:'+str(two['targetId'])
    elif kind=='Scry':scry=True;pick='confirm-scry' if 'confirm-scry' in ids else next(i for i in ids if i.startswith('scry-top:'))
    elif kind=='InputSelectCardsFromList' and r['seat']==2:
     pick='ok' if 'ok' in ids else next(i for i in ids if i.startswith('card:'))
    elif kind=='InputMulligan':pick='keep'
    elif kind not in {'InputPassPriority','InputConfirmMulligan','InputConfirm'}:raise AssertionError(('Unhandled predicate flow',r))
    assert pick in ids,(case,step,kind,pick,r)
    print(case,step,kind,pick,flush=True);proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');proc.stdin.flush()
   raise AssertionError('Step limit')
  except Exception as e:finish('FAIL',repr(e));raise
  finally:
   if proc.poll() is None:proc.kill();proc.wait()
if __name__=='__main__':
 selected=list(sys.argv[1:] or CASES);results=[]
 summary=OUT/'run-summary.json'
 summary.write_text(json.dumps(dict(status='RUNNING',selected=selected,results=results)),encoding='utf8')
 try:
  for case in selected:
   result=run(case);results.append(result);print(json.dumps(result),flush=True)
  assert len(results)==len(selected) and all(r['status']=='PASS' for r in results)
 except BaseException:
  summary.write_text(json.dumps(dict(status='FAIL',selected=selected,results=results),ensure_ascii=False,indent=2)+'\n',encoding='utf8');raise
 summary.write_text(json.dumps(dict(status='PASS',selected=selected,results=results),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
