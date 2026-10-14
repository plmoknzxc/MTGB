"""Actual mutual next-turn control and nested Word of Command while controlled."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/nested-control-1006/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURE=ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java';SHA=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(FIXTURE)],check=True)
CASES=['mutual-next-turn','nested-word'];DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
def run(case):
 folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);meta={'case':case,'folder':folder.relative_to(ROOT).as_posix(),'runtimeHash':CFG['runtimeHash'],'driverSha256':SHA(Path(__file__)),'fixtures':[{'path':FIXTURE.relative_to(ROOT).as_posix(),'sha256':SHA(FIXTURE)}]}
 def finish(status,msg):
  d={**meta,'status':status,'result':status+' '+case+': '+msg}
  for path in [folder/'result.json',OUT/(case+'-result.json')]:path.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return d
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'trace.jsonl').open('w',encoding='utf8') as trace,(folder/'engine.log').open('w',encoding='utf8') as log:
  audit=Path(temp)/'audit';audit.mkdir();start=Path(temp)/'start.json';word=case=='nested-word'
  start.write_text(json.dumps(dict(session=case,actor=1,own={'Hand':['Worst Fears','Opt']},other={'Hand':['Word of Command' if word else 'Worst Fears'],'Battlefield':['Swamp']*8},auditDir=str(audit),seed=4,firstPlayerSeat=1,startingLife=30,decks=[DECK,DECK])),encoding='utf8')
  proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.RoyalStateFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');messages=queue.Queue()
  def reader():
   try:
    for line in proc.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def respond(r,pick,seat=None):proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'] if seat is None else seat,requestId=r['requestId'],option=pick))+'\n');proc.stdin.flush()
  def snap(m,name):
   for seat,v in enumerate(m['views'],1):
    d=dict(v);d.update(request=m['request'],awaitingInput=m['request']['seat']==seat);(folder/(name+('' if seat==1 else '-other')+'.json')).write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf8')
  stage=0;pending=False;target=2;opt_id=None;nested=False;scry=False;wrong=set();deadline=time.monotonic()+300
  try:
   for step in range(650):
    assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=40);assert m is not None,'Native exit';trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
    if m['type']!='request':continue
    r=m['request'];kind=r['kind'];options=r['options'];ids=[o['id'] for o in options];view=m['views'][0];players=view['players'];pick=None;control=[p.get('controllingSeat',0) for p in players]
    if kind=='InputPassPriority' and not view['stackItems']:
     if pending:
      assert any(c['name']==('Worst Fears' if stage==0 or not word and stage==1 else 'Word of Command' if stage==1 else 'Opt') for c in players[(0 if stage==0 or stage==2 else 1)]['exileCards' if stage==0 or stage==1 and not word else 'graveyardCards']),('Cast not resolved',stage)
      if stage==1 and word:assert nested and scry and any(c['id']==opt_id for c in players[0]['graveyardCards']),('Nested Opt not resolved',nested,scry)
      stage+=1;pending=False;snap(m,'resolved-'+str(stage))
     actual=r['playerSeat'];turn=view['turn']
     if stage==0 and actual==1 and view['phase']=='MAIN1':
      assert not m['views'][0]['players'][1]['hand'] and not m['views'][1]['players'][0]['hand'],'Initial unseen hand exposed';target=2;name='Worst Fears';pending=True;opt_id=next(c['id'] for c in players[0]['hand'] if c['name']=='Opt')
     elif stage==1 and actual==2 and turn==2 and view['phase']=='MAIN1':
      assert control==[0,1] and r['seat']==1,('Whole turn operator',control,r['seat']);target=1;name='Word of Command' if word else 'Worst Fears';pending=True;snap(m,'controlled-slave')
     elif not word and stage==2 and actual==1 and turn==3 and view['phase']=='MAIN1':
      assert control==[2,0] and r['seat']==2,('Mutual next controller',control,r['seat']);name='Opt';pending=True;snap(m,'mutual-master')
     elif (word and stage==2 and turn>=3 or not word and stage==3 and turn>=4):
      assert control==[0,0] and r['seat']==actual,('Expiry ordinary controller',control,r)
      assert scry and any(c['name']=='Opt' for c in players[0]['graveyardCards']);snap(m,'released')
      return finish('PASS','actual Worst Fears target/payment/exile, controlled opponent casts '+('Word of Command with forced owner Opt/mana/scry and nested restore' if word else 'Worst Fears then mutual next-turn controlled Opt/mana/scry')+', exact original holder zones and operating seats, wrong-seat rejection, continuation and expiry; original/private views captured; broader death/network unfinished-input still open')
     if pending:
      actor=1 if stage in [0,2] else 2;cards=m['views'][r['seat']-1]['players'][actor-1]['hand'];c=next(c for c in cards if c['name']==name);pick='card:'+str(c['id'])
     else:pick='ok'
    elif 'PayMana' in kind:
     if word and stage==1 and r['playerSeat']==1:
      nested=True;assert r['seat']==1 and control==[2,1],('Nested forced casting controller',control,r['seat']);snap(m,'nested-payment')
     pick='ok' if 'ok' in ids and not next(o for o in options if o['id']=='ok').get('disabled') else next(o['id'] for o in options if o['id'].startswith('card:'))
    elif 'SelectTargets' in kind:
     pick='ok' if 'ok' in ids and not next(o for o in options if o['id']=='ok').get('disabled') else 'player:'+str(players[target-1]['targetId']);snap(m,'target-'+str(stage)+'-'+str(step))
    elif kind=='Scry':
     assert stage in [1,2] and r['playerSeat']==1 and r['seat']==(1 if word else 2),('Scry operator',stage,r['seat'],r['playerSeat']);scry=True;snap(m,'nested-scry' if word else 'mutual-scry');pick=next((i for i in ids if i.startswith('scry-top:')),'confirm-scry')
    elif word and stage==1 and kind.startswith(('InputSelectCards','InputSelectEntities','SelectCards')):
     # Word's native revealed hand may be a modal choice instead of ordinary hand selection.
     if 'ok' in ids and not next(o for o in options if o['id']=='ok').get('disabled'):pick='ok'
     else:
      option=next(o for o in options if o.get('card',{}).get('name')=='Opt');opt_id=option['card']['id'];pick=option['id']
     snap(m,'word-choice-'+str(step))
    elif kind=='RevealCards':pick='confirm-cards';snap(m,'word-reveal-'+str(step))
    elif kind=='OrderAbilities':pick='confirm-order'
    elif 'ok' in ids:pick='ok'
    else:raise AssertionError(('Unhandled',kind,r['message'],ids))
    if kind=='InputPassPriority' and r['playerSeat']!=r['seat'] and (stage,r['playerSeat']) not in wrong:
     wrong.add((stage,r['playerSeat']));respond(r,ids[0],3-r['seat']);rejected=messages.get(timeout=10);trace.write(json.dumps(rejected)+'\n');trace.flush();assert rejected['type']=='rejected' and rejected['error']=='wrong-seat',rejected
    assert pick in ids,(kind,pick,ids);print(case,step,'stage',stage,kind,r['seat'],r['playerSeat'],control,pick,r['message'][:80],flush=True);respond(r,pick)
   raise AssertionError('Step limit')
  except Exception:finish('FAIL',traceback.format_exc());raise
  finally:
   if proc.poll() is None:proc.kill();proc.wait()
if __name__=='__main__':
 results=[]
 for case in sys.argv[1:] or CASES:
  try:result=run(case);print(result['result'],flush=True);results.append(result)
  except Exception as error:print('FAIL '+case+': '+repr(error),flush=True);results.append(json.loads((OUT/(case+'-result.json')).read_text()))
 (OUT/'results.json').write_text(json.dumps({'status':'PASS' if all(r['status']=='PASS' for r in results) else 'FAIL','results':results},ensure_ascii=False,indent=2)+'\n',encoding='utf8');sys.exit(any(r['status']!='PASS' for r in results))
