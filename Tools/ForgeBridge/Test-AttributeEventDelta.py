"""N28 actual attribute producer/lifecycle deltas. Initial positions only; observers are read-only.

Positional cases: harnessed, solved, suspected, saddled, plotted.
PASS_BOUNDED is printed only after all original operations finish without violations.
"""
from pathlib import Path
import hashlib, json, os, queue, subprocess, tempfile, threading, time, traceback, sys, zipfile

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / '.tools/attribute-selector-tail-1011/event-identity'
RT = ROOT / 'ForgeRuntime'
EFFECTIVE_ASSETS=Path(os.environ.get('MTGB_CHOICE_ASSETS',str(RT/'assets')))
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
    fixtures = [ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java', ROOT/'Tools/ForgeBridge/tests/AttributeEventFixture.java']
    sources = fixtures + [Path(__file__), BASE/'cases.json', RT/'runtime.json', ROOT/'Tools/ForgeBridge/java/mtgb/bridge/ForgeBridge.java', ROOT/'Tools/ForgeBridge/java/mtgb/bridge/ForgeCharacteristicDetails.java', RT/'source/native-patches/forge/game/card/Card.java']
    upstream = ROOT / '.tools/forge-proof/upstream/forge-game/src/main/java/forge/game'
    sources += [upstream/'ability/effects/AlterAttributeEffect.java']
    sources += [RT/'assets/res/cardsfolder'/p for p in plan['sourceScripts']]
    frozen = []
    for i, path in enumerate(sources):
        dest = folder/'frozen'/str(i)/path.name; dest.parent.mkdir(parents=True, exist_ok=True); dest.write_bytes(path.read_bytes())
        frozen.append(dict(sourcePath=path.relative_to(ROOT).as_posix(), sha256=ref(path)['sha256'], frozen=ref(dest)))
    frozen_classes = []
    for relative in ['mtgb/bridge/ForgeBridge.class','mtgb/bridge/ForgeCharacteristicDetails.class','forge/game/card/Card.class','forge/game/ability/effects/AlterAttributeEffect.class']:
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
    checks=[]; violations=[]; paid=[]; requests=[]; endpoints=[]; original={}; p=None; eventProof=[]
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
        elif result=='plot-event':
            child=byid(n,original['Longhorn Sharpshooter']);check(child['plotted'] and child['zone']=='Exile','Actual original Longhorn plotted in exile')
            check(n['players'][1]['life']==28,'Actual plotted consumer damage reaches original opponent')
            check(any(e['name']=='Longhorn Sharpshooter' and e['triggerObjects'].get('Card',{}).get('cardId')==child['id'] for e in eventProof),'Actual BecomesPlotted trigger preserves original Card identity')
        elif result=='saddle-event':
            child=byid(n,original['Stubborn Burrowfiend']);check(child['saddled'] and child['power']==4 and child['toughness']==4,'Actual saddled consumer mills two creatures and applies native graveyard-derived pump')
            check(any(e['name']=='Stubborn Burrowfiend' and e['triggerObjects'].get('Card',{}).get('cardId')==child['id'] for e in eventProof),'Actual BecomesSaddled trigger preserves original Mount identity')
        elif result=='solve-event':
            child=byid(n,original['Case of the Filched Falcon']);check(child['solved'],'Actual natural case solve flag')
            check(any(c['name']=='Pacifism' and c['zone']=='Hand' for c in cards(n)),'Actual solve consumer gives original legal enchantment choice to hand')
            check(any(e['name']=='Case File Auditor' and e['triggerObjects'].get('Card',{}).get('cardId')==child['id'] and e['triggerObjects'].get('Player',{}).get('playerSeat')==1 for e in eventProof),'Actual CaseSolved Card/Player identities reach original other-card listener')
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
            cfg=dict(session='attribute-delta-'+case,seed=4,startingLife=30,firstPlayerSeat=1,decks=[deck,deck],positions=plan['positions'],auditDir=str(audit));write(folder/'start.json',cfg)
            p=subprocess.Popen([str(RT/runtime['java']),'-Xmx512m','-XX:ActiveProcessorCount=3','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.AttributeEventFixture',str(EFFECTIVE_ASSETS),str(folder/'start.json')],cwd=EFFECTIVE_ASSETS,env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
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
                require(m is not None,'Native process live');trace.write(json.dumps(m)+'\n');trace.flush();require(m['type'] not in ['fatal','ended','rejected'],str(m))
                if m['type']!='request':continue
                r=m['request'];rid=r['requestId']
                if rid in seen:continue
                seen.add(rid);opts=r['options'];enabled=[o['id'] for o in opts if not o.get('disabled')];kind=r['kind'];view=m['views'][0];n=None if 'Mulligan' in kind else observe(rid);pick='ok';op=plan['operations'][index]
                requests.append(dict(id=rid,kind=kind,seat=r['seat'],playerSeat=r['playerSeat'],stage=stage,operation=index))
                if n:
                    freeze(m,n,'request-'+str(rid));eventProof.extend(x for x in n['stack'] if x['trigger'] and x.get('triggerObjects'));write(folder/'event-proof.json',eventProof)
                if stage=='resolve' and not paidSeen:
                    ability=next((x for x in n['stack'] if x['source']==source and not x['trigger']),None)
                    if ability:
                        check(len(ability['payingMana'])==op['mana'],'Actual paid mana count '+str(index));check(ability['actor']==1,'Actual source paying actor '+str(index))
                        paid.append(dict(operation=index,name=op['source'],sourceId=source,mana=len(ability['payingMana']),objects=ability['payingMana'],requestId=rid,api=ability['api'],spell=ability['spell'],targetCards=ability['targetCards'],targetPlayers=ability['targetPlayers']));write(folder/'paid.json',paid);freeze(m,n,'paid-'+str(index));paidSeen=True
                if kind=='InputPassPriority':
                    if stage=='resolve' and not n['stack'] and r['seat']==1:
                        endpoint(m,n,op)
                        if index==len(plan['operations']):break
                        op=plan['operations'][index]
                    if stage=='wait' and not n['stack'] and view['phase']==op['endPhase'] and n['turn']>=op['minTurn'] and view['turnSeat']==op['endTurnSeat']:
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
                elif kind=='InputSelectTargets':
                    todo=[target(x,n,m) for x in op.get('targets',[]) if target(x,n,m) not in targeted]
                    if todo:pick=todo[0];targeted.append(pick)
                    else:pick='ok'
                elif kind in ['dialog','SelectCards','InputSelectEntities','InputSelectEntitiesFromList','ChooseCards']:
                    optsCard=[o for o in opts if o['id'] in enabled and o.get('card') and o['card'].get('name')]
                    seat=r.get('playerSeat',r['seat']);chosen=picked.setdefault(seat,[])
                    cand=[dict(id=o['card']['id'],name=o['card']['name'],controller=byid(n,o['card']['id'])['controller'],power=byid(n,o['card']['id'])['power']) for o in optsCard]
                    info=dict(requestId=rid,seat=seat,kind=kind,message=r.get('message'),candidates=cand,enabled=enabled,operation=index)
                    if optsCard:
                        wanted=[x for x in op.get('pickCards',{}).get(str(seat),[]) if x not in chosen]
                        matching=[o for name in wanted for o in optsCard if o['card']['name']==name]
                        if r.get('selectedCount',0)>0 and 'ok' in enabled:pick='ok'
                        elif matching:pick=matching[0]['id'];chosen.append(matching[0]['card']['name'])
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
                elif kind=='RevealCards':pick='confirm-cards'
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
