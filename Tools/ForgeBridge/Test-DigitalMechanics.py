"""Real native Conjure, Draft, Seek and perpetual effects, including private library proof."""
import json,os,queue,re,shutil,subprocess,sys,tempfile,threading,time
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2];RT=ROOT/'ForgeRuntime'
CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
OUT=ROOT/'Tools/Validation/Reports/DigitalMechanics';CLASSES=ROOT/'.tools/digital-mechanics/classes'
if (OUT/'result.txt').exists():shutil.copytree(OUT,ROOT/'.tools/digital-mechanics/attempts'/str(time.time_ns()))
OUT.mkdir(parents=True,exist_ok=True);CLASSES.mkdir(parents=True,exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(CLASSES),str(ROOT/'Tools/ForgeBridge/tests/DigitalMechanicFixture.java')],check=True)
DECK={'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}
CASES=['conjure-ionize','conjure-electrolyze','draft-first','draft-last','seek','seek-empty','perpetual-same','perpetual-split']

def run(case):
    source='Shock' if case.startswith(('conjure','draft')) else "Ureni's Counsel" if case.startswith('seek') else 'Hypnotic Pattern'
    with tempfile.TemporaryDirectory(prefix='mtgb-digital-') as temp,(OUT/(case+'.log')).open('w',encoding='utf8') as log,(OUT/(case+'.jsonl')).open('w',encoding='utf8') as trace:
        audit=Path(temp)/'audit';audit.mkdir()
        start=Path(temp)/'start.json';start.write_text(json.dumps(dict(session=case,fixture=case,auditDir=str(audit),seed=4,startingLife=20,firstPlayerSeat=1,decks=[DECK,DECK])),encoding='utf8')
        p=subprocess.Popen([str(RT/CFG['java']),'-Djava.awt.headless=true','-cp',';'.join(map(str,[CLASSES,RT/'classes',RT/'lib/*'])),'mtgb.bridge.DigitalMechanicFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        messages=queue.Queue()
        def read():
            try:
                for line in p.stdout:messages.put(json.loads(line))
            except Exception as error:messages.put(error)
            finally:messages.put(None)
        threading.Thread(target=read,daemon=True).start()
        def snapshot(m,label):
            v=dict(m['views'][0]);v.update(request=m['request'] if m['request']['seat']==1 else None,awaitingInput=m['request']['seat']==1)
            (OUT/(case+'-'+label+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
        def proof(r,label):
            path=audit/(str(r['requestId'])+'.json');limit=time.monotonic()+5
            while not path.exists() and time.monotonic()<limit:time.sleep(.01)
            assert path.exists(),'Missing read-only library proof'
            data=json.loads(path.read_text());assert data['requestId']==r['requestId']
            (OUT/(case+'-'+label+'-audit.json')).write_text(json.dumps(data,indent=2),encoding='utf8')
            return data['players'][0]['library']
        stage=0;choices=0;generated=None;initial=None;first_library=None;initial_ids=None;target_count=0;deadline=time.monotonic()+150
        try:
            for step in range(500):
                assert time.monotonic()<deadline,'Deadline'
                m=messages.get(timeout=45);assert m is not None,'Bridge exited'
                if isinstance(m,Exception):raise m
                trace.write(json.dumps(m)+'\n');trace.flush()
                assert m['type'] not in ('fatal','ended','rejected'),m
                if m['type']!='request':continue
                r=m['request'];v=m['views'][r['seat']-1];one=m['views'][0]['players'][0];public_one=m['views'][1]['players'][0]
                ids=[o['id'] for o in r['options']];kind=r['kind'];pick='ok'
                if kind=='InputPassPriority' and not v['stackItems']:
                    field={c['name']:c for c in one['battlefield']}
                    if stage==0 and r['seat']==1 and v['phase']=='MAIN1':
                        initial=len(one['hand']);initial_ids={c['id'] for c in one['hand']};first_library=proof(r,'before');snapshot(m,'before')
                        pick='card:'+str(next(c for c in one['hand'] if c['name']==source)['id']);stage=1
                    elif stage==1 and any(c['name']==source for c in one['graveyardCards']):
                        assert not public_one['hand'],'Private hand leaked to opponent'
                        if case.startswith('conjure'):
                            name='Ionize' if case.endswith('ionize') else 'Electrolyze'
                            created=[c for c in one['hand'] if c['id'] not in initial_ids]
                            assert choices==1 and len(created)==1 and created[0]['name']==name and not created[0].get('token',False)
                            assert len(one['hand'])==initial and all(c['name']!='Izzet Polarizer' for c in one['battlefield'])
                            assert any(c['name']=='Izzet Polarizer' for c in one['graveyardCards'])
                            assert created[0]['printingKey'] and created[0]['currentRules']
                            snapshot(m,'resolved');return 'actual Shock target/payment/death trigger; chosen non-token card created in private hand; no other choice created; priority resumes'
                        if case.startswith('draft'):
                            assert choices==1 and any(c['id']==generated['id'] and c['name']==generated['name'] for c in one['hand'])
                            assert len(one['hand'])==initial
                            assert sum(c['id'] in draft_ids for c in one['hand'])==1
                            snapshot(m,'resolved');stage=2
                            pick='card:'+str(next(c for c in one['hand'] if c['name']=='Shock')['id'])
                        elif case.startswith('seek'):
                            after=proof(r,'resolved');created=[c for c in one['hand'] if c['id'] not in initial_ids]
                            if case.endswith('empty'):
                                assert not created and after==first_library and len(one['hand'])==initial-1
                            else:
                                assert len(created)==1 and created[0]['name'] in ('Shivan Dragon','Dragon Whelp')
                                sought=created[0]['id'];assert any(c['id']==sought for c in first_library)
                                assert after==[c for c in first_library if c['id']!=sought],'Seek shuffled/reordered unrelated library cards'
                                assert len(one['hand'])==initial
                            assert choices==0,'Seek improperly prompts to choose hidden library card'
                            snapshot(m,'resolved');return 'actual Counsel cast/payment; '+('empty pool completes without choices or movement' if case.endswith('empty') else 'one exact Dragon ID moves library to private hand; every remaining library ID preserves order')+'; priority resumes'
                        else:
                            assert field['Grizzly Bears']['power']==(-2 if case.endswith('same') else 0)
                            assert field['Runeclaw Bear']['power']==(2 if case.endswith('same') else 0)
                            snapshot(m,'both-modifiers');stage=2
                    elif stage==2 and case.startswith('draft') and 'Runeclaw Bear' not in field:
                        assert choices==1,'Once-per-turn draft triggered twice'
                        assert sum(c['name']=='Shock' for c in one['graveyardCards'])==2
                        assert any(c['name']=='Runeclaw Bear' for c in one['graveyardCards'])
                        assert len(one['hand'])==initial-1
                        snapshot(m,'limited');return 'actual two Shock casts/deaths; three unique native spellbook options; selected exact ID creates one private hand card; second death same turn cannot draft again'
                    elif stage==2 and case.startswith('perpetual') and v['turnSeat']==2 and v['phase']=='MAIN1':
                        assert field['Grizzly Bears']['power']==0 and field['Runeclaw Bear']['power']==2,'Wrong EOT expiry/perpetual duration'
                        snapshot(m,'expired');stage=3
                    elif stage==3 and r['seat']==1 and v['turnSeat']==1 and v['phase']=='MAIN1':
                        pick='card:'+str(next(c for c in one['hand'] if c['name']=='Unsummon')['id']);stage=4
                    elif stage==4 and 'Grizzly Bears' not in field and any(c['name']=='Unsummon' for c in one['graveyardCards']):
                        bear=next(c for c in one['hand'] if c['name']=='Grizzly Bears')
                        assert bear['power']==0 and bear['toughness']==2,'Perpetual lost on return to hand'
                        snapshot(m,'returned');pick='card:'+str(bear['id']);stage=5
                    elif stage==5 and 'Grizzly Bears' in field:
                        assert field['Grizzly Bears']['power']==0 and field['Grizzly Bears']['toughness']==2,'Perpetual lost after recast'
                        assert field['Runeclaw Bear']['power']==2
                        snapshot(m,'recast');return 'actual two targets, mana payment and modifiers; temporary -2 expires, perpetual -2 survives EOT, Unsummon to private hand and real recast'
                elif r.get('selectionPurpose')=='cardName':
                    assert case.startswith('conjure') and r['seat']==1
                    choices+=1;names={o['cardName'] for o in r['options']};assert names=={'Ionize','Electrolyze'} and 'cancel' not in ids
                    snapshot(m,'choice');pick=next(o['id'] for o in r['options'] if o['cardName']==('Ionize' if case.endswith('ionize') else 'Electrolyze'))
                elif kind=='SelectCards' and case.startswith('draft'):
                    if not r.get('selectedCount'):
                        choices+=1;cards=[o['card'] for o in r['options'] if o.get('card')];assert len(cards)==3 and len({c['name'] for c in cards})==3
                        snapshot(m,'choice')
                        for c in cards:assert c.get('printingKey') and c.get('currentType') and c.get('currentRules'),'Unplaced draft card lacks printing/type/rules preview'
                        generated=cards[-1] if case.endswith('last') else cards[0];draft_ids={c['id'] for c in cards}
                        pick=next(o['id'] for o in r['options'] if o.get('card',{}).get('id')==generated['id'])
                    else:
                        assert r['selectedCount']==1 and len([o for o in r['options'] if o.get('selected')])==1
                        snapshot(m,'selected');pick='confirm-cards'
                elif 'PayMana' in kind:
                    cost=r.get('remainingManaCost') or r['message'].split('Pay Mana Cost:',1)[-1];colors=re.findall(r'\{([WUBRG])\}',cost);allowed=colors or list('WUBRGC')
                    pick=next((i for i in ids if i.startswith('mana:') and i.split(':')[1] in allowed),None)
                    if pick is None:
                        basics=dict(zip('WUBRG',['Plains','Island','Swamp','Mountain','Forest']))
                        pick=next('card:'+str(c['id']) for c in one['battlefield'] if c['name'] in [basics[x] for x in allowed if x in basics] and 'card:'+str(c['id']) in ids)
                elif kind=='InputSelectTargets':
                    if r.get('selectedCount') and 'ok' in ids:pick='ok'
                    else:
                        target='Izzet Polarizer' if case.startswith('conjure') else 'Runeclaw Bear' if case.startswith('draft') and stage==2 else 'Grizzly Bears'
                        if case=='perpetual-split' and stage==1 and 'Select target creature (until end of turn)' in r['message']:target='Runeclaw Bear'
                        pick='card:'+str(next(c for c in one['battlefield'] if c['name']==target)['id']);target_count+=1
                elif kind in ('InputSelectCardsFromList','InputSelectEntitiesFromList'):
                    pick='ok' if r.get('selectedCount') and 'ok' in ids else next(i for i in ids if i.startswith('card:'))
                elif kind=='RevealCards':pick='confirm-cards'
                elif kind=='dialog':pick=ids[0]
                elif kind=='OrderAbilities':pick='confirm-order'
                assert pick in ids,(case,stage,kind,pick,r['message'],ids[:12])
                p.stdin.write(json.dumps(dict(type='respond',session=case,seat=r['seat'],requestId=r['requestId'],option=pick))+'\n');p.stdin.flush()
            raise AssertionError('No completion')
        finally:
            if p.poll() is None:
                p.stdin.write('{"type":"shutdown"}\n');p.stdin.flush()
                try:p.wait(timeout=3)
                except subprocess.TimeoutExpired:p.kill();p.wait()

if __name__=='__main__':
    results=[]
    for case in sys.argv[1:] or CASES:
        try:result='PASS '+case+': '+run(case)
        except Exception as error:result='FAIL '+case+': '+repr(error)
        print(result,flush=True);results.append(result)
    (OUT/'result.txt').write_text('\n'.join(results),encoding='utf8');sys.exit(any(r.startswith('FAIL') for r in results))
