"""Incremental live network boundary steps; retains completed checkpoints."""
import runpy,json,time,traceback,pathlib,sys
M=runpy.run_path(str(pathlib.Path(__file__).with_name('Run-NetworkBoundaries.py')))
R=M['Run'](M['read'](M['B']/'current.json')['path']);save=M['save'];brief=M['brief']

def view(seat=1):return R.state('server')['first' if seat==1 else 'second']['forge']
def card(name,seat=1,zone='hand'):
    return next(c for c in view()['players'][seat-1][zone] if c['name']==name)
def advance(predicate,limit=60):
    for _ in range(limit):
        s,r=R.ready();v=view(s)
        if predicate(s,r,v):return s,r
        assert r['kind']=='InputPassPriority',(r['kind'],r['message'])
        R.choose('ok')
    raise AssertionError('Advance exceeded real native priorities')
def settle():return advance(lambda s,r,v:r['kind']!='InputPassPriority' or not v.get('stackItems'))
def cast(name):
    s,r=R.ready();assert s==1 and r['kind']=='InputPassPriority'
    c=card(name);R.choose('card:'+str(c['id']));return c
def pay():
    for _ in range(8):
        s,r=R.ready()
        if 'PayMana' in r['kind']:R.choose('ok')
        elif r['kind']=='dialog':
            enabled=[o for o in r['options'] if not o.get('disabled')];assert len(enabled)==1,brief((s,r));R.choose(enabled[0]['id'])
        else:return
    raise AssertionError('Payment did not finish')
def opening():
    for _ in range(10):
        s,r=R.ready()
        if 'Mulligan' not in r['kind']:break
        R.choose(next(o['id'] for o in r['options'] if o['label']=='Keep'))
    R.sync();print('PASS opening resume; original successful keep retained',flush=True)
def checkpoint(name,value):
    R.sync();save(R.d/(name+'-pass.json'),{'status':'PASS','value':value,'snapshot':R.state('server')});print('PASS',name,flush=True)

def partial():
    advance(lambda s,r,v:s==1 and v['phase']=='MAIN1' and not v['stackItems'])
    initial=view();source=cast('Divination');s,r=R.ready();assert 'PayMana' in r['kind'],brief((s,r))
    before=R.state('server');island=next(c for c in initial['players'][0]['battlefield'] if c['name']=='Island' and not c['tapped'])
    R.choose('card:'+str(island['id']));s,r=R.ready();assert 'PayMana' in r['kind'] and r['remainingManaCost']!=before['first']['forge']['request']['remainingManaCost']
    assert next(c for c in view()['players'][0]['battlefield'] if c['id']==island['id'])['tapped']
    restored=R.disconnect_restore(1,'partial-payment-before-grace-restored')
    pay();settle();after=view();assert after['stackCount']==0
    assert any(c['id']==source['id'] for c in after['players'][0]['graveyardCards'])
    assert after['players'][0]['handCount']==initial['players'][0]['handCount']+1
    checkpoint('partial-payment-completes',{'spell':source['id'],'paidIsland':island['id'],'initial':initial,'restored':restored})

def target():
    initial=view();source=cast('Shock');s,r=R.ready();assert r['kind']=='InputSelectTargets'
    target=card('Hill Giant',2,'battlefield');R.choose('card:'+str(target['id']));s,r=R.ready();assert r['kind']=='InputSelectTargets' and r['remainingCount']==0
    restored=R.disconnect_restore(1,'target-draft-restored');R.choose('ok');pay();settle()
    after=view();assert next(c for c in after['players'][1]['battlefield'] if c['id']==target['id'])['damage']==2
    assert any(c['id']==source['id'] for c in after['players'][0]['graveyardCards'])
    checkpoint('target-draft-completes',{'source':source['id'],'target':target['id'],'restored':restored})

def pile_start():
    source=cast('Atris, Oracle of Half-Truths');pay();s,r=settle();print(json.dumps(brief((s,r)),ensure_ascii=False))
    save(R.d/'pile-source.json',{'id':source['id']})

