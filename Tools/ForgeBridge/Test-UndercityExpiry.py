"""Native venture branches, effects and completion; fixture only sets initial positions."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';cfg=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/undercity-paths-1006/expiry-native';OUT.mkdir(parents=True,exist_ok=True);classes=OUT/'classes';classes.mkdir(exist_ok=True)
fixture=ROOT/'Tools/ForgeBridge/tests/UndercityStateFixture.java';subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),str(fixture)],check=True)
CASES=['well-arena-complete-expiry'];deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]};sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def run(case):
 dungeon='Undercity'
 route={'well-arena-complete-expiry':['Secret Entrance','Lost Well','Arena','Catacombs','Throne of the Dead Three'],'forge-trap-reenter':['Secret Entrance','Forge','Trap!','Archive','Throne of the Dead Three','Secret Entrance'],'well-arena-empty':['Secret Entrance','Lost Well','Arena','Catacombs','Throne of the Dead Three'],'well-stash':['Secret Entrance','Lost Well','Stash','Catacombs','Throne of the Dead Three']}[case]
 successors={'Secret Entrance':['Forge','Lost Well'],'Forge':['Trap!','Arena'],'Lost Well':['Arena','Stash'],'Trap!':['Archive'],'Arena':['Archive','Catacombs'],'Stash':['Catacombs'],'Archive':['Throne of the Dead Three'],'Catacombs':['Throne of the Dead Three']}
 own={'Hand':['White Plume Adventurer']*(2 if case=='forge-trap-reenter' else 1)+['Kick in the Door']*(len(route)-(2 if case=='forge-trap-reenter' else 1)),'Battlefield':['Grizzly Bears','Elvish Mystic']+['Mountain']*10,'Library':['Island','Forest','Plains']+(['Grizzly Bears']*20 if case!='well-arena-empty' else ['Island']*20)};other={'Battlefield':['Hill Giant']}
 folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);meta={'case':case,'folder':folder.relative_to(ROOT).as_posix(),'runtimeHash':cfg['runtimeHash'],'driverSha256':sha(Path(__file__)),'fixtures':[{'path':fixture.relative_to(ROOT).as_posix(),'sha256':sha(fixture)}]}
 def finish(status,msg):
  d={**meta,'status':status,'result':status+' '+case+': '+msg}
  for file in [folder/'result.json',OUT/(case+'-result.json')]:file.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return d
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'trace.jsonl').open('w',encoding='utf8') as trace,(folder/'engine.log').open('w',encoding='utf8') as log:
  audit=Path(temp)/'audit';audit.mkdir();start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,actor=1,own=own,other=other,auditDir=str(audit),seed=4,firstPlayerSeat=1,startingLife=30,decks=[deck,deck])),encoding='utf8')
  p=subprocess.Popen([str(RT/cfg['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.UndercityStateFixture',str(RT/cfg['assets']),str(start)],cwd=RT/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');messages=queue.Queue()
  def reader():
   try:
    for line in p.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,name):
   for seat,v in enumerate(m['views'],1):
    d=dict(v);d.update(request=m['request'],awaitingInput=True);(folder/(name+('' if seat==1 else '-other')+'.json')).write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf8')
  def observe(r):
   q=audit/(str(r['requestId'])+'.json');until=time.monotonic()+3
   while True:
    try:d=json.loads(q.read_text());break
    except (FileNotFoundError,PermissionError,json.JSONDecodeError):
     if time.monotonic()>=until:raise
     time.sleep(.01)
   (folder/('audit-'+str(r['requestId'])+'.json')).write_text(json.dumps(d,indent=2),encoding='utf8');return d
  cast=0;resolved=0;bear=None;enemy=None;helper=None;discard=None;sacrificed=set();scry_counts=[];beforehand=None;beforelibrary=None;freecast=None;throne=None;enemy_attack=False;deadline=time.monotonic()+300
  try:
   for step in range(600):
    assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=40);assert m is not None,'Native exit';trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
    if m['type']!='request':continue
    r=m['request'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];v=m['views'][0];pl,op=v['players'];stack=v['stackItems'];pick=None
    if bear is None and any(c['name']=='Grizzly Bears' for c in pl['battlefield']):bear=next(c['id'] for c in pl['battlefield'] if c['name']=='Grizzly Bears');enemy=next(c['id'] for c in op['battlefield'] if c['name']=='Hill Giant')
    if kind=='InputPassPriority':
     if cast>resolved and not stack:
      resolved=cast;room=route[resolved-1];dgs=[c for c in pl['effectMarkers'] if c['markerKind']=='dungeon'];snap(m,'resolved-'+str(resolved))
      kicks=resolved-(2 if resolved==len(route) and case=='forge-trap-reenter' else 1)
      assert len([c for c in pl['graveyardCards'] if c['name']=='Kick in the Door'])==kicks
      assert next(c for c in pl['battlefield'] if c['id']==bear)['power']==2+kicks+(2 if case=='forge-trap-reenter' and resolved>=2 else 0)
      if room=='Throne of the Dead Three':
       assert not dgs and dungeon in pl['completedDungeons'];candidates=[c for c in pl['battlefield'] if c['name']=='Grizzly Bears' and c['id']!=bear]
       if case=='well-arena-empty':assert not candidates and pl['libraryCount']==beforelibrary
       else:
        assert len(candidates)==1;throne=candidates[0]['id'];assert candidates[0]['power']==candidates[0]['toughness']==5 and 'HEXPROOF' in candidates[0]['keywords'] and sum(c['count'] for c in candidates[0]['counters'] if c['kind']=='P1P1')==3 and pl['libraryCount']==beforelibrary-1
      else:
       assert len(dgs)==1 and dgs[0]['name']==dungeon
       if helper is None:helper=dgs[0]['id']
       elif resolved==len(route) and room==route[0]:assert dgs[0]['id']!=helper and dungeon in pl['completedDungeons']
       else:assert dgs[0]['id']==helper
      if room=='Secret Entrance':assert pl['handCount']==beforehand and pl['libraryCount']==beforelibrary-1 and any(c['land'] for c in pl['hand'])
      if room=='Forge':assert sum(c['count'] for c in next(c for c in pl['battlefield'] if c['id']==bear)['counters'] if c['kind']=='P1P1')==3
      if room=='Trap!':assert op['life']==25 and pl['life']==30
      if room=='Arena':assert next(c for c in observe(r)['cards'] if c['id']==enemy)['goadedByActor']
      if room=='Stash':assert any(c.get('token',False) and 'Treasure' in c['name'] for c in pl['battlefield'])
      if room=='Archive':assert pl['handCount']==beforehand and pl['libraryCount']==beforelibrary-1
      if room=='Catacombs':assert any(c.get('token',False) and 'Skeleton' in c['currentType'] and c['power']==4 and c['toughness']==1 and 'MENACE' in c['keywords'] for c in pl['battlefield'])
      if resolved==len(route) and False:return finish('PASS','actual '+str(cast)+' native White Plume/Kick casts/target/cost, exact native legal successors '+str(route)+', named room effects/identities, completion'+('/new dungeon identity on reentry' if route[-1]==route[0] else '')+', both projections and next priority; separate UI proof required')
     if resolved==len(route) and v['turn']==3 and v['phase']=='MAIN1' and not stack:
      c=next(c for c in pl['battlefield'] if c['id']==throne);assert 'HEXPROOF' not in c['keywords'] and c['power']==c['toughness']==5 and sum(c['count'] for c in c['counters'] if c['kind']=='P1P1')==3
      assert not next(c for c in observe(r)['cards'] if c['id']==enemy)['goaded'];assert pl['life']==27 and op['life']==30;snap(m,'expiry-restored');return finish('PASS','actual White Plume and four Kick casts through Arena/Catacombs/Throne, native observer exact goad identity and attack for3 in opponent turn, until-next-own-turn goad/hexproof removal while three counters/5-5 persist, actual initiative upkeep reentry and next main priority; multiplayer combat separate')
     if cast==resolved and cast<len(route) and not stack and r['seat']==1 and v['phase']=='MAIN1':
      beforehand=pl['handCount'];beforelibrary=pl['libraryCount'];pick='card:'+str(next(c['id'] for c in pl['hand'] if c['name']==('White Plume Adventurer' if route[cast]=='Secret Entrance' else 'Kick in the Door')));cast+=1
     else:pick='ok'
    elif 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif 'PayMana' in kind:pick='ok' if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled') else next(o['id'] for o in opts if o['id'].startswith('card:'))
    elif 'SelectTargets' in kind:
     if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled'):pick='ok'
     else:pick=next(o['id'] for o in opts if (o.get('id')=='player:'+str(op['targetId']) if route[cast-1]=='Trap!' and r.get('sourceCard',{}).get('name')==dungeon else o.get('card',{}).get('id')==(enemy if route[cast-1]=='Arena' and r.get('sourceCard',{}).get('name')==dungeon else bear)));snap(m,'target-'+str(cast)+'-'+str(step))
    elif r.get('selectionPurpose')=='dungeon':
     assert len(opts)==1 and opts[0]['card']['name']==dungeon;pick=next(o['id'] for o in opts if o['card']['name']==dungeon);snap(m,'dungeon-choice-'+str(cast))
    elif r.get('selectionPurpose')=='dungeonRoom':
     previous=route[cast-2];assert {o['label'].split(' —')[0] for o in opts}==set(successors[previous]),('Legal successors',r,previous);assert r['sourceCard']['name']==dungeon
     pick=next(o['id'] for o in opts if o['label'].startswith(route[cast-1]+' —'));snap(m,'room-choice-'+str(cast))
    elif kind=='Scry':
     snap(m,'scry-'+str(cast));scry_counts.append(len([o for o in opts if o['id'].startswith('scry-top:')]))
     pick='confirm-scry' if 'confirm-scry' in ids else next(i for i in ids if i.startswith('scry-top:'))
    elif kind=='RevealCards':pick='confirm-cards';snap(m,'reveal-'+str(cast))
    elif 'SelectCards' in kind or 'SelectEntities' in kind:
     if r['seat']==2 and 'cancel' in ids and route[cast-1] in ['Veils of Fear','Sandfall Cell']:pick='cancel'
     elif 'Select a card to play' in r['message']:
      candidates=[o for o in opts if o.get('card',{}).get('name')=='Grizzly Bears' and o['card']['zone']=='Hand']
      if 'ok' in ids:pick='ok'
      elif candidates:pick=candidates[0]['id'];freecast=candidates[0]['card']['id']
      else:pick='cancel'
     elif 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled'):pick='ok'
     else:
      candidates=[o for o in opts if o.get('card') and not o.get('selected') and not o.get('disabled')]
      # Mandatory Oubliette sacrifice has distinct types; choose spare objects.
      target=next((o for o in candidates if o['card']['name'] in ['Island','Sol Ring','Elvish Mystic','Plains']),candidates[0]);pick=target['id']
      if target['card']['zone']=='Hand':discard=target['card']['id']
      elif target['card']['zone']=='Battlefield':sacrificed.add(target['card']['id'])
     snap(m,'select-'+str(cast)+'-'+str(step))
    elif kind in ['dialog','Option']:
     if route[cast-1] in ['Veils of Fear','Sandfall Cell'] and ('yes' in ids or 'no' in ids):pick='yes' if r['seat']==1 else 'no'
     elif 'no' in ids:pick='no'
     elif 'cancel' in ids:pick='cancel'
     else:pick=ids[0]
     snap(m,'option-'+str(cast)+'-'+str(step))
    elif kind=='InputConfirm' and 'Do you want to play Grizzly Bears?' in r['message']:
     freecast=next(c['id'] for c in pl['hand'] if c['name']=='Grizzly Bears');pick='ok';snap(m,'freecast-choice')
    elif kind=='InputAttack':
     if r['seat']==2 and not enemy_attack:pick=next(o['id'] for o in opts if o.get('attackerId')==enemy and o.get('defenderKind')=='player');snap(m,'goaded-attack');enemy_attack=True
     else:pick='ok'
    elif kind=='OrderAbilities':pick='confirm-order'
    elif 'ok' in ids:pick='ok'
    else:raise AssertionError(('Unhandled',kind,r['message'],ids))
    assert pick in ids,(kind,pick,ids);print(case,step,kind,r['seat'],pick,'cast',cast,'resolved',resolved,r['message'][:95],flush=True);p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
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
