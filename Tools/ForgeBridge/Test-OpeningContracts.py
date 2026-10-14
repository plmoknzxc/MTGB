"""Official extra starting hands and separately labeled GUI adapter contracts."""
import json
import os
from pathlib import Path
import queue
import subprocess
import sys
import tempfile
import threading
import time

ROOT=Path(__file__).resolve().parents[2];RUNTIME=ROOT/'ForgeRuntime'
CFG=json.loads((RUNTIME/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'Tools/Validation/Reports/OpeningContracts';CLASSES=ROOT/'.tools/opening-contracts/classes'
OUT.mkdir(parents=True,exist_ok=True);CLASSES.mkdir(parents=True,exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RUNTIME/'classes')+';'+str(RUNTIME/'lib/*'),'-d',str(CLASSES),
 str(ROOT/'Tools/ForgeBridge/tests/StartingHandFixture.java'),str(ROOT/'Tools/ForgeBridge/tests/CallbackContractFixture.java')],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
STARTING=['accept','rotate-two','rotate-cycle']
ADAPTER=['empty','display','zero','insert-first','insert-middle','insert-last']


def run(case):
    starting=case in STARTING
    with tempfile.TemporaryDirectory(prefix='mtgb-opening-') as temp,(OUT/(case+'.log')).open('w',encoding='utf8') as log,(OUT/(case+'.jsonl')).open('w',encoding='utf8') as trace:
        start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,fixture=case,seed=4,startingLife=20,firstPlayerSeat=1,decks=[DECK,DECK])),encoding='utf8')
        fixture='StartingHandFixture' if starting else 'CallbackContractFixture'
        process=subprocess.Popen([str(RUNTIME/CFG['java']),'-Djava.awt.headless=true','-cp',';'.join(map(str,[CLASSES,RUNTIME/'classes',RUNTIME/'lib/*'])),
            'mtgb.bridge.'+fixture,str(RUNTIME/CFG['assets']),str(start)],cwd=RUNTIME/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),
            stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        messages=queue.Queue()
        def read():
            try:
                for line in process.stdout:messages.put(json.loads(line))
            except Exception as error:messages.put(error)
            finally:messages.put(None)
        threading.Thread(target=read,daemon=True).start()
        def snap(message,label):
            view=dict(message['views'][0]);view.update(request=message['request'],awaitingInput=True)
            (OUT/(case+'-'+label+'.json')).write_text(json.dumps(view,ensure_ascii=False,indent=2),encoding='utf8')
        rotations=0;initial=None;accepted=None;selections=0;reported=False;deadline=time.monotonic()+60
        try:
            for step in range(100):
                assert time.monotonic()<deadline,'Scenario deadline'
                message=messages.get(timeout=20);assert message is not None,'Bridge exited'
                if isinstance(message,Exception):raise message
                trace.write(json.dumps(message)+'\n');trace.flush()
                assert message['type'] not in ('fatal','rejected','ended'),message
                if message['type']=='callback-contract-result':
                    assert not starting and message['case']==case and message['status']=='PASS';reported=True;continue
                if message['type']!='request':continue
                r=message['request'];ids=[o['id'] for o in r['options']];kind=r['kind'];pick='ok'
                assert ids,'Published an unanswerable empty request'
                if kind=='InputChooseStartingHand':
                    assert starting and r['seat']==1 and set(ids)=={'ok','cancel'}
                    one=message['views'][0]['players'][0];hand={c['id'] for c in one['hand']};assert len(hand)==7
                    assert not message['views'][1]['players'][0]['hand'],'Other viewer sees a hidden starting hand'
                    copies=2 if case=='rotate-two' else 1
                    assert one['libraryCount']==60-7*(copies+1)
                    if initial is None:initial=hand
                    elif case=='rotate-cycle' and rotations==2:assert hand==initial,'Hand rotation did not cycle'
                    else:assert hand.isdisjoint(initial),'View next retained the initial hand'
                    snap(message,'hand-'+str(rotations))
                    wanted=0 if case=='accept' else 2
                    if rotations<wanted:pick='ok';rotations+=1
                    else:pick='cancel';accepted=hand
                elif kind in ('InputMulligan','InputConfirmMulligan'):pick='ok'
                elif kind=='InputPassPriority':
                    if starting:
                        one=message['views'][0]['players'][0]
                        assert accepted is not None and {c['id'] for c in one['hand']}==accepted
                        assert one['libraryCount']==53 and one['handCount']==7
                        assert message['views'][0]['players'][1]['libraryCount']==53
                        assert message['views'][0]['players'][1]['handCount']==7
                        snap(message,'resolved')
                        return 'PASS '+case+': official Backup Plan initialization, private hand rotation/acceptance, unchosen hands returned, ordinary mulligan and priority continue'
                    assert reported,'Callback result missing'
                    if case=='empty':assert selections==0,'Empty contract created an unnecessary dialog'
                    snap(message,'resolved')
                    return 'PASS adapter '+case+': original object/bounds/order contract, no empty pending prompt, normal priority resumes; not a card-effect test'
                elif kind=='dialog':
                    assert not starting
                    snap(message,'dialog-'+str(selections))
                    if case=='display':
                        assert [o['label'] for o in r['options']]==['display a','display b'];pick='choice:1'
                    elif case.startswith('insert-'):
                        pick='choice:'+str({'insert-first':0,'insert-middle':1,'insert-last':2}[case])
                    else:raise AssertionError(('Unexpected dialog',case,r))
                    selections+=1
                elif kind=='ChooseOptions':
                    assert not starting;snap(message,'choices-'+str(selections))
                    if case=='zero':
                        assert r['minCount']==r['maxCount']==r['selectedCount']==0;pick='confirm-cards'
                    elif case=='display':
                        assert [o['label'] for o in r['options'] if o['id'].startswith('select:')]==['display a','display b']
                        pick='select:1' if selections==1 else 'confirm-cards'
                        assert r['selectedCount']==1 and r['options'][0 if selections==1 else 1]['selected']
                    else:raise AssertionError((case,r))
                    selections+=1
                else:raise AssertionError((case,kind,r))
                assert pick in ids,(case,kind,pick,ids)
                process.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');process.stdin.flush()
            raise AssertionError('No completion after 100 requests')
        finally:
            if process.poll() is None:
                process.stdin.write('{"type":"shutdown"}\n');process.stdin.flush()
                try:process.wait(timeout=3)
                except subprocess.TimeoutExpired:process.kill();process.wait()


if __name__=='__main__':
    results=[]
    for case in sys.argv[1:] or STARTING+ADAPTER:
        try:result=run(case)
        except Exception as error:result='FAIL '+case+': '+repr(error)
        results.append(result);print(result,flush=True)
    (OUT/'result.txt').write_text('\n'.join(results),encoding='utf8')
    sys.exit(any(r.startswith('FAIL') for r in results))