def pile_tail():
    s,r=R.ready();assert s==1 and r['kind']=='ChoosePile' and r['contextId']==0
    hidden=next(p for p in r['piles'] if p['concealed']);public=next(p for p in r['piles'] if not p['concealed'])
    assert hidden['cardCount']==1 and not hidden['cards'] and public['cardCount']==2
    native=M['read'](R.d/'native-audit'/(str(r['requestId'])+'.json'));assert native
    public_ids=[c['id'] for c in public['cards']];initial_top=native['players'][0]['library'][:3];hidden_ids=[i for i in initial_top if i not in public_ids];assert len(hidden_ids)==1
    before=R.state('server');label='concealed-pile-modal-context-zero-restored'
    previous=[json.loads(x) for x in (R.d/'checks.jsonl').read_text(encoding='utf-8').splitlines() if json.loads(x)['label']==label]
    if not previous:R.disconnect_restore(1,label)
    R.choose('pile:'+str(hidden['pileId']));settle()
    p=view()['players'][0];assert any(c['id']==hidden_ids[0] for c in p['hand']);assert all(any(c['id']==i for c in p['graveyardCards']) for i in public_ids)
    checkpoint('concealed-pile-modal-completes',{'before':before,'publicIds':public_ids,'hiddenIds':hidden_ids})

def combat_start():
    advance(lambda s,r,v:r['kind']!='InputPassPriority')
    s,r=R.ready();assert s==1 and r['kind']=='InputAttack',brief((s,r))
    ids=[]
    for name in ['Grizzly Bears','Hill Giant']:
        c=card(name,1,'battlefield');ids.append(c['id']);s,r=R.ready()
        o=next(o for o in r['options'] if o['attackerId']==c['id'] and o['id'].startswith('attack:') and o['defenderKind']=='player');R.choose(o['id'])
    R.disconnect_restore(1,'attack-draft-restored');save(R.d/'combat-source.json',{'attackers':ids,'opponentLife':view()['players'][1]['life']});R.choose('ok')
    advance(lambda s,r,v:r['kind']!='InputPassPriority');s,r=R.ready();assert s==2 and r['kind']=='InputBlock';print(json.dumps(brief((s,r)),ensure_ascii=False))

def combat_tail():
    x=M['read'](R.d/'combat-source.json');s,r=R.ready();assert s==2 and r['kind']=='InputBlock'
    wall=card('Wall of Air',2,'battlefield');R.choose('block:'+str(wall['id'])+':'+str(x['attackers'][0]))
    R.disconnect_restore(2,'block-draft-restored');R.choose('ok')
    advance(lambda s,r,v:s==1 and v['phase']=='MAIN2' and not v['stackItems'])
    p=view()['players'];assert p[1]['life']==x['opponentLife']-3
    assert all(any(c['id']==i for c in p[0]['battlefield']) for i in x['attackers'])
    assert next(c for c in p[1]['battlefield'] if c['id']==wall['id'])['damage']==2
    checkpoint('attack-block-drafts-complete',x)

def control_start():
    source=card('Mindslaver',1,'battlefield');R.choose('card:'+str(source['id']))
    for _ in range(10):
        s,r=R.ready()
        if r['kind']=='InputSelectTargets':
            R.choose('ok' if r['remainingCount']==0 else 'player:'+str(view()['players'][1]['targetId']))
        elif 'PayMana' in r['kind']:R.choose('ok')
        elif r['kind']=='dialog':
            enabled=[o for o in r['options'] if not o.get('disabled')];assert len(enabled)==1;R.choose(enabled[0]['id'])
        elif r['kind']=='InputConfirm' and 'Sacrifice Mindslaver' in r['message']:R.choose('ok')
        elif r['kind']=='InputPassPriority':break
        else:raise AssertionError(brief((s,r)))
    control_after_arm(source['id'])

def control_arm_tail():
    source=card('Mindslaver',1,'battlefield');s,r=R.ready();assert r['kind']=='InputConfirm' and 'Sacrifice Mindslaver' in r['message'];R.choose('ok')
    control_after_arm(source['id'])

def control_after_arm(source_id):
    settle();assert any(c['id']==source_id for c in view()['players'][0]['graveyardCards'])
    advance(lambda s,r,v:v['turnSeat']==2 and v['phase']=='MAIN1' and r['playerSeat']==2)
    looter=card('Merfolk Looter',2,'battlefield');R.choose('card:'+str(looter['id']));pay();s,r=settle()
    save(R.d/'control-source.json',{'mindslaver':source_id,'looter':looter['id']});print(json.dumps(brief((s,r)),ensure_ascii=False))

