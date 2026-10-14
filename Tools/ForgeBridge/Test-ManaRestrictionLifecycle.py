"""Native restriction consumers and exact step/turn mana expiry. Setup only is injected."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'.tools/mana-lifecycle-1006/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURE=ROOT/'Tools/ForgeBridge/tests/RestrictedManaFixture.java';SHA=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(FIXTURE)],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES={
 'cost-X':dict(board=['Rosheen Meanderer'],activate=['Rosheen Meanderer'],hand=['Walking Ballista','Myr Retriever'],spell='Walking Ballista',forbidden=['Myr Retriever'],pool=[0,0,0,0,0,4],pay='C'),
 'activated-only':dict(board=['James, Wandering Dad','Mind Stone'],activate=['James, Wandering Dad'],hand=['Myr Retriever'],spell=None,forbidden=['Myr Retriever'],pool=[0,0,0,0,0,2],pay='C'),
 'graveyard-source':dict(board=['Vhal, Candlekeep Researcher','Island'],activate=['Vhal, Candlekeep Researcher','Island'],hand=['Myr Retriever'],graveyard=['Deep Analysis'],spell='Deep Analysis',forbidden=['Myr Retriever'],pool=[0,1,0,0,0,3],pay='C'),
 'persistent-expiry':dict(board=['Birgi, God of Storytelling','Mountain'],activate=['Mountain'],hand=['Bloodlust Inciter'],spell='Bloodlust Inciter',forbidden=[],pool=[0,0,0,1,0,0],pay='R'),
 'combat-expiry':dict(board=['Avatar Roku, Firebender'],activate=[],hand=[],spell=None,forbidden=[],pool=[0,0,0,0,0,0],pay='R'),
 'nonspell-upkeep':dict(board=['Thran Turbine','Mind Stone'],activate=[],hand=['Warping Wail'],spell=None,forbidden=['Warping Wail'],pool=[0,0,0,0,0,2],pay='C'),
 'cumulative-upkeep':dict(board=['Mystic Remora','Adarkar Unicorn'],activate=[],hand=['Opt'],spell=None,forbidden=['Opt'],pool=[0,1,0,0,0,0],pay='C'),
}
def run(case):
 spec=CASES[case];folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True)
 meta=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driverSha256=SHA(Path(__file__)),fixtures=[dict(path=FIXTURE.relative_to(ROOT).as_posix(),sha256=SHA(FIXTURE))])
 def finish(status,message):
  result=dict(meta,status=status,result=status+' '+case+': '+message)
  for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return result
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
  start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,fixture=case,hand=spec['hand'],lands=spec['board'],graveyard=spec.get('graveyard',[]),seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK])),encoding='utf8')
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.RestrictedManaFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
  messages=queue.Queue()
  def reader():
   try:
    for line in proc.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,label):
   for seat,v in enumerate(m['views'],1):(folder/(label+'-seat'+str(seat)+'.json')).write_text(json.dumps(dict(v,request=m['request'],awaitingInput=True),ensure_ascii=False,indent=2),encoding='utf8')
  floated=set();cast=False;stack_seen=False;resolved=False;attack_selected=False;mana_seen=False;phases=set();hand_before=0
  try:
   for step in range(320):
    m=messages.get(timeout=35);assert m is not None,'Bridge exited';trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush();assert m['type'] not in {'fatal','rejected','ended'},m
    if m['type']!='request':continue
    r=m['request'];v=m['views'][0];one=v['players'][0];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts];pick='ok'
    if case=='cumulative-upkeep' and kind=='InputPassPriority' and r['seat']==1 and v['phase']=='UPKEEP' and not v['stackItems'] and one['manaPool'][1]==1:
     remora=next(c for c in one['battlefield'] if c['name']=='Mystic Remora');assert any(c['kind']=='AGE' and c['count']==1 for c in remora['counters'])
     assert sum(one['manaPool'])==1 and next(c for c in one['battlefield'] if c['name']=='Adarkar Unicorn')['tapped']
     assert 'card:'+str(one['hand'][0]['id']) not in ids;snap(m,'cumulative-paid')
     return finish('PASS','Real cumulative upkeep trigger creates one AGE; exact native CU mana activation pays one C, Remora retained, one U left and Opt forbidden; source tapped and upkeep priority restores.')
    if case=='nonspell-upkeep' and kind=='InputPassPriority' and r['seat']==1 and v['phase']=='UPKEEP' and not v['stackItems']:
     if not cast and one['manaPool']==spec['pool']:
      assert 'card:'+str(one['hand'][0]['id']) not in ids;snap(m,'nonspell-legality');hand_before=len(one['hand']);pick='card:'+str(next(c for c in one['battlefield'] if c['name']=='Mind Stone')['id']);cast=True
     elif cast:
      assert 'Mind Stone' in one['graveyard'] and len(one['hand'])==hand_before+1 and one['manaPool']==[0,0,0,0,0,1];snap(m,'nonspell-paid')
      return finish('PASS','Actual optional Thran Turbine upkeep produces two C; affordable instant Warping Wail forbidden; Mind Stone activation consumes one C/sacrifices/draws, one C remains and native upkeep priority continues.')
     else:pick='ok'
     assert pick in ids;proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');proc.stdin.flush();continue
    if any(s['source']['name']==(spec['spell'] or 'Mind Stone') for s in v['stackItems']):stack_seen=True;snap(m,'stack-'+str(step))
    if case=='combat-expiry' and attack_selected and kind=='InputPassPriority':
     if one['manaPool'][3]==6:
      assert sum(one['manaPool'])==6;mana_seen=True;phases.add(v['phase']);snap(m,'retained-'+v['phase'])
     elif mana_seen and v['phase']=='MAIN2':
      assert sum(one['manaPool'])==0 and v['players'][1]['life']==14 and len(phases)>=2;snap(m,'combat-expired')
      return finish('PASS','Native Roku attack trigger makes six R, retained across multiple combat steps; six combat damage, exact pool empties in second main and native priority continues.')
    if case=='persistent-expiry' and resolved and kind=='InputPassPriority':
     if v['turn']==1:
      assert one['manaPool'][3]==1 and sum(one['manaPool'])==1;phases.add(v['phase']);snap(m,'retained-'+v['phase'])
     elif v['turn']==2:
      assert len(phases)>=4 and sum(one['manaPool'])==0;snap(m,'turn-expired')
      return finish('PASS','Actual creature cast/payment triggers Birgi R; native mana stays through main/combat/end steps, expires at cleanup before next turn; source/card/pool exact and priority continues.')
    if kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1' and v['turn']==1 and not v['stackItems'] and not resolved and case not in {'combat-expiry','nonspell-upkeep','cumulative-upkeep'}:
     source=next((c for c in one['battlefield'] if c['name'] in spec['activate'] and c['id'] not in floated),None)
     if source:pick='card:'+str(source['id']);floated.add(source['id'])
     elif not cast:
      assert one['manaPool']==spec['pool'],('Production pool',one['manaPool']);snap(m,'floated-legality');hand_before=len(one['hand'])
      for name in spec['forbidden']:
       assert 'card:'+str(next(c for c in one['hand'] if c['name']==name)['id']) not in ids,('Forbidden hand spell',name,r)
      if case=='activated-only':pick='card:'+str(next(c for c in one['battlefield'] if c['name']=='Mind Stone')['id'])
      else:
       cards=one['graveyardCards'] if case=='graveyard-source' else one['hand'];card=next(c for c in cards if c['name']==spec['spell']);pick='card:'+str(card['id'])
      assert pick in ids;cast=True
     elif stack_seen:
      assert all(c['tapped'] for c in one['battlefield'] if c['id'] in floated)
      if case=='cost-X':
       c=next(c for c in one['battlefield'] if c['name']=='Walking Ballista');assert c['power']==2 and c['toughness']==2 and any(n['kind']=='P1P1' and n['count']==2 for n in c['counters']) and sum(one['manaPool'])==0
      elif case=='activated-only':
       assert 'Mind Stone' in one['graveyard'] and len(one['hand'])==hand_before+1 and one['manaPool']==[0,0,0,0,0,1]
      elif case=='graveyard-source':
       assert one['life']==17 and len(one['hand'])==hand_before+2 and 'Deep Analysis' not in one['graveyard'] and any(c['name']=='Deep Analysis' for c in one['exileCards']) and one['manaPool']==[0,0,0,0,0,2]
      else:
       assert one['life']==20 and one['manaPool']==[0,0,0,1,0,0] and any(c['name']=='Bloodlust Inciter' and c['power']==1 and c['toughness']==1 for c in one['battlefield'])
      resolved=True;snap(m,'resolved')
      if case!='persistent-expiry':return finish('PASS','Actual restricted production, native hand prohibition and exact legal activation/X/graveyard payment, resolved resource and zone assertions, next priority.')
    elif kind=='ChooseNumber':
     snap(m,'number-'+str(step));pick='number-plus' if r['selectedValue']<2 else 'confirm-number'
    elif kind=='dialog':
     snap(m,'choice-'+str(step))
     if case=='cost-X':pick=next(o['id'] for o in opts if o['label']=='2')
     elif case in {'activated-only','nonspell-upkeep'}:pick=next(o['id'] for o in opts if 'Draw a card' in o['label'])
     elif case=='graveyard-source':pick=next(o['id'] for o in opts if 'Flashback' in o['label'])
     elif case=='cumulative-upkeep':pick=next(o['id'] for o in opts if '{C}{U}' in o['label'].replace(' ',''))
     else:raise AssertionError(('Unexpected dialog',r))
    elif 'PayMana' in kind:
     snap(m,'payment-'+str(step))
     if case=='cumulative-upkeep' and sum(one['manaPool'])==0:pick='card:'+str(next(c for c in one['battlefield'] if c['name']=='Adarkar Unicorn')['id'])
     else:pick='mana:'+('U' if case=='graveyard-source' and one['manaPool'][1] else spec['pay'])
    elif kind=='InputSelectTargets':pick='ok' if 'ok' in ids else 'player:'+str(v['players'][0]['targetId'])
    elif kind=='InputAttack':
     if case=='combat-expiry' and r['seat']==1 and not attack_selected:pick=next(o['id'] for o in opts if o['id'].startswith('attack:'));attack_selected=True
    elif kind.startswith('InputSelect') and r['message'].startswith('Cleanup Phase'):pick='ok' if 'ok' in ids else next(o['id'] for o in opts if o['id'].startswith('card:') and not o.get('selected'))
    elif kind=='InputMulligan':pick='keep'
    elif kind not in {'InputPassPriority','InputConfirmMulligan','InputConfirm','InputBlock'}:raise AssertionError(('Unhandled restricted flow',r))
    assert pick in ids,(case,kind,pick,r)
    print(case,step,v['phase'],kind,pick,flush=True);proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');proc.stdin.flush()
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
