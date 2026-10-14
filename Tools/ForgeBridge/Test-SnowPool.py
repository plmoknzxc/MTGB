"""Native 12-category mana pool: exact provenance, rejected payment and refunds."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime'
OUT=ROOT/'.tools/snow-pool-1006/native';OUT.mkdir(parents=True,exist_ok=True)
CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(ROOT/'Tools/ForgeBridge/tests/SnowPoolFixture.java')],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['snow-green','snow-colorless','regular-colored','snow-colored']

def run(case):
    folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True)
    seat=2 if case in {'snow-colorless','snow-colored'} else 1
    colored=case.endswith('colored');name='Ice-Fang Coatl' if colored else "Arcum's Astrolabe"
    with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
        start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,fixture=case,seed=4,firstPlayerSeat=seat,startingLife=20,decks=[DECK,DECK])))
        process=subprocess.Popen([str(RT/CFG['java']),'-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.SnowPoolFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        messages=queue.Queue()
        def read():
            try:
                for line in process.stdout:messages.put(json.loads(line))
            finally:messages.put(None)
        threading.Thread(target=read,daemon=True).start()
        def receive():
            m=messages.get(timeout=35);assert m is not None,'Bridge exited'
            trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in {'fatal','ended'},m
            return m
        def send(r,pick,actor=None):
            process.stdin.write(json.dumps(dict(type='respond',session=case,seat=actor or r['seat'],requestId=r['requestId'],option=pick))+'\n');process.stdin.flush()
        def snap(m,label):
            v=dict(m['views'][seat-1]);v.update(request=m['request'],awaitingInput=True)
            (folder/(label+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
        before=None;cast=False;stack=False;cancelled=False;wrong=False;pending=None;paid=0;guarded=False
        try:
            for step in range(160):
                m=receive()
                if m['type']!='request':continue
                r=m['request'];v=m['views'][seat-1];p=v['players'][seat-1];ids=[o['id'] for o in r['options']];kind=r['kind'];pick='ok'
                assert len(p['manaPool'])==6 and len(p['snowManaPool'])==6
                assert all(0<=s<=t for s,t in zip(p['snowManaPool'],p['manaPool']))
                for other in m['views']:assert other['players'][seat-1]['snowManaPool']==p['snowManaPool']
                if pending:
                    assert r['contextId']==pending['request']['contextId'] and r['requestId']!=pending['request']['requestId']
                    assert p==pending['views'][seat-1]['players'][seat-1] and ids==[o['id'] for o in pending['request']['options']]
                    snap(m,'wrong-retry');pending=None
                if any(s['kind']=='spell' and s['source']['name']==name for s in v['stackItems']):stack=True;snap(m,'stack')
                if kind=='InputPassPriority' and r['seat']==seat and v['phase']=='MAIN1' and not v['stackItems']:
                    card=next((c for c in p['hand'] if c['name']==name),None)
                    if stack and card is None:
                        assert any(c['name']==name for c in p['battlefield'])
                        assert p['life']==20 and len(p['hand'])==len(before['hand'])
                        expected=[2]*6;snow=[1]*6
                        if colored:
                            expected[4]-=1;expected[1]-=1
                            if case=='snow-colored':snow[4]=snow[1]=0
                        else:
                            index=5 if case=='snow-colorless' else 4;expected[index]-=1;snow[index]-=1
                        assert p['manaPool']==expected and p['snowManaPool']==snow,(p['manaPool'],p['snowManaPool'])
                        assert cancelled and (colored or wrong)
                        snap(m,'resolved')
                        return dict(case=case,status='PASS',seat=seat,folder=str(folder.relative_to(ROOT)),spell=name,total=p['manaPool'],snow=p['snowManaPool'],cancelRefund=True,ordinaryRejectedForSnow=wrong)
                    if before is None:
                        untapped=[c for c in p['battlefield'] if c['land'] and not c['tapped']]
                        if untapped:pick='card:'+str(untapped[0]['id'])
                        else:
                            assert p['manaPool']==[2]*6 and p['snowManaPool']==[1]*6
                            before=json.loads(json.dumps(p));snap(m,'full-pool');pick='card:'+str(card['id']);cast=True
                    else:
                        if cancelled and not cast:assert p==before,'Cancellation lost mana provenance'
                        pick='card:'+str(card['id']);cast=True
                elif 'PayMana' in kind:
                    snap(m,'payment-'+str(paid))
                    if not guarded:
                        assert all('mana:'+s in ids and 'mana:S:'+s in ids for s in 'WUBRGC')
                        send(r,ids[0],3-seat);reject=receive();assert reject['type']=='rejected' and reject['error']=='wrong-seat';guarded=True
                    if not colored and not wrong:
                        pick='mana:G';wrong=True;pending=json.loads(json.dumps(m));snap(m,'wrong-before')
                    elif not cancelled and (not colored or paid==1):
                        pick='cancel';cancelled=True;cast=False;stack=False;paid=0;snap(m,'cancel-payment')
                    else:
                        symbol='G' if paid==0 else 'U'
                        if case=='snow-colorless':symbol='C'
                        pick='mana:'+('S:' if case!='regular-colored' else '')+symbol;paid+=1
                elif kind in {'Option','dialog'}:
                    # Scrying Sheets also has a non-mana ability. Select its real
                    # mana ability rather than activate the library-top ability.
                    opts=[o for o in r['options'] if o['id'] not in {'cancel','no'}]
                    pick=next((o['id'] for o in opts if 'Add' in o['label']),opts[0]['id'])
                elif kind=='InputMulligan':pick='keep'
                assert pick in ids,(case,kind,pick,ids)
                print(case,step,kind,pick,flush=True);send(r,pick)
            raise AssertionError('Step limit')
        finally:
            if process.poll() is None:process.kill();process.wait()

for case in sys.argv[1:] or CASES:
    assert case in CASES,case
    result=run(case);result.update(runtimeHash=CFG['runtimeHash'],sourceHash=hashlib.sha256(Path(__file__).read_bytes()).hexdigest())
    (OUT/(case+'-result.json')).write_text(json.dumps(result,indent=2),encoding='utf8')
    print('PASS '+case+': '+json.dumps(result),flush=True)
