"""Native damage/prevention/split shields and draw replacement/optional boundaries."""
import json,os,queue,shutil,subprocess,sys,tempfile,threading,time,traceback
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'Tools/Validation/Reports/DamageDraw';CLASSES=ROOT/'.tools/damage-draw/classes'
if (OUT/'result.txt').exists():shutil.copytree(OUT,ROOT/'.tools/damage-draw/attempts'/str(time.time_ns()))
OUT.mkdir(parents=True,exist_ok=True);CLASSES.mkdir(parents=True,exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),*[str(ROOT/'Tools/ForgeBridge/tests'/n) for n in ('LibraryCostFixture.java','ReplacementFixture.java','DamageDrawFixture.java')]],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['damage-add-first','damage-double-first','damage-seat-two','damage-creature-survive','damage-prevent-first','damage-double-prevent','damage-cant-prevent','damage-only-prevent','damage-optional-accept','damage-optional-decline','damage-split-repeat','draw-double','draw-thief-first','draw-token-first','draw-underrealm-first','draw-underrealm-middle','draw-underrealm-short','draw-underrealm-seat-two','draw-dredge-accept','draw-dredge-decline','draw-dredge-short','draw-optional-skip','draw-optional-draw','draw-limit','draw-limit-after-first']

def run(case):
    actor=2 if case.endswith('seat-two') else 1;other=3-actor
    spell="Harm's Way" if case=='damage-split-repeat' else 'Lightning Bolt' if case.startswith('damage-') else 'Divination' if case=='draw-limit' else 'Reach Through Mists'
    with tempfile.TemporaryDirectory(prefix='mtgb-damage-draw-') as temp,(OUT/(case+'.log')).open('w',encoding='utf8') as log,(OUT/(case+'.jsonl')).open('w',encoding='utf8') as trace:
        audit=Path(temp)/'audit';audit.mkdir();start=Path(temp)/'start.json'
        start.write_text(json.dumps(dict(session=case,fixture=case,auditDir=str(audit),seed=4,startingLife=20,firstPlayerSeat=actor,decks=[DECK,DECK])),encoding='utf8')
        p=subprocess.Popen([str(RT/CFG['java']),'-Djava.awt.headless=true','-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.DamageDrawFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        messages=queue.Queue()
        def read():
            try:
                for line in p.stdout:messages.put(json.loads(line))
            except Exception as e:messages.put(e)
            finally:messages.put(None)
        threading.Thread(target=read,daemon=True).start()
        def snapshot(m,label):
            r=m['request'];v=dict(m['views'][r['seat']-1]);v.update(request=r,awaitingInput=True)
            (OUT/(case+'-'+label+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
        def proof(r,label):
            path=audit/(str(r['requestId'])+'.json');end=time.monotonic()+5
            while not path.exists() and time.monotonic()<end:time.sleep(.01)
            assert path.exists(),'Missing full library identities';d=json.loads(path.read_text());assert d['requestId']==r['requestId']
            (OUT/(case+'-'+label+'-audit.json')).write_text(json.dumps(d,indent=2),encoding='utf8');return [x['library'] for x in d['players']]
        started=False;source=None;initial=None;initial_other_hand=None;library=None;paid=0;round_no=0;selected=set();chosen=None;optional=0;orders=0;source_choices=0;pyro=None;step_no=0;deadline=time.monotonic()+200
        try:
            for step in range(700):
                assert time.monotonic()<deadline,'Deadline';m=messages.get(timeout=40);assert m is not None,'Bridge exited'
                if isinstance(m,Exception):raise m
                trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in ('fatal','ended','rejected'),m
                if m['type']!='request':continue
                r=m['request'];v=m['views'][actor-1];players=v['players'];one=players[actor-1];two=players[other-1];ids=[o['id'] for o in r['options']];kind=r['kind'];pick='ok'
                assert not m['views'][other-1]['players'][actor-1]['hand'],'Private hand leak'
                assert not m['views'][actor-1]['players'][other-1]['hand'],'Other private hand leak'
                if kind=='InputPassPriority' and not v['stackItems']:
                    if not started and r['seat']==actor and v['phase']=='MAIN1':
                        source=next(c['id'] for c in one['hand'] if c['name']==spell);initial=players;initial_other_hand={c['id'] for c in m['views'][other-1]['players'][other-1]['hand']};library=proof(r,'before-0');snapshot(m,'before-0');pick='card:'+str(source);started=True
                        if case=='damage-split-repeat':pyro=next(c['id'] for c in one['battlefield'] if c['name']=='Pyrohemia')
                    elif started and source not in {c['id'] for c in one['hand']}:
                        assert sum(one['manaPool'])==0,'Unspent mana'
                        current=proof(r,'resolved-'+str(round_no));snapshot(m,'resolved-'+str(round_no))
                        assert paid==(3 if case=='draw-limit' else 1),('Wrong native cost',case,round_no,paid)
                        assert source in {c['id'] for c in one['graveyardCards']},'Spell did not reach graveyard'
                        if case.startswith('damage-'):
                            assert current==library,'Damage changed library';assert one['life']==20 or case=='damage-split-repeat' and round_no==3 and one['life']==19
                            expected={'damage-add-first':10,'damage-double-first':12,'damage-seat-two':12,'damage-creature-survive':20,'damage-prevent-first':16,'damage-double-prevent':15,'damage-cant-prevent':14,'damage-only-prevent':18,'damage-optional-accept':18,'damage-optional-decline':17}.get(case)
                            if case=='damage-split-repeat':
                                assert two['life']==[20,18,16,15][round_no],('Shield split/consumption',round_no,one['life'],two['life'])
                                assert pyro in {c['id'] for c in one['battlefield']}
                                if round_no<3:
                                    assert r['seat']==actor;round_no+=1;paid=0;pick='card:'+str(pyro)
                                else:assert source_choices==1;return 'three real source activations; split two damage events then shield exhausted; exact life, IDs, costs, full libraries and priority'
                            else:
                                assert two['life']==expected,('Wrong damage',two['life'],expected)
                                if case=='damage-creature-survive':
                                    creature=next(c for c in two['battlefield'] if c['name']=='Ancient Brontodon');assert creature['damage']==8 and creature['power']==creature['toughness']==9
                                assert [{c['id'] for c in x['battlefield']} for x in players]==[{c['id'] for c in x['battlefield']} for x in initial]
                                if 'optional' in case:assert optional==1,'Missing optional confirmation'
                                if case not in ('damage-only-prevent','damage-cant-prevent') and 'optional' not in case:assert orders>0,'Missing native damage order'
                                return 'real damage/affected seat and replacement order/optional or prevention result; exact costs, field IDs, full libraries and priority'
                        else:
                            assert all(x['life']==20 for x in players)
                            removed=4 if case=='draw-double' else 3 if case.startswith('draw-underrealm') and case!='draw-underrealm-short' else 2 if case=='draw-underrealm-short' else 5 if case=='draw-dredge-accept' else 0 if case in ('draw-thief-first','draw-token-first','draw-optional-skip') or case=='draw-limit-after-first' and round_no==1 else 1
                            assert current[actor-1]==library[actor-1][removed:],('Incorrect library order',case,removed,current[actor-1][:6],library[actor-1][:6])
                            transferred=1 if case=='draw-thief-first' else 0
                            assert current[other-1]==library[other-1][transferred:],'Other library changed incorrectly'
                            expected_hand={c['id'] for c in initial[actor-1]['hand']}-{source}
                            expected_grave={c['id'] for c in initial[actor-1]['graveyardCards']}|{source}
                            if case.startswith('draw-underrealm'):
                                assert chosen in library[actor-1][:removed];expected_hand.add(chosen);expected_grave.update(set(library[actor-1][:removed])-{chosen})
                            elif case=='draw-dredge-accept':
                                imp=next(c['id'] for c in initial[actor-1]['graveyardCards'] if c['name']=='Stinkweed Imp');expected_hand.add(imp);expected_grave.remove(imp);expected_grave.update(library[actor-1][:5])
                            elif case not in ('draw-thief-first','draw-token-first','draw-optional-skip'):expected_hand.update(library[actor-1][:removed])
                            assert {c['id'] for c in one['hand']}==expected_hand,('Exact drawn hand',case)
                            assert {c['id'] for c in one['graveyardCards']}==expected_grave,('Exact graveyard',case)
                            assert {c['id'] for c in m['views'][other-1]['players'][other-1]['hand']}==initial_other_hand|set(library[other-1][:transferred])
                            assert [{c['id'] for c in x['exileCards']} for x in players]==[{c['id'] for c in x['exileCards']} for x in initial]
                            new=[c for c in two['battlefield'] if c['id'] not in {x['id'] for x in initial[other-1]['battlefield']}]
                            if case=='draw-token-first':assert len(new)==1 and new[0].get('token') and new[0]['name']=='Treasure Token' and new[0]['ownerSeat']==other
                            else:assert not new
                            assert {c['id'] for c in one['battlefield']}=={c['id'] for c in initial[actor-1]['battlefield']}
                            if case in ('draw-dredge-accept','draw-dredge-decline','draw-optional-skip','draw-optional-draw'):assert optional==1
                            if case=='draw-dredge-short':assert optional==0
                            if case=='draw-limit-after-first' and round_no==0:
                                round_no=1;paid=0;source=next(c['id'] for c in one['hand'] if c['name']==spell);initial=players;library=current;pick='card:'+str(source);snapshot(m,'before-1')
                            else:return 'real native draw/replacement/optional/limit; exact drawn/milled IDs, hand/grave/exile, both full library sequences, privacy, costs and continued priority'
                elif 'PayMana' in kind:
                    pick=next((i for i in ids if i.startswith('mana:')),None)
                    if pick is None:
                        legal=[c for c in one['battlefield'] if 'card:'+str(c['id']) in ids and c.get('land')]
                        color='Plains' if spell=="Harm's Way" and round_no==0 else 'Island' if case.startswith('draw-') else 'Mountain'
                        pick='card:'+str(next(c['id'] for c in legal if c['name']==color))
                    paid+=1
                elif kind=='InputSelectTargets':
                    target='card:'+str(next(c['id'] for c in two['battlefield'] if c['name']=='Ancient Brontodon')) if case=='damage-creature-survive' else 'player:'+str(two['targetId'])
                    pick='ok' if r.get('selectedCount') or target not in ids and 'ok' in ids else target;snapshot(m,'target-'+str(step_no))
                elif kind=='OrderAbilities':
                    assert r.get('selectionPurpose')=='replacementOrder' and r['seat']==(actor if case.startswith('draw-') else other),'Wrong replacement decider'
                    orders+=1;snapshot(m,'order-'+str(step_no));options=[o for o in r['options'] if o['id'].startswith('order:')]
                    first='Torbran' if case=='damage-add-first' else "Urza's Armor" if case=='damage-prevent-first' else 'Notion Thief' if case=='draw-thief-first' else 'Hullbreacher' if case=='draw-token-first' else 'Thought Reflection' if case=='draw-double' else 'Furnace of Rath'
                    if not options:pick='confirm-order'
                    else:pick=next((o['id'] for o in options if first in o['label'] and not r.get('selectedCount')),options[0]['id'])
                elif kind=='InputConfirm':
                    assert r.get('selectionPurpose')=='optionalReplacement','Optional effect lacks typed context'
                    entries=r['orderedAbilities'];assert len(entries)==1 and entries[0]['kind']=='replacement' and entries[0]['source']['id']==r['sourceCardId']>0
                    assert r['replacementAffectedPlayerSeat']==(other if case.startswith('damage-') else actor),'Wrong affected player'
                    optional+=1;assert r['seat']==actor,'Wrong optional decider';snapshot(m,'optional-'+str(step_no));pick='cancel' if case.endswith('decline') or case=='draw-optional-draw' else 'ok'
                elif kind in ('SelectCards','InputSelectCardsFromList','InputSelectEntitiesFromList'):
                    snapshot(m,'select-'+str(step_no));options=[o for o in r['options'] if o['id'].startswith(('select:','card:')) and not o.get('selected')]
                    if r.get('selectedCount'):pick='confirm-cards' if 'confirm-cards' in ids else 'ok'
                    elif case=='damage-split-repeat':
                        assert r.get('selectionPurpose')=='chooseSource' and r['sourceCardId']==source,'Source choice lacks native identity'
                        assert all(o.get('card',{}).get('id',0)>0 for o in options),'Native headings became selectable cards'
                        source_choices+=1;pick=next(o['id'] for o in options if o.get('card',{}).get('id')==pyro)
                    elif case.startswith('draw-underrealm'):
                        index=0 if case=='draw-underrealm-first' else 1;chosen=library[actor-1][index];pick=next(o['id'] for o in options if o.get('card',{}).get('id')==chosen or o['id']=='card:'+str(chosen))
                        hidden=set(library[actor-1][:3]);exposed=m['views'][other-1]
                        def card_ids(value):
                            if isinstance(value,dict):
                                if 'id' in value and 'name' in value:yield value['id']
                                for x in value.values():yield from card_ids(x)
                            elif isinstance(value,list):
                                for x in value:yield from card_ids(x)
                        assert not hidden.intersection(card_ids(exposed)),'Private looked-at cards leaked'
                    else:raise AssertionError(('Unexpected card choice',kind,ids))
                elif kind=='RevealCards':pick='confirm-cards'
                elif kind=='InputChooseStartingHand' or 'Mulligan' in kind:pick='ok'
                elif kind!='InputPassPriority':raise AssertionError(('Unadapted test request',case,kind,ids,r['message']))
                assert pick in ids,(case,round_no,kind,pick,ids,r['message']);step_no+=1
                p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
            raise AssertionError('No final priority')
        finally:
            if p.poll() is None:
                p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
                try:p.wait(timeout=3)
                except subprocess.TimeoutExpired:p.kill();p.wait()

if __name__=='__main__':
    results=[]
    for case in sys.argv[1:] or CASES:
        try:result='PASS '+case+': '+run(case)
        except Exception as e:result='FAIL '+case+': '+repr(e);(OUT/(case+'-error.txt')).write_text(traceback.format_exc(),encoding='utf8')
        print(result,flush=True);results.append(result)
    (OUT/'result.txt').write_text('\n'.join(results),encoding='utf8');sys.exit(any(x.startswith('FAIL') for x in results))
