"""Native validity predicates on spent mana; opening-only shared fixture."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'.tools/conditional-spent-mana-1006/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURE=ROOT/'Tools/ForgeBridge/tests/RestrictedManaFixture.java';SHA=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES={
 'nonhuman-counter':dict(source='Animal Attendant',spell='Goblin Piker',board=['Animal Attendant','Mountain'],color='R',stats=[3,2],counters=1),
 'human-no-counter':dict(source='Animal Attendant',spell='Bloodlust Inciter',board=['Animal Attendant'],color='R',stats=[1,1],counters=0),
 'multicolor-counter':dict(source="Guildmages' Forum",spell='Watchwolf',board=["Guildmages' Forum"]+['Plains']*2+['Forest']*2,color='G',stats=[4,4],counters=1),
 'monocolor-no-counter':dict(source="Guildmages' Forum",spell='Goblin Piker',board=["Guildmages' Forum"]+['Mountain']*2,color='R',stats=[2,1],counters=0),
 'dragon-haste-expiry':dict(source='Carnelian Orb of Dragonkind',spell='Dragon Hatchling',board=['Carnelian Orb of Dragonkind','Mountain'],color='R',stats=[0,1],counters=0,haste=True),
 'nondragon-no-haste':dict(source='Carnelian Orb of Dragonkind',spell='Goblin Piker',board=['Carnelian Orb of Dragonkind','Mountain'],color='R',stats=[2,1],counters=0,haste=False),
}
def run(case):
 spec=CASES[case];folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True)
 meta=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driverSha256=SHA(Path(__file__)),fixtures=[dict(path=FIXTURE.relative_to(ROOT).as_posix(),sha256=SHA(FIXTURE))])
 (folder/'driver.py').write_bytes(Path(__file__).read_bytes());(folder/FIXTURE.name).write_bytes(FIXTURE.read_bytes())
 def finish(status,message):
  r=dict(meta,status=status,result=status+' '+case+': '+message)
  for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(r,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return r
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
  start=dict(session=case,fixture=case,hand=[spec['spell']],lands=spec['board'],opponentHand=[],opponentBoard=[],seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK]);(folder/'start.json').write_text(json.dumps(start,indent=2)+'\n',encoding='utf8');p=Path(temp)/'start.json';p.write_text(json.dumps(start),encoding='utf8')
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.RestrictedManaFixture',str(RT/CFG['assets']),str(p)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
  messages=queue.Queue()
  def reader():
   try:
    for line in proc.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,label):
   for seat,v in enumerate(m['views'],1):(folder/(label+'-seat'+str(seat)+'.json')).write_text(json.dumps(dict(v,request=m['request'],awaitingInput=True),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  activated=False;cast=False;stack_seen=False;settled=False;source_id=None;spell_id=None;last=None;paid=False
  try:
   for step in range(300):
    m=messages.get(timeout=35);assert m is not None,'Native exited';trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush();assert m['type'] not in {'fatal','rejected','ended'},m
    if m['type']!='request':continue
    q=m['request'];v=m['views'][0];one,two=v['players'];kind=q['kind'];opts=q['options'];ids=[o['id'] for o in opts];pick='ok';snap(m,'request-'+str(step))
    if any(s['kind']=='spell' and s['source']['id']==spell_id for s in v['stackItems']):stack_seen=True
    if last is not None:assert last!=(kind,one['manaPool'],q.get('remainingManaCost')),('No payment progress',q);last=None
    if kind=='InputPassPriority':
     if q['seat']==1 and not v['stackItems']:
      if not activated and v['turn']==1 and v['phase']=='MAIN1':
       source_id=next(c for c in one['battlefield'] if c['name']==spec['source'])['id'];spell_id=next(c for c in one['hand'] if c['name']==spec['spell'])['id'];pick='card:'+str(source_id);activated=True
      elif activated and not cast:
       expected=[0]*6;expected['WUBRGC'.index(spec['color'])]=1;assert one['manaPool']==expected,('Source production',one['manaPool']);assert next(c for c in one['battlefield'] if c['id']==source_id)['tapped'];snap(m,'produced');pick='card:'+str(spell_id);cast=True
      elif cast and stack_seen and not settled:
       assert paid and one['life']==20 and two['life']==20 and sum(one['manaPool'])==0
       c=next(c for c in one['battlefield'] if c['id']==spell_id);n=sum(n['count'] for n in c['counters'] if n['kind']=='P1P1')
       assert [c['power'],c['toughness']]==spec['stats'] and n==spec['counters'],('ETB stats/counters',c)
       assert ('HASTE' in c['keywords'])==spec.get('haste',False),('Keyword predicate',c)
       assert len(one['hand'])==0 and not one['graveyard'] and not two['battlefield'];snap(m,'resolved');settled=True
       if case!='dragon-haste-expiry':return finish('PASS','Actual source activation/color/native spending, original creature ETB predicate exact counters/stats/keyword, life20/pool0 and main priority restored.')
      elif settled and v['turn']==2:
       c=next(c for c in one['battlefield'] if c['id']==spell_id);assert 'HASTE' not in c['keywords'] and [c['power'],c['toughness']]==spec['stats'] and sum(one['manaPool'])==0;snap(m,'expired-next-turn');return finish('PASS','Actual Orb R spends on Dragon grants HASTE; native cleanup removes it before next opponent turn priority, original ID/stats/resources retained.')
    elif kind=='dialog':
     # Forum has a separate plain-C ability; choose the actual conditional ability.
     candidates=[o for o in opts if o['label']==dict(W='WHITE',U='BLUE',B='BLACK',R='RED',G='GREEN',C='COLORLESS')[spec['color']]] if q['message']=='ManaColor' else [o for o in opts if 'Add one mana of any color' in o['label']]
     assert len(candidates)==1,('Unknown ability dialog',q);pick=candidates[0]['id']
    elif 'PayMana' in kind:
     if cast and 'mana:'+spec['color'] in ids:
      pick='mana:'+spec['color'];last=(kind,one['manaPool'],q.get('remainingManaCost'));paid=True
     else:pick='ok'
    elif kind in {'InputConfirm','ChooseColor'}:
     colors={'W':'white','U':'blue','B':'black','R':'red','G':'green','C':'colorless'}
     color=next((o for o in opts if o['label'].lower()==colors[spec['color']]),None)
     assert color is not None,('Unknown color request',q);pick=color['id']
    elif kind=='InputMulligan':pick='keep'
    elif kind=='OrderAbilities':pick='confirm-order'
    elif kind in {'InputAttack','InputBlock','InputConfirmMulligan'}:pick='ok'
    elif kind.startswith('InputSelect') and q['message'].startswith('Cleanup Phase'):pick='ok' if 'ok' in ids else next(o['id'] for o in opts if o['id'].startswith('card:') and not o.get('selected'))
    else:raise AssertionError(('Unhandled conditional spent flow',q))
    assert pick in ids,(case,step,kind,pick,q);print(case,step,v['turn'],v['phase'],kind,pick,flush=True);proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=q['seat'],requestId=q['requestId'],option=pick))+'\n');proc.stdin.flush()
   raise AssertionError('Step limit')
  except Exception as e:finish('FAIL',repr(e));raise
  finally:
   if proc.poll() is None:proc.kill();proc.wait()
if __name__=='__main__':
 selected=list(sys.argv[1:] or CASES);assert all(k in CASES for k in selected);results=[];p=OUT/'run-summary.json';p.write_text(json.dumps(dict(status='RUNNING',selected=selected,results=[])),encoding='utf8')
 try:
  subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(FIXTURE)],check=True)
  for case in selected:r=run(case);results.append(r);print(json.dumps(r),flush=True)
 except BaseException:p.write_text(json.dumps(dict(status='FAIL',selected=selected,results=results)),encoding='utf8');raise
 assert len(results)==len(selected) and all(r['status']=='PASS' for r in results);p.write_text(json.dumps(dict(status='PASS',selected=selected,results=results),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
