"""Native spending-shard, nonartifact restriction, ETB counter and spent-trigger deltas."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'.tools/mana-spending-1006/native';OUT.mkdir(parents=True,exist_ok=True);CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURE=ROOT/'Tools/ForgeBridge/tests/RestrictedManaFixture.java';SHA=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(FIXTURE)],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES={
 'colored-not-generic':dict(sources=['Jegantha, the Wellspring'],hand=['Shock','Goblin Piker'],spell='Shock',forbidden=['Goblin Piker'],pool=[1,1,1,1,1,0],pay='R'),
 'nonartifact-forbidden':dict(sources=['Hydraulic Helper','Hydraulic Helper'],hand=['Etherium Sculptor','Opt'],spell='Etherium Sculptor',forbidden=['Opt'],pool=[0,2,0,0,0,0],pay='U'),
 'mana-etb-counter':dict(sources=['Biophagus'],hand=['Bloodlust Inciter'],spell='Bloodlust Inciter',forbidden=[],pool=[0,0,0,1,0,0],pay='R'),
 'spent-copy':dict(sources=["Pyromancer's Goggles"],hand=['Shock'],spell='Shock',forbidden=[],pool=[0,0,0,1,0,0],pay='R'),
}
def run(case):
    spec=CASES[case];folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True)
    meta=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],driverSha256=SHA(Path(__file__)),fixtures=[dict(path=FIXTURE.relative_to(ROOT).as_posix(),sha256=SHA(FIXTURE))])
    def finish(status,message):
        result=dict(meta,status=status,result=status+' '+case+': '+message)
        for p in [folder/'result.json',OUT/(case+'-result.json')]:p.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
        return result
    with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
        start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,fixture=case,hand=spec['hand'],lands=spec['sources'],seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK])),encoding='utf8')
        proc=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.RestrictedManaFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        messages=queue.Queue()
        def reader():
            try:
                for line in proc.stdout:messages.put(json.loads(line))
            finally:messages.put(None)
        threading.Thread(target=reader,daemon=True).start()
        def snap(m,label):
            for seat,v in enumerate(m['views'],1):(folder/(label+'-seat'+str(seat)+'.json')).write_text(json.dumps(dict(v,request=m['request'],awaitingInput=True),ensure_ascii=False,indent=2),encoding='utf8')
        floated=set();cast=False;stack_seen=False;copy_seen=False
        try:
            for step in range(160):
                m=messages.get(timeout=35);assert m is not None,'Bridge exited';trace.write(json.dumps(m,ensure_ascii=False)+'\n');trace.flush();assert m['type'] not in {'fatal','rejected','ended'},m
                if m['type']!='request':continue
                r=m['request'];v=m['views'][0];one=v['players'][0];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts];pick='ok'
                spells=[s for s in v['stackItems'] if s['kind']=='spell' and s['source']['name']==spec['spell']]
                if spells:stack_seen=True;snap(m,'stack-'+str(step))
                if len(spells)==2:
                    assert len({s['id'] for s in spells})==2 and sum(bool(s['source']['copy']) for s in spells)==1
                    copy_seen=True;snap(m,'copy-stack')
                if kind=='InputPassPriority' and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
                    source=next((c for c in one['battlefield'] if c['name'] in spec['sources'] and c['id'] not in floated),None)
                    if source:
                        pick='card:'+str(source['id']);floated.add(source['id'])
                    elif not cast:
                        assert one['manaPool']==spec['pool'],('Production pool',one['manaPool']);snap(m,'floated-legality')
                        for name in [spec['spell']]+spec['forbidden']:
                            card=next(c for c in one['hand'] if c['name']==name)
                            assert ('card:'+str(card['id']) in ids)==(name==spec['spell']),('Restriction legality',name,r)
                        pick='card:'+str(next(c for c in one['hand'] if c['name']==spec['spell'])['id']);cast=True
                    elif stack_seen:
                        assert one['life']==20 and all(c['tapped'] for c in one['battlefield'] if c['name'] in spec['sources'])
                        if case=='colored-not-generic':
                            assert one['manaPool']==[1,1,1,0,1,0] and v['players'][1]['life']==18 and 'Shock' in one['graveyard']
                        elif case=='nonartifact-forbidden':
                            assert sum(one['manaPool'])==0 and any(c['name']==spec['spell'] and c['power']==1 and c['toughness']==2 for c in one['battlefield'])
                        elif case=='mana-etb-counter':
                            creature=next(c for c in one['battlefield'] if c['name']==spec['spell']);assert creature['power']==2 and creature['toughness']==2 and sum(one['manaPool'])==0
                            assert any(c['kind']=='P1P1' and c['count']==1 for c in creature['counters'])
                        else:
                            assert copy_seen and v['players'][1]['life']==16 and one['graveyard'].count('Shock')==1 and sum(one['manaPool'])==0
                        snap(m,'resolved');return finish('PASS','Actual mana activation, exact pool, native allowed/forbidden spell, original mana symbol payment and exact resolved resources/effect; next priority.')
                elif kind=='dialog':
                    snap(m,'choice-'+str(step));pick=next(o['id'] for o in opts if o['label']=='RED')
                elif 'PayMana' in kind:snap(m,'payment-'+str(step));pick='mana:'+spec['pay']
                elif kind=='InputSelectTargets':pick='ok' if 'ok' in ids else 'player:'+str(v['players'][1]['targetId'])
                elif kind=='InputMulligan':pick='keep'
                elif kind not in {'InputPassPriority','InputConfirmMulligan','InputConfirm'}:raise AssertionError(('Unhandled mana flow',r))
                assert pick in ids,(case,kind,pick,r)
                print(case,step,kind,pick,flush=True);proc.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');proc.stdin.flush()
            raise AssertionError('Step limit')
        except Exception as e:finish('FAIL',repr(e));raise
        finally:
            if proc.poll() is None:proc.kill();proc.wait()
if __name__=='__main__':
    for case in sys.argv[1:] or CASES:print(json.dumps(run(case)),flush=True)
