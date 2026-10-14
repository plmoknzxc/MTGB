"""N25 actual names and legacy payloads. Initial positions only; observers are read-only.

Positional cases: legacy-damage-jace, name-layer-restoration.
PASS_BOUNDED is printed only after all original operations finish without violations.
"""
from pathlib import Path
import hashlib, json, os, queue, subprocess, tempfile, threading, time, traceback, sys, zipfile

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / '.tools/repeat-reveal-names-1011/names'
RT = ROOT / 'ForgeRuntime'
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
    fixtures = [ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java', ROOT/'Tools/ForgeBridge/tests/NameAndLegacyPayloadFixture.java']
    sources = fixtures + [Path(__file__), BASE/'cases.json', RT/'runtime.json', ROOT/'Tools/ForgeBridge/java/mtgb/bridge/ForgeBridge.java', ROOT/'Tools/ForgeBridge/java/mtgb/bridge/ForgeCharacteristicDetails.java', RT/'source/native-patches/forge/game/card/Card.java']
    upstream = ROOT / '.tools/forge-proof/upstream/forge-game/src/main/java/forge/game'
    sources += [upstream/p for p in ['ability/effects/EmpowerEffect.java', 'ability/effects/AnimateEffect.java', 'ability/effects/AnimateEffectBase.java', 'ability/effects/ChooseCardNameEffect.java', 'ability/effects/ChooseTypeEffect.java', 'staticability/StaticAbilityContinuous.java']]
    sources += [RT/'assets/res/cardsfolder'/p for p in ['upcoming/no_admittance.txt','w/witness_protection.txt','p/psychic_paper.txt','s/spy_kit.txt','a/awakening_of_vitu_ghazi.txt']]
    frozen = []
    for i, path in enumerate(sources):
        dest = folder/'frozen'/str(i)/path.name; dest.parent.mkdir(parents=True, exist_ok=True); dest.write_bytes(path.read_bytes())
        frozen.append(dict(sourcePath=path.relative_to(ROOT).as_posix(), sha256=ref(path)['sha256'], frozen=ref(dest)))
    frozen_classes = []
    for relative in ['mtgb/bridge/ForgeBridge.class','mtgb/bridge/ForgeCharacteristicDetails.class','forge/game/card/Card.class','forge/game/ability/effects/EmpowerEffect.class','forge/game/ability/effects/AnimateEffect.class','forge/game/ability/effects/ChooseCardNameEffect.class','forge/game/staticability/StaticAbilityContinuous.class']:
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
    checks=[]; violations=[]; paid=[]; requests=[]; endpoints=[]; original={}; p=None
    index=0; stage='next'; source=None; targeted=[]; paidSeen=False; before=None
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
        if case=='legacy-damage-jace':
            check([x['life'] for x in n['players']]==[30,27],'Exact target loses three life')
            js=[c for c in cards(n) if c['zone']=='Battlefield' and c['token'] and 'Jace' in c['type']]
            check(len(js)==1,'Exactly one actual Jace token')
            if js:
                c=js[0];check(c['owner']==c['controller']==1 and c['loyalty']==1 and c['colors']==2 and c['planeswalker'],'Blue Jace owner/controller and loyalty one')
                check(c['id'] not in {x['id'] for x in cards(before)},'Actually created token new ID')
                check(any(e['event']=='tokenCreated' for e in n['events']),'Actual token-created event')
                check(any(e['event']=='zone' and e['id']==c['id'] and e['zone']=='Battlefield' for e in n['events']),'Actual token battlefield event')
            check(any(e['event']=='damage' and e['source']==source and e['target']==2 and e['amount']==3 and not e['combat'] for e in n['events']),'Original source noncombat damage event')
            check(byid(n,source)['zone']=='Graveyard','Original paid sorcery reaches graveyard')
        else:
            bear=byid(n,original['Grizzly Bears']);check(bear['originalName']=='Grizzly Bears' and bear['id']==original['Grizzly Bears'],'Same recipient printed name/ID '+result)
            expected={'initial':('Grizzly Bears',False),'spy-added':('Grizzly Bears',True),'witness-overwrite':('Legitimate Businessperson',False),'paper-chosen':('Silvercoat Lion',False),'paper-removed':('Legitimate Businessperson',False),'witness-removed':('Grizzly Bears',True),'spy-removed':('Grizzly Bears',False)}
            if result in expected:
                name,flag=expected[result];check((bear['name'],bear['hasNonLegendaryCreatureNames'])==(name,flag),'Actual overwrite/additional name semantics '+result)
                check(bear['sharesNames'][name],'Native exact current name matching '+result)
                if flag:
                    check(bear['sharesNames']['Silvercoat Lion'] and bear['sharesNames']['Grizzly Bears'] and not bear['sharesNames']['Isamaru, Hound of Konda'],'Native nonlegendary name membership and legendary exclusion '+result)
                if result=='spy-removed':check(not bear['nameChanges'] and not bear['sharesNames']['Silvercoat Lion'],'Actual final name modifier removal')
                if result=='paper-chosen':
                    paper=byid(n,original['Psychic Paper']);check(paper['chosenName']=='Silvercoat Lion' and paper['chosenType']=='Human' and paper['attachedTo']==bear['id'],'Actual Psychic Paper chosen fields/attachment')
                for w in wirecards(m,bear['id']):check(w['card']['name']==bear['name'] and w['card']['originalName']=='Grizzly Bears','Public current/original labels '+result+' seat '+str(w['viewSeat']))
                if result=='spy-added':
                    write(folder/'spy-kit-sample.json',dict(native=bear,publicWire=wirecards(m,bear['id']),request=m['request'],observation=ref(folder/('after-'+str(index)+'-observation.json')),wire=ref(folder/('after-'+str(index)+'-wire.json'))))
            if result.startswith('vitu-'):
                c=byid(n,original['vituForest']);check(c['originalName']=='Forest','Same original Forest '+result)
                if result!='vitu-bounced':
                    check(c['name']=='Vitu-Ghazi' and c['plusCounters']==9 and [c['power'],c['toughness']]==[9,9],'Actual named nine-counter land animation '+result)
                    check(all(x in c['type'] for x in ['Legendary','Land','Creature','Elemental']) and any(x['newName']=='Vitu-Ghazi' and x['staticId']==0 for x in c['nameChanges']),'Actual permanent Animate name/table '+result)
                else:check(c['zone']=='Hand' and c['name']=='Forest' and not c['nameChanges'] and c['plusCounters']==0 and not c['creature'],'Actual zone transition restores printed Forest')
                for w in wirecards(m,c['id']):check(w['card']['name']==c['name'] and w['card']['originalName']=='Forest','Actual Forest current/original projection '+result)
        endpoints.append(dict(operation=index,result=result,requestId=m['request']['requestId'],seat=m['request']['seat'],phase=n['phase'],turn=n['turn'],wire=ref(folder/('after-'+str(index)+'-wire.json')),observation=ref(folder/('after-'+str(index)+'-observation.json'))));write(folder/'endpoints.json',endpoints)
        index+=1;stage='next'
    try:
        compiler=Path(os.environ.get('MTGB_JAVAC','C:/Program Files/Java/jdk-17/bin/javac.exe'))
        with (folder/'compile.log').open('w',encoding='utf8') as log:subprocess.run([str(compiler),'-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*map(str,fixtures)],check=True,stdout=log,stderr=subprocess.STDOUT,timeout=60)
        write(folder/'fixture-classes.json',[ref(x) for x in sorted(classes.rglob('*.class'))])
        with tempfile.TemporaryDirectory(prefix='profile-',dir=folder,ignore_cleanup_errors=True) as temp,(folder/'stderr.log').open('w',encoding='utf8') as err,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
            deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
            cfg=dict(session='names-'+case,seed=4,startingLife=30,firstPlayerSeat=1,decks=[deck,deck],positions=plan['positions'],auditDir=str(audit));write(folder/'start.json',cfg)
            p=subprocess.Popen([str(RT/runtime['java']),'-Xmx512m','-XX:ActiveProcessorCount=3','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.NameAndLegacyPayloadFixture',str(RT/runtime['assets']),str(folder/'start.json')],cwd=RT/runtime['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
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
                if n:freeze(m,n,'request-'+str(rid))
                if stage=='resolve' and not paidSeen:
                    ability=next((x for x in n['stack'] if x['source']==source and not x['trigger']),None)
                    if ability:
                        check(len(ability['payingMana'])==op['mana'],'Actual paid mana count '+str(index));check(ability['actor']==1,'Actual source paying actor '+str(index))
                        paid.append(dict(operation=index,name=op['source'],sourceId=source,mana=len(ability['payingMana']),objects=ability['payingMana'],requestId=rid,api=ability['api'],spell=ability['spell'],targetCards=ability['targetCards'],targetPlayers=ability['targetPlayers']));write(folder/'paid.json',paid);freeze(m,n,'paid-'+str(index));paidSeen=True
                if kind=='InputPassPriority':
                    if stage=='resolve' and not n['stack']:
                        endpoint(m,n,op)
                        if index==len(plan['operations']):break
                        op=plan['operations'][index]
                    if stage=='wait' and not n['stack'] and view['phase']==op['endPhase'] and n['turn']>=op['minTurn'] and view['turnSeat']==op['endTurnSeat']:
                        endpoint(m,n,op)
                        if index==len(plan['operations']):break
                        op=plan['operations'][index]
                    if stage=='next' and not n['stack'] and view['turnSeat']==op.get('turnSeat',1) and r['seat']==1 and (op.get('action')=='wait' or view['phase']==op.get('phase','MAIN1')):
                        before=n;targeted=[];paidSeen=False;source=None if op.get('action') in ['observe','wait'] else find(n,op['source'],1,'Battlefield' if op.get('action')=='activate' else 'Hand')['id'];freeze(m,n,'before-'+str(index))
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
                elif kind=='dialog':
                    if r.get('selectionPurpose')=='cardName':
                        pick=next(o['id'] for o in opts if o['id'] in enabled and o.get('cardName')==op['chosenName'])
                    elif 'creature type' in r.get('message','').lower():pick=next(o['id'] for o in opts if o['id'] in enabled and o['label']==op['chosenType'])
                    elif op.get('choice'):pick=next(o['id'] for o in opts if o['id'] in enabled and op['choice'].lower() in o['label'].lower())
                    else:raise AssertionError(('Unexpected name dialog',r))
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
    result=dict(status=status,case=case,folder=folder.relative_to(ROOT).as_posix(),checks=len(checks),assertions=checks,violations=violations,paid=paid,endpoints=endpoints,requests=requests,runtimeHash=runtime['runtimeHash'],originalIds=original,frozenSources=frozen,runtimeClasses=frozen_classes)
    if status=='FAIL':result['error']=error
    write(folder/'result.json',result);write(BASE/(case+'-latest.json'),result);print(status,case,folder,flush=True);return result

if __name__=='__main__':
    for case in sys.argv[1:]:
        result=run(case)
        if result['status']!='PASS_BOUNDED' or result.get('violations') or result.get('failures'):raise SystemExit(1)
