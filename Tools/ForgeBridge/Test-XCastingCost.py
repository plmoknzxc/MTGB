"""Bounded native X cost evidence, distinct from printed/X and resolution taxes."""
import json,os,queue,subprocess,tempfile,threading,time,sys,hashlib,shutil
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime'
OUT=ROOT/os.environ.get('MTGB_X_COST_OUTPUT','.tools/x-stack-insert-1005/native');OUT.mkdir(parents=True,exist_ok=True)
CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(ROOT/'Tools/ForgeBridge/tests/XCastingCostFixture.java')],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES={'normal':(3,'{3}{R}',4),'reduce':(3,'{2}{R}',3),'increase':(3,'{4}{R}',5),'xx-reduce':(3,'{5}',5),'xx-increase':(3,'{7}',7),'xx-zero':(0,'{0}',0),'counter-tax':(3,'{3}{R}',7)}
def run(case):
    x,cost,spent=CASES[case]
    if (OUT/(case+'.jsonl')).exists():
        archive=OUT/'attempts'/str(time.time_ns());archive.mkdir(parents=True)
        for old in OUT.glob(case+'-*'):
            if old.is_file():shutil.copy2(old,archive/old.name)
        for suffix in ('.log','.jsonl'):shutil.copy2(OUT/(case+suffix),archive/(case+suffix))
    with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(OUT/(case+'.jsonl')).open('w',encoding='utf8') as trace,(OUT/(case+'.log')).open('w',encoding='utf8') as log:
        start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,fixture=case,seed=4,firstPlayerSeat=1,startingLife=20,decks=[DECK,DECK])))
        p=subprocess.Popen([str(RT/CFG['java']),'-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.XCastingCostFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        q=queue.Queue()
        def read():
            try:
                for line in p.stdout:q.put(json.loads(line))
            finally:q.put(None)
        threading.Thread(target=read,daemon=True).start()
        cast=False;counter=False;seen=False;tax=False;number_cancelled=False;pay_cancelled=False;spell='Walking Ballista' if case.startswith('xx') else 'Song of Totentanz'
        def snap(m,label):
            v=dict(m['views'][m['request']['seat']-1]);v.update(request=m['request'],awaitingInput=True)
            (OUT/(case+'-'+label+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
        try:
            for step in range(190):
                m=q.get(timeout=40);assert m is not None,'Engine exited';trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ('fatal','rejected','ended'),m
                if m['type']!='request':continue
                r=m['request'];v=m['views'][r['seat']-1];one,two=m['views'][0]['players'];ids=[o['id'] for o in r['options']];kind=r['kind'];stack=v['stackItems'];pick='ok'
                print(case,step,kind,ids,flush=True)
                own=[e for e in stack if e['source']['name']==spell and e['kind']=='spell']
                if own:
                    e=own[0];assert e['hasXValue'] and e['xValue']==x,e
                    assert e['source'].get('castingManaCost')==cost,e
                    seen=True;snap(m,'stack')
                if kind=='InputPassPriority':
                    if seen and not stack:
                        assert sum(c['tapped'] for c in one['battlefield'] if c['land'])==spent,one
                        if case.startswith('xx'):
                            ballista=next((c for c in one['battlefield'] if c['name']==spell),None)
                            if x==0:assert ballista is None and spell in one['graveyard']
                            else:assert ballista and ballista['power']==x and any(c['kind']=='P1P1' and c['count']==x for c in ballista['counters'])
                        else:assert sum(c.get('token',False) and 'Rat' in c['currentType'] for c in one['battlefield'])==x
                        if case=='counter-tax':assert tax and counter
                        snap(m,'resolved');return dict(case=case,status='PASS',x=x,castingManaCost=cost,manaSpent=spent,resolutionTax=3 if tax else 0,numberCancel=number_cancelled,paymentCancel=pay_cancelled)
                    if not cast and r['seat']==1 and v['phase']=='MAIN1':pick='card:'+str(next(c for c in one['hand'] if c['name']==spell)['id']);cast=True
                    elif case=='counter-tax' and own and r['seat']==2 and not counter:pick='card:'+str(next(c for c in v['players'][1]['hand'] if c['name']=='Mana Leak')['id']);counter=True
                elif kind=='ChooseNumber':
                    if case=='reduce' and not number_cancelled:pick='cancel';number_cancelled=True;cast=False
                    else:pick='number-plus' if r['selectedValue']<x else 'confirm-number'
                elif kind in ('InputSelectTargets','SelectStackTarget'):pick='ok' if 'ok' in ids else next(i for i in ids if i.startswith('stack:'))
                elif 'PayMana' in kind:
                    if case=='reduce' and not pay_cancelled and not any(c['tapped'] for c in one['battlefield'] if c['land']):pick='cancel';pay_cancelled=True;cast=False
                    else:
                        if case=='counter-tax' and r['seat']==1 and counter:tax=True;assert own and own[0]['source']['castingManaCost']==cost;snap(m,'resolution-tax')
                        if 'ok' in ids:pick='ok'
                        elif any(i.startswith('mana:') for i in ids):pick=next(i for i in ids if i.startswith('mana:'))
                        else:pick=next('card:'+str(c['id']) for c in v['players'][r['seat']-1]['battlefield'] if c['land'] and not c['tapped'] and 'card:'+str(c['id']) in ids)
                elif kind=='Option':pick=next(i for i in ids if i not in ('cancel','no'))
                elif kind in ('InputConfirmMulligan','InputAttack','InputBlock') and 'ok' in ids:pick='ok'
                else:raise AssertionError((kind,ids,r['message']))
                assert pick in ids,(pick,ids)
                p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
            raise AssertionError('Step limit')
        finally:p.kill();p.wait()
for case in (sys.argv[1:] or CASES):
    result=run(case);result.update(runtimeHash=CFG['runtimeHash'],traceSha256=hashlib.sha256((OUT/(case+'.jsonl')).read_bytes()).hexdigest())
    (OUT/(case+'-result.json')).write_text(json.dumps(result,indent=2),encoding='utf8');print(result,flush=True)