def control_tail():
    s,r=R.ready();assert s==1 and r['playerSeat']==2 and r['kind']=='InputSelectCardsFromList'
    before=R.state('server');assert before['first']['forge']['players'][1]['controllingSeat']==1
    assert not before['second']['forge']['awaitingInput']
    ids=[o['card']['id'] for o in r['options'] if (o.get('card') or {}).get('name')];assert len(ids)==6
    slave_hand=[c['id'] for c in before['first']['forge']['players'][1]['hand']];assert sorted(ids)==sorted(slave_hand)
    nonmaster=R.state('client2');R.cmd('client2','raw',matchId=nonmaster['matchId'],clientId=nonmaster['nextClientId']+10,action={'type':44,'playerId':1,'sourceObjectId':r['requestId'],'optionId':0})
    time.sleep(.3);assert R.pending()[1]['requestId']==r['requestId'] and R.state('server')['first']['appliedSequence']==before['first']['appliedSequence']
    R.disconnect_restore(1,'controlled-private-hand-choice-restored')
    chosen=next(o for o in r['options'] if o['label'].startswith('Divination'));cid=chosen['card']['id'];R.choose(chosen['id']);s,q=R.ready()
    if q['kind']=='InputSelectCardsFromList':R.choose('ok')
    settle();after=view();assert any(c['id']==cid for c in after['players'][1]['graveyardCards']);assert after['players'][1]['handCount']==len(slave_hand)-1
    assert [c['id'] for c in after['players'][0]['hand']]==[c['id'] for c in before['first']['forge']['players'][0]['hand']]
    checkpoint('controlled-private-choice-completes',{'before':before,'chosen':cid,'physicalSeat':1,'underlyingPlayerSeat':2})
    for _ in range(80):
        s,q=R.ready();v=view()
        if v['turnSeat']==1 and v['turn']>=3 and v['players'][1]['controllingSeat'] in [0,2]:
            if s==2 and q['playerSeat']==2:break
        assert q['kind'] in ['InputPassPriority','InputAttack','InputBlock'],brief((s,q));R.choose('ok')
    else:raise AssertionError('Normal original player authority did not return')
    checkpoint('controlled-private-authority-returns',{'originalSlaveSeat':2,'actualNextRequest':brief((s,q))})

def control_return_frozen():
    candidates=[]
    for p in sorted(R.d.glob('server-snapshot-*.json'),key=lambda p:int(p.stem.rsplit('-',1)[1])):
        s=M['read'](p) or {};v=(s.get('second') or {}).get('forge') or {};q=v.get('request') or {}
        if v.get('turn')==3 and v.get('turnSeat')==1 and v.get('awaitingInput') and q.get('seat')==2 and q.get('playerSeat')==2 and v['players'][1]['controllingSeat']==0:
            assert not s['first']['forge']['awaitingInput'];candidates.append((p,s));break
    assert candidates,'Original normal authority snapshot missing'
    p,s=candidates[0];matched=[]
    for seat,key in [(1,'first'),(2,'second')]:
        for cp in R.d.glob('client'+str(seat)+'-snapshot-*.json'):
            c=M['read'](cp) or {}
            if (c.get('view') or {}).get('viewHash')==s[key]['viewHash']:matched.append(str(cp));break
    assert len(matched)==2
    save(R.d/'controlled-private-authority-returns-pass.json',{'status':'PASS','serverSnapshot':str(p),'clients':matched,'snapshot':s,'semanticCorrection':'controllingSeat0 means no external controller, not seat2; original failed expectation2 preserved, no repeat successful paid operations or natural return'})
    print('PASS original natural return recovered from frozen live snapshots',flush=True)

def match_history(match):
    path=pathlib.Path.home()/'AppData/LocalLow/DefaultCompany/MTGB/ForgeMatchHistory.jsonl'
    return [json.loads(x) for x in path.read_text(encoding='utf-8-sig').splitlines() if json.loads(x).get('matchId')==match]
def token_rejected(role):
    def check():
        lines=(R.d/(role+'-wire.jsonl')).read_text(encoding='utf-8-sig').splitlines()
        for line in reversed(lines):
            e=json.loads(line)
            if e['type']=='error':
                p=json.loads(e['payload'])
                if p.get('code')=='reconnect-rejected':return p
    return R.wait(check,15)
def grace_both():
    before=R.sync();pid=before['nativePid'];match=before['first']['matchId']
    R.cmd('client1','disconnect',hold=True);R.cmd('client2','disconnect',hold=True)
    disconnected=R.wait(lambda:s if len((s:=R.state('server')).get('members') or [])==2 and all(not m['connected'] for m in s['members']) else None)
    deadline=min(m['deadline'] for m in disconnected['members']);R.wait(lambda:s if (s:=R.state('server'))['nativePid']==0 and s['time']>=deadline else None,40)
    history=match_history(match);assert len(history)==1 and history[0]['winnerSeat']==0 and history[0]['series']['draws']==1,history
    R.cmd('client1','reconnect');rejection=token_rejected('client1');assert len(match_history(match))==1
    save(R.d/'both-disconnected-grace-draw-pass.json',{'status':'PASS','before':before,'disconnected':disconnected,'deadline':deadline,'after':R.state('server'),'history':history,'oldTokenRejected':rejection,'nativePid':pid})
    print('PASS both disconnected actual30s draw and old token rejected',flush=True)

