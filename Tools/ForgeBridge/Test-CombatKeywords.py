"""Native R29 combat outcomes. Each producer retains its source/runtime and raw trace."""
import hashlib,json,os,queue,shutil,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
RT=ROOT/'ForgeRuntime'
CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'.tools/combat-keywords-1007/native'
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
SPECS={
 'protection-prevent':(['Vampire Hexmage'],['White Knight']),
 'protection-block':(['White Knight'],['Vampire Hexmage','Wall of Air']),
 'indestructible':(['Grizzly Bears'],['Darksteel Myr']),
 'infect-creature':(['Cystbearer'],['Wall of Air']),
 'infect-death':(['Cystbearer'],['Grizzly Bears']),
 'infect-player':(['Cystbearer'],[]),
 'wither-creature':(['Boggart Ram-Gang'],['Wall of Air']),
 'wither-death':(['Boggart Ram-Gang'],['Grizzly Bears']),
 'banding-defense':(['Colossal Dreadmaw'],['Benalish Hero','Grizzly Bears']),
 'banding-attack-probe':(['Benalish Hero','Grizzly Bears'],['Colossal Dreadmaw']),
}
CASES=[c for c in SPECS if not c.endswith('-probe')]

def sha(p):return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def write(p,v):Path(p).write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def counters(obj):return {c['kind']:c['count'] for c in obj.get('counters',[])}

