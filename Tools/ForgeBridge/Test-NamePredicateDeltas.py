"""N29 actual native name-predicate deltas. Initial positions only; observers are read-only.
PASS_BOUNDED is printed only after all original operations finish without violations.
"""
from pathlib import Path
import hashlib, json, os, queue, subprocess, tempfile, threading, time, traceback, sys, zipfile

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / '.tools/name-predicate-tail-1011'
RT = ROOT / 'ForgeRuntime'
EFFECTIVE_ASSETS=Path(os.environ.get('MTGB_CHOICE_ASSETS',str(RT/'assets')))
OVERRIDE=Path(os.environ['MTGB_NAME_OVERRIDE']) if os.environ.get('MTGB_NAME_OVERRIDE') else None
runtime = json.loads((RT / 'runtime.json').read_text('utf-8-sig'))

def write(p, value):
    p.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf8')

def ref(p):
    return dict(path=p.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(p.read_bytes()).hexdigest())

def cards(n):
    return list({c['id']: c for p in n['players'] for z in p['zones'].values() for c in z}.values())

def find(n, name, owner=1, zone=None):
    return next(c for c in cards(n) if c['originalName'] == name and c['owner'] == owner and (zone is None or c['zone'] == zone))

def byid(n, ident):
    return next(c for c in cards(n) if c['id'] == ident)

def wirecards(m, ident):
    found = []
    def walk(v, view):
        if isinstance(v, dict):
            if v.get('id') == ident and 'basePower' in v: found.append(dict(viewSeat=view, card=v))
            for x in v.values(): walk(x, view)
        elif isinstance(v, list):
            for x in v: walk(x, view)
    for i, view in enumerate(m['views']): walk(view, i + 1)
    return found