def grace_one():
    advance(lambda s,r,v:s==1 and v['phase']=='MAIN1' and not v['stackItems']);initial=view();source=cast('Divination')
    island=next(c for c in initial['players'][0]['battlefield'] if c['name']=='Island' and not c['tapped']);R.choose('card:'+str(island['id']))
    before=R.sync();s,r=R.ready();assert 'PayMana' in r['kind'] and r['remainingManaCost'];match=before['first']['matchId'];pid=before['nativePid']
    old=M['read'](M['B']/'run-20261011T003120/actual-grace-winner2-pass.json')['before']['first']['matchId'];peer=R.state('client2')
    R.cmd('client2','raw',matchId=old,clientId=peer['nextClientId']+10,action={'type':44,'playerId':1,'sourceObjectId':r['requestId'],'optionId':0});time.sleep(.3)
    assert R.state('server')['first']['appliedSequence']==before['first']['appliedSequence'] and R.pending()[1]['requestId']==r['requestId']
    R.record('actual-old-session-match-id-rejected',{'oldMatchId':old,'currentMatchId':match,'nativeRequestId':r['requestId'],'before':before,'after':R.state('server')})
    R.cmd('client1','disconnect',hold=True)
    disconnected=R.wait(lambda:s if (s:=R.state('server')) and any(m['seat']==1 and not m['connected'] for m in s['members']) else None)
    deadline=next(m['deadline'] for m in disconnected['members'] if m['seat']==1)
    final=R.wait(lambda:c if (c:=R.state('client2')).get('ended',{}).get('winnerSeat')==2 else None,40)
    assert R.state('server')['time']>=deadline and R.state('server')['nativePid']==0
    history=match_history(match);assert len(history)==1 and history[0]['series']['player2Wins']==1
    client=R.state('client2');R.cmd('client2','raw',matchId=match,clientId=client['nextClientId']+10,action={'type':44,'playerId':1,'sourceObjectId':r['requestId'],'optionId':0})
    time.sleep(.5);assert len(match_history(match))==1 and R.state('server')['nativePid']==0
    R.cmd('client1','reconnect');rejection=token_rejected('client1');assert len(match_history(match))==1
    save(R.d/'partial-payment-grace-winner2-pass.json',{'status':'PASS','before':before,'disconnected':disconnected,'deadline':deadline,'final':final,'history':history,'oldTokenRejected':rejection,'lateOldActionNoNewSessionOrOutcome':True,'nativePid':pid,'source':source['id']})
    print('PASS partial payment actual30s winner2 and old token/action rejected',flush=True)

def stale_native():
    s,r=R.ready();assert s==1 and r['kind']=='InputPassPriority'
    records=[json.loads(line) for line in (R.d/'client1-wire.jsonl').read_text(encoding='utf-8-sig').splitlines()]
    old=[]
    for e in records:
        if e['type'] in ['start-match','state-update']:
            p=json.loads(e['payload']);v=(p.get('view') or {}).get('forge') or {};q=v.get('request') or {}
            if q.get('seat')==1 and q.get('requestId',0)<r['requestId'] and q.get('requestId',0)>0:old.append(q['requestId'])
    assert old,'Actual formerly valid operator request required';old_id=min(old);before=R.sync();client=R.state('client1');outer=client['nextClientId']+10
    R.cmd('client1','raw',matchId=client['matchId'],clientId=outer,action={'type':44,'playerId':1,'sourceObjectId':old_id,'optionId':0})
    R.wait(lambda:next(m for m in R.state('server')['members'] if m['seat']==1)['lastClientId']==outer)
    after=R.state('server');assert after['first']['viewHash']==before['first']['viewHash'] and R.pending()[1]['requestId']==r['requestId']
    R.choose('ok');checkpoint('actual-previously-valid-native-request-rejected',{'oldNativeId':old_id,'currentNativeId':r['requestId'],'freshClientId':outer,'before':before,'afterRejected':after})

