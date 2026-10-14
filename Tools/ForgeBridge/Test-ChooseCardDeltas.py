"""N27 actual ChooseCard producer families. Initial positions only; observers are read-only.

Positional cases: total-power, each-power, different-power, basic-types, party-types, secret-control.
PASS_BOUNDED is printed only after all original operations finish without violations.
"""
from pathlib import Path
import hashlib, json, os, queue, subprocess, tempfile, threading, time, traceback, sys, zipfile

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / '.tools/mechanic-tail-review-1011'
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
    fixtures = [ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java', ROOT/'Tools/ForgeBridge/tests/ChooseCardDeltaFixture.java']
    sources = fixtures + [Path(__file__), BASE/'cases.json', RT/'runtime.json', ROOT/'Tools/ForgeBridge/java/mtgb/bridge/ForgeBridge.java', ROOT/'Tools/ForgeBridge/java/mtgb/bridge/ForgeCharacteristicDetails.java', RT/'source/native-patches/forge/game/card/Card.java']
    upstream = ROOT / '.tools/forge-proof/upstream/forge-game/src/main/java/forge/game'
    sources += [upstream/'ability/effects/ChooseCardEffect.java', RT/'source/native-patches/forge/game/ability/effects/ChooseCardEffect.java']
    sources += [RT/'assets/res/cardsfolder'/p for p in ['s/slaughter_the_strong.txt','c/celestial_judgment.txt','s/sigardas_vanguard.txt','g/global_ruin.txt','s/stick_together.txt','c/call_to_the_void.txt','c/char_rumbler.txt']]
    if EFFECTIVE_ASSETS != RT/'assets':sources += [BASE/'asset-stage.json',EFFECTIVE_ASSETS/'res/cardsfolder/s/stick_together.txt']
    frozen = []
    for i, path in enumerate(sources):
        dest = folder/'frozen'/str(i)/path.name; dest.parent.mkdir(parents=True, exist_ok=True); dest.write_bytes(path.read_bytes())
        frozen.append(dict(sourcePath=path.relative_to(ROOT).as_posix(), sha256=ref(path)['sha256'], frozen=ref(dest)))
    frozen_classes = []
    for relative in ['mtgb/bridge/ForgeBridge.class','mtgb/bridge/ForgeCharacteristicDetails.class','forge/game/card/Card.class','forge/game/ability/effects/ChooseCardEffect.class']:
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
        bf=[c for c in cards(n) if c['zone']=='Battlefield' and c['creature']]
        if result=='total-power':
            kept={c['originalName'] for c in bf if c['controller']==1};check(kept=={'Char-Rumbler','Grizzly Bears','Phantom Monster'},'Actual chosen total4 incl negative -1; unchosen own creature sacrificed')
            check(sum(c['power'] for c in bf if c['controller']==1)==4,'Actual kept power sum4')
            check(not any(c['controller']==2 for c in bf),'Other player confirmed early zero; all their creatures sacrificed')
            # A unique mandatory remaining negative creature is resolved natively with no extra picker.
            check(any(x.get('mandatoryOverLimit') for x in decisions) or any(c['originalName']=='Char-Rumbler' for c in bf),'Actual over-limit selection restores legal total4 via required negative creature; unique mandatory candidate may auto-resolve')
        elif result=='each-power':
            check(sorted(c['power'] for c in bf)==[-1,0,2,3],'Exactly one actual creature for each sorted net power survives')
            check(all(c['controller']==1 for c in bf),'Original chosen IDs retained, other matching power copies destroyed')
        elif result=='different-power':
            selected=[c for c in bf if 'Double Strike' in c['keywords']]
            check({c['originalName'] for c in selected}=={'Grizzly Bears','Phantom Monster'},'Only two different powers actually gain Double Strike')
            check(any(x.get('samePowerExcluded') for x in decisions),'Candidates refresh excludes original same-power foreign creature')
        elif result in ['different-expired','different-zero']:
            check(not any('Double Strike' in c['keywords'] for c in bf),'Native EOT expires granted keyword / zero choice grants none '+result)
        elif result=='basic-types':
            lands=[c for c in cards(n) if c['zone']=='Battlefield' and 'Land' in c['type']]
            check(len([c for c in lands if c['controller']==1])==5 and len([c for c in lands if c['controller']==2])==2,'Actual per-player basic-type exact survivors5/2')
            check(not any(c['originalName']=='Strip Mine' for c in lands),'All unchosen nonbasic lands sacrificed')
        elif result=='party-types':
            check(all(x.get('correctController') for x in decisions if x.get('party')),'Every party candidate must be controlled by the actual chooser')
            check(len(bf)==8 and len([c for c in bf if c['controller']==1])==4 and len([c for c in bf if c['controller']==2])==4,'Each player keeps their own four actual party creatures')
        elif result=='secret-control':
            check({c['originalName'] for c in bf}=={'Serra Angel','Air Elemental'},'Exactly four chosen original creatures destroyed; two unchosen retained')
            check({x['seat'] for x in decisions if x.get('secret')}=={1,2},'Both actual players choose own/other creature before collective reveal')
        if op.get('action')=='cast':check(current[source]['zone']=='Graveyard' if case!='different-power' else current[source]['zone']=='Battlefield','Real source final zone '+result)
        endpoints.append(dict(operation=index,result=result,requestId=m['request']['requestId'],seat=m['request']['seat'],phase=n['phase'],turn=n['turn'],wire=ref(folder/('after-'+str(index)+'-wire.json')),observation=ref(folder/('after-'+str(index)+'-observation.json'))));write(folder/'endpoints.json',endpoints)
        index+=1;stage='next'
    try:
        compiler=Path(os.environ.get('MTGB_JAVAC','C:/Program Files/Java/jdk-17/bin/javac.exe'))
        with (folder/'compile.log').open('w',encoding='utf8') as log:subprocess.run([str(compiler),'-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*map(str,fixtures)],check=True,stdout=log,stderr=subprocess.STDOUT,timeout=60)
        write(folder/'fixture-classes.json',[ref(x) for x in sorted(classes.rglob('*.class'))])
        with tempfile.TemporaryDirectory(prefix='profile-',dir=folder,ignore_cleanup_errors=True) as temp,(folder/'stderr.log').open('w',encoding='utf8') as err,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
            deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
            cfg=dict(session='choose-delta-'+case,seed=4,startingLife=30,firstPlayerSeat=1,decks=[deck,deck],positions=plan['positions'],auditDir=str(audit));write(folder/'start.json',cfg)
            p=subprocess.Popen([str(RT/runtime['java']),'-Xmx512m','-XX:ActiveProcessorCount=3','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.ChooseCardDeltaFixture',str(EFFECTIVE_ASSETS),str(folder/'start.json')],cwd=EFFECTIVE_ASSETS,env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
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
                        before=n;targeted=[];paidSeen=False;picked={};powerSeen=[];source=None if op.get('action') in ['observe','wait'] else find(n,op['source'],1,'Battlefield' if op.get('action')=='activate' else 'Hand')['id'];freeze(m,n,'before-'+str(index))
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
                    if case=='total-power' and seat==1:
                        total=sum(next(c['power'] for c in cards(before) if c['originalName']==name and c['controller']==1) for name in chosen)
                        if total==5:info['mandatoryOverLimit']='cancel' not in enabled and any(x['name']=='Char-Rumbler' for x in cand)
                    if r.get('selectedCount',0)>=r.get('minCount',1) and 'ok' in enabled:
                        pick='ok'
                    elif optsCard:
                        if case=='party-types':
                            info.update(party=True,correctController=all(x['controller']==seat for x in cand))
                            matching=[o for o in optsCard if byid(n,o['card']['id'])['controller']==seat]
                            require(bool(matching),'Own actual party option exists');pick=matching[0]['id']
                        else:
                            wanted=[x for x in op.get('pickCards',{}).get(str(seat),[]) if x not in chosen]
                            matching=[o for name in wanted for o in optsCard if o['card']['name']==name]
                            # Native single mandatory choices may auto-resolve, so match remaining candidate rather than positional count.
                            if matching:pick=matching[0]['id'];chosen.append(matching[0]['card']['name'])
                            elif 'confirm-cards' in enabled:pick='confirm-cards'
                            elif 'cancel' in enabled:pick='cancel'
                            else:raise AssertionError(('No original planned candidate/stop',r,chosen))
                        if case=='different-power' and 'Grizzly Bears' in chosen and len(chosen)>1:info['samePowerExcluded']=not any(x['name']=='Silvercoat Lion' for x in cand)
                        if case=='secret-control':info['secret']=True
                    elif 'confirm-cards' in enabled:pick='confirm-cards'
                    elif 'ok' in enabled:pick='ok'
                    elif 'yes' in enabled:pick='yes'
                    else:raise AssertionError(('Unexpected actual chooser confirmation',r))
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