def run(case):
    folder=OUT/case/str(time.time_ns());folder.mkdir(parents=True)
    fixtures=['StateBranchFixture.java','CombatKeywordFixture.java']
    for name in fixtures:shutil.copyfile(ROOT/'Tools/ForgeBridge/tests'/name,folder/name)
    shutil.copyfile(__file__,folder/'driver.py')
    identity=dict(case=case,folder=folder.relative_to(ROOT).as_posix(),runtimeHash=CFG['runtimeHash'],revision=CFG['revision'],driverSha256=sha(folder/'driver.py'),fixtures={n:sha(folder/n) for n in fixtures},assertions=[])
    classes=folder/'classes';classes.mkdir()
    subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),*[str(folder/n) for n in fixtures]],check=True)
    p=None
    try:
        with tempfile.TemporaryDirectory(prefix='mtgb-combat-keywords-') as temp,(folder/'stderr.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
            audit=folder/'audit';audit.mkdir();actor_names,defender_names=SPECS[case]
            start=dict(session=case,fixture=case,auditDir=str(audit),seed=4,startingLife=20,firstPlayerSeat=1,decks=[DECK,DECK],own={'Hand':['Negate'],'Battlefield':actor_names},other={'Hand':['Negate'],'Battlefield':defender_names})
            write(folder/'start.json',start)
            p=subprocess.Popen([str(RT/CFG['java']),'-Xmx768m','-XX:ActiveProcessorCount=4','-Djava.awt.headless=true','-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.CombatKeywordFixture',str(RT/CFG['assets']),str(folder/'start.json')],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
            messages=queue.Queue()
            def reader():
                try:
                    for line in p.stdout:messages.put(json.loads(line))
                except Exception as e:messages.put(e)
                finally:messages.put(None)
            threading.Thread(target=reader,daemon=True).start()
            def proof(r,label):
                path=audit/(str(r['requestId'])+'.json');end=time.monotonic()+5
                while not path.exists() and time.monotonic()<end:time.sleep(.01)
                assert path.exists(),('Missing native observer',r['requestId'])
                data=json.loads(path.read_text());assert data['requestId']==r['requestId'];write(folder/(label+'-audit.json'),data);return data
            def snapshot(m,label):
                r=m['request'];v=dict(m['views'][r['seat']-1]);v.update(request=r,awaitingInput=True);write(folder/(label+'.json'),v)
            seen=set();initial=None;before=None;hands=None;attacked=set();pairs=set();contexts={};probe_clicked=False;block_seen=False
            deadline=time.monotonic()+160
            for step in range(260):
                assert time.monotonic()<deadline,'Deadline'
                m=messages.get(timeout=35)
                if isinstance(m,Exception):raise m
                assert m is not None,'Bridge exited';trace.write(json.dumps(m)+'\n');trace.flush()
                assert m['type'] not in ('fatal','ended','rejected'),m
                if m['type']!='request':continue
                r=m['request']
                if r['requestId'] in seen:continue
                seen.add(r['requestId']);v=m['views'][0];players=v['players'];one,two=players;kind=r['kind'];ids=[o['id'] for o in r['options']];pick='ok'
                assert not m['views'][1]['players'][0]['hand'] and not m['views'][0]['players'][1]['hand'],'Private hand leak'
                if kind=='InputPassPriority':
                    if initial is None and r['seat']==1 and v['phase']=='MAIN1':
                        initial=players;before=proof(r,'before');hands=[{c['id'] for c in m['views'][s]['players'][s]['hand']} for s in range(2)];snapshot(m,'before')
                        assert [c['name'] for c in one['battlefield']]==actor_names
                        assert [c['name'] for c in two['battlefield']]==defender_names
                    if initial is not None and attacked and v['phase']=='MAIN2' and not v['stackItems']:
                        after=proof(r,'resolved');snapshot(m,'resolved')
                        assert [p['library'] for p in before['players']]==[p['library'] for p in after['players']],'Library mutation'
                        expected_life=(20,18) if case=='protection-block' else (20,20)
                        assert tuple(p['life'] for p in players)==expected_life,('Life',case,[p['life'] for p in players])
                        poison=2 if case=='infect-player' else 0
                        assert counters(two)==({'POISON':poison} if poison else {}),('Player counters',two['counters'])
                        assert after['players'][1]['counters']==counters(two) and not counters(one)
                        dead_actor={'Vampire Hexmage'} if case=='protection-prevent' else set()
                        dead_other={'Grizzly Bears'} if case.endswith('-death') else {'Benalish Hero'} if case=='banding-defense' else set()
                        for s,pv in enumerate(players):
                            dead=dead_actor if s==0 else dead_other
                            orig={c['id']:c for c in initial[s]['battlefield']};gone={i for i,c in orig.items() if c['name'] in dead}
                            assert {c['id'] for c in pv['battlefield']}==set(orig)-gone,('Field identities',case,s)
                            assert {c['id'] for c in pv['graveyardCards']}==gone,('Grave identities',case,s)
                            assert not pv['exileCards'] and sum(pv['manaPool'])==0 and sum(pv['snowManaPool'])==0
                            assert {c['id'] for c in m['views'][s]['players'][s]['hand']}==hands[s]
                            assert after['players'][s]['life']==pv['life']
                            for c in pv['battlefield']:
                                native=next(n for n in after['cards'] if n['id']==c['id'] and n['zone']=='Battlefield')
                                assert (c['power'],c['toughness'],c['damage'],counters(c))==(native['power'],native['toughness'],native['damage'],native['counters'])
                        if case in ('infect-creature','wither-creature'):
                            n=2 if case.startswith('infect') else 3;c=two['battlefield'][0]
                            assert (c['power'],c['toughness'],c['damage'],counters(c))==(1-n,5-n,0,{'M1M1':n})
                            assert one['battlefield'][0]['damage']==1
                        if case=='indestructible':assert two['battlefield'][0]['damage']==2 and one['battlefield'][0]['damage']==0
                        if case=='protection-prevent':assert two['battlefield'][0]['damage']==0 and not counters(two['battlefield'][0])
                        if case.endswith('-death'):assert one['battlefield'][0]['damage']==2
                        if case=='banding-defense':
                            assert len(contexts)==1 and block_seen and len(pairs)==2
                            assert one['battlefield'][0]['damage']==3 and two['battlefield'][0]['damage']==0
                        if case=='protection-block':assert block_seen and not pairs
                        identity['assertions']=['actual native attack/block declarations and combat damage; MAIN2 priority resumes','exact life, card IDs and battlefield/graveyard/exile outcomes','native player/card counter multisets, net power/toughness and marked damage match both public projections','private hands preserved/concealed, exact full library order and empty mana pools']
                        identity['assertions'].append({'protection-prevent':'black first-strike damage prevented; white first-strike kills Hexmage','protection-block':'black blocker never offered while blue Wall is legal against protection-from-black attacker; declining block causes two unblocked damage','indestructible':'lethal two marked damage does not destroy 0/1 Myr','infect-creature':'two -1/-1 counters and no marked damage on surviving 1/5 blocker; source takes one marked damage','infect-death':'two -1/-1 counters cause 2/2 blocker death; source survives two marked damage','infect-player':'opponent receives two poison counters and loses no life','wither-creature':'three -1/-1 counters and no marked damage on surviving 1/5 blocker; source takes one marked damage','wither-death':'three -1/-1 counters cause blocker death; 3/3 source survives two damage','banding-defense':'defender, not attacker, assigns trample source six damage entirely to Hero; Bear lives and no player damage'}[case])
                        identity.update(status='PASS',result='PASS '+case+': '+'; '.join(identity['assertions']),requests=len(seen));break
                elif kind=='InputAttack':
                    assert initial is not None and r['seat']==1
                    sources=[c['id'] for c in one['battlefield'] if c['creature']]
                    if case.endswith('-probe') and len(attacked)==1 and not probe_clicked:
                        # Repeat the existing public action that should activate the native band.
                        source=next(iter(attacked));pick=next(o['id'] for o in r['options'] if o['id'].startswith('attack:') and o['attackerId']==source);probe_clicked=True
                        snapshot(m,'repeat-declared-attacker')
                    else:
                        option=next((o for o in r['options'] if o['id'].startswith('attack:') and o['attackerId'] in sources and o['attackerId'] not in attacked and o['defenderKind']=='player' and o['defenderId']==two['targetId']),None)
                        if option:pick=option['id'];attacked.add(option['attackerId'])
                        else:assert attacked==set(sources);snapshot(m,'attack-confirm');proof(r,'attack-confirm')
                elif kind=='InputBlock':
                    block_seen=True;assert r['seat']==2;snapshot(m,'block-'+str(step))
                    if case.endswith('-probe'):
                        native=proof(r,'band-probe');assert probe_clicked and len(attacked)==2
                        assert len(native['bands'])==2 and all(len(b['members'])==1 for b in native['bands']),native['bands']
                        assert not any('band' in o['id'] for o in r['options'])
                        identity.update(status='GAP_CONFIRMED',result='GAP '+case+': repeated declared-attacker wire action skipped native band activation; both attackers remain separate singleton native bands',assertions=['Actual declaration/repeated action/second declaration reached native InputBlock','Read-only native band memberships show both singleton groups; no attack-band wire action was offered'],requests=len(seen));break
                    candidates=[o for o in r['options'] if o['id'].startswith('block:')]
                    if case=='protection-block':
                        legal={o['defenderId'] for o in candidates}
                        assert legal=={c['id'] for c in two['battlefield'] if c['name']=='Wall of Air'},('Illegal pro-black block offered or missing legal blue blocker',candidates)
                    option=None if case=='protection-block' else next((o for o in candidates if (o['attackerId'],o['defenderId']) not in pairs),None)
                    if option:pick=option['id'];pairs.add((option['attackerId'],option['defenderId']))
                elif kind=='AssignAmount':
                    assert case=='banding-defense' and r['seat']==2,('Unexpected assignment/decider',case,r)
                    assert r['selectionPurpose']=='combatDamage' and r['sourceCard']['name']=='Colossal Dreadmaw' and r['maxCount']==6
                    rows=r['allocations'];ctx=r['contextId'];contexts.setdefault(ctx,True)
                    assert len(rows)==3
                    for row in rows:
                        if row.get('card'):
                            assert row['targetId']==row['card']['id'] and row['hasLethal'] and row['lethal']==row['card']['toughness']
                        else:assert row['targetKind']=='player' and row['playerSeat']==2 and row['targetId']==two['targetId']
                    hero=next(a for a in rows if a.get('card',{}).get('name')=='Benalish Hero')
                    spent=sum(a['amount'] for a in rows)
                    assert spent==r['selectedCount'] and spent+r['remainingCount']==6 and ('confirm-amount' in ids)==(spent==6)
                    assert all(a['amount']==0 for a in rows if a is not hero)
                    snapshot(m,'allocation-'+str(step))
                    pick='amount-add:'+str(hero['id']) if hero['amount']<6 else 'confirm-amount'
                elif kind in ('InputChooseStartingHand',) or 'Mulligan' in kind:pass
                elif kind=='RevealCards':pick='confirm-cards'
                else:raise AssertionError(('Unexpected request',case,kind,ids,r['message']))
                assert pick in ids,(case,kind,pick,ids)
                p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
            else:raise AssertionError('No final priority')
            assert 'status' in identity,'No result'
    except Exception as e:
        identity.update(status='FAIL',result='FAIL '+case+': '+repr(e));(folder/'failure.txt').write_text(traceback.format_exc(),encoding='utf8')
    finally:
        if p is not None and p.poll() is None:
            try:p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush();p.wait(timeout=3)
            except (OSError,subprocess.TimeoutExpired):p.kill();p.wait()
    write(folder/'result.json',identity);write(OUT/(case+'-result.json'),identity)
    print(identity['result'],flush=True);return identity

if __name__=='__main__':
    OUT.mkdir(parents=True,exist_ok=True)
    results=[run(c) for c in sys.argv[1:] or CASES]
    write(OUT/'run-summary.json',dict(status='PASS' if all(r['status']=='PASS' for r in results) else 'PARTIAL',results=results))
    sys.exit(any(r['status']=='FAIL' for r in results))
