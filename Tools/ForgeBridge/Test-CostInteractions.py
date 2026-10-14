"""Native cost matrix: expected CR totals are declared separately from Forge outcomes.

Fixtures arrange cards only. Each unique attempt retains every native request,
response and both views, plus actual paid resources and post-resolution checks.
"""
import hashlib,json,os,queue,re,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime'
OUT=ROOT/'.tools/cost-interactions-1006/native';OUT.mkdir(parents=True,exist_ok=True)
CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
DRIVER_SOURCE=Path(__file__).read_bytes();DRIVER_HASH=hashlib.sha256(DRIVER_SOURCE).hexdigest()
FIXTURE_SOURCE=(ROOT/'Tools/ForgeBridge/tests/CostInteractionFixture.java').read_bytes();FIXTURE_HASH=hashlib.sha256(FIXTURE_SOURCE).hexdigest()
CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(ROOT/'Tools/ForgeBridge/tests/CostInteractionFixture.java')],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES={}
def add(key,spell,mana,lands=(),mods=(),own=(),life=0,effect='draw',**kw):
    CASES[key]=dict(spell=spell,mana=mana,hand=[spell],field=list(lands)+list(own),enemy=list(mods)+['Grizzly Bears'],lifePaid=life,effect=effect,**kw)
for mod,label,extra in [('Sphere of Resistance','tax',1),('Helm of Awakening','reduce',-1),('Trinisphere','floor',0)]:
    add('hybrid-'+label,'Burning-Tree Emissary',3 if label=='floor' else 2+max(extra,0),['Forest','Mountain']+['Island']*2,[mod],effect='emissary')
    add('twobrid-'+label,'Spectral Procession',6+extra,['Island']*8,[mod],effect='spirits')
    add('snow-'+label,"Arcum's Astrolabe",3 if label=='floor' else 1+max(extra,0),['Snow-Covered Forest']+['Island']*3,[mod],effect='astrolabe')
    add('xx-'+label,'Walking Ballista',3 if label=='floor' else 4+extra,['Island']*7,[mod],effect='ballista',x=1 if label=='floor' else 2)
add('strict-colorless-tax','Thought-Knot Seer',5,['Wastes']+['Island']*4,['Sphere of Resistance'],effect='seer')
add('strict-colorless-reduce','Thought-Knot Seer',3,['Wastes']+['Island']*3,['Helm of Awakening'],effect='seer')
add('strict-colorless-wrong','Thought-Knot Seer',0,['Island']*6,[],effect='unavailable')
add('generic-reducer-keeps-color','Lightning Bolt',1,['Mountain'],[],['Helm of Awakening']*3,effect='bolt')
add('colored-reduce','Soul Warden',0,[],[],['Edgewalker'],effect='creature')
add('colored-reduce-floor','Soul Warden',3,['Island']*3,['Trinisphere'],['Edgewalker'],effect='creature')
add('colored-reduce-krrik','Priest of Forgotten Gods',1,['Island'],[],['Edgewalker',"K'rrik, Son of Yawgmoth"],effect='creature')
add('krrik-tax','Murder',2,['Island']*2,['Sphere of Resistance'],["K'rrik, Son of Yawgmoth"],life=4,effect='kill',reserve=4)
add('krrik-reduce','Murder',0,[],['Helm of Awakening'],["K'rrik, Son of Yawgmoth"],life=4,effect='kill',reserve=4)
add('life-additional-tax','Toxic Deluge',4,['Swamp']+['Island']*3,['Sphere of Resistance'],life=2,effect='wipe',x=2)
add('life-additional-reduce','Toxic Deluge',2,['Swamp','Island'],['Helm of Awakening'],life=2,effect='wipe',x=2)
add('life-additional-floor','Toxic Deluge',3,['Swamp']+['Island']*2,['Trinisphere'],life=2,effect='wipe',x=2)
for label,mods,mana in [('base',[],0),('tax',['Sphere of Resistance'],1),('reduce',['Helm of Awakening'],0),('floor',['Trinisphere'],3)]:
    add('citadel-'+label,'Opt',mana,['Island']*4,mods,["Bolas's Citadel"],life=1,effect='draw',library=['Opt'],fromLibrary=True)
    CASES['citadel-'+label]['hand']=[]
