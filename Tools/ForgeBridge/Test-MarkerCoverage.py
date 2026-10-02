"""Check all pinned Forge delayed-trigger storage paths and public Command effects."""
import importlib.util,json,os,subprocess,tempfile,sys
from pathlib import Path
spec=importlib.util.spec_from_file_location('base',Path(__file__).with_name('Test-AdvancedInteractions.py'))
b=importlib.util.module_from_spec(spec);spec.loader.exec_module(b)
cp=str(b.runtime/'classes')+';'+str(b.runtime/'lib/*')
subprocess.run(['javac','-encoding','UTF-8','-cp',cp,'-d',str(b.classes),str(b.root/'Tools/ForgeBridge/tests/EffectMarkersFixture.java'),str(b.root/'Tools/ForgeBridge/tests/MarkerCoverageFixture.java')],check=True)
special='--special-states' in sys.argv
out=b.root/'Tools/Validation/Reports'/('SpecialStates' if special else 'MarkerCoverage');out.mkdir(parents=True,exist_ok=True)
with tempfile.TemporaryDirectory() as temp,(out/'engine.log').open('w',encoding='utf8') as log:
 cfg=Path(temp)/'start.json';cfg.write_text(json.dumps(dict(session='coverage',seed=4,startingLife=20,firstPlayerSeat=1,decks=[b.deck,b.deck])),encoding='utf8')
 p=subprocess.Popen([str(b.runtime/b.cfg['java']),'-Djava.awt.headless=true','-Dmtgb.specialStates='+str(special).lower(),'-cp',str(b.classes)+';'+cp,'mtgb.bridge.MarkerCoverageFixture',str(b.runtime/b.cfg['assets']),str(cfg)],cwd=b.runtime/b.cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
 try:
  for line in p.stdout:
   m=json.loads(line);assert m['type'] not in ('fatal','ended'),m
   if m['type']!='request':continue
   if m['views'][0]['turn']==0:
    r=m['request'];p.stdin.write(json.dumps(dict(type='respond',session='coverage',seat=r['seat'],requestId=r['requestId'],option=r['options'][0]['id']))+'\n');p.stdin.flush();continue
   markers=m['views'][0]['players'][0]['effectMarkers']
   text=' '.join(c.get('currentRules','') for c in markers)
   expected=['active','this-turn','player','future','extra-turn','extra-phase','no-spawning']
   for name in expected:assert 'coverage-'+name in text,(name,text)
   names=[c['name'] for c in markers]
   for name in ['The Monarch','The Initiative','Radiation','持续效果']:assert name in names,names
   assert 'SECRET-NAME' not in json.dumps(markers) and 'Ephemerate' not in json.dumps(markers),markers
   count=16 if special else 11
   assert len(markers)==count and len({c['id'] for c in markers})==count,markers
   if special:
    player=m['views'][0]['players'][0]
    assert {c['kind']:c['count'] for c in player['counters']}==dict(POISON=2,ENERGY=3,EXPERIENCE=4,RAD=5,TICKET=6),player['counters']
    assert player['speed']==2 and player['ringTemptedCount']==1
    dungeon=next(c for c in markers if c['markerKind']=='dungeon')
    assert dungeon['name']=='Lost Mine of Phandelver' and 'Cave Entrance' in dungeon['markerStatus']
    assert m['views'][0]['dayTime']=='day' and m['views'][0]['globalEffects'][0]['name']=='Day'
    for name in ["City's Blessing",'An Enduring Story','The Ring','Start Your Engines!']:assert name in names,names
    assert m['views'][1]['players'][0]['counters']==player['counters']
   assert m['views'][1]['players'][0]['effectMarkers']==markers
   (out/'snapshot.json').write_text(json.dumps(m['views'][0]),encoding='utf8')
   (out/'result.json').write_text(json.dumps(dict(status='PASS',delayedStoragePaths=expected,publicEffectObjects=names,markerCount=len(markers))),encoding='utf8')
   print('PASS: 7 delayed paths, 4 Command effects, unique IDs, both viewers, private sources redacted');break
  else:raise AssertionError('No request')
 finally:
  if p.poll() is None:p.kill();p.wait()
