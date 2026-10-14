"""Real casting and effect continuation through the generic multi-choice adapter."""
import json
import os
from pathlib import Path
import queue
import re
import subprocess
import sys
import tempfile
import threading
import time

ROOT=Path(__file__).resolve().parents[2]
RUNTIME=ROOT/'ForgeRuntime'
CFG=json.loads((RUNTIME/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'Tools/Validation/Reports/GenericChoices'
CLASSES=ROOT/'.tools/generic-choices/classes'
OUT.mkdir(parents=True,exist_ok=True);CLASSES.mkdir(parents=True,exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RUNTIME/'classes')+';'+str(RUNTIME/'lib/*'),
                '-d',str(CLASSES),str(ROOT/'Tools/ForgeBridge/tests/GenericChoiceFixture.java')],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['colors','villain','villain-repeat']


def run(case):
    with tempfile.TemporaryDirectory(prefix='mtgb-generic-') as temp,(OUT/(case+'.log')).open('w',encoding='utf8') as log,(OUT/(case+'.jsonl')).open('w',encoding='utf8') as trace:
        start=Path(temp)/'start.json'
        start.write_text(json.dumps(dict(session=case,fixture=case,seed=4,startingLife=20,firstPlayerSeat=1,decks=[DECK,DECK])),encoding='utf8')
        process=subprocess.Popen([str(RUNTIME/CFG['java']),'-Djava.awt.headless=true','-cp',';'.join(map(str,[CLASSES,RUNTIME/'classes',RUNTIME/'lib/*'])),
            'mtgb.bridge.GenericChoiceFixture',str(RUNTIME/CFG['assets']),str(start)],cwd=RUNTIME/CFG['assets'],
            env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        messages=queue.Queue()
        def read():
            try:
                for line in process.stdout: messages.put(json.loads(line))
            except Exception as error: messages.put(error)
            finally: messages.put(None)
        threading.Thread(target=read,daemon=True).start()
        def snapshot(message,label):
            view=dict(message['views'][message['request']['seat']-1]);view.update(request=message['request'],awaitingInput=True)
            (OUT/(case+'-'+label+'.json')).write_text(json.dumps(view,ensure_ascii=False,indent=2),encoding='utf8')
        casts=0;choice_step=0;completed_choices=0;deadline=time.monotonic()+100
        try:
            for step in range(400):
                assert time.monotonic()<deadline,'Scenario deadline'
                message=messages.get(timeout=30)
                assert message is not None,'Bridge exited; inspect log'
                if isinstance(message,Exception):raise message
                trace.write(json.dumps(message)+'\n');trace.flush()
                assert message['type'] not in ('fatal','ended','rejected'),message
                if message['type']!='request':continue
                request=message['request'];view=message['views'][request['seat']-1]
                one,two=message['views'][0]['players'];options=request['options'];ids=[o['id'] for o in options];kind=request['kind'];pick='ok'
                if kind=='InputPassPriority' and not view['stackItems']:
                    if case=='colors':
                        if casts==2:assert one['life']==21 and two['life']==18,(one['life'],two['life'])
                        if casts==3:
                            bear=next(c for c in one['battlefield'] if c['name']=='Grizzly Bears')
                            assert one['life']==21 and bear['power']==5 and bear['toughness']==5
                            assert completed_choices==1;snapshot(message,'resolved')
                            return 'PASS colors: actual Tablet cast; exact two-color minimum/maximum, deselect/reset, red spell gains one life, unchosen green spell gains none, both spells resolve and priority resumes'
                        if request['seat']==1 and view['phase']=='MAIN1':
                            name=['Tablet of the Guilds','Shock','Giant Growth'][casts]
                            pick='card:'+str(next(c for c in one['hand'] if c['name']==name)['id']);casts+=1
                    elif casts and completed_choices:
                        bear=next(c for c in two['battlefield'] if c['name']=='Grizzly Bears')
                        assert bear['power']==1 and bear['toughness']==1 and 'Human' in bear['currentType']
                        copies=[c for c in one['battlefield'] if c.get('token') and c['name']=='Grizzly Bears']
                        assert completed_choices==(2 if case=='villain-repeat' else 1)
                        if case=='villain-repeat':assert len(copies)==1 and copies[0]['power']==2 and copies[0]['toughness']==2 and 'Bear' in copies[0]['currentType']
                        else:assert not copies
                        snapshot(message,'resolved')
                        return 'PASS '+case+': real Hunted cast/target/payment; opponent selects actual spell ability; creature becomes Human 1/1; '+('second choice creates separate 2/2 Bear token from copiable original values; ' if copies else '')+'all selected effects resolve and priority resumes'
                    elif request['seat']==1 and view['phase']=='MAIN1' and casts==0:
                        pick='card:'+str(next(c for c in one['hand'] if c['name']=='Hunted by The Family')['id']);casts=1
                elif kind=='ChooseOptions':
                    snapshot(message,'choices-'+str(completed_choices)+'-'+str(choice_step))
                    if case=='colors':
                        assert request['seat']==1 and request['minCount']==2 and request['maxCount']==2
                        assert len([o for o in options if o['id'].startswith('select:')])==5
                        expected=[0,1,0,1,2,0,1,2]
                        assert request['selectedCount']==expected[choice_step]
                        assert ('confirm-cards' in ids)==(request['selectedCount']==2)
                        sequence=['RED','RED','RED','BLUE','reset-cards','RED','BLUE','confirm-cards']
                        action=sequence[choice_step]
                        pick=action if action in ids else next(o['id'] for o in options if o['label'].upper()==action)
                        if pick=='confirm-cards':completed_choices+=1
                    else:
                        assert request['seat']==2 and request['minCount']==1 and request['maxCount']==1
                        assert ('confirm-cards' in ids)==bool(request['selectedCount'])
                        if request['selectedCount']:pick='confirm-cards';completed_choices+=1
                        else:
                            phrase='1/1 white Human' if completed_choices==0 else "token that's a copy"
                            pick=next(o['id'] for o in options if phrase in o['label'])
                    choice_step+=1
                elif 'PayMana' in kind:
                    cost=request['message'].split('Pay Mana Cost:',1)[-1]
                    colors=re.findall(r'\{([WUBRG])\}',cost);allowed=colors or list('WUBRGC')
                    pick=next((i for i in ids if i.startswith('mana:') and i.split(':')[1] in allowed),None)
                    if pick is None:
                        basics=dict(zip('WUBRG',['Plains','Island','Swamp','Mountain','Forest']))
                        pick=next('card:'+str(c['id']) for c in one['battlefield'] if c['name'] in [basics[x] for x in allowed if x in basics] and 'card:'+str(c['id']) in ids)
                elif kind=='InputSelectTargets':
                    if request.get('selectedCount') and 'ok' in ids:pick='ok'
                    elif case=='colors' and casts==2:pick='player:'+str(two['targetId'])
                    else:
                        owner=1 if case=='colors' else 2
                        target=next(c for c in (one if owner==1 else two)['battlefield'] if c['name']=='Grizzly Bears')
                        pick='card:'+str(target['id'])
                elif kind=='dialog':pick=ids[0]
                elif kind=='OrderAbilities':pick='confirm-order'
                elif kind=='RevealCards':pick='confirm-cards'
                assert pick in ids,(case,casts,choice_step,kind,pick,options)
                process.stdin.write(json.dumps(dict(type='respond',session=case,seat=request['seat'],requestId=request['requestId'],option=pick))+'\n');process.stdin.flush()
            raise AssertionError('No completion after 400 requests')
        finally:
            if process.poll() is None:
                process.stdin.write('{"type":"shutdown"}\n');process.stdin.flush()
                try:process.wait(timeout=3)
                except subprocess.TimeoutExpired:process.kill();process.wait()


if __name__=='__main__':
    results=[]
    for case in sys.argv[1:] or CASES:
        try:result=run(case)
        except Exception as error:result='FAIL '+case+': '+repr(error)
        print(result,flush=True);results.append(result)
    (OUT/'result.txt').write_text('\n'.join(results),encoding='utf8')
    sys.exit(any(r.startswith('FAIL') for r in results))