def run(case):
    plan = json.loads((BASE / 'cases.json').read_text('utf8'))[case]
    folder = BASE / 'native' / case / str(time.time_ns()); folder.mkdir(parents=True)
    audit = folder / 'audit'; audit.mkdir(); classes = folder / 'classes'; classes.mkdir()
    fixtures = [ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java', ROOT/'Tools/ForgeBridge/tests/NamePredicateFixture.java']
    sources = fixtures + [Path(__file__), BASE/'cases.json', RT/'runtime.json', ROOT/'Tools/ForgeBridge/java/mtgb/bridge/ForgeBridge.java', ROOT/'Tools/ForgeBridge/java/mtgb/bridge/ForgeCharacteristicDetails.java', RT/'source/native-patches/forge/game/card/Card.java']
    upstream = ROOT / '.tools/forge-proof/upstream/forge-game/src/main/java/forge/game'
    sources += [upstream/'ability/effects/ChooseCardNameEffect.java']
    if OVERRIDE:sources += [OVERRIDE/'source/forge/game/ability/effects/PlayEffect.java']
    sources += [RT/'assets/res/cardsfolder'/p for p in plan['sourceScripts']]
    frozen = []
    for i, path in enumerate(sources):
        dest = folder/'frozen'/str(i)/path.name; dest.parent.mkdir(parents=True, exist_ok=True); dest.write_bytes(path.read_bytes())
        frozen.append(dict(sourcePath=path.relative_to(ROOT).as_posix(), sha256=ref(path)['sha256'], frozen=ref(dest)))
    frozen_classes = []
    for relative in ['mtgb/bridge/ForgeBridge.class','mtgb/bridge/ForgeCharacteristicDetails.class','forge/game/card/Card.class','forge/game/ability/effects/ChooseCardNameEffect.class']:
        path = RT/'classes'/relative
        dest = folder/'frozen-runtime-classes'/relative; dest.parent.mkdir(parents=True, exist_ok=True)
        if path.exists():
            dest.write_bytes(path.read_bytes()); frozen_classes.append(dict(sourcePath=path.relative_to(ROOT).as_posix(), sha256=ref(path)['sha256'], frozen=ref(dest)))
        else:
            matches=[]
            for jar in (RT/'lib').glob('*.jar'):
                with zipfile.ZipFile(jar) as archive:
                    if relative in archive.namelist(): matches.append((jar, archive.read(relative)))
            assert len(matches)==1, ('Exact native class container',relative,len(matches))
            jar,data=matches[0];dest.write_bytes(data);frozen_classes.append(dict(container=ref(jar),entry=relative,sha256=hashlib.sha256(data).hexdigest(),frozen=ref(dest)))
    write(folder/'source-identities.json', dict(runtimeHash=runtime['runtimeHash'], frozenSources=frozen, runtimeClasses=frozen_classes))
    if OVERRIDE:
        overrideClass=OVERRIDE/'classes/forge/game/ability/effects/PlayEffect.class'
        saved=folder/'override/PlayEffect.class';saved.parent.mkdir(parents=True);saved.write_bytes(overrideClass.read_bytes())
        write(folder/'override-identity.json',dict(status='ISOLATED_TEST_OVERRIDE',source=ref(OVERRIDE/'source/forge/game/ability/effects/PlayEffect.java'),originalClass=ref(overrideClass),frozenClass=ref(saved)))
    checks=[]; violations=[]; paid=[]; requests=[]; endpoints=[]; original={}; p=None; naming=[]
    index=0; stage='next'; source=None; targeted=[]; paidSeen=False; before=None; decisions=[]; picked={}; powerSeen=[]
    def check(ok, message):
        checks.append(message)
        if not ok: violations.append(message)
    def require(ok, message):
        check(ok, message)
        if not ok: raise AssertionError(message)
    def observe(rid):
        deadline=time.monotonic()+8
        while time.monotonic()<deadline:
            try: return json.loads((audit/(str(rid)+'.json')).read_text('utf8'))
            except (FileNotFoundError,PermissionError,json.JSONDecodeError): time.sleep(.01)
        raise AssertionError('Missing read-only pending observation '+str(rid))
    def freeze(m,n,label):
        write(folder/(label+'-wire.json'),m); write(folder/(label+'-observation.json'),n)
    def target(spec,n,m):
        if spec[0]=='player':
            player=m['views'][0]['players'][spec[1]-1]
            return 'player:'+str(player['targetId'])
        ident=original.get('vituForest') if spec[1]=='Forest' and 'vituForest' in original else find(n,spec[1],spec[0],'Battlefield')['id']
        if spec[1]=='Forest':original['vituForest']=ident
        return 'card:'+str(ident)
    def endpoint(m,n,op):
        nonlocal index,stage
        freeze(m,n,'after-'+str(index));check(not n['stack'],'Empty native stack '+str(index))
        check(m['request']['seat']==m['request']['playerSeat'],'Physical/logical restored actor '+str(index))
        result=op['result']
        if op.get('action','cast') in ['cast','activate']:check(paidSeen,'Real committed source/payment '+str(index))
        current={c['id']:c for c in cards(n)}; initial={c['id']:c for c in cards(before)}
        result=op['result']
        sourceCard=next((c for c in cards(n) if c['id']==original.get(op.get('source'))),None)
        if result=='harnessed':check(sourceCard['harnessed'],'Actual original Mind Stone harnessed after six mana and tap')
        elif result=='harnessed-child':
            bear=byid(n,original['Grizzly Bears']);check(bear['zone']=='Battlefield' and bear['timestamp']!=initial[original['Grizzly Bears']]['timestamp'],'Actual natural end step blink returns original Grizzly with new game timestamp')
        elif result=='case-unsolved':
            caseCard=byid(n,original['Case of the Filched Falcon']);check(not caseCard['solved'],'Actual entered Case initially unsolved')
            check(any(c['zone']=='Battlefield' and c['token'] and 'Clue' in c['type'] for c in cards(n)),'Actual Case ETB investigate child creates Clue')
        elif result=='case-solved':check(byid(n,original['Case of the Filched Falcon'])['solved'],'Natural own end step solves exact original Case')
        elif result=='case-ready':check(byid(n,original['Case of the Filched Falcon'])['solved'],'Solved state survives real turn boundary')
        elif result=='case-action':
            check(byid(n,original['Case of the Filched Falcon'])['zone']=='Graveyard','Actual solved action sacrifices original Case')
            artifact=byid(n,original['Sol Ring']);check(artifact['power']==4 and artifact['toughness']==4 and artifact['plusCounters']==4 and 'Flying' in artifact['keywords'] and 'Bird' in artifact['type'],'Actual solved action gives original artifact four counters, base0/0, Bird and flying')
        elif result=='suspected':
            bear=byid(n,original['Grizzly Bears']);check(bear['suspected'] and not bear['canBlock'] and 'Menace' in bear['keywords'],'Actual suspected original creature has menace and cannot block')
        elif result=='unsuspected':
            bear=byid(n,original['Grizzly Bears']);check(not bear['suspected'] and bear['canBlock'] and 'Menace' not in bear['keywords'],'Actual un-suspect removes only suspected rules and restores blocking')
            check('Hexproof' in bear['keywords'],'Independent Alibi granted hexproof retained')
        elif result=='saddled':
            mount=byid(n,original['Trained Arynx']);check(mount['saddled'] and mount['saddleCount']==1,'Actual original Mount becomes saddled exactly once')
        elif result=='saddle-expired':
            mount=byid(n,original['Trained Arynx']);check(not mount['saddled'] and mount['saddleCount']==0,'Natural cleanup resets saddled flag and count')
        elif result=='plotted':
            bear=byid(n,original['Grizzly Bears']);check(bear['plotted'] and bear['zone']=='Exile','Actual hand card becomes plotted in Exile')
            check('card:'+str(bear['id']) not in [o['id'] for o in m['request']['options'] if not o.get('disabled')],'Plotted card unavailable on same turn')
        elif result=='plot-ready':
            bear=byid(n,original['Grizzly Bears']);check(bear['plotted'] and bear['zone']=='Exile' and 'card:'+str(bear['id']) in [o['id'] for o in m['request']['options'] if not o.get('disabled')],'Same plotted card offered at actual later sorcery timing')
        elif result=='plot-cast':
            bear=byid(n,original['Grizzly Bears']);check(bear['zone']=='Battlefield' and not bear['plotted'],'Actual free cast resolves original plotted card and clears plotting')
        elif result=='alpine-named':
            card=byid(n,original['Alpine Moon']);check(card['chosenName']=='Urborg, Tomb of Yawgmoth','Actual nonbasic land name stored on original Alpine Moon')
            actual=next(x for x in naming if x['operation']==index);check('Plains' not in actual['names'] and 'Grizzly Bears' not in actual['names'] and card['chosenName'] in actual['names'],'Actual native nonbasic+Land candidate boundary')
        elif result=='council-named':
            card=byid(n,original['Council of the Absolute']);check(card['chosenName']=='Opt','Actual original Council stores named instant')
            actual=next(x for x in naming if x['operation']==index);check('Grizzly Bears' not in actual['names'] and 'Plains' not in actual['names'] and 'Opt' in actual['names'],'Actual native nonCreature AND nonLand boundary')
        elif result=='equipment-entered':check(byid(n,original['Killer Cosplay'])['zone']=='Battlefield','Actual paid equipment source enters')
        elif result=='equipment-copy':
            card=byid(n,original['Silvercoat Lion']);check(card['name']=='Whitemane Lion' and card['zone']=='Battlefield','Native name-selected clone affects original equipped object')
            actual=next(x for x in naming if x['operation']==index);check('Whitemane Lion' in actual['names'] and 'Grizzly Bears' not in actual['names'] and 'Savannah Lions' not in actual['names'],'Actual exact Equipped mana cost, excludes same mana value wrong color and different cost')
            check(byid(n,original['Killer Cosplay'])['equipping']==card['id'],'Actual native equipment relation identity')
        elif result=='equipment-unattached':
            card=byid(n,original['Silvercoat Lion']);check(card['name']=='Silvercoat Lion' and card['zone']=='Battlefield','Actual source destroyed, UntilUnattached clone restores original creature')
        elif result=='garth-entered':check(byid(n,original['Garth One-Eye'])['zone']=='Battlefield','Actual five-color Garth cast')
        elif result=='garth-name':
            card=byid(n,original['Garth One-Eye']);check(card['tapped'],'Actual source pays tap cost')
            actual=next((x for x in naming if x['operation']==index),None);check(op['nameChoice'] in card['namedCards'],'Actual newly selected Garth name persists')
            if actual:check(set(actual['names'])==set(op['expectedNames']),'Actual original fixed list minus previously chosen names')
            else:check(op['expectedNames']==[op['nameChoice']] and card['chosenName']==op['nameChoice'],'Native sole remaining candidate automatically selected without a prompt')
        elif result=='garth-empty':
            card=byid(n,original['Garth One-Eye']);check(set(card['namedCards'])==set(op['allNames']) and len(card['namedCards'])==6,'No seventh or duplicate name added when all6 exhausted')
            check(not any(x['operation']==index for x in naming),'Exhausted list has no unanswerable name prompt')
            check(not any(x.get('operation')==index and x.get('unexpectedCopy') for x in decisions),'Exhausted list must not offer old chosen-name copy')
        elif result=='untapped':check(not byid(n,original['Garth One-Eye'])['tapped'],'Actual paid Twiddle untaps original Garth')
        elif result=='garth-ready':check(not byid(n,original['Garth One-Eye'])['sick'],'Natural later own turn clears actual Garth summoning sickness')
        elif result=='svega-entered':check(byid(n,original['Svega, the Unconventional'])['loyalty']==3,'Actual printed loyalty on paid Svega entry')
        elif result=='or-cmc':
            actual=next(x for x in naming if x['operation']==index);check('Liliana, the Last Hope' in actual['names'] and 'Jace Beleren' not in actual['names'] and 'Liliana of the Dark Realms' not in actual['names'],'Actual OR of five planeswalker subtypes with calculated3 mana value')
            check(any(c['name']=='Liliana, the Last Hope' and c['zone']=='Battlefield' for c in cards(n)),'Actual native chosen-name free copy cast resolves')
        elif result=='random-list':
            choices=[x.replace(';',',') for x in op['names'].split(',')];tokens=[c for c in cards(n) if c['token'] and c['zone']=='Battlefield' and c['id'] not in initial]
            check(len(tokens)==1 and tokens[0]['name'] in choices,'Actual fixed-list random name creates exactly one native legal token')
            check(not any(x['operation']==index for x in naming),'Actual AtRandom path has no fabricated name selection UI')
            check(not byid(n,source)['namedCards'],'Actual random source clears names after child')
        elif result=='nissa-exhaustion':
            pool=json.loads((audit/'nissa-pool.json').read_text('utf8'))['names'];tokens=[c for c in cards(n) if c['token'] and c['planeswalker'] and c['zone']=='Battlefield' and c['id'] not in initial]
            check(len(tokens)==len(pool) and set(c['name'] for c in tokens)==set(pool),'Actual X above whole Nissa pool creates exactly all unique eligible names, no duplicate or stale twelfth')
            check(all(c['loyalty']>0 for c in tokens),'Actual native planeswalker copies have loyalty')
            check(not byid(n,source)['namedCards'],'Actual repeated random names cleared after token creation')
            manaObjects={m['identity']:m for e in n['events'] if e['event']=='manaPool' and e['mode']=='Added' for p in e['pools'] if p['seat']==1 for m in p['mana']}
            check(len(manaObjects)==op['mana'] and sum(e['event']=='manaPool' and e['mode']=='Removed' for e in n['events'])==op['mana'],'Actual triggered XXXGG payment consumes exactly38 original mana identities')
            paid.append(dict(operation=index,name=op['source'],sourceId=source,mana=len(manaObjects),objects=list(manaObjects.values()),requestId=m['request']['requestId'],api='ImmediateTrigger',kind='actual-pool-payment-events',spell=False));write(folder/'paid.json',paid)
        elif result=='calculated-m-x':
            added=[c for c in cards(n) if c['id'] not in initial and c['creature'] and not c['token']]
            check(len(added)==4 and sorted(c['manaValue'] for c in added)==[1,2,3,3],'Actual Anina X3 and Ornate M1/M2/M3 conjure four native creatures with exact mana values')
            check(not byid(n,source)['namedCards'] and not byid(n,original['Anina, Natural Parallelist'])['namedCards'],'Both calculated naming sources clean their names')
        elif result=='gallery-entered':
            check(find(n,'Gallery of Legends')['zone']=='Battlefield','Actual paid Deadbeat ETB opens registered original Attraction')
        elif result=='gallery-visit':
            actual=next(x for x in naming if x['operation']==index);check('Ayula, Queen Among Bears' in actual['names'] and 'Grizzly Bears' not in actual['names'] and 'Isamaru, Hound of Konda' not in actual['names'],'Actual Visit legendary-creature AND calculated imprinted mana value2 candidates')
            token=next(c for c in cards(n) if c['token'] and c['name']=='Ayula, Queen Among Bears');check('Haste' in token['keywords'] and token['zone']=='Battlefield','Actual Visit creates chosen legendary token with haste')
            gallery=find(n,'Gallery of Legends');check(not gallery['namedCards'] and not gallery['imprinted'],'Actual Visit cleans original name and imprint')
        elif result=='imprinted-cost':
            revealed=[c for x in decisions if x.get('operation')==index for c in x.get('inputCards',x.get('reveal',[]))]
            check(len({c['id'] for c in revealed})==1 and all(c.get('manaCost')=='{1}{W}' for c in revealed),'Actual combat imprint random name preserves exact1W source mana cost')
            check(any(e['event']=='damage' and e['source']==source and e['combat'] and e['amount']==2 for e in n['events']),'Actual original Silvercoat combat damage causes Vanguard naming chain')
            avatar=find(n,'Maelstrom Archangel Avatar');check(not avatar['namedCards'] and not avatar['imprinted'],'Actual Vanguard random name/imprint cleanup')
            check(len([c for c in cards(n) if 'Land' in c['type'] and c['owner']==1 and c['tapped']])-len([c for c in cards(before) if 'Land' in c['type'] and c['owner']==1 and c['tapped']])==3,'Actual native optional3 payment taps exactly three original lands')
        else:raise AssertionError('Undefined result assertion '+result)
        if op.get('finalZone'):check(byid(n,source)['zone']==op['finalZone'],'Real source final zone '+result)
        endpoints.append(dict(operation=index,result=result,requestId=m['request']['requestId'],seat=m['request']['seat'],phase=n['phase'],turn=n['turn'],wire=ref(folder/('after-'+str(index)+'-wire.json')),observation=ref(folder/('after-'+str(index)+'-observation.json'))));write(folder/'endpoints.json',endpoints)
        index+=1;stage='next'
    try:
        compiler=Path(os.environ.get('MTGB_JAVAC','C:/Program Files/Java/jdk-17/bin/javac.exe'))
        with (folder/'compile.log').open('w',encoding='utf8') as log:subprocess.run([str(compiler),'-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*map(str,fixtures)],check=True,stdout=log,stderr=subprocess.STDOUT,timeout=60)
        write(folder/'fixture-classes.json',[ref(x) for x in sorted(classes.rglob('*.class'))])
        with tempfile.TemporaryDirectory(prefix='profile-',dir=folder,ignore_cleanup_errors=True) as temp,(folder/'stderr.log').open('w',encoding='utf8') as err,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
            deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
            cfg=dict(session='name-predicate-'+case,seed=4,startingLife=30,firstPlayerSeat=1,decks=[deck,deck],positions=plan['positions'],auditDir=str(audit));write(folder/'start.json',cfg)
            for key in ['initialLoyalty','initialNames','Attractions','Avatar','catalogAudit']:
                if key in plan:cfg[key]=plan[key]
            write(folder/'start.json',cfg)
            nativePaths=[classes,*([OVERRIDE/'classes'] if OVERRIDE else []),RT/'classes',RT/'lib/*']
            p=subprocess.Popen([str(RT/runtime['java']),'-Xmx512m','-XX:ActiveProcessorCount=3','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,nativePaths)),'mtgb.bridge.NamePredicateFixture',str(EFFECTIVE_ASSETS),str(folder/'start.json')],cwd=EFFECTIVE_ASSETS,env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
            q=queue.Queue()
            def reader():
                try:
                    for line in p.stdout:q.put(json.loads(line))
                except Exception as e:q.put(e)
                finally:q.put(None)
            threading.Thread(target=reader,daemon=True).start();seen=set();deadline=time.monotonic()+500
            for step in range(1200):
                require(time.monotonic()<deadline,'Bounded native deadline');m=q.get(timeout=45)
                if isinstance(m,Exception):raise m
                require(m is not None,'Native process live');trace.write(json.dumps(m)+'\n');trace.flush();require(m['type'] not in ['fatal','ended','rejected'],('Native envelope',m['type'],m.get('error')))
                if m['type']!='request':continue
                r=m['request'];rid=r['requestId']
                if rid in seen:continue
                seen.add(rid);opts=r['options'];enabled=[o['id'] for o in opts if not o.get('disabled')];kind=r['kind'];view=m['views'][0];n=None if 'Mulligan' in kind else observe(rid);pick='ok';op=plan['operations'][index]
                requests.append(dict(id=rid,kind=kind,seat=r['seat'],playerSeat=r['playerSeat'],stage=stage,operation=index))
                if n:freeze(m,n,'request-'+str(rid))
                if stage=='resolve' and not paidSeen:
                    ability=next((x for x in n['stack'] if x['source']==source and not x['trigger']),None)
                    if ability:
                        check(len(ability['payingMana'])==op['mana'],'Actual paid mana count '+str(index));check(ability['actor']==1,'Actual source paying actor '+str(index))
                        paid.append(dict(operation=index,name=op['source'],sourceId=source,mana=len(ability['payingMana']),objects=ability['payingMana'],requestId=rid,api=ability['api'],spell=ability['spell'],targetCards=ability['targetCards'],targetPlayers=ability['targetPlayers']));write(folder/'paid.json',paid);freeze(m,n,'paid-'+str(index));paidSeen=True
                if kind=='InputPassPriority':
                    if stage=='resolve' and not n['stack'] and r['seat']==1 and (not op.get('endPhase') or view['phase']==op['endPhase']):
                        endpoint(m,n,op)
                        if index==len(plan['operations']):break
                        op=plan['operations'][index]
                    if stage=='wait' and not n['stack'] and view['phase']==op['endPhase'] and n['turn']>=op['minTurn'] and view['turnSeat']==op['endTurnSeat'] and (not op.get('untilToken') or any(c['token'] and c['name']==op['untilToken'] and c['zone']=='Battlefield' for c in cards(n))):
                        endpoint(m,n,op)
                        if index==len(plan['operations']):break
                        op=plan['operations'][index]
                    if stage=='next' and not n['stack'] and view['turnSeat']==op.get('turnSeat',1) and r['seat']==1 and (op.get('action')=='wait' or view['phase']==op.get('phase','MAIN1')):
                        before=n;targeted=[];paidSeen=False;picked={};powerSeen=[];source=None if op.get('action') in ['observe','wait'] else find(n,op['source'],1,'Battlefield' if op.get('action')=='activate' else op.get('origin','Hand'))['id'];freeze(m,n,'before-'+str(index))
                        if not original:original.update({c['originalName']:c['id'] for c in cards(n) if c['originalName'] not in ['Plains','Island','Swamp','Mountain','Forest']})
                        if op.get('action')=='observe':
                            endpoint(m,n,op)
                            if index==len(plan['operations']):break
                        elif op.get('action')=='wait':stage='wait'
                        else:pick='card:'+str(source);stage='resolve'
                elif 'Mulligan' in kind:pick='keep' if 'keep' in enabled else 'ok'
                elif 'PayMana' in kind:pick='ok'
                elif kind=='InputAttack' and op.get('action')=='attack':
                    if stage=='next':
                        before=n;targeted=[];paidSeen=False;picked={};source=find(n,op['source'],1,'Battlefield')['id'];freeze(m,n,'before-'+str(index));original.update({c['originalName']:c['id'] for c in cards(n) if c['originalName'] not in ['Plains','Island','Swamp','Mountain','Forest']});stage='attack'
                    assigned=any(o.get('attackerId')==source and o['id'].startswith('attack-remove:') for o in opts)
                    pick='ok' if assigned else next(o['id'] for o in opts if o['id'] in enabled and o.get('attackerId')==source and o.get('defenderId')==view['players'][1]['targetId'])
                    if pick=='ok':stage='resolve'
                elif kind=='InputSelectTargets':
                    todo=[target(x,n,m) for x in op.get('targets',[]) if target(x,n,m) not in targeted]
                    if todo:pick=todo[0];targeted.append(pick)
                    elif op.get('pickAny') and r.get('remainingCount',0)>0:pick=next(o['id'] for o in opts if o['id'] in enabled and o['id'].startswith(('card:','player:')) and not o.get('selected'))
                    else:pick='ok'
                elif r.get('selectionPurpose')=='cardName':
                    names=[o['cardName'] for o in opts if o['id'] in enabled and o.get('cardName')]
                    chosen=next(o for o in opts if o['id'] in enabled and o.get('cardName')==op['nameChoice']);pick=chosen['id']
                    naming.append(dict(operation=index,requestId=rid,actor=r['playerSeat'],names=names,chosen=chosen['cardName'],option=pick));write(folder/'name-proof.json',naming)
                elif kind=='ChooseNumber':
                    require(op.get('x') is not None and r['minValue']<=op['x']<=r['maxValue'],'Planned X within actual native bounds')
                    pick='number-plus' if r['selectedValue']<op['x'] else 'number-minus' if r['selectedValue']>op['x'] else 'confirm-number'
                elif kind=='InputConfirm' and op.get('source')=='Twiddle' and r['message'].startswith('Tap or untap '):pick=next(o['id'] for o in opts if o['id'] in enabled and o['label']=='Untap')
                elif kind=='dialog' and op.get('x') is not None and any(o['label'].strip()==str(op['x']) for o in opts):pick=next(o['id'] for o in opts if o['id'] in enabled and o['label'].strip()==str(op['x']))
                elif op.get('declinePlay') and (kind=='Confirm' or kind=='InputConfirm' or kind=='dialog') and 'cancel' in enabled:
                    if op['result']=='garth-empty' and 'Do you want to play ' in r.get('message',''):
                        decisions.append(dict(operation=index,unexpectedCopy=True,requestId=rid,message=r['message'],previewCards=r.get('previewCards')));write(folder/'decisions.json',decisions)
                    pick='cancel'
                elif kind in ['dialog','SelectCards','InputSelectEntities','InputSelectEntitiesFromList','ChooseCards']:
                    optsCard=[o for o in opts if o['id'] in enabled and o.get('card') and o['card'].get('name')]
                    seat=r.get('playerSeat',r['seat']);chosen=picked.setdefault(seat,[])
                    known={c['id']:c for c in cards(n)};cand=[dict(id=o['card']['id'],name=o['card']['name'],controller=known.get(o['card']['id'],{}).get('controller'),power=known.get(o['card']['id'],{}).get('power')) for o in optsCard]
                    info=dict(requestId=rid,seat=seat,kind=kind,message=r.get('message'),candidates=cand,inputCards=[o['card'] for o in optsCard],enabled=enabled,operation=index)
                    if optsCard:
                        wanted=[x for x in op.get('pickCards',{}).get(str(seat),[]) if x not in chosen]
                        matching=[o for name in wanted for o in optsCard if o['card']['name']==name]
                        if r.get('selectedCount',0)>0 and 'ok' in enabled:pick='ok'
                        elif matching:pick=matching[0]['id'];chosen.append(matching[0]['card']['name'])
                        elif op.get('pickAny') and not chosen:pick=optsCard[0]['id'];chosen.append(optsCard[0]['card']['name'])
                        elif 'confirm-cards' in enabled:pick='confirm-cards'
                        elif 'ok' in enabled:pick='ok'
                        elif 'cancel' in enabled:pick='cancel'
                        else:raise AssertionError(('No actual planned card or confirmation',r,chosen))
                    elif kind=='dialog':
                        matching=[o for o in opts if o['id'] in enabled and op.get('ability','').lower() in o['label'].lower()]
                        require(bool(op.get('ability')) and len(matching)==1,('Exact actual ability option',r,op));pick=matching[0]['id']
                    elif 'ok' in enabled:pick='ok'
                    elif 'confirm-cards' in enabled:pick='confirm-cards'
                    else:raise AssertionError(('Unexpected chooser confirmation',r))
                    decisions.append(info);write(folder/'decisions.json',decisions)
                elif kind in ['InputAttack','InputBlock','InputConfirm']:pick='ok'
                elif kind=='OrderCards':
                    if 'confirm-cards' in enabled and r.get('selectedCount',0)>=r.get('minCount',1):pick='confirm-cards'
                    else:pick=next(o['id'] for o in opts if o['id'] in enabled and o.get('card') and 'Jace' in o['card']['name'])
                elif kind=='RevealCards':
                    decisions.append(dict(operation=index,requestId=rid,reveal=r.get('previewCards',[])));write(folder/'decisions.json',decisions);pick='confirm-cards'
                elif kind=='OrderAbilities':pick='confirm-order' if 'confirm-order' in enabled else next(x for x in enabled if x.startswith('order:'))
                else:raise AssertionError(('Unhandled actual name request',r))
                require(pick in enabled,('Actual enabled input',kind,pick));response=dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=rid,option=pick);trace.write(json.dumps(response)+'\n');trace.flush();p.stdin.write(json.dumps(response)+'\n');p.stdin.flush();print(case,index,kind,pick,flush=True)
            else:raise AssertionError('Native request step limit')
        status='PASS_BOUNDED' if not violations else 'FAIL_BOUNDED_AUDIT_REQUIRED'
    except Exception:
        status='FAIL';error=traceback.format_exc();print(error,flush=True)
    finally:
        if p is not None and p.poll() is None:p.kill();p.wait()
    result=dict(status=status,case=case,folder=folder.relative_to(ROOT).as_posix(),checks=len(checks),assertions=checks,violations=violations,paid=paid,endpoints=endpoints,requests=requests,runtimeHash=runtime['runtimeHash'],effectiveAssets=EFFECTIVE_ASSETS.relative_to(ROOT).as_posix(),originalIds=original,decisions=decisions,frozenSources=frozen,runtimeClasses=frozen_classes)
    if status=='FAIL':result['error']=error
    write(folder/'result.json',result);write(BASE/(case+'-latest.json'),result);print(status,case,folder,flush=True);return result

if __name__=='__main__':
    for case in sys.argv[1:]:
        result=run(case)
        if result['status']!='PASS_BOUNDED' or result.get('violations') or result.get('failures'):raise SystemExit(1)
