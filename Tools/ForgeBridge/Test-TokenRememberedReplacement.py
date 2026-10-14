"""One actual N26 Encore prototype-memory tail. Positional case: encore-nonempty.

PASS_BOUNDED is emitted only for complete real actions and zero failed assertions.
No reportdir CLI. Initial positions only; immutable runs and read-only engine observations.
"""
from pathlib import Path
import hashlib,json,os,queue,subprocess,tempfile,threading,time,traceback,sys,zipfile
ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'.tools/layers-spellbook-reconcile-1011/reconciliation'
RT=ROOT/'ForgeRuntime'
def write(p,x):p.write_text(json.dumps(x,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def ref(p):return dict(path=p.relative_to(ROOT).as_posix(),sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def cards(n):return [c for p in n['players'] for z in p['zones'].values() for c in z]
def find(n,name,zone=None):return next(c for c in cards(n) if c['name']==name and (zone is None or c['zone']==zone))
def run(case):
    assert case=='encore-nonempty'
    runtime=json.loads((RT/'runtime.json').read_text('utf-8-sig'))
    assert len(runtime['runtimeHash'])==64 and all(x in '0123456789abcdef' for x in runtime['runtimeHash']), 'Recorded actual runtime manifest hash'
    folder=BASE/'native'/case/str(time.time_ns());folder.mkdir(parents=True)
    audit=folder/'audit';audit.mkdir();classes=folder/'classes';classes.mkdir()
    positions=[dict(Graveyard=['Kinsbaile Courier'],Battlefield=['Plains']*3),dict(Hand=['Crafty Cutpurse'],Battlefield=['Divine Visitation','Doubling Season','Chatterfang, Squirrel General']+['Island']*4)]
    fixtures=[ROOT/'Tools/ForgeBridge/tests/RoyalStateFixture.java',ROOT/'Tools/ForgeBridge/tests/TokenRememberedReplacementFixture.java']
    up=ROOT/'.tools/forge-proof/upstream/forge-game/src/main/java/forge/game'
    sources=fixtures+[Path(__file__),RT/'runtime.json',ROOT/'Tools/ForgeBridge/java/mtgb/bridge/ForgeBridge.java']
    sources += [up/p for p in ['ability/effects/CopyPermanentEffect.java','ability/effects/ReplaceTokenEffect.java','ability/effects/TokenEffectBase.java','card/CardFactoryUtil.java','card/CardCopyService.java','card/TokenCreateTable.java','replacement/ReplaceToken.java','replacement/ReplacementHandler.java']]
    sources += [RT/'assets/res/cardsfolder'/p for p in ['k/kinsbaile_courier.txt','c/crafty_cutpurse.txt','d/divine_visitation.txt','d/doubling_season.txt','c/chatterfang_squirrel_general.txt']]
    frozen=[]
    for i,path in enumerate(sources):
        dest=folder/'frozen'/str(i)/path.name;dest.parent.mkdir(parents=True);dest.write_bytes(path.read_bytes());frozen.append(dict(sourcePath=path.relative_to(ROOT).as_posix(),sha256=ref(path)['sha256'],frozen=ref(dest)))
    native_classes=[]
    for relative in ['mtgb/bridge/ForgeBridge.class','forge/game/card/Card.class','forge/game/ability/effects/CopyPermanentEffect.class','forge/game/ability/effects/ReplaceTokenEffect.class','forge/game/ability/effects/TokenEffectBase.class','forge/game/card/CardFactoryUtil.class','forge/game/card/CardCopyService.class','forge/game/replacement/ReplacementHandler.class']:
        path=RT/'classes'/relative;dest=folder/'frozen-runtime-classes'/relative;dest.parent.mkdir(parents=True,exist_ok=True)
        if path.exists():data=path.read_bytes();container=ref(path)
        else:
            matches=[]
            for jar in (RT/'lib').glob('*.jar'):
                with zipfile.ZipFile(jar) as z:
                    if relative in z.namelist():matches.append((jar,z.read(relative)))
            assert len(matches)==1,(relative,len(matches));jar,data=matches[0];container=ref(jar)
        dest.write_bytes(data);native_classes.append(dict(container=container,entry=relative,sha256=hashlib.sha256(data).hexdigest(),frozen=ref(dest)))
    write(folder/'source-identities.json',dict(runtimeHash=runtime['runtimeHash'],frozenSources=frozen,runtimeClasses=native_classes,observer='Read-only actual tables through cached applied replacement OriginalParams; native token-zone event captures instance remembered before Encore cleanup.'))
    checks=[];failures=[];paid=[];requests=[];table_stages=[];p=None;status='FAIL';error=None;initial=None;started=False;responded=False;source=None;cutpurse=None;finished=False;endpoints=[]
    def check(ok,label):
        checks.append(label)
        if not ok:failures.append(label)
    def require(ok,label):
        check(ok,label)
        if not ok:raise AssertionError(label)
    def observation(rid):
        end=time.monotonic()+8
        while time.monotonic()<end:
            try:return json.loads((audit/(str(rid)+'.json')).read_text('utf8'))
            except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
        raise AssertionError('Missing actual observer '+str(rid))
    try:
        with (folder/'compile.log').open('w',encoding='utf8') as log:subprocess.run(['C:/Program Files/Java/jdk-17/bin/javac.exe','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*map(str,fixtures)],check=True,stdout=log,stderr=subprocess.STDOUT,timeout=60)
        write(folder/'fixture-classes.json',[ref(x) for x in sorted(classes.rglob('*.class'))])
        with tempfile.TemporaryDirectory(prefix='profile-',dir=folder,ignore_cleanup_errors=True) as temp,(folder/'stderr.log').open('w',encoding='utf8') as err,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
            deck=dict(cards=[dict(setCode='10E',collectorNumber='376',count=60)],sideboard=[])
            cfg=dict(session='token-memory-tail',seed=4,startingLife=30,firstPlayerSeat=1,decks=[deck,deck],positions=positions,auditDir=str(audit));write(folder/'start.json',cfg)
            p=subprocess.Popen([str(RT/runtime['java']),'-Xmx512m','-XX:ActiveProcessorCount=2','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.TokenRememberedReplacementFixture',str(RT/runtime['assets']),str(folder/'start.json')],cwd=RT/runtime['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=err,text=True,encoding='utf8')
            q=queue.Queue()
            def reader():
                try:
                    for line in p.stdout:
                        if line.strip():q.put(json.loads(line))
                except Exception as e:q.put(e)
                finally:q.put(None)
            threading.Thread(target=reader,daemon=True).start();seen=set();deadline=time.monotonic()+300
            for step in range(300):
                require(time.monotonic()<deadline,'Bounded actual process deadline');m=q.get(timeout=45)
                if isinstance(m,Exception):raise m
                require(m is not None,'Native process live');trace.write(json.dumps(m)+'\n');trace.flush();require(m['type'] not in ['fatal','ended','rejected'],'Native envelope '+m['type'])
                if m['type']!='request':continue
                r=m['request'];rid=r['requestId']
                if rid in seen:continue
                seen.add(rid);kind=r['kind'];opts=r['options'];enabled=[o['id'] for o in opts if not o.get('disabled')];pick='ok'
                n=None if 'Mulligan' in kind else observation(rid)
                requests.append(dict(id=rid,kind=kind,seat=r['seat'],playerSeat=r['playerSeat'],purpose=r.get('selectionPurpose')))
                write(folder/('request-'+str(rid)+'-wire.json'),m)
                if n:
                    write(folder/('request-'+str(rid)+'-observation.json'),n)
                    for a in n['stack']:
                        if not a['trigger'] and a['name'] in ['Kinsbaile Courier','Crafty Cutpurse'] and not any(x['sourceId']==a['source'] for x in paid):
                            require(len(a['payingMana'])==(3 if a['name']=='Kinsbaile Courier' else 4),'Exact real paid amount '+a['name'])
                            require(a['actor']==(1 if a['name']=='Kinsbaile Courier' else 2),'Exact real paying actor '+a['name'])
                            paid.append(dict(name=a['name'],sourceId=a['source'],mana=len(a['payingMana']),actor=a['actor'],objects=a['payingMana'],params=a['params'],requestId=rid,observation=ref(folder/('request-'+str(rid)+'-observation.json'))));write(folder/'paid.json',paid)
                    if n['tables']:
                        table_stages.append(dict(requestId=rid,requestKind=kind,request=r,table=n['tables'],observation=ref(folder/('request-'+str(rid)+'-observation.json'))));write(folder/'table-stages.json',table_stages)
                if 'Mulligan' in kind:pick='keep' if 'keep' in enabled else 'ok'
                elif kind=='InputPassPriority':
                    if initial is None:
                        initial=n;source=find(n,'Kinsbaile Courier','Graveyard')['id'];cutpurse=find(n,'Crafty Cutpurse','Hand')['id'];write(folder/'initial-observation.json',n)
                    if started and not n['stack'] and len(paid)==2:
                        token_events=[e for e in n['events'] if e['card']['name']=='Angel Token' and e['mode']=='Added']
                        require(len(token_events)==2,'Two actual Angel instance creation events')
                        opponent=n['players'][1]
                        for e in token_events:
                            c=e['card'];require(c['remembered']==[dict(kind='Player',seat=2,nativeId=opponent['nativeId'],identity=opponent['identity'])],'Actual instance preserves exact nonempty original opponent identity')
                            require(c['owner']==1 and c['controller']==2 and c['power']==c['toughness']==4,'Actual replacement instance owner/controller/base')
                        final_tokens=[c for c in cards(n) if c['token'] and c['zone']=='Battlefield']
                        require(len(final_tokens)==4 and sum(c['name']=='Angel Token' for c in final_tokens)==2 and sum(c['name']=='Squirrel Token' for c in final_tokens)==2,'Exact four native instances after four-policy chain')
                        require(all(not c['remembered'] for c in final_tokens),'Encore native cleanup clears per-token remembered after command effect')
                        require(find(n,'Kinsbaile Courier')['zone']=='Exile','Encore actual source exile cost')
                        require(find(n,'Crafty Cutpurse')['zone']=='Battlefield','Actual paid Cutpurse resolved')
                        require(r['seat']==r['playerSeat']==1,'Original next priority actor restored')
                        require(len({c['id'] for c in final_tokens})==4,'Actual distinct token IDs')
                        stages=[x for x in table_stages if x['requestKind']=='OrderAbilities' and x['request'].get('selectedCount')==0]
                        require(any(any(c['prototype']['name']=='Kinsbaile Courier' and c['prototype']['remembered'] and c['prototype']['controller']==2 for t in x['table'] for c in t['cells']) for x in stages),'Actual pre-ReplaceToken nonempty prototype after controller change')
                        require(any(any(c['prototype']['name']=='Angel Token' and c['prototype']['remembered'] and c['amount']==1 for t in x['table'] for c in t['cells']) for x in stages),'Actual ReplaceToken prototype retains nonempty memory')
                        amount_tables=[t for x in stages for t in x['table']]+[t for e in n['events'] for t in e['tables']]
                        require(any(any(c['prototype']['name']=='Angel Token' and c['prototype']['remembered'] and c['amount']==2 for c in t['cells']) for t in amount_tables),'Actual Type Amount retains memory and doubles amount')
                        endpoints.append(dict(name='encore-four-policy-nonempty',requestId=rid,wire=ref(folder/('request-'+str(rid)+'-wire.json')),observation=ref(folder/('request-'+str(rid)+'-observation.json')),tokenIds=[c['id'] for c in final_tokens]));finished=True;break
                    if not started and r['seat']==1 and m['views'][0]['phase']=='MAIN1':pick='card:'+str(source);started=True
                    elif started and not responded and r['seat']==2 and any(a['source']==source for a in n['stack']):pick='card:'+str(cutpurse);responded=True
                elif 'PayMana' in kind:pick='ok'
                elif kind=='OrderAbilities':
                    if r.get('selectionPurpose')=='replacementOrder' and not r.get('selectedCount'):
                        want=next((name for name in ['Divine Visitation','Doubling Season','Chatterfang'] if any(name in o['label'] for o in opts)),None)
                        require(want is not None,'Exact intended four-policy replacement order')
                        pick=next(o['id'] for o in opts if want in o['label'])
                    else:pick='confirm-order' if 'confirm-order' in enabled else next(x for x in enabled if x.startswith('order:'))
                elif kind=='dialog':pick=next(o['id'] for o in opts if 'Encore' in o['label'])
                elif kind in ['InputConfirm','InputAttack','InputBlock']:pick='ok'
                else:raise AssertionError(('Unhandled actual input',r))
                require(pick in enabled,('Actually enabled input',kind,pick));response=dict(type='respond',session=cfg['session'],seat=r['seat'],requestId=rid,option=pick);trace.write(json.dumps(response)+'\n');trace.flush();p.stdin.write(json.dumps(response)+'\n');p.stdin.flush();print(case,kind,r['seat'],pick,flush=True)
            require(finished and len(endpoints)==1 and len(paid)==2,'Full bounded actual chain completed')
        status='PASS_BOUNDED' if not failures else 'FAIL_BOUNDED_AUDIT_REQUIRED'
    except Exception:error=traceback.format_exc();print(error,flush=True)
    finally:
        if p is not None and p.poll() is None:p.kill();p.wait()
    result=dict(status=status,case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=runtime['runtimeHash'],checks=len(checks),assertions=checks,failures=failures,paid=paid,endpoints=endpoints,requests=requests,tableStages=table_stages,frozenSources=frozen,runtimeClasses=native_classes,error=error)
    write(folder/'result.json',result);write(BASE/(case+'-latest.json'),result);print(status,case,folder,flush=True);return result
if __name__=='__main__':
    result=run(sys.argv[1] if len(sys.argv)>1 else 'encore-nonempty')
    if result['status']!='PASS_BOUNDED' or result['failures']:raise SystemExit(1)