for label,mods,mana in [('tax',['Sphere of Resistance'],1),('reduce',['Helm of Awakening'],0),('floor',['Trinisphere'],3)]:
    add('alt-life-'+label,'Snuff Out',mana,['Swamp']+['Island']*4,mods,life=4,effect='kill',choice='Pay 4 life|without paying|rather than|Alternative')
add('convoke-floor','Gather Courage',0,[],['Trinisphere'],['Llanowar Elves']*3,effect='buff',tapSelect=3)
add('convoke-tax','Gather Courage',0,[],['Sphere of Resistance'],['Llanowar Elves']*2,effect='buff',tapSelect=2)
add('delve-floor','Murderous Cut',1,['Swamp'],['Trinisphere'],effect='kill',grave=['Island']*4,delve=4)
add('delve-tax','Murderous Cut',1,['Swamp'],['Sphere of Resistance'],effect='kill',grave=['Island']*5,delve=5)
add('delve-reduce','Murderous Cut',1,['Swamp'],['Helm of Awakening'],effect='kill',grave=['Island']*3,delve=3)
add('improvise-phy-floor','Gut Shot',0,[],['Trinisphere'],['Inspiring Statuary',"Tormod's Crypt",'Sol Ring'],life=2,effect='gut',reserve=2,tapSelect=3)
add('defiler-floor','Grizzly Bears',3,['Island']*3,['Trinisphere'],['Defiler of Vigor'],life=2,effect='creature',choice='Pay 2 life|pay.*2 life|Yes')
add('defiler-tax','Grizzly Bears',2,['Island']*2,['Sphere of Resistance'],['Defiler of Vigor'],life=2,effect='creature',choice='Pay 2 life|pay.*2 life|Yes')
add('defiler-reduce','Grizzly Bears',0,[],['Helm of Awakening'],['Defiler of Vigor'],life=2,effect='creature',choice='Pay 2 life|pay.*2 life|Yes')
add('affinity-floor','Frogmite',3,['Island']*3,['Trinisphere'],["Tormod's Crypt"]*5,effect='creature')
add('affinity-tax','Frogmite',0,[],['Sphere of Resistance'],["Tormod's Crypt"]*5,effect='creature')
add('affinity-reduce','Frogmite',0,[],['Helm of Awakening'],["Tormod's Crypt"]*3,effect='creature')
add('twobrid-colored-floor-reduce','Spectral Procession',3,['Plains']*4,['Trinisphere','Helm of Awakening','Helm of Awakening'],effect='spirits')
add('hybrid-floor-tax-reduce','Burning-Tree Emissary',3,['Forest','Mountain','Island'],['Trinisphere','Sphere of Resistance','Helm of Awakening'],effect='emissary')
for label,mods,mana in [('tax',['Sphere of Resistance'],1),('reduce',['Helm of Awakening'],0),('floor',['Trinisphere'],3)]:
    add('alt-gain-life-'+label,'Invigorate',mana,['Forest']+['Island']*3,mods,['Llanowar Elves'],effect='invigorate',choice='gain 3 life',enemyGain=3)
    add('alt-return-'+label,'Gush',mana,['Island']*5,mods,effect='draw-two',choice='return two Islands',returned=2)
    add('alt-sacrifice-'+label,'Fireblast',mana,['Mountain']*2+['Island']*3,mods,effect='fireblast',choice='sacrifice two Mountains',sacrificed=2)
    add('alt-exile-'+label,'Allosaurus Rider',mana,['Island']*3,mods,effect='creature',choice='exile two green',exiled=2)
    CASES['alt-exile-'+label]['hand']+=['Grizzly Bears','Llanowar Elves']
    add('mandatory-discard-'+label,'Lightning Axe',mana+1,['Mountain']+['Island']*3,mods,effect='kill',choice='discard a card',discarded=1)
    CASES['mandatory-discard-'+label]['hand']+=['Grizzly Bears']
    add('mandatory-sacrifice-'+label,'Bone Splinters',mana+1,['Swamp']+['Island']*3,mods,['Llanowar Elves'],effect='kill',sacrificed=1)
