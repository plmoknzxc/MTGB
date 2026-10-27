"""Actual casts: Spree combined affordability/target filtering, modal spell and Kicker."""
import json,os,queue,subprocess,tempfile,threading,sys,hashlib,datetime
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime';CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'Tools/Validation/Reports/ModeChoices';OUT.mkdir(parents=True,exist_ok=True)
CLASSES=ROOT/'.tools/mode-choices/classes';CLASSES.mkdir(parents=True,exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(ROOT/'Tools/ForgeBridge/tests/ModeChoiceFixture.java')],check=True)
def run(case):
    deck={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
    with tempfile.TemporaryDirectory(prefix='mtgb-modes-') as temp,(OUT/(case+'.log')).open('w',encoding='utf8') as log,(OUT/(case+'.jsonl')).open('w',encoding='utf8') as trace:
        start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,seed=4,startingLife=20,firstPlayerSeat=1,decks=[deck,deck])),encoding='utf8')
        p=subprocess.Popen([str(RT/CFG['java']),'-Djava.awt.headless=true','-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.ModeChoiceFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        q=queue.Queue();threading.Thread(target=lambda:[q.put(json.loads(line)) for line in p.stdout],daemon=True).start()
        def send(r,id):p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=id))+'\n');p.stdin.flush()
        def snap(m,label):
            v=dict(m['views'][m['request']['seat']-1]);v.update(request=m['request'],awaitingInput=True);(OUT/(case+'-'+label+'.json')).write_text(json.dumps(v),encoding='utf8')
        modal_views={}
        cast=False;cancelled=False;rejected=False;chosen=False;seen_stack=False;replicate_number=False
        spell_name='Tender Wildguide' if case.startswith('offspring') else 'One Dozen Eyes' if case.startswith('entwine') else 'Shattering Spree' if case.startswith('replicate') else 'Mulldrifter' if case.startswith('evoke') else 'Boros Charm' if case.startswith('choose-one') else 'Burst Lightning' if case.startswith('kicker') else 'Requisition Raid'
        try:
            for step in range(100):
                while True:
                    m=q.get(timeout=40);trace.write(json.dumps(m)+'\n');trace.flush()
                    assert m['type'] not in ('fatal','ended'),m
                    if m['type']=='request':break
                    assert m['type']!='rejected',m
                r=m['request'];v=m['views'][0];one,two=v['players'];opts=r['options'];ids=[o['id'] for o in opts];kind=r['kind'];pick='ok'
                if kind in ('ChooseModes','ChooseNumber') and r.get('contextId',0)>0:
                    key=(kind,r['contextId']);snapshot=json.dumps(m['views'],sort_keys=True)
                    assert snapshot==modal_views.setdefault(key,snapshot),'Modal selection changed the board projection'
                    assert 'decisionViews' not in r,'Internal modal snapshot leaked'
                for entry in v['stackItems']:
                    if entry['kind']!='spell' or entry['source']['name']!=spell_name:continue
                    modes=entry.get('chosenModes',[]);costs=entry.get('castingChoices',[])
                    expected={'spree-rich':[1,2,3],'spree-limited':[1],'spree-targets':[3],'choose-one':[1],'entwine':[1,2],'entwine-base':[1]}.get(case,[])
                    assert sorted(mode['order'] for mode in modes)==expected,(case,modes,expected)
                    assert all(mode['description'] for mode in modes)
                    if case in ['kicker','evoke','evoke-limited','entwine','replicate','offspring']:
                        required={'kicker':'Kicker1','evoke':'Evoke','evoke-limited':'Evoke','entwine':'Entwine','replicate':'REPLICATE','offspring':'OFFSPRING'}[case]
                        assert any(c['kind']==required and c['count']==1 for c in costs),(case,costs)
                    elif case in ['kicker-base','evoke-normal','entwine-base','offspring-base']:assert not costs,(case,costs)
                    public=next(e for e in m['views'][1]['stackItems'] if e['id']==entry['id'])
                    assert public.get('chosenModes',[])==modes and public.get('castingChoices',[])==costs,'Public choices differ by seat'
                    if not seen_stack:
                        snap(m,'stack')
                        for seat_index,seat_view in enumerate(m['views']):
                            (OUT/(case+'-stack-seat'+str(seat_index+1)+'.json')).write_text(json.dumps(seat_view),encoding='utf8')
                        seen_stack=True
                if kind=='InputConfirmMulligan':pass
                elif kind=='InputPassPriority':
                    if cancelled:
                        name='Boros Charm' if case.startswith('choose-one') else 'Requisition Raid'
                        assert not v['stackItems'] and any(c['name']==name for c in one['hand'])
                        assert not any(c['tapped'] for c in one['battlefield']) and one['life']==two['life']==20
                        snap(m,'resolved');return 'PASS '+case+' cancel restores hand, priority, untapped lands, life and empty stack'
                    name=spell_name
                    finished=name in one['graveyard'] or (case=='evoke-normal' or case.startswith('offspring')) and any(c['name']==name for c in one['battlefield'])
                    if finished and not v['stackItems']:
                        bear=next((c for c in one['battlefield'] if c['name']=='Grizzly Bears'),None)
                        assert seen_stack,'Actual stack choices never verified'
                        if case.startswith('offspring'):
                            cards=[c for c in one['battlefield'] if c['name']==name]
                            assert len(cards)==(2 if case=='offspring' else 1) and sum(c.get('token',False) and c.get('copy',False) and c['power']==1 and c['toughness']==1 for c in cards)==(1 if case=='offspring' else 0)
                            assert sum(not c.get('token',False) and c['power']==2 and c['toughness']==2 for c in cards)==1
                        elif case.startswith('entwine'):
                            tokens=[c for c in one['battlefield'] if c.get('token',False)]
                            assert len(tokens)==(6 if case=='entwine' else 1) and sum(c['power']==5 and c['toughness']==5 for c in tokens)==1
                            assert sum(c['power']==1 and c['toughness']==1 for c in tokens)==(5 if case=='entwine' else 0)
                        elif case.startswith('replicate'):assert all(n in two['graveyard'] for n in ["Tormod's Crypt",'Sol Ring']) and two['life']==20
                        elif case=='spree-rich':assert bear['power']==3 and bear['toughness']==3 and all(n in two['graveyard'] for n in ["Tormod's Crypt",'Glorious Anthem'])
                        elif case=='spree-limited':assert bear['power']==2 and "Tormod's Crypt" in two['graveyard'] and any(c['name']=='Glorious Anthem' for c in two['battlefield']) and rejected
                        elif case=='spree-targets':assert bear['power']==3 and bear['toughness']==3
                        elif case.startswith('evoke'):
                            assert chosen and one['handCount']==9 and one['libraryCount']==51 and two['life']==20
                            assert any(c['name']==name for c in one['battlefield'])==(case=='evoke-normal')
                            assert (name in one['graveyard'])==(case!='evoke-normal')
                        else:assert two['life']==(18 if case=='kicker-base' else 16) and chosen
                        assert sum(c['land'] and c['tapped'] for c in one['battlefield'])==({'spree-rich':4,'kicker':5,'kicker-base':1,'evoke':3,'evoke-limited':3,'evoke-normal':5,'entwine':9,'entwine-base':6,'replicate':2,'offspring':4,'offspring-base':2}.get(case,2));snap(m,'resolved');return 'PASS '+case+' actual cast/payment/stack choices/targets/effects/priority'
                    if not cast and r['seat']==1 and v['phase']=='MAIN1' and not v['stackItems']:
                        pick=next(o['id'] for o in opts if o['label'].startswith(name+' ['));cast=True
                elif kind=='ChooseModes':
                    snap(m,'selected' if r['selectedCount'] else 'choices');modes=[o for o in opts if o['id'].startswith('mode:')]
                    assert ('confirm-modes' in ids)==(r['selectedCount']>=r['minCount'])
                    if case=='choose-one-cancel':
                        assert 'cancel' in ids and not v['stackItems'] and one['handCount']==8
                        pick='mode:0' if not r['selectedCount'] else 'cancel';cancelled=pick=='cancel'
                    elif case=='spree-unpayable':assert all(o['disabled'] for o in modes) and 'confirm-modes' not in ids;pick='cancel';cancelled=True
                    elif case=='spree-targets':
                        legal=[o for o in modes if not o['id'].startswith('mode:unavailable:')]
                        assert len(modes)==3 and len(legal)==1 and legal[0]['order']==3
                        assert all(o['disabled'] for o in modes if o['id'].startswith('mode:unavailable:'))
                        pick=legal[0]['id'] if not r['selectedCount'] else 'confirm-modes'
                    elif case=='spree-limited':
                        if not r['selectedCount']:assert all(not o['disabled'] for o in modes);pick='mode:0'
                        else:
                            assert all(o['disabled'] for o in modes if not o['selected'])
                            send(r,'mode:1')
                            while True:
                                bad=q.get(timeout=40);trace.write(json.dumps(bad)+'\n');trace.flush()
                                if bad['type']=='rejected':assert bad['error']=='invalid-option';rejected=True;break
                                assert bad['type']!='request',bad
                            pick='confirm-modes'
                    elif case=='spree-rich':
                        assert not any(o['disabled'] for o in modes);pick=next((o['id'] for o in modes if not o['selected']),'confirm-modes')
                    else:
                        assert len(modes)==(2 if case.startswith('entwine') else 3) and r['maxCount']==1;pick='mode:0' if not r['selectedCount'] else 'confirm-modes';chosen=True
                elif kind=='ChooseOptions':
                    assert case.startswith(('kicker','entwine')) and r['selectionPurpose']=='optionalCosts';snap(m,'selected' if r['selectedCount'] else 'choices')
                    pick='confirm-cards' if case in ['kicker-base','entwine-base'] or r['selectedCount'] else 'select:0';chosen=True
                elif kind.startswith('InputPayMana'):
                    if case=='evoke-limited':chosen=True
                    pick=next((i for i in ids if i.startswith('mana:')),None) or next(o['id'] for n in ['Plains','Mountain','Island','Forest'] for o in opts if o['label'].startswith(n+' ['))
                elif kind=='InputSelectTargets':
                    target=one if case.startswith('spree-') else two
                    targets=['Sol Ring'] if case.startswith('replicate') and seen_stack else ["Tormod's Crypt",'Glorious Anthem']
                    pick='ok' if r['selectedCount'] and 'ok' in ids else next((o['id'] for o in opts if o.get('card',{}).get('name') in targets),None) or 'player:'+str(target['targetId'])
                elif kind=='ChooseNumber':
                    assert case.startswith('replicate');pick='confirm-number' if replicate_number else 'number-plus';replicate_number=True
                elif kind=='InputConfirm':
                    assert case.startswith('offspring');pick='ok' if case=='offspring' else 'cancel'
                elif kind=='OrderAbilities':
                    assert case.startswith(('evoke','offspring'));pick=next((id for id in ids if id.startswith('order:')),'confirm-order')
                elif kind=='dialog':
                    if r.get('selectionPurpose')=='castingCosts':
                        assert case.startswith('evoke');snap(m,'choices')
                        assert {o.get('faceKind') for o in opts if o['id']!='cancel'}=={'evoke','normal'}
                        pick=next(o['id'] for o in opts if o.get('faceKind')==('normal' if case=='evoke-normal' else 'evoke'));chosen=True
                    elif case.startswith('offspring'):pick=ids[0] if case=='offspring' else ids[1]
                    else:pick=ids[0]
                    if case=='evoke-limited':chosen=True
                else:raise AssertionError((kind,opts))
                assert pick in ids,(kind,pick,ids);send(r,pick)
            raise AssertionError('No completion')
        finally:
            if p.poll() is None:
                p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
                try:p.wait(timeout=3)
                except subprocess.TimeoutExpired:p.kill();p.wait()
results=[]
for case in sys.argv[1:] or ['spree-rich','spree-limited','spree-targets','spree-unpayable','choose-one','choose-one-cancel','kicker','kicker-base','evoke','evoke-normal','evoke-limited','entwine','entwine-base','replicate','offspring','offspring-base']:
    try:result=run(case)
    except Exception as e:result='FAIL '+case+': '+repr(e)
    results.append(result)
    (OUT/(case+'-result.json')).write_text(json.dumps({'case':case,'result':result,'timeUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'testSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'fixtureSha256':hashlib.sha256((ROOT/'Tools/ForgeBridge/tests/ModeChoiceFixture.java').read_bytes()).hexdigest(),'runtimeManifest':CFG['runtimeHash'],'traceSha256':hashlib.sha256((OUT/(case+'.jsonl')).read_bytes()).hexdigest()},indent=2),encoding='utf8')
(OUT/'result.txt').write_text('\n'.join(results),encoding='utf8');print('\n'.join(results));sys.exit(any(s.startswith('FAIL') for s in results))
