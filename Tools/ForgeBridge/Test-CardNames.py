"""Native card naming: unrestricted/nonland, split names, reveal-limited and empty sets."""
import json,os,queue,re,shutil,subprocess,sys,tempfile,threading,time
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2];RUNTIME=ROOT/'ForgeRuntime'
CFG=json.loads((RUNTIME/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'Tools/Validation/Reports/CardNames';CLASSES=ROOT/'.tools/card-names/classes'
if OUT.exists() and (OUT/'result.txt').exists():shutil.copytree(OUT,ROOT/'.tools/card-names/attempts'/str(time.time_ns()))
OUT.mkdir(parents=True,exist_ok=True);CLASSES.mkdir(parents=True,exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RUNTIME/'classes')+';'+str(RUNTIME/'lib/*'),'-d',str(CLASSES),str(ROOT/'Tools/ForgeBridge/tests/CardNameFixture.java')],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['therapy-hit','therapy-miss','therapy-split','therapy-cancel','mage','needle-ability','needle-mana','arbiter','arbiter-empty','mage-only','mage-other','needle-only','needle-other']

def run(case):
    source='Cabal Therapy' if case.startswith('therapy') else 'Pithing Needle' if case.startswith('needle') else 'Meddling Mage' if case.startswith('mage') else 'Alhammarret, High Arbiter'
    named={'therapy-miss':'Lightning Bolt','therapy-split':'Fire','therapy-cancel':'Cancel','needle-ability':'Mogg Fanatic','needle-mana':'Mountain'}.get(case,'Shock')
    if case in ('needle-only','needle-other'):named='Mogg Fanatic'
    with tempfile.TemporaryDirectory(prefix='mtgb-name-') as temp,(OUT/(case+'.log')).open('w',encoding='utf8') as log,(OUT/(case+'.jsonl')).open('w',encoding='utf8') as trace:
        start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,fixture=case,seed=4,startingLife=20,firstPlayerSeat=1,decks=[DECK,DECK])),encoding='utf8')
        process=subprocess.Popen([str(RUNTIME/CFG['java']),'-Djava.awt.headless=true','-cp',';'.join(map(str,[CLASSES,RUNTIME/'classes',RUNTIME/'lib/*'])),'mtgb.bridge.CardNameFixture',str(RUNTIME/CFG['assets']),str(start)],cwd=RUNTIME/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        messages=queue.Queue()
        def read():
            try:
                for line in process.stdout:messages.put(json.loads(line))
            except Exception as error:messages.put(error)
            finally:messages.put(None)
        threading.Thread(target=read,daemon=True).start()
        def snapshot(m,label,seat=1):
            v=dict(m['views'][seat-1]);v.update(request=m['request'] if m['request']['seat']==seat else None,awaitingInput=m['request']['seat']==seat)
            (OUT/(case+'-'+label+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
        casts=0;choices=0;returning=False;activated=False;initial_hand=None;blocked_opponent=False;deadline=time.monotonic()+150
        try:
            for step in range(300):
                assert time.monotonic()<deadline,'Deadline'
                m=messages.get(timeout=45);assert m is not None,'Bridge exited'
                if isinstance(m,Exception):raise m
                trace.write(json.dumps(m)+'\n');trace.flush()
                assert m['type'] not in ('fatal','ended','rejected'),m
                if m['type']!='request':continue
                r=m['request'];v=m['views'][r['seat']-1];one,two=m['views'][0]['players'];own_two=m['views'][1]['players'][1]
                ids=[o['id'] for o in r['options']];kind=r['kind'];pick='ok'
                if r.get('selectionPurpose')=='cardName':
                    choices+=1;options=r['options'];names=[o['cardName'] for o in options]
                    assert r['seat']==1 and 'cancel' not in ids,'Mandatory naming can cancel'
                    # Native full-pool faces include multiple print/rules variants with
                    # the same canonical name. Submit the original candidate index.
                    if case=='arbiter':assert set(names)=={'Shock','Opt'},names
                    else:
                        assert len(names)>30000,'Full card-name list missing'
                        assert ('Mountain' in names)==case.startswith('needle'),'Nonland restriction lost'
                        assert 'Fire' in names and 'Ice' in names,'Split faces omitted'
                        assert ('Soporific Springs' in names)==case.startswith('needle'),'MDFC land face restriction lost'
                    option=next(o for o in options if o['cardName']==named);assert option['id'].startswith('choice:')
                    pick=option['id'];snapshot(m,'name-choice')
                elif kind=='InputPassPriority' and not v['stackItems']:
                    field={c['name']:c for c in one['battlefield']}
                    if casts==0 and r['seat']==1 and v['phase']=='MAIN1':
                        initial_hand=two['handCount'];snapshot(m,'before');pick='card:'+str(next(c for c in one['hand'] if c['name']==source)['id']);casts=1
                    elif casts and case.startswith('therapy') and any(c['name']==source for c in one['graveyardCards']):
                        expected=0 if case=='therapy-miss' else 1 if case=='therapy-cancel' else 2
                        discarded='Fire // Ice' if case=='therapy-split' else 'Cancel' if case=='therapy-cancel' else 'Shock'
                        assert choices==1 and two['handCount']==initial_hand-expected
                        assert len([c for c in two['graveyardCards'] if c['name']==discarded])==expected
                        assert any(c['name']=='Opt' for c in own_two['hand']),'Unmatched card discarded'
                        assert not two['hand'],'Reveal permission persists after resolution'
                        snapshot(m,'resolved');return f'PASS {case}: actual cast/target/payment/name/reveal/discard; exact {expected} named cards removed; unrelated card retained; private hand hidden; priority resumes'
                    elif casts and case.startswith('needle') and source in field:
                        assert choices==1
                        if case in ('needle-only','needle-other'):
                            if r['seat']==2:
                                mogg=next(c for c in own_two['battlefield'] if c['name']=='Mogg Fanatic')
                                assert 'card:'+str(mogg['id']) not in ids,'Named opponent ability remains actionable'
                                snapshot(m,'response',2)
                                assert v['canRespond']==(case=='needle-other'),'Wrong response availability after named ability prohibition'
                                return f'PASS {case}: actual Needle cast/payment/naming; named opposing nonmana ability blocked; response availability respects '+('independent Opt' if case.endswith('other') else 'only blocked ability and mana sources')
                            pick='ok'
                            process.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');process.stdin.flush();continue
                        mountain=next(c for c in one['battlefield'] if c['name']=='Mountain' and not c['tapped'])
                        assert 'card:'+str(mountain['id']) in ids,'Naming suppresses exempt mana ability'
                        if case=='needle-ability':
                            assert 'card:'+str(field['Mogg Fanatic']['id']) not in ids,'Named nonmana ability remains actionable'
                            snapshot(m,'resolved');return 'PASS needle-ability: real Needle cast/name; Mogg Fanatic nonmana ability blocked, Mountain mana ability remains legal; priority resumes'
                        if not activated:
                            pick='card:'+str(mountain['id']);activated=True
                        else:
                            assert one['manaPool'][3]==1,'Named Mountain mana ability did not resolve'
                            snapshot(m,'resolved');return 'PASS needle-mana: land name offered and chosen; named Mountain taps and produces exactly one red mana despite Needle; priority resumes'
                    elif casts and (case.startswith('mage') or case.startswith('arbiter')):
                        if case=='arbiter-empty' and source in field:
                            assert choices==0 and field[source]['power']==5 and field[source]['toughness']==5
                            snapshot(m,'resolved');return 'PASS arbiter-empty: real cast/payment/reveal, no nonland candidates; no unanswerable name prompt; 5/5 enters and priority resumes'
                        if source in field:
                            assert choices==1
                            owner=one if r['seat']==1 else own_two
                            shock=next(c for c in owner['hand'] if c['name']=='Shock')
                            blocked=case.startswith('mage') or r['seat']==2
                            assert ('card:'+str(shock['id']) in ids)!=blocked,('Wrong caster name permission',case,r['seat'],ids)
                            if r['seat']==2:
                                blocked_opponent=True;snapshot(m,'opponent-blocked',2)
                                if case in ('mage-only','mage-other'):
                                    snapshot(m,'response',2)
                                    assert v['canRespond']==(case=='mage-other'),'Wrong response availability after named spell prohibition'
                            if r['seat']==1 and not returning and blocked_opponent:
                                snapshot(m,'name-blocked');pick='card:'+str(next(c for c in one['hand'] if c['name']=='Unsummon')['id']);returning=True
                        elif returning:
                            assert any(c['name']==source for c in one['hand']),'Source did not return to hand'
                            shock=next(c for c in (one if r['seat']==1 else own_two)['hand'] if c['name']=='Shock')
                            assert 'card:'+str(shock['id']) in ids,'Name restriction did not expire'
                            assert not two['hand'],'Revealed hand stays visible'
                            snapshot(m,'released');return f'PASS {case}: actual naming ETB, '+('both casters blocked' if case.startswith('mage') else 'only opponent blocked; revealed nonland list deduplicated')+'; real Unsummon target/payment removes source and restores Shock casting; private sight restored'
                elif 'PayMana' in kind:
                    cost=r['message'].split('Pay Mana Cost:',1)[-1];colors=re.findall(r'\{([WUBRG])\}',cost);allowed=colors or list('WUBRGC')
                    pick=next((i for i in ids if i.startswith('mana:') and i.split(':')[1] in allowed),None)
                    if pick is None:
                        basics=dict(zip('WUBRG',['Plains','Island','Swamp','Mountain','Forest']))
                        pick=next('card:'+str(c['id']) for c in one['battlefield'] if c['name'] in [basics[x] for x in allowed if x in basics] and 'card:'+str(c['id']) in ids)
                elif kind=='InputSelectTargets':
                    if r.get('selectedCount') and 'ok' in ids:pick='ok'
                    elif returning:pick='card:'+str(next(c for c in one['battlefield'] if c['name']==source)['id'])
                    else:pick='player:'+str(two['targetId'])
                elif kind=='RevealCards':snapshot(m,'reveal');pick='confirm-cards'
                elif kind=='dialog':pick=ids[0]
                elif kind=='OrderAbilities':pick='confirm-order'
                assert pick in ids,(case,kind,pick,ids[:12])
                process.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');process.stdin.flush()
            raise AssertionError('No completion')
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
    (OUT/'result.txt').write_text('\n'.join(results),encoding='utf8');sys.exit(any(r.startswith('FAIL') for r in results))
