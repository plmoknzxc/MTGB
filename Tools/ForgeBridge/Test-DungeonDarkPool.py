"""Native venture branches, effects and completion; fixture only sets initial positions."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';cfg=json.loads((RT/'runtime.json').read_text('utf-8-sig'));OUT=ROOT/'.tools/dungeon-paths-1006/pool-native';OUT.mkdir(parents=True,exist_ok=True);classes=OUT/'classes';classes.mkdir(exist_ok=True)
fixture=ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java';subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),str(fixture)],check=True)
CASES=['mine-dark-pool'];deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]};sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
def run(case):
 dungeon='Lost Mine of Phandelver' if case.startswith('mine') else 'Tomb of Annihilation' if case.startswith('tomb') else 'Dungeon of the Mad Mage'
 route={'mine-dark-pool':['Cave Entrance','Goblin Lair','Dark Pool','Temple of Dumathoin'],'mine-goblin-reenter':['Cave Entrance','Goblin Lair','Storeroom','Temple of Dumathoin','Cave Entrance'],'mine-fungi':['Cave Entrance','Mine Tunnels','Fungi Cavern','Temple of Dumathoin'],'tomb-slow':['Trapped Entry','Veils of Fear','Sandfall Cell','Cradle of the Death God'],'tomb-oubliette':['Trapped Entry','Oubliette','Cradle of the Death God'],'mage-runestone':['Yawning Portal','Dungeon Level','Goblin Bazaar','Lost Level','Runestone Caverns','Deep Mines',"Mad Wizard's Lair",'Yawning Portal'],'mage-graveyard':['Yawning Portal','Dungeon Level','Twisted Caverns','Lost Level',"Muiral's Graveyard",'Deep Mines',"Mad Wizard's Lair"]}[case]
 successors={'Cave Entrance':['Goblin Lair','Mine Tunnels'],'Goblin Lair':['Storeroom','Dark Pool'],'Mine Tunnels':['Dark Pool','Fungi Cavern'],'Dark Pool':['Temple of Dumathoin'],'Storeroom':['Temple of Dumathoin'],'Fungi Cavern':['Temple of Dumathoin'],'Trapped Entry':['Veils of Fear','Oubliette'],'Veils of Fear':['Sandfall Cell'],'Oubliette':['Cradle of the Death God'],'Sandfall Cell':['Cradle of the Death God'],'Yawning Portal':['Dungeon Level'],'Dungeon Level':['Goblin Bazaar','Twisted Caverns'],'Goblin Bazaar':['Lost Level'],'Twisted Caverns':['Lost Level'],'Lost Level':['Runestone Caverns',"Muiral's Graveyard"],'Runestone Caverns':['Deep Mines'],"Muiral's Graveyard":['Deep Mines'],'Deep Mines':["Mad Wizard's Lair"]}
 own={'Hand':['Kick in the Door']*len(route)+['Island'],'Battlefield':['Grizzly Bears','Elvish Mystic','Sol Ring']+['Mountain']*10,'Library':['Island','Forest','Plains','Swamp','Mountain','Forest','Island','Plains','Swamp','Mountain']};other={'Battlefield':['Hill Giant']}
 if case=='mage-runestone':own['Library']=['Grizzly Bears']*3+['Forest','Plains','Island','Mountain','Swamp','Island','Plains']
 folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True);meta={'case':case,'folder':folder.relative_to(ROOT).as_posix(),'runtimeHash':cfg['runtimeHash'],'driverSha256':sha(Path(__file__)),'fixtures':[{'path':fixture.relative_to(ROOT).as_posix(),'sha256':sha(fixture)}]}
 def finish(status,msg):
  d={**meta,'status':status,'result':status+' '+case+': '+msg}
  for file in [folder/'result.json',OUT/(case+'-result.json')]:file.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
  return d
 with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'trace.jsonl').open('w',encoding='utf8') as trace,(folder/'engine.log').open('w',encoding='utf8') as log:
  audit=Path(temp)/'audit';audit.mkdir();start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,actor=1,own=own,other=other,auditDir=str(audit),seed=4,firstPlayerSeat=1,startingLife=30,decks=[deck,deck])),encoding='utf8')
  p=subprocess.Popen([str(RT/cfg['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.RoyalStateFixture',str(RT/cfg['assets']),str(start)],cwd=RT/cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8');messages=queue.Queue()
  def reader():
   try:
    for line in p.stdout:messages.put(json.loads(line))
   finally:messages.put(None)
  threading.Thread(target=reader,daemon=True).start()
  def snap(m,name):
   for seat,v in enumerate(m['views'],1):
    d=dict(v);d.update(request=m['request'],awaitingInput=True);(folder/(name+('' if seat==1 else '-other')+'.json')).write_text(json.dumps(d,ensure_ascii=False,indent=2),encoding='utf8')
  cast=0;resolved=0;bear=None;enemy=None;helper=None;discard=None;sacrificed=set();scry_counts=[];beforehand=None;beforelibrary=None;freecast=None;deadline=time.monotonic()+300
  try:
   for step in range(600):
    assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=40);assert m is not None,'Native exit';trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ['fatal','ended','rejected'],m
    if m['type']!='request':continue
    r=m['request'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];v=m['views'][0];pl,op=v['players'];stack=v['stackItems'];pick=None
    if bear is None and any(c['name']=='Grizzly Bears' for c in pl['battlefield']):bear=next(c['id'] for c in pl['battlefield'] if c['name']=='Grizzly Bears');enemy=next(c['id'] for c in op['battlefield'] if c['name']=='Hill Giant')
    if kind=='InputPassPriority':
     if cast>resolved and not stack:
      resolved=cast;room=route[resolved-1];dgs=[c for c in pl['effectMarkers'] if c['markerKind']=='dungeon'];snap(m,'resolved-'+str(resolved))
      assert len([c for c in pl['graveyardCards'] if c['name']=='Kick in the Door'])==resolved
      assert next(c for c in pl['battlefield'] if c['id']==bear)['power']==2+resolved+(1 if case=='mine-goblin-reenter' and resolved>=3 else 0)
      if room in ['Temple of Dumathoin','Cradle of the Death God',"Mad Wizard's Lair"]:
       assert not dgs and dungeon in pl['completedDungeons'],('Completion',pl)
       if room=='Temple of Dumathoin':assert pl['handCount']==beforehand and pl['libraryCount']==beforelibrary-1
       if room=='Cradle of the Death God':
        token=next(c for c in pl['battlefield'] if c.get('token',False) and 'Atropal' in c['name']);assert token['power']==token['toughness']==4 and 'Legendary' in token['currentType'] and 'DEATHTOUCH' in token['keywords']
       if room=="Mad Wizard's Lair":
        assert pl['handCount']==beforehand+(1 if case=='mage-runestone' else 2) and pl['libraryCount']==beforelibrary-3
        if case=='mage-runestone':assert freecast is not None and freecast!=bear and any(c['id']==freecast and c['power']==c['toughness']==2 for c in pl['battlefield'])
      else:
       assert len(dgs)==1 and dgs[0]['name']==dungeon
       if helper is None:helper=dgs[0]['id']
       elif resolved==len(route) and resolved>4 and room==route[0]:assert dgs[0]['id']!=helper and dungeon in pl['completedDungeons']
       else:assert dgs[0]['id']==helper
      if room=='Goblin Lair':assert any(c.get('token',False) and c['power']==c['toughness']==1 and 'Goblin' in c['currentType'] for c in pl['battlefield'])
      if room in ['Mine Tunnels','Goblin Bazaar']:assert any(c.get('token',False) and 'Treasure' in c['name'] for c in pl['battlefield'])
      if room=='Dark Pool':assert pl['life']==31 and op['life']==29
      if room=='Storeroom':assert sum(c['count'] for c in next(c for c in pl['battlefield'] if c['id']==bear)['counters'] if c['kind']=='P1P1')==resolved+1
      if room=='Fungi Cavern':assert next(c for c in op['battlefield'] if c['id']==enemy)['power']==-1
      if room=='Trapped Entry':assert pl['life']==op['life']==29
      if room=='Veils of Fear':assert pl['life']==29 and op['life']==27 and discard is not None and any(c['id']==discard for c in pl['graveyardCards'])
      if room=='Sandfall Cell':assert pl['life']==29 and op['life']==25 and len(sacrificed)==1
      if room=='Oubliette':assert len(sacrificed)==3 and discard is not None and all(any(c['id']==i for c in pl['graveyardCards']) for i in sacrificed|{discard})
      if room=='Yawning Portal':assert pl['life']==(32 if resolved==len(route) and len(route)>7 else 31) and op['life']==30
      if room=="Muiral's Graveyard":assert len([c for c in pl['battlefield'] if c.get('token',False) and c['power']==c['toughness']==1 and 'Skeleton' in c['currentType']])==2
      if room=='Runestone Caverns':assert len(pl['exileCards'])==2 and pl['libraryCount']==beforelibrary-2
      if resolved==len(route):return finish('PASS','actual '+str(cast)+' Kick casts/target/cost, exact native legal successors '+str(route)+', named room effects/identities, completion'+('/new dungeon identity on reentry' if route[-1]==route[0] else '')+', both projections and next priority; separate UI proof required')
     if cast==resolved and cast<len(route) and not stack and r['seat']==1 and v['phase']=='MAIN1':
      beforehand=pl['handCount'];beforelibrary=pl['libraryCount'];pick='card:'+str(next(c['id'] for c in pl['hand'] if c['name']=='Kick in the Door'));cast+=1
     else:pick='ok'
    elif 'Mulligan' in kind:pick='keep' if 'keep' in ids else 'ok'
    elif 'PayMana' in kind:pick='ok' if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled') else next(o['id'] for o in opts if o['id'].startswith('card:'))
    elif 'SelectTargets' in kind:
     if 'ok' in ids and not next(o for o in opts if o['id']=='ok').get('disabled'):pick='ok'
     else:pick=next(o['id'] for o in opts if o.get('card',{}).get('id')==(enemy if route[cast-1] in ['Fungi Cavern','Twisted Caverns'] and r.get('sourceCard',{}).get('name')==dungeon else bear));snap(m,'target-'+str(cast)+'-'+str(step))
    elif r.get('selectionPurpose')=='dungeon':
     assert len(opts)==3 and {o['card']['name'] for o in opts}=={'Lost Mine of Phandelver','Dungeon of the Mad Mage','Tomb of Annihilation'};pick=next(o['id'] for o in opts if o['card']['name']==dungeon);snap(m,'dungeon-choice-'+str(cast))
    elif r.get('selectionPurpose')=='dungeonRoom':
     previous=route[cast-2];assert {o['label'].split(' —')[0] for o in opts}==set(successors[previous]),('Legal successors',r,previous);assert r['sourceCard']['helperArtName']==dungeon
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
