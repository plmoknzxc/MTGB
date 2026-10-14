"""Native discover chain: exact identities, private libraries, costs and continuation."""
import json, os, queue, subprocess, sys, tempfile, threading, time, hashlib
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime'
CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'.tools/discover-1006/native';OUT.mkdir(parents=True,exist_ok=True)
CLASSES=OUT/'classes';CLASSES.mkdir(exist_ok=True)
FIXTURE=ROOT/'Tools/ForgeBridge/tests/DiscoverFixture.java'
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(FIXTURE)],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['cast','hand','empty','repeat','seat-two','x-zero','kicker','floor','activated']

def run(case):
    actor=2 if case=='seat-two' else 1;target='Walking Ballista' if case=='x-zero' else 'Goblin Bushwhacker' if case=='kicker' else 'Shock'
    source='Hidden Volcano' if case=='activated' else 'Geological Appraiser'
    spec={'hand':[] if case=='activated' else [source]*(2 if case=='repeat' else 1),
          'field':[source] if case=='activated' else ['Trinisphere'] if case=='floor' else [],
          'top':['Mountain','Shivan Dragon']+([] if case=='empty' else [target])+(['Island','Serra Angel','Shock'] if case=='repeat' else [])}
    folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True)
    metadata={'case':case,'folder':str(folder.relative_to(ROOT)).replace('\\','/'),'runtimeHash':CFG['runtimeHash'],
              'fixtureSha256':hashlib.sha256(FIXTURE.read_bytes()).hexdigest(),'driverSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest()}
    with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'trace.jsonl').open('w',encoding='utf8') as trace,(folder/'engine.log').open('w',encoding='utf8') as log:
        audit=Path(temp)/'audit';audit.mkdir();start=Path(temp)/'start.json'
        start.write_text(json.dumps(dict(session=case,fixture=case,spec=spec,auditDir=str(audit),seed=4,firstPlayerSeat=actor,startingLife=20,decks=[DECK,DECK])),encoding='utf8')
        p=subprocess.Popen([str(RT/CFG['java']),'-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.DiscoverFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        q=queue.Queue()
        def read():
            try:
                for line in p.stdout:q.put(json.loads(line))
            finally:q.put(None)
        threading.Thread(target=read,daemon=True).start()
        def snapshot(m,label):
            v=dict(m['views'][actor-1]);v.update(request=m['request'],awaitingInput=True)
            (folder/(label+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
        def proof(r):
            path=audit/(str(r['requestId'])+'.json');deadline=time.monotonic()+5
            while time.monotonic()<deadline:
                try:
                    data=json.loads(path.read_text(encoding='utf8'));assert data['requestId']==r['requestId'];break
                except (FileNotFoundError,PermissionError,json.JSONDecodeError):time.sleep(.01)
            else:raise AssertionError('Missing complete native audit '+str(path))
            (folder/('audit-'+str(r['requestId'])+'.json')).write_text(json.dumps(data,indent=2),encoding='utf8');return data
        stage=0;cycles=0;decisions=0;reveals=0;initial=None;start_library=None;found=None;rest=None;tail=None;paid={};deadline=time.monotonic()+180
        try:
            for step in range(600):
                assert time.monotonic()<deadline,'Deadline';m=q.get(timeout=40);assert m is not None,'Native exit'
                trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ('fatal','ended','rejected'),m
                if m['type']!='request':continue
                r=m['request'];kind=r['kind'];opts=r['options'];ids=[o['id'] for o in opts];v=m['views'][actor-1];own=v['players'][actor-1];other=v['players'][2-actor];pick='ok'
                # The setup/audit observer starts after native opening choices.
                # Opening requests have no fixture audit and are not discover proof.
                a=proof(r) if stage or kind=='InputPassPriority' and v['phase']=='MAIN1' else {'players':[{'library':[]},{'library':[]}],'stack':[]}
                library=a['players'][actor-1]['library']
                for item in a['stack']:
                    if item['spell']:paid[item['id']]=item
                snapshot(m,'step-'+str(step))
                if kind=='InputPassPriority' and not v['stackItems']:
                    if stage==0 and r['seat']==actor and v['phase']=='MAIN1':
                        initial=own;start_library=library;limit=4 if case=='activated' else 3
                        found=next((c for c in library if not c['land'] and c['mv']<=limit),None)
                        index=library.index(found) if found else len(library);rest=library[:index];tail=library[index+1:] if found else []
                        snapshot(m,'before-'+str(cycles));card=next(c for c in own['battlefield'] if c['name']==source) if case=='activated' else next(c for c in own['hand'] if c['name']==source)
                        pick='card:'+str(card['id']);stage=1
                    elif stage==1 and (case=='activated' and source in own['graveyard'] or case!='activated' and sum(c['name']==source for c in own['battlefield'])==cycles+1):
                        assert reveals>=cycles+1,'No exact reveal confirmation'
                        after_ids=[c['id'] for c in library];rest_ids={c['id'] for c in rest};tail_ids=[c['id'] for c in tail]
                        assert after_ids[:len(tail)]==tail_ids,'Unrelated library order changed'
                        assert set(after_ids[len(tail):])==rest_ids and len(after_ids)==len(tail)+len(rest),'Random bottom lost/duplicated cards'
                        assert not own['exileCards'],'Discover exile retained'
                        assert not m['views'][2-actor]['players'][actor-1]['hand'],'Hand leaked'
                        if found:
                            assert decisions==cycles+1,'Wrong number of discover decisions'
                            if case=='hand':assert any(c['id']==found['id'] for c in own['hand']) and found['id'] not in paid,'Hand choice cast card'
                            else:
                                assert found['id'] in paid,'Discovered spell never cast'
                                expected=3 if case=='floor' else 1 if case=='kicker' else 0
                                assert paid[found['id']]['mana']==expected,paid[found['id']]
                                if case=='x-zero':assert paid[found['id']]['x']==0 and any(c['id']==found['id'] for c in own['graveyardCards'])
                                elif case=='kicker':assert next(c for c in own['battlefield'] if c['id']==found['id'])['power']==2
                                else:assert other['life']==20-2*(cycles+1) and any(c['id']==found['id'] for c in own['graveyardCards'])
                        else:assert decisions==0 and not paid.keys()-{c['id'] for c in initial['hand']},'Empty discover created cast'
                        expected_taps=(5 if case in ('activated','kicker') else 7 if case=='floor' else 4)*(cycles+1)
                        assert sum(c['tapped'] for c in own['battlefield'] if c['name']=='Mountain')==expected_taps,'Source/additional payment mismatch'
                        cycles+=1;snapshot(m,'resolved-'+str(cycles))
                        if case=='repeat' and cycles==1:
                            assert r['seat']==actor,'Second cast needs actor priority'
                            initial=own;start_library=library;found=next(c for c in library if not c['land'] and c['mv']<=3)
                            index=library.index(found);rest=library[:index];tail=library[index+1:]
                            snapshot(m,'before-1');pick='card:'+str(next(c for c in own['hand'] if c['name']==source)['id'])
                        else:
                            result=dict(metadata,status='PASS',result='PASS '+case+': actual discover/payment/reveal/choice/cast or hand; exact IDs, full unaffected library order/random bottom membership, private hand and next priority',cycles=cycles,decisions=decisions,reveals=reveals,paid=list(paid.values()))
                            (folder/'result.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8');(OUT/(case+'-result.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf8');return result
                elif kind=='RevealCards':
                    expected=rest+([found] if found else [])
                    assert [c['id'] for c in r['previewCards']]==[c['id'] for c in expected],'Wrong reveal sequence'
                    if r['seat']==actor:reveals+=1
                    pick='confirm-cards'
                elif kind in ('dialog','InputConfirm'):
                    labels=[o['label'] for o in opts]
                    if any(x.lower()=='cast' for x in labels):
                        assert r['seat']==actor and len(opts)==2;decisions+=1;snapshot(m,'choice-'+str(cycles));pick=next(o['id'] for o in opts if o['label'].lower()==('hand' if case=='hand' else 'cast'))
                    elif case=='kicker' and any('kicker' in x.lower() for x in labels):pick=next(o['id'] for o in opts if 'kicker' in o['label'].lower())
                    elif case=='activated' and kind=='dialog':pick=next(o['id'] for o in opts if 'Discover' in o['label'])
                    else:pick=next(i for i in ids if i not in ('cancel','no'))
                elif 'PayMana' in kind:
                    pick='ok' if 'ok' in ids else next((i for i in ids if i=='mana:R'),None) or next(o['id'] for o in opts if o['label'].startswith('Mountain ['))
                elif kind=='ChooseOptions' and case=='kicker':
                    pick='confirm-cards' if any(o.get('selected') for o in opts) else next(o['id'] for o in opts if 'Kicker' in o['label'])
                elif kind=='InputSelectTargets':pick='ok' if r.get('selectedCount') and 'ok' in ids else next(o['id'] for o in opts if o['label']=='Player '+str(3-actor))
                elif kind=='OrderAbilities':pick='confirm-order'
                elif kind=='InputMulligan':pick='keep'
                elif kind.startswith('InputSelect'):pick='ok' if 'ok' in ids else next(i for i in ids if i.startswith('card:'))
                assert pick in ids,(case,stage,kind,pick,opts)
                print(case,step,kind,pick,flush=True)
                p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
            raise AssertionError('Step limit')
        finally:
            if p.poll() is None:p.kill();p.wait()

if __name__=='__main__':
    results=[]
    for case in sys.argv[1:] or CASES:
        try:r=run(case);print(r['result'],flush=True);results.append(r)
        except Exception as error:print('FAIL '+case+': '+repr(error),flush=True);results.append(dict(case=case,status='FAIL',error=repr(error)))
    (OUT/'results.json').write_text(json.dumps(dict(status='PASS' if all(x['status']=='PASS' for x in results) else 'FAIL',results=results),ensure_ascii=False,indent=2),encoding='utf8')
    sys.exit(any(x['status']!='PASS' for x in results))
