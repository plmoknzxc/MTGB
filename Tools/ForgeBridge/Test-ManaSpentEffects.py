"""Actual spent-mana keyword/ETB/exile effects. Fixture only arranges opening cards."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'.tools/mana-spent-effects-1006/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURE=ROOT/'Tools/ForgeBridge/tests/RestrictedManaFixture.java';SHA=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES={
 'riot-counter':dict(board=['Mountain']*2+['Forest']*2,hand=['Domri, Chaos Bringer','Bloodlust Inciter'],source='Domri, Chaos Bringer',spell='Bloodlust Inciter'),
 'riot-haste':dict(board=['Mountain']*2+['Forest']*2,hand=['Domri, Chaos Bringer','Bloodlust Inciter'],source='Domri, Chaos Bringer',spell='Bloodlust Inciter'),
 'haste-no-expiry':dict(board=['Hall of the Bandit Lord','Mountain'],hand=['Goblin Piker'],source='Hall of the Bandit Lord',spell='Goblin Piker'),
 'jade-next-turn':dict(board=['Jade Orb of Dragonkind','Mountain'],hand=['Dragon Hatchling'],source='Jade Orb of Dragonkind',spell='Dragon Hatchling'),
 'foundry-exile':dict(board=["Forger's Foundry"],hand=['Opt'],source="Forger's Foundry",spell='Opt'),
 'foundry-graveyard':dict(board=["Forger's Foundry"],hand=['Opt'],source="Forger's Foundry",spell='Opt'),
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
  start=dict(session=case,fixture=case,hand=spec['hand'],lands=spec['board'],opponentHand=[],opponentBoard=[],seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK])
  (folder/'start.json').write_text(json.dumps(start,ensure_ascii=False,indent=2)+'\n',encoding='utf8');p=Path(temp)/'start.json';p.write_text(json.dumps(start),encoding='utf8')
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.RestrictedManaFixture',str(RT/CFG['assets']),str(p)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
  messages=queue.Queue()
  def reader():
   try:
    for line in proc.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,label):
   for seat,v in enumerate(m['views'],1):(folder/(label+'-seat'+str(seat)+'.json')).write_text(json.dumps(dict(v,request=m['request'],awaitingInput=True),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  activated=False;domri_cast=False;cast=False;stack_seen=False;settled=False;riot=False;scry=False;exile_choice=False;expiry_retained=False;before_hand=None;creature_id=None;last=None
  try:
   for step in range(450):
    m=messages.get(timeout=35);assert m is not None,'Native exited';trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush();assert m['type'] not in {'fatal','rejected','ended'},m
    if m['type']!='request':continue
    q=m['request'];v=m['views'][0];one,two=v['players'];kind=q['kind'];opts=q['options'];ids=[o['id'] for o in opts];pick='ok';snap(m,'request-'+str(step))
    if any(s['kind']=='spell' and s['source']['name']==spec['spell'] for s in v['stackItems']):stack_seen=True
    if last is not None:
     assert last!=(kind,one['manaPool'],q.get('remainingManaCost')),('No payment progress',q);last=None
    if kind=='InputPassPriority':
     if q['seat']==1 and not v['stackItems']:
      if not activated and v['turn']==1 and v['phase']=='MAIN1':
       if case.startswith('riot') and not any(c['name']==spec['source'] for c in one['battlefield']):
        assert not domri_cast;pick='card:'+str(next(c for c in one['hand'] if c['name']==spec['source'])['id']);domri_cast=True
       else:
        pick='card:'+str(next(c for c in one['battlefield'] if c['name']==spec['source'])['id']);activated=True
      elif activated and not cast:
       pool=[0,0,0,1,0,0] if case.startswith('riot') else [0,0,0,0,0,1] if case=='haste-no-expiry' else [0,0,0,0,1,0] if case=='jade-next-turn' else [0,1,0,0,0,0]
       assert one['manaPool']==pool,('Source production',one['manaPool']);before_hand=len(one['hand']);snap(m,'produced')
       pick='card:'+str(next(c for c in one['hand'] if c['name']==spec['spell'])['id']);cast=True
      elif cast and stack_seen and not settled:
       assert sum(one['manaPool'])==0 and two['life']==20 and one['life']==(17 if case=='haste-no-expiry' else 20)
       assert next(c for c in one['battlefield'] if c['name']==spec['source'])['tapped']==(not case.startswith('riot'))
       if case.startswith('foundry'):
        assert scry and exile_choice and len(one['hand'])==before_hand and 'Opt' not in [c['name'] for c in one['hand']]
        assert one['graveyard'].count('Opt')==(case=='foundry-graveyard') and sum(c['name']=='Opt' for c in one['exileCards'])==(case=='foundry-exile')
        snap(m,'resolved');return finish('PASS','Actual foundry mana pays Opt, scry/draw resolves, native optional replacement selects exact exile/graveyard, resources and main priority recover.')
       c=next(c for c in one['battlefield'] if c['name']==spec['spell']);creature_id=c['id'];kw=c['keywords'];n=sum(n['count'] for n in c['counters'] if n['kind']=='P1P1')
       if case.startswith('riot'):
        assert riot and next(c for c in one['battlefield'] if c['name']==spec['source'])['loyalty']==6
        assert 'RIOT' in kw and (n,c['power'],c['toughness'])==((1,2,2) if case=='riot-counter' else (0,1,1))
        assert ('HASTE' in kw)==(case=='riot-haste');snap(m,'resolved')
        return finish('PASS','Actual Domri cast/+1 produces R and grants Riot to paid creature; native entry choice exact counter/haste, loyalty6, pool0 and main priority recover.')
       if case=='haste-no-expiry':assert 'HASTE' in kw and (c['power'],c['toughness'],n)==(2,1,0)
       else:assert 'HEXPROOF' in kw and (c['power'],c['toughness'],n)==(1,2,1)
       settled=True;snap(m,'resolved')
      elif settled:
       c=next(c for c in one['battlefield'] if c['id']==creature_id)
       if case=='haste-no-expiry' and v['turn']==2:
        assert 'HASTE' in c['keywords'] and sum(one['manaPool'])==0 and one['life']==17;snap(m,'retained-next-turn')
        return finish('PASS','Actual Hall activation/life20->17/C pays Piker, haste granted with no duration and remains after cleanup/opponent next turn; pool0 and priority recover.')
       if case=='jade-next-turn':
        if v['turn']==2:assert 'HEXPROOF' in c['keywords'];expiry_retained=True;snap(m,'retained-opponent-turn')
        if v['turn']==3:
         assert expiry_retained and 'HEXPROOF' not in c['keywords'] and any(n['kind']=='P1P1' and n['count']==1 for n in c['counters']) and (c['power'],c['toughness'])==(1,2)
         assert one['life']==20 and sum(one['manaPool'])==0;snap(m,'expired-own-turn')
         return finish('PASS','Actual jade G spends on Dragon; native spellcast trigger/replacement adds one counter/hexproof, retained opponent turn then expires before own turn priority; counter remains and resources exact.')
    elif kind=='dialog':
     label=[o['label'] for o in opts];snap(m,'dialog-'+str(step))
     if any(x=='RED' for x in label):pick=next(o['id'] for o in opts if o['label']=='RED')
     elif case.startswith('riot') and any('Add {R}' in x for x in label):pick=next(o['id'] for o in opts if 'Add {R}' in o['label'])
     else:raise AssertionError(('Unknown dialog',q))
    elif 'PayMana' in kind:
     if case.startswith('riot') and not activated:pick='ok'
     elif any(i.startswith('mana:') for i in ids):
      color='R' if case.startswith('riot') else 'C' if case=='haste-no-expiry' else 'G' if case=='jade-next-turn' else 'U';pick='mana:'+color
      if pick not in ids:pick='ok'
      else:last=(kind,one['manaPool'],q.get('remainingManaCost'))
     else:pick='ok'
    elif kind=='InputConfirm':
     if q['message'].endswith('Select Mana to Produce'):
      pick=next(o['id'] for o in opts if o['label'].lower()=='red')
     elif case.startswith('riot'):
      assert 'riot' in q['message'].lower() or '+1/+1' in q['message'];riot=True;pick='ok' if case=='riot-counter' else 'cancel';snap(m,'riot-entry-choice')
     elif case.startswith('foundry'):
      assert 'exile' in q['message'].lower();exile_choice=True;pick='ok' if case=='foundry-exile' else 'cancel';snap(m,'foundry-exile-choice')
    elif kind=='Scry':scry=True;pick='confirm-scry' if 'confirm-scry' in ids else next(i for i in ids if i.startswith('scry-top:'));snap(m,'scry')
    elif kind=='InputMulligan':pick='keep'
    elif kind=='OrderAbilities':pick='confirm-order'
    elif kind in {'InputAttack','InputBlock','InputConfirmMulligan'}:pick='ok'
    elif kind.startswith('InputSelect') and q['message'].startswith('Cleanup Phase'):pick='ok' if 'ok' in ids else next(o['id'] for o in opts if o['id'].startswith('card:') and not o.get('selected'))
    else:raise AssertionError(('Unhandled spent flow',q))
    assert pick in ids,(case,step,kind,pick,q)
    print(case,step,v['turn'],v['phase'],kind,pick,flush=True);proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=q['seat'],requestId=q['requestId'],option=pick))+'\n');proc.stdin.flush()
   raise AssertionError('Step limit')
  except Exception as e:finish('FAIL',repr(e));raise
  finally:
   if proc.poll() is None:proc.kill();proc.wait()

if __name__=='__main__':
 selected=list(sys.argv[1:] or CASES);assert all(k in CASES for k in selected);results=[];p=OUT/'run-summary.json'
 p.write_text(json.dumps(dict(status='RUNNING',selected=selected,results=[])),encoding='utf8')
 try:
  subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(FIXTURE)],check=True)
  for case in selected:r=run(case);results.append(r);print(json.dumps(r),flush=True)
 except BaseException:p.write_text(json.dumps(dict(status='FAIL',selected=selected,results=results)),encoding='utf8');raise
 assert len(results)==len(selected) and all(r['status']=='PASS' for r in results)
 p.write_text(json.dumps(dict(status='PASS',selected=selected,results=results),ensure_ascii=False,indent=2)+'\n',encoding='utf8')