def partial_cancel():
    initial=view();source=cast('Mindslaver');s,r=R.ready();assert 'PayMana' in r['kind']
    land=next(c for c in initial['players'][0]['battlefield'] if c['land'] and not c['tapped']);R.choose('card:'+str(land['id']))
    s,r=R.ready();assert 'PayMana' in r['kind'] and r['remainingManaCost']
    R.disconnect_restore(1,'partial-payment-cancel-restored');R.choose('cancel');after=view()
    assert [c['id'] for c in after['players'][0]['hand']]==[c['id'] for c in initial['players'][0]['hand']]
    assert [(c['id'],c['tapped']) for c in after['players'][0]['battlefield']]==[(c['id'],c['tapped']) for c in initial['players'][0]['battlefield']]
    assert after['players'][0]['manaPool']==initial['players'][0]['manaPool']
    checkpoint('partial-payment-cancel-refunds',{'source':source['id'],'paidLand':land['id'],'initial':initial})
    cast('Mindslaver');pay();settle();assert any(c['id']==source['id'] for c in view()['players'][0]['battlefield'])
    checkpoint('partial-payment-cancel-retry',{'source':source['id']})

def timing():
    s,r=R.ready();assert r['kind']=='InputPassPriority'
    role='client'+str(s);before=R.state('server');client=R.state(role);outer=client['nextClientId']+10
    option=next(i for i,o in enumerate(r['options']) if o['id']=='ok');seq=before['first']['appliedSequence']
    def raw(outerid,native,player=s,match=None):
        return R.cmd(role,'raw',matchId=match or client['matchId'],clientId=outerid,action={'type':44,'playerId':player,'sourceObjectId':native,'optionId':option})
    raw(outer,r['requestId']-1);R.wait(lambda:next(m for m in R.state('server')['members'] if m['seat']==s)['lastClientId']==outer)
    assert R.state('server')['first']['appliedSequence']==seq and R.pending()[1]['requestId']==r['requestId']
    raw(outer,r['requestId']);time.sleep(.4);assert R.pending()[1]['requestId']==r['requestId']
    raw(outer-1,r['requestId']);time.sleep(.4);assert R.pending()[1]['requestId']==r['requestId']
    raw(outer+1,r['requestId'],match='wrong-match');time.sleep(.4);assert R.pending()[1]['requestId']==r['requestId']
    raw(outer+2,r['requestId'],player=3-s)
    R.wait(lambda:(p:=R.pending()) and p[1]['requestId']!=r['requestId']);R.sync()
    after=R.state('server');raw(outer+2,r['requestId']);time.sleep(.4)
    assert R.state('server')['first']['appliedSequence']==after['first']['appliedSequence']
    checkpoint('old-duplicate-reordered-native-client-ids',{'before':before,'after':after,'outer':outer,'spoofSeatAuthenticated':s})
    timing_tail()

def timing_tail():
    for order in ['manual-first','auto-first']:
        s,r=R.ready();assert r['kind']=='InputPassPriority'
        before=R.state('server');result=R.cmd('client'+str(s),'race',nativeId=r['requestId'],optionId='ok',order=order)
        assert result['afterClientId']-result['beforeClientId']==1,result
        R.wait(lambda:(p:=R.pending()) and p[1]['requestId']!=r['requestId']);R.sync()
        checkpoint('manual-auto-race-'+order,{'before':before,'result':result})
    s,r=R.ready();assert r['kind']=='InputPassPriority';role='client'+str(s);before=R.state('server');start=time.monotonic()
    R.cmd(role,'drop-updates',drop=True);R.cmd(role,'choose',nativeId=r['requestId'],optionId='ok')
    R.wait(lambda:(p:=R.pending()) and p[1]['requestId']!=r['requestId']);assert R.state(role)['state']=='Submitting'
    R.wait(lambda:R.state(role)['state']=='Idle',20);elapsed=time.monotonic()-start;assert elapsed>=9
    R.cmd(role,'drop-updates',drop=False);R.sync()
    checkpoint('lost-ack-actual-timeout-resync',{'before':before,'elapsedSeconds':elapsed})

def inspect():print(json.dumps(brief(R.ready()),ensure_ascii=False))
def main():
    action=sys.argv[1]
    for script in [pathlib.Path(__file__),pathlib.Path(__file__).with_name('Run-NetworkBoundaries.py')]:
        (R.d/(action+'-'+script.name+'-'+str(time.time_ns()))).write_bytes(script.read_bytes())
    try:globals()[action]();save(R.d/(action+'-result.json'),{'status':'PASS_STEP','action':action})
    except Exception:
        save(R.d/(action+'-fail-'+str(time.time_ns())+'.json'),{'status':'FAIL','error':traceback.format_exc(),'snapshot':R.state('server')});raise

if __name__=='__main__':main()