CASES['mandatory-discard-floor']['mana']=3
CASES['mandatory-sacrifice-floor']['mana']=3
for label,mods,mana in [('tax',['Sphere of Resistance'],5),('reduce',['Helm of Awakening'],3),('floor',['Trinisphere'],4)]:
    add('hybrid-phy-'+label,'Tamiyo, Compleated Sage',mana,['Forest','Island']+['Island']*4,mods,life=2,effect='tamiyo',reserve=2)
for label,mods,mana in [('tax',['Sphere of Resistance'],6),('reduce',['Helm of Awakening'],5),('floor',['Trinisphere'],5)]:
    add('colorless-hybrid-'+label,'Ulalek, Fused Atrocity',mana,['Wastes']*6,mods,effect='creature')
    add('colorless-hybrid-colored-'+label,'Ulalek, Fused Atrocity',mana,['Plains','Island','Swamp','Mountain','Forest','Island'],mods,effect='creature')
for label,mods,mana in [('tax',['Sphere of Resistance'],1),('reduce',['Helm of Awakening'],0),('floor',['Trinisphere'],3)]:
    add('free-cast-'+label,'Lightning Bolt',mana,['Island']*3,mods,['Omniscience'],effect='bolt',choice='without paying')
    add('flashback-'+label,'Deep Analysis',max(1,2+(1 if label=='tax' else -1 if label=='reduce' else 1)),['Island']*4,mods,life=3,effect='draw-two',fromGrave=True,grave=['Deep Analysis'])
    CASES['flashback-'+label]['hand']=[]
    CASES['flashback-'+label]['mana']=3 if label in {'tax','floor'} else 1
# Cancelling after reserving life and selecting nonmana resources must leave the
# original resources intact. These are separate actual native casting attempts.
for base in ['krrik-tax','delve-tax','defiler-tax']:
    CASES[base+'-cancel']=dict(CASES[base],cancelPayment=True)
for label,mods,mana in [('tax',['Sphere of Resistance'],4),('reduce',['Helm of Awakening'],2),('floor',['Trinisphere'],3)]:
    add('commander-'+label,'Isamaru, Hound of Konda',mana,['Plains']+['Island']*4,mods,effect='creature',commander='Isamaru, Hound of Konda',priorCasts=1)
    CASES['commander-'+label]['hand']=[]
for label,mods in [('tax',['Sphere of Resistance']),('reduce',['Helm of Awakening']),('floor',['Trinisphere'])]:
    add('activation-unaffected-'+label,'Icy Manipulator',1,['Island'],mods,['Icy Manipulator'],effect='tap',activated=True)
    CASES['activation-unaffected-'+label]['hand']=[]
    add('energy-unaffected-'+label,'Longtusk Cub',0,[],mods,['Longtusk Cub'],effect='energy',activated=True,energy=2)
    CASES['energy-unaffected-'+label]['hand']=[]
add('activation-reducer-floor','Icy Manipulator',1,['Island'],['Trinisphere'],['Icy Manipulator','Zirda, the Dawnwaker'],effect='tap',activated=True)
CASES['activation-reducer-floor']['hand']=[]
add('last-life-is-legal','Gut Shot',0,[],[],life=2,effect='terminal',reserve=2,initialLife=2,expectedLose=True)
add('phy-life-prohibited-reduced','Gut Shot',0,[],['Helm of Awakening'],['Angel of Jubilation'],effect='unavailable')
add('krrik-life-prohibited-reduced','Murder',0,[],['Helm of Awakening'],["K'rrik, Son of Yawgmoth",'Angel of Jubilation'],effect='unavailable')
add('xx-free-fixed-zero','Chalice of the Void',0,[],[],['Omniscience'],effect='chalice',choice='without paying',x=0)
add('xx-free-floor','Chalice of the Void',3,['Island']*3,['Trinisphere'],['Omniscience'],effect='chalice',choice='without paying',x=0)
for label,mods,extra in [('tax',['Sphere of Resistance'],1),('reduce',['Helm of Awakening'],-1),('floor',['Trinisphere'],0)]:
    add('emerge-'+label,'Elder Deep-Fiend',5+extra,['Island']*7,mods,['Grizzly Bears'],effect='creature',choice='emerge',sacrificed=1,permanentSpell=True)
    add('offering-'+label,'Patron of the Akki',4+extra,['Mountain']+['Island']*5,mods,['Goblin Piker'],effect='creature',choice='offering',sacrificed=1,permanentSpell=True)
    add('escape-'+label,'Woe Strider',5+extra,['Swamp']*2+['Island']*5,mods,effect='escape',fromGrave=True,grave=['Woe Strider']+['Island']*4,exiled=4,exiledNames=['Island'])
    CASES['escape-'+label]['hand']=[]
    add('kicker-'+label,'Burst Lightning',5+extra,['Mountain']+['Island']*5,mods,effect='burst-kicked',choice='kicker')
    add('prototype-'+label,'Phyrexian Fleshgorger',3+extra,['Swamp']*2+['Island']*2,mods,effect='prototype',choice='prototype')
    add('morph-'+label,'Willbender',3+extra,['Island']*5,mods,effect='morph',choice='morph|face down')
