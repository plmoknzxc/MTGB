"""Focused real land-confirmation and Mana Leak payment requests/results."""
import json, os, queue, subprocess, tempfile, threading, time, traceback, sys, shutil
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'.tools/payment-dock-1005/native'
OUT.mkdir(parents=True,exist_ok=True)
RT=ROOT/'ForgeRuntime'
CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
CLASSES=OUT/'classes'; CLASSES.mkdir(exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(Path(__file__).with_name('PaymentReviewFixture.java'))],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}

def run(case):
    if (OUT/(case+'.jsonl')).exists():
        archive=OUT/'attempts'/str(time.time_ns());archive.mkdir(parents=True)
        for old in OUT.glob(case+'*'):
            if old.is_file():shutil.copy2(old,archive/old.name)
        if (OUT/'results.json').exists():shutil.copy2(OUT/'results.json',archive/'results.json')
    with tempfile.TemporaryDirectory(prefix='mtgb-payment-review-') as temp, (OUT/(case+'.log')).open('w',encoding='utf-8') as log, (OUT/(case+'.jsonl')).open('w',encoding='utf-8') as trace:
        start=Path(temp)/'start.json'
        start.write_text(json.dumps(dict(session=case,fixture=case,seed=4,startingLife=20,firstPlayerSeat=1,decks=[DECK,DECK])),encoding='utf-8')
        process=subprocess.Popen([str(RT/CFG['java']),'-Djava.awt.headless=true','-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.PaymentReviewFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf-8')
        messages=queue.Queue()
        def read():
            try:
                for line in process.stdout: messages.put(json.loads(line))
            except Exception as exc: messages.put(exc)
            finally: messages.put(None)
        threading.Thread(target=read,daemon=True).start()
        def save(message,label):
            request=message['request']; view=dict(message['views'][request['seat']-1]);view.update(request=request,awaitingInput=True)
            (OUT/(case+'-'+label+'.json')).write_text(json.dumps(view,ensure_ascii=False,indent=2),encoding='utf-8')
        stage=0;source_id=None;opt_id=None;library_before=None;paid=False;manual_count=0;deadline=time.monotonic()+100
        try:
            for step in range(180):
                assert time.monotonic()<deadline,'Deadline'
                message=messages.get(timeout=40); assert message is not None,'Exited'
                if isinstance(message,Exception):raise message
                trace.write(json.dumps(message)+'\n');trace.flush()
                assert message['type'] not in ('fatal','ended','rejected'),message
                if message['type']!='request':continue
                request=message['request'];view=message['views'][0];one=view['players'][0]
                ids=[item['id'] for item in request['options']];kind=request['kind'];pick='ok'
                if kind=='InputPassPriority':
                    if stage==0 and request['seat']==1 and view['phase']=='MAIN1':
                        name='Hallowed Fountain' if case.startswith('shock') else 'Opt'
                        card=next(c for c in one['hand'] if c['name']==name)
                        source_id=card['id'];opt_id=source_id;library_before=one['libraryCount']
                        save(message,'before');pick='card:'+str(source_id);stage=1
                    elif case.startswith('shock') and stage==2:
                        fountain=next(c for c in one['battlefield'] if c['id']==source_id)
                        assert one['life']==(18 if case=='shock-yes' else 20),one['life']
                        assert fountain['tapped']==(case=='shock-no')
                        save(message,'resolved');return {'case':case,'PASS':True,'life':one['life'],'tapped':fountain['tapped']}
                    elif not case.startswith('shock') and stage==1 and request['seat']==2 and any(c['source']['id']==opt_id for c in view['stackItems']):
                        opponent=message['views'][1]['players'][1]
                        card=next(c for c in opponent['hand'] if c['name']=='Mana Leak')
                        pick='card:'+str(card['id']);stage=2
                    elif not case.startswith('shock') and stage==3 and not view['stackItems']:
                        opt=next(c for c in one['graveyardCards'] if c['id']==opt_id)
                        assert opt['name']=='Opt'
                        expected=library_before-(1 if paid else 0)
                        assert one['libraryCount']==expected,(one['libraryCount'],expected)
                        tapped=sum(c['tapped'] for c in one['battlefield'] if c['name']=='Island')
                        assert tapped==(4 if paid else 1),(tapped,paid)
                        assert one['life']==20 and view['players'][1]['life']==20
                        save(message,'resolved');return {'case':case,'PASS':True,'paid':paid,'tappedIslands':tapped,'libraryCount':one['libraryCount']}
                elif kind=='InputConfirm' and case.startswith('shock'):
                    assert stage==1 and '2' in request['message']
                    save(message,'confirm');pick='ok' if case=='shock-yes' else 'cancel';stage=2
                elif 'PayMana' in kind:
                    if request['seat']==1 and stage==2:
                        save(message,'payment' if manual_count==0 else 'payment-'+str(manual_count))
                        if case in ('tax-cancel','tax-insufficient'):pick='cancel';stage=3
                        elif case=='tax-auto':assert 'ok' in ids;pick='ok';paid=True;stage=3
                        else:
                            land=next(c for c in one['battlefield'] if c['name']=='Island' and not c['tapped'] and 'card:'+str(c['id']) in ids)
                            pick='card:'+str(land['id']);manual_count+=1
                            if manual_count==3:paid=True;stage=3
                    else:pick='ok'
                elif kind in ('SelectStackTarget','InputSelectTargets'):
                    targets=[i for i in ids if i.startswith('stack:')]
                    pick=targets[0] if targets and not request.get('selectedCount') else 'ok'
                elif kind=='Scry':pick=next((i for i in ids if i in ('ok','confirm-scry')),next((i for i in ids if i.startswith('scry-top:')),ids[0]))
                elif kind=='dialog':pick=ids[0]
                assert pick in ids,(case,stage,kind,pick,ids,request['message'])
                process.stdin.write(json.dumps(dict(type='respond',session=case,seat=request['seat'],requestId=request['requestId'],option=pick))+'\n');process.stdin.flush()
            raise AssertionError('Step budget')
        finally:
            process.kill();process.wait(timeout=5)

results=[]
for case in sys.argv[1:] or ['shock-yes','shock-no','tax-auto','tax-cancel','tax-insufficient','tax-manual']:
    try:result=run(case)
    except Exception:result={'case':case,'PASS':False,'error':traceback.format_exc()}
    results.append(result);print(json.dumps(result,ensure_ascii=False),flush=True)
    (OUT/'results.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
    if not result['PASS']:raise SystemExit(1)
