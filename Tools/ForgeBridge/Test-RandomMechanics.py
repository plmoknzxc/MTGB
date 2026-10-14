"""Native seeded dice/coin effects. Read-only audit observes rather than sets random results."""
import json,os,queue,re,shutil,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'Tools/Validation/Reports/RandomMechanics';CLASSES=ROOT/'.tools/random-mechanics/classes'
if (OUT/'result.txt').exists():shutil.copytree(OUT,ROOT/'.tools/random-mechanics/attempts'/str(time.time_ns()))
OUT.mkdir(parents=True,exist_ok=True);CLASSES.mkdir(parents=True,exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(ROOT/'Tools/ForgeBridge/tests/RandomMechanicFixture.java')],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['dice-low','dice-mid','dice-high','pixie','monitor-keep','monitor-reroll','xeno-inc','xeno-dec','night-inc','night-dec','swap-power','swap-toughness','coin-win','coin-lose','thumb-win','thumb-lose','mana-clash','notice-private','notice-cap']
SEEDS={'dice-low':4,'dice-mid':0,'dice-high':28,'xeno-dec':5}
BASELINE='--baseline' in sys.argv

def run(case,seed=None,discover=False):
    source='Shock' if case.startswith(('coin','thumb')) else 'Mana Clash' if case=='mana-clash' else 'Ebony Fly' if case.startswith('swap') else 'Contact Other Plane'
    with tempfile.TemporaryDirectory(prefix='mtgb-random-') as temp,(OUT/(case+'.log')).open('w',encoding='utf8') as log,(OUT/(case+'.jsonl')).open('w',encoding='utf8') as trace:
        audit=Path(temp)/'audit';audit.mkdir();start=Path(temp)/'start.json'
        start.write_text(json.dumps(dict(session=case,fixture=case,auditDir=str(audit),seed=seed if seed is not None else SEEDS.get(case,4),startingLife=20,firstPlayerSeat=1,decks=[DECK,DECK])),encoding='utf8')
        p=subprocess.Popen([str(RT/CFG['java']),'-Djava.awt.headless=true','-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.RandomMechanicFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        messages=queue.Queue()
        def read():
            try:
                for line in p.stdout:messages.put(json.loads(line))
            except Exception as error:messages.put(error)
            finally:messages.put(None)
        threading.Thread(target=read,daemon=True).start()
        def snapshot(m,label):
            view=dict(m['views'][m['request']['seat']-1]);view.update(request=m['request'],awaitingInput=True)
            (OUT/(case+'-'+label+'.json')).write_text(json.dumps(view,ensure_ascii=False,indent=2),encoding='utf8')
        def proof(r,label):
            path=audit/(str(r['requestId'])+'.json');limit=time.monotonic()+5
            while not path.exists() and time.monotonic()<limit:time.sleep(.01)
            assert path.exists(),'Missing random audit'
            data=json.loads(path.read_text());assert data['requestId']==r['requestId'];(OUT/(case+'-'+label+'-audit.json')).write_text(json.dumps(data,indent=2),encoding='utf8')
            return data['players']
        stage=0;paid=0;roll_choice=None;scry_count=0;modified=0;rerolls=0;thumb_choice=0;initial=None;initial_library=None;source_id=None;deadline=time.monotonic()+160;outcomes=[]
        try:
            for step in range(500):
                assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=40);assert m is not None,'Bridge exited'
                if isinstance(m,Exception):raise m
                trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ('fatal','ended','rejected'),m
                if m['type']!='request':continue
                r=m['request'];kind=r['kind'];v=m['views'][r['seat']-1];one=v['players'][0];two=v['players'][1];ids=[o['id'] for o in r['options']];pick='ok'
                if case.startswith('notice') and kind=='InputPassPriority':
                    a,b=[x.get('notices',[]) for x in m['views']];snapshot(m,'resolved')
                    other=dict(m['views'][1]);other.update(awaitingInput=False);(OUT/(case+'-other.json')).write_text(json.dumps(other,ensure_ascii=False,indent=2),encoding='utf8')
                    assert all(n['kind']=='notice' and not n['publicNotice'] for n in a+b)
                    if case=='notice-private':
                        assert len(a)==len(b)==1 and a[0]['value']=='Owner-only value' and b[0]['value']=='Other recipient value'
                        assert a[0]['sourceCard']['name']=='Contact Other Plane' and not b[0].get('sourceCard') and b[0]['title']==''
                    else:
                        assert len(a)==16 and not b and [n['value'] for n in a]==['Private value '+str(i) for i in range(4,20)]
                        assert len({n['id'] for n in a})==16
                    return 'adapter contract only: native recipient exclusion, hidden source omitted, RollDice API outside public call site remains private, bounded notification IDs'
                if kind=='InputPassPriority' and not v['stackItems']:
                    if stage==0 and r['seat']==1 and v['phase']=='MAIN1':
                        initial={c['id'] for c in one['hand']};initial_library=proof(r,'before')[0]['library'];snapshot(m,'before')
                        source_id=next(c['id'] for c in (one['battlefield'] if case.startswith('swap') else one['hand']) if c['name']==source);pick='card:'+str(source_id);stage=1
                    elif stage==1 and ((case.startswith('swap') and next(c for c in one['battlefield'] if c['id']==source_id).get('creature')) or (case.startswith(('coin','thumb')) and (two['life']<20 or any(c['id']==source_id for c in one['hand']))) or (not case.startswith(('coin','thumb','swap')) and any(c['id']==source_id for c in one['graveyardCards']))):
                        state=proof(r,'resolved');snapshot(m,'resolved');assert not m['views'][1]['players'][0]['hand'],'Private hand leak'
                        cost=1 if case.startswith(('coin','thumb')) or case=='mana-clash' else 4+(1 if case=='monitor-reroll' else 0)
                        assert paid==cost and sum(one['manaPool'])==0,('Payment',paid,cost)
                        if case.startswith(('coin','thumb')):
                            assert state[0]['flipped']==1 and state[1]['flipped']==0
                            win=two['life']==16;assert two['life'] in (16,20)
                            assert (any(c['id']==source_id for c in one['graveyardCards']))==win
                            assert (source_id in {c['id'] for c in one['hand']})== (not win)
                            if not discover:assert win==case.endswith('win')
                            if case.startswith('thumb'):assert thumb_choice==1,'Thumb never offered two distinct outcomes'
                            if discover:return dict(win=win,thumb_choice=thumb_choice)
                        elif case=='mana-clash':
                            if BASELINE:return dict(flips=[x['flipped'] for x in state],life=[one['life'],two['life']])
                        else:
                            values=state[0]['rolls'];result=values[-1];physical=2 if case in ('pixie','monitor-reroll') else 1
                            assert state[0]['rolled']==physical and len(values)==1
                            if case.startswith('monitor'):assert rerolls==(1 if case.endswith('reroll') else 0)
                            if case.startswith('swap'):
                                expect=4 if case.endswith('power') else 5;assert result==expect
                                whacker=next(c for c in one['battlefield'] if c['name']=='Vedalken Squirrel-Whacker');fly=next(c for c in one['battlefield'] if c['id']==source_id)
                                assert fly['power']==expect and fly['toughness']==expect
                                assert whacker['power']==(roll_choice if case.endswith('power') else 4) and whacker['toughness']==(roll_choice if case.endswith('toughness') else 5)
                            else:
                                draw=3 if result==20 else 2 if 1<=result<=19 else 0;expected_scry=3 if result==20 else 2 if 10<=result<=19 else 0
                                assert scry_count==expected_scry and len(one['hand'])==len(initial)-1+draw
                                hand={c['id'] for c in one['hand']};after=state[0]['library']
                                assert set(after)==set(initial_library)-(hand-initial) and len(after)==len(initial_library)-draw
                                assert after==initial_library[draw:] and hand-initial==set(initial_library[:draw])
                                if case.startswith(('xeno','night')):
                                    assert modified==1 and result==roll_choice+(1 if case.endswith('inc') else -1)
                                    if case.startswith('xeno'):
                                        x=next(c for c in one['battlefield'] if c['name']=='Xenosquirrels');assert x['power']==1 and x['toughness']==1 and sum(c['count'] for c in x['counters'] if c['kind']=='P1P1')==1
                                    else:assert one['life']==19
                                if not discover and case.startswith('dice'):
                                    assert (result<=9 if case.endswith('low') else 10<=result<=19 if case.endswith('mid') else result==20),result
                            if discover:return dict(result=result,scry=scry_count,physical=physical)
                        if not BASELINE:
                            notes=v.get('notices',[]);assert notes,'Native result notification discarded'
                            assert len(notes)<=16 and len({n['id'] for n in notes})==len(notes)
                            notes=[n for n in notes if n['kind'] in ('dice','coin')];assert notes and all(n['publicNotice'] for n in notes)
                            other_notes=[n for n in m['views'][1].get('notices',[]) if n['kind'] in ('dice','coin')]
                            assert [(n['kind'],n['value'],n['actorSeat']) for n in notes]==[(n['kind'],n['value'],n['actorSeat']) for n in other_notes]
                            if case=='pixie':assert 'Ignored rolls:' in notes[-1]['value']
                            assert all(n['value'] and n.get('sourceCard',{}).get('name')==('Krark, the Thumbless' if case.startswith(('coin','thumb')) else source) for n in notes)
                            if case=='mana-clash':
                                assert len(notes)<16 and len(notes)%2==0 and [n['value'].lower() for n in notes[-2:]]==['heads','heads']
                                for seat,player in enumerate((one,two),1):assert player['life']==20-sum(n['actorSeat']==seat and n['value'].lower()=='tails' for n in notes)
                            elif case.startswith(('coin','thumb')):assert notes[-1]['value'].lower()==('win' if case.endswith('win') else 'lose')
                        return 'native activation/cast, target/payment, random branch and exact result zones/counts; optional/replacement decisions; private proof and result notices; priority resumes'
                elif 'PayMana' in kind:
                    cost=r.get('remainingManaCost') or r['message'].split('Pay Mana Cost:',1)[-1];colors=re.findall(r'\{([WUBRG])\}',cost);allowed=colors or list('WUBRGC')
                    pick=next((i for i in ids if i.startswith('mana:') and i.split(':')[1] in allowed),None)
                    if pick is None:
                        basics=dict(zip('WUBRG',['Plains','Island','Swamp','Mountain','Forest']))
                        pick=next('card:'+str(c['id']) for c in one['battlefield'] if c['name'] in [basics[x] for x in allowed if x in basics] and 'card:'+str(c['id']) in ids)
                    paid+=1
                elif kind=='InputSelectTargets':pick='ok' if r.get('selectedCount') and 'ok' in ids else 'player:'+str(two['targetId'])
                elif kind=='Scry':
                    if not scry_count:scry_count=r['selectedCount']+r['remainingCount'];snapshot(m,'scry')
                    pick='confirm-scry' if 'confirm-scry' in ids else next(i for i in ids if i.startswith('scry-top:'))
                elif kind=='ChooseOptions' and 'reroll' in r['message'].lower():
                    snapshot(m,'reroll-selected' if r.get('selectedCount') else 'reroll-choice')
                    if case.endswith('keep') or r.get('selectedCount'):pick='confirm-cards'
                    else:pick=next(i for i in ids if i.startswith('select:'));rerolls+=1
                elif kind=='InputConfirm' and 'increase or decrease' in r['message'].lower():
                    modified+=1;snapshot(m,'increment-choice');pick=next(o['id'] for o in r['options'] if ('increase' if case.endswith('inc') else 'decrease') in o['label'].lower())
                elif kind=='InputConfirm' and 'call coin flip' in r['message'].lower():
                    snapshot(m,'call-choice');pick=next(o['id'] for o in r['options'] if o['label']==('tails' if case=='coin-win' else 'heads'))
                elif kind=='dialog':
                    text=r['message'].lower();labels={o['label'].lower():o['id'] for o in r['options']}
                    if 'choose a roll to modify' in text or 'choose a roll to swap' in text:
                        snapshot(m,'roll-choice');roll_choice=int(r['options'][0]['label']);pick=ids[0]
                    elif 'increase or decrease' in text:
                        modified+=1;snapshot(m,'increment-choice');pick=next(o['id'] for o in r['options'] if ('increase' if case.endswith('inc') else 'decrease') in o['label'].lower())
                    elif 'power or toughness' in text:
                        snapshot(m,'swap-choice');pick=next(o['id'] for o in r['options'] if o['label'].lower()==('power' if case.endswith('power') else 'toughness'))
                    elif 'choose a result' in text:
                        thumb_choice+=1;snapshot(m,'thumb-choice');pick=labels['heads' if case.endswith('win') else 'tails']
                    elif 'call coin flip' in text or 'flip' in text and set(labels)=={'heads','tails'}:
                        snapshot(m,'call-choice');pick=labels['heads']
                    elif 'new targets' in text:pick=labels.get('no',ids[-1])
                    elif 'getabilitytoplay' in text:pick=next(o['id'] for o in r['options'] if 'roll a d6' in o['label'].lower())
                    else:pick=ids[0]
                elif kind in ('InputSelectCardsFromList','InputSelectEntitiesFromList','SelectCards'):
                    pick=('confirm-cards' if kind=='SelectCards' else 'ok') if r.get('selectedCount') and ('ok' in ids or 'confirm-cards' in ids) else next(i for i in ids if i.startswith(('card:','select:')))
                elif kind=='RevealCards':pick='confirm-cards'
                elif kind=='OrderAbilities':pick='confirm-order'
                assert pick in ids,(case,kind,pick,r['message'],ids[:12])
                p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
            raise AssertionError('No completion')
        finally:
            if p.poll() is None:
                p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
                try:p.wait(timeout=3)
                except subprocess.TimeoutExpired:p.kill();p.wait()

if __name__=='__main__':
    if '--discover' in sys.argv:
        found={'low':4,'mid':0}
        for seed in range(16,80):
            result=run('dice-low',seed,True);key='high' if result['result']==20 else 'mid' if result['result']>=10 else 'low';found.setdefault(key,seed)
            print('DISCOVER',seed,result,found,flush=True)
            if len(found)==3:break
        (OUT/'seeds.json').write_text(json.dumps(found,indent=2),encoding='utf8');sys.exit(len(found)!=3)
    results=[]
    for case in [x for x in sys.argv[1:] if x not in ('--baseline',)] or CASES:
        try:result='PASS '+case+': '+str(run(case))
        except Exception as error:
            result='FAIL '+case+': '+repr(error);(OUT/(case+'-error.txt')).write_text(traceback.format_exc(),encoding='utf8')
        print(result,flush=True);results.append(result)
    (OUT/'result.txt').write_text('\n'.join(results),encoding='utf8');sys.exit(any(x.startswith('FAIL') for x in results))
