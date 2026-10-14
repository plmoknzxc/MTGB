"""Actual monarch and initiative acquisition, upkeep/draw, source death and combat transfer."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';cfg=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/monarch-old-endstep-1006/native';OUT.mkdir(parents=True,exist_ok=True);classes=OUT/'classes';classes.mkdir(exist_ok=True)
fixture=ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java';subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),str(fixture)],check=True)
CASES=['monarch-old-endstep'];deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]};sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def run(case):
 initiative=case.startswith('initiative');leave=case.endswith('source-left');combat=case.endswith('combat');source_name='White Plume Adventurer' if initiative else 'Palace Sentinels';own={'Hand':[source_name]+(['Murder'] if leave else [])};other={'Battlefield':['Throne of the High City']+['Plains']*6};folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);meta={'case':case,'folder':folder.relative_to(ROOT).as_posix(),'runtimeHash':cfg['runtimeHash'],'driverSha256':sha(Path(__file__)),'fixtures':[{'path':fixture.relative_to(ROOT).as_posix(),'sha256':sha(fixture)}]}
 def finish(status,message):
  d={**meta,'status':status,'result':status+' '+case+': '+message}
  for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return d
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'trace.jsonl').open('w',encoding='utf8') as trace,(folder/'engine.log').open('w',encoding='utf8') as log:
  audit=Path(temp)/'audit';audit.mkdir();start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,actor=1,own=own,other=other,auditDir=str(audit),seed=4,firstPlayerSeat=1,startingLife=30,decks=[deck,deck])),encoding='utf8');p=subprocess.Popen([str(RT/cfg['java']),'-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.RoyalStateFixture',str(RT/cfg['assets']),str(start)],cwd=RT/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');messages=queue.Queue()
  def reader():
   try:
    for line in p.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,name):
   for seat,v in enumerate(m['views'],1):
    d=dict(v);d.update(request=m['request'],awaitingInput=True);(folder/(name+('' if seat==1 else '-other')+'.json')).write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf8')
  throne_used=False;old_draw_id=None;old_crown_id=None;stage=0;source=None;acquired=False;killed=False;attacked=False;transferred=False;draw_hand=None;draw_lib=None;deadline=time.monotonic()+220
  def observed(r):
   path=audit/(str(r['requestId'])+'.json');until=time.monotonic()+3
   while time.monotonic()<until:
    try:d=json.loads(path.read_text('utf8'));break
    except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
   else:raise AssertionError('Missing observer')
   (folder/('audit-'+str(r['requestId'])+'.json')).write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf8');return d
  try:
   for step in range(450):
    assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=40);assert m is not None,'Native exited';trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
    if m['type']!='request':continue
    r=m['request'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];v=m['views'][0];pl,op=v['players'];stack=v['stackItems'];snap(m,'step-'+str(step));pick=None;n=observed(r) if stage>0 or kind=='InputPassPriority' and v['phase']=='MAIN1' else {'initiative':0,'monarch':0};owner=n['initiative' if initiative else 'monarch'];helper='The Initiative' if initiative else 'The Monarch'
    if owner:
     markers=[(i+1,c) for i,p2 in enumerate(v['players']) for c in p2['effectMarkers'] if c['name']==helper];assert len(markers)==1 and markers[0][0]==owner and markers[0][1]['helperArtName']==helper
     for view in m['views']:assert [p2['effectMarkers'] for p2 in view['players']]==[p2['effectMarkers'] for p2 in v['players']]
    if kind=='InputPassPriority':
     if stage==0 and r['seat']==1 and v['phase']=='MAIN1':source=next(c['id'] for c in pl['hand'] if c['name']==source_name);pick='card:'+str(source);stage=1;snap(m,'before')
     elif stage>=1 and owner==1 and not stack and not acquired:
      assert any(c['id']==source for c in pl['battlefield']);acquired=True;draw_hand=pl['handCount'];draw_lib=pl['libraryCount'];snap(m,'acquired')
      if initiative:assert any(c['name']=='Undercity' and c['dungeonRoom']=='Secret Entrance' for c in pl['effectMarkers']) and pl['handCount']==(2 if leave else 1)
     if acquired and leave and not killed and not stack and stage==1:
      pick='card:'+str(next(c['id'] for c in pl['hand'] if c['name']=='Murder'));stage=2
     elif leave and stage==2 and not stack and not killed:
      assert any(c['id']==source for c in pl['graveyardCards']) and owner==1;killed=True;snap(m,'source-left');draw_hand=pl['handCount'];draw_lib=pl['libraryCount']
      if initiative:return finish('PASS','actual White Plume cast/ETB takes initiative and Secret Entrance finds one basic; Murder kills source but native initiative and helper/dungeon remain, both public marker projections and next priority')
     if acquired and not stack:
      if combat and owner==2:
       assert attacked and pl['life']==28 and op['life']==30 and any(c['id']==source for c in pl['battlefield']);snap(m,'transferred')
       if initiative:assert any(c['name']=='Undercity' and c['dungeonRoom']=='Secret Entrance' for c in op['effectMarkers']) and op['handCount']==2
       return finish('PASS','actual '+source_name+' grants status, opponent Bear unblocked combat transfers exact native status/helper; life28/30, original source remains, '+('new opponent Secret Entrance searches basic while original dungeon remains; ' if initiative else '')+'both projections and next priority')
      if False:
       assert pl['handCount']==draw_hand+1 and pl['libraryCount']==draw_lib-1 and owner==1;snap(m,'endstep-draw');return finish('PASS','actual Palace cast/ETB becomes monarch; '+('Murder kills source and independent crown remains; ' if leave else '')+'own endstep draws exactly1 from library, source/status identity and both marker views, next priority')
      if initiative and not combat and not leave and v['turn']==3 and v['phase']=='MAIN1':
       c=next(c for c in pl['battlefield'] if c['id']==source);assert owner==1 and c['power']==5 and c['toughness']==5 and any(c['name']=='Undercity' and c['dungeonRoom']=='Forge' for c in pl['effectMarkers']);snap(m,'upkeep-advanced');return finish('PASS','actual initiative ETB Secret Entrance finds basic; next own upkeep ventures into selected Forge and places exactlytwo +1/+1 counters on exact Adventurer (5/5), status persists, both marker views and next priority')
     if acquired and v['phase']=='END_OF_TURN' and not throne_used and r['seat']==2 and any(e['source']['name']=='The Monarch' and 'draw a card' in e['description'] for e in stack):
      old=next(e for e in stack if e['source']['name']=='The Monarch' and 'draw a card' in e['description']);old_draw_id=old['id'];old_crown_id=old['source']['id'];pick='card:'+str(next(c['id'] for c in op['battlefield'] if c['name']=='Throne of the High City'));throne_used=True;snap(m,'old-draw-before-transfer')
     elif throne_used and owner==2 and any(e['id']==old_draw_id for e in stack):
      old=next(e for e in stack if e['id']==old_draw_id);assert old['source']['id']==old_crown_id and old['source']['helperArtName']=='The Monarch' and old['controllerSeat']==1;snap(m,'old-draw-after-transfer')
     elif throne_used and owner==2 and not stack:
      assert pl['handCount']==draw_hand+1 and pl['libraryCount']==draw_lib-1 and all(c['id']!=old_crown_id for c in pl['effectMarkers']) and any(c['helperArtName']=='The Monarch' for c in op['effectMarkers']) and any(c['name']=='Throne of the High City' for c in op['graveyardCards']);snap(m,'old-draw-resolved');return finish('PASS','actual Palace cast establishes monarch; endstep draw pending, opponent activates and sacrifices Throne to take crown, old creator-controlled draw retains source ID/art after helper removal and still draws exactly1, new crown stays opponent, both views and next priority')
     if pick is None:pick='ok'
    elif 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif 'PayMana' in kind:pick='ok' if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled') else next(o['id'] for o in opts if o['id'].startswith('card:'))
    elif kind=='InputAttack':
     if combat and r['seat']==2 and v['turn']==2 and not attacked:pick=next(o['id'] for o in opts if o['id'].startswith('attack:') and o['defenderKind']=='player');attacked=True
     else:pick='ok'
    elif kind=='InputBlock':pick='ok'
    elif 'SelectTargets' in kind:
     if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled'):pick='ok'
     else:pick=next(o['id'] for o in opts if o.get('card',{}).get('id')==source)
    elif 'SelectCards' in kind or 'SelectEntities' in kind:
     if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled'):pick='ok'
     else:pick=next(o['id'] for o in opts if o.get('card') and not o.get('selected') and not o.get('disabled'))
    elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in ids else next(o['id'] for o in opts if o['id'].startswith('order:'))
    elif kind in ['dialog','Option'] and throne_used:pick=next(o['id'] for o in opts if 'become the monarch' in o['label'].lower())
    elif kind in ['dialog','Option']:pick=next((o['id'] for o in opts if 'Forge' in o['label']),next(i for i in ids if i not in ['cancel','no']))
    elif kind=='RevealCards':pick='confirm-cards'
    elif 'ok' in ids:pick='ok'
    else:raise AssertionError(('Unhandled',kind,r['message'],ids))
    assert pick in ids,(kind,pick,ids);print(case,step,kind,r['seat'],pick,'stage',stage,'owner',owner,'phase',v['phase'],flush=True);p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   raise AssertionError('Step limit')
  except Exception:finish('FAIL',traceback.format_exc());raise
  finally:
   if p.poll() is None:p.kill();p.wait()
if __name__=='__main__':
 results=[]
 for case in sys.argv[1:] or CASES:
  try:d=run(case);print(d['result'],flush=True);results.append(d)
  except Exception as e:print('FAIL '+case+': '+repr(e),flush=True);results.append(json.loads((OUT/(case+'-result.json')).read_text()))
 (OUT/'results.json').write_text(json.dumps({'status':'PASS' if all(d['status']=='PASS' for d in results) else 'FAIL','results':results},ensure_ascii=False,indent=2)+'\n',encoding='utf8');sys.exit(any(d['status']!='PASS' for d in results))