add('improvise-phy-mixed','Gut Shot',1,['Island'],['Trinisphere'],['Inspiring Statuary',"Tormod's Crypt"],life=2,effect='gut',reserve=2,tapSelect=2)
CASES['improvise-phy-reserved-cancel']=dict(CASES['improvise-phy-mixed'],field=['Inspiring Statuary',"Tormod's Crypt"],cancelPayment=True)
add('improvise-two-phy-floor','Dismember',0,[],['Trinisphere'],['Inspiring Statuary',"Tormod's Crypt",'Sol Ring'],life=4,effect='kill',reserve=4,tapSelect=3)
add('convoke-partial-cancel','Gather Courage',1,[],['Trinisphere'],['Llanowar Elves']*2,effect='buff',tapSelect=2,cancelPayment=True)
add('convoke-trigger-cancel','Gather Courage',2,[],['Trinisphere'],['Judge of Currents'],effect='buff',tapSelect=1,cancelPayment=True)
add('waterbend-partial-cancel','Ruthless Waterbender',1,[],['Trinisphere'],['Ruthless Waterbender',"Tormod's Crypt"],effect='pump-water',tapSelect=1,cancelPayment=True,activated=True)
CASES['waterbend-partial-cancel']['hand']=[]

def run(name):
    spec=CASES[name];folder=OUT/name/str(time.time_ns());folder.mkdir(parents=True)
    (folder/'driver.py').write_bytes(DRIVER_SOURCE);(folder/'fixture.java').write_bytes(FIXTURE_SOURCE)
    seat=spec.get('seat',2 if name.endswith('tax') else 1);cast=False;seen=False;before=None;reserved=0;cancelled=False;cast_id=None
    with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
        audit=folder/'payment-audit';audit.mkdir()
        start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=name,spec=spec,auditDir=str(audit),seed=4,startingLife=20,firstPlayerSeat=seat,decks=[DECK,DECK])),encoding='utf8')
        p=subprocess.Popen([str(RT/CFG['java']),'-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.CostInteractionFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        q=queue.Queue()
        def reader():
            try:
                for line in p.stdout:q.put(json.loads(line))
            finally:q.put(None)
        threading.Thread(target=reader,daemon=True).start()
        def snap(m,label):
            v=dict(m['views'][seat-1]);v.update(request=m['request'],awaitingInput=True);(folder/(label+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
        try:
            for step in range(220):
                m=q.get(timeout=40);assert m is not None,'Native process exited';trace.write(json.dumps(m)+'\n');trace.flush()
                if m['type']=='ended' and spec.get('expectedLose'):
                    assert cast and m['winnerSeat']==3-seat and m['views'][seat-1]['players'][seat-1]['life']==0
                    (folder/'ended.json').write_text(json.dumps(m,ensure_ascii=False,indent=2),encoding='utf8')
                    return dict(case=name,status='PASS',spec=spec,seat=seat,folder=str(folder.relative_to(ROOT)),nativeLossAfterLegalLifePayment=True)
                assert m['type'] not in {'fatal','ended','rejected'},m
                if m['type']!='request':continue
                r=m['request'];v=m['views'][seat-1];own=v['players'][seat-1];other=v['players'][2-seat];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];pick='ok'
                snap(m,'step-'+str(step));print(name,step,kind,[o.get('label') for o in opts if not o['id'].startswith('card:')],flush=True)
                spell=next((c for c in own['hand'] if c['name']==spec['spell']),None)
                entries=[s for s in v['stackItems'] if s['kind']==('ability' if spec.get('activated') else 'spell') and (s['source']['id']==cast_id or s['source']['name']==spec['spell'])]
                if entries and kind=='InputPassPriority':
                    seen=True;snap(m,'stack')
                    assert own['life']==spec.get('initialLife',20)-spec['lifePaid'],('stack life',own['life'],spec['lifePaid'])
                    proof=audit/(str(r['requestId'])+'.json');until=time.monotonic()+3
                    payments=None
                    while payments is None and time.monotonic()<until:
                        try:payments=json.loads(proof.read_text(encoding='utf8'))
                        except (OSError,json.JSONDecodeError):time.sleep(.01)
                    assert payments is not None,'Missing native payment audit'
                    assert next(a['mana'] for a in payments if (a['id']==cast_id or a['card']==spec['spell']) and a['kind']==('ability' if spec.get('activated') else 'spell'))==spec['mana'],('stack mana',payments,spec)
                    if 'x' in spec and spec['spell']=='Walking Ballista':assert entries[0]['xValue']==spec['x']
                if kind=='InputPassPriority' and r['seat']==seat and v['phase']=='MAIN1' and not v['stackItems']:
                    if cancelled:
                        assert own['life']==before['life']
                        for zone in ['hand','battlefield','graveyardCards','exileCards']:
                            assert {c['id'] for c in own[zone]}=={c['id'] for c in before[zone]},('cancel zone',zone)
                        assert [c['tapped'] for c in own['battlefield']]==[c['tapped'] for c in before['battlefield']]
                        snap(m,'cancelled');return dict(case=name,status='PASS',spec=spec,seat=seat,folder=str(folder.relative_to(ROOT)),cancelRestored=True)
                    if seen:
                        assert own['life']==spec.get('initialLife',20)-spec['lifePaid']
                        if not spec.get('sacrificed') and not spec.get('returned'):assert sum(c['tapped'] for c in own['battlefield'] if c['land'])==spec['mana']
                        effect=spec['effect'];field=own['battlefield']
                        if effect in {'kill','wipe'}:assert 'Grizzly Bears' in other['graveyard']
                        elif effect=='bolt':assert other['life']==17
                        elif effect=='gut':assert other['life']==19
                        elif effect=='burst-kicked':assert other['life']==16
                        elif effect=='fireblast':assert other['life']==16
                        elif effect in {'creature','emissary','astrolabe','ballista','seer'}:assert any(c['name']==spec['spell'] for c in field)
                        elif effect=='tamiyo':assert next(c for c in field if c['name']==spec['spell'])['loyalty']==3
                        elif effect=='spirits':assert sum(c.get('token',False) and 'Spirit' in c['currentType'] for c in field)==3
                        elif effect=='buff':assert next(c for c in field if c['name']=='Llanowar Elves')['power']==3
                        elif effect=='invigorate':assert next(c for c in field if c['name']=='Llanowar Elves')['power']==5 and other['life']==23
                        elif effect=='tap':assert next(c for c in other['battlefield'] if c['name']=='Grizzly Bears')['tapped']
                        elif effect=='energy':assert next(c for c in field if c['name']=='Longtusk Cub')['power']==3 and next((c['count'] for c in own['counters'] if c['kind']=='ENERGY'),0)==0
                        elif effect=='chalice':assert next(c for c in field if c['name']==spec['spell']).get('counters',[])==[]
                        elif effect=='escape':assert next(c for c in field if c['name']==spec['spell'])['power']==5
                        elif effect=='prototype':assert next(c for c in field if c['name']==spec['spell'])['power']==3
                        elif effect=='morph':assert next(c for c in field if c['id']==cast_id)['faceDown'] and next(c for c in field if c['id']==cast_id)['power']==2
                        elif effect=='pump-water':assert next(c for c in field if c['name']==spec['spell'])['power']==2
                        if effect in {'draw','astrolabe'}:assert own['libraryCount']==before['libraryCount']-1-(1 if spec.get('fromLibrary') else 0)
                        if effect=='ballista':assert next(c for c in field if c['name']==spec['spell'])['power']==spec['x']
                        if effect=='draw-two':
                            assert own['libraryCount']==before['libraryCount']-2
                            if spec.get('returned'):assert sum(c['name']=='Island' for c in own['hand'])==2
                        if spec.get('sacrificed'):assert len(own['graveyardCards'])==spec['sacrificed']+(0 if spec.get('permanentSpell') else 1)
                        if spec.get('discarded'):assert len(own['graveyardCards'])==spec['discarded']+1 and 'Grizzly Bears' in own['graveyard']
                        if spec.get('exiled'):assert len(own['exileCards'])==spec['exiled'] and {c['name'] for c in own['exileCards']}==set(spec.get('exiledNames',['Grizzly Bears','Llanowar Elves']))
                        if spec.get('delve'):assert len(own['exileCards'])==spec['delve'] and not own['graveyardCards'] or len(own['exileCards'])==spec['delve'] and [c['name'] for c in own['graveyardCards']]==[spec['spell']]
                        if spec.get('tapSelect'):assert sum(c['tapped'] for c in field if not c['land'])==spec['tapSelect']
                        snap(m,'resolved');return dict(case=name,status='PASS',spec=spec,paidMana=spec['mana'],paidLife=spec['lifePaid'],seat=seat,folder=str(folder.relative_to(ROOT)),steps=step)
                    if spec['effect']=='unavailable':assert spell and 'card:'+str(spell['id']) not in ids;return dict(case=name,status='PASS',spec=spec,seat=seat,folder=str(folder.relative_to(ROOT)),unavailable=True)
                    assert not cast,'Cancelled or failed native cast';before=json.loads(json.dumps(own));snap(m,'before-cast');cast=True
                    if spec.get('fromLibrary'):pick=next(o['id'] for o in opts if o['label'].startswith(spec['spell']+' [Library]'))
                    elif spec.get('fromGrave'):pick=next(o['id'] for o in opts if o['label'].startswith(spec['spell']+' [Graveyard]'))
                    elif spec.get('commander'):pick=next(o['id'] for o in opts if o['label'].startswith(spec['spell']+' [Command]'))
                    elif spec.get('activated'):pick='card:'+str(next(c['id'] for c in own['battlefield'] if c['name']==spec['spell']))
                    else:assert spell and 'card:'+str(spell['id']) in ids,('Not offered',spec,r);pick='card:'+str(spell['id'])
                    cast_id=int(pick.split(':')[1])
                elif kind=='ChooseNumber':
                    desired=spec.get('reserve',0)//2 if 'Phyrexian symbols' in r['message'] else spec.get('x',0)
                    pick='number-plus' if r['selectedValue']<desired else 'confirm-number'
                elif kind in {'InputSelectTargets','SelectStackTarget'}:
                    if 'ok' in ids:pick='ok'
                    elif r.get('sourceCard',{}).get('name')=='Defiler of Flesh':pick='card:'+str(next(c['id'] for c in own['battlefield'] if c['creature']))
                    elif spec['effect'] in {'gut','bolt','fireblast','burst-kicked'}:pick='player:'+str(other['targetId'])
                    elif spec['effect'] in {'buff','invigorate'}:pick='card:'+str(next((c for c in own['battlefield'] if c['name']=='Llanowar Elves'),next(c for c in own['battlefield'] if c['creature']))['id'])
                    elif spec['effect']=='draw-two':pick='player:'+str(own['targetId'])
                    else:pick='card:'+str(next(c for c in other['battlefield'] if c['name']=='Grizzly Bears')['id'])
                elif kind=='InputSelectCardsForConvokeOrImprovise':
                    pick='ok' if r['selectedCount']>=spec['tapSelect'] else next(o['id'] for o in opts if o.get('card') and not o.get('selected'))
                elif 'PayMana' in kind:
                    snap(m,'payment')
                    reserved=max(reserved,r.get('reservedLifePayment',0))
                    if reserved<spec.get('reserve',0):pick='player:'+str(own['targetId']);reserved+=2
                    elif spec.get('cancelPayment'):pick='cancel';cancelled=True
                    elif 'ok' in ids:pick='ok' # Native auto-pay, not a test-side affordability computation.
                    else:pick=next(o['id'] for o in opts if o.get('label','').endswith('[Battlefield]') and o['id'].startswith('card:'))
                elif kind.startswith('InputSelectCards'):
                    needed=spec.get('sacrificed',0) if 'sacrifice' in r['message'].lower() else spec.get('exiled',0) if 'exile' in r['message'].lower() else 0
                    if 'ok' in ids and r.get('selectedCount',0)>=needed:pick='ok'
                    else:
                        candidates=[o for o in opts if o.get('card') and not o.get('selected')]
                        if spec.get('discarded'):candidates.sort(key=lambda o:o['card']['name']!='Grizzly Bears')
                        pick=candidates[0]['id']
                elif kind=='InputSelectEntitiesFromList':
                    needed=spec.get('exiled',r.get('maxCount',0))
                    pick='ok' if r.get('selectedCount',0)>=needed else next(o['id'] for o in opts if o.get('card') and not o.get('selected'))
                elif kind in {'Option','dialog'}:
                    pattern=spec.get('choice')
                    matches=[o for o in opts if pattern and re.search(pattern,o['label'],re.I)]
                    if 'Phyrexian symbols' in r['message']:pick=next(o['id'] for o in opts if o['label']==str(spec.get('reserve',0)//2))
                    elif spec.get('delve') and any(o['label']==str(spec['delve']) for o in opts):pick=next(o['id'] for o in opts if o['label']==str(spec['delve']))
                    elif spec.get('delve') and 'Exile which card' in r['message']:pick=next(o['id'] for o in opts if o.get('card'))
                    elif matches:pick=matches[0]['id']
                    elif r['message']=='getAbilityToPlay':pick=next(o['id'] for o in opts if o['label'].startswith(spec['spell']+' -'))
                    elif spec['spell']=='Opt' and 'scry' in r['message'].lower():pick='no' if 'no' in ids else ids[0]
                    elif len([i for i in ids if i not in {'cancel','no'}])==1:pick=next(i for i in ids if i not in {'cancel','no'})
                    else:raise AssertionError(('Unhandled native choice',r))
                elif kind=='Scry':pick='confirm-scry' if 'confirm-scry' in ids else next(i for i in ids if i.startswith('scry-top:'))
                elif kind=='RevealCards':pick='confirm-cards'
                elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in ids else next(i for i in ids if i.startswith('order:'))
                elif kind=='InputMulligan':pick='keep'
                elif kind=='ChooseOptions':
                    pick='confirm-cards' if r.get('selectedCount',0)>=spec.get('choiceCount',1) else next(o['id'] for o in opts if o['id'].startswith('select:') and not o.get('selected') and re.search(spec.get('choice','.*'),o['label'],re.I))
                assert pick in ids,(name,kind,pick,ids,r)
                response=dict(type='respond',session=name,seat=r['seat'],requestId=r['requestId'],option=pick);trace.write(json.dumps(dict(response=response))+'\n');trace.flush();p.stdin.write(json.dumps(response)+'\n');p.stdin.flush()
            raise AssertionError('Step limit')
        finally:
            if p.poll() is None:p.kill();p.wait()

if __name__=='__main__':
    import argparse,concurrent.futures
    parser=argparse.ArgumentParser();parser.add_argument('cases',nargs='*');parser.add_argument('--workers',type=int,default=2);args=parser.parse_args()
    def execute(name):
        assert name in CASES,name
        try:result=run(name)
        except Exception as e:result=dict(case=name,status='FAIL',error=repr(e),traceback=traceback.format_exc());print(result['traceback'],flush=True)
        result.update(result=result['status']+' '+name,runtimeHash=CFG['runtimeHash'],sourceHash=DRIVER_HASH,fixtureHash=FIXTURE_HASH)
        (OUT/(name+'-result.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8');print(result['status']+' '+name,flush=True);return result
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as executor:results=list(executor.map(execute,args.cases or CASES))
    (OUT/('run-'+str(time.time_ns())+'.json')).write_text(json.dumps(dict(runtimeHash=CFG['runtimeHash'],sourceHash=DRIVER_HASH,fixtureHash=FIXTURE_HASH,results=results),ensure_ascii=False,indent=2),encoding='utf8')
    sys.exit(any(r['status']!='PASS' for r in results))
