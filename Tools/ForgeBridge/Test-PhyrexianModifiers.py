"""Actual casting taxes/reductions/minimum costs, never counterspell resolution taxes."""
import hashlib,json,os,queue,subprocess,sys,tempfile,threading,time
from pathlib import Path
R=Path(__file__).resolve().parents[2];RT=R/'ForgeRuntime';OUT=R/'.tools/phyrexian-modifiers-1006'
OUT.mkdir(parents=True,exist_ok=True);CFG=json.loads((RT/'runtime.json').read_text(encoding='utf-8-sig'))
classes=OUT/'classes';classes.mkdir(exist_ok=True)
subprocess.run(['javac','-encoding','UTF-8','-cp',str(RT/'classes')+';'+str(RT/'lib/*'),'-d',str(classes),str(R/'Tools/ForgeBridge/tests/PhyrexianModifierFixture.java')],check=True)
def case(mana,life,mods=(),black=0,**kw):return dict(mana=mana,life=life,modifiers=list(mods),swamps=black,islands=max(mana-black,0),spell='Dismember',seat=1,triTapped=False,cancel=False,insufficient=False,**kw)
C={
 'tri-life':case(3,4,['Trinisphere']),
 'tri-mixed':case(3,2,['Trinisphere'],1),
 'tri-mana':case(3,0,['Trinisphere'],2),
 'tri-tapped-life':case(1,4,['Trinisphere']),
 'tax-life':case(2,4,['Sphere of Resistance']),
 'tax-mixed':case(3,2,['Sphere of Resistance'],1),
 'tax-mana':case(4,0,['Sphere of Resistance'],2),
 'reduce-life':case(0,4,['Helm of Awakening']),
 'reduce-mixed':case(1,2,['Helm of Awakening'],1),
 'reduce-mana':case(2,0,['Helm of Awakening'],2),
 'tax-reduce-life':case(2,4,['Sphere of Resistance','Sphere of Resistance','Helm of Awakening']),
 'tri-reduce-life':case(3,4,['Trinisphere','Helm of Awakening']),
 'tri-tax-reduce-life':case(3,4,['Trinisphere','Sphere of Resistance','Helm of Awakening']),
 'tri-above-floor-life':case(4,4,['Trinisphere']+['Sphere of Resistance']*3),
 'tri-insufficient':case(3,4,['Trinisphere']),
 'tri-cancel-life':case(3,4,['Trinisphere']),
 'tri-small-life':case(3,2,['Trinisphere']),
 'tri-small-mana':case(3,0,['Trinisphere']),
 'tri-late-life':case(3,4,['Trinisphere']),
 'tri-above-floor-late-life':case(4,4,['Trinisphere']+['Sphere of Resistance']*3),
 'tri-krrik-black':case(1,4,['Trinisphere']),
 'small-life':case(0,2),
 'tri-small-insufficient':case(3,2,['Trinisphere']),
}
C['tri-mixed']['seat']=2
C['tri-late-life']['lateMana']=True
C['tri-above-floor-late-life']['lateMana']=True
C['tri-krrik-black'].update(spell='Murder',krrik=True)
C['tri-tapped-life']['triTapped']=True
C['tri-insufficient'].update(islands=2,insufficient=True)
C['tri-cancel-life']['cancel']=True
for n in ['tri-small-life','tri-small-mana','small-life','tri-small-insufficient']:C[n]['spell']='Gut Shot' # R/P, player target 1 damage
C['tri-small-insufficient'].update(islands=2,insufficient=True)
C['tri-small-mana'].update(mountains=1,islands=2)
def run(name):
    spec=C[name];folder=OUT/name/str(time.time_ns());folder.mkdir(parents=True)
    actor=spec['seat'];startLife=20
    with tempfile.TemporaryDirectory(dir=OUT,prefix='temp-') as temp,(folder/'engine.log').open('w',encoding='utf8') as log,(folder/'trace.jsonl').open('w',encoding='utf8') as trace:
        data=dict(spec,session=name,seed=4,firstPlayerSeat=actor,startingLife=startLife,decks=[{'cards':[{'setCode':'10E','collectorNumber':'376','count':60}],'sideboard':[]}]*2)
        start=Path(temp)/'start.json';start.write_text(json.dumps(data),encoding='utf8')
        p=subprocess.Popen([str(RT/CFG['java']),'-Djava.awt.headless=true','-Djava.io.tmpdir='+temp,'-cp',';'.join(map(str,[classes,RT/'classes',RT/'lib/*'])),'mtgb.bridge.PhyrexianModifierFixture',str(RT/CFG['assets']),str(start)],cwd=RT/CFG['assets'],env=dict(os.environ,APPDATA=temp,LOCALAPPDATA=temp,TEMP=temp,TMP=temp),stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=log,text=True,encoding='utf8')
        q=queue.Queue()
        def reader():
            try:
                for line in p.stdout:q.put(json.loads(line))
            finally:q.put(None)
        threading.Thread(target=reader,daemon=True).start()
        def get():
            m=q.get(timeout=35);assert m is not None,'Native process exited';trace.write(json.dumps(m)+'\n');trace.flush();assert m['type'] not in {'fatal','ended'},m;return m
        def send(r,id,seat=None):p.stdin.write(json.dumps(dict(type='respond',session=name,requestId=r['requestId'],seat=seat or r['seat'],option=id))+'\n');p.stdin.flush()
        def snap(m,label):
            v=dict(m['views'][actor-1]);v.update(request=m['request'],awaitingInput=True);(folder/(label+'.json')).write_text(json.dumps(v,ensure_ascii=False,indent=2),encoding='utf8')
        cast=False;stack=False;before=None;reserved=0;cancelled=False;guard=False;costs=[];earlyMana=False
        try:
            for step in range(120):
                m=get();assert m['type']!='rejected',m
                if m['type']!='request':continue
                r=m['request'];v=m['views'][actor-1];own=v['players'][actor-1];enemy=v['players'][2-actor];ids=[o['id'] for o in r['options']];kind=r['kind'];pick='ok'
                entries=[s for s in v['stackItems'] if s['kind']=='spell' and s['source']['name']==spec['spell']]
                if entries:
                    stack=True;snap(m,'stack');tapped=sum(c['tapped'] for c in own['battlefield'] if c['land'])
                    assert tapped==spec['mana'],(name,'wrong actual mana',tapped,spec['mana'],costs)
                    assert own['life']==startLife-spec['life'] and sum(own['manaPool'])==0,(name,own)
                if kind=='InputPassPriority' and r['seat']==actor and v['phase']=='MAIN1' and not v['stackItems']:
                    spell=next((c for c in own['hand'] if c['name']==spec['spell']),None)
                    if stack and not spell:
                        assert own['life']==startLife-spec['life']
                        assert any(c['name']==spec['spell'] for c in own['graveyardCards'])
                        if spec['spell'] in {'Dismember','Murder'}:assert any(c['name']=='Grizzly Bears' for c in enemy['graveyardCards']) and not any(c['name']=='Grizzly Bears' for c in enemy['battlefield'])
                        else:assert enemy['life']==19
                        snap(m,'resolved');return dict(case=name,status='PASS',spec=spec,folder=str(folder.relative_to(R)),paidMana=spec['mana'],paidLife=spec['life'],costRequests=costs,cancelled=cancelled,wrongSeatGuard=guard)
                    if spec['insufficient']:
                        assert spell and 'card:'+str(spell['id']) not in ids;snap(m,'insufficient');return dict(case=name,status='PASS',spec=spec,folder=str(folder.relative_to(R)),insufficientNotOffered=True)
                    if cancelled and not cast:assert own==before,'Cancel changed native resources'
                    assert spell and 'card:'+str(spell['id']) in ids
                    before=json.loads(json.dumps(own));snap(m,'before-cast');pick='card:'+str(spell['id']);cast=True
                elif kind=='InputSelectTargets':
                    if 'ok' in ids:pick='ok'
                    elif spec['spell']=='Gut Shot':pick='player:'+str(enemy['targetId'])
                    else:pick='card:'+str(next(c['id'] for c in enemy['battlefield'] if c['name']=='Grizzly Bears'))
                elif 'PayMana' in kind:
                    costs.append(dict(remaining=r.get('remainingManaCost'),reserved=r.get('reservedLifePayment')));snap(m,'payment-'+str(len(costs)-1))
                    assert r['reservedLifePayment']==reserved and own['life']==startLife
                    if not guard:
                        send(r,ids[0],3-actor);reject=get();assert reject['type']=='rejected' and reject['error']=='wrong-seat';guard=True
                    if spec['cancel'] and reserved==2 and not cancelled:
                        snap(m,'cancel-payment');pick='cancel';cancelled=True;cast=False;stack=False;reserved=0
                    elif spec.get('lateMana') and not earlyMana:
                        land=next(c for c in own['battlefield'] if c['land'] and not c['tapped'] and 'card:'+str(c['id']) in ids);pick='card:'+str(land['id']);earlyMana=True
                    elif reserved<spec['life']:
                        pick='player:'+str(own['targetId']);assert pick in ids;(folder/('life-payment-'+str(reserved)+'.json')).write_text(json.dumps(dict(v,request=r,awaitingInput=True)),encoding='utf8');reserved+=2
                    else:
                        land=next((c for c in own['battlefield'] if c['land'] and not c['tapped'] and 'card:'+str(c['id']) in ids),None)
                        assert land,(name,'more mana required than fixture',r);pick='card:'+str(land['id'])
                elif kind=='InputMulligan':pick='keep'
                elif kind in {'Option','dialog'}:pick=next(i for i in ids if i not in {'cancel','no'})
                assert pick in ids,(name,kind,pick,ids);print(name,step,kind,pick,flush=True);send(r,pick)
            raise AssertionError('Step limit')
        finally:
            if p.poll() is None:p.kill();p.wait()
for name in sys.argv[1:] or list(C):
    assert name in C
    try:
        result=run(name);result.update(runtimeHash=CFG['runtimeHash'],sourceHash=hashlib.sha256(Path(__file__).read_bytes()).hexdigest());(OUT/(name+'-result.json')).write_text(json.dumps(result,indent=2),encoding='utf8');print('PASS '+name+': '+json.dumps(result),flush=True)
    except Exception:print('FAIL '+name,flush=True);raise
