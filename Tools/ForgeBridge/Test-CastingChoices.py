"""Real Forge casting choices: Adventure, both MDFC faces and Spree costs/modes."""
import importlib.util,json,os,queue,subprocess,tempfile,threading,sys
from pathlib import Path
spec=importlib.util.spec_from_file_location('advanced',Path(__file__).with_name('Test-AdvancedInteractions.py'))
base=importlib.util.module_from_spec(spec);spec.loader.exec_module(base)
out=base.root/'Tools/Validation/Reports/CastingChoices';out.mkdir(parents=True,exist_ok=True)

def run(case):
 with tempfile.TemporaryDirectory(prefix='mtgb-casting-') as temp,(out/(case+'.log')).open('w',encoding='utf-8') as log,(out/(case+'.jsonl')).open('w',encoding='utf-8') as trace:
  config=Path(temp)/'start.json';config.write_text(json.dumps(dict(session=case,fixture=case,seed=4,startingLife=20,firstPlayerSeat=1,decks=[base.deck,base.deck])),encoding='utf-8')
  p=subprocess.Popen([str(base.runtime/base.cfg['java']),'-Djava.awt.headless=true','-cp',str(base.classes)+';'+str(base.runtime/'classes')+';'+str(base.runtime/'lib/*'),'mtgb.bridge.AdvancedInteractionFixture',str(base.runtime/base.cfg['assets']),str(config)],cwd=base.runtime/base.cfg['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
  q=queue.Queue();threading.Thread(target=lambda:[q.put(json.loads(line)) for line in p.stdout],daemon=True).start()
  def snap(m,label):
   r=m['request'];v=dict(m['views'][r['seat']-1]);v.update(request=r,awaitingInput=True);(out/(case+'-'+label+'.json')).write_text(json.dumps(v),encoding='utf-8')
  try:
   cast=0;cancelled=False;mode_step=0;menu=False;exile=False
   for step in range(130):
    while True:
     m=q.get(timeout=40);trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ('fatal','ended','rejected'),m
     if m['type']=='request':break
    r=m['request'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];one,two=m['views'][0]['players'];field=one['battlefield'];stack=m['views'][0]['stackItems'];pick='ok'
    print(case,step,kind,[(o['id'],o['label'][:60]) for o in opts],flush=True)
    if kind=='InputPassPriority':
     if cast and not stack:
      if case=='cast-graveyard' and one['libraryCount']==52:
       assert 'Think Twice' not in one['graveyard'] and not any(c['name']=='Think Twice' for c in m['views'][0]['playableGraveyard'])
       snap(m,'resolved');return 'PASS graveyard: legal flashback projected, cast from graveyard, draw resolves, used entry removed'
      if case=='cast-exile' and 'Reckless Impulse' in one['graveyard']:
       available=m['views'][0]['playableExile'];assert len(available)==2 and all(c['zone']=='Exile' for c in available)
       assert not m['views'][1]['playableExile']
       if r['seat']==1:
        assert all('card:'+str(c['id']) in ids for c in available);snap(m,'exile-ready');return 'PASS impulse exile: both revealed cards projected, own legal play actions, opponent excluded'
      if case=='cast-omen' and one['libraryCount']==54:
       assert 'Scavenger Regent' not in one['graveyard'] and not any(c['name']=='Scavenger Regent' for c in one['hand'])
       assert 'Grizzly Bears' in two['graveyard'] and menu;snap(m,'resolved');return 'PASS Omen: Exude Toxin face, X=4, non-Dragon dies, card shuffled into library'
      if case=='cast-mdfc-back' and any(c['name']=='Bala Ged Sanctuary' for c in field):
       c=next(c for c in field if c['name']=='Bala Ged Sanctuary');assert c['land'] and c['backFace'] and c['tapped'] and menu;snap(m,'resolved');return 'PASS MDFC back: separate face preview, cancel/reopen, land play and tapped back-face battlefield'
      if case=='cast-mdfc-front' and 'Bala Ged Recovery' in one['graveyard']:
       assert any(c['name']=='Grizzly Bears' for c in one['hand']) and menu;snap(m,'resolved');return 'PASS MDFC front: distinct spell preview, green payment, graveyard target returned to hand'
      if case=='cast-spree' and 'Requisition Raid' in one['graveyard']:
       assert not any(c['name'] in ("Tormod's Crypt",'Glorious Anthem') for c in two['battlefield'])
       bear=next(c for c in field if c['name']=='Grizzly Bears');assert bear['power']==3 and bear['toughness']==3
       assert sum(c['tapped'] for c in field if c['land'])==4 and cancelled;snap(m,'resolved');return 'PASS Spree: cancel, toggle, reset, all three modes, four mana, both destructions and +1/+1'
      if case=='cast-adventure':
       if any(c['name']=='Bonecrusher Giant' for c in field):
        assert exile and two['life']==18 and menu;snap(m,'resolved');return 'PASS Adventure: Stomp face, two damage, exile, creature cast from exile and enters battlefield'
       if two['life']==18:exile=True
     if r['seat']==1 and m['views'][0]['phase']=='MAIN1' and not stack:
      name='Think Twice' if case=='cast-graveyard' else 'Reckless Impulse' if case=='cast-exile' else 'Bonecrusher Giant' if case=='cast-adventure' else 'Scavenger Regent' if case=='cast-omen' else 'Requisition Raid' if case=='cast-spree' else 'Bala Ged Recovery'
      card=next((o for o in opts if o['label'].startswith(name+' [')),None)
      if card and (cast==0 or case=='cast-adventure' and exile):
       if case=='cast-graveyard':
        assert any(c['name']=='Think Twice' for c in m['views'][0]['playableGraveyard']);assert not m['views'][1]['playableGraveyard'];snap(m,'graveyard-ready')
       if case=='cast-adventure' and exile:
        assert any(c['name']=='Bonecrusher Giant' for c in m['views'][0]['playableExile'])
        snap(m,'exile-ready')
       pick=card['id'];cast+=1
    elif kind=='dialog':
     faces=[o for o in opts if o.get('castingFace')]
     if faces:
      menu=True;snap(m,'menu');assert len({o['castingFace']['name'] for o in faces})==2
      if not cancelled:pick='cancel';cancelled=True;cast=0
      else:
       wanted='adventure' if case=='cast-adventure' else 'omen' if case=='cast-omen' else 'mdfc-back' if case.endswith('back') else 'mdfc-front'
       chosen=next(o for o in faces if o['faceKind']==wanted);pick=chosen['id']
       if wanted=='adventure':assert chosen['castingFace']['name']=='Stomp'
       if wanted=='mdfc-back':assert chosen['castingFace']['backFace']
     else:pick=ids[0]
    elif kind=='ChooseNumber':pick='number-plus' if r['selectedValue']<(4 if case=='cast-omen' else 2) else 'confirm-number'
    elif kind=='ChooseModes':
     snap(m,'modes-'+str(mode_step))
     assert all(o.get('modeCost')=='{1}' for o in opts if o['id'].startswith('mode:'))
     if not cancelled:pick='cancel';cancelled=True;cast=0
     else:
      actions=['mode:0','mode:0','mode:1','reset-modes','mode:0','mode:1','mode:2','confirm-modes']
      expected=[0,1,0,1,0,1,2,3][mode_step];assert r['selectedCount']==expected
      assert ('confirm-modes' in ids)==(expected>=1);pick=actions[mode_step];mode_step+=1
    elif kind.startswith('InputPayMana'):
     colors=['W','G','R','U','B'];pick=next((i for color in colors for i in ids if i=='mana:'+color),None)
     if pick is None:
      names=['Island','Mountain','Forest','Plains','Swamp'] if case=='cast-graveyard' else ['Swamp','Forest','Mountain','Plains','Island'] if case=='cast-omen' else ['Plains','Forest','Mountain','Island','Swamp'] if case=='cast-spree' else ['Forest','Mountain','Plains','Island','Swamp'] if 'mdfc' in case else ['Mountain','Forest','Island','Plains','Swamp']
      pick=next(o['id'] for name in names for o in opts if o['label'].startswith(name+' ['))
    elif kind=='InputSelectTargets':
     if r.get('selectedCount') and 'ok' in ids:pick='ok'
     elif case=='cast-spree':
      pick=next((o['id'] for o in opts if o.get('card',{}).get('name') in ("Tormod's Crypt",'Glorious Anthem')),None) or 'player:'+str(one['targetId'])
     elif 'mdfc' in case:pick=next(o['id'] for o in opts if o.get('card',{}).get('name')=='Grizzly Bears')
     else:pick='player:'+str(two['targetId'])
    elif kind.startswith('InputSelect'):pick='ok' if 'ok' in ids else next(i for i in ids if i.startswith('card:'))
    assert pick in ids,(kind,pick,opts)
    p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
   raise AssertionError('No completion after 130 choices')
  finally:
   if p.poll() is None:
    p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
    try:p.wait(timeout=3)
    except subprocess.TimeoutExpired:p.kill();p.wait()

results=[]
for case in sys.argv[1:] or ['cast-adventure','cast-mdfc-front','cast-mdfc-back','cast-spree','cast-omen']:
 try:results.append(run(case))
 except Exception as e:results.append('FAIL '+case+': '+repr(e))
(out/'result.txt').write_text('\n'.join(results),encoding='utf-8');print('\n'.join(results));sys.exit(any(s.startswith('FAIL') for s in results))
